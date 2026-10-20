using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// Builds the whole portrait battle screen in code (1080x1920 reference) and renders
    /// BattleManager state. Everything lives under a safe-area root and uses percentage
    /// anchors, the palette follows the folklore direction (deep purple / muted lavender /
    /// warm ivory / subtle gold), and all animations here are cosmetic - battle state is
    /// authoritative.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        private const float RefH = 1920f;

        // ---- vertical band anchors (fraction of reference height, bottom-up) ----
        private const float BandPlayerHeader = 0.0219f;   // 42px
        private const float BandHand = 0.1031f;           // 198px
        private const float BandBenchPlayer = 0.2406f;    // 462px
        private const float BandActivePlayer = 0.4151f;   // 797px
        private const float BandStatus = 0.5488f;         // 1054px
        private const float BandButtons = 0.5901f;        // 1133px
        private const float BandDivider = 0.6203f;        // 1191px
        private const float BandActiveBot = 0.7328f;      // 1407px
        private const float BandBenchBot = 0.8958f;       // 1720px
        private const float BandBotHeader = 0.9672f;      // 1857px

        // ---- palette: deep purple / muted lavender / warm ivory / subtle gold ----
        private static readonly Color ColDeep = Hex(0x241B36);
        private static readonly Color ColBotZone = Hex(0xCBC3E4);
        private static readonly Color ColPanelInk = Hex(0x1C1530);
        private static readonly Color ColIvory = Hex(0xF2EADA);
        private static readonly Color ColIvoryDim = Hex(0xC9BFDB);
        private static readonly Color ColGold = Hex(0xD9A843);
        private static readonly Color ColGoldBright = Hex(0xF2C14E);
        private static readonly Color ColInk = Hex(0x241B36);
        private static readonly Color ColInkSoft = Hex(0x4C3D78);
        private static readonly Color ColMaroon = Hex(0x5E3336);
        private static readonly Color ColRed = Hex(0xC0504E);
        private static readonly Color ColInkInfo = Hex(0x4A3F66);
        private static readonly Color ColLavIdle = new Color(0.72f, 0.66f, 0.86f, 1f);
        private static readonly Color ColBotPipEmpty = new Color(0.14f, 0.11f, 0.21f, 0.22f);
        private static readonly Color ColPlayerPipEmpty = new Color(1f, 1f, 1f, 0.30f);

        private BattleManager battle;
        private BattleStarter starter;
        private BotController bot;

        // ---- built references ----
        private RectTransform canvasRt;
        private RectTransform safeRoot;
        private CardView botActiveView, playerActiveView;
        private CardView[] botBenchViews, playerBenchViews;
        private RectTransform[] botBenchSlots, playerBenchSlots;
        private GameObject[] botBenchFrames, playerBenchFrames;
        private TMP_Text[] botBenchLabels, playerBenchLabels;
        private GameObject botActiveFrame;
        private GameObject playerActiveFrame;
        private TMP_Text botActiveLabel, playerActiveLabel;
        private HandController hand;
        private TMP_Text botInfoText, playerDeckText;
        private TMP_Text botEnergyText, playerEnergyText;
        private Image[] botPips, playerPips;
        private Image statusPill;
        private Image statusShadow;
        private TMP_Text statusText;
        private string statusBaseline = "";
        private Image turnPillBg;
        private TMP_Text turnPillText;
        private Button mundurBtn, serangBtn, endBtn;
        private TMP_Text mundurLabel;
        private GameObject coinOverlay;
        private Image coinImage, coinRing;
        private TMP_Text coinText;
        private GameObject overOverlay;
        private TMP_Text overText, overScoreText;
        private Image overDiamondL, overDiamondR;
        private Button againBtn;

        // pending cosmetic animations applied after the next full refresh
        private CardInstance _pendingPromoPlayer, _pendingPromoBot;
        private CardInstance _pendingBenchPlayer, _pendingBenchBot;
        private CardInstance _pendingRetreatPlayer, _pendingRetreatBot;
        private bool _pendingPlayerDraw;

        private Coroutine turnBannerRoutine;
        private BattleManager _boundManager;

        // responsive layout scaling (k = safeRoot height / reference height)
        private float _k;   // 0 forces the first apply
        private readonly List<ScaledItem> _registry = new List<ScaledItem>();

        // ================================================================== binding

        public void Bind(BattleManager mgr, BattleStarter st, BotController botCtl)
        {
            if (_boundManager != null)
                Unsubscribe(_boundManager);

            battle = mgr;
            starter = st;
            bot = botCtl;
            _boundManager = battle;

            // queued animations from a previous match must not leak into the new one
            _pendingPromoPlayer = _pendingPromoBot = null;
            _pendingBenchPlayer = _pendingBenchBot = null;
            _pendingRetreatPlayer = _pendingRetreatBot = null;
            _pendingPlayerDraw = false;
            statusBaseline = "";

            if (canvasRt == null)
                BuildAll();

            Subscribe(mgr);
            RefreshAll();
        }

        private void Subscribe(BattleManager m)
        {
            m.OnStatus += HandleStatus;
            m.OnStateChanged += RefreshAll;
            m.OnCoinFlipped += ShowCoin;
            m.OnTurnStarted += ShowTurnBanner;
            m.OnAttack += HandleAttack;
            m.OnKnockedOut += HandleKo;
            m.OnPromoted += HandlePromoted;
            m.OnCardPlayedToBench += HandleBenchPlay;
            m.OnRetreated += HandleRetreat;
            m.OnScoreChanged += HandleScore;
            m.OnDrawn += HandleDraw;
            m.OnGameOver += ShowGameOver;
        }

        private void Unsubscribe(BattleManager m)
        {
            m.OnStatus -= HandleStatus;
            m.OnStateChanged -= RefreshAll;
            m.OnCoinFlipped -= ShowCoin;
            m.OnTurnStarted -= ShowTurnBanner;
            m.OnAttack -= HandleAttack;
            m.OnKnockedOut -= HandleKo;
            m.OnPromoted -= HandlePromoted;
            m.OnCardPlayedToBench -= HandleBenchPlay;
            m.OnRetreated -= HandleRetreat;
            m.OnScoreChanged -= HandleScore;
            m.OnDrawn -= HandleDraw;
            m.OnGameOver -= ShowGameOver;
        }

        public void Unbind(BattleManager m)
        {
            if (m == null)
                return;
            Unsubscribe(m);
            if (_boundManager == m)
                _boundManager = null;
            if (turnBannerRoutine != null)
            {
                StopCoroutine(turnBannerRoutine);
                turnBannerRoutine = null;
            }
            if (overOverlay != null)
                overOverlay.SetActive(false);
            if (coinOverlay != null)
                coinOverlay.SetActive(false);
            if (statusPill != null)
                statusPill.color = ColIvory;
        }

        private void OnDestroy()
        {
            if (_boundManager != null)
                Unsubscribe(_boundManager);
        }

        // ================================================================== build

        private void BuildAll()
        {
            canvasRt = (RectTransform)transform;

            // safe-area root: every UI element lands under this so notches/home
            // indicators can never cover the battle screen.
            var rootGo = new GameObject("SafeRoot", typeof(RectTransform), typeof(SafeAreaFitter));
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.SetParent(transform, false);
            StretchFull(rootRt, 0f);
            safeRoot = rootRt;

            BuildBackground();
            BuildStageOrnaments();
            BuildSlots();
            BuildBotHeader();
            BuildPlayerHeader();
            BuildActionRow();
            BuildHand();
            BuildOverlays();

            // capture the static layout once so it can be rescaled when the canvas size
            // changes (card views capture themselves in CardView.ApplyScale)
            UiRescaler.CaptureTreeExcludingCards(safeRoot, _registry);
        }

        private void Update()
        {
            if (safeRoot == null)
                return;
            float h = safeRoot.rect.height;
            if (h <= 0f)
                return;
            float k = Mathf.Clamp(h / RefH, 0.3f, 2f);
            if (Mathf.Abs(k - _k) < 0.004f)
                return;
            _k = k;
            ApplyScaleEverywhere(k);
        }

        private void ApplyScaleEverywhere(float k)
        {
            foreach (var it in _registry)
                it.Apply(k);

            CardView.GlobalScale = k;
            if (hand != null)
            {
                hand.SetScale(k);
                foreach (var v in hand.Views)
                    if (v != null)
                        v.ApplyScale(k);
            }
            if (botActiveView != null)
                botActiveView.ApplyScale(k);
            if (playerActiveView != null)
                playerActiveView.ApplyScale(k);
            foreach (var v in botBenchViews)
                if (v != null)
                    v.ApplyScale(k);
            foreach (var v in playerBenchViews)
                if (v != null)
                    v.ApplyScale(k);

            RefreshAll();
        }

        private void BuildBackground()
        {
            var deep = MakeImage("BgDeep", null, ColDeep);
            StretchFull(deep.rectTransform, 0f);

            // Bot territory: muted lavender panel from the divider line up beyond the
            // top edge (rounded top corners pushed off-screen).
            var panel = MakeImage("BotZonePanel", UiSpriteFactory.Rounded, ColBotZone);
            panel.type = Image.Type.Sliced;
            var pr = panel.rectTransform;
            pr.anchorMin = new Vector2(0f, BandDivider + 0.0042f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.offsetMin = new Vector2(0f, 0f);
            pr.offsetMax = new Vector2(0f, 46f);
        }

        /// <summary>Spot pool + ornament ring under each active slot (focal-point staging).</summary>
        private void BuildStageOrnaments()
        {
            // player stage (deep zone): warm ivory pool, gold ring, kawung-style diamonds
            AddStage(BandActivePlayer, new Vector2(420f, 450f), new Vector2(660f, 330f),
                new Color(1f, 0.86f, 0.62f, 0.07f), new Color(0.85f, 0.66f, 0.26f, 0.32f));

            // bot stage (lavender zone): ink pool with a deep gold ring
            AddStage(BandActiveBot, new Vector2(390f, 420f), new Vector2(600f, 300f),
                new Color(0.14f, 0.11f, 0.21f, 0.07f), new Color(0.55f, 0.42f, 0.15f, 0.30f));
        }

        private void AddStage(float yPercent, Vector2 ringSize, Vector2 poolSize, Color poolColor, Color ringColor)
        {
            var pool = MakeImage("StagePool", UiSpriteFactory.SoftShadow, poolColor);
            Place(pool.rectTransform, 0f, yPercent, poolSize);

            var ring = MakeImage("StageRing", UiSpriteFactory.RoundOutline, ringColor);
            ring.type = Image.Type.Sliced;
            Place(ring.rectTransform, 0f, yPercent, ringSize);

            // small diamonds riding the ring's north/south points (kawung hint) -
            // children of the ring so the rescaler keeps them glued to it
            var dN = MakeImage("StageDiamondN", UiSpriteFactory.Diamond, ringColor, ring.rectTransform);
            PlaceStageDiamond(dN.rectTransform, ringSize.y * 0.5f);
            var dS = MakeImage("StageDiamondS", UiSpriteFactory.Diamond, ringColor, ring.rectTransform);
            PlaceStageDiamond(dS.rectTransform, -ringSize.y * 0.5f);
        }

        private RectTransform PlaceStageDiamond(RectTransform rt, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(18f, 18f);
            rt.anchoredPosition = new Vector2(0f, y);
            return rt;
        }

        private void BuildSlots()
        {
            CreateBenchRow("BotBenchSlot", BandBenchBot, out botBenchSlots, out botBenchFrames,
                out botBenchLabels, out botBenchViews, "BotBench");

            var botActiveSlot = Node("BotActiveSlot", 0f, BandActiveBot, new Vector2(300f, 400f));
            botActiveFrame = MakeSlotFrame(botActiveSlot, "Kosong", 26f, out botActiveLabel);
            botActiveView = CardView.Create(botActiveSlot, new Vector2(300f, 400f), "BotActive");

            CreateBenchRow("PlayerBenchSlot", BandBenchPlayer, out playerBenchSlots, out playerBenchFrames,
                out playerBenchLabels, out playerBenchViews, "PlayerBench");

            var playerActiveSlot = Node("PlayerActiveSlot", 0f, BandActivePlayer, new Vector2(330f, 440f));
            playerActiveFrame = MakeSlotFrame(playerActiveSlot, "PILIH KARTU\nAKTIFMU", 28f, out playerActiveLabel);
            playerActiveView = CardView.Create(playerActiveSlot, new Vector2(330f, 440f), "PlayerActive");
        }

        private void CreateBenchRow(string slotName, float band, out RectTransform[] slots,
            out GameObject[] frames, out TMP_Text[] labels, out CardView[] views, string viewPrefix)
        {
            slots = new RectTransform[3];
            frames = new GameObject[3];
            labels = new TMP_Text[3];
            views = new CardView[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = Node($"{slotName}{i}", -178f + i * 178f, band, new Vector2(150f, 210f));
                slots[i] = slot;
                frames[i] = MakeSlotFrame(slot, "Kosong", 22f, out labels[i]);
                views[i] = CardView.Create(slot, new Vector2(150f, 210f), $"{viewPrefix}{i}");
            }
        }

        private void BuildBotHeader()
        {
            var label = MakeText("BotLabel", null, 34, ColInk, TextAlignmentOptions.Left, bold: true);
            label.characterSpacing = 5f;
            Place(label.rectTransform, -330f, BandBotHeader, new Vector2(240f, 44f));
            label.text = "BOT";

            botPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var pip = MakeImage($"BotPip{i}", UiSpriteFactory.Diamond, ColBotPipEmpty);
                Place(pip.rectTransform, -170f + i * 46f, BandBotHeader, new Vector2(30f, 30f));
                botPips[i] = pip;
            }

            // bot header sits on the lavender zone: dark number for contrast
            CreateEnergyWidget("BotEnergy", BandBotHeader, ColInk, out botEnergyText);

            botInfoText = MakeText("BotInfo", null, 24, ColInkInfo, TextAlignmentOptions.Right);
            Place(botInfoText.rectTransform, 350f, BandBotHeader, new Vector2(340f, 40f));
        }

        private void BuildPlayerHeader()
        {
            var label = MakeText("KamuLabel", null, 34, ColIvory, TextAlignmentOptions.Left, bold: true);
            label.characterSpacing = 5f;
            Place(label.rectTransform, -330f, BandPlayerHeader, new Vector2(240f, 44f));
            label.text = "KAMU";

            playerPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var pip = MakeImage($"PlayerPip{i}", UiSpriteFactory.Diamond, ColPlayerPipEmpty);
                Place(pip.rectTransform, -170f + i * 46f, BandPlayerHeader, new Vector2(30f, 30f));
                playerPips[i] = pip;
            }

            CreateEnergyWidget("PlayerEnergy", BandPlayerHeader, ColIvory, out playerEnergyText);

            playerDeckText = MakeText("PlayerDeckText", null, 24, ColIvoryDim, TextAlignmentOptions.Right);
            Place(playerDeckText.rectTransform, 350f, BandPlayerHeader, new Vector2(340f, 40f));
        }

        private void CreateEnergyWidget(string baseName, float band, Color numberColor, out TMP_Text numberText)
        {
            var orb = MakeImage(baseName + "Orb", UiSpriteFactory.Circle, ColGoldBright);
            Place(orb.rectTransform, 56f, band, new Vector2(30f, 30f));

            var orbRing = MakeImage(baseName + "OrbRing", UiSpriteFactory.Ring, new Color(0f, 0f, 0f, 0.45f));
            var ringRt = orbRing.rectTransform;
            ringRt.SetParent(orb.rectTransform, false);
            Stretch(ringRt, -2, -2, -2, -2);

            numberText = MakeText(baseName + "Text", null, 30, numberColor, TextAlignmentOptions.Left, bold: true);
            Place(numberText.rectTransform, 104f, band, new Vector2(68f, 40f));
            numberText.text = "0";
        }

        private void BuildActionRow()
        {
            // ---- status pill (own band between the player's active slot and the buttons) ----
            var statusShadow = MakeImage("StatusShadow", UiSpriteFactory.SoftShadow, new Color(0f, 0f, 0f, 0.35f));
            this.statusShadow = statusShadow;
            Place(statusShadow.rectTransform, 0f, BandStatus - 0.003f, new Vector2(1040f, 96f));

            statusPill = MakeImage("StatusPill", UiSpriteFactory.Rounded, ColIvory);
            statusPill.type = Image.Type.Sliced;
            Place(statusPill.rectTransform, 0f, BandStatus, new Vector2(1000f, 58f));

            var statusRing = MakeImage("StatusRing", UiSpriteFactory.RoundOutline, new Color(0.85f, 0.66f, 0.26f, 0.35f));
            statusRing.type = Image.Type.Sliced;
            var ringRt = statusRing.rectTransform;
            ringRt.SetParent(statusPill.rectTransform, false);
            Stretch(ringRt, 2, 2, 2, 2);

            statusText = MakeText("StatusText", statusPill.rectTransform, 27, ColInk, TextAlignmentOptions.Center, bold: true);
            Stretch(statusText.rectTransform, 24, 4, 24, 6);

            // ---- divider ornament row: line / diamond / turn pill / diamond / line ----
            var lineL = MakeImage("DivLineL", null, new Color(1f, 1f, 1f, 0.25f));
            Place(lineL.rectTransform, -376f, BandDivider, new Vector2(320f, 3f));
            var lineR = MakeImage("DivLineR", null, new Color(1f, 1f, 1f, 0.25f));
            Place(lineR.rectTransform, 376f, BandDivider, new Vector2(320f, 3f));
            var dL = MakeImage("DivDiamondL", UiSpriteFactory.Diamond, new Color(0.85f, 0.66f, 0.26f, 0.75f));
            Place(dL.rectTransform, -188f, BandDivider, new Vector2(16f, 16f));
            var dR = MakeImage("DivDiamondR", UiSpriteFactory.Diamond, new Color(0.85f, 0.66f, 0.26f, 0.75f));
            Place(dR.rectTransform, 188f, BandDivider, new Vector2(16f, 16f));

            turnPillBg = MakeImage("TurnPill", UiSpriteFactory.Rounded, ColPanelInk);
            turnPillBg.type = Image.Type.Sliced;
            Place(turnPillBg.rectTransform, 0f, BandDivider, new Vector2(320f, 46f));
            turnPillText = MakeText("TurnPillText", turnPillBg.rectTransform, 24, ColIvory, TextAlignmentOptions.Center, bold: true);
            turnPillText.characterSpacing = 3f;
            Stretch(turnPillText.rectTransform, 8, 2, 8, 2);

            // ---- action buttons ----
            mundurBtn = MakeButton("MundurBtn", "MUNDUR", 27f, ColIvory, ColInk,
                -352f, BandButtons, new Vector2(288f, 88f));
            mundurLabel = mundurBtn.GetComponentInChildren<TMP_Text>();
            mundurBtn.onClick.AddListener(() =>
            {
                if (battle != null)
                    battle.Retreat();
            });

            serangBtn = MakeButton("SerangBtn", "SERANG", 29f, ColGoldBright, Hex(0x3A2506),
                0f, BandButtons, new Vector2(316f, 88f));
            serangBtn.onClick.AddListener(() =>
            {
                if (battle != null)
                    battle.Attack();
            });

            endBtn = MakeButton("EndTurnBtn", "AKHIRI GILIRAN", 25f, ColInkSoft, ColIvory,
                352f, BandButtons, new Vector2(288f, 88f));
            endBtn.onClick.AddListener(() =>
            {
                if (battle != null)
                    battle.EndPlayerTurn();
            });
        }

        private void BuildHand()
        {
            var handGo = new GameObject("Hand", typeof(RectTransform), typeof(HandController));
            hand = handGo.GetComponent<HandController>();
            hand.transform.SetParent(safeRoot, false);
            var rt = (RectTransform)hand.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, BandHand);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1020f, 340f);
            rt.anchoredPosition = Vector2.zero;
            hand.SetClickHandler(OnCardClicked);
        }

        private void BuildOverlays()
        {
            // ---------------- coin flip overlay ----------------
            var coinGo = new GameObject("CoinOverlay", typeof(RectTransform));
            coinOverlay = coinGo;
            var coinRt = (RectTransform)coinGo.transform;
            coinRt.SetParent(safeRoot, false);
            StretchFull(coinRt);
            var coinDim = coinGo.AddComponent<Image>();
            coinDim.color = new Color(0.05f, 0.03f, 0.10f, 0.78f);
            coinDim.raycastTarget = false; // let the player already pick their starting active

            var pedestal = MakeChildImage(coinRt, "Pedestal", UiSpriteFactory.SoftShadow,
                new Color(0f, 0f, 0f, 0.5f), new Vector2(0f, -60f), new Vector2(250f, 46f));

            coinRing = MakeChildImage(coinRt, "CoinRing", UiSpriteFactory.Ring,
                new Color(0.85f, 0.66f, 0.26f, 0.85f), new Vector2(0f, 87f), new Vector2(184f, 184f));

            coinImage = MakeChildImage(coinRt, "Coin", UiSpriteFactory.Circle, ColGoldBright,
                new Vector2(0f, 87f), new Vector2(150f, 150f));

            var coinChip = MakeChildImage(coinRt, "CoinChip", UiSpriteFactory.Rounded,
                new Color(0.11f, 0.07f, 0.18f, 0.55f), new Vector2(0f, -54f), new Vector2(480f, 84f));
            coinChip.type = Image.Type.Sliced;

            var coinDl = MakeChildImage(coinRt, "CoinDiamondL", UiSpriteFactory.Diamond,
                new Color(0.85f, 0.66f, 0.26f, 0.8f), new Vector2(-270f, -54f), new Vector2(20f, 20f));
            var coinDr = MakeChildImage(coinRt, "CoinDiamondR", UiSpriteFactory.Diamond,
                new Color(0.85f, 0.66f, 0.26f, 0.8f), new Vector2(270f, -54f), new Vector2(20f, 20f));

            coinText = MakeText("CoinText", coinRt, 42, ColIvory, TextAlignmentOptions.Center, bold: true);
            Place(coinText.rectTransform, 0f, 0.472f, new Vector2(460f, 76f));
            coinOverlay.SetActive(false);

            // ---------------- game over overlay ----------------
            var overGo = new GameObject("GameOverOverlay", typeof(RectTransform));
            overOverlay = overGo;
            var overRt = (RectTransform)overGo.transform;
            overRt.SetParent(safeRoot, false);
            StretchFull(overRt);
            var overDim = overGo.AddComponent<Image>();
            overDim.color = new Color(0.04f, 0.02f, 0.08f, 0.62f);
            overDim.raycastTarget = true; // block the board behind the result

            var panel = MakeChildImage(overRt, "Panel", UiSpriteFactory.Rounded,
                new Color(0.11f, 0.08f, 0.19f, 0.97f), Vector2.zero, new Vector2(880f, 680f));
            panel.type = Image.Type.Sliced;

            var panelRing = MakeChildImage(overRt, "PanelRing", UiSpriteFactory.RoundOutline,
                new Color(0.85f, 0.66f, 0.26f, 0.45f), Vector2.zero, new Vector2(866f, 666f));
            panelRing.type = Image.Type.Sliced;

            var ribbon = MakeChildImage(overRt, "Ribbon", UiSpriteFactory.Rounded, ColIvory,
                new Vector2(0f, 246f), new Vector2(740f, 118f));
            ribbon.type = Image.Type.Sliced;

            overDiamondL = MakeChildImage(overRt, "OverDiamondL", UiSpriteFactory.Diamond, ColGoldBright,
                new Vector2(-404f, 246f), new Vector2(30f, 30f));
            overDiamondR = MakeChildImage(overRt, "OverDiamondR", UiSpriteFactory.Diamond, ColGoldBright,
                new Vector2(404f, 246f), new Vector2(30f, 30f));

            overText = MakeText("OverText", ribbon.transform, 54, ColInk, TextAlignmentOptions.Center, bold: true);
            overText.characterSpacing = 2f;
            Stretch(overText.rectTransform, 16, 6, 16, 6);

            overScoreText = MakeText("OverScore", overRt, 34, ColIvory, TextAlignmentOptions.Center);
            Place(overScoreText.rectTransform, 0f, 0.556f, new Vector2(880f, 70f));

            againBtn = MakeButton("AgainBtn", "MAIN LAGI", 30f, ColGoldBright, Hex(0x3A2506),
                0f, 0.411f, new Vector2(340f, 96f), overRt);
            againBtn.onClick.AddListener(() =>
            {
                if (starter != null)
                    starter.Restart();
            });
            overOverlay.SetActive(false);
        }

        // ================================================================== refresh

        private void RefreshAll()
        {
            if (battle == null || safeRoot == null)
                return;

            bool setup = battle.Phase == Phase.Setup;
            bool myTurn = battle.Phase == Phase.PlayerTurn;

            // ---- board slots ----
            BindSlot(botActiveView, battle.Bot.Active, CardDisplayMode.Active);
            bool botActiveEmpty = battle.Bot.Active == null;
            botActiveFrame.SetActive(botActiveEmpty);
            if (botActiveEmpty && botActiveLabel != null)
                botActiveLabel.text = "Kosong";
            BindBenchRow(botBenchViews, botBenchFrames, botBenchLabels, battle.Bot.Bench, "Kosong");

            BindSlot(playerActiveView, battle.Player.Active, CardDisplayMode.Active);
            bool pickHint = setup && battle.Player.Active == null;
            playerActiveFrame.SetActive(battle.Player.Active == null);
            playerActiveLabel.text = pickHint ? "PILIH KARTU\nAKTIFMU" : "Kosong";
            BindBenchRow(playerBenchViews, playerBenchFrames, playerBenchLabels, battle.Player.Bench, "Kosong");

            // ---- hand ----
            hand.Sync(battle.Player.Hand);
            if (setup)
            {
                foreach (var v in hand.Views)
                    v.SetSelected(v.Card != null && v.Card.IsBattle);
                hand.Layout(); // lift eligible starting cards
            }
            if (_pendingPlayerDraw && hand.Views.Count > 0)
            {
                _pendingPlayerDraw = false;
                StartCoroutine(UiFx.Punch(hand.Views[hand.Views.Count - 1].Root, 1.2f, 0.35f));
            }

            // ---- header counters ----
            botEnergyText.text = battle.Bot.Energy.ToString();
            playerEnergyText.text = battle.Player.Energy.ToString();
            botInfoText.text = $"DEK {battle.Bot.Deck.Count} \u00b7 TANGAN {battle.Bot.Hand.Count}";
            playerDeckText.text = $"DEK {battle.Player.Deck.Count} \u00b7 TANGAN {battle.Player.Hand.Count}";

            SetPips(botPips, battle.Bot.Points, ColBotPipEmpty);
            SetPips(playerPips, battle.Player.Points, ColPlayerPipEmpty);

            // ---- turn pill + buttons ----
            switch (battle.Phase)
            {
                case Phase.Setup:
                    turnPillText.text = "LEMPAR KOIN";
                    turnPillBg.color = ColPanelInk;
                    break;
                case Phase.PlayerTurn:
                    turnPillText.text = "GILIRANMU";
                    turnPillBg.color = Hex(0x35294F);
                    break;
                case Phase.BotTurn:
                    turnPillText.text = "GILIRAN BOT";
                    turnPillBg.color = ColMaroon;
                    break;
                case Phase.GameOver:
                    turnPillText.text = "PERMAINAN SELESAI";
                    turnPillBg.color = ColPanelInk;
                    break;
            }

            mundurBtn.interactable = myTurn && battle.CanRetreat(out _);
            serangBtn.interactable = myTurn && battle.CanAttack(out _);
            endBtn.interactable = myTurn;

            if (mundurLabel != null)
            {
                if (battle.Player.Active == null)
                    mundurLabel.text = "MUNDUR";
                else if (BattleManager.BenchContainsMbokSumi(battle.Player) && !battle.Player.UsedFreeRetreatViaMbokSumi)
                    mundurLabel.text = "MUNDUR \u00b7GRATIS";
                else
                    mundurLabel.text = $"MUNDUR \u00b7{battle.Player.Active.RetreatCost}";
            }

            // ---- queued cosmetic animations (after rebind so views show new cards) ----
            if (_pendingPromoPlayer != null)
            {
                AnimateSlotIn(FindBoardView(_pendingPromoPlayer), new Vector2(0f, -170f * _k)); // rises from the bench
                _pendingPromoPlayer = null;
            }
            if (_pendingPromoBot != null)
            {
                AnimateSlotIn(FindBoardView(_pendingPromoBot), new Vector2(0f, 170f * _k)); // drops from the bench
                _pendingPromoBot = null;
            }
            if (_pendingBenchPlayer != null)
            {
                AnimateSlotIn(FindBoardView(_pendingBenchPlayer), new Vector2(0f, -200f * _k)); // hand sits below the bench
                _pendingBenchPlayer = null;
            }
            if (_pendingBenchBot != null)
            {
                AnimateSlotIn(FindBoardView(_pendingBenchBot), new Vector2(0f, 220f * _k));
                _pendingBenchBot = null;
            }
            if (_pendingRetreatPlayer != null)
            {
                AnimateSlotIn(FindBoardView(_pendingRetreatPlayer), new Vector2(0f, 140f * _k)); // active sits above the bench
                _pendingRetreatPlayer = null;
            }
            if (_pendingRetreatBot != null)
            {
                AnimateSlotIn(FindBoardView(_pendingRetreatBot), new Vector2(0f, -140f * _k));
                _pendingRetreatBot = null;
            }
        }

        private void BindSlot(CardView view, CardInstance card, CardDisplayMode mode)
        {
            if (view == null)
                return;
            view.Bind(card, mode);
            view.Clicked -= OnCardClicked;
            view.Clicked += OnCardClicked;
        }

        private void BindBenchRow(CardView[] views, GameObject[] frames, TMP_Text[] labels,
            List<CardInstance> bench, string emptyLabel)
        {
            for (int i = 0; i < views.Length; i++)
            {
                var card = i < bench.Count ? bench[i] : null;
                views[i].Bind(card, CardDisplayMode.Bench);
                views[i].Clicked -= OnCardClicked;
                views[i].Clicked += OnCardClicked;
                frames[i].SetActive(card == null);
                if (card == null && labels[i] != null)
                    labels[i].text = emptyLabel;
            }
        }

        private void SetPips(Image[] pips, int points, Color emptyColor)
        {
            for (int i = 0; i < pips.Length; i++)
                pips[i].color = i < points ? ColGoldBright : emptyColor;
        }

        // ================================================================== events

        private void HandleStatus(string msg)
        {
            statusBaseline = msg;
            // during a turn banner the pill shows the banner; the baseline is restored after
            if (turnBannerRoutine == null && statusText != null)
                statusText.text = msg;
        }

        private void OnCardClicked(CardView view)
        {
            var card = view.Card;
            if (card == null || battle == null)
                return;

            // Hand card?
            if (hand.FindView(card) == view)
            {
                if (battle.Phase == Phase.Setup)
                {
                    if (battle.CanSelectStartingActive(card))
                        battle.ConfirmStartingActive(card);
                    else
                        ShakeView(view, "Pilih kartu bertarung sebagai kartu aktif.");
                    return;
                }
                if (battle.Phase == Phase.PlayerTurn)
                {
                    if (card.IsSupport && card.CardId == "TA")
                    {
                        ShakeView(view, "Tumbal Anak tetap di tangan untuk melindungi Babi Ngepet.");
                        return;
                    }
                    if (!battle.PlayCardFromHand(card))
                        ShakeView(view, null); // manager already posted the reason
                    return;
                }
                if (battle.Phase == Phase.BotTurn)
                    ShakeView(view, "Giliran bot sedang berjalan...");
                return;
            }

            // Own bench card: retreat to it.
            for (int i = 0; i < playerBenchViews.Length; i++)
            {
                if (playerBenchViews[i] != view)
                    continue;
                if (!card.IsBattle)
                {
                    ShakeView(view, "Kartu pasif tidak bisa menjadi kartu aktif.");
                    return;
                }
                if (battle.Phase == Phase.PlayerTurn)
                {
                    if (!battle.RetreatTo(card))
                        ShakeView(view, null);
                }
                else
                {
                    ShakeView(view, "Bukan giliranmu.");
                }
                return;
            }
        }

        private void HandleAttack(CardInstance atk, Side side, CardInstance def, int dmg, bool effective)
        {
            StartCoroutine(AttackAnim(atk, side, def, dmg, effective));
        }

        private IEnumerator AttackAnim(CardInstance atk, Side side, CardInstance def, int dmg, bool effective)
        {
            var atkView = FindBoardView(atk);
            var defView = FindBoardView(def);
            float dir = side == Side.Player ? 1f : -1f;

            if (atkView != null)
                yield return Lunge(atkView.Root, dir, 0.30f);
            if (defView != null)
            {
                StartCoroutine(UiFx.Shake(defView.Root, 9f, 0.28f));
                StartCoroutine(UiFx.Punch(defView.Root, 1.05f, 0.2f));
                StartCoroutine(ImpactFlash(defView));
                // OnAttack fires before the manager applies damage, so the tween target
                // is the pre-damage HP minus the incoming damage.
                int targetHp = Mathf.Max(0, def.CurrentHp - dmg);
                defView.TweenHpTo(targetHp, def.MaxHp);
            }

            var anchor = defView != null ? defView.Root : (atkView != null ? atkView.Root : null);
            if (anchor != null)
            {
                string label = dmg <= 0 ? "TERTAHAN" : dmg.ToString();
                var color = dmg <= 0 ? ColLavIdle : (effective ? ColGoldBright : ColIvory);
                SpawnDamagePopup(anchor, label, color);
                if (effective && dmg > 0)
                    SpawnDamagePopup(anchor, "2x!", ColRed, extra: true);
            }
        }

        private IEnumerator Lunge(RectTransform rt, float dir, float duration)
        {
            if (rt == null)
                yield break;
            Vector2 basePos = rt.anchoredPosition;
            float reach = 70f * _k;
            float t = 0f;
            while (t < duration)
            {
                if (rt == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float s = Mathf.Sin(k * Mathf.PI);
                rt.anchoredPosition = basePos + new Vector2(0f, dir * reach * s);
                yield return null;
            }
            if (rt != null)
                rt.anchoredPosition = basePos;
        }

        private IEnumerator ImpactFlash(CardView view)
        {
            var flash = MakeChildImage(view.Root, "ImpactFlash", UiSpriteFactory.Rounded,
                new Color(1f, 1f, 1f, 0.55f), Vector2.zero, view.Root.sizeDelta);
            flash.type = Image.Type.Sliced;
            flash.raycastTarget = false;
            yield return UiFx.FlashOut(flash, 0.22f, 0.55f);
        }

        private void HandleKo(CardInstance card, Side side)
        {
            StartCoroutine(KoAnim(card, side));
        }

        private IEnumerator KoAnim(CardInstance card, Side side)
        {
            var view = FindBoardView(card);
            if (view == null)
                yield break;
            // Compute the slot's safe-root-local center so the ghost (a canvas-space object)
            // appears exactly where the KO'd card was, regardless of canvas size.
            var slotRt = view.Root.parent as RectTransform;
            Vector2 pos = CanvasLocal(slotRt != null ? slotRt : view.Root);
            Vector2 size = view.Root.sizeDelta;
            float angle = view.Root.localRotation.eulerAngles.z;

            // Ghost copy that fades out while the real slot rebinds to the promoted card.
            var ghost = new GameObject("KoGhost", typeof(RectTransform));
            var ghostRt = (RectTransform)ghost.transform;
            ghostRt.SetParent(safeRoot, false);
            ghostRt.anchorMin = ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRt.sizeDelta = size;
            ghostRt.anchoredPosition = pos;
            ghostRt.localRotation = Quaternion.Euler(0f, 0f, angle);

            var backing = ghost.AddComponent<Image>();
            backing.sprite = UiSpriteFactory.Rounded;
            backing.type = Image.Type.Sliced;
            backing.color = new Color(0.12f, 0.09f, 0.19f, 1f);
            backing.raycastTarget = false;
            var art = MakeChildImage(ghostRt, "GhostArt", ArtLibrary.GetCardArt(card.ArtResource), Color.white,
                Vector2.zero, size - new Vector2(10f, 10f));
            art.preserveAspect = true;

            float koDrift = 60f * _k;
            yield return UiFx.Move(ghostRt, pos + new Vector2(0f, side == Side.Bot ? koDrift : -koDrift), 0.5f);
            var c = Color.white;
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - t / 0.45f;
                art.color = c = new Color(1f, 1f, 1f, a);
                backing.color = new Color(0.12f, 0.09f, 0.19f, a);
                yield return null;
            }
            Destroy(ghost);
        }

        private void HandlePromoted(CardInstance card, Side side)
        {
            if (side == Side.Player)
                _pendingPromoPlayer = card;
            else
                _pendingPromoBot = card;
        }

        private void HandleBenchPlay(CardInstance card, Side side)
        {
            if (side == Side.Player)
                _pendingBenchPlayer = card;
            else
                _pendingBenchBot = card;
        }

        private void HandleRetreat(CardInstance card, Side side)
        {
            if (side == Side.Player)
                _pendingRetreatPlayer = card;
            else
                _pendingRetreatBot = card;
        }

        private void HandleScore(Side side, int points)
        {
            var pips = side == Side.Player ? playerPips : botPips;
            int idx = Mathf.Clamp(points - 1, 0, pips.Length - 1);
            StartCoroutine(UiFx.Punch(pips[idx].rectTransform, 1.7f, 0.4f));
            SpawnScorePopup(pips[idx].rectTransform);
        }

        private void HandleDraw(CardInstance card, Side side)
        {
            if (side == Side.Player)
                _pendingPlayerDraw = true;
        }

        private void AnimateSlotIn(CardView view, Vector2 fromOffset)
        {
            if (view == null)
                return;
            StartCoroutine(SlotInAnim(view.Root, fromOffset));
        }

        private IEnumerator SlotInAnim(RectTransform rt, Vector2 fromOffset)
        {
            if (rt == null)
                yield break;
            Vector2 to = rt.anchoredPosition;
            rt.anchoredPosition = to + fromOffset;
            rt.localScale = Vector3.one * 0.75f;
            yield return UiFx.Move(rt, to, 0.28f);
            yield return UiFx.Punch(rt, 1.08f, 0.22f);
        }

        private void ShakeView(CardView view, string extraMessage)
        {
            if (view != null)
                StartCoroutine(UiFx.Shake(view.Root, 10f, 0.3f));
            if (!string.IsNullOrEmpty(extraMessage) && battle != null)
                battle.PostLocalStatus(extraMessage);
        }

        // ============================================== floating popups

        private void SpawnDamagePopup(RectTransform anchor, string text, Color color, bool extra = false)
        {
            var go = new GameObject("DmgPopup", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = extra ? new Vector2(150f, 54f) * _k : new Vector2(300f, 76f) * _k;
            rt.anchoredPosition = CanvasLocal(anchor) +
                (extra ? new Vector2(104f, 118f) * _k : new Vector2(0f, 96f) * _k);

            var chip = go.AddComponent<Image>();
            chip.sprite = UiSpriteFactory.Rounded;
            chip.type = Image.Type.Sliced;
            chip.color = new Color(0.10f, 0.07f, 0.18f, 0.45f);
            chip.raycastTarget = false;

            var tmp = MakeText("Text", rt, (extra ? 30f : 44f) * _k, color, TextAlignmentOptions.Center, bold: true);
            Stretch(tmp.rectTransform, 8, 4, 8, 4);
            tmp.text = text;

            StartCoroutine(PopupAnim(rt, extra));
        }

        private void SpawnScorePopup(RectTransform pip)
        {
            var go = new GameObject("ScorePopup", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(130f, 54f) * _k;
            rt.anchoredPosition = CanvasLocal(pip) + new Vector2(70f, 56f) * _k;

            var chip = go.AddComponent<Image>();
            chip.sprite = UiSpriteFactory.Rounded;
            chip.type = Image.Type.Sliced;
            chip.color = new Color(0.10f, 0.07f, 0.18f, 0.5f);
            chip.raycastTarget = false;

            var tmp = MakeText("Text", rt, 30f * _k, ColGoldBright, TextAlignmentOptions.Center, bold: true);
            Stretch(tmp.rectTransform, 8, 4, 8, 4);
            tmp.text = "+1";

            StartCoroutine(PopupAnim(rt, true));
        }

        private IEnumerator PopupAnim(RectTransform rt, bool small)
        {
            float t = 0f;
            var tmp = rt.GetComponentInChildren<TMP_Text>();
            var chip = rt.GetComponent<Image>();
            Vector2 start = rt.anchoredPosition;
            Vector2 end = start + new Vector2(0f, small ? 56f : 84f);
            while (t < 0.9f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.9f);
                rt.anchoredPosition = Vector2.Lerp(start, end, k);
                float a = 1f - k * k;
                if (chip != null)
                    chip.color = WithA(chip.color, Mathf.Clamp01(a * 0.48f));
                if (tmp != null)
                    tmp.color = WithA(tmp.color, a);
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        // ================================================================== overlays

        private void ShowCoin(bool playerFirst)
        {
            if (coinOverlay == null)
                return;
            coinOverlay.SetActive(true);
            // the coin sits on the status pill's band - hide the pill while the
            // flip presentation runs, restore it when the coin fades out.
            if (statusPill != null)
                statusPill.gameObject.SetActive(false);
            if (statusShadow != null)
                statusShadow.gameObject.SetActive(false);
            coinImage.color = ColGoldBright;  // reset alpha AND tint (wiped by the previous fade)
            coinRing.color = new Color(0.85f, 0.66f, 0.26f, 0.85f);
            coinText.text = playerFirst ? "KAMU PERTAMA!" : "BOT PERTAMA!";
            coinText.color = playerFirst ? ColGoldBright : ColRed;
            StartCoroutine(CoinAnim(playerFirst));
        }

        private IEnumerator CoinAnim(bool playerFirst)
        {
            // Coin "flips" by squashing on X, then the result fades out.
            float t = 0f;
            float flipDuration = 0.8f;
            Vector3 start = coinImage.rectTransform.localScale;
            while (t < flipDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / flipDuration;
                float squash = Mathf.Abs(Mathf.Cos(k * Mathf.PI * 3f));
                coinImage.rectTransform.localScale = new Vector3(Mathf.Max(0.06f, squash), 1f, 1f);
                yield return null;
            }
            coinImage.rectTransform.localScale = start;
            yield return UiFx.Punch(coinImage.rectTransform, 1.35f, 0.4f);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return UiFx.Fade(coinImage, 0f, 0.4f);
            yield return UiFx.Fade(coinRing, 0f, 0.4f);
            yield return UiFx.Fade(coinText, 0f, 0.35f);
            coinOverlay.SetActive(false);
            if (statusPill != null)
                statusPill.gameObject.SetActive(true);
            if (statusShadow != null)
                statusShadow.gameObject.SetActive(true);
            coinImage.color = ColGoldBright;
            coinRing.color = new Color(0.85f, 0.66f, 0.26f, 0.85f);
            coinText.color = playerFirst ? ColGoldBright : ColRed;

            if (battle != null && battle.Phase == Phase.Setup)
                battle.PostLocalStatus("Pilih kartu aktif dari tanganmu!");
        }

        private void ShowTurnBanner(Side side)
        {
            if (turnBannerRoutine != null)
                StopCoroutine(turnBannerRoutine);
            turnBannerRoutine = StartCoroutine(TurnBannerAnim(side));

            // Launch the bot's turn driver.
            if (side == Side.Bot && bot != null)
                StartCoroutine(bot.RunBotTurn());
        }

        /// <summary>
        /// The turn banner now lives in the status pill: it highlights gold (player) or
        /// maroon (bot), then reverts and shows the latest status line again.
        /// </summary>
        private IEnumerator TurnBannerAnim(Side side)
        {
            if (statusPill == null)
                yield break;
            bool mine = side == Side.Player;
            statusPill.color = mine ? ColGoldBright : ColMaroon;
            statusText.text = mine ? "GILIRANMU" : "GILIRAN BOT";
            statusText.color = mine ? ColInk : ColIvory;
            yield return UiFx.Punch(statusPill.rectTransform, 1.07f, 0.3f);
            yield return new WaitForSecondsRealtime(0.9f);
            if (statusPill == null || statusText == null)
                yield break;
            statusPill.color = ColIvory;
            statusText.color = ColInk;
            statusText.text = statusBaseline;
            turnBannerRoutine = null;
        }

        private void ShowGameOver(Side winner)
        {
            if (overOverlay == null)
                return;
            overOverlay.SetActive(true);
            bool playerWins = winner == Side.Player;
            overText.text = playerWins ? "KAMU MENANG!" : "BOT MENANG!";
            var accent = playerWins ? ColGoldBright : ColRed;
            overDiamondL.color = accent;
            overDiamondR.color = accent;
            overScoreText.text = $"Skor akhir  {battle.Player.Points} - {battle.Bot.Points}";
        }

        // ================================================================== helpers

        private CardView FindBoardView(CardInstance card)
        {
            if (card == null)
                return null;
            if (botActiveView.Card == card)
                return botActiveView;
            if (playerActiveView.Card == card)
                return playerActiveView;
            foreach (var v in botBenchViews)
                if (v.Card == card)
                    return v;
            foreach (var v in playerBenchViews)
                if (v.Card == card)
                    return v;
            return null;
        }

        /// <summary>Canvas-local position (in safe-root space) of a board transform.</summary>
        private Vector2 CanvasLocal(RectTransform target)
        {
            if (safeRoot == null || target == null)
                return Vector2.zero;
            return (Vector2)safeRoot.InverseTransformPoint(target.position);
        }

        private static Color WithA(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        // ---------------------------------------------------------------- builders

        private RectTransform Node(string name, float x, float yPercent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            // Percent-anchored so the portrait layout stays coherent in any Game View aspect.
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, yPercent);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, 0f);
            return rt;
        }

        private Image MakeImage(string name, Sprite sprite, Color color, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent != null ? parent : safeRoot, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Image MakeChildImage(RectTransform parent, string name, Sprite sprite, Color color,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private GameObject MakeSlotFrame(RectTransform slot, string label, float fontSize, out TMP_Text labelText)
        {
            var go = new GameObject("EmptyFrame", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            StretchFull(rt, 0f);
            var img = go.GetComponent<Image>();
            img.sprite = UiSpriteFactory.Rounded;
            img.type = Image.Type.Sliced; // crisp corner radius at any slot size
            img.color = new Color(0f, 0f, 0f, 0.30f);
            img.raycastTarget = false;

            var innerRing = MakeImage("InnerRing", UiSpriteFactory.RoundOutline, new Color(1f, 1f, 1f, 0.16f));
            innerRing.type = Image.Type.Sliced;
            var ringRt = innerRing.rectTransform;
            ringRt.SetParent(rt, false);
            Stretch(ringRt, 5, 5, 5, 5);

            labelText = MakeText("EmptyLabel", rt, fontSize, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center, bold: true);
            Stretch(labelText.rectTransform, 6, 4, 6, 4);
            labelText.text = label;
            return go;
        }

        private Button MakeButton(string name, string label, float fontSize, Color bg, Color fg,
            float x, float yPercent, Vector2 size, Transform parentOverride = null)
        {
            Transform parent = parentOverride != null ? parentOverride : safeRoot;

            var shadow = MakeImage(name + "Shadow", UiSpriteFactory.SoftShadow, new Color(0f, 0f, 0f, 0.32f));
            Place(shadow.rectTransform, x, yPercent, size + new Vector2(26f, 30f));
            shadow.rectTransform.anchoredPosition += new Vector2(0f, -7f);

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, yPercent);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, 0f);
            var img = go.GetComponent<Image>();
            img.sprite = UiSpriteFactory.Rounded;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var btn = go.GetComponent<Button>();

            var text = MakeText("Label", rt, fontSize, fg, TextAlignmentOptions.Center, bold: true);
            Stretch(text.rectTransform, 8, 6, 8, 6);
            text.text = label;
            text.characterSpacing = 2f;
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = 19f;
            text.fontSizeMax = fontSize;

            go.AddComponent<ButtonFx>();

            var colors = btn.colors;
            colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.35f);
            btn.colors = colors;
            return btn;
        }

        private TMP_Text MakeText(string name, Transform parent, float fontSize, Color color,
            TextAlignmentOptions align, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent != null ? parent : safeRoot, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = TmpFontService.Get();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            if (bold)
                tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private void Place(RectTransform rt, float x, float yPercent, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, yPercent);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private void StretchFull(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }

        private static Color Hex(int rgba)
        {
            return new Color32((byte)(rgba >> 16 & 0xFF), (byte)(rgba >> 8 & 0xFF), (byte)(rgba & 0xFF), 0xFF);
        }
    }
}
