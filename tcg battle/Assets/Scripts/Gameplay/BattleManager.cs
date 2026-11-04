using System;
using System.Collections.Generic;
using UnityEngine;
using TcgBattle.Data;

namespace TcgBattle.Gameplay
{
    /// <summary>
    /// All battle state transitions. Pure logic: raises events for the UI and bot,
    /// validates every action, and never touches GameObjects.
    /// Support card effects (transcribed from the card artwork):
    ///   - Mbok Sumi  (bench, 50 HP) : while she sits on your bench, Pak Karta may
    ///                                 evolve into Babi Ngepet. Babi Ngepet then
    ///                                 evolves into Siluman Babi as a normal step.
    ///   - Tumbal Anak(hand) : one copy is consumed so Babi Ngepet's next attack
    ///                        always steals 1 energy (the coin flip is guaranteed).
    ///   - Kucing Liar(skillo) : when played, deals 40 damage to the opponent's
    ///                           bench Mbok Sumi (she has 50 HP, so one hit is not a KO).
    ///   - Punarma    (skillo) : when played (only if opposing active is Babi Ngepet),
    ///                           the next Warga attack deals +50 damage; it persists
    ///                           until that attack happens.
    /// </summary>
    public class BattleManager
    {
        public CardDatabase Database { get; }
        public PlayerState Player { get; } = new PlayerState { Side = Side.Player, DisplayName = "Kamu" };
        public PlayerState Bot { get; } = new PlayerState { Side = Side.Bot, DisplayName = "Bot" };

        public Phase Phase { get; private set; } = Phase.Setup;
        public bool PlayerStartsFirst { get; private set; }
        public string StatusMessage { get; private set; } = "";
        public bool LastBabiNgepetStealHeads { get; private set; }
        public bool LastBabiNgepetStealGuaranteed { get; private set; }

        // --- events consumed by the UI layer and bot controller ---
        public event Action<string> OnStatus;
        public event Action OnStateChanged;
        public event Action<CardInstance, Side> OnCardPlayedToBench;
        public event Action<CardInstance, Side> OnPromoted;      // bench/hand -> active
        public event Action<CardInstance, Side> OnRetreated;     // old active -> bench
        public event Action<CardInstance, Side, CardInstance, int, bool> OnAttack;
            // (attacker card, attacker side, defender card, final damage, wasSuperEffective)
        public event Action<CardInstance, Side> OnKnockedOut;
        public event Action<Side, int> OnScoreChanged;          // side, new points
        public event Action<CardInstance, Side> OnDrawn;
        public event Action<Side> OnEnergyChanged;
        public event Action<CardInstance, Side> OnSupportUsed;  // support consumed (one-shot or Tumbal Anak guarantee)
        public event Action<CardInstance, CardInstance, Side> OnEvolved; // old active, new active, side
        public event Action<CardInstance, Side> OnSupportDestroyed; // bench support reaching 0 HP
        public event Action<CardInstance, Side, int> OnSupportDamaged; // bench support, side, damage
        public event Action<CardInstance, Side, int> OnSelfDamaged;    // recoil, side, damage
        public event Action<CardInstance, Side, int> OnHealed;         // in-play heal, side, amount
        public event Action<bool> OnCoinFlipped;                // playerStartsFirst
        public event Action<Side> OnGameOver;                   // winner

        public BattleManager(CardDatabase database)
        {
            Database = database != null
                ? database
                : ScriptableObject.CreateInstance<CardDatabase>();
        }

        // ------------------------------------------------------------------ setup

        public void StartBattle()
        {
            SetupPlayer(Player);
            SetupPlayer(Bot);

            // Bot immediately chooses its starting active. Evolved cards may not be
            // placed there directly, so it must pick a legal basic battle card.
            Bot.Active = null;
            var botActive = FindFirstLegalActiveCardInList(Bot.Hand);
            if (botActive != null)
            {
                Bot.Hand.Remove(botActive);
                Bot.Active = botActive;
            }

            PlayerStartsFirst = UnityEngine.Random.value < 0.5f;
            Phase = Phase.Setup;
            SetStatus("Koin dilempar...");
            OnCoinFlipped?.Invoke(PlayerStartsFirst);
            OnStateChanged?.Invoke();
        }

        private void SetupPlayer(PlayerState p)
        {
            p.Reset();
            bool playerSide = p.Side == Side.Player;
            var deckList = playerSide ? Database.playerDeck : Database.botDeck;
            string deckError = CardDatabase.ValidateDeck(deckList, playerSide);
            if (deckError != null)
            {
                Debug.LogError($"[BattleManager] Invalid {(playerSide ? "player" : "bot")} deck: {deckError} " +
                               "Using the required 20-card list instead.");
                deckList = CardDatabase.CreateRequiredDeck(playerSide);
            }

            foreach (var entry in deckList)
            {
                var battle = Database.GetBattleCard(entry.cardId);
                var support = Database.GetSupportCard(entry.cardId);
                for (int i = 0; i < entry.copies; i++)
                {
                    if (battle != null)
                        p.Deck.Add(CardInstance.CreateBattle(battle));
                    else if (support != null)
                        p.Deck.Add(CardInstance.CreateSupport(support));
                }
            }
            Shuffle(p.Deck);
            for (int i = 0; i < BattleRules.StartingHandSize && p.Deck.Count > 0; i++)
                DrawInternal(p);

            // Simple mulligan: guarantee at least one card that may legally start in
            // the active slot (Warga Desa for the fire deck; any battle card for Dark).
            int attempts = 0;
            while (!HandHasLegalStartingCard(p) && attempts < 10)
            {
                attempts++;
                var all = new List<CardInstance>(p.Hand);
                p.Hand.Clear();
                p.Deck.AddRange(all);
                Shuffle(p.Deck);
                for (int i = 0; i < BattleRules.StartingHandSize && p.Deck.Count > 0; i++)
                    DrawInternal(p);
            }
            EnsureLegalStartingCard(p);
        }

        private static bool HandHasLegalStartingCard(PlayerState p)
        {
            foreach (var c in p.Hand)
                if (BattleRules.CanEnterActiveDirectly(c))
                    return true;
            return false;
        }

        /// <summary>
        /// Extremely rare fallback after the normal mulligans: swap a non-legal card
        /// in hand with a legal starter from the deck so setup can never deadlock.
        /// </summary>
        private static void EnsureLegalStartingCard(PlayerState p)
        {
            if (HandHasLegalStartingCard(p) || p.Hand.Count == 0 || p.Deck.Count == 0)
                return;

            int deckIndex = -1;
            for (int i = 0; i < p.Deck.Count; i++)
            {
                if (BattleRules.CanEnterActiveDirectly(p.Deck[i]))
                {
                    deckIndex = i;
                    break;
                }
            }
            if (deckIndex < 0)
                return;

            int handIndex = -1;
            for (int i = 0; i < p.Hand.Count; i++)
            {
                if (!BattleRules.CanEnterActiveDirectly(p.Hand[i]))
                {
                    handIndex = i;
                    break;
                }
            }
            if (handIndex < 0)
                handIndex = 0;

            var deckCard = p.Deck[deckIndex];
            var handCard = p.Hand[handIndex];
            p.Deck[deckIndex] = handCard;
            p.Hand[handIndex] = deckCard;
        }

        /// <summary>UI calls this after the player picked a starting active from hand.</summary>
        public void ConfirmStartingActive(CardInstance card)
        {
            if (!CanSelectStartingActive(card))
                return;
            Player.Hand.Remove(card);
            Player.Active = card;
            OnPromoted?.Invoke(card, Side.Player);
            OnStateChanged?.Invoke();
            SetStatus(PlayerStartsFirst
                ? $"{card.DisplayName} menjadi kartu aktifmu!"
                : $"{card.DisplayName} menjadi kartu aktifmu. Bot bermain...");
            if (PlayerStartsFirst)
                BeginPlayerTurn();
            else
                BeginBotTurn();
        }

        public bool CanSelectStartingActive(CardInstance card)
        {
            return Phase == Phase.Setup && Player.Active == null
                && BattleRules.CanEnterActiveDirectly(card) && Player.Hand.Contains(card);
        }

        // ------------------------------------------------------------------ turns

        private void BeginPlayerTurn()
        {
            Phase = Phase.PlayerTurn;
            Player.HasAttackedThisTurn = false;
            HealActiveAtTurnStart(Player);
            int energyBefore = Player.Energy;
            bool discarded;
            bool drew = DrawInternal(Player, out discarded);
            Player.Energy = Mathf.Min(BattleRules.EnergyCap,
                Player.Energy + BattleRules.EnergyPerTurn);
            OnTurnStartedAnnounce(Side.Player, Player.Energy - energyBefore, drew, discarded);
            OnStateChanged?.Invoke();
        }

        private void BeginBotTurn()
        {
            Phase = Phase.BotTurn;
            Bot.HasAttackedThisTurn = false;
            HealActiveAtTurnStart(Bot);
            int energyBefore = Bot.Energy;
            bool discarded;
            bool drew = DrawInternal(Bot, out discarded);
            Bot.Energy = Mathf.Min(BattleRules.EnergyCap,
                Bot.Energy + BattleRules.EnergyPerTurn);
            OnTurnStartedAnnounce(Side.Bot, Bot.Energy - energyBefore, drew, discarded);
            OnStateChanged?.Invoke();
        }

        private void OnTurnStartedAnnounce(Side side, int energyGained, bool drew, bool discarded)
        {
            OnTurnEvent?.Invoke(side);
            OnEnergyChanged?.Invoke(side);
            if (side == Side.Bot)
            {
                SetStatus("Giliran Bot...");
                return;
            }

            string energy = energyGained > 0
                ? $"+{energyGained} energi"
                : $"energi penuh ({BattleRules.EnergyCap})";
            string draw = drew
                ? "1 kartu diambil"
                : discarded ? "tangan penuh, kartu dibuang" : "dek habis";
            SetStatus($"Giliranmu! {energy}, {draw}.");
        }

        private event Action<Side> OnTurnEvent;

        public event Action<Side> OnTurnStarted
        {
            add => OnTurnEvent += value;
            remove => OnTurnEvent -= value;
        }

        public void EndPlayerTurn()
        {
            if (Phase != Phase.PlayerTurn)
                return;
            BeginBotTurn();
        }

        public void EndBotTurn()
        {
            if (Phase != Phase.BotTurn)
                return;
            BeginPlayerTurn();
        }

        // ------------------------------------------------------------------ player actions

        public bool CanPlayToBench(CardInstance card, out string error)
        {
            error = null;
            if (Phase != Phase.PlayerTurn)
            {
                error = "Bukan giliranmu.";
                return false;
            }
            if (card == null || !Player.Hand.Contains(card))
            {
                error = "Kartu tidak ada di tangan.";
                return false;
            }
            if (card.IsSupport && IsOneShotSupport(card))
            {
                // handled by CanUseSupport
                return CanUseOneShotSupport(card, out error);
            }
            if (card.IsSupport && card.Support.effect == Data.SupportEffect.TumbalAnakHandPassive)
            {
                error = "Tumbal Anak tetap di tangan dan dikorbankan saat Babi Ngepet menyerang.";
                return false;
            }
            if (BattleRules.IsEvolutionOnly(card))
            {
                // Advanced Warga cards never go to the bench; they only replace
                // their pre-evolution card in the active slot.
                if (CanEvolve(card, out error))
                    return true;
                return false;
            }
            if (Player.Bench.Count >= BattleRules.BenchLimit)
            {
                error = "Bangku sudah penuh.";
                return false;
            }
            return true;
        }

        public bool CanEvolve(CardInstance card, out string error)
        {
            return CanEvolveForSide(Player, card, out error);
        }

        private bool CanEvolveForSide(PlayerState p, CardInstance card, out string error)
        {
            error = null;
            if (Phase != GetPhaseForSide(p.Side))
            {
                error = "Bukan giliranmu.";
                return false;
            }
            if (card == null || !p.Hand.Contains(card))
            {
                error = "Kartu tidak ada di tangan.";
                return false;
            }
            if (!card.IsBattle || !BattleRules.IsEvolutionOnly(card))
            {
                error = "Kartu ini bukan kartu evolusi.";
                return false;
            }
            if (p.Active == null)
            {
                error = "Tidak ada kartu aktif untuk berevolusi.";
                return false;
            }
            if (p.Active.IsKnockedOut)
            {
                error = "Kartu aktif sudah pingsan.";
                return false;
            }
            if (card.CardId == BattleRules.BabiNgepetId && !BenchContainsMbokSumi(p))
            {
                error = "Mbok Sumi harus ada di bangku agar Pak Karta bisa berevolusi.";
                return false;
            }

            string source = BattleRules.EvolutionSource(card.CardId);
            if (p.Active.CardId != source)
            {
                var sourceDef = Database.GetBattleCard(source);
                string sourceName = sourceDef != null ? sourceDef.displayName : source;
                error = $"{card.DisplayName} hanya bisa berevolusi dari {sourceName}.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Evolve the active card using the matching card from hand. The hand card
        /// becomes the active instance; the old active leaves play. Damage already
        /// taken carries over against the evolved card's printed maximum. No extra
        /// energy is charged (the requirement sheet only assigns energy to attacks/retreats).
        /// </summary>
        public bool Evolve(CardInstance card)
        {
            if (!CanEvolve(card, out var error))
            {
                if (error != null)
                    SetStatus(error);
                return false;
            }
            return EvolveInternal(Player, card);
        }

        private bool EvolveInternal(PlayerState p, CardInstance card)
        {
            var oldActive = p.Active;
            int carriedHp = BattleRules.EvolutionHpAfterDamage(
                oldActive.MaxHp, oldActive.CurrentHp, card.MaxHp);

            // The current Warga line always grows in max HP, so this guard is
            // unreachable with the shipped cards. It keeps future/edited data safe:
            // never leave an active card at 0 HP without running the KO path.
            if (carriedHp <= 0)
            {
                SetStatus($"{card.DisplayName} tidak bisa berevolusi: HP hasil evolusi habis.");
                return false;
            }

            p.Hand.Remove(card);
            card.CurrentHp = carriedHp;
            p.Active = card;

            // The form change is already authoritative here. The UI captures the old
            // visuals synchronously, then plays the cosmetic reveal after the refresh.
            OnEvolved?.Invoke(oldActive, card, p.Side);
            OnPromoted?.Invoke(card, p.Side);
            SetStatus(p.Side == Side.Player
                ? $"{oldActive.DisplayName} berevolusi menjadi {card.DisplayName}! ({card.CurrentHp}/{card.MaxHp} HP)"
                : $"Bot berevolusi: {oldActive.DisplayName} menjadi {card.DisplayName}! ({card.CurrentHp}/{card.MaxHp} HP)");
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Legacy transform entry point. Pak Karta now evolves only into Babi Ngepet;
        /// Babi Ngepet then evolves into Siluman Babi. Kept so older callers fail
        /// cleanly instead of retaining a direct form-selection action.
        /// </summary>
        public bool CanTransformPakKarta(out string error)
        {
            error = "Pak Karta berevolusi menjadi Babi Ngepet. Mainkan kartu Babi Ngepet dari tangan.";
            return false;
        }

        /// <summary>Legacy transform entry point; Pak Karta uses the evolution path now.</summary>
        public bool TransformPakKarta(string formId)
        {
            SetStatus("Pak Karta berevolusi menjadi Babi Ngepet. Mainkan kartu Babi Ngepet dari tangan.");
            return false;
        }

        /// <summary>
        /// Playing a card from hand: battle cards and Mbok Sumi go to the bench;
        /// Tumbal Anak stays in hand and is consumed when Babi Ngepet attacks;
        /// one-shot supports take effect and leave play. Pasukan Obor / Amukan Warga
        /// instead evolve the matching active card.
        /// </summary>
        public bool PlayCardFromHand(CardInstance card)
        {
            if (Phase != Phase.PlayerTurn || card == null || !Player.Hand.Contains(card))
                return false;

            if (BattleRules.IsEvolutionOnly(card))
                return Evolve(card);

            if (card.IsSupport && IsOneShotSupport(card))
            {
                if (!CanUseOneShotSupport(card, out var why))
                {
                    SetStatus(why);
                    return false;
                }
                Player.Hand.Remove(card);
                ApplyOneShotSupport(Player, Bot, card);
                OnSupportUsed?.Invoke(card, Side.Player);
                OnStateChanged?.Invoke();
                return true;
            }

            if (card.IsSupport && card.Support.effect == Data.SupportEffect.TumbalAnakHandPassive)
            {
                SetStatus("Tumbal Anak tetap di tangan dan dikorbankan saat Babi Ngepet menyerang.");
                return false;
            }

            if (Player.Bench.Count >= BattleRules.BenchLimit)
            {
                SetStatus("Bangku sudah penuh.");
                return false;
            }
            Player.Hand.Remove(card);
            Player.Bench.Add(card);
            OnCardPlayedToBench?.Invoke(card, Side.Player);
            SetStatus(card.IsBattle
                ? $"{card.DisplayName} diletakkan di bangku."
                : $"{card.DisplayName} berjaga di bangku.");
            OnStateChanged?.Invoke();
            return true;
        }

        public bool CanRetreat(out string error)
        {
            error = null;
            if (Phase != Phase.PlayerTurn)
            {
                error = "Bukan giliranmu.";
                return false;
            }
            if (Player.Active == null)
            {
                error = "Tidak ada kartu aktif.";
                return false;
            }
            if (Player.Bench.Count == 0)
            {
                error = "Bangku kosong.";
                return false;
            }
            if (!BenchHasLegalActiveCard(Player))
            {
                error = "Tidak ada kartu bangku yang bisa menjadi aktif.";
                return false;
            }
            if (Player.Energy < Player.Active.RetreatCost)
            {
                error = $"Butuh {Player.Active.RetreatCost} energi untuk mundur.";
                return false;
            }
            return true;
        }

        private static bool BenchHasLegalActiveCard(PlayerState p)
        {
            foreach (var c in p.Bench)
                if (BattleRules.CanEnterActiveDirectly(c))
                    return true;
            return false;
        }

        public bool Retreat()
        {
            if (!CanRetreat(out var error))
            {
                if (error != null)
                    SetStatus(error);
                return false;
            }
            var swap = FindFirstLegalActiveCardInList(Player.Bench);
            if (swap == null)
                return false;
            Player.Energy -= Player.Active.RetreatCost;
            var oldActive = Player.Active;
            Player.Bench.Remove(swap);
            Player.Active = swap;
            Player.Bench.Add(oldActive);
            OnPromoted?.Invoke(Player.Active, Side.Player);
            OnRetreated?.Invoke(oldActive, Side.Player);
            OnEnergyChanged?.Invoke(Side.Player);
            SetStatus($"{oldActive.DisplayName} mundur. {Player.Active.DisplayName} kini aktif.");
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>Retreat to a specific bench card (player clicks that bench card).</summary>
        public bool RetreatTo(CardInstance target)
        {
            if (Phase != Phase.PlayerTurn || target == null || !Player.Bench.Contains(target))
                return false;
            if (!BattleRules.CanEnterActiveDirectly(target))
            {
                SetStatus(BattleRules.IsEvolutionOnly(target)
                    ? $"{target.DisplayName} hanya bisa masuk posisi aktif lewat evolusi."
                    : "Kartu pasif tidak bisa menjadi kartu aktif.");
                return false;
            }
            if (!CanRetreat(out var error))
            {
                if (error != null)
                    SetStatus(error);
                return false;
            }
            Player.Energy -= Player.Active.RetreatCost;
            var oldActive = Player.Active;
            Player.Bench.Remove(target);
            Player.Active = target;
            Player.Bench.Add(oldActive);
            OnPromoted?.Invoke(target, Side.Player);
            OnRetreated?.Invoke(oldActive, Side.Player);
            OnEnergyChanged?.Invoke(Side.Player);
            SetStatus($"{target.DisplayName} kini aktif.");
            OnStateChanged?.Invoke();
            return true;
        }

        public bool CanAttack(out string error)
        {
            error = null;
            if (Phase != Phase.PlayerTurn)
            {
                error = "Bukan giliranmu.";
                return false;
            }
            if (Player.Active == null)
            {
                error = "Tidak ada kartu aktif.";
                return false;
            }
            if (Player.HasAttackedThisTurn)
            {
                error = "Sudah menyerang giliran ini.";
                return false;
            }
            if (Player.Energy < Player.Active.AttackCost)
            {
                error = $"Butuh {Player.Active.AttackCost} energi untuk menyerang.";
                return false;
            }
            if (Bot.Active == null)
            {
                error = "Lawan tidak punya kartu aktif.";
                return false;
            }
            return true;
        }

        public bool Attack()
        {
            if (!CanAttack(out var error))
            {
                if (error != null)
                    SetStatus(error);
                return false;
            }
            Player.Energy -= Player.Active.AttackCost;
            Player.HasAttackedThisTurn = true;
            ResolveAttack(Player, Bot);
            OnEnergyChanged?.Invoke(Side.Player);
            return true;
        }

        // ------------------------------------------------------------------ bot hooks

        public bool BotPlayToBench(CardInstance card)
        {
            if (BattleRules.IsEvolutionOnly(card))
                return false;
            return PlayCardInternal(Bot, Player, card);
        }

        public bool BotEvolve(CardInstance card)
        {
            if (!CanEvolveForSide(Bot, card, out _))
                return false;
            return EvolveInternal(Bot, card);
        }

        public bool BotRetreat()
        {
            if (Phase != Phase.BotTurn || Bot.Active == null || Bot.Bench.Count == 0)
                return false;
            var swap = FindFirstLegalActiveCardInList(Bot.Bench);
            if (swap == null)
                return false;
            if (Bot.Energy < Bot.Active.RetreatCost)
                return false;
            Bot.Energy -= Bot.Active.RetreatCost;
            var oldActive = Bot.Active;
            Bot.Bench.Remove(swap);
            Bot.Active = swap;
            Bot.Bench.Add(oldActive);
            OnPromoted?.Invoke(Bot.Active, Side.Bot);
            OnRetreated?.Invoke(oldActive, Side.Bot);
            OnEnergyChanged?.Invoke(Side.Bot);
            SetStatus($"Bot mengundurkan {oldActive.DisplayName}.");
            OnStateChanged?.Invoke();
            return true;
        }

        public bool BotAttack()
        {
            if (Phase != Phase.BotTurn || Bot.Active == null || Bot.HasAttackedThisTurn)
                return false;
            if (Bot.Energy < Bot.Active.AttackCost || Player.Active == null)
                return false;
            Bot.Energy -= Bot.Active.AttackCost;
            Bot.HasAttackedThisTurn = true;
            ResolveAttack(Bot, Player);
            OnEnergyChanged?.Invoke(Side.Bot);
            return true;
        }

        public bool CanBotAttack()
        {
            if (Phase != Phase.BotTurn || Bot.Active == null || Bot.HasAttackedThisTurn)
                return false;
            return Bot.Energy >= Bot.Active.AttackCost && Player.Active != null;
        }

        public bool CanBotRetreat()
        {
            if (Phase != Phase.BotTurn || Bot.Active == null || Bot.Bench.Count == 0)
                return false;
            if (!BenchHasLegalActiveCard(Bot))
                return false;
            return Bot.Energy >= Bot.Active.RetreatCost;
        }

        public void BotEndTurn()
        {
            if (Phase == Phase.BotTurn)
                EndBotTurn();
        }

        private bool PlayCardInternal(PlayerState p, PlayerState opponent, CardInstance card)
        {
            if (Phase != GetPhaseForSide(p.Side) || card == null || !p.Hand.Contains(card))
                return false;

            if (card.IsSupport && IsOneShotSupport(card))
            {
                if (!CanUseOneShotSupportForSide(p, p == Bot ? Player : Bot, card, out var why))
                    return false;
                p.Hand.Remove(card);
                ApplyOneShotSupport(p, p == Bot ? Player : Bot, card);
                OnSupportUsed?.Invoke(card, p.Side);
                OnStateChanged?.Invoke();
                return true;
            }

            if (card.IsSupport && card.Support.effect == Data.SupportEffect.TumbalAnakHandPassive)
                return false;
            if (BattleRules.IsEvolutionOnly(card))
                return false;
            if (p.Bench.Count >= BattleRules.BenchLimit)
                return false;
            p.Hand.Remove(card);
            p.Bench.Add(card);
            OnCardPlayedToBench?.Invoke(card, p.Side);
            SetStatus(p.Side == Side.Bot ? $"Bot meletakkan {card.DisplayName} di bangku." : $"{card.DisplayName} diletakkan di bangku.");
            OnStateChanged?.Invoke();
            return true;
        }

        private Phase GetPhaseForSide(Side side)
        {
            return side == Side.Player ? Phase.PlayerTurn : Phase.BotTurn;
        }

        // ------------------------------------------------------------------ attack resolution

        private void ResolveAttack(PlayerState attacker, PlayerState defender)
        {
            if (attacker.Active == null || defender.Active == null)
                return;
            var atkCard = attacker.Active;
            var defCard = defender.Active;

            int bonus = attacker.NextAttackBonus;
            attacker.NextAttackBonus = 0;

            int damage = BattleRules.ComputeDamage(atkCard, defCard, bonus);
            bool effective = BattleRules.IsSuperEffective(atkCard.Element, defCard.Element);

            // The UI captures the pre-damage HP from this event, so it must fire before
            // the defender's HP is mutated.
            OnAttack?.Invoke(atkCard, attacker.Side, defCard, damage, effective);

            defCard.CurrentHp = Mathf.Max(0, defCard.CurrentHp - damage);
            string effectNote = ApplyAttackEffects(attacker, defender, atkCard);

            if (defCard.IsKnockedOut)
            {
                attacker.Points++;
                OnKnockedOut?.Invoke(defCard, defender.Side);
                OnScoreChanged?.Invoke(attacker.Side, attacker.Points);
                if (attacker.Points >= BattleRules.PointsToWin)
                {
                    EndGame(attacker.Side);
                    OnStateChanged?.Invoke();
                    return;
                }
                PromoteAfterKnockout(defender);
                if (Phase == Phase.GameOver)
                    return;
            }
            else
            {
                string hit = effective
                    ? $"{atkCard.DisplayName} mendarat {damage} damage (2x)!"
                    : $"{atkCard.DisplayName} mendarat {damage} damage.";
                SetStatus(string.IsNullOrEmpty(effectNote) ? hit : $"{hit} {effectNote}");
            }

            // Siluman Babi's recoil is part of its attack. Resolve it after the
            // defender's KO/promotion so the attack animation always has both slots.
            if (atkCard.CardId == BattleRules.SilumanBabiId &&
                ReferenceEquals(attacker.Active, atkCard))
            {
                int recoil = BattleRules.SilumanBabiRecoil;
                atkCard.CurrentHp = Mathf.Max(0, atkCard.CurrentHp - recoil);
                OnSelfDamaged?.Invoke(atkCard, attacker.Side, recoil);
                if (atkCard.IsKnockedOut)
                {
                    defender.Points++;
                    OnKnockedOut?.Invoke(atkCard, attacker.Side);
                    OnScoreChanged?.Invoke(defender.Side, defender.Points);
                    if (defender.Points >= BattleRules.PointsToWin)
                    {
                        EndGame(defender.Side);
                        OnStateChanged?.Invoke();
                        return;
                    }
                    PromoteAfterKnockout(attacker);
                    if (Phase == Phase.GameOver)
                        return;
                }
                else
                {
                    SetStatus($"{atkCard.DisplayName} menerima {recoil} damage dari serangannya sendiri.");
                }
            }

            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Secondary effects printed on the attacking card. They resolve after the
        /// primary damage. The returned note is appended to the normal hit message.
        /// </summary>
        private string ApplyAttackEffects(PlayerState attacker, PlayerState defender, CardInstance atkCard)
        {
            switch (atkCard.CardId)
            {
                case "PK":
                    bool discarded;
                    bool drew = DrawInternal(attacker, out discarded);
                    if (drew)
                        return "Pak Karta mengambil 1 kartu dari draw pile.";
                    return discarded ? "Tangan penuh: kartu yang diambil dibuang." : "Draw pile kosong.";

                case "PO":
                    if (attacker.Energy >= BattleRules.EnergyCap)
                        return "Energi sudah penuh.";
                    attacker.Energy = Mathf.Min(BattleRules.EnergyCap,
                        attacker.Energy + BattleRules.PasukanOborEnergyGain);
                    OnEnergyChanged?.Invoke(attacker.Side);
                    return "Pasukan Obor mendapatkan 1 energi.";

                case "BN":
                    return ResolveBabiNgepetSteal(attacker, defender);

                case "AW":
                    var benchNote = DamageBenchSupport(defender, BattleRules.MbokSumiId,
                        BattleRules.AmukanWargaBenchDamage);
                    return benchNote != null ? "Balas Dendam juga " + benchNote : null;

                default:
                    return null;
            }
        }

        private string ResolveBabiNgepetSteal(PlayerState attacker, PlayerState defender)
        {
            LastBabiNgepetStealHeads = false;
            LastBabiNgepetStealGuaranteed = false;

            // Tumbal Anak: one copy is consumed so this attack's coin flip is
            // guaranteed to steal energy. It is the owner's hand passive.
            var tumbal = FindHandSupport(attacker, Data.SupportEffect.TumbalAnakHandPassive);
            if (tumbal != null)
            {
                attacker.Hand.Remove(tumbal);
                OnSupportUsed?.Invoke(tumbal, attacker.Side);
                LastBabiNgepetStealGuaranteed = true;
            }

            bool heads = LastBabiNgepetStealGuaranteed || UnityEngine.Random.value < 0.5f;
            LastBabiNgepetStealHeads = heads;
            if (!heads)
                return "Lempar koin: angka. Energi lawan tidak dicuri.";
            if (defender.Energy <= 0)
                return "Lempar koin: gambar, tetapi lawan tidak punya energi.";

            defender.Energy = Mathf.Max(0, defender.Energy - BattleRules.BabiNgepetEnergySteal);
            if (attacker.Energy < BattleRules.EnergyCap)
                attacker.Energy++;
            OnEnergyChanged?.Invoke(defender.Side);
            OnEnergyChanged?.Invoke(attacker.Side);
            return LastBabiNgepetStealGuaranteed
                ? "Tumbal Anak: 1 energi lawan pasti dicuri!"
                : "Lempar koin: gambar! 1 energi dicuri.";
        }

        /// <summary>
        /// Damage a named support card in a bench. Mbok Sumi is the only support with
        /// printed HP today. A support that reaches 0 leaves play and scores no point.
        /// </summary>
        private string DamageBenchSupport(PlayerState owner, string cardId, int damage)
        {
            var target = FindBenchSupport(owner, cardId);
            if (target == null)
                return null;

            target.CurrentHp = Mathf.Max(0, target.CurrentHp - damage);
            OnSupportDamaged?.Invoke(target, owner.Side, damage);
            if (target.IsKnockedOut)
            {
                owner.Bench.Remove(target);
                OnSupportDestroyed?.Invoke(target, owner.Side);
                return $"{target.DisplayName} di bangku lawan pingsan!";
            }
            return $"{target.DisplayName} di bangku lawan menerima {damage} damage " +
                   $"({target.CurrentHp}/{target.MaxHp} HP).";
        }

        private void PromoteAfterKnockout(PlayerState p)
        {
            var next = FindPromotableBattleCard(p);
            if (next != null)
            {
                PromoteToActive(p, next);
                SetStatus($"{p.DisplayName}: kartu aktif pingsan! {next.DisplayName} maju.");
            }
            else
            {
                SetStatus($"{p.DisplayName} kehabisan kartu!");
                EndGame(p.Side == Side.Player ? Side.Bot : Side.Player);
            }
            OnStateChanged?.Invoke();
        }

        private CardInstance FindPromotableBattleCard(PlayerState p)
        {
            foreach (var c in p.Bench)
                if (BattleRules.CanEnterActiveDirectly(c))
                    return c;
            if (p.Active == null)
                foreach (var c in p.Hand)
                    if (BattleRules.CanEnterActiveDirectly(c))
                        return c;
            return null;
        }

        private void PromoteToActive(PlayerState p, CardInstance card)
        {
            if (p.Bench.Contains(card))
                p.Bench.Remove(card);
            else
                p.Hand.Remove(card);
            p.Active = card;
            OnPromoted?.Invoke(card, p.Side);
        }

        // ------------------------------------------------------------------ support card rules

        public static bool IsOneShotSupport(CardInstance card)
        {
            return card.IsSupport && (card.Support.effect == Data.SupportEffect.KucingLiarSingleShot
                                      || card.Support.effect == Data.SupportEffect.PunarmaJumatLegiSingleShot);
        }

        public bool CanUseOneShotSupport(CardInstance card, out string why)
        {
            return CanUseOneShotSupportForSide(Player, Bot, card, out why);
        }

        private bool CanUseOneShotSupportForSide(PlayerState p, PlayerState opponent, CardInstance card, out string why)
        {
            why = null;
            if (!card.IsSupport || !IsOneShotSupport(card))
            {
                why = "Bukan kartu sekali pakai.";
                return false;
            }
            if (card.Support.effect == Data.SupportEffect.PunarmaJumatLegiSingleShot
                && (opponent.Active == null || opponent.Active.CardId != "BN"))
            {
                why = "Punarma hanya bisa dipakai jika Babi Ngepet aktif di sisi lawan.";
                return false;
            }
            if (card.Support.effect == Data.SupportEffect.KucingLiarSingleShot
                && !BenchContainsMbokSumi(opponent))
            {
                why = "Kucing Liar hanya bisa dipakai jika Mbok Sumi ada di bangku lawan.";
                return false;
            }
            return true;
        }

        private void ApplyOneShotSupport(PlayerState p, PlayerState opponent, CardInstance card)
        {
            if (card.Support.effect == Data.SupportEffect.KucingLiarSingleShot)
            {
                string note = DamageBenchSupport(opponent, BattleRules.MbokSumiId, card.Support.effectValue);
                if (note != null)
                    SetStatus($"Kucing Liar menyerang Mbok Sumi di bangku lawan! {note}");
            }
            else if (card.Support.effect == Data.SupportEffect.PunarmaJumatLegiSingleShot)
            {
                p.NextAttackBonus += card.Support.effectValue;
                SetStatus($"Serangan berikutnya +{card.Support.effectValue} damage!");
            }
        }

        public static bool BenchContainsMbokSumi(PlayerState p)
        {
            return FindBenchSupport(p, BattleRules.MbokSumiId) != null;
        }

        public static bool HandContainsSupport(PlayerState p, Data.SupportEffect effect)
        {
            return FindHandSupport(p, effect) != null;
        }

        private static CardInstance FindHandSupport(PlayerState p, Data.SupportEffect effect)
        {
            foreach (var c in p.Hand)
                if (c.IsSupport && c.Support.effect == effect)
                    return c;
            return null;
        }

        private static CardInstance FindBenchSupport(PlayerState p, string cardId)
        {
            foreach (var c in p.Bench)
                if (c.IsSupport && c.CardId == cardId && !c.IsKnockedOut)
                    return c;
            return null;
        }

        /// <summary>
        /// Amukan Warga's printed passive: heal 20 HP at the start of its owner's turn.
        /// It only applies while Amukan Warga is the active card.
        /// </summary>
        private void HealActiveAtTurnStart(PlayerState p)
        {
            var active = p.Active;
            if (active == null || !active.IsBattle ||
                active.CardId != BattleRules.WargaEvolution2Id)
                return;
            if (active.IsKnockedOut || active.CurrentHp >= active.MaxHp)
                return;

            int healed = Mathf.Min(BattleRules.AmukanWargaHealPerTurn,
                active.MaxHp - active.CurrentHp);
            active.CurrentHp += healed;
            OnHealed?.Invoke(active, p.Side, healed);
        }

        // ------------------------------------------------------------------ helpers

        private static CardInstance FindFirstLegalActiveCardInList(List<CardInstance> list)
        {
            foreach (var c in list)
                if (BattleRules.CanEnterActiveDirectly(c))
                    return c;
            return null;
        }

        private static void Shuffle(List<CardInstance> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private bool DrawInternal(PlayerState p)
        {
            bool discarded;
            return DrawInternal(p, out discarded);
        }

        /// <summary>
        /// Draw the top card. A full ten-card hand still consumes the deck card, but the
        /// new card is discarded (no hand entry, no draw event) and the excess is lost.
        /// </summary>
        private bool DrawInternal(PlayerState p, out bool discarded)
        {
            discarded = false;
            if (p.Deck.Count == 0)
                return false;
            var card = p.Deck[p.Deck.Count - 1];
            p.Deck.RemoveAt(p.Deck.Count - 1);
            if (p.Hand.Count >= BattleRules.HandLimit)
            {
                discarded = true;
                return false;
            }
            p.Hand.Add(card);
            OnDrawn?.Invoke(card, p.Side);
            return true;
        }

        private void EndGame(Side winner)
        {
            Phase = Phase.GameOver;
            SetStatus(winner == Side.Player ? "KAMU MENANG!" : "BOT MENANG!");
            OnGameOver?.Invoke(winner);
        }

        private void SetStatus(string message)
        {
            StatusMessage = message;
            OnStatus?.Invoke(message);
        }

        /// <summary>UI hook: post a helper message without changing battle state.</summary>
        public void PostLocalStatus(string message)
        {
            SetStatus(message);
        }
    }
}
