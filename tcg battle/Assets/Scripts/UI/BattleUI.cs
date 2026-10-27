using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgBattle.Data;
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

        // ---- palette: shared with CardView / preview / FX via UiStyle ----
        private static readonly Color ColDeep = UiStyle.BoardDeep;
        private static readonly Color ColBotZone = UiStyle.BotZone;
        private static readonly Color ColPanelInk = UiStyle.Panel;
        private static readonly Color ColIvory = UiStyle.Ivory;
        private static readonly Color ColIvoryDim = UiStyle.IvoryDim;
        private static readonly Color ColGold = UiStyle.Gold;
        private static readonly Color ColGoldBright = UiStyle.GoldBright;
        private static readonly Color ColInk = UiStyle.Ink;
        private static readonly Color ColInkSoft = UiStyle.InkSoft;
        private static readonly Color ColMaroon = UiStyle.Maroon;
        private static readonly Color ColRed = UiStyle.Danger;
        private static readonly Color ColInkInfo = UiStyle.IvoryDim;
        private static readonly Color ColLavIdle = UiStyle.IvoryDim;
        private static readonly Color ColBotPipEmpty = UiStyle.PipEmpty;
        private static readonly Color ColPlayerPipEmpty = UiStyle.PipEmpty;

        private BattleManager battle;
        private BattleStarter starter;
        private BotController bot;

        // ---- built references ----
        private RectTransform canvasRt;
        private RectTransform safeRoot;
        private CardView botActiveView, playerActiveView;
        private CardView[] botBenchViews, playerBenchViews;
        private RectTransform[] botBenchSlots, playerBenchSlots;
        private RectTransform playerActiveSlot;
        private Image playerActiveDropRing;
        private Image[] playerBenchDropRings;
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
        private RectTransform formFxLayer;
        private CardFormFx formFxPlayer, formFxBot;
        private RectTransform combatFxLayer;
        private RectTransform playerStaminaRoot, botStaminaRoot;
        private int _fxEpoch;
        private bool _combatBusy;
        private int _lastPlayerEnergy, _lastBotEnergy;
        private bool _shieldPending;
        private Side _shieldSide;
        private bool _restarting;
        private Coroutine _attackRoutine;
        private Coroutine _playerActiveSlotIn, _botActiveSlotIn;
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

        private struct PendingFormFx
        {
            public Side Side;
            public CardInstance NewCard;
            public CardVisualSnapshot OldVisual;
            public string Heading;
            public string Path;
        }
        private readonly List<PendingFormFx> _pendingFormFx = new List<PendingFormFx>();

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
            StopAllCoroutines();
            ResetHudTransforms();
            turnBannerRoutine = null;
            transformFade = null;
            // Keep the restart guard latched through the bind: a second click in the
            // same frame must not tear down the freshly-created match. ShowGameOver
            // clears it when the next match actually ends.
            ClearCombatFx();
            ClearFormFx();
            FinishDrawAnimations();
            if (hand != null)
                hand.SetDragEnabled(false);
            ClearDropHighlights();
            _pendingPromoPlayer = _pendingPromoBot = null;
            _pendingBenchPlayer = _pendingBenchBot = null;
            _pendingRetreatPlayer = _pendingRetreatBot = null;
            statusBaseline = "";
            if (botHandFan != null)
                botHandFan.ResetVisuals();

            if (canvasRt == null)
                BuildAll();

            if (overOverlay != null)
            {
                overOverlay.SetActive(false);
                if (overGroup != null)
                    overGroup.alpha = 0f;
            }
            if (coinOverlay != null)
                coinOverlay.SetActive(false);
            if (againBtn != null)
                againBtn.interactable = !_restarting;
            if (statusPill != null)
            {
                statusPill.gameObject.SetActive(true);
                statusPill.color = UiStyle.StatusPill;
            }
            if (statusShadow != null)
                statusShadow.gameObject.SetActive(true);
            if (statusText != null)
            {
                statusText.color = UiStyle.StatusText;
                statusText.text = "";
            }
            if (turnPillText != null)
                turnPillText.text = "LEMPAR KOIN";

            _lastPlayerEnergy = battle != null ? battle.Player.Energy : 0;
            _lastBotEnergy = battle != null ? battle.Bot.Energy : 0;

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
            m.OnEvolved += HandleEvolved;
            m.OnPromoted += HandlePromoted;
            m.OnCardPlayedToBench += HandleBenchPlay;
            m.OnRetreated += HandleRetreat;
            m.OnScoreChanged += HandleScore;
            m.OnDrawn += HandleDraw;
            m.OnSupportDestroyed += HandleSupportDestroyed;
            m.OnSupportUsed += HandleSupportUsed;
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
            m.OnEvolved -= HandleEvolved;
            m.OnPromoted -= HandlePromoted;
            m.OnCardPlayedToBench -= HandleBenchPlay;
            m.OnRetreated -= HandleRetreat;
            m.OnScoreChanged -= HandleScore;
            m.OnDrawn -= HandleDraw;
            m.OnSupportDestroyed -= HandleSupportDestroyed;
            m.OnSupportUsed -= HandleSupportUsed;
            m.OnGameOver -= ShowGameOver;
        }

        public void Unbind(BattleManager m)
        {
            if (m == null)
                return;
            Unsubscribe(m);
            if (_boundManager == m)
                _boundManager = null;
            // Stop every cosmetic routine before hiding the board: an interrupted
            // lunge/shake/slot-in must not keep writing transforms after a restart.
            StopAllCoroutines();
            ResetHudTransforms();
            turnBannerRoutine = null;
            transformFade = null;
            ClearCombatFx();
            ClearFormFx();
            FinishDrawAnimations();
            if (hand != null)
                hand.SetDragEnabled(false);
            ClearDropHighlights();
            CloseTransformOverlayImmediate();
            if (overOverlay != null)
                overOverlay.SetActive(false);
            if (coinOverlay != null)
                coinOverlay.SetActive(false);
            if (statusPill != null)
                statusPill.color = UiStyle.StatusPill;
        }

        private void OnDestroy()
        {
            if (_boundManager != null)
                Unsubscribe(_boundManager);
            StopAllCoroutines();
            ClearCombatFx();
            ClearFormFx();
            FinishDrawAnimations();
            transformFade = null;
        }

        // ================================================================== build

        private void BuildAll()
        {
            canvasRt = (RectTransform)transform;

            // Full-screen art sits directly under the canvas, behind the safe-area
            // root, so no board or UI layer is ever hidden by it.
            BuildScreenBackground();

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
            BuildBotHandFan();
            BuildHand();
            BuildDrawFxLayer();
            BuildFormFxLayer();
            BuildCombatFxLayer();
            BuildOverlays();

            // capture the static layout once so it can be rescaled when the canvas size
            // changes (card views capture themselves in CardView.ApplyScale)
            UiRescaler.CaptureTreeExcludingCards(safeRoot, _registry);

            // Built after the capture: the fixed side inspection panel owns its
            // responsive width and must not be resized away by UiRescaler.
            BuildInspectPreview();   // last sibling: drawn over everything, never intercepts input
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
                FitInspectPanel();
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

        /// <summary>
        /// Full-screen battle illustration. It is anchored to the canvas rather than
        /// the safe-area root so it always covers the screen, and it is the first
        /// sibling so every card, panel, overlay and FX draws above it. The built-in
        /// AspectRatioFitter crops the 16:9 art to whatever aspect the window has
        /// instead of stretching it. Its Image is raycast-free.
        /// </summary>
        private void BuildScreenBackground()
        {
            if (transform.Find("BattleBackground") != null)
                return;

            var go = new GameObject("BattleBackground", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.SetAsFirstSibling();
            StretchFull(rt, 0f);

            // Fallback surface behind the art; it only shows if the sprite is missing.
            var baseFill = MakeImage("BaseFill", null, ColDeep, rt);
            StretchFull(baseFill.rectTransform, 0f);

            var sprite = ArtLibrary.GetBattleBackground();
            if (sprite == null)
                return;

            var art = MakeImage("Artwork", sprite, Color.white, rt);
            art.raycastTarget = false;
            art.preserveAspect = true;
            var ar = art.rectTransform;
            ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
            ar.pivot = new Vector2(0.5f, 0.5f);
            ar.anchoredPosition = Vector2.zero;
            ar.sizeDelta = new Vector2(1920f, 1080f);

            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
        }

        private void BuildBackground()
        {
            // The illustration itself is built under the canvas (BuildScreenBackground).
            // These zone veils only tint it for readability; they never cover it fully.
            var panel = MakeImage("BotZonePanel", UiSpriteFactory.Rounded,
                UiStyle.WithAlpha(ColBotZone, 0.42f));
            panel.type = Image.Type.Sliced;
            var pr = panel.rectTransform;
            pr.anchorMin = new Vector2(0f, BandDivider);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.offsetMin = new Vector2(0f, 0f);
            pr.offsetMax = new Vector2(0f, 46f);

            var botAtmosphere = MakeImage("BotAtmosphere", UiSpriteFactory.FadeDown,
                UiStyle.WithAlpha(UiStyle.BotZoneGlow, 0.30f));
            var ba = botAtmosphere.rectTransform;
            ba.anchorMin = new Vector2(0f, BandDivider);
            ba.anchorMax = new Vector2(1f, 1f);
            ba.pivot = new Vector2(0.5f, 0.5f);
            ba.offsetMin = Vector2.zero;
            ba.offsetMax = new Vector2(0f, 46f);

            // Player field: a very soft warm-violet lift from the bottom edge.
            var playerAtmosphere = MakeImage("PlayerAtmosphere", UiSpriteFactory.FadeUp,
                UiStyle.WithAlpha(UiStyle.PlayerZoneGlow, 0.22f));
            var pa = playerAtmosphere.rectTransform;
            pa.anchorMin = new Vector2(0f, 0f);
            pa.anchorMax = new Vector2(1f, BandDivider);
            pa.pivot = new Vector2(0.5f, 0.5f);
            pa.offsetMin = Vector2.zero;
            pa.offsetMax = Vector2.zero;

            BuildBoardFrame();
        }

        /// <summary>
        /// Thin inner frame and corner diamonds give the board a finished "table"
        /// edge. Everything is decorative, raycast-free and sits behind the cards.
        /// </summary>
        private void BuildBoardFrame()
        {
            var frame = MakeImage("BoardFrame", UiSpriteFactory.RoundOutline, UiStyle.BoardFrame);
            frame.type = Image.Type.Sliced;
            var fr = frame.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.pivot = new Vector2(0.5f, 0.5f);
            fr.offsetMin = new Vector2(18f, 18f);
            fr.offsetMax = new Vector2(-18f, -18f);

            AddFrameDiamond(fr, "FrameDiamondTL", new Vector2(0f, 1f));
            AddFrameDiamond(fr, "FrameDiamondTR", new Vector2(1f, 1f));
            AddFrameDiamond(fr, "FrameDiamondBL", new Vector2(0f, 0f));
            AddFrameDiamond(fr, "FrameDiamondBR", new Vector2(1f, 0f));
        }

        private void AddFrameDiamond(RectTransform parent, string name, Vector2 anchor)
        {
            var d = MakeImage(name, UiSpriteFactory.Diamond, UiStyle.BoardFrameDiamond);
            var rt = d.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(9f, 9f);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>Spot pool + ornament ring under each active slot (focal-point staging).</summary>
        private void BuildStageOrnaments()
        {
            // Player stage (dark field): a restrained violet pool under the active card.
            AddStage(-380f, BandActivePlayer, new Vector2(320f, 330f), new Vector2(440f, 300f),
                UiStyle.WithAlpha(UiStyle.Dark, 0.10f), UiStyle.WithAlpha(UiStyle.Gold, 0.38f));

            // Bot stage (dark terrace): a warm ember pool so the opponent's side still
            // reads as the Fire deck without tinting the card art.
            AddStage(-380f, BandActiveBot, new Vector2(310f, 320f), new Vector2(430f, 290f),
                UiStyle.WithAlpha(UiStyle.Ember, 0.10f), UiStyle.WithAlpha(UiStyle.Gold, 0.34f));
        }

        private void AddStage(float x, float yPercent, Vector2 ringSize, Vector2 poolSize, Color poolColor, Color ringColor)
        {
            // SoftGlow is tintable (the black SoftShadow texture is not); this gives the
            // active slots a real warm/violet pool instead of a dark smudge.
            var pool = MakeImage("StagePool", UiSpriteFactory.SoftGlow, poolColor);
            Place(pool.rectTransform, x, yPercent, poolSize);

            var ring = MakeImage("StageRing", UiSpriteFactory.RoundOutline, ringColor);
            ring.type = Image.Type.Sliced;
            Place(ring.rectTransform, x, yPercent, ringSize);

            // base line: a warm stage edge under each active slot (the focal marker)
            var baseLine = MakeImage("StageBase", UiSpriteFactory.Rounded, UiStyle.WithAlpha(UiStyle.Gold, 0.55f));
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
            CreateBenchRow("BotBenchSlot", BandBenchBot, -50f, 220f, new Vector2(172f, 230f),
                out botBenchSlots, out botBenchFrames, out botBenchLabels, out botBenchViews, "BotBench");

            var botActiveSlot = Node("BotActiveSlot", -380f, BandActiveBot, new Vector2(224f, 300f));
            botActiveFrame = MakeSlotFrame(botActiveSlot, "Kosong", 22f, out botActiveLabel);
            botActiveView = CardView.Create(botActiveSlot, new Vector2(224f, 300f), "BotActive");

            CreateBenchRow("PlayerBenchSlot", BandBenchPlayer, -50f, 220f, new Vector2(172f, 230f),
                out playerBenchSlots, out playerBenchFrames, out playerBenchLabels, out playerBenchViews, "PlayerBench");

            playerActiveSlot = Node("PlayerActiveSlot", -380f, BandActivePlayer, new Vector2(224f, 300f));
            playerActiveFrame = MakeSlotFrame(playerActiveSlot, "PILIH KARTU\nAKTIFMU", 22f, out playerActiveLabel);
            playerActiveView = CardView.Create(playerActiveSlot, new Vector2(224f, 300f), "PlayerActive");
            playerActiveDropRing = CreateDropRing(playerActiveSlot);

            playerBenchDropRings = new Image[playerBenchSlots.Length];
            for (int i = 0; i < playerBenchSlots.Length; i++)
                playerBenchDropRings[i] = CreateDropRing(playerBenchSlots[i]);
        }

        private Image CreateDropRing(RectTransform slot)
        {
            var ring = MakeImage("DropRing", UiSpriteFactory.RoundOutline,
                UiStyle.SelectedFrame, slot);
            ring.type = Image.Type.Sliced;
            Stretch(ring.rectTransform, -5, -5, -5, -5);
            ring.gameObject.SetActive(false);
            // EmptyFrame is sibling 0; keep the ring behind the card but above the slot.
            ring.rectTransform.SetSiblingIndex(1);
            return ring;
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
            var label = MakeText("BotLabel", group, 26, ColIvory, TextAlignmentOptions.Left, bold: true);
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
            var playerRoot = NodeRight("PlayerStamina", 110f, 0.100f, new Vector2(148f, 148f));
            playerStaminaRoot = playerRoot;
            var playerOuter = MakeChildImage(playerRoot, "Outer", UiSpriteFactory.Diamond,
                UiStyle.WithAlpha(UiStyle.Gold, 0.88f), Vector2.zero, new Vector2(146f, 146f));
            playerOuter.raycastTarget = false;
            var playerInner = MakeChildImage(playerRoot, "Fill", UiSpriteFactory.Diamond,
                UiStyle.WithAlpha(UiStyle.PanelInset, 0.97f), Vector2.zero, new Vector2(134f, 134f));
            playerInner.raycastTarget = false;

            playerEnergyText = MakeText("PlayerStaminaText", playerRoot, 34, ColIvory,
                TextAlignmentOptions.Center, bold: true);
            Stretch(playerEnergyText.rectTransform, 10f, 24f, 10f, 16f);
            playerEnergyText.text = "0/" + BattleRules.EnergyCap;

            var playerCaption = MakeText("PlayerStaminaCaption", playerRoot, 10, ColGold,
                TextAlignmentOptions.Center, bold: true);
            playerCaption.characterSpacing = 1.5f;
            PlaceCenter(playerCaption.rectTransform, new Vector2(0f, -44f), new Vector2(120f, 16f));
            playerCaption.text = "ENERGI";

            var botRoot = NodeRight("BotStamina", 110f, 0.7800f, new Vector2(74f, 74f));
            botStaminaRoot = botRoot;
            var botOuter = MakeChildImage(botRoot, "Outer", UiSpriteFactory.Diamond,
                UiStyle.WithAlpha(UiStyle.Gold, 0.82f), Vector2.zero, new Vector2(72f, 72f));
            botOuter.raycastTarget = false;
            var botInner = MakeChildImage(botRoot, "Fill", UiSpriteFactory.Diamond,
                UiStyle.WithAlpha(UiStyle.PanelInset, 0.94f), Vector2.zero, new Vector2(64f, 64f));
            botInner.raycastTarget = false;

            botEnergyText = MakeText("BotStaminaText", botRoot, 15, ColIvory,
                TextAlignmentOptions.Center, bold: true);
            Stretch(botEnergyText.rectTransform, 4f, 13f, 4f, 8f);
            botEnergyText.text = "0/" + BattleRules.EnergyCap;

            var botCaption = MakeText("BotStaminaCaption", botRoot, 7, ColGold,
                TextAlignmentOptions.Center, bold: true);
            PlaceCenter(botCaption.rectTransform, new Vector2(0f, -22f), new Vector2(64f, 10f));
            botCaption.text = "ENERGI";
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
                    UiStyle.PipSocket, botGroup);
                PlaceCenter(socket.rectTransform, new Vector2(x, 0f), new Vector2(32f, 32f));
                var pip = MakeImage($"BotPip{i}", UiSpriteFactory.Diamond, ColBotPipEmpty, botGroup);
                PlaceCenter(pip.rectTransform, new Vector2(x, 0f), new Vector2(24f, 24f));
                botPips[i] = pip;
            }

            var playerGroup = NodeRight("PlayerScorePips", 265f, 0.100f, new Vector2(140f, 44f));
            playerPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -40f + i * 40f;
                var socket = MakeImage($"PlayerPipSocket{i}", UiSpriteFactory.Ring,
                    UiStyle.PipSocket, playerGroup);
                PlaceCenter(socket.rectTransform, new Vector2(x, 0f), new Vector2(32f, 32f));
                var pip = MakeImage($"PlayerPip{i}", UiSpriteFactory.Diamond, ColPlayerPipEmpty, playerGroup);
                PlaceCenter(pip.rectTransform, new Vector2(x, 0f), new Vector2(24f, 24f));
                playerPips[i] = pip;
            }
        }

        private void BuildActionRow()
        {
            // Soft warm glow along the zone divider, behind the status/action rail.
            var divGlow = MakeImage("DivGlow", UiSpriteFactory.SoftGlow, UiStyle.DividerGlow);
            var dg = divGlow.rectTransform;
            dg.anchorMin = dg.anchorMax = new Vector2(0.5f, BandDivider);
            dg.pivot = new Vector2(0.5f, 0.5f);
            dg.sizeDelta = new Vector2(1400f, 64f);
            dg.anchoredPosition = Vector2.zero;

            // ---- one compact control bar between the two battlefields ----
            var statusShadow = MakeImage("StatusShadow", UiSpriteFactory.SoftShadow, new Color(0f, 0f, 0f, 0.35f));
            this.statusShadow = statusShadow;
            Place(statusShadow.rectTransform, -410f, BandStatus - 0.003f, new Vector2(600f, 52f));

            statusPill = MakeImage("StatusPill", UiSpriteFactory.Rounded, UiStyle.StatusPill);
            statusPill.type = Image.Type.Sliced;
            Place(statusPill.rectTransform, -410f, BandStatus, new Vector2(560f, 44f));

            var statusRing = MakeImage("StatusRing", UiSpriteFactory.RoundOutline, UiStyle.StatusRing);
            statusRing.type = Image.Type.Sliced;
            var ringRt = statusRing.rectTransform;
            ringRt.SetParent(statusPill.rectTransform, false);
            Stretch(ringRt, 2, 2, 2, 2);

            statusText = MakeText("StatusText", statusPill.rectTransform, 22, UiStyle.StatusText,
                TextAlignmentOptions.Center, bold: true);
            Stretch(statusText.rectTransform, 18, 3, 18, 4);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 14f;
            statusText.fontSizeMax = 22f;

            // Bot-zone edge: a thin gold-topped divider spanning the desktop width.
            var divLine = MakeImage("DivLine", null, UiStyle.WithAlpha(UiStyle.Gold, 0.38f));
            var dl = divLine.rectTransform;
            dl.anchorMin = new Vector2(0f, BandDivider);
            dl.anchorMax = new Vector2(1f, BandDivider);
            dl.pivot = new Vector2(0.5f, 0.5f);
            dl.offsetMin = new Vector2(80f, -1.5f);
            dl.offsetMax = new Vector2(-80f, 1.5f);
            var divDiamond = MakeImage("DivDiamond", UiSpriteFactory.Diamond, UiStyle.WithAlpha(UiStyle.Gold, 0.75f));
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
                if (battle != null && !_combatBusy)
                    battle.Retreat();
            });

            serangBtn = MakeButton("SerangBtn", "SERANG", 24f, ColGoldBright, UiStyle.InkOnGold,
                200f, BandButtons, new Vector2(180f, 60f));
            serangLabel = serangBtn.GetComponentInChildren<TMP_Text>();
            serangBtn.onClick.AddListener(() =>
            {
                if (battle != null && !_combatBusy)
                    battle.Attack();
            });

            endBtn = MakeButton("EndTurnBtn", "AKHIRI GILIRAN", 20f, ColInkSoft, ColIvory,
                430f, BandButtons, new Vector2(220f, 60f));
            endBtn.onClick.AddListener(() =>
            {
                if (battle != null && !_combatBusy)
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
            transformBtn = MakeButton("TransformBtn", "TRANSFORMASI", 19f, ColGoldBright, UiStyle.InkOnGold,
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
            // Both piles live on the left rail, one per side, matching the large
            // tabletop deck stacks in the reference composition.
            var botGo = new GameObject("BotDeckStack", typeof(RectTransform));
            var botRt = (RectTransform)botGo.transform;
            botRt.SetParent(safeRoot, false);
            botRt.anchorMin = botRt.anchorMax = new Vector2(0f, 0.7400f);
            botRt.pivot = new Vector2(0.5f, 0.5f);
            botRt.sizeDelta = new Vector2(140f, 190f);
            botRt.anchoredPosition = new Vector2(160f, 0f);
            botDeckStack = botRt;
            botDeckPile = botGo.AddComponent<DeckPileView>();
            botDeckPile.Build();

            var playerGo = new GameObject("PlayerDeckStack", typeof(RectTransform));
            var playerRt = (RectTransform)playerGo.transform;
            playerRt.SetParent(safeRoot, false);
            playerRt.anchorMin = playerRt.anchorMax = new Vector2(0f, 0.3000f);
            playerRt.pivot = new Vector2(0.5f, 0.5f);
            playerRt.sizeDelta = new Vector2(140f, 190f);
            playerRt.anchoredPosition = new Vector2(160f, 0f);
            playerDeckStack = playerRt;
            playerDeckPile = playerGo.AddComponent<DeckPileView>();
            playerDeckPile.Build(mirrored: true);
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
            fanRt.anchoredPosition = new Vector2(-240f, 0f);

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

        /// <summary>
        /// Cosmetic layer for evolution/transformation ghosts. It sits above the board
        /// and draw FX but below the game-over/transform overlays and inspect preview,
        /// and never intercepts input. Form FX are destroyed wholesale on unbind.
        /// </summary>
        private void BuildFormFxLayer()
        {
            var go = new GameObject("FormFxLayer", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            StretchFull(rt, 0f);
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            formFxLayer = rt;
        }

        /// <summary>
        /// Cosmetic combat/feedback layer: damage numbers, impact rings, KO ghosts,
        /// energy pops and turn flourishes. It is above the board and form FX but below
        /// the modal overlays, and never intercepts input. Cleared wholesale on game
        /// over/restart so no transient visual can outlive the match.
        /// </summary>
        private void BuildCombatFxLayer()
        {
            var go = new GameObject("CombatFxLayer", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(safeRoot, false);
            StretchFull(rt, 0f);
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            combatFxLayer = rt;
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
            hand.SetDragHandlers(OnHandDragStarted, OnHandDragMoved, OnHandDrop, ClearDropHighlights);
        }

        // ---------------------------------------------------------------- hand drag/drop

        private static bool IsInside(RectTransform target, Vector2 screenPosition)
        {
            return target != null &&
                RectTransformUtility.RectangleContainsScreenPoint(target, screenPosition, null);
        }

        private static bool IsTumbalAnak(CardInstance card)
        {
            return card != null && card.IsSupport && card.Support != null &&
                card.Support.effect == SupportEffect.TumbalAnakHandPassive;
        }

        private void ClearDropHighlights()
        {
            if (playerActiveDropRing != null)
                playerActiveDropRing.gameObject.SetActive(false);
            if (playerBenchDropRings != null)
                for (int i = 0; i < playerBenchDropRings.Length; i++)
                    if (playerBenchDropRings[i] != null)
                        playerBenchDropRings[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Highlight only destinations that the authoritative manager would accept
        /// for this exact card. Invalid destinations stay unhighlighted.
        /// </summary>
        private void UpdateDropHighlights(CardView view)
        {
            ClearDropHighlights();
            if (battle == null || view == null || view.Card == null)
                return;
            var card = view.Card;

            if (battle.Phase == Phase.Setup)
            {
                if (playerActiveDropRing != null && battle.CanSelectStartingActive(card))
                    playerActiveDropRing.gameObject.SetActive(true);
                return;
            }
            if (battle.Phase != Phase.PlayerTurn)
                return;

            if (BattleRules.IsEvolutionOnly(card))
            {
                if (playerActiveDropRing != null && battle.CanEvolve(card, out _))
                    playerActiveDropRing.gameObject.SetActive(true);
                return;
            }

            // One-shot supports take effect from hand; Tumbal Anak stays in hand.
            // Neither is placed into a battle slot by dragging.
            if (BattleManager.IsOneShotSupport(card) || IsTumbalAnak(card))
                return;

            if (battle.CanPlayToBench(card, out _))
            {
                int free = battle.Player.Bench.Count;
                if (playerBenchDropRings != null && free >= 0 && free < playerBenchDropRings.Length &&
                    playerBenchDropRings[free] != null)
                    playerBenchDropRings[free].gameObject.SetActive(true);
            }
        }

        private void OnHandDragStarted(CardView view, Vector2 screenPosition)
        {
            UpdateDropHighlights(view);
        }

        private void OnHandDragMoved(CardView view, Vector2 screenPosition)
        {
            UpdateDropHighlights(view);
        }

        /// <summary>
        /// Drop handler. Every branch delegates the actual state change to the
        /// authoritative BattleManager and returns whether it was accepted.
        /// </summary>
        private bool OnHandDrop(CardView view, Vector2 screenPosition)
        {
            ClearDropHighlights();
            if (battle == null || view == null || view.Card == null)
                return false;
            var card = view.Card;

            if (battle.Phase == Phase.Setup)
            {
                if (IsInside(playerActiveSlot, screenPosition) && battle.CanSelectStartingActive(card))
                {
                    battle.ConfirmStartingActive(card);
                    return true;
                }
                return false;
            }

            if (battle.Phase != Phase.PlayerTurn)
                return false;

            if (BattleRules.IsEvolutionOnly(card))
            {
                if (IsInside(playerActiveSlot, screenPosition) && battle.CanEvolve(card, out _))
                    return battle.PlayCardFromHand(card);
                return false;
            }

            if (BattleManager.IsOneShotSupport(card) || IsTumbalAnak(card))
                return false;

            if (battle.CanPlayToBench(card, out _))
            {
                // Bench cards are appended; the next free slot is the real destination.
                int free = battle.Player.Bench.Count;
                if (free >= 0 && free < playerBenchSlots.Length &&
                    IsInside(playerBenchSlots[free], screenPosition))
                    return battle.PlayCardFromHand(card);
            }
            return false;
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
            overDim.color = UiStyle.OverlayDim;
            overDim.raycastTarget = true; // block the board behind the result

            overGroup = overGo.AddComponent<CanvasGroup>();
            overGroup.alpha = 0f; // fades in on ShowGameOver

            var panel = MakeChildImage(overRt, "Panel", UiSpriteFactory.Rounded,
                UiStyle.ResultPanel, Vector2.zero, new Vector2(820f, 460f));
            panel.type = Image.Type.Sliced;

            var panelRing = MakeChildImage(overRt, "PanelRing", UiSpriteFactory.RoundOutline,
                UiStyle.WithAlpha(UiStyle.Gold, 0.45f), Vector2.zero, new Vector2(806f, 446f));
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

            againBtn = MakeButton("AgainBtn", "MAIN LAGI", 24f, ColGoldBright, UiStyle.InkOnGold,
                0f, 0.385f, new Vector2(300f, 80f), overRt);
            againBtn.onClick.AddListener(() =>
            {
                if (_restarting || starter == null)
                    return;
                _restarting = true;
                againBtn.interactable = false;
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
            dim.color = UiStyle.OverlayDim;
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
                UiStyle.ResultPanel, Vector2.zero, new Vector2(840f, 650f));
            panel.type = Image.Type.Sliced;
            var panelRing = MakeChildImage(panel.rectTransform, "TransformPanelRing",
                UiSpriteFactory.RoundOutline, UiStyle.WithAlpha(UiStyle.Gold, 0.45f),
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
            if (battle == null || transformOverlay == null || _combatBusy)
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

            if (battle.Phase == Phase.GameOver)
                ClearFormFx();

            bool setup = battle.Phase == Phase.Setup;
            bool myTurn = battle.Phase == Phase.PlayerTurn;

            // ---- board slots ----
            BindSlot(botActiveView, battle.Bot.Active, CardDisplayMode.Active, inspect: true);
            bool botActiveEmpty = battle.Bot.Active == null;
            botActiveFrame.SetActive(botActiveEmpty);
            if (botActiveEmpty && botActiveLabel != null)
                botActiveLabel.text = "Kosong";
            BindBenchRow(botBenchViews, botBenchFrames, botBenchLabels, battle.Bot.Bench, "Kosong", inspect: true);

            BindSlot(playerActiveView, battle.Player.Active, CardDisplayMode.Active, inspect: true);
            bool pickHint = setup && battle.Player.Active == null;
            playerActiveFrame.SetActive(battle.Player.Active == null);
            playerActiveLabel.text = pickHint ? "PILIH KARTU\nAKTIFMU" : "Kosong";
            BindBenchRow(playerBenchViews, playerBenchFrames, playerBenchLabels, battle.Player.Bench, "Kosong", inspect: true);

            // inspection preview: keep a board-sourced panel in sync with live HP/art,
            // and drop it when the shown card leaves play (KO, promotion, retreat).
            RefreshBoardInspectPreview();

            // ---- hand ----
            hand.Sync(battle.Player.Hand);
            if (setup)
            {
                foreach (var v in hand.Views)
                    v.SetEligible(v.Card != null && battle.CanSelectStartingActive(v.Card));
                hand.Layout(); // lift eligible starting cards
            }

            bool canDrag = setup || myTurn;
            hand.SetDragEnabled(canDrag);
            if (!canDrag)
                ClearDropHighlights();

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
                    turnPillText.text = "GILIRANMU!";
                    turnPillBg.color = UiStyle.PanelRaised;
                    break;
                case Phase.BotTurn:
                    turnPillText.text = "GILIRAN LAWAN!";
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
                bv.SetTarget(canRetreat && BattleRules.CanEnterActiveDirectly(bv.Card), false);
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

            // Evolution/transformation visuals run after the active slot has rebound to
            // the committed card, so the ghost can cover the new face and reveal it at
            // the flash peak without touching gameplay state.
            StartPendingFormFx();
        }

        /// <summary>
        /// Board-sourced inspect previews (both sides' active/bench) are refreshed in
        /// place so damage, healing, and evolution always show the live card values.
        /// Hand previews are owned by HandController's hover handler.
        /// </summary>
        private void RefreshBoardInspectPreview()
        {
            if (handInspect == null || handInspect.CurrentCard == null || _inspectSourceView == null)
                return;
            if (!IsBoardView(_inspectSourceView))
                return;

            var card = handInspect.CurrentCard;
            if (card.FaceDown || !BoardContains(card))
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

        private bool BoardContains(CardInstance card)
        {
            if (card == null)
                return false;
            return card == battle.Player.Active || battle.Player.Bench.Contains(card) ||
                   card == battle.Bot.Active || battle.Bot.Bench.Contains(card);
        }

        private bool IsBoardView(CardView view)
        {
            if (view == null)
                return false;
            if (view == playerActiveView || view == botActiveView)
                return true;
            if (playerBenchViews != null)
                foreach (var benchView in playerBenchViews)
                    if (benchView == view)
                        return true;
            if (botBenchViews != null)
                foreach (var benchView in botBenchViews)
                    if (benchView == view)
                        return true;
            return false;
        }

        private void BindSlot(CardView view, CardInstance card, CardDisplayMode mode, bool inspect = false)
        {
            if (view == null)
                return;
            view.Bind(card, mode);
            view.Clicked -= OnCardClicked;
            view.Clicked += OnCardClicked;
            if (inspect)
            {
                view.Hovered -= OnBoardHovered;
                view.Hovered += OnBoardHovered;
            }
        }

        private void BindBenchRow(CardView[] views, GameObject[] frames, TMP_Text[] labels,
            List<CardInstance> bench, string emptyLabel, bool inspect = false)
        {
            for (int i = 0; i < views.Length; i++)
            {
                var card = i < bench.Count ? bench[i] : null;
                views[i].Bind(card, CardDisplayMode.Bench);
                // bench cards recede slightly so the active stays the focal point
                views[i].SetDimmed(card != null, 0.08f);
                views[i].Clicked -= OnCardClicked;
                views[i].Clicked += OnCardClicked;
                if (inspect)
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
            if (battle == null)
                return;

            // Energy can change without a board rebind (retreat/attack/steal). The
            // manager remains authoritative; this only compares the new authoritative
            // value with the last value the rail displayed, so a duplicate callback can
            // never show "+1" twice.
            int now = side == Side.Player ? battle.Player.Energy : battle.Bot.Energy;
            int previous = side == Side.Player ? _lastPlayerEnergy : _lastBotEnergy;
            if (side == Side.Player)
                _lastPlayerEnergy = now;
            else
                _lastBotEnergy = now;

            RefreshEnergyTexts();

            int delta = now - previous;
            if (delta <= 0 || battle.Phase == Phase.GameOver)
                return;

            var root = side == Side.Player ? playerStaminaRoot : botStaminaRoot;
            if (root != null)
                StartCoroutine(UiFx.Punch(root, 1.18f, 0.32f));
            SpawnEnergyPopup(side, delta);
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
            if (card == null || battle == null || _combatBusy)
                return;
            if (hand != null && (hand.IsDragging || hand.ShouldIgnoreClick))
                return; // a completed drag must not also count as a click

            // Hand card?
            if (hand.FindView(card) == view)
            {
                if (battle.Phase == Phase.Setup)
                {
                    if (battle.CanSelectStartingActive(card))
                        battle.ConfirmStartingActive(card);
                    else if (BattleRules.IsEvolutionOnly(card))
                        ShakeView(view, $"{card.DisplayName} hanya bisa dimainkan sebagai evolusi dari kartu dasarnya.");
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
            if (battle == null || atk == null || def == null || battle.Phase == Phase.GameOver)
                return;
            if (_combatBusy)
                return;

            Side defenderSide = side == Side.Player ? Side.Bot : Side.Player;
            bool blocked = dmg <= 0 && _shieldPending && _shieldSide == defenderSide;
            _shieldPending = false;

            // The attack is cosmetic only: gameplay already resolved (or is about to
            // resolve) in BattleManager. Lock clicks for this short presentation so a
            // card click cannot open inspection mid-lunge.
            CancelFormFxForSide(Side.Player);
            CancelFormFxForSide(Side.Bot);
            CancelActiveSlotIn(Side.Player);
            CancelActiveSlotIn(Side.Bot);
            ClearCombatFx();
            _combatBusy = true;
            _fxEpoch++;
            int epoch = _fxEpoch;
            if (hand != null)
                hand.HideInspectPreview();
            _inspectSourceView = null;
            SetHandInput(false);

            _attackRoutine = StartCoroutine(AttackAnim(atk, side, def, dmg, effective, blocked, epoch));
        }

        private IEnumerator AttackAnim(CardInstance atk, Side side, CardInstance def, int dmg,
            bool effective, bool blocked, int epoch)
        {
            var atkView = FindBoardView(atk);
            var defView = FindBoardView(def);
            float dir = side == Side.Player ? 1f : -1f;
            var impactColor = ElementColor(atk.Element);
            var hitColor = blocked ? ColGoldBright : impactColor;

            // Everything before the first yield runs before the manager applies damage:
            // capture the post-damage HP now so the bar can hold pre-damage, then drain
            // on the impact frame (0.14s into the 0.30s lunge = its peak).
            int targetHp = Mathf.Max(0, def.CurrentHp - dmg);
            if (defView != null && dmg > 0)
                defView.TweenHpTo(targetHp, def.MaxHp, 0.35f, 0.14f);

            SetCardInput(atkView, false);
            SetCardInput(defView, false);

            if (atkView != null)
                StartCoroutine(Lunge(atkView.Root, dir, 0.30f, epoch));

            yield return new WaitForSecondsRealtime(0.14f);

            if (epoch != _fxEpoch || battle == null || battle.Phase == Phase.GameOver)
                yield break;

            // The manager may already have rebound this view to a promoted card if the
            // hit was lethal. Never shake/punch the replacement card for the old card's
            // damage - that stale motion used to leave promoted actives offset sideways.
            bool defenderStillBound = defView != null &&
                ReferenceEquals(defView.Card, def) && !def.IsKnockedOut;
            if (defenderStillBound)
            {
                StartCoroutine(CardHitReaction(defView.Root, atk.Element, 0.26f, epoch));
                StartCoroutine(ImpactFlash(defView, hitColor, effective && !blocked, epoch));
            }

            var anchor = defenderStillBound
                ? defView.Root
                : (defView != null && defView.Root.parent is RectTransform slot
                    ? slot
                    : (atkView != null ? atkView.Root : null));
            if (anchor != null)
            {
                if (blocked)
                {
                    SpawnFloatingLabel(anchor, "DITAHAN!", ColGoldBright,
                        new Vector2(0f, 96f), new Vector2(300f, 76f), 40f, 0.9f);
                    SpawnFloatingLabel(anchor, "TUMBAL ANAK", ColGold,
                        new Vector2(0f, 24f), new Vector2(240f, 48f), 26f, 0.9f, true);
                }
                else
                {
                    string label = dmg > 0 ? $"-{dmg} HP" : "0 HP";
                    var color = dmg <= 0 ? ColLavIdle : impactColor;
                    SpawnDamagePopup(anchor, label, color);
                    if (effective && dmg > 0)
                        SpawnDamagePopup(anchor, "2x!", ColRed, extra: true);
                }
            }

            // Belt-and-suspenders for non-lethal hits: once the cosmetic motion has
            // finished, snap surviving board cards back to the slot centre.
            yield return new WaitForSecondsRealtime(0.45f);
            if (epoch != _fxEpoch || battle == null || battle.Phase == Phase.GameOver)
                yield break;

            if (defenderStillBound && defView != null && ReferenceEquals(defView.Card, def) &&
                defView.Root != null)
                ResetCardTransform(defView);
            if (atkView != null && ReferenceEquals(atkView.Card, atk))
                ResetCardTransform(atkView);

            SetCardInput(atkView, true);
            SetCardInput(defView, true);
            SetHandInput(true);
            _combatBusy = false;
            _attackRoutine = null;
        }

        private IEnumerator Lunge(RectTransform rt, float dir, float duration, int epoch)
        {
            if (rt == null)
                yield break;
            Vector2 basePos = rt.anchoredPosition;
            // Keep the lunge inside the board bands: 82px pushed the active card
            // under the control bar at 1920x1080. 60px still reads as a lunge while
            // leaving the status pill/action row unobstructed.
            float reach = 60f * _k;
            float t = 0f;
            while (t < duration)
            {
                if (epoch != _fxEpoch || rt == null)
                {
                    if (rt != null)
                        rt.anchoredPosition = basePos;
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float s = Mathf.Sin(k * Mathf.PI);
                rt.anchoredPosition = basePos + new Vector2(0f, dir * reach * s);
                yield return null;
            }
            if (rt != null)
                rt.anchoredPosition = basePos;
        }

        /// <summary>
        /// Owned target reaction (shake + small scale punch) so an interrupted attack
        /// cannot leave a board card offset or oversized after game over/restart.
        /// </summary>
        private IEnumerator CardHitReaction(RectTransform rt, TcgBattle.Data.Element element,
            float duration, int epoch)
        {
            if (rt == null)
                yield break;
            Vector2 basePos = rt.anchoredPosition;
            Vector3 baseScale = rt.localScale;
            float strength = (element == TcgBattle.Data.Element.Fire ? 9f : 7f) *
                (_k > 0.01f ? _k : 1f);
            float t = 0f;
            while (t < duration)
            {
                if (epoch != _fxEpoch || rt == null)
                {
                    if (rt != null)
                    {
                        rt.anchoredPosition = basePos;
                        rt.localScale = baseScale;
                    }
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float decay = 1f - k;
                float x = Mathf.Sin(t * 90f) * strength * decay;
                rt.anchoredPosition = basePos + new Vector2(x, 0f);
                float scale = 1f + 0.045f * Mathf.Sin(k * Mathf.PI);
                rt.localScale = baseScale * scale;
                yield return null;
            }
            if (rt != null)
            {
                rt.anchoredPosition = basePos;
                rt.localScale = baseScale;
            }
        }

        private IEnumerator ImpactFlash(CardView view, Color color, bool effective, int epoch)
        {
            if (view == null || combatFxLayer == null)
                yield break;
            // Safe-root space so a KO rebind can never drag the effect onto the promoted card.
            Vector2 pos = CanvasLocal(view.Root);
            Vector2 size = view.Root.rect.size;

            SpawnRing(view.Root, color, effective ? 34f : 26f, 0.30f);

            var go = new GameObject("ImpactFlash", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(FxParent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var flash = go.GetComponent<Image>();
            flash.sprite = UiSpriteFactory.Rounded;
            flash.type = Image.Type.Sliced;
            flash.raycastTarget = false;
            flash.color = WithA(color, 0.55f);

            float t = 0f;
            while (t < 0.20f)
            {
                if (epoch != _fxEpoch || flash == null || rt == null)
                {
                    if (go != null)
                        Destroy(go);
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                float a = 0.55f * (1f - Mathf.Clamp01(t / 0.20f));
                flash.color = WithA(color, a);
                yield return null;
            }
            if (go != null)
                Destroy(go);
        }

        /// <summary>Expanding outline ring used for impacts, KOs and support destruction.</summary>
        private void SpawnRing(RectTransform anchor, Color color, float extra, float duration)
        {
            if (anchor == null || safeRoot == null)
                return;
            var go = new GameObject("FxRing", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(FxParent, false);
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
            if (card == null)
                return;
            CancelFormFxForSide(side);
            StartCoroutine(KoAnim(card, side, _fxEpoch));
        }

        private IEnumerator KoAnim(CardInstance card, Side side, int epoch)
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

            // Ghost copy that fades, drifts and shrinks while the real slot rebinds to
            // the promoted card. It is purely visual and never owns the card data.
            var ghost = new GameObject("KoGhost", typeof(RectTransform));
            var ghostRt = (RectTransform)ghost.transform;
            ghostRt.SetParent(FxParent, false);
            ghostRt.anchorMin = ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRt.sizeDelta = size;
            ghostRt.anchoredPosition = pos;
            ghostRt.localRotation = Quaternion.Euler(0f, 0f, angle);
            var ghostGroup = ghost.AddComponent<CanvasGroup>();
            ghostGroup.interactable = false;
            ghostGroup.blocksRaycasts = false;

            var backing = ghost.AddComponent<Image>();
            backing.sprite = UiSpriteFactory.Rounded;
            backing.type = Image.Type.Sliced;
            backing.color = UiStyle.PanelInset;
            backing.raycastTarget = false;
            var art = MakeChildImage(ghostRt, "GhostArt", ArtLibrary.GetCardArt(card.ArtResource), Color.white,
                Vector2.zero, size - new Vector2(10f, 10f));
            art.preserveAspect = true;

            SpawnRing(view.Root, UiStyle.WithAlpha(UiStyle.Fire, 0.85f), 40f, 0.34f);
            SpawnFloatingLabel(slotRt != null ? slotRt : view.Root, "KNOCKOUT!", ColRed,
                new Vector2(0f, 96f), new Vector2(360f, 84f), 38f, 0.82f);

            float koDrift = 46f * _k;
            Vector2 end = pos + new Vector2(0f, side == Side.Bot ? koDrift : -koDrift);
            const float duration = 0.55f;
            float t = 0f;
            while (t < duration)
            {
                if (epoch != _fxEpoch || ghostRt == null || art == null || backing == null)
                {
                    if (ghost != null)
                        Destroy(ghost);
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float ease = Mathf.SmoothStep(0f, 1f, k);
                ghostRt.anchoredPosition = Vector2.Lerp(pos, end, ease);
                ghostRt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.78f, ease);
                ghostRt.localRotation = Quaternion.Euler(0f, 0f,
                    angle + (side == Side.Bot ? -8f : 8f) * ease);
                float a = 1f - k;
                art.color = new Color(1f, 1f, 1f, a);
                backing.color = UiStyle.WithAlpha(UiStyle.PanelInset, a);
                yield return null;
            }
            if (ghost != null)
                Destroy(ghost);
        }

        private void HandleEvolved(CardInstance oldCard, CardInstance newCard, Side side)
        {
            if (oldCard == null || newCard == null)
                return;
            var view = side == Side.Player ? playerActiveView : botActiveView;
            if (view == null || view.Card == null)
                return;

            // Capture the old face now, before RefreshAll rebinds the slot. The
            // authoritative card has already changed; this snapshot is visual only.
            _pendingFormFx.Add(new PendingFormFx
            {
                Side = side,
                NewCard = newCard,
                OldVisual = view.CaptureVisual(),
                Heading = "EVOLVED!",
                Path = $"{oldCard.DisplayName.ToUpperInvariant()} \u2192 {newCard.DisplayName.ToUpperInvariant()}",
            });
        }

        private void HandlePromoted(CardInstance card, Side side)
        {
            // Evolution has its own form-change FX; do not also queue the generic
            // bench/hand slot-in animation for the same card.
            if (HasPendingFormFx(card))
                return;
            if (side == Side.Player)
                _pendingPromoPlayer = card;
            else
                _pendingPromoBot = card;
        }

        private bool HasPendingFormFx(CardInstance card)
        {
            for (int i = 0; i < _pendingFormFx.Count; i++)
                if (ReferenceEquals(_pendingFormFx[i].NewCard, card))
                    return true;
            return false;
        }

        /// <summary>
        /// Start the cosmetic form reveal after RefreshAll has rebound the active slot.
        /// Only one FX may own a side at a time; a newer form change cancels the old one.
        /// </summary>
        private void StartPendingFormFx()
        {
            if (_pendingFormFx.Count == 0)
                return;
            if (formFxLayer == null)
            {
                _pendingFormFx.Clear();
                return;
            }

            var pending = new List<PendingFormFx>(_pendingFormFx);
            _pendingFormFx.Clear();
            float k = _k > 0.01f ? _k : 1f;

            foreach (var p in pending)
            {
                var target = p.Side == Side.Player ? playerActiveView : botActiveView;
                if (target == null || target.Card == null || !ReferenceEquals(target.Card, p.NewCard))
                    continue;

                var newVisual = target.CaptureVisual();
                var oldFx = p.Side == Side.Player ? formFxPlayer : formFxBot;
                if (oldFx != null)
                    oldFx.CancelImmediate();

                var fx = CardFormFx.Play(formFxLayer, target, p.OldVisual, newVisual,
                    p.Heading, p.Path, k, ColGoldBright, OnFormFxFinished);
                if (p.Side == Side.Player)
                    formFxPlayer = fx;
                else
                    formFxBot = fx;
            }
        }

        private void OnFormFxFinished(CardFormFx fx)
        {
            if (formFxPlayer == fx)
                formFxPlayer = null;
            if (formFxBot == fx)
                formFxBot = null;
        }

        /// <summary>Cancel every cosmetic form animation and restore its target alpha.</summary>
        private void ClearFormFx()
        {
            if (formFxPlayer != null)
            {
                formFxPlayer.CancelImmediate();
                formFxPlayer = null;
            }
            if (formFxBot != null)
            {
                formFxBot.CancelImmediate();
                formFxBot = null;
            }
            _pendingFormFx.Clear();

            if (formFxLayer != null)
            {
                var fxs = formFxLayer.GetComponentsInChildren<CardFormFx>(true);
                for (int i = fxs.Length - 1; i >= 0; i--)
                    if (fxs[i] != null)
                        fxs[i].CancelImmediate();
            }
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
            SpawnRing(pips[idx].rectTransform, side == Side.Player ? ColGoldBright : ColRed, 18f, 0.34f);
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

        /// <summary>
        /// Tumbal Anak is consumed by BattleManager before the attack event. Remember
        /// which side was protected so the zero-damage hit can show a clear block
        /// response instead of a misleading "-0 HP" number.
        /// </summary>
        private void HandleSupportUsed(CardInstance card, Side side)
        {
            if (card == null || card.Support == null)
                return;
            if (card.Support.effect == TcgBattle.Data.SupportEffect.TumbalAnakHandPassive)
            {
                _shieldPending = true;
                _shieldSide = side;
            }
        }

        /// <summary>Kucing Liar sniping a bench Mbok Sumi: one-shot destruction feedback.</summary>
        private void HandleSupportDestroyed(CardInstance card, Side side)
        {
            var view = FindBoardView(card);
            if (view == null)
                return;
            SpawnRing(view.Root, UiStyle.WithAlpha(UiStyle.Fire, 0.85f), 34f, 0.32f);
            StartCoroutine(UiFx.Shake(view.Root, 7f, 0.24f));
            SpawnDamagePopup(view.Root, "HANCUR", ColRed);
        }

        private void AnimateSlotIn(CardView view, Vector2 fromOffset)
        {
            if (view == null || (battle != null && battle.Phase == Phase.GameOver))
                return;

            bool playerActive = view == playerActiveView;
            bool botActive = view == botActiveView;
            if (playerActive && _playerActiveSlotIn != null)
            {
                StopCoroutine(_playerActiveSlotIn);
                _playerActiveSlotIn = null;
                ResetCardTransform(view);
            }
            if (botActive && _botActiveSlotIn != null)
            {
                StopCoroutine(_botActiveSlotIn);
                _botActiveSlotIn = null;
                ResetCardTransform(view);
            }

            var routine = StartCoroutine(SlotInAnim(view, fromOffset, playerActive, botActive));
            if (playerActive)
                _playerActiveSlotIn = routine;
            else if (botActive)
                _botActiveSlotIn = routine;
        }

        private IEnumerator SlotInAnim(CardView view, Vector2 fromOffset, bool playerActive, bool botActive)
        {
            var rt = view != null ? view.Root : null;
            if (rt == null)
            {
                if (playerActive)
                    _playerActiveSlotIn = null;
                if (botActive)
                    _botActiveSlotIn = null;
                yield break;
            }
            // Board card views are always centred in their slot when at rest. Target
            // that invariant explicitly rather than capturing the live mid-flight
            // position (which could bake a previous shake offset into the new card).
            Vector2 to = Vector2.zero;
            rt.anchoredPosition = to + fromOffset;
            rt.localScale = Vector3.one * 0.75f;
            yield return UiFx.Move(rt, to, 0.28f);
            // restore full size before the settle punch: UiFx.Punch returns to the scale
            // it started at (leaving 0.75 here made every promoted/benched card small).
            if (rt == null)
            {
                if (playerActive)
                    _playerActiveSlotIn = null;
                if (botActive)
                    _botActiveSlotIn = null;
                yield break;
            }
            rt.localScale = Vector3.one;
            yield return UiFx.Punch(rt, 1.08f, 0.22f);
            // Highlight the new active only after the settle, so a KO ghost's
            // "KNOCKOUT!" label and this "AKTIF!" label never overlap at one slot.
            if ((playerActive || botActive) && view != null && view.Card != null &&
                battle != null && battle.Phase != Phase.GameOver)
            {
                yield return new WaitForSecondsRealtime(0.08f);
                SpawnRing(rt, ColGoldBright, 20f, 0.34f);
                SpawnFloatingLabel(rt, "AKTIF!", ColGoldBright,
                    new Vector2(0f, 110f), new Vector2(180f, 54f), 24f, 0.62f);
            }
            if (playerActive)
                _playerActiveSlotIn = null;
            if (botActive)
                _botActiveSlotIn = null;
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
            SpawnFloatingLabel(anchor, text, color,
                extra ? new Vector2(0f, 24f) : new Vector2(0f, 96f),
                extra ? new Vector2(120f, 48f) : new Vector2(300f, 76f),
                extra ? 28f : 44f, 0.9f, extra);
        }

        private void SpawnScorePopup(RectTransform pip)
        {
            SpawnFloatingLabel(pip, "+1", ColGoldBright,
                new Vector2(70f, 56f), new Vector2(130f, 54f), 30f, 0.9f, true);
        }

        private void SpawnEnergyPopup(Side side, int delta)
        {
            var anchor = side == Side.Player ? playerStaminaRoot : botStaminaRoot;
            if (anchor == null)
                return;
            SpawnFloatingLabel(anchor, $"+{delta} ENERGI", ColGoldBright,
                new Vector2(-110f, 0f), new Vector2(190f, 48f), 22f, 0.8f, true);
        }

        /// <summary>
        /// Shared floating chip used by damage, block, KO, score and energy feedback.
        /// The object lives under the combat FX layer so it can be cleared atomically
        /// on game over/restart and never intercepts input.
        /// </summary>
        private void SpawnFloatingLabel(RectTransform anchor, string text, Color color,
            Vector2 offset, Vector2 size, float fontSize, float duration, bool small = false)
        {
            if (anchor == null)
                return;
            float k = _k > 0.01f ? _k : 1f;
            var go = new GameObject("FloatLabel", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(FxParent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size * k;
            rt.anchoredPosition = CanvasLocal(anchor) + offset * k;

            var chip = go.AddComponent<Image>();
            chip.sprite = UiSpriteFactory.Rounded;
            chip.type = Image.Type.Sliced;
            chip.color = UiStyle.WithAlpha(UiStyle.PanelInset, small ? 0.5f : 0.45f);
            chip.raycastTarget = false;

            var tmp = MakeText("Text", rt, fontSize * k, color, TextAlignmentOptions.Center, bold: true);
            Stretch(tmp.rectTransform, 8, 4, 8, 4);
            tmp.text = text;

            StartCoroutine(PopupAnim(rt, small, duration));
        }

        private IEnumerator PopupAnim(RectTransform rt, bool small, float duration)
        {
            if (rt == null)
                yield break;
            float t = 0f;
            var tmp = rt.GetComponentInChildren<TMP_Text>();
            var chip = rt.GetComponent<Image>();
            Vector2 start = rt.anchoredPosition;
            Vector2 end = start + new Vector2(0f, small ? 56f : 84f) * _k;
            float startScale = small ? 0.82f : 0.72f;
            rt.localScale = Vector3.one * startScale;
            while (t < duration)
            {
                if (rt == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
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
            if (rt != null)
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
            {
                StartCoroutine(UiFx.Punch(turnPillBg.rectTransform, 1.10f, 0.34f));
                SpawnRing(turnPillBg.rectTransform,
                    side == Side.Player ? ColGoldBright : ColRed, 12f, 0.34f);
            }

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
            statusText.text = mine ? "GILIRANMU!" : "GILIRAN LAWAN!";
            statusText.color = mine ? ColInk : ColIvory;
            yield return UiFx.Punch(statusPill.rectTransform, 1.07f, 0.3f);
            yield return new WaitForSecondsRealtime(0.9f);
            if (statusPill == null || statusText == null)
                yield break;
            statusPill.color = UiStyle.StatusPill;
            statusText.color = UiStyle.StatusText;
            statusText.text = statusBaseline;
            turnBannerRoutine = null;
        }

        private void ShowGameOver(Side winner)
        {
            if (overOverlay == null)
                return;

            // Stop every cosmetic routine first. The result screen must never be
            // overwritten by a late lunge, KO ghost, damage chip, turn banner or energy
            // pop that was already in flight when the manager reached GameOver.
            StopAllCoroutines();
            ResetHudTransforms();
            if (statusPill != null)
                statusPill.color = UiStyle.StatusPill;
            if (statusText != null)
            {
                statusText.color = UiStyle.StatusText;
                statusText.text = statusBaseline;
            }
            turnBannerRoutine = null;
            transformFade = null;
            _combatBusy = false;
            ClearCombatFx();
            ClearFormFx();
            FinishDrawAnimations();
            _pendingPromoPlayer = _pendingPromoBot = null;
            _pendingBenchPlayer = _pendingBenchBot = null;
            _pendingRetreatPlayer = _pendingRetreatBot = null;
            CloseTransformOverlayImmediate();
            if (hand != null)
            {
                hand.SetDragEnabled(false);   // cancel any in-flight drag
                hand.HideInspectPreview();    // inspection preview never outlives the battle
            }
            ClearDropHighlights();
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
            if (overText != null)
                StartCoroutine(UiFx.Punch(overText.rectTransform, 1.06f, 0.34f));

            bool playerWins = winner == Side.Player;
            if (overText != null)
            {
                overText.text = playerWins ? "KEMENANGAN!" : "KEKALAHAN!";
                overText.color = playerWins ? ColInk : ColIvory;
            }
            var ribbon = overRibbonRt != null ? overRibbonRt.GetComponent<Image>() : null;
            if (ribbon != null)
                ribbon.color = playerWins ? ColGoldBright : ColMaroon;
            var accent = playerWins ? ColGoldBright : ColRed;
            if (overDiamondL != null)
                overDiamondL.color = accent;
            if (overDiamondR != null)
                overDiamondR.color = accent;
            if (overScoreText != null)
            {
                overScoreText.color = playerWins ? ColGoldBright : ColIvory;
                overScoreText.text = battle != null
                    ? $"Skor akhir  {battle.Player.Points} - {battle.Bot.Points}"
                    : "Skor akhir";
            }

            _restarting = false;
            if (againBtn != null)
                againBtn.interactable = true;
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

        private Transform FxParent => combatFxLayer != null ? combatFxLayer : safeRoot;

        private static Color ElementColor(TcgBattle.Data.Element element)
        {
            return element == TcgBattle.Data.Element.Fire
                ? UiStyle.Ember
                : UiStyle.Dark;
        }

        /// <summary>
        /// Board-card input ownership for the short combat presentation. The CanvasGroup
        /// is restored after the effect (or by ClearCombatFx on game over/restart), so a
        /// card can never stay unclickable or keep intercepting inspection clicks.
        /// </summary>
        private static void SetCardInput(CardView view, bool enabled)
        {
            if (view == null || view.Root == null)
                return;
            var group = view.Root.GetComponent<CanvasGroup>();
            if (group == null)
                group = view.Root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = enabled;
            group.interactable = enabled;
        }

        private static void ResetCardTransform(CardView view)
        {
            if (view == null || view.Root == null)
                return;
            view.Root.anchoredPosition = Vector2.zero;
            view.Root.localRotation = Quaternion.identity;
            view.Root.localScale = Vector3.one;
        }

        private void ResetBoardCardTransforms()
        {
            ResetCardTransform(playerActiveView);
            ResetCardTransform(botActiveView);
            if (playerBenchViews != null)
                for (int i = 0; i < playerBenchViews.Length; i++)
                    ResetCardTransform(playerBenchViews[i]);
            if (botBenchViews != null)
                for (int i = 0; i < botBenchViews.Length; i++)
                    ResetCardTransform(botBenchViews[i]);
        }

        private void SetBoardCardInput(bool enabled)
        {
            SetCardInput(playerActiveView, enabled);
            SetCardInput(botActiveView, enabled);
            if (playerBenchViews != null)
                for (int i = 0; i < playerBenchViews.Length; i++)
                    SetCardInput(playerBenchViews[i], enabled);
            if (botBenchViews != null)
                for (int i = 0; i < botBenchViews.Length; i++)
                    SetCardInput(botBenchViews[i], enabled);
        }

        private void SetHandInput(bool enabled)
        {
            if (hand == null)
                return;
            var group = hand.GetComponent<CanvasGroup>();
            if (group == null)
                group = hand.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = enabled;
            group.interactable = enabled;
        }

        /// <summary>
        /// Snap every persistent HUD transform owned by a transient punch back to rest.
        /// StopAllCoroutines can abort UiFx.Punch between its first and last frame, and
        /// Punch restores the scale it captured, so an aborted punch would otherwise
        /// leave score pips, stamina badges or the result ribbon oversized on the end
        /// screen and into the next match. All of these elements have a scale-1 rest
        /// pose, so this never overwrites a layout pose.
        /// </summary>
        private void ResetHudTransforms()
        {
            ResetScale(turnPillBg != null ? turnPillBg.rectTransform : null);
            ResetScale(statusPill != null ? statusPill.rectTransform : null);
            ResetScale(playerStaminaRoot);
            ResetScale(botStaminaRoot);
            if (playerPips != null)
                for (int i = 0; i < playerPips.Length; i++)
                    ResetScale(playerPips[i] != null ? playerPips[i].rectTransform : null);
            if (botPips != null)
                for (int i = 0; i < botPips.Length; i++)
                    ResetScale(botPips[i] != null ? botPips[i].rectTransform : null);
            ResetScale(overRibbonRt);
            ResetScale(overText != null ? overText.rectTransform : null);
            ResetScale(coinImage != null ? coinImage.rectTransform : null);
            ResetScale(coinRing != null ? coinRing.rectTransform : null);
            ResetScale(coinText != null ? coinText.rectTransform : null);
        }

        private static void ResetScale(RectTransform rt)
        {
            if (rt != null)
                rt.localScale = Vector3.one;
        }

        /// <summary>
        /// Atomically cancel transient combat visuals and restore every board transform
        /// and raycast flag. Called on game over, unbind, restart and teardown.
        /// </summary>
        private void ClearCombatFx()
        {
            _fxEpoch++;
            _combatBusy = false;
            _attackRoutine = null;
            _playerActiveSlotIn = null;
            _botActiveSlotIn = null;
            _shieldPending = false;

            if (combatFxLayer != null)
            {
                for (int i = combatFxLayer.childCount - 1; i >= 0; i--)
                {
                    var child = combatFxLayer.GetChild(i);
                    if (child == null)
                        continue;
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            ResetBoardCardTransforms();
            SetBoardCardInput(true);
            SetHandInput(true);
        }

        private void CancelFormFxForSide(Side side)
        {
            if (side == Side.Player && formFxPlayer != null)
            {
                formFxPlayer.CancelImmediate();
                formFxPlayer = null;
            }
            if (side == Side.Bot && formFxBot != null)
            {
                formFxBot.CancelImmediate();
                formFxBot = null;
            }
            _pendingFormFx.RemoveAll(p => p.Side == side);
        }

        private void CancelActiveSlotIn(Side side)
        {
            if (side == Side.Player)
            {
                if (_playerActiveSlotIn != null)
                {
                    StopCoroutine(_playerActiveSlotIn);
                    _playerActiveSlotIn = null;
                }
                ResetCardTransform(playerActiveView);
            }
            else
            {
                if (_botActiveSlotIn != null)
                {
                    StopCoroutine(_botActiveSlotIn);
                    _botActiveSlotIn = null;
                }
                ResetCardTransform(botActiveView);
            }
        }

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
            img.color = UiStyle.SlotEmpty;
            img.raycastTarget = false;

            var innerRing = MakeImage("InnerRing", UiSpriteFactory.RoundOutline, UiStyle.SlotEmptyRing);
            innerRing.type = Image.Type.Sliced;
            var ringRt = innerRing.rectTransform;
            ringRt.SetParent(rt, false);
            Stretch(ringRt, 5, 5, 5, 5);

            labelText = MakeText("EmptyLabel", rt, fontSize,
                UiStyle.WithAlpha(UiStyle.IvoryMuted, 0.78f), TextAlignmentOptions.Center, bold: true);
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

            // A thin rim ties flat buttons to the card frames without competing with
            // ButtonFx's hover/press colour animation.
            var outline = MakeImage(name + "Outline", UiSpriteFactory.RoundOutline,
                UiStyle.ButtonOutline(bg));
            outline.type = Image.Type.Sliced;
            outline.rectTransform.SetParent(rt, false);
            Stretch(outline.rectTransform, 1, 1, 1, 1);

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
            // Fixed right-side inspection panel: right edge of the safe area, vertically
            // centred in the clear band between the bot HUD and the player HUD.
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.497f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(380f, 531f);
            rt.anchoredPosition = new Vector2(-20f, 0f);
            handInspect = go.AddComponent<CardInspectPreview>();
            handInspect.SetPanelWidth(380f);
            hand.SetInspectPreview(handInspect);
        }

        /// <summary>
        /// Give the fixed preview as much width as the right rail can spare without
        /// covering the action row. At 1920x1080 it gets the full 350 px; narrower
        /// aspects shrink it rather than overlapping the End Turn button.
        /// </summary>
        private void FitInspectPanel()
        {
            if (handInspect == null || safeRoot == null || endBtn == null)
                return;
            var ert = (RectTransform)endBtn.transform;
            Vector3 rightWorld = ert.TransformPoint(new Vector3(ert.rect.xMax, ert.rect.center.y, 0f));
            Vector2 rightLocal = safeRoot.InverseTransformPoint(rightWorld);
            float available = safeRoot.rect.width * 0.5f - rightLocal.x;
            handInspect.SetPanelWidth(available - 40f);
        }

        /// <summary>
        /// Every face-up board card (both sides' active and bench) feeds the same fixed
        /// right-side inspection panel. Face-down cards never open it, so no hidden
        /// information is leaked; hovering stays a passive mirror with no gameplay effect.
        /// </summary>
        private void OnBoardHovered(CardView view, bool entered)
        {
            if (_combatBusy)
                return;
            if (entered)
            {
                if (view.Card == null || view.Card.FaceDown)
                {
                    _inspectSourceView = null;
                    if (handInspect != null)
                        handInspect.Hide();
                    return;
                }
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
    }
}
