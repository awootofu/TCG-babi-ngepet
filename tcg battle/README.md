# TCG Battle – "Legenda Babi Ngepet" Prototype

A playable Unity 2D trading-card-game battle prototype: **Babi Ngepet (Dark)** vs **Warga Desa (Fire)**,
played on a 1920×1080 landscape board with the artist's original card artwork.

The current balance pass uses the supplied KISATARA card faces and the official purple/gold
`Back Side` card back for **both** deck piles, the bot's hidden hand, and every draw ghost.

## How to play

1. **Open** the Unity project folder `tcg battle/` in Unity **2022.3.x** (the project that opens is
   the one under `C:\TCG Battle\tcg battle\`).
2. From the menu pick **TCG Battle → Build Battle Scene**. This creates/refreshes:
   - `Assets/Resources/CardDatabase.asset` (card stats + deck lists)
   - `Assets/Scenes/BattleScene.unity` (the playable scene; also set in Build Settings)
3. Open `Assets/Scenes/BattleScene.unity` and press **Play**.
   (The UI itself is built at runtime from `BattleStarter` – the scene only needs the camera + BattleRoot.)

### Controls (in Indonesian, matching the card art language)

| UI element | Action |
|---|---|
| Click a hand card during setup | send that battle card to your **active slot** |
| Click a hand card on your turn | play it (battle card → bench, Kucing Liar/Punarma → instant effect) |
| **SERANG** | attack with your active card (costs its attack energy) |
| **MUNDUR ·N** | retreat: your active goes to the bench, first bench battle card steps up (costs N energy) |
| Click a specific own bench card | targeted retreat to that card |
| **AKHIRI GILIRAN** | end your turn |

### Rules implemented (from the design brief)

- 20-card decks per side; opening hand of 5 (with a "mulligan" if a hand has no battle card).
- Coin flip decides who starts (shown before the first turn).
- Each turn: draw 1 card + gain 1 energy.
- Maximum 10 energy; energy gained beyond 10 is lost.
- Maximum 10 cards in hand; draws beyond 10 are discarded.
- Attacks cost the card's attack energy; retreats cost its retreat energy.
- **Fire deals 2× damage to Dark; Dark deals 1× to Fire.**
- 1 active slot + 3 bench slots per player.
- Knocking out a card scores **1 point**; **3 points wins**.
- A KO'd active card is replaced automatically; if no card is available to fill the slot, that player **loses**.
- Invalid actions are refused with an explanatory message.

### Card stats (KISATARA printed values)

| Card | Element | HP | Attack | Cost | Effect |
|---|---|---:|---|---:|---|
| **Warga Desa** | Fire | 150 | Serang Balik · 30 | 1 | — |
| **Pasukan Obor** | Fire | 200 | Obor Api · 45 | 1 | Gain 1 energy after attacking |
| **Amukan Warga** | Fire | 220 | Balas Dendam · 75 | 1 | Passive: heal 20 HP at the start of its owner's turn. Attack also deals 20 to the opponent's bench Mbok Sumi |
| **Pak Karta** | Dark | 150 | Ingin Kaya · 20 | 1 | Draw 1 card after attacking |
| **Babi Ngepet** | Dark | 200 | Mencuri Kekayaan · 80 | 1 | Flip a coin; on heads steal 1 energy from the opponent |
| **Siluman Babi** | Dark | 300 | Hilang Kemanusiaan · 120 | 1 | Attack also deals 30 damage to itself |

### Support-card effects (as implemented)

| Card | Effect as implemented |
|---|---|
| **Mbok Sumi** | 50 HP while on the bench. While she is on your bench, **Pak Karta can evolve into Babi Ngepet**. She can be damaged by Kucing Liar and Amukan Warga's splash |
| **Tumbal Anak** | While in your **hand**, Babi Ngepet's **next attack is guaranteed to steal 1 energy**; one copy is consumed. It is never played to the bench |
| **Kucing Liar** | When played: 40 damage to the opponent's bench **Mbok Sumi** (she has 50 HP, so one hit leaves her at 10; a second hit KOs her). Only playable while an enemy Mbok Sumi is benched |
| **Punarma Jumat Legi** | When played: the next Warga attack gains **+50 damage**. It persists until that attack happens. Only playable while **Babi Ngepet** is the opponent's active card |

### Evolution lines

- **Warga:** Warga Desa → Pasukan Obor → Amukan Warga. Damage already taken carries over.
- **Babi:** Pak Karta → Babi Ngepet → Siluman Babi. Damage carries over and each step consumes the
  matching card from hand. **Pak Karta's first step requires Mbok Sumi on the bench**; the
  Babi Ngepet → Siluman Babi step is a normal evolution.

## Editing card stats / decks

All numbers live in **two places that stay in sync**:

- `Assets/Scripts/Data/CardDatabase.cs` – source of truth with the stats transcribed from the
  artwork (HP, ATK, attack name, energy costs, deck copies per side). Edit and re-run
  **TCG Battle → Refresh Card Database Asset** if the asset exists.
- `Assets/Resources/CardDatabase.asset` – the serialized asset the scene references
  (editable in the Inspector; delete it and press *Build Battle Scene* to regenerate with code defaults).

## Replacing artwork

- Card art: drop 1260×1760 PNGs named exactly like the current files into
  `Assets/Resources/Cards/` (e.g. `Babi Ngepet.png`). They are loaded via `Resources.Load` at runtime.
- Official card back: `Assets/Resources/Cards/Back Side.png`. `CardBackVisual` is the single shared
  factory used by both `DeckPileView` and `BotHandFan`, so the player deck, bot deck, bot hand and
  draw ghosts always show the exact same supplied design.
- Element icons: `Assets/Resources/Elements/Dark Element.png`, `Fire Element.png`.
- The supplied SVGs are in `C:\TCG Battle\Assets\cards and back\`; the project PNGs were rendered
  from them at 2× with headless Chrome. The original SVGs are untouched.

## Project layout

```
Assets/
  Scripts/
    BattleStarter.cs        – scene root: creates BattleManager + UI + bot, restarts matches
    Data/
      CardDatabase.cs       – ScriptableObject: card defs + deck lists (edit stats here)
    Gameplay/
      GameState.cs          – CardInstance / PlayerState / Side / Phase models
      BattleRules.cs        – pure rules: damage matrix, limits, constants
      BattleManager.cs      – all state transitions; pure logic, event-driven
      BotController.cs      – coroutine bot that only uses BattleManager hooks (never cheats)
    UI/
      BattleUI.cs           – builds the whole landscape canvas in code; animations & event wiring
      CardView.cs           – reusable card visual (art + live HP bar/element/cost/selection)
      CardBackVisual.cs     – shared official KISATARA card back
      DeckPileView.cs       – mirrored deck piles with live counts
      BotHandFan.cs         – opaque face-down opponent hand fan
      HandController.cs     – fanned hand layout + hand card clicks
      UiSpriteFactory.cs    – procedural rounded-rect/circle/shadow sprites
      UiFx.cs, ArtLibrary.cs, TmpFontService.cs – small helpers
    Editor/
      BattleSceneBuilder.cs – creates the scene and refreshes CardDatabase.asset
      BalanceTestRunner.cs  – 182 targeted EditMode checks (TCG Battle menu)
  Resources/Cards/*.png     – card faces rendered from the artist SVGs + Back Side.png
  Resources/Elements/*.png  – element icons
  Scenes/BattleScene.unity  – built by the menu item
```

## TextMeshPro

TMP package 3.0.7 is in the manifest. If TMP Essential Resources were never imported
(`Assets/TextMesh Pro/` missing), the game automatically generates a runtime fallback font from
Unity's built-in `LegacyRuntime.ttf`, so text always renders. Recommended:
**Window → TextMeshPro → Import TMP Essential Resources**.

## Automated validation (KISATARA balance update)

Run **TCG Battle → Run Balance Tests** in the Unity editor. The runner writes a plain-text report to
`C:\TCG Battle\.stage1-tests\balance_results.txt`.

Latest run: **182 checks passed, 0 failed**. Coverage:

- Every updated HP, attack, cost, attack name and effect text in `CardDatabase.asset`.
- Both 20-card deck lists and the required-card validation.
- Every card face PNG and the official `Back Side.png` (1260×1760) load from Resources.
- Mbok Sumi's 50 HP support model and KO behavior.
- Warga `WD → PO → AW` evolution and damage carry-over.
- Babi `PK → BN → SB` evolution; Mbok Sumi required for `PK → BN`, normal `BN → SB`.
- Damage multipliers, energy payment, once-per-turn attack restriction.
- Babi Ngepet coin flip + Tumbal Anak guarantee.
- Pasukan Obor energy gain and cap.
- Pak Karta draw, including empty/full-hand behavior.
- Siluman Babi recoil and self-KO scoring.
- Amukan Warga heal and Mbok Sumi splash.
- Kucing Liar 40 damage (leaves Mbok Sumi at 10; second hit KOs).
- Punarma +50 condition and persistence until the next Warga attack.
- Shared card-back factory: both decks, bot hand and draw ghosts use `ArtLibrary.GetCardBack()`.

## Known limitations / interpretations

- End-of-damage animations are cosmetic and may overlap the next action; game state is always correct.
- The decorative turn banner can briefly overlap the game-over overlay in the same frame (cosmetic).
- Opening hand is auto-mulliganned only until at least one battle card appears.
- **Punarma persists until the next Warga attack** because the printed text has no end-of-turn clause.
- **Mbok Sumi gates only `PK → BN`.** The `BN → SB` step is a normal evolution because the active card is
  no longer Pak Karta. If Mbok Sumi should gate both steps, move the bench check to both forms.
- **Amukan Warga heals at the start of its owner's turn** (interpretation of "setiap giliran").
- **Amukan Warga's 20 splash and Kucing Liar target the first Mbok Sumi** if more than one is benched.
- **Siluman Babi's 30 recoil resolves after the defender's KO/promotion.** A self-KO awards the opponent
  1 point; if the attack already ended the match, the match ends immediately.
- **Knocking out a support card scores no point** (existing interpretation).
- The Mbok Sumi artwork says "berubah wujud menjadi apa pun"; the implemented rule is the clarified
  evolution chain `PK → BN → SB`, not a free-form transformation.
- The Unity MCP package (`com.coplaydev.unity-mcp`) was added to the manifest for validation;
  you can remove it from `Packages/manifest.json` if you don't want it.
