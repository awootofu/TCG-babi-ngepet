using System.Collections;
using UnityEngine;

namespace TcgBattle.Gameplay
{
    /// <summary>
    /// Drives the bot's turn. Strictly uses BattleManager's bot hooks, so it can never
    /// perform an illegal action. Launched by the UI when the bot's turn starts.
    /// </summary>
    public class BotController : MonoBehaviour
    {
        private BattleManager battle;

        [Header("Bot pacing (seconds)")]
        public float actionDelay = 0.7f;
        public float afterAttackDelay = 1.2f;

        public void Bind(BattleManager manager)
        {
            battle = manager;
        }

        // Realtime waits keep bot pacing stable even when the editor is unfocused
        // (scaled delta-time can crawl while domain tools pump the player loop).
        public IEnumerator RunBotTurn()
        {
            if (battle == null || battle.Phase != Phase.BotTurn)
                yield break;

            yield return new WaitForSecondsRealtime(actionDelay);

            // 1. Evolve the active Warga line when the next form is in hand.
            //    Evolution-only cards are never sent to the bench.
            for (int i = 0; i < 2; i++)
            {
                if (battle.Bot.Active == null)
                    break;
                string nextId = BattleRules.EvolutionTarget(battle.Bot.Active.CardId);
                if (nextId == null)
                    break;
                var evolution = FindCard(battle.Bot.Hand, nextId);
                if (evolution == null || !battle.BotEvolve(evolution))
                    break;
                yield return new WaitForSecondsRealtime(actionDelay);
            }

            // 2. Bench development: play up to two basic battle cards, then Mbok Sumi.
            int played = 0;
            while (played < 2 && battle.Bot.Bench.Count < BattleRules.BenchLimit)
            {
                var card = FindFirstBenchBattleCard(battle.Bot.Hand);
                if (card == null)
                    break;
                if (!battle.BotPlayToBench(card))
                    break;
                played++;
                yield return new WaitForSecondsRealtime(actionDelay);
            }

            var ms = FindSupport(battle.Bot.Hand, Data.SupportEffect.MbokSumiBenchPassive);
            if (ms != null && battle.Bot.Bench.Count < BattleRules.BenchLimit)
            {
                battle.BotPlayToBench(ms);
                yield return new WaitForSecondsRealtime(actionDelay);
            }

            // 3. Punarma: requires Babi Ngepet in the opposing active slot; boosts next attack.
            var pj = FindSupport(battle.Bot.Hand, Data.SupportEffect.PunarmaJumatLegiSingleShot);
            if (pj != null && battle.Player.Active != null && battle.Player.Active.CardId == "BN")
            {
                battle.BotPlayToBench(pj);
                yield return new WaitForSecondsRealtime(actionDelay);
            }

            // 4. Kucing Liar: snipes the player's bench Mbok Sumi.
            var kl = FindSupport(battle.Bot.Hand, Data.SupportEffect.KucingLiarSingleShot);
            if (kl != null && BattleManager.BenchContainsMbokSumi(battle.Player))
            {
                battle.BotPlayToBench(kl);
                yield return new WaitForSecondsRealtime(actionDelay);
            }

            // 5. Attack if legal, otherwise consider a tactical retreat.
            if (battle.CanBotAttack())
            {
                battle.BotAttack();
                yield return new WaitForSecondsRealtime(afterAttackDelay);
            }
            else if (battle.CanBotRetreat())
            {
                bool worthIt = battle.Bot.Active != null
                    && battle.Bot.Active.CurrentHp <= battle.Bot.Active.MaxHp * 0.4f
                    && battle.Bot.Energy >= battle.Bot.Active.RetreatCost;
                if (worthIt)
                {
                    battle.BotRetreat();
                    yield return new WaitForSecondsRealtime(actionDelay);
                    if (battle.CanBotAttack())
                    {
                        battle.BotAttack();
                        yield return new WaitForSecondsRealtime(afterAttackDelay);
                    }
                }
            }

            // 6. End the turn (always legal for the bot).
            if (battle.Phase == Phase.BotTurn)
                battle.BotEndTurn();
        }

        private static CardInstance FindFirstBenchBattleCard(System.Collections.Generic.List<CardInstance> list)
        {
            foreach (var c in list)
                if (c.IsBattle && !BattleRules.IsEvolutionOnly(c))
                    return c;
            return null;
        }

        private static CardInstance FindCard(System.Collections.Generic.List<CardInstance> list, string cardId)
        {
            foreach (var c in list)
                if (c.CardId == cardId)
                    return c;
            return null;
        }

        private static CardInstance FindSupport(System.Collections.Generic.List<CardInstance> list, Data.SupportEffect effect)
        {
            foreach (var c in list)
                if (c.IsSupport && c.Support.effect == effect)
                    return c;
            return null;
        }
    }
}
