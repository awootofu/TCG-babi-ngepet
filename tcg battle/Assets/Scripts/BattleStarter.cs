using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TcgBattle.Data;
using TcgBattle.Gameplay;
using TcgBattle.UI;

namespace TcgBattle
{
    /// <summary>
    /// Roots the whole battle: creates the BattleManager from the card database,
    /// builds the UI canvas, wires the bot, and restarts matches.
    /// Put this on any GameObject in a scene (or use TCG Battle &gt; Build Battle Scene).
    /// </summary>
    public class BattleStarter : MonoBehaviour
    {
        [Header("Leave empty to use the code defaults (art-transcribed stats)")]
        public CardDatabase database;

        private BattleManager battle;
        private BattleUI ui;
        private BotController bot;

        public BattleManager Battle => battle;

        private void Awake()
        {
            EnsureEventSystem();
            BuildCanvas();

            bot = GetComponent<BotController>();
            if (bot == null)
                bot = gameObject.AddComponent<BotController>();

            ui = GetComponentInChildren<BattleUI>();
            if (ui == null)
            {
                var canvas = GetComponentInChildren<Canvas>();
                ui = canvas.gameObject.AddComponent<BattleUI>();
            }

            CreateBattle();
        }

        private void CreateBattle()
        {
            var db = database;
            if (db == null)
            {
                db = Resources.Load<CardDatabase>("CardDatabase");
                database = db; // cache for the session
            }
            if (db == null)
            {
                // Code defaults carry the exact stats transcribed from the artwork.
                db = ScriptableObject.CreateInstance<CardDatabase>();
                db.name = "CardDatabase (runtime defaults)";
            }

            battle = new BattleManager(db);
            bot.Bind(battle);
            ui.Bind(battle, this, bot);

            battle.StartBattle();
        }

        public void Restart()
        {
            if (battle != null)
                ui.Unbind(battle);
            CreateBattle();
        }

        private void BuildCanvas()
        {
            if (GetComponentInChildren<Canvas>() != null)
                return;

            var go = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(BattleUI));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.transform.SetParent(transform, false);

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Height-matched: the landscape composition keeps a fixed 1080-unit height
            // and gains/loses horizontal room as the window aspect changes.
            scaler.matchWidthOrHeight = 1f;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
