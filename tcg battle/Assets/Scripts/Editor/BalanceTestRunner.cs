#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using TcgBattle.Data;
using TcgBattle.Gameplay;
using TcgBattle.UI;

namespace TcgBattle.EditorTools
{
    /// <summary>
    /// Targeted EditMode checks for the KISATARA balance update. Runs from
    /// TCG Battle > Run Balance Tests and writes a plain-text report next to the
    /// project's existing .stage1-tests folder.
    /// </summary>
    public static class BalanceTestRunner
    {
        private const string DatabasePath = "Assets/Resources/CardDatabase.asset";
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _passed;
        private static int _failed;

        [MenuItem("TCG Battle/Run Balance Tests")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;
            Report.Length = 0;
            Report.AppendLine("KISATARA balance update - automated checks");
            Report.AppendLine("Run at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Report.AppendLine();

            var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (db == null)
            {
                Check(false, "database asset exists", DatabasePath + " was not found");
                WriteReport();
                return;
            }

            TestDatabase(db);
            TestDeckLists(db);
            TestArtwork(db);
            TestSupportHp(db);
            TestWargaEvolution(db);
            TestPakKartaEvolution(db);
            TestAttackMath(db);
            TestBabiNgepetSteal(db);
            TestPasukanOborEnergy(db);
            TestPakKartaDraw(db);
            TestSilumanBabiRecoil(db);
            TestAmukanWarga(db);
            TestKucingLiar(db);
            TestPunarma(db);
            TestCardBack();

            Report.AppendLine();
            Report.AppendLine($"RESULT: {_passed} passed, {_failed} failed");
            WriteReport();

            if (_failed == 0)
                Debug.Log($"[BalanceTestRunner] PASS - {_passed} checks. Report: {ReportPath()}");
            else
                Debug.LogError($"[BalanceTestRunner] FAIL - {_failed} of {_passed + _failed} checks failed. Report: {ReportPath()}");
        }

        // ------------------------------------------------------------------ checks

        private static void TestDatabase(CardDatabase db)
        {
            Section("Card statistics from the supplied artwork");

            var bn = db.GetBattleCard("BN");
            Check(bn != null, "Babi Ngepet exists");
            if (bn != null)
            {
                Check(bn.maxHp == 200, "Babi Ngepet HP is 200", $"got {bn.maxHp}");
                Check(bn.attack == 80, "Babi Ngepet attack is 80", $"got {bn.attack}");
                Check(bn.attackName == "Mencuri Kekayaan", "Babi Ngepet attack name",
                    bn.attackName);
                Check(bn.attackEnergyCost == 1, "Babi Ngepet attack cost is 1",
                    $"got {bn.attackEnergyCost}");
                CheckContains(bn.attackEffectText, "gambar", "Babi Ngepet coin-flip text");
                CheckContains(bn.attackEffectText, "1 Energi", "Babi Ngepet energy text");
            }

            var pk = db.GetBattleCard("PK");
            Check(pk != null, "Pak Karta exists");
            if (pk != null)
            {
                Check(pk.maxHp == 150, "Pak Karta HP is 150", $"got {pk.maxHp}");
                Check(pk.attack == 20, "Pak Karta attack is 20", $"got {pk.attack}");
                Check(pk.attackName == "Ingin Kaya", "Pak Karta attack name", pk.attackName);
                CheckContains(pk.attackEffectText, "draw pile", "Pak Karta draw text");
                Check(pk.attackEnergyCost == 1, "Pak Karta attack cost is 1",
                    $"got {pk.attackEnergyCost}");
            }

            var sb = db.GetBattleCard("SB");
            Check(sb != null, "Siluman Babi exists");
            if (sb != null)
            {
                Check(sb.maxHp == 300, "Siluman Babi HP is 300", $"got {sb.maxHp}");
                Check(sb.attack == 120, "Siluman Babi attack is 120", $"got {sb.attack}");
                Check(sb.attackName == "Hilang Kemanusiaan", "Siluman Babi attack name",
                    sb.attackName);
                CheckContains(sb.attackEffectText, "30 damage", "Siluman Babi recoil text");
                Check(sb.attackEnergyCost == 1, "Siluman Babi attack cost is 1",
                    $"got {sb.attackEnergyCost}");
            }

            var wd = db.GetBattleCard("WD");
            Check(wd != null, "Warga Desa exists");
            if (wd != null)
            {
                Check(wd.maxHp == 150, "Warga Desa HP is 150", $"got {wd.maxHp}");
                Check(wd.attack == 30, "Warga Desa attack is 30", $"got {wd.attack}");
                Check(wd.attackName == "Serang Balik", "Warga Desa attack name",
                    wd.attackName);
            }

            var po = db.GetBattleCard("PO");
            Check(po != null, "Pasukan Obor exists");
            if (po != null)
            {
                Check(po.maxHp == 200, "Pasukan Obor HP is 200", $"got {po.maxHp}");
                Check(po.attack == 45, "Pasukan Obor attack is 45", $"got {po.attack}");
                Check(po.attackName == "Obor Api", "Pasukan Obor attack name",
                    po.attackName);
                CheckContains(po.attackEffectText, "1 energi", "Pasukan Obor energy text");
                Check(po.attackEnergyCost == 1, "Pasukan Obor attack cost is 1",
                    $"got {po.attackEnergyCost}");
            }

            var aw = db.GetBattleCard("AW");
            Check(aw != null, "Amukan Warga exists");
            if (aw != null)
            {
                Check(aw.maxHp == 220, "Amukan Warga HP is 220", $"got {aw.maxHp}");
                Check(aw.attack == 75, "Amukan Warga attack is 75", $"got {aw.attack}");
                Check(aw.attackName == "Balas Dendam", "Amukan Warga attack name",
                    aw.attackName);
                Check(aw.attackEnergyCost == 1, "Amukan Warga attack cost is 1",
                    $"got {aw.attackEnergyCost}");
                CheckContains(aw.passiveText, "20 HP", "Amukan Warga heal passive");
                CheckContains(aw.attackEffectText, "Mbok Sumi", "Amukan Warga splash text");
            }

            var ms = db.GetSupportCard("MS");
            Check(ms != null, "Mbok Sumi exists");
            if (ms != null)
            {
                Check(ms.maxHp == 50, "Mbok Sumi printed HP is 50", $"got {ms.maxHp}");
                CheckContains(ms.description, "Babi Ngepet", "Mbok Sumi evolution text");
            }

            var ta = db.GetSupportCard("TA");
            Check(ta != null, "Tumbal Anak exists");
            if (ta != null)
            {
                CheckContains(ta.description, "pasti mencuri energi",
                    "Tumbal Anak guarantee text");
            }

            var kl = db.GetSupportCard("KL");
            Check(kl != null, "Kucing Liar exists");
            if (kl != null)
            {
                Check(kl.effectValue == 40, "Kucing Liar damage is 40", $"got {kl.effectValue}");
                CheckContains(kl.description, "40 damage", "Kucing Liar damage text");
            }

            var pj = db.GetSupportCard("PJ");
            Check(pj != null, "Punarma Jumat Legi exists");
            if (pj != null)
            {
                Check(pj.effectValue == 50, "Punarma bonus is 50", $"got {pj.effectValue}");
                CheckContains(pj.description, "Babi Ngepet", "Punarma condition text");
            }

            Check(!string.IsNullOrEmpty(db.rulesText), "rules text is present");
        }

        private static void TestDeckLists(CardDatabase db)
        {
            Section("Deck lists");
            Check(CardDatabase.CountDeckCopies(db.playerDeck) == CardDatabase.DeckSize,
                "player deck has 20 cards", $"got {CardDatabase.CountDeckCopies(db.playerDeck)}");
            Check(CardDatabase.CountDeckCopies(db.botDeck) == CardDatabase.DeckSize,
                "bot deck has 20 cards", $"got {CardDatabase.CountDeckCopies(db.botDeck)}");
            Check(CardDatabase.ValidateDeck(db.playerDeck, true) == null,
                "player deck matches the required list",
                CardDatabase.ValidateDeck(db.playerDeck, true));
            Check(CardDatabase.ValidateDeck(db.botDeck, false) == null,
                "bot deck matches the required list",
                CardDatabase.ValidateDeck(db.botDeck, false));
            Check(db.GetBattleCard("KL") == null, "Kucing Liar is a support def");
            Check(db.GetSupportCard("BN") == null, "Babi Ngepet is a battle def");
        }

        private static void TestArtwork(CardDatabase db)
        {
            Section("Artwork resources");
            foreach (var def in db.battleCards)
            {
                var sprite = ArtLibrary.GetCardArt(def.artResource);
                Check(sprite != null, $"battle art loads: {def.id}",
                    def.artResource);
            }
            foreach (var def in db.supportCards)
            {
                var sprite = ArtLibrary.GetCardArt(def.artResource);
                Check(sprite != null, $"support art loads: {def.id}",
                    def.artResource);
            }
            var back = ArtLibrary.GetCardBack();
            Check(back != null, "official card back loads", "Cards/Back Side");
            if (back != null)
            {
                Check(back.texture.width == 1260 && back.texture.height == 1760,
                    "official card back is 1260x1760",
                    $"{back.texture.width}x{back.texture.height}");
            }
        }

        private static void TestSupportHp(CardDatabase db)
        {
            Section("Support HP model");
            var ms = CardInstance.CreateSupport(db.GetSupportCard("MS"));
            Check(ms.MaxHp == 50 && ms.CurrentHp == 50 && ms.HasHp,
                "Mbok Sumi instance starts at 50/50",
                $"{ms.CurrentHp}/{ms.MaxHp}");
            ms.CurrentHp = 10;
            Check(!ms.IsKnockedOut, "Mbok Sumi at 10 HP is not KO'd");
            ms.CurrentHp = 0;
            Check(ms.IsKnockedOut, "Mbok Sumi at 0 HP is KO'd");

            var ta = CardInstance.CreateSupport(db.GetSupportCard("TA"));
            Check(!ta.HasHp && !ta.IsKnockedOut,
                "Tumbal Anak has no printed HP and never counts as KO'd");
        }

        private static void TestWargaEvolution(CardDatabase db)
        {
            Section("Warga Desa -> Pasukan Obor -> Amukan Warga evolution");
            var b = NewBattle(db);
            b.Player.Active = Battle(db, "WD");
            b.Player.Active.CurrentHp = 100;
            var po = Battle(db, "PO");
            b.Player.Hand.Add(po);

            Check(b.Evolve(po), "WD 100/150 evolves into PO");
            Check(b.Player.Active.CardId == "PO", "active is Pasukan Obor",
                b.Player.Active.CardId);
            Check(b.Player.Active.CurrentHp == 150 && b.Player.Active.MaxHp == 200,
                "damage carries over: PO is 150/200",
                $"{b.Player.Active.CurrentHp}/{b.Player.Active.MaxHp}");
            Check(!b.Player.Hand.Contains(po), "evolution card is consumed");

            b.Player.Active.CurrentHp = 180;
            var aw = Battle(db, "AW");
            b.Player.Hand.Add(aw);
            Check(b.Evolve(aw), "PO 180/200 evolves into AW");
            Check(b.Player.Active.CardId == "AW", "active is Amukan Warga",
                b.Player.Active.CardId);
            Check(b.Player.Active.CurrentHp == 200 && b.Player.Active.MaxHp == 220,
                "damage carries over: AW is 200/220",
                $"{b.Player.Active.CurrentHp}/{b.Player.Active.MaxHp}");

            Check(BattleRules.EvolutionHpAfterDamage(150, 0, 200) == 50,
                "carry-over formula: (150,0,200) = 50");
            Check(BattleRules.EvolutionHpAfterDamage(150, 150, 200) == 200,
                "carry-over formula: (150,150,200) = 200");
        }

        private static void TestPakKartaEvolution(CardDatabase db)
        {
            Section("Pak Karta -> Babi Ngepet -> Siluman Babi evolution");
            Check(BattleRules.EvolutionSource("BN") == "PK",
                "Babi Ngepet evolves from Pak Karta");
            Check(BattleRules.EvolutionSource("SB") == "BN",
                "Siluman Babi evolves from Babi Ngepet");
            Check(BattleRules.EvolutionTarget("PK") == "BN",
                "Pak Karta's next form is Babi Ngepet");
            Check(BattleRules.EvolutionTarget("BN") == "SB",
                "Babi Ngepet's next form is Siluman Babi");
            Check(BattleRules.EvolutionTarget("SB") == null,
                "Siluman Babi is the end of the Babi line");

            var b = NewBattle(db);
            b.Player.Active = Battle(db, "PK");
            b.Player.Active.CurrentHp = 100;
            b.Player.Bench.Add(Support(db, "MS"));
            var bn = Battle(db, "BN");
            b.Player.Hand.Add(bn);
            b.Player.Energy = 3;

            Check(b.CanEvolve(bn, out var canErr),
                "PK + Mbok Sumi + BN in hand is legal", canErr);
            Check(b.Evolve(bn), "PK evolves into Babi Ngepet");
            Check(b.Player.Active.CardId == "BN", "active is Babi Ngepet",
                b.Player.Active.CardId);
            Check(b.Player.Active.CurrentHp == 150 && b.Player.Active.MaxHp == 200,
                "evolution carries damage: BN is 150/200",
                $"{b.Player.Active.CurrentHp}/{b.Player.Active.MaxHp}");
            Check(!b.Player.Hand.Contains(bn), "Babi Ngepet card is consumed");
            Check(BattleManager.BenchContainsMbokSumi(b.Player), "Mbok Sumi stays on bench");
            Check(b.Player.Energy == 3, "evolution is free", $"got {b.Player.Energy}");
            Check(b.Player.Points == 0, "evolution does not score");

            // The second step is the normal BN > SB evolution; Mbok Sumi is no
            // longer required because the active card is Babi Ngepet, not Pak Karta.
            b.Player.Bench.Clear();
            b.Player.Active.CurrentHp = 120;
            var sb = Battle(db, "SB");
            b.Player.Hand.Add(sb);
            Check(b.CanEvolve(sb, out var sbErr),
                "BN > SB is legal without Mbok Sumi", sbErr);
            Check(b.Evolve(sb), "Babi Ngepet evolves into Siluman Babi");
            Check(b.Player.Active.CardId == "SB", "active is Siluman Babi",
                b.Player.Active.CardId);
            Check(b.Player.Active.CurrentHp == 220 && b.Player.Active.MaxHp == 300,
                "evolution carries damage: SB is 220/300",
                $"{b.Player.Active.CurrentHp}/{b.Player.Active.MaxHp}");
            Check(!b.Player.Hand.Contains(sb), "Siluman Babi card is consumed");

            var noMs = NewBattle(db);
            noMs.Player.Active = Battle(db, "PK");
            var bn2 = Battle(db, "BN");
            noMs.Player.Hand.Add(bn2);
            Check(!noMs.CanEvolve(bn2, out var noMsErr),
                "PK > BN is illegal without Mbok Sumi");
            CheckContains(noMsErr, "Mbok Sumi", "no-Mbok Sumi error mentions her");

            var skip = NewBattle(db);
            skip.Player.Active = Battle(db, "PK");
            skip.Player.Bench.Add(Support(db, "MS"));
            var sb2 = Battle(db, "SB");
            skip.Player.Hand.Add(sb2);
            Check(!skip.CanEvolve(sb2, out var skipErr),
                "Pak Karta cannot skip directly to Siluman Babi");
            CheckContains(skipErr, "Babi Ngepet", "skip error names the missing step");

            var legacy = NewBattle(db);
            legacy.Player.Active = Battle(db, "PK");
            legacy.Player.Bench.Add(Support(db, "MS"));
            legacy.Player.Hand.Add(Battle(db, "BN"));
            Check(!legacy.CanTransformPakKarta(out _),
                "legacy transform entry point is inert");
            Check(!legacy.TransformPakKarta("BN"),
                "legacy transform call is refused");
        }

        private static void TestAttackMath(CardDatabase db)
        {
            Section("Damage, energy cost and element multipliers");
            var b = NewBattle(db);
            b.Player.Active = Battle(db, "BN");
            b.Bot.Active = Battle(db, "WD");
            b.Player.Energy = 10;
            b.Bot.Energy = 0;

            Check(b.Attack(), "Babi Ngepet attacks");
            Check(b.Bot.Active.CurrentHp == 70, "BN 80 vs WD 150 leaves 70",
                $"got {b.Bot.Active.CurrentHp}");
            Check(b.Player.Energy == 9, "BN attack costs 1 energy",
                $"got {b.Player.Energy}");
            Check(b.Player.HasAttackedThisTurn, "once-per-turn attack flag is set");

            var b2 = NewBattle(db);
            b2.Player.Active = Battle(db, "WD");
            b2.Bot.Active = Battle(db, "BN");
            b2.Player.Energy = 10;
            Check(b2.Attack(), "Warga Desa attacks Babi Ngepet");
            Check(b2.Bot.Active.CurrentHp == 140, "Fire 30 x2 vs Dark leaves 140",
                $"got {b2.Bot.Active.CurrentHp}");

            Check(BattleRules.ComputeDamage(Battle(db, "WD"), Battle(db, "BN"), 50) == 160,
                "Punarma +50 is doubled by Fire vs Dark");
            Check(BattleRules.ComputeDamage(Battle(db, "BN"), Battle(db, "WD"), 0) == 80,
                "Dark vs Fire is 1x");
        }

        private static void TestBabiNgepetSteal(CardDatabase db)
        {
            Section("Babi Ngepet coin flip and Tumbal Anak");
            var b = NewBattle(db);
            b.Player.Active = Battle(db, "BN");
            b.Bot.Active = Battle(db, "WD");
            b.Player.Energy = 5;
            b.Bot.Energy = 5;
            var ta = Support(db, "TA");
            b.Player.Hand.Add(ta);
            int supportUsed = 0;
            b.OnSupportUsed += (card, side) => supportUsed++;

            Check(!b.CanPlayToBench(ta, out _), "Tumbal Anak cannot be benched");
            Check(b.Attack(), "BN attacks with Tumbal Anak in hand");
            Check(!b.Player.Hand.Contains(ta), "Tumbal Anak is consumed");
            Check(supportUsed == 1, "OnSupportUsed fired once", $"got {supportUsed}");
            Check(b.LastBabiNgepetStealGuaranteed, "Tumbal Anak guarantees the steal");
            Check(b.LastBabiNgepetStealHeads, "guaranteed flip counts as heads");
            Check(b.Bot.Energy == 4, "opponent lost 1 energy", $"got {b.Bot.Energy}");
            Check(b.Player.Energy == 5, "attacker paid 1 and stole 1",
                $"got {b.Player.Energy}");

            var b2 = NewBattle(db);
            b2.Player.Active = Battle(db, "BN");
            b2.Bot.Active = Battle(db, "WD");
            b2.Player.Energy = 5;
            b2.Bot.Energy = 5;
            UnityEngine.Random.InitState(12345);
            Check(b2.Attack(), "BN attacks without Tumbal Anak");
            if (b2.LastBabiNgepetStealHeads)
            {
                Check(b2.Bot.Energy == 4 && b2.Player.Energy == 5,
                    "heads steals 1 energy",
                    $"player={b2.Player.Energy} bot={b2.Bot.Energy}");
            }
            else
            {
                Check(b2.Bot.Energy == 5 && b2.Player.Energy == 4,
                    "tails does not steal energy",
                    $"player={b2.Player.Energy} bot={b2.Bot.Energy}");
            }
        }

        private static void TestPasukanOborEnergy(CardDatabase db)
        {
            Section("Pasukan Obor energy gain");
            var b = NewBattle(db);
            b.Player.Active = Battle(db, "PO");
            b.Bot.Active = Battle(db, "WD");
            b.Player.Energy = 5;
            int energyEvents = 0;
            b.OnEnergyChanged += side => energyEvents++;

            Check(b.Attack(), "Pasukan Obor attacks");
            Check(b.Player.Energy == 5, "PO pays 1 and gains 1", $"got {b.Player.Energy}");
            Check(b.Bot.Active.CurrentHp == 105, "PO 45 vs WD 150 leaves 105",
                $"got {b.Bot.Active.CurrentHp}");
            Check(energyEvents >= 2, "energy change events fired", $"got {energyEvents}");

            var b2 = NewBattle(db);
            b2.Player.Active = Battle(db, "PO");
            b2.Bot.Active = Battle(db, "WD");
            b2.Player.Energy = BattleRules.EnergyCap;
            Check(b2.Attack(), "Pasukan Obor attacks at the energy cap");
            Check(b2.Player.Energy == BattleRules.EnergyCap,
                "energy gain is capped at 10", $"got {b2.Player.Energy}");
        }

        private static void TestPakKartaDraw(CardDatabase db)
        {
            Section("Pak Karta draw effect");
            var b = NewBattle(db);
            b.Player.Active = Battle(db, "PK");
            b.Bot.Active = Battle(db, "WD");
            b.Player.Energy = 5;
            b.Player.Hand.Clear();
            b.Player.Deck.Add(Battle(db, "BN"));
            int drawn = 0;
            b.OnDrawn += (card, side) => drawn++;

            Check(b.Attack(), "Pak Karta attacks");
            Check(b.Player.Hand.Count == 1, "Pak Karta drew 1 card",
                $"hand={b.Player.Hand.Count}");
            Check(b.Player.Deck.Count == 0, "draw pile is consumed",
                $"deck={b.Player.Deck.Count}");
            Check(drawn == 1, "OnDrawn fired once", $"got {drawn}");
            Check(b.Bot.Active.CurrentHp == 130, "PK 20 vs WD 150 leaves 130",
                $"got {b.Bot.Active.CurrentHp}");

            var b2 = NewBattle(db);
            b2.Player.Active = Battle(db, "PK");
            b2.Bot.Active = Battle(db, "WD");
            b2.Player.Energy = 5;
            b2.Player.Deck.Clear();
            Check(b2.Attack(), "Pak Karta attacks with an empty draw pile");
            Check(b2.Player.Hand.Count == 0, "empty draw pile adds no card");
        }

        private static void TestSilumanBabiRecoil(CardDatabase db)
        {
            Section("Siluman Babi recoil");
            var b = NewBattle(db);
            b.Player.Active = Battle(db, "SB");
            b.Bot.Active = Battle(db, "AW");
            b.Player.Energy = 5;
            int selfDamage = 0;
            b.OnSelfDamaged += (card, side, amount) => selfDamage += amount;

            Check(b.Attack(), "Siluman Babi attacks");
            Check(b.Bot.Active.CurrentHp == 100, "SB 120 vs AW 220 leaves 100",
                $"got {b.Bot.Active.CurrentHp}");
            Check(b.Player.Active.CurrentHp == 270, "SB takes 30 recoil: 270/300",
                $"{b.Player.Active.CurrentHp}/{b.Player.Active.MaxHp}");
            Check(selfDamage == 30, "OnSelfDamaged reported 30", $"got {selfDamage}");

            var b2 = NewBattle(db);
            b2.Player.Active = Battle(db, "SB");
            b2.Player.Active.CurrentHp = 30;
            b2.Player.Bench.Add(Battle(db, "PK"));
            b2.Bot.Active = Battle(db, "WD");
            b2.Player.Energy = 5;
            Check(b2.Attack(), "Siluman Babi attacks at 30 HP");
            Check(b2.Player.Active.CardId == "PK", "self-KO promotes the bench card",
                b2.Player.Active.CardId);
            Check(b2.Bot.Points == 1, "opponent scores on Siluman Babi self-KO",
                $"got {b2.Bot.Points}");
            Check(b2.Player.Points == 0, "attacker does not score on self-KO",
                $"got {b2.Player.Points}");
        }

        private static void TestAmukanWarga(CardDatabase db)
        {
            Section("Amukan Warga heal and bench splash");
            var b = NewBattle(db);
            b.Player.Active = Battle(db, "AW");
            b.Player.Active.CurrentHp = 100;
            SetPhase(b, Phase.BotTurn);
            int healed = 0;
            b.OnHealed += (card, side, amount) => healed += amount;
            b.EndBotTurn();
            Check(b.Player.Active.CurrentHp == 120, "AW heals 20 at turn start",
                $"{b.Player.Active.CurrentHp}/{b.Player.Active.MaxHp}");
            Check(healed == 20, "OnHealed reported 20", $"got {healed}");

            var b2 = NewBattle(db);
            b2.Player.Active = Battle(db, "AW");
            b2.Player.Active.CurrentHp = 210;
            SetPhase(b2, Phase.BotTurn);
            b2.EndBotTurn();
            Check(b2.Player.Active.CurrentHp == 220, "AW heal is capped at max HP",
                $"{b2.Player.Active.CurrentHp}/{b2.Player.Active.MaxHp}");

            var b3 = NewBattle(db);
            b3.Player.Active = Battle(db, "AW");
            b3.Bot.Active = Battle(db, "WD");
            var ms = Support(db, "MS");
            b3.Bot.Bench.Add(ms);
            b3.Player.Energy = 5;
            int supportDamage = 0;
            b3.OnSupportDamaged += (card, side, amount) => supportDamage += amount;

            Check(b3.Attack(), "AW attacks with an enemy Mbok Sumi benched");
            Check(ms.CurrentHp == 30, "AW splash deals 20 to Mbok Sumi: 30/50",
                $"{ms.CurrentHp}/{ms.MaxHp}");
            Check(supportDamage == 20, "OnSupportDamaged reported 20",
                $"got {supportDamage}");
            Check(b3.Bot.Points == 0, "damaging a support does not score");

            var b4 = NewBattle(db);
            b4.Player.Active = Battle(db, "AW");
            b4.Bot.Active = Battle(db, "WD");
            var ms2 = Support(db, "MS");
            ms2.CurrentHp = 20;
            b4.Bot.Bench.Add(ms2);
            b4.Player.Energy = 5;
            int destroyed = 0;
            b4.OnSupportDestroyed += (card, side) => destroyed++;
            Check(b4.Attack(), "AW attacks a 20 HP Mbok Sumi");
            Check(!b4.Bot.Bench.Contains(ms2), "Mbok Sumi leaves the bench at 0 HP");
            Check(destroyed == 1, "OnSupportDestroyed fired once", $"got {destroyed}");
            Check(b4.Bot.Points == 0, "KO'ing a support scores no point",
                $"got {b4.Bot.Points}");
        }

        private static void TestKucingLiar(CardDatabase db)
        {
            Section("Kucing Liar");
            var b = NewBattle(db);
            b.Bot.Active = Battle(db, "WD");
            var kl = Support(db, "KL");
            b.Bot.Hand.Add(kl);
            b.Player.Active = Battle(db, "PK");
            var ms = Support(db, "MS");
            b.Player.Bench.Add(ms);
            SetPhase(b, Phase.BotTurn);
            int damaged = 0;
            b.OnSupportDamaged += (card, side, amount) => damaged += amount;

            Check(b.BotPlayToBench(kl), "bot plays Kucing Liar");
            Check(ms.CurrentHp == 10, "Kucing Liar deals 40 to Mbok Sumi: 10/50",
                $"{ms.CurrentHp}/{ms.MaxHp}");
            Check(damaged == 40, "OnSupportDamaged reported 40", $"got {damaged}");
            Check(!b.Bot.Hand.Contains(kl), "Kucing Liar leaves the hand");

            var kl2 = Support(db, "KL");
            b.Bot.Hand.Add(kl2);
            int destroyed = 0;
            b.OnSupportDestroyed += (card, side) => destroyed++;
            Check(b.BotPlayToBench(kl2), "bot plays a second Kucing Liar");
            Check(!b.Player.Bench.Contains(ms), "second Kucing Liar KOs Mbok Sumi");
            Check(destroyed == 1, "OnSupportDestroyed fired once", $"got {destroyed}");
            Check(b.Player.Points == 0, "Mbok Sumi KO scores no point",
                $"got {b.Player.Points}");

            var b2 = NewBattle(db);
            b2.Bot.Active = Battle(db, "WD");
            b2.Player.Active = Battle(db, "PK");
            var kl3 = Support(db, "KL");
            b2.Bot.Hand.Add(kl3);
            SetPhase(b2, Phase.BotTurn);
            Check(!b2.BotPlayToBench(kl3), "Kucing Liar is illegal without enemy Mbok Sumi");
            Check(b2.Bot.Hand.Contains(kl3), "illegal Kucing Liar stays in hand");
        }

        private static void TestPunarma(CardDatabase db)
        {
            Section("Punarma Jumat Legi");
            var b = NewBattle(db);
            b.Bot.Active = Battle(db, "WD");
            var pj = Support(db, "PJ");
            b.Bot.Hand.Add(pj);
            b.Player.Active = Battle(db, "BN");
            b.Bot.Energy = 5;
            SetPhase(b, Phase.BotTurn);

            Check(b.BotPlayToBench(pj), "bot plays Punarma while BN is opposing active");
            Check(b.Bot.NextAttackBonus == 50, "Punarma grants +50",
                $"got {b.Bot.NextAttackBonus}");
            Check(b.BotAttack(), "empowered Warga attack resolves");
            Check(b.Player.Active.CurrentHp == 40, "(30 + 50) x2 vs BN leaves 40",
                $"got {b.Player.Active.CurrentHp}");
            Check(b.Bot.NextAttackBonus == 0, "Punarma bonus is consumed",
                $"got {b.Bot.NextAttackBonus}");

            var b2 = NewBattle(db);
            b2.Bot.Active = Battle(db, "WD");
            var pj2 = Support(db, "PJ");
            b2.Bot.Hand.Add(pj2);
            b2.Player.Active = Battle(db, "BN");
            SetPhase(b2, Phase.BotTurn);
            Check(b2.BotPlayToBench(pj2), "bot plays Punarma for the persistence check");
            Check(b2.Bot.NextAttackBonus == 50, "bonus is set");
            b2.BotEndTurn();
            Check(b2.Bot.NextAttackBonus == 50,
                "Punarma persists to the next Warga attack",
                $"got {b2.Bot.NextAttackBonus}");
            b2.Bot.Energy = 5;
            b2.Bot.HasAttackedThisTurn = false;
            SetPhase(b2, Phase.BotTurn);
            Check(b2.BotAttack(), "empowered attack resolves after the turn change");
            Check(b2.Player.Active.CurrentHp == 40,
                "persisted +50 still applies", $"got {b2.Player.Active.CurrentHp}");

            var b3 = NewBattle(db);
            b3.Bot.Active = Battle(db, "WD");
            var pj3 = Support(db, "PJ");
            b3.Bot.Hand.Add(pj3);
            b3.Player.Active = Battle(db, "PK");
            SetPhase(b3, Phase.BotTurn);
            Check(!b3.BotPlayToBench(pj3), "Punarma requires Babi Ngepet as opposing active");
            Check(b3.Bot.Hand.Contains(pj3), "illegal Punarma stays in hand");
        }

        private static void TestCardBack()
        {
            Section("Official card back");
            var back = ArtLibrary.GetCardBack();
            Check(back != null, "card back sprite is available");
            if (back == null)
                return;

            var parent = new GameObject("CardBackTest");
            try
            {
                var rt = CardBackVisual.Create(parent.transform, "Back", new Vector2(123f, 165f));
                var art = rt.Find("Art");
                Check(art != null, "card back art child exists");
                if (art != null)
                {
                    var img = art.GetComponent<UnityEngine.UI.Image>();
                    Check(img != null, "card back art has an Image");
                    if (img != null)
                    {
                        Check(img.sprite == back, "card back art uses ArtLibrary.GetCardBack()");
                        Check(img.preserveAspect, "card back preserves aspect ratio");
                        Check(!img.raycastTarget, "card back never intercepts input");
                    }
                }
                Check(rt.childCount == 2,
                    "card back contains only the seat and the supplied art",
                    $"children={rt.childCount}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static BattleManager NewBattle(CardDatabase db)
        {
            var b = new BattleManager(db);
            b.StartBattle();
            ClearPlayer(b.Player);
            ClearPlayer(b.Bot);
            SetPhase(b, Phase.PlayerTurn);
            return b;
        }

        private static void ClearPlayer(PlayerState p)
        {
            p.Deck.Clear();
            p.Hand.Clear();
            p.Bench.Clear();
            p.Active = null;
            p.Energy = 0;
            p.Points = 0;
            p.HasAttackedThisTurn = false;
            p.NextAttackBonus = 0;
        }

        private static CardInstance Battle(CardDatabase db, string id)
        {
            return CardInstance.CreateBattle(db.GetBattleCard(id));
        }

        private static CardInstance Support(CardDatabase db, string id)
        {
            return CardInstance.CreateSupport(db.GetSupportCard(id));
        }

        private static void SetPhase(BattleManager battle, Phase phase)
        {
            var prop = typeof(BattleManager).GetProperty("Phase",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(battle, phase);
        }

        private static void Check(bool condition, string name, string detail = null)
        {
            if (condition)
            {
                _passed++;
                Report.AppendLine("PASS  " + name);
            }
            else
            {
                _failed++;
                Report.AppendLine("FAIL  " + name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
            }
        }

        private static void CheckContains(string text, string expected, string name)
        {
            Check(!string.IsNullOrEmpty(text) &&
                  text.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0,
                name, "text='" + text + "'");
        }

        private static void Section(string title)
        {
            Report.AppendLine();
            Report.AppendLine("-- " + title);
        }

        private static string ReportPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../.stage1-tests/balance_results.txt"));
        }

        private static void WriteReport()
        {
            try
            {
                var path = ReportPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BalanceTestRunner] Could not write report: " + e.Message);
            }
        }
    }
}
#endif
