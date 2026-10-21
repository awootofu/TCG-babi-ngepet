using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// Builds the whole landscape battle screen in code (1920x1080 reference) and renders
    /// BattleManager state. Everything lives under a safe-area root and uses percentage
    /// anchors, the palette follows the folklore direction (deep purple / muted lavender /
    /// warm ivory / subtle gold), and all animations here are cosmetic - battle state is
    /// authoritative.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        private const float RefH = 1080f;

        // ---- landscape bands (fraction of reference height, bottom-up) ----
        private const float BandPlayerInfo = 0.4630f;     // 500px  player label / score
        private const float BandPlayerDeck = 0.4213f;     // 455px  player deck line
        private const float BandHand = 0.1185f;           // 128px  hand fan centre
        private const float BandBenchPlayer = 0.3565f;    // 385px
        private const float BandActivePlayer = 0.3750f;   // 405px
        private const float BandStatus = 0.5694f;         // 615px  control bar
        private const float BandButtons = 0.5694f;        // 615px
        private const float BandDivider = 0.6111f;        // 660px  bot-zone edge
        private const float BandActiveBot = 0.7870f;      // 850px
        private const float BandBenchBot = 0.7685f;       // 830px
        private const float BandBotHeader = 0.9583f;      // 1035px
        private const float BandBotDeck = 0.9167f;        // 990px  deck count under BOT

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
        private BotHandFan botHandFan;
        private RectTransform botDeckStack, playerDeckStack;
        private DeckPileView botDeckPile, playerDeckPile;
        private RectTransform playerDrawFxLayer;
        private CardInspectPreview handInspect;
        private CardView _inspectSourceView;   // opponent board view feeding the inspect preview
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
        private TMP_Text mundurLabel, serangLabel;
        private RectTransform transformActionRoot;
        private Button transformBtn;
        private GameObject transformOverlay;
        private CanvasGroup transformGroup;
        private CardView[] transformChoiceViews;
        private TMP_Text transformErrorText;
        private Coroutine transformFade;
        private GameObject coinOverlay;
        private Image coinImage, coinRing;
        private Image coinGlow, coinGlow2;
        private TMP_Text coinText;
        private GameObject overOverlay;
        private CanvasGroup overGroup;
        private RectTransform overRibbonRt;
        private TMP_Text overText, overScoreText;
        private Image overDiamondL, overDiamondR;
        private Button againBtn;

        // pending cosmetic animations applied after the next full refresh
        private CardInstance _pendingPromoPlayer, _pendingPromoBot;
        private CardInstance _pendingBenchPlayer, _pendingBenchBot;
        private CardInstance _pendingRetreatPlayer, _pendingRetreatBot;
        private readonly List<CardInstance> _pendingPlayerDraws = new List<CardInstance>();
        private readonly HashSet<CardInstance> _pendingPlayerDrawSet = new HashSet<CardInstance>();

        private Coroutine turnBannerRoutine;
        private BattleManager _boundManager;

        // responsive layout scaling (k = safeRoot height / reference height)
        private float _k;   // 0 forces the first apply
        private float _lastLayoutWidth;
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
            FinishDrawAnimations();
            _pendingPromoPlayer = _pendingPromoBot = null;
            _pendingBenchPlayer = _pendingBenchBot = null;
            _pendingRetreatPlayer = _pendingRetreatBot = null;
            statusBaseline = "";
            if (botHandFan != null)
                botHandFan.ResetVisuals();

            if (canvasRt == null)
                BuildAll();

            CloseTransformOverlayImmediate();
            Subscribe(mgr);
            RefreshAll();
        }

        private void Subscribe(BattleManager m)
        {
            m.OnStatus += HandleStatus;
            m.OnStateChanged += RefreshAll;
            m.OnEnergyChanged += HandleEnergyChanged;
            m.OnCoinFlipped += ShowCoin;
            m.OnTurnStarted += ShowTurnBanner;
            m.OnAttack += HandleAttack;
            m.OnKnockedOut += HandleKo;
            m.OnPromoted += HandlePromoted;
            m.OnCardPlayedToBench += HandleBenchPlay;
            m.OnRetreated += HandleRetreat;
            m.OnScoreChanged += HandleScore;
            m.OnDrawn += HandleDraw;
            m.OnTransformed += HandleTransformed;
            m.OnSupportDestroyed += HandleSupportDestroyed;
            m.OnGameOver += ShowGameOver;
        }

        private void Unsubscribe(BattleManager m)
        {
            m.OnStatus -= HandleStatus;
            m.OnStateChanged -= RefreshAll;
            m.OnEnergyChanged -= HandleEnergyChanged;
            m.OnCoinFlipped -= ShowCoin;
            m.OnTurnStarted -= ShowTurnBanner;
            m.OnAttack -= HandleAttack;
            m.OnKnockedOut -= HandleKo;
            m.OnPromoted -= HandlePromoted;
            m.OnCardPlayedToBench -= HandleBenchPlay;
            m.OnRetreated -= HandleRetreat;
            m.OnScoreChanged -= HandleScore;
            m.OnDrawn -= HandleDraw;
            m.OnTransformed -= HandleTransformed;
            m.OnSupportDestroyed -= HandleSupportDestroyed;
            m.OnGameOver -= ShowGameOver;
        }

        public void Unbind(BattleManager m)
        {
            if (m == null)
                return;
            Unsubscribe(m);
            if (_boundManager == m)
                _boundManager = null;
            FinishDrawAnimations();
            CloseTransformOverlayImmediate();
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
            FinishDrawAnimations();
            if (transformFade != null)
            {
                StopCoroutine(transformFade);
                transformFade = null;
            }
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
            BuildDeckPiles();
            BuildStaminaBadge();
            BuildScorePips();
            BuildActionRow();
            BuildTransformAction();
            BuildBotHandFan();
            BuildHand();
            BuildDrawFxLayer();
            BuildOverlays();
            BuildTransformOverlay();
            BuildInspectPreview();   // last sibling: drawn over everything, never intercepts input

            // capture the static layout once so it can be rescaled when the canvas size
            // changes (card views capture themselves in CardView.ApplyScale)
            UiRescaler.CaptureTreeExcludingCards(safeRoot, _registry);
        }

        private void Update()
        {
            if (safeRoot == null)
                return;
            if (transformOverlay != null && transformOverlay.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                CloseTransformOverlay();
            float h = safeRoot.rect.height;
            if (h <= 0f)
                return;

            // Height-matched CanvasScaler keeps the UI at 1080 units tall, but the useful
            // hand width still changes with the window aspect. Clamp the fan to the
            // available middle of the screen (and leave the side badges their margin).
            float w = safeRoot.rect.width;
            if (Mathf.Abs(w - _lastLayoutWidth) > 0.5f)
            {
                _lastLayoutWidth = w;
                if (hand != null)
                    hand.SetViewportWidth(Mathf.Min(1450f, Mathf.Max(640f, w - 180f)));
            }

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
            if (botHandFan != null)
                botHandFan.SetScale(k);
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
            if (transformChoiceViews != null)
                foreach (var v in transformChoiceViews)
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
            pr.anchorMin = new Vector2(0f, BandDivider);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.offsetMin = new Vector2(0f, 0f);
            pr.offsetMax = new Vector2(0f, 46f);
        }

        /// <summary>Spot pool + ornament ring under each active slot (focal-point staging).</summary>
        private void BuildStageOrnaments()
        {
            // player stage (deep zone): warm ivory pool, gold ring, kawung-style diamonds
            // Keep the decorative shell clear of the first bench slot (slot left edge
            // sits at x=654px); 320px leaves a 14px breathing gap at 1080p.
            AddStage(-480f, BandActivePlayer, new Vector2(320f, 330f), new Vector2(440f, 300f),
                new Color(1f, 0.86f, 0.62f, 0.07f), new Color(0.85f, 0.66f, 0.26f, 0.42f));

            // bot stage (lavender zone): ink pool with a deep gold ring
            AddStage(-480f, BandActiveBot, new Vector2(310f, 320f), new Vector2(430f, 290f),
                new Color(0.14f, 0.11f, 0.21f, 0.07f), new Color(0.55f, 0.42f, 0.15f, 0.42f));
        }

        private void AddStage(float x, float yPercent, Vector2 ringSize, Vector2 poolSize, Color poolColor, Color ringColor)
        {
            var pool = MakeImage("StagePool", UiSpriteFactory.SoftShadow, poolColor);
            Place(pool.rectTransform, x, yPercent, poolSize);

            var ring = MakeImage("StageRing", UiSpriteFactory.RoundOutline, ringColor);
            ring.type = Image.Type.Sliced;
            Place(ring.rectTransform, x, yPercent, ringSize);

            // base line: a warm stage edge under each active slot (the focal marker)
            var baseLine = MakeImage("StageBase", UiSpriteFactory.Rounded, new Color(0.85f, 0.66f, 0.26f, 0.62f));
            baseLine.type = Image.Type.Sliced;
            Place(baseLine.rectTransform, x, yPercent, new Vector2(ringSize.x * 0.46f, 6f));
            baseLine.rectTransform.anchoredPosition = new Vector2(0f, -ringSize.y * 0.5f);

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
            CreateBenchRow("BotBenchSlot", BandBenchBot, -220f, 220f, new Vector2(172f, 230f),
                out botBenchSlots, out botBenchFrames, out botBenchLabels, out botBenchViews, "BotBench");

            var botActiveSlot = Node("BotActiveSlot", -480f, BandActiveBot, new Vector2(224f, 300f));
            botActiveFrame = MakeSlotFrame(botActiveSlot, "Kosong", 22f, out botActiveLabel);
            botActiveView = CardView.Create(botActiveSlot, new Vector2(224f, 300f), "BotActive");

            CreateBenchRow("PlayerBenchSlot", BandBenchPlayer, -220f, 220f, new Vector2(172f, 230f),
                out playerBenchSlots, out playerBenchFrames, out playerBenchLabels, out playerBenchViews, "PlayerBench");

            var playerActiveSlot = Node("PlayerActiveSlot", -480f, BandActivePlayer, new Vector2(224f, 300f));
            playerActiveFrame = MakeSlotFrame(playerActiveSlot, "PILIH KARTU\nAKTIFMU", 22f, out playerActiveLabel);
            playerActiveView = CardView.Create(playerActiveSlot, new Vector2(224f, 300f), "PlayerActive");
        }

        private void CreateBenchRow(string slotName, float band, float startX, float stepX, Vector2 size,
            out RectTransform[] slots, out GameObject[] frames, out TMP_Text[] labels,
            out CardView[] views, string viewPrefix)
        {
            slots = new RectTransform[3];
            frames = new GameObject[3];
            labels = new TMP_Text[3];
            views = new CardView[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = Node($"{slotName}{i}", startX + i * stepX, band, size);
                slots[i] = slot;
                frames[i] = MakeSlotFrame(slot, "Kosong", 18f, out labels[i]);
                views[i] = CardView.Create(slot, size, $"{viewPrefix}{i}");
            }
        }

        private void BuildBotHeader()
        {
            var group = NodeLeft("BotHeader", 80f, BandBotHeader, new Vector2(480f, 44f));
            var label = MakeText("BotLabel", group, 26, ColInk, TextAlignmentOptions.Left, bold: true);
            label.characterSpacing = 4f;
            PlaceLocal(label.rectTransform, 0f, 0f, new Vector2(160f, 40f));
            label.text = "BOT";

            // Score pips live beside the right-rail stamina badges (BuildScorePips),
            // keeping the BOT header quiet.

            // Deck count only - the hand itself is the face-down fan, never a number.
            botInfoText = MakeText("BotInfo", null, 18, ColInkInfo, TextAlignmentOptions.Left);
            PlaceLeft(botInfoText.rectTransform, 80f, BandBotDeck, new Vector2(240f, 30f));
        }

        private void BuildPlayerHeader()
        {
            var group = NodeLeft("PlayerHeader", 80f, BandPlayerInfo, new Vector2(480f, 44f));
            var label = MakeText("KamuLabel", group, 26, ColIvory, TextAlignmentOptions.Left, bold: true);
            label.characterSpacing = 4f;
            PlaceLocal(label.rectTransform, 0f, 0f, new Vector2(160f, 40f));
            label.text = "KAMU";

            // Score pips live beside the right-rail stamina badge (BuildScorePips).

            playerDeckText = MakeText("PlayerDeckText", null, 20, ColIvoryDim, TextAlignmentOptions.Left);
            PlaceLeft(playerDeckText.rectTransform, 80f, BandPlayerDeck, new Vector2(320f, 32f));
        }

        /// <summary>
        /// Minimal right-rail stamina: a diamond fraction for the player and a smaller
        /// secondary badge for the bot. The denominator comes from BattleRules.EnergyCap.
        /// </summary>
        private void BuildStaminaBadge()
        {
            var playerRoot = NodeRight("PlayerStamina", 110f, 0.435f, new Vector2(148f, 148f));
            var playerOuter = MakeChildImage(playerRoot, "Outer", UiSpriteFactory.Diamond,
                new Color(0.85f, 0.66f, 0.26f, 0.88f), Vector2.zero, new Vector2(146f, 146f));
            playerOuter.raycastTarget = false;
            var playerInner = MakeChildImage(playerRoot, "Fill", UiSpriteFactory.Diamond,
                new Color(0.10f, 0.07f, 0.18f, 0.97f), Vector2.zero, new Vector2(134f, 134f));
            playerInner.raycastTarget = false;

            playerEnergyText = MakeText("PlayerStaminaText", playerRoot, 34, ColIvory,
                TextAlignmentOptions.Center, bold: true);
            Stretch(playerEnergyText.rectTransform, 10f, 24f, 10f, 16f);
            playerEnergyText.text = "0/" + BattleRules.EnergyCap;

            var playerCaption = MakeText("PlayerStaminaCaption", playerRoot, 10, ColGold,
                TextAlignmentOptions.Center, bold: true);
            playerCaption.characterSpacing = 1.5f;
            PlaceCenter(playerCaption.rectTransform, new Vector2(0f, -44f), new Vector2(120f, 16f));
            playerCaption.text = "STAMINA";

            var botRoot = NodeRight("BotStamina", 110f, 0.7800f, new Vector2(74f, 74f));
            var botOuter = MakeChildImage(botRoot, "Outer", UiSpriteFactory.Diamond,
                new Color(0.55f, 0.42f, 0.15f, 0.72f), Vector2.zero, new Vector2(72f, 72f));
            botOuter.raycastTarget = false;
            var botInner = MakeChildImage(botRoot, "Fill", UiSpriteFactory.Diamond,
                new Color(0.10f, 0.07f, 0.18f, 0.94f), Vector2.zero, new Vector2(64f, 64f));
            botInner.raycastTarget = false;

            botEnergyText = MakeText("BotStaminaText", botRoot, 15, ColIvory,
                TextAlignmentOptions.Center, bold: true);
            Stretch(botEnergyText.rectTransform, 4f, 13f, 4f, 8f);
            botEnergyText.text = "0/" + BattleRules.EnergyCap;

            var botCaption = MakeText("BotStaminaCaption", botRoot, 7, ColGold,
                TextAlignmentOptions.Center, bold: true);
            PlaceCenter(botCaption.rectTransform, new Vector2(0f, -22f), new Vector2(64f, 10f));
            botCaption.text = "BOT";
            botCaption.characterSpacing = 0.8f;
        }

        /// <summary>
        /// Three score diamonds per side, parked immediately left of that side's
        /// stamina badge. This clears the left headers/deck lines and keeps score and
        /// stamina in one right-rail cluster for both the player and the bot.
        /// </summary>
        private void BuildScorePips()
        {
            var botGroup = NodeRight("BotScorePips", 230f, 0.7800f, new Vector2(140f, 44f));
            botPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -40f + i * 40f;
                var socket = MakeImage($"BotPipSocket{i}", UiSpriteFactory.Ring,
                    new Color(0f, 0f, 0f, 0.22f), botGroup);
                PlaceCenter(socket.rectTransform, new Vector2(x, 0f), new Vector2(32f, 32f));
                var pip = MakeImage($"BotPip{i}", UiSpriteFactory.Diamond, ColBotPipEmpty, botGroup);
                PlaceCenter(pip.rectTransform, new Vector2(x, 0f), new Vector2(24f, 24f));
                botPips[i] = pip;
            }

            var playerGroup = NodeRight("PlayerScorePips", 265f, 0.435f, new Vector2(140f, 44f));
            playerPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -40f + i * 40f;
                var socket = MakeImage($"PlayerPipSocket{i}", UiSpriteFactory.Ring,
                    new Color(1f, 1f, 1f, 0.16f), playerGroup);
                PlaceCenter(socket.rectTransform, new Vector2(x, 0f), new Vector2(32f, 32f));
                var pip = MakeImage($"PlayerPip{i}", UiSpriteFactory.Diamond, ColPlayerPipEmpty, playerGroup);
                PlaceCenter(pip.rectTransform, new Vector2(x, 0f), new Vector2(24f, 24f));
                playerPips[i] = pip;
            }
        }

        private void BuildActionRow()
        {
            // ---- one compact control bar between the two battlefields ----
            var statusShadow = MakeImage("StatusShadow", UiSpriteFactory.SoftShadow, new Color(0f, 0f, 0f, 0.35f));
            this.statusShadow = statusShadow;
            Place(statusShadow.rectTransform, -410f, BandStatus - 0.003f, new Vector2(600f, 52f));

            statusPill = MakeImage("StatusPill", UiSpriteFactory.Rounded, ColIvory);
            statusPill.type = Image.Type.Sliced;
            Place(statusPill.rectTransform, -410f, BandStatus, new Vector2(560f, 44f));

            var statusRing = MakeImage("StatusRing", UiSpriteFactory.RoundOutline, new Color(0.85f, 0.66f, 0.26f, 0.35f));
            statusRing.type = Image.Type.Sliced;
            var ringRt = statusRing.rectTransform;
            ringRt.SetParent(statusPill.rectTransform, false);
            Stretch(ringRt, 2, 2, 2, 2);

            statusText = MakeText("StatusText", statusPill.rectTransform, 22, ColInk, TextAlignmentOptions.Center, bold: true);
            Stretch(statusText.rectTransform, 18, 3, 18, 4);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 14f;
            statusText.fontSizeMax = 22f;

            // Bot-zone edge: a thin gold-topped divider spanning the desktop width.
            var divLine = MakeImage("DivLine", null, new Color(0.85f, 0.66f, 0.26f, 0.38f));
            var dl = divLine.rectTransform;
            dl.anchorMin = new Vector2(0f, BandDivider);
            dl.anchorMax = new Vector2(1f, BandDivider);
            dl.pivot = new Vector2(0.5f, 0.5f);
            dl.offsetMin = new Vector2(80f, -1.5f);
            dl.offsetMax = new Vector2(-80f, 1.5f);
            var divDiamond = MakeImage("DivDiamond", UiSpriteFactory.Diamond, new Color(0.85f, 0.66f, 0.26f, 0.75f));
            Place(divDiamond.rectTransform, 0f, BandDivider, new Vector2(14f, 14f));

            turnPillBg = MakeImage("TurnPill", UiSpriteFactory.Rounded, ColPanelInk);
            turnPillBg.type = Image.Type.Sliced;
            Place(turnPillBg.rectTransform, -775f, BandStatus, new Vector2(170f, 34f));
            turnPillText = MakeText("TurnPillText", turnPillBg.rectTransform, 16, ColIvory, TextAlignmentOptions.Center, bold: true);
            turnPillText.characterSpacing = 1.2f;
            Stretch(turnPillText.rectTransform, 6, 2, 6, 2);
            turnPillText.enableAutoSizing = true;
            turnPillText.fontSizeMin = 11f;
            turnPillText.fontSizeMax = 16f;

            // ---- action buttons ----
            mundurBtn = MakeButton("MundurBtn", "MUNDUR", 22f, ColIvory, ColInk,
                0f, BandButtons, new Vector2(170f, 60f));
            mundurLabel = mundurBtn.GetComponentInChildren<TMP_Text>();
            mundurBtn.onClick.AddListener(() =>
            {
                if (battle != null)
                    battle.Retreat();
            });

            serangBtn = MakeButton("SerangBtn", "SERANG", 24f, ColGoldBright, Hex(0x3A2506),
                200f, BandButtons, new Vector2(180f, 60f));
            serangLabel = serangBtn.GetComponentInChildren<TMP_Text>();
            serangBtn.onClick.AddListener(() =>
            {
                if (battle != null)
                    battle.Attack();
            });

            endBtn = MakeButton("EndTurnBtn", "AKHIRI GILIRAN", 20f, ColInkSoft, ColIvory,
                430f, BandButtons, new Vector2(220f, 60f));
            endBtn.onClick.AddListener(() =>
            {
                if (battle != null)
                    battle.EndPlayerTurn();
            });
        }

        /// <summary>
        /// Contextual Pak Karta action. It only appears while the transformation is
        /// legal, in the empty space to the right of the player's bench row, so the
        /// normal 1920x1080 action row and board layout stay untouched.
        /// </summary>
        private void BuildTransformAction()
        {
            transformActionRoot = Node("TransformAction", 420f, BandActivePlayer, new Vector2(200f, 80f));
            transformBtn = MakeButton("TransformBtn", "TRANSFORMASI", 19f, ColGoldBright, Hex(0x3A2506),
                0f, 0.5f, new Vector2(190f, 54f), transformActionRoot);
            transformBtn.onClick.AddListener(OpenTransformOverlay);

            transformActionRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Visible deck piles for both sides. The bot's pile stays in the upper-right
        /// free space; the player's pile sits left of the active row, close to the hand
        /// so every draw has a readable origin. Each pile carries its own live count.
        /// </summary>
        private void BuildDeckPiles()
        {
            var botGo = new GameObject("BotDeckStack", typeof(RectTransform));
            var botRt = (RectTransform)botGo.transform;
            botRt.SetParent(safeRoot, false);
            botRt.anchorMin = botRt.anchorMax = new Vector2(1f, 0.9500f);
            botRt.pivot = new Vector2(0.5f, 0.5f);
            botRt.sizeDelta = new Vector2(74f, 100f);
            botRt.anchoredPosition = new Vector2(-130f, 0f);
            botDeckStack = botRt;
            botDeckPile = botGo.AddComponent<DeckPileView>();
            botDeckPile.Build();

            var playerGo = new GameObject("PlayerDeckStack", typeof(RectTransform));
            var playerRt = (RectTransform)playerGo.transform;
            playerRt.SetParent(safeRoot, false);
            playerRt.anchorMin = playerRt.anchorMax = new Vector2(0f, 0.3500f);
            playerRt.pivot = new Vector2(0.5f, 0.5f);
            playerRt.sizeDelta = new Vector2(74f, 100f);
            playerRt.anchoredPosition = new Vector2(100f, 0f);
            playerDeckStack = playerRt;
            playerDeckPile = playerGo.AddComponent<DeckPileView>();
            playerDeckPile.Build();
        }

        /// <summary>
        /// Opponent hand representation: generic face-down backs only. The fan sits in
        /// the top-right free space, clear of the bot's active/bench cards, deck pile,
        /// secondary stamina badge and the central action bar.
        /// </summary>
        private void BuildBotHandFan()
        {
            var fanGo = new GameObject("BotHandFan", typeof(RectTransform));
            var fanRt = (RectTransform)fanGo.transform;
            fanRt.SetParent(safeRoot, false);
            fanRt.anchorMin = fanRt.anchorMax = new Vector2(1f, 0.9259f);
            fanRt.pivot = new Vector2(0.5f, 0.5f);
            fanRt.sizeDelta = new Vector2(440f, 200f);
            fanRt.anchoredPosition = new Vector2(-410f, 0f);

            botHandFan = fanGo.AddComponent<BotHandFan>();
            botHandFan.Initialize(fanRt, botDeckStack, card =>
            {
                var view = FindBoardView(card);
                if (view != null)
                    return view.Root;
                return statusPill != null ? statusPill.rectTransform : null;
            });
        }

        /// <summary>
        /// Transparent layer above the hand for player draw ghosts. It never intercepts
        /// input and can be cleared wholesale on restart or game over.
        /// </summary>
        private void BuildDrawFxLayer()
        {
            var go = new GameObject("PlayerDrawFx", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            StretchFull(rt, 0f);
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            playerDrawFxLayer = rt;
        }

        private void BuildHand()
        {
            var handGo = new GameObject("Hand", typeof(RectTransform), typeof(HandController));
            hand = handGo.GetComponent<HandController>();
            hand.transform.SetParent(safeRoot, false);
            var rt = (RectTransform)hand.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, BandHand);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1500f, 320f);
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

            // halo rings: tinted stock sprites (SoftShadow's texture is black, so it can't
            // be tinted gold - the ring sprite can)
            coinGlow2 = MakeChildImage(coinRt, "CoinGlowOuter", UiSpriteFactory.Ring,
                new Color(0.95f, 0.76f, 0.35f, 0.14f), new Vector2(0f, 87f), new Vector2(312f, 312f));
            coinGlow = MakeChildImage(coinRt, "CoinGlow", UiSpriteFactory.Ring,
                new Color(0.95f, 0.76f, 0.35f, 0.28f), new Vector2(0f, 87f), new Vector2(250f, 250f));

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

            coinText = MakeText("CoinText", coinRt, 32, ColIvory, TextAlignmentOptions.Center, bold: true);
            Place(coinText.rectTransform, 0f, 0.45f, new Vector2(460f, 64f));
            coinOverlay.SetActive(false);

            // ---------------- game over overlay ----------------
            var overGo = new GameObject("GameOverOverlay", typeof(RectTransform));
            overOverlay = overGo;
            var overRt = (RectTransform)overGo.transform;
            overRt.SetParent(safeRoot, false);
            StretchFull(overRt);

            // The board/hand use nested canvases, so a sibling-only overlay can still be
            // drawn behind them. Put the result modal on its own high-sorting canvas.
            var overCanvas = overGo.AddComponent<Canvas>();
            overCanvas.overrideSorting = true;
            overCanvas.sortingOrder = 5000;
            overGo.AddComponent<GraphicRaycaster>();

            var overDim = overGo.AddComponent<Image>();
            overDim.color = new Color(0.04f, 0.02f, 0.08f, 0.72f);
            overDim.raycastTarget = true; // block the board behind the result

            overGroup = overGo.AddComponent<CanvasGroup>();
            overGroup.alpha = 0f; // fades in on ShowGameOver

            var panel = MakeChildImage(overRt, "Panel", UiSpriteFactory.Rounded,
                new Color(0.11f, 0.08f, 0.19f, 1f), Vector2.zero, new Vector2(820f, 460f));
            panel.type = Image.Type.Sliced;

            var panelRing = MakeChildImage(overRt, "PanelRing", UiSpriteFactory.RoundOutline,
                new Color(0.85f, 0.66f, 0.26f, 0.45f), Vector2.zero, new Vector2(806f, 446f));
            panelRing.type = Image.Type.Sliced;

            var ribbon = MakeChildImage(overRt, "Ribbon", UiSpriteFactory.Rounded, ColIvory,
                new Vector2(0f, 150f), new Vector2(640f, 92f));
            ribbon.type = Image.Type.Sliced;
            overRibbonRt = ribbon.rectTransform;

            overDiamondL = MakeChildImage(overRt, "OverDiamondL", UiSpriteFactory.Diamond, ColGoldBright,
                new Vector2(-350f, 150f), new Vector2(26f, 26f));
            overDiamondR = MakeChildImage(overRt, "OverDiamondR", UiSpriteFactory.Diamond, ColGoldBright,
                new Vector2(350f, 150f), new Vector2(26f, 26f));

            overText = MakeText("OverText", ribbon.transform, 42, ColInk, TextAlignmentOptions.Center, bold: true);
            overText.characterSpacing = 2f;
            Stretch(overText.rectTransform, 16, 6, 16, 6);

            overScoreText = MakeText("OverScore", overRt, 28, ColIvory, TextAlignmentOptions.Center);
            Place(overScoreText.rectTransform, 0f, 0.535f, new Vector2(760f, 54f));

            againBtn = MakeButton("AgainBtn", "MAIN LAGI", 24f, ColGoldBright, Hex(0x3A2506),
                0f, 0.385f, new Vector2(300f, 80f), overRt);
            againBtn.onClick.AddListener(() =>
            {
                if (starter != null)
                    starter.Restart();
            });
            overOverlay.SetActive(false);
        }

        /// <summary>
        /// Chess-promotion-style choice modal for Pak Karta. Two large card choices
        /// (the existing BN/SB artwork and live card data) sit above a dimmed board;
        /// hovering a choice drives the same passive inspect preview as the hand.
        /// Clicking a card confirms; BATAL, Escape, or a backdrop click cancels.
        /// </summary>
        private void BuildTransformOverlay()
        {
            var go = new GameObject("TransformOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            transformOverlay = go;
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            StretchFull(rt);

            var dim = go.GetComponent<Image>();
            dim.color = new Color(0.04f, 0.02f, 0.08f, 0.76f);
            dim.raycastTarget = true;
            var backdrop = go.AddComponent<Button>();
            backdrop.targetGraphic = dim;
            backdrop.transition = Selectable.Transition.None;
            backdrop.onClick.AddListener(CloseTransformOverlay);

            transformGroup = go.GetComponent<CanvasGroup>();
            transformGroup.alpha = 0f;
            transformGroup.interactable = false;
            transformGroup.blocksRaycasts = false;

            var panel = MakeChildImage(rt, "TransformPanel", UiSpriteFactory.Rounded,
                new Color(0.11f, 0.08f, 0.19f, 1f), Vector2.zero, new Vector2(840f, 650f));
            panel.type = Image.Type.Sliced;
            var panelRing = MakeChildImage(panel.rectTransform, "TransformPanelRing",
                UiSpriteFactory.RoundOutline, new Color(0.85f, 0.66f, 0.26f, 0.45f),
                Vector2.zero, new Vector2(826f, 636f));
            panelRing.type = Image.Type.Sliced;

            var title = MakeText("TransformTitle", panel.rectTransform, 30, ColIvory,
                TextAlignmentOptions.Center, bold: true);
            PlaceCenter(title.rectTransform, new Vector2(0f, 270f), new Vector2(760f, 44f));
            title.text = "TRANSFORMASI PAK KARTA";
            title.characterSpacing = 1.4f;

            var subtitle = MakeText("TransformSubtitle", panel.rectTransform, 17, ColIvoryDim,
                TextAlignmentOptions.Center);
            PlaceCenter(subtitle.rectTransform, new Vector2(0f, 232f), new Vector2(760f, 28f));
            subtitle.text = "Pilih wujud baru";

            var bn = CreateTransformChoice(panel.rectTransform, "BN", -150f, "TransformChoiceBN");
            var sb = CreateTransformChoice(panel.rectTransform, "SB", 150f, "TransformChoiceSB");
            transformChoiceViews = new[] { bn, sb };

            var bnLabel = MakeText("TransformBnLabel", panel.rectTransform, 19, ColGoldBright,
                TextAlignmentOptions.Center, bold: true);
            PlaceCenter(bnLabel.rectTransform, new Vector2(-150f, -170f), new Vector2(240f, 30f));
            bnLabel.text = "BABI NGEPET";
            bnLabel.characterSpacing = 1f;

            var sbLabel = MakeText("TransformSbLabel", panel.rectTransform, 19, ColGoldBright,
                TextAlignmentOptions.Center, bold: true);
            PlaceCenter(sbLabel.rectTransform, new Vector2(150f, -170f), new Vector2(240f, 30f));
            sbLabel.text = "SILUMAN BABI";
            sbLabel.characterSpacing = 1f;

            transformErrorText = MakeText("TransformError", panel.rectTransform, 16, ColRed,
                TextAlignmentOptions.Center);
            PlaceCenter(transformErrorText.rectTransform, new Vector2(0f, -214f), new Vector2(740f, 28f));
            transformErrorText.text = "";

            var cancel = MakeButton("TransformCancelBtn", "BATAL", 20f, ColIvory, ColInk,
                0f, 0.10f, new Vector2(220f, 54f), panel.rectTransform);
            cancel.onClick.AddListener(CloseTransformOverlay);

            transformOverlay.SetActive(false);
        }

        private CardView CreateTransformChoice(RectTransform parent, string cardId, float x, string name)
        {
            if (battle == null || battle.Database == null)
                return null;
            var def = battle.Database.GetBattleCard(cardId);
            if (def == null)
                return null;

            var card = CardInstance.CreateBattle(def);
            var view = CardView.Create(parent, new Vector2(240f, 330f), name);
            view.Bind(card, CardDisplayMode.Active);
            PlaceCenter(view.Root, new Vector2(x, 30f), new Vector2(240f, 330f));
            view.Clicked += _ => ConfirmTransform(cardId);
            view.Hovered += OnTransformChoiceHovered;
            return view;
        }

        private void OnTransformChoiceHovered(CardView view, bool entered)
        {
            if (handInspect == null || view == null || view.Card == null)
                return;
            if (entered)
                handInspect.Show(view.Card, view.Root);
            else
                handInspect.HideIfCard(view.Card);
        }

        private void OpenTransformOverlay()
        {
            if (battle == null || transformOverlay == null)
                return;
            if (!battle.CanTransformPakKarta(out var error))
            {
                if (!string.IsNullOrEmpty(error))
                    battle.PostLocalStatus(error);
                return;
            }
            if (transformOverlay.activeSelf)
                return;

            if (transformFade != null)
            {
                StopCoroutine(transformFade);
                transformFade = null;
            }
            if (handInspect != null)
            {
                handInspect.Hide();
                _inspectSourceView = null;
            }
            if (transformErrorText != null)
                transformErrorText.text = "";

            transformOverlay.SetActive(true);
            if (transformActionRoot != null)
                transformActionRoot.gameObject.SetActive(false);
            transformGroup.alpha = 0f;
            transformGroup.interactable = true;
            transformGroup.blocksRaycasts = true;
            transformFade = StartCoroutine(FadeTransformOverlay(0f, 1f, 0.14f, false));
        }

        private void CloseTransformOverlay()
        {
            if (transformOverlay == null || !transformOverlay.activeSelf)
                return;
            if (transformFade != null)
            {
                StopCoroutine(transformFade);
                transformFade = null;
            }
            if (handInspect != null)
                handInspect.Hide();
            transformGroup.interactable = false;
            transformGroup.blocksRaycasts = false;
            transformFade = StartCoroutine(FadeTransformOverlay(transformGroup.alpha, 0f, 0.10f, true));
        }

        private void CloseTransformOverlayImmediate()
        {
            if (transformFade != null)
            {
                StopCoroutine(transformFade);
                transformFade = null;
            }
            if (handInspect != null)
                handInspect.Hide();
            if (transformOverlay == null)
                return;
            transformGroup.alpha = 0f;
            transformGroup.interactable = false;
            transformGroup.blocksRaycasts = false;
            transformOverlay.SetActive(false);
            RefreshTransformActionVisibility();
        }

        private void RefreshTransformActionVisibility()
        {
            if (transformActionRoot == null)
                return;
            bool canTransform = battle != null && battle.Phase == Phase.PlayerTurn &&
                                battle.CanTransformPakKarta(out _);
            bool overlayOpen = transformOverlay != null && transformOverlay.activeSelf;
            transformActionRoot.gameObject.SetActive(canTransform && !overlayOpen);
        }

        private IEnumerator FadeTransformOverlay(float from, float to, float duration, bool deactivateAtEnd)
        {
            float t = 0f;
            while (t < duration)
            {
                if (transformGroup == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                transformGroup.alpha = Mathf.Lerp(from, to, k);
                yield return null;
            }
            if (transformGroup != null)
                transformGroup.alpha = to;
            if (deactivateAtEnd && transformOverlay != null)
            {
                transformOverlay.SetActive(false);
                RefreshTransformActionVisibility();
            }
            transformFade = null;
        }

        private void ConfirmTransform(string formId)
        {
            if (battle == null)
                return;
            if (!battle.TransformPakKarta(formId))
            {
                if (transformErrorText != null)
                    transformErrorText.text = battle.StatusMessage;
                return;
            }
            CloseTransformOverlay();
        }

        // ================================================================== refresh

        private void RefreshAll()
        {
            if (battle == null || safeRoot == null)
                return;

            bool setup = battle.Phase == Phase.Setup;
            bool myTurn = battle.Phase == Phase.PlayerTurn;

            // ---- board slots ----
            BindSlot(botActiveView, battle.Bot.Active, CardDisplayMode.Active, botInspect: true);
            bool botActiveEmpty = battle.Bot.Active == null;
            botActiveFrame.SetActive(botActiveEmpty);
            if (botActiveEmpty && botActiveLabel != null)
                botActiveLabel.text = "Kosong";
            BindBenchRow(botBenchViews, botBenchFrames, botBenchLabels, battle.Bot.Bench, "Kosong", botInspect: true);

            BindSlot(playerActiveView, battle.Player.Active, CardDisplayMode.Active);
            bool pickHint = setup && battle.Player.Active == null;
            playerActiveFrame.SetActive(battle.Player.Active == null);
            playerActiveLabel.text = pickHint ? "PILIH KARTU\nAKTIFMU" : "Kosong";
            BindBenchRow(playerBenchViews, playerBenchFrames, playerBenchLabels, battle.Player.Bench, "Kosong");

            // inspection preview: keep a board-sourced panel in sync with live HP/art,
            // and drop it when the shown card leaves play (KO, promotion, retreat).
            RefreshBoardInspectPreview();

            // ---- Pak Karta transformation action ----
            bool canTransform = myTurn && battle.CanTransformPakKarta(out _);
            RefreshTransformActionVisibility();
            if (playerActiveView != null)
                playerActiveView.SetEligible(canTransform);

            // ---- hand ----
            hand.Sync(battle.Player.Hand);
            if (setup)
            {
                foreach (var v in hand.Views)
                    v.SetEligible(v.Card != null && battle.CanSelectStartingActive(v.Card));
                hand.Layout(); // lift eligible starting cards
            }

            // ---- visible deck piles: counts update before any draw ghost starts ----
            if (botDeckPile != null)
                botDeckPile.SetCount(battle.Bot.Deck.Count);
            if (playerDeckPile != null)
                playerDeckPile.SetCount(battle.Player.Deck.Count);

            // ---- opponent hidden hand: backs only, never faces or a hand count ----
            if (botHandFan != null)
                botHandFan.Sync(battle.Bot.Hand);

            // ---- player draw ghosts, after the hand has its new layout target ----
            StartPendingPlayerDraws();

            // ---- header counters / stamina rail ----
            RefreshEnergyTexts();
            botInfoText.text = $"DEK {battle.Bot.Deck.Count}";
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

            bool canRetreat = myTurn && battle.CanRetreat(out _);
            bool canAttack = myTurn && battle.CanAttack(out _);
            mundurBtn.interactable = canRetreat;
            serangBtn.interactable = canAttack;
            endBtn.interactable = myTurn;

            // Valid-target affordances (visual only): the enemy active gets a warm red
            // ring while an attack is legal; bench battle cards get a soft gold ring
            // while a retreat is legal. Clicks keep their normal rules.
            botActiveView.SetTarget(canAttack, true);
            for (int i = 0; i < playerBenchViews.Length; i++)
            {
                var bv = playerBenchViews[i];
                bv.SetTarget(canRetreat && bv.Card != null && bv.Card.IsBattle, false);
            }

            if (mundurLabel != null)
            {
                if (battle.Player.Active == null)
                    mundurLabel.text = "MUNDUR";
                else if (BattleManager.BenchContainsMbokSumi(battle.Player) && !battle.Player.UsedFreeRetreatViaMbokSumi)
                    mundurLabel.text = "MUNDUR \u00b7GRATIS";
                else
                    mundurLabel.text = $"MUNDUR \u00b7{battle.Player.Active.RetreatCost}";
            }
            if (serangLabel != null)
            {
                if (battle.Player.Active == null)
                    serangLabel.text = "SERANG";
                else if (battle.Player.Active.AttackCost == 0)
                    serangLabel.text = "SERANG \u00b7GRATIS";
                else
                    serangLabel.text = $"SERANG \u00b7{battle.Player.Active.AttackCost}";
            }

            // ---- queued cosmetic animations (after rebind so views show new cards) ----
            if (_pendingPromoPlayer != null)
            {
                AnimateSlotIn(FindBoardView(_pendingPromoPlayer), new Vector2(250f, -22f) * _k); // from the bench to the left
                _pendingPromoPlayer = null;
            }
            if (_pendingPromoBot != null)
            {
                AnimateSlotIn(FindBoardView(_pendingPromoBot), new Vector2(250f, 22f) * _k);
                _pendingPromoBot = null;
            }
            if (_pendingBenchPlayer != null)
            {
                AnimateSlotIn(FindBoardView(_pendingBenchPlayer), new Vector2(0f, -190f * _k)); // hand sits below
                _pendingBenchPlayer = null;
            }
            if (_pendingBenchBot != null)
            {
                AnimateSlotIn(FindBoardView(_pendingBenchBot), new Vector2(0f, 190f * _k));
                _pendingBenchBot = null;
            }
            if (_pendingRetreatPlayer != null)
            {
                AnimateSlotIn(FindBoardView(_pendingRetreatPlayer), new Vector2(250f, -22f) * _k);
                _pendingRetreatPlayer = null;
            }
            if (_pendingRetreatBot != null)
            {
                AnimateSlotIn(FindBoardView(_pendingRetreatBot), new Vector2(250f, 22f) * _k);
                _pendingRetreatBot = null;
            }
        }

        /// <summary>
        /// Board-sourced inspect previews (opponent active/bench) are refreshed in place
        /// so damage, healing, and evolution always show the live card values. Hand and
        /// transformation-choice previews are owned by their own hover handlers.
        /// </summary>
        private void RefreshBoardInspectPreview()
        {
            if (handInspect == null || handInspect.CurrentCard == null || _inspectSourceView == null)
                return;
            if (!IsBotBoardView(_inspectSourceView))
                return;

            var card = handInspect.CurrentCard;
            if (card.FaceDown ||
                (card != battle.Bot.Active && !battle.Bot.Bench.Contains(card)))
            {
                _inspectSourceView = null;
                handInspect.Hide();
                return;
            }

            if (!ReferenceEquals(_inspectSourceView.Card, card))
            {
                _inspectSourceView = null;
                handInspect.Hide();
                return;
            }

            handInspect.Show(card, _inspectSourceView.Root);
        }

        private bool IsBotBoardView(CardView view)
        {
            if (view == null)
                return false;
            if (view == botActiveView)
                return true;
            if (botBenchViews != null)
                foreach (var benchView in botBenchViews)
                    if (benchView == view)
                        return true;
            return false;
        }

        private void BindSlot(CardView view, CardInstance card, CardDisplayMode mode, bool botInspect = false)
        {
            if (view == null)
                return;
            view.Bind(card, mode);
            view.Clicked -= OnCardClicked;
            view.Clicked += OnCardClicked;
            if (botInspect)
            {
                view.Hovered -= OnBoardHovered;
                view.Hovered += OnBoardHovered;
            }
        }

        private void BindBenchRow(CardView[] views, GameObject[] frames, TMP_Text[] labels,
            List<CardInstance> bench, string emptyLabel, bool botInspect = false)
        {
            for (int i = 0; i < views.Length; i++)
            {
                var card = i < bench.Count ? bench[i] : null;
                views[i].Bind(card, CardDisplayMode.Bench);
                // bench cards recede slightly so the active stays the focal point
                views[i].SetDimmed(card != null, 0.08f);
                views[i].Clicked -= OnCardClicked;
                views[i].Clicked += OnCardClicked;
                if (botInspect)
                {
                    views[i].Hovered -= OnBoardHovered;
                    views[i].Hovered += OnBoardHovered;
                }
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

        private void HandleEnergyChanged(Side side)
        {
            // Energy can change without a board rebind (retreat/attack/steal). Update the
            // rail immediately; the value itself remains owned by BattleManager.
            RefreshEnergyTexts();
        }

        private void RefreshEnergyTexts()
        {
            if (battle == null)
                return;
            string fraction = "/" + BattleRules.EnergyCap;
            if (playerEnergyText != null)
                playerEnergyText.text = battle.Player.Energy + fraction;
            if (botEnergyText != null)
                botEnergyText.text = battle.Bot.Energy + fraction;
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
                        ShakeView(view, "Tumbal Anak di tangan akan dikorbankan untuk menangkal serangan api pada Babi Ngepet.");
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

            // Everything before the first yield runs before the manager applies damage:
            // capture the post-damage HP now so the bar can hold pre-damage, then drain
            // on the impact frame (0.14s into the 0.30s lunge = its peak).
            int targetHp = Mathf.Max(0, def.CurrentHp - dmg);
            if (defView != null && dmg > 0)
                defView.TweenHpTo(targetHp, def.MaxHp, 0.35f, 0.14f);

            if (atkView != null)
                StartCoroutine(Lunge(atkView.Root, dir, 0.30f));

            yield return new WaitForSecondsRealtime(0.14f);

            // The manager may already have rebound this view to a promoted card if the
            // hit was lethal. Never shake/punch the replacement card for the old card's
            // damage - that stale motion used to leave promoted actives offset sideways.
            bool defenderStillBound = defView != null &&
                ReferenceEquals(defView.Card, def) && !def.IsKnockedOut;
            if (defenderStillBound)
            {
                StartCoroutine(UiFx.Shake(defView.Root, 8f, 0.26f));
                StartCoroutine(UiFx.Punch(defView.Root, 1.05f, 0.2f));
                StartCoroutine(ImpactFlash(defView, effective));
            }

            var anchor = defenderStillBound
                ? defView.Root
                : (defView != null && defView.Root.parent is RectTransform slot
                    ? slot
                    : (atkView != null ? atkView.Root : null));
            if (anchor != null)
            {
                string label = dmg <= 0 ? "TERTAHAN" : dmg.ToString();
                var color = dmg <= 0 ? ColLavIdle : (effective ? ColGoldBright : ColIvory);
                SpawnDamagePopup(anchor, label, color);
                if (effective && dmg > 0)
                    SpawnDamagePopup(anchor, "2x!", ColRed, extra: true);
            }

            // Belt-and-suspenders for non-lethal hits: once the cosmetic motion has
            // finished, snap surviving board cards back to the slot centre.
            yield return new WaitForSecondsRealtime(0.45f);
            if (defenderStillBound && defView != null && ReferenceEquals(defView.Card, def) &&
                defView.Root != null)
                defView.Root.anchoredPosition = Vector2.zero;
            if (atkView != null && ReferenceEquals(atkView.Card, atk) && atkView.Root != null)
                atkView.Root.anchoredPosition = Vector2.zero;
        }

        private IEnumerator Lunge(RectTransform rt, float dir, float duration)
        {
            if (rt == null)
                yield break;
            Vector2 basePos = rt.anchoredPosition;
            float reach = 82f * _k;
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

        private IEnumerator ImpactFlash(CardView view, bool effective)
        {
            if (view == null || safeRoot == null)
                yield break;
            // Safe-root space so a KO rebind can never drag the effect onto the promoted card.
            Vector2 pos = CanvasLocal(view.Root);
            Vector2 size = view.Root.rect.size;

            SpawnRing(view.Root, effective ? ColGoldBright : ColIvory, 26f, 0.30f);

            var go = new GameObject("ImpactFlash", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var flash = go.GetComponent<Image>();
            flash.sprite = UiSpriteFactory.Rounded;
            flash.type = Image.Type.Sliced;
            flash.raycastTarget = false;
            yield return UiFx.FlashOut(flash, 0.20f, 0.55f);
        }

        /// <summary>Expanding outline ring used for impacts, KOs and support destruction.</summary>
        private void SpawnRing(RectTransform anchor, Color color, float extra, float duration)
        {
            if (anchor == null || safeRoot == null)
                return;
            var go = new GameObject("FxRing", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = anchor.rect.size + new Vector2(extra, extra);
            rt.anchoredPosition = CanvasLocal(anchor);
            var img = go.GetComponent<Image>();
            img.sprite = UiSpriteFactory.RoundOutline;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            StartCoroutine(RingAnim(rt, img, duration));
        }

        private IEnumerator RingAnim(RectTransform rt, Image img, float duration)
        {
            Color baseColor = img.color;
            float t = 0f;
            while (t < duration)
            {
                if (rt == null || img == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float s = Mathf.Lerp(0.94f, 1.18f, Mathf.SmoothStep(0f, 1f, k));
                rt.localScale = new Vector3(s, s, 1f);
                img.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - k));
                yield return null;
            }
            if (rt != null)
                Destroy(rt.gameObject);
        }

        private IEnumerator PunchAfter(RectTransform rt, float delay, float scaleTo, float duration)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (rt != null)
                yield return UiFx.Punch(rt, scaleTo, duration);
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

            float koDrift = 46f * _k;
            SpawnRing(view.Root, new Color(0.94f, 0.45f, 0.42f, 0.85f), 40f, 0.34f);
            yield return UiFx.Move(ghostRt, pos + new Vector2(0f, side == Side.Bot ? koDrift : -koDrift), 0.38f);
            var c = Color.white;
            float t = 0f;
            while (t < 0.38f)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - t / 0.38f;
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

        private void HandleTransformed(CardInstance card, Side side)
        {
            var view = side == Side.Player ? playerActiveView : botActiveView;
            if (view == null || !ReferenceEquals(view.Card, card))
                return;
            SpawnRing(view.Root, ColGoldBright, 36f, 0.34f);
            StartCoroutine(UiFx.Punch(view.Root, 1.06f, 0.22f));
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
            if (side != Side.Player || card == null)
                return;
            if (_pendingPlayerDrawSet.Add(card))
                _pendingPlayerDraws.Add(card);
        }

        /// <summary>
        /// Start the face-down travel from the player's visible deck to each new hand
        /// view. The view is hidden until the ghost arrives, so identity is never shown
        /// before the card reaches the hand. Multiple rapid draws are handled together.
        /// </summary>
        private void StartPendingPlayerDraws()
        {
            if (_pendingPlayerDraws.Count == 0)
                return;

            if (battle == null || hand == null || playerDrawFxLayer == null || playerDeckStack == null)
            {
                _pendingPlayerDraws.Clear();
                _pendingPlayerDrawSet.Clear();
                return;
            }

            var cards = new List<CardInstance>(_pendingPlayerDraws);
            _pendingPlayerDraws.Clear();
            _pendingPlayerDrawSet.Clear();

            int order = 0;
            foreach (var card in cards)
            {
                if (card == null || !battle.Player.Hand.Contains(card))
                    continue;
                var view = hand.FindView(card);
                if (view == null)
                    continue;

                var group = view.Root.GetComponent<CanvasGroup>();
                if (group == null)
                    group = view.Root.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                var cardSize = new Vector2(hand.cardWidth, hand.cardHeight);
                float delay = Mathf.Min(order * 0.035f, 0.14f);
                var fx = CardDrawFx.Play(playerDrawFxLayer, playerDeckStack, view.Root,
                    cardSize, 0.32f, delay, () =>
                    {
                        if (view != null)
                            StartCoroutine(UiFx.Punch(view.Root, 1.05f, 0.18f));
                    });
                if (fx == null)
                {
                    group.alpha = 1f;
                    group.interactable = true;
                    group.blocksRaycasts = true;
                }
                order++;
            }
        }

        /// <summary>
        /// Snap or drop every in-flight draw. Cancelling the ghosts restores their hand
        /// slots via CardDrawFx.OnDestroy, so restart/game over can never strand an
        /// invisible card or leave an animation object behind.
        /// </summary>
        private void FinishDrawAnimations()
        {
            if (botHandFan != null)
                botHandFan.FinishDrawAnimations();
            if (playerDrawFxLayer != null)
            {
                var fxs = playerDrawFxLayer.GetComponentsInChildren<CardDrawFx>(true);
                for (int i = fxs.Length - 1; i >= 0; i--)
                    if (fxs[i] != null)
                        fxs[i].Cancel();
            }
            _pendingPlayerDraws.Clear();
            _pendingPlayerDrawSet.Clear();
        }

        /// <summary>Kucing Liar sniping a bench Mbok Sumi: one-shot destruction feedback.</summary>
        private void HandleSupportDestroyed(CardInstance card, Side side)
        {
            var view = FindBoardView(card);
            if (view == null)
                return;
            SpawnRing(view.Root, new Color(0.94f, 0.45f, 0.42f, 0.85f), 34f, 0.32f);
            StartCoroutine(UiFx.Shake(view.Root, 7f, 0.24f));
            SpawnDamagePopup(view.Root, "HANCUR", ColRed);
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
            // Board card views are always centred in their slot when at rest. Target
            // that invariant explicitly rather than capturing the live mid-flight
            // position (which could bake a previous shake offset into the new card).
            Vector2 to = Vector2.zero;
            rt.anchoredPosition = to + fromOffset;
            rt.localScale = Vector3.one * 0.75f;
            yield return UiFx.Move(rt, to, 0.28f);
            // restore full size before the settle punch: UiFx.Punch returns to the scale
            // it started at (leaving 0.75 here made every promoted/benched card small).
            rt.localScale = Vector3.one;
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
            float startScale = small ? 0.82f : 0.72f;
            rt.localScale = Vector3.one * startScale;
            while (t < 0.9f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.9f);
                rt.anchoredPosition = Vector2.Lerp(start, end, k);
                // pop in quickly, then settle (the number lands with the hit)
                float s = t < 0.12f
                    ? Mathf.Lerp(startScale, 1.06f, t / 0.12f)
                    : Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((t - 0.12f) / 0.1f));
                rt.localScale = new Vector3(s, s, 1f);
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
            if (coinGlow != null) coinGlow.color = new Color(0.95f, 0.76f, 0.35f, 0.28f);
            if (coinGlow2 != null) coinGlow2.color = new Color(0.95f, 0.76f, 0.35f, 0.14f);
            coinText.text = playerFirst ? "KAMU PERTAMA!" : "BOT PERTAMA!";
            coinText.color = playerFirst ? ColGoldBright : ColIvory;
            StartCoroutine(CoinAnim(playerFirst));
        }

        private IEnumerator CoinAnim(bool playerFirst)
        {
            // Coin "flips" by squashing on X, then the result fades out.
            float t = 0f;
            float flipDuration = 0.7f;
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
            yield return UiFx.Punch(coinImage.rectTransform, 1.35f, 0.35f);
            yield return new WaitForSecondsRealtime(0.6f);
            if (coinGlow != null) StartCoroutine(UiFx.Fade(coinGlow, 0f, 0.35f));
            if (coinGlow2 != null) StartCoroutine(UiFx.Fade(coinGlow2, 0f, 0.35f));
            yield return UiFx.Fade(coinImage, 0f, 0.35f);
            yield return UiFx.Fade(coinRing, 0f, 0.35f);
            yield return UiFx.Fade(coinText, 0f, 0.3f);
            coinOverlay.SetActive(false);
            if (statusPill != null)
                statusPill.gameObject.SetActive(true);
            if (statusShadow != null)
                statusShadow.gameObject.SetActive(true);
            coinImage.color = ColGoldBright;
            coinRing.color = new Color(0.85f, 0.66f, 0.26f, 0.85f);
            if (coinGlow != null) coinGlow.color = new Color(0.95f, 0.76f, 0.35f, 0.28f);
            if (coinGlow2 != null) coinGlow2.color = new Color(0.95f, 0.76f, 0.35f, 0.14f);
            // the toss is over but the first turn hasn't started yet: the divider should
            // prompt the setup pick instead of still reading "LEMPAR KOIN"
            if (turnPillText != null && battle != null && battle.Phase == Phase.Setup)
                turnPillText.text = "PILIH KARTU";
            coinText.color = playerFirst ? ColGoldBright : ColIvory;

            if (battle != null && battle.Phase == Phase.Setup)
                battle.PostLocalStatus("Pilih kartu aktif dari tanganmu!");
        }

        private void ShowTurnBanner(Side side)
        {
            if (turnBannerRoutine != null)
                StopCoroutine(turnBannerRoutine);
            turnBannerRoutine = StartCoroutine(TurnBannerAnim(side));
            if (turnPillBg != null)
                StartCoroutine(UiFx.Punch(turnPillBg.rectTransform, 1.10f, 0.34f));

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
            // Never leave a face-down ghost, transformation modal, or invisible hand
            // card behind the result.
            CloseTransformOverlayImmediate();
            FinishDrawAnimations();
            if (turnBannerRoutine != null)
            {
                StopCoroutine(turnBannerRoutine);
                turnBannerRoutine = null;
            }
            if (hand != null)
                hand.HideInspectPreview();   // inspection preview never outlives the battle
            _inspectSourceView = null;       // board-sourced previews: forget the source slot too
            overOverlay.transform.SetAsLastSibling(); // result modal must sit above the board, HUD and hand
            overOverlay.SetActive(true);
            if (overGroup != null)
            {
                overGroup.alpha = 0f;
                StartCoroutine(GameOverIn());
            }
            if (overRibbonRt != null)
                StartCoroutine(UiFx.Punch(overRibbonRt, 1.04f, 0.32f));
            bool playerWins = winner == Side.Player;
            overText.text = playerWins ? "KAMU MENANG!" : "BOT MENANG!";
            var accent = playerWins ? ColGoldBright : ColRed;
            overDiamondL.color = accent;
            overDiamondR.color = accent;
            overScoreText.text = $"Skor akhir  {battle.Player.Points} - {battle.Bot.Points}";
        }

        /// <summary>Soft fade-in for the result overlay (board is already authoritative).</summary>
        private IEnumerator GameOverIn()
        {
            float t = 0f;
            const float d = 0.22f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                if (overGroup != null)
                    overGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
                yield return null;
            }
            if (overGroup != null)
                overGroup.alpha = 1f;
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
            // Percent-anchored so the landscape composition stays coherent in any Game View aspect.
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, yPercent);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, 0f);
            return rt;
        }

        private RectTransform NodeLeft(string name, float inset, float yPercent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, yPercent);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(inset, 0f);
            return rt;
        }

        private RectTransform NodeRight(string name, float inset, float yPercent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, yPercent);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(-inset, 0f);
            return rt;
        }

        private static void PlaceLocal(RectTransform rt, float x, float y, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static void PlaceLeft(RectTransform rt, float inset, float yPercent, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, yPercent);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(inset, 0f);
        }

        private static void PlaceRight(RectTransform rt, float inset, float yPercent, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, yPercent);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(-inset, 0f);
        }

        private static void PlaceCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
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
            img.color = new Color(0.08f, 0.05f, 0.13f, 0.55f);
            img.raycastTarget = false;

            var innerRing = MakeImage("InnerRing", UiSpriteFactory.RoundOutline, new Color(1f, 1f, 1f, 0.20f));
            innerRing.type = Image.Type.Sliced;
            var ringRt = innerRing.rectTransform;
            ringRt.SetParent(rt, false);
            Stretch(ringRt, 5, 5, 5, 5);

            labelText = MakeText("EmptyLabel", rt, fontSize, new Color(1f, 1f, 1f, 0.62f), TextAlignmentOptions.Center, bold: true);
            Stretch(labelText.rectTransform, 6, 4, 6, 4);
            labelText.text = label;
            return go;
        }

        private Button MakeButton(string name, string label, float fontSize, Color bg, Color fg,
            float x, float yPercent, Vector2 size, Transform parentOverride = null)
        {
            Transform parent = parentOverride != null ? parentOverride : safeRoot;

            var shadow = MakeImage(name + "Shadow", UiSpriteFactory.SoftShadow, new Color(0f, 0f, 0f, 0.32f));
            if (parentOverride != null)
                shadow.rectTransform.SetParent(parentOverride, false);
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

            // ButtonFx owns hover/press/disabled visuals (bg image + label color).
            // The built-in ColorTint transition would fight it, so it stays off.
            btn.transition = Selectable.Transition.None;
            return btn;
        }

        /// <summary>
        /// Hand-card inspection overlay lives under SafeRoot like every other UI (the
        /// rescale registry below covers it). Built after the overlays so it renders
        /// above the board, but it cannot intercept input (CanvasGroup in CardInspectPreview).
        /// </summary>
        private void BuildInspectPreview()
        {
            var go = new GameObject("HandInspectPreview", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 414f);
            handInspect = go.AddComponent<CardInspectPreview>();
            hand.SetInspectPreview(handInspect);
        }

        /// <summary>
        /// Stage-3 addendum: opponent board cards (active + bench) feed the same inspect
        /// preview. Face-down cards (still hidden during the coin reveal) never open the
        /// preview, so nothing hidden is leaked, and hovering stays a passive mirror - no
        /// selection or combat behavior change. The panel sits just above the hovered
        /// card's top edge inside the safe area, i.e. in the bot zone for opponent cards,
        /// where it never covers the action buttons (see CardInspectPreview.Reposition).
        /// </summary>
        private void OnBoardHovered(CardView view, bool entered)
        {
            if (entered)
            {
                if (view.Card == null || view.Card.FaceDown)
                    return;
                _inspectSourceView = view;
                if (handInspect != null)
                    handInspect.Show(view.Card, view.Root);
            }
            else
            {
                if (_inspectSourceView == view)
                    _inspectSourceView = null;
                if (handInspect != null && view.Card != null)
                    handInspect.HideIfCard(view.Card);
            }
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
