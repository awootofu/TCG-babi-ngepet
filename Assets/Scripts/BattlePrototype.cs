using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// A self-contained, placeholder-art 2D card battle prototype.
/// Attach this script to an empty GameObject in a Unity scene and press Play.
/// Uses Unity's built-in UI system; no external packages or art assets required.
/// </summary>
public class BattlePrototype : MonoBehaviour
{
    private enum Element { Dark, Fire }
    private enum Side { Player, Bot }

    [Serializable]
    private class CardDefinition
    {
        public string cardName;
        public Element element;
        public int maxHP;
        public int attack;
        public int attackCost;
        public int retreatCost;
    }

    private class CardInstance
    {
        public CardDefinition data;
        public int hp;
        public bool IsKnockedOut => hp <= 0;

        public CardInstance(CardDefinition definition)
        {
            data = definition;
            hp = definition.maxHP;
        }
    }

    private class PlayerState
    {
        public string displayName;
        public Side side;
        public List<CardInstance> deck = new List<CardInstance>();
        public List<CardInstance> hand = new List<CardInstance>();
        public List<CardInstance> bench = new List<CardInstance>();
        public CardInstance active;
        public int energy;
        public int points;
        public bool hasAttackedThisTurn;
    }

    [Header("Prototype settings")]
    [SerializeField] private int deckSize = 20;
    [SerializeField] private int startingHandSize = 5;
    [SerializeField] private int benchLimit = 3;
    [SerializeField] private int pointsToWin = 3;
    [SerializeField] private int energyPerTurn = 1;
    [SerializeField] private int baseAttackCost = 1;
    [SerializeField] private int baseRetreatCost = 1;
    [SerializeField] private int minPlaceholderHP = 70;
    [SerializeField] private int maxPlaceholderHP = 110;
    [SerializeField] private int minPlaceholderATK = 20;
    [SerializeField] private int maxPlaceholderATK = 40;
    [SerializeField] private int botActionDelay = 1;

    private PlayerState player;
    private PlayerState bot;
    private bool gameOver;
    private bool choosingStartingActive = true;
    private bool botTurnRunning;
    private bool playerStartsFirst;
    private string statusMessage = "Choose one card from your hand to be your Active card.";

    // Runtime-created UI: no Canvas or Inspector references required.
    private Canvas canvas;
    private Font uiFont;
    private Text headerText;
    private Text statusText;
    private Text playerInfoText;
    private Text botInfoText;
    private Text playerActiveText;
    private Text botActiveText;
    private Text playerBenchText;
    private Text botBenchText;
    private Text handText;
    private Text energyText;
    private Text pointsText;
    private Text hintText;
    private Button attackButton;
    private Button retreatButton;
    private Button endTurnButton;
    private Button restartButton;
    private Transform handButtonContainer;
    private Transform benchButtonContainer;

    private void Start()
    {
        BuildUI();
        StartNewGame();
    }

    private void StartNewGame()
    {
        StopAllCoroutines();
        gameOver = false;
        botTurnRunning = false;
        choosingStartingActive = true;

        player = CreatePlayer("BABI NGEPET", Side.Player, Element.Dark);
        bot = CreatePlayer("WARGA BOT", Side.Bot, Element.Fire);

        Shuffle(player.deck);
        Shuffle(bot.deck);

        DrawCards(player, startingHandSize);
        DrawCards(bot, startingHandSize);

        // The bot selects its starting Active card automatically.
        if (bot.hand.Count > 0)
        {
            bot.active = bot.hand[0];
            bot.hand.RemoveAt(0);
        }

        playerStartsFirst = UnityEngine.Random.value >= 0.5f;
        statusMessage = playerStartsFirst
            ? "Coin flip: YOU go first. Choose your Active card."
            : "Coin flip: BOT goes first. Choose your Active card; then the bot will take its turn.";
        RefreshUI();
    }

    private PlayerState CreatePlayer(string name, Side side, Element element)
    {
        var state = new PlayerState { displayName = name, side = side, energy = 0, points = 0 };

        for (int i = 0; i < deckSize; i++)
        {
            int hp = UnityEngine.Random.Range(minPlaceholderHP, maxPlaceholderHP + 1);
            int atk = UnityEngine.Random.Range(minPlaceholderATK, maxPlaceholderATK + 1);
            var definition = new CardDefinition
            {
                cardName = (element == Element.Dark ? "Babi Ngepet " : "Warga ") + (i + 1),
                element = element,
                maxHP = hp,
                attack = atk,
                attackCost = baseAttackCost,
                retreatCost = baseRetreatCost
            };
            state.deck.Add(new CardInstance(definition));
        }

        return state;
    }

    private void DrawCards(PlayerState state, int count)
    {
        for (int i = 0; i < count && state.deck.Count > 0; i++)
        {
            int last = state.deck.Count - 1;
            state.hand.Add(state.deck[last]);
            state.deck.RemoveAt(last);
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // Runs automatically after any scene loads, so pressing Play works without manual setup.
    // A BattlePrototype already placed in the scene takes precedence and prevents duplicates.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoRunPrototype()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<BattlePrototype>() != null) return;
#else
        if (FindObjectOfType<BattlePrototype>() != null) return;
#endif
        new GameObject("BattlePrototype", typeof(BattlePrototype));
    }

    private static EventSystem FindExistingEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<EventSystem>();
#else
        return FindObjectOfType<EventSystem>();
#endif
    }

    private static Font LoadUiFont()
    {
        // Unity 2022.2+ ships the built-in font as LegacyRuntime.ttf; older versions call it Arial.ttf.
        try
        {
            Font legacyRuntime = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyRuntime != null) return legacyRuntime;
        }
        catch (Exception)
        {
            // LegacyRuntime.ttf does not exist in this Unity version; fall through.
        }

        try
        {
            Font arial = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (arial != null) return arial;
        }
        catch (Exception)
        {
            // Arial.ttf was removed in this Unity version; fall through.
        }

        // Last resort for unusual installs: render with a system font.
        return Font.CreateDynamicFontFromOSFont("Arial", 16);
    }

    private void BuildUI()
    {
        uiFont = LoadUiFont();

        // UI buttons need an EventSystem to receive pointer input.
        if (FindExistingEventSystem() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        var canvasObject = new GameObject("Runtime Battle Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        CreatePanel("Background", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Vector2(1280, 720), new Color(0.075f, 0.085f, 0.12f, 1f));

        headerText = CreateText("Title", canvas.transform, "TCG BATTLE PROTOTYPE", 28, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -34), new Vector2(500, 50), TextAnchor.MiddleCenter, Color.white);

        botInfoText = CreateText("Bot Info", canvas.transform, "", 18, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -90), new Vector2(900, 34), TextAnchor.MiddleCenter, new Color(1f, 0.72f, 0.55f));
        botActiveText = CreateText("Bot Active", canvas.transform, "", 20, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -135), new Vector2(900, 45), TextAnchor.MiddleCenter, Color.white);
        botBenchText = CreateText("Bot Bench", canvas.transform, "", 16, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -178), new Vector2(1050, 35), TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f));

        statusText = CreateText("Status", canvas.transform, "", 19, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 58), new Vector2(1100, 70), TextAnchor.MiddleCenter, new Color(0.8f, 0.85f, 1f));

        playerInfoText = CreateText("Player Info", canvas.transform, "", 18, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 280), new Vector2(900, 34), TextAnchor.MiddleCenter, new Color(0.8f, 0.7f, 1f));
        playerActiveText = CreateText("Player Active", canvas.transform, "", 20, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 236), new Vector2(900, 45), TextAnchor.MiddleCenter, Color.white);
        playerBenchText = CreateText("Player Bench", canvas.transform, "", 16, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 196), new Vector2(1050, 32), TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f));

        handText = CreateText("Hand Label", canvas.transform, "YOUR HAND — click a card to play it or select your Active card", 15,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 155), new Vector2(1000, 28),
            TextAnchor.MiddleCenter, Color.white);

        handButtonContainer = CreateContainer("Hand Buttons", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 105), new Vector2(1100, 65));
        benchButtonContainer = CreateContainer("Bench Buttons", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 60), new Vector2(1100, 38));

        energyText = CreateText("Energy", canvas.transform, "", 17, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(125, 18), new Vector2(220, 30), TextAnchor.MiddleLeft, Color.white);
        pointsText = CreateText("Points", canvas.transform, "", 17, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-130, 18), new Vector2(220, 30), TextAnchor.MiddleRight, Color.white);
        hintText = CreateText("Hint", canvas.transform, "Prototype art uses text placeholders. Final illustrations can be added later.", 12,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, -4), new Vector2(1000, 22),
            TextAnchor.MiddleCenter, new Color(0.65f, 0.68f, 0.75f));

        attackButton = CreateButton("Attack", canvas.transform, "ATTACK", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-120, 105), new Vector2(160, 48), Attack);
        retreatButton = CreateButton("Retreat", canvas.transform, "RETREAT", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-295, 105), new Vector2(160, 48), Retreat);
        endTurnButton = CreateButton("End Turn", canvas.transform, "END TURN", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-120, 55), new Vector2(160, 42), EndPlayerTurn);
        restartButton = CreateButton("Restart", canvas.transform, "PLAY AGAIN", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -10), new Vector2(190, 50), StartNewGame);
        restartButton.gameObject.SetActive(false);
    }

    private void CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
    }

    private Text CreateText(string name, Transform parent, string value, int size, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 dimensions, TextAnchor alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = position;
        rt.sizeDelta = dimensions;
        var text = go.GetComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Transform CreateContainer(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = position;
        rt.sizeDelta = dimensions;
        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return go.transform;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 dimensions, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = position;
        rt.sizeDelta = dimensions;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.28f, 0.31f, 0.42f, 1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var text = CreateText(name + " Label", go.transform, label, 15, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, Color.white);
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void RefreshUI()
    {
        if (player == null || bot == null) return;

        botInfoText.text = $"{bot.displayName}   |   Deck: {bot.deck.Count}   Hand: {bot.hand.Count}   Energy: {bot.energy}";
        playerInfoText.text = $"{player.displayName}   |   Deck: {player.deck.Count}   Hand: {player.hand.Count}   Energy: {player.energy}";
        botActiveText.text = "ACTIVE: " + DescribeCard(bot.active);
        playerActiveText.text = "ACTIVE: " + DescribeCard(player.active);
        botBenchText.text = "BOT BENCH: " + DescribeBench(bot.bench);
        playerBenchText.text = "YOUR BENCH: " + DescribeBench(player.bench);
        statusText.text = statusMessage;
        energyText.text = $"Energy: {player.energy}";
        pointsText.text = $"Points: YOU {player.points}  —  {bot.points} BOT";

        bool playerCanAct = !gameOver && !choosingStartingActive && !botTurnRunning;
        attackButton.interactable = playerCanAct && player.active != null && !player.hasAttackedThisTurn
            && player.energy >= player.active.data.attackCost;
        retreatButton.interactable = playerCanAct && player.active != null && player.bench.Count > 0
            && player.energy >= player.active.data.retreatCost;
        endTurnButton.interactable = playerCanAct;

        restartButton.gameObject.SetActive(gameOver);

        ClearChildren(handButtonContainer);
        foreach (CardInstance card in player.hand)
        {
            string label = $"{ShortName(card.data.cardName)}\n{card.data.maxHP} HP / {card.data.attack} ATK";
            CardInstance captured = card;
            Button button = CreateButton("Hand Card", handButtonContainer, label,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 58),
                () => OnHandCardClicked(captured));
            button.interactable = !gameOver && !botTurnRunning && (choosingStartingActive || player.bench.Count < benchLimit);
            button.GetComponent<Image>().color = card.data.element == Element.Dark
                ? new Color(0.32f, 0.24f, 0.48f, 1f)
                : new Color(0.65f, 0.31f, 0.16f, 1f);
        }

        ClearChildren(benchButtonContainer);
        foreach (CardInstance card in player.bench)
        {
            CardInstance captured = card;
            Button button = CreateButton("Bench Card", benchButtonContainer,
                $"Switch to {ShortName(card.data.cardName)} ({card.hp} HP)",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230, 34),
                () => SwitchActiveTo(captured));
            button.interactable = !gameOver && !choosingStartingActive && !botTurnRunning;
        }
    }

    private string DescribeCard(CardInstance card)
    {
        if (card == null) return "(EMPTY)";
        return $"{card.data.cardName} [{card.data.element}]  HP {card.hp}/{card.data.maxHP}  ATK {card.data.attack}  Cost {card.data.attackCost}";
    }

    private string DescribeBench(List<CardInstance> cards)
    {
        if (cards.Count == 0) return "(empty)";
        List<string> names = new List<string>();
        foreach (CardInstance c in cards) names.Add($"{ShortName(c.data.cardName)} {c.hp}HP");
        return string.Join("   |   ", names);
    }

    private string ShortName(string name)
    {
        return name.Replace("Babi Ngepet ", "BN ").Replace("Warga ", "W ");
    }

    private void OnHandCardClicked(CardInstance card)
    {
        if (gameOver || botTurnRunning || !player.hand.Contains(card)) return;

        if (choosingStartingActive)
        {
            player.active = card;
            player.hand.Remove(card);
            choosingStartingActive = false;
            statusMessage = $"{card.data.cardName} is now Active. Play cards to your bench, then attack or end your turn.";
            RefreshUI();
            if (playerStartsFirst)
            {
                StartTurn(player);
                DrawCards(player, 1);
                statusMessage += " Your first turn has started: 1 energy gained and 1 card drawn.";
                RefreshUI();
            }
            else
            {
                StartCoroutine(BotFirstTurn());
            }
            return;
        }

        if (player.bench.Count >= benchLimit)
        {
            statusMessage = "Your bench is full.";
            return;
        }

        player.hand.Remove(card);
        player.bench.Add(card);
        statusMessage = $"{card.data.cardName} was placed on your bench.";
        RefreshUI();
    }

    private void Attack()
    {
        if (gameOver || choosingStartingActive || botTurnRunning || player.active == null || player.hasAttackedThisTurn) return;
        if (player.energy < player.active.data.attackCost)
        {
            statusMessage = "Not enough energy to attack.";
            return;
        }

        player.energy -= player.active.data.attackCost;
        player.hasAttackedThisTurn = true;
        ResolveAttack(player, bot);
        if (!gameOver)
            StartCoroutine(BeginBotTurnAfterDelay());
        RefreshUI();
    }

    private void ResolveAttack(PlayerState attacker, PlayerState defender)
    {
        if (attacker.active == null || defender.active == null) return;

        CardInstance attackingCard = attacker.active;
        CardInstance defendingCard = defender.active;
        int damage = attackingCard.data.attack;
        bool effective = attackingCard.data.element == Element.Fire && defendingCard.data.element == Element.Dark;
        if (effective) damage *= 2;

        defendingCard.hp = Mathf.Max(0, defendingCard.hp - damage);
        statusMessage = $"{attackingCard.data.cardName} attacks for {damage} damage"
            + (effective ? " — SUPER EFFECTIVE!" : ".")
            + $" {defendingCard.data.cardName} has {defendingCard.hp} HP left.";

        if (defendingCard.IsKnockedOut)
        {
            attacker.points++;
            defender.active = null;
            statusMessage += $" KNOCKOUT! {attacker.displayName} gains 1 point.";

            if (attacker.points >= pointsToWin)
            {
                EndGame(attacker);
                return;
            }

            PromoteFromBenchOrLose(defender);
        }
    }

    private void PromoteFromBenchOrLose(PlayerState state)
    {
        if (state.bench.Count > 0)
        {
            state.active = state.bench[0];
            state.bench.RemoveAt(0);
            statusMessage += $" {state.displayName} promotes {state.active.data.cardName} to Active.";
        }
        else
        {
            EndGame(state == player ? bot : player);
        }
    }

    private void Retreat()
    {
        if (gameOver || choosingStartingActive || botTurnRunning || player.active == null || player.bench.Count == 0) return;
        if (player.energy < player.active.data.retreatCost)
        {
            statusMessage = "Not enough energy to retreat.";
            return;
        }

        player.energy -= player.active.data.retreatCost;
        CardInstance oldActive = player.active;
        player.active = player.bench[0];
        player.bench.RemoveAt(0);
        player.bench.Add(oldActive);
        statusMessage = $"Retreated {oldActive.data.cardName}. {player.active.data.cardName} is now Active.";
        RefreshUI();
    }

    private void SwitchActiveTo(CardInstance target)
    {
        if (gameOver || choosingStartingActive || botTurnRunning || player.active == null || target == null || !player.bench.Contains(target)) return;
        if (player.energy < player.active.data.retreatCost)
        {
            statusMessage = $"You need {player.active.data.retreatCost} energy to retreat.";
            return;
        }

        player.energy -= player.active.data.retreatCost;
        CardInstance oldActive = player.active;
        player.active = target;
        player.bench.Remove(target);
        player.bench.Add(oldActive);
        statusMessage = $"{target.data.cardName} is now Active.";
        RefreshUI();
    }

    private void EndPlayerTurn()
    {
        if (gameOver || choosingStartingActive || botTurnRunning) return;
        StartCoroutine(BeginBotTurnAfterDelay());
    }

    private IEnumerator BotFirstTurn()
    {
        botTurnRunning = true;
        RefreshUI();
        yield return new WaitForSeconds(botActionDelay);
        if (gameOver) yield break;

        StartTurn(bot);
        DrawCards(bot, 1);
        BotPlayCards();
        if (!gameOver) BotTakeAction();

        if (!gameOver)
        {
            StartTurn(player);
            DrawCards(player, 1);
            statusMessage += " Your turn has started: 1 energy gained and 1 card drawn.";
        }
        botTurnRunning = false;
        RefreshUI();
    }

    private IEnumerator BeginBotTurnAfterDelay()
    {
        botTurnRunning = true;
        RefreshUI();
        yield return new WaitForSeconds(botActionDelay);
        if (gameOver) yield break;

        StartTurn(bot);
        DrawCards(bot, 1);
        BotPlayCards();
        if (!gameOver) BotTakeAction();
        if (!gameOver)
        {
            StartTurn(player);
            DrawCards(player, 1);
            statusMessage += " Your turn: energy gained and 1 card drawn.";
        }
        botTurnRunning = false;
        RefreshUI();
    }

    private void StartTurn(PlayerState state)
    {
        state.energy += energyPerTurn;
        state.hasAttackedThisTurn = false;
    }

    private void BotPlayCards()
    {
        while (bot.hand.Count > 0 && bot.bench.Count < benchLimit)
        {
            CardInstance card = bot.hand[0];
            bot.hand.RemoveAt(0);
            bot.bench.Add(card);
        }

        if (bot.active == null && bot.bench.Count > 0)
        {
            bot.active = bot.bench[0];
            bot.bench.RemoveAt(0);
        }
    }

    private void BotTakeAction()
    {
        if (bot.active == null)
        {
            EndGame(player);
            return;
        }

        // Simple AI: attack if legal; otherwise end turn.
        if (!bot.hasAttackedThisTurn && bot.energy >= bot.active.data.attackCost && player.active != null)
        {
            bot.energy -= bot.active.data.attackCost;
            bot.hasAttackedThisTurn = true;
            ResolveAttack(bot, player);
        }

        // If the bot has no active card after a knockout, promote a bench card.
        if (!gameOver && bot.active == null)
            PromoteFromBenchOrLose(bot);
    }

    private void EndGame(PlayerState winner)
    {
        gameOver = true;
        botTurnRunning = false;
        statusMessage = winner == player
            ? "VICTORY! You reached 3 points."
            : "DEFEAT! The bot won the battle.";
        if (restartButton != null) restartButton.gameObject.SetActive(true);
    }
}
