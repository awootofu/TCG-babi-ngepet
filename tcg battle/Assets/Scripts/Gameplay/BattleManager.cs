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
    ///   - Mbok Sumi  (bench) : while she sits on your bench, your retreats are free.
    ///   - Tumbal Anak(hand) : one copy is consumed to nullify a Fire attack on your Babi Ngepet.
    ///   - Kucing Liar(skillo) : when played, deals 40 damage to the opponent's bench Mbok Sumi.
    ///   - Punarma    (skillo) : when played (only if opposing active is Babi Ngepet),
    ///                           your next attack this turn deals +50 damage.
    /// </summary>
    public class BattleManager
    {
        public CardDatabase Database { get; }
        public PlayerState Player { get; } = new PlayerState { Side = Side.Player, DisplayName = "Kamu" };
        public PlayerState Bot { get; } = new PlayerState { Side = Side.Bot, DisplayName = "Bot" };

        public Phase Phase { get; private set; } = Phase.Setup;
        public bool PlayerStartsFirst { get; private set; }
        public string StatusMessage { get; private set; } = "";

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
        public event Action<CardInstance, Side> OnSupportUsed;  // support consumed (one-shot or Tumbal Anak shield)
        public event Action<CardInstance, Side> OnTransformed;  // Pak Karta -> BN / SB
        public event Action<CardInstance, Side> OnSupportDestroyed; // Kucing Liar killing bench MS
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
            Player.UsedFreeRetreatViaMbokSumi = false;
            Player.NextAttackBonus = 0;
            DrawInternal(Player);
            Player.Energy += BattleRules.EnergyPerTurn;
            OnTurnStartedAnnounce(Side.Player, Player);
            OnStateChanged?.Invoke();
        }

        private void BeginBotTurn()
        {
            Phase = Phase.BotTurn;
            Bot.HasAttackedThisTurn = false;
            Bot.UsedFreeRetreatViaMbokSumi = false;
            Bot.NextAttackBonus = 0;
            DrawInternal(Bot);
            Bot.Energy += BattleRules.EnergyPerTurn;
            OnTurnStartedAnnounce(Side.Bot, Bot);
            OnStateChanged?.Invoke();
        }

        private void OnTurnStartedAnnounce(Side side, PlayerState p)
        {
            OnTurnEvent?.Invoke(side);
            OnEnergyChanged?.Invoke(side);
            SetStatus(side == Side.Player
                ? $"Giliranmu! +{BattleRules.EnergyPerTurn} energi, 1 kartu diambil."
                : "Giliran Bot...");
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
            Player.NextAttackBonus = 0;
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
                error = "Tumbal Anak tetap di tangan dan dikorbankan saat melindungi Babi Ngepet.";
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

            string source = BattleRules.EvolutionSource(card.CardId);
            if (p.Active.CardId != source)
            {
                string sourceName = source == BattleRules.WargaBasicId
                    ? "Warga Desa"
                    : "Pasukan Obor";
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
            OnPromoted?.Invoke(card, p.Side);
            SetStatus(p.Side == Side.Player
                ? $"{oldActive.DisplayName} berevolusi menjadi {card.DisplayName}! ({card.CurrentHp}/{card.MaxHp} HP)"
                : $"Bot berevolusi: {oldActive.DisplayName} menjadi {card.DisplayName}! ({card.CurrentHp}/{card.MaxHp} HP)");
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Pak Karta may transform for free while he is the player's active card and
        /// Mbok Sumi is on the player's bench. The transformation does not consume
        /// Mbok Sumi, energy, the turn, or the once-per-turn attack.
        /// </summary>
        public bool CanTransformPakKarta(out string error)
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
            if (Player.Active.CardId != BattleRules.PakKartaId)
            {
                error = "Hanya Pak Karta yang bisa bertransformasi.";
                return false;
            }
            if (Player.Active.IsKnockedOut)
            {
                error = "Kartu aktif sudah pingsan.";
                return false;
            }
            if (!BenchContainsMbokSumi(Player))
            {
                error = "Mbok Sumi harus ada di bangku untuk bertransformasi.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Replace the active Pak Karta instance in place with Babi Ngepet or
        /// Siluman Babi. The same CardInstance stays in the active slot, so every
        /// existing reference (UI, score, board) agrees on the transformed card.
        /// Transformation starts the new form at its printed max HP; the assignment
        /// only defines damage carry-over for Warga evolution.
        /// </summary>
        public bool TransformPakKarta(string formId)
        {
            if (!CanTransformPakKarta(out var error))
            {
                if (error != null)
                    SetStatus(error);
                return false;
            }
            if (!BattleRules.IsPakKartaTransformForm(formId))
            {
                SetStatus("Pilih Babi Ngepet atau Siluman Babi.");
                return false;
            }

            var def = Database.GetBattleCard(formId);
            if (def == null)
            {
                SetStatus("Data wujud transformasi tidak ditemukan.");
                return false;
            }

            var active = Player.Active;
            string oldName = active.DisplayName;
            active.CardId = def.id;
            active.Battle = def;
            active.Support = null;
            active.CurrentHp = def.maxHp;
            active.FaceDown = false;

            SetStatus($"{oldName} berubah menjadi {def.displayName}!");
            OnTransformed?.Invoke(active, Side.Player);
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Playing a card from hand: battle cards and Mbok Sumi go to the bench;
        /// Tumbal Anak stays in hand as a shield and is consumed when it protects
        /// Babi Ngepet; one-shot supports take effect and leave play. Pasukan Obor /
        /// Amukan Warga instead evolve the matching active card.
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
                SetStatus("Tumbal Anak tetap di tangan dan dikorbankan saat melindungi Babi Ngepet.");
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
            if (!HasFreeRetreat(Player) && Player.Energy < Player.Active.RetreatCost)
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
            if (!HasFreeRetreat(Player))
                Player.Energy -= Player.Active.RetreatCost;
            else
                Player.UsedFreeRetreatViaMbokSumi = true;
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
            if (!HasFreeRetreat(Player))
                Player.Energy -= Player.Active.RetreatCost;
            else
                Player.UsedFreeRetreatViaMbokSumi = true;
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
            if (!HasFreeRetreat(Bot) && Bot.Energy < Bot.Active.RetreatCost)
                return false;
            if (!HasFreeRetreat(Bot))
                Bot.Energy -= Bot.Active.RetreatCost;
            else
                Bot.UsedFreeRetreatViaMbokSumi = true;
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
            return HasFreeRetreat(Bot) || Bot.Energy >= Bot.Active.RetreatCost;
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

            // Tumbal Anak hand-passive: one copy is consumed to shield Babi Ngepet
            // from a Fire attack. Additional copies remain in hand for later attacks.
            var shieldCard = atkCard.Element == Data.Element.Fire && defCard.CardId == "BN"
                ? FindHandSupport(defender, Data.SupportEffect.TumbalAnakHandPassive)
                : null;
            bool shielded = shieldCard != null;
            if (shielded)
            {
                defender.Hand.Remove(shieldCard);
                OnSupportUsed?.Invoke(shieldCard, defender.Side);
            }

            int bonus = attacker.NextAttackBonus;
            attacker.NextAttackBonus = 0;

            int damage = shielded ? 0 : BattleRules.ComputeDamage(atkCard, defCard, bonus);
            bool effective = BattleRules.IsSuperEffective(atkCard.Element, defCard.Element);

            OnAttack?.Invoke(atkCard, attacker.Side, defCard, damage, effective);
            if (shielded)
            {
                SetStatus($"{shieldCard.DisplayName} dikorbankan untuk menangkal serangan {atkCard.DisplayName}!");
            }
            else
            {
                defCard.CurrentHp = Mathf.Max(0, defCard.CurrentHp - damage);

                // Babi Ngepet's signature: steals one energy on hit.
                if (atkCard.CardId == "BN" && damage > 0)
                {
                    defender.Energy = Mathf.Max(0, defender.Energy - 1);
                    if (attacker.Energy < BattleRules.EnergyCap)
                        attacker.Energy++;
                    OnEnergyChanged?.Invoke(defender.Side);
                    OnEnergyChanged?.Invoke(attacker.Side);
                    SetStatus($"{atkCard.DisplayName} mencuri 1 energi!");
                }
            }

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
            }
            else if (!shielded)
            {
                SetStatus(effective
                    ? $"{atkCard.DisplayName} mendarat {damage} damage (2x)!"
                    : $"{atkCard.DisplayName} mendarat {damage} damage.");
            }
            OnStateChanged?.Invoke();
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
                for (int i = opponent.Bench.Count - 1; i >= 0; i--)
                {
                    var b = opponent.Bench[i];
                    if (b.IsSupport && b.CardId == "MS")
                    {
                        opponent.Bench.RemoveAt(i);
                        OnSupportDestroyed?.Invoke(b, opponent.Side);
                        SetStatus($"Kucing Liar menyerang Mbok Sumi di bangku lawan! ({card.Support.effectValue} damage)");
                        return;
                    }
                }
            }
            else if (card.Support.effect == Data.SupportEffect.PunarmaJumatLegiSingleShot)
            {
                p.NextAttackBonus += card.Support.effectValue;
                SetStatus($"Serangan berikutnya +{card.Support.effectValue} damage!");
            }
        }

        public static bool BenchContainsMbokSumi(PlayerState p)
        {
            foreach (var c in p.Bench)
                if (c.IsSupport && c.CardId == "MS")
                    return true;
            return false;
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

        private static bool HasFreeRetreat(PlayerState p)
        {
            // Mbok Sumi's passive: bila dia di bangku, mundur gratis (sekali per giliran).
            return !p.UsedFreeRetreatViaMbokSumi && BenchContainsMbokSumi(p);
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

        private void DrawInternal(PlayerState p)
        {
            if (p.Deck.Count == 0)
                return;
            var card = p.Deck[p.Deck.Count - 1];
            p.Deck.RemoveAt(p.Deck.Count - 1);
            p.Hand.Add(card);
            OnDrawn?.Invoke(card, p.Side);
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
