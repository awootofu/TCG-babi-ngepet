# TCG Battle – "Legenda Babi Ngepet" Prototype

A playable Unity 2D trading-card-game battle prototype: **Babi Ngepet (Dark)** vs **Warga Desa (Fire)**,
played on a portrait phone-style layout with the artist's original card artwork.

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
| **MUNDUR ·N** | retreat: your active goes to the bench, first bench battle card steps up (costs N energy; **GRATIS** while Mbok Sumi is on your bench) |
| Click a specific own bench card | targeted retreat to that card |
| **AKHIRI GILIRAN** | end your turn |

### Rules implemented (from the design brief)

- 20-card decks per side; opening hand of 5 (with a "mulligan" if a hand has no battle card).
- Coin flip decides who starts (shown before the first turn).
- Each turn: draw 1 card + gain 1 energy.
- Attacks cost the card's attack energy; retreats cost its retreat energy.
- **Fire deals 2× damage to Dark; Dark deals 1× to Fire.**
- 1 active slot + 3 bench slots per player.
- Knocking out a card scores **1 point**; **3 points wins**.
- A KO'd active card is replaced automatically; if no card is available to fill the slot, that player **loses**.
- Invalid actions are refused with an explanatory message.

### Support-card effects (as printed on the cards)

| Card | Effect as implemented |
|---|---|
| **Mbok Sumi** (PASIF) | While she is on your bench, your retreat is free |
| **Tumbal Anak** (PASIF) | While in your **hand**, the opposing Fire attack on **Babi Ngepet** is nullified (0 damage) and **one copy is consumed**. Clicking it shows a hint; it is never played to the bench |
| **Kucing Liar** | When played: 40 damage to the opponent's bench **Mbok Sumi** (she leaves play, no score). Only playable while an enemy Mbok Sumi is benched |
| **Punarma Jumat Legi** | When played: your **next attack this turn gains +50**. Only playable while **Babi Ngepet** is the opponents active card |

## Editing card stats / decks

All numbers live in **two places that stay in sync**:

- `Assets/Scripts/Data/CardDatabase.cs` – source of truth with the stats transcribed from the
  artwork (HP, ATK, attack name, energy costs, deck copies per side). Edit and re-run
  **TCG Battle → Refresh Card Database Asset** if the asset exists.
- `Assets/Resources/CardDatabase.asset` – the serialized asset the scene references
  (editable in the Inspector; delete it and press *Build Battle Scene* to regenerate with code defaults).

## Replacing artwork

- Card art: drop 630×880 (or any aspect) PNGs named exactly like the current files into
  `Assets/Resources/Cards/` (e.g. `Babi Ngepet.png`). They are loaded via `Resources.Load` at runtime.
- Element icons: `Assets/Resources/Elements/Dark Element.png`, `Fire Element.png`.
- The original artist SVGs in the *other* project (`C:\TCG Battle\Assets\Arts\*.svg`) are untouched.
- The PNGs were rendered from those SVGs at 2× with a headless browser; the conversion script can be
  recreated by asking for `convert-svg.ps1` (see report).

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
      BattleUI.cs           – builds the whole portrait canvas in code; animations & event wiring
      CardView.cs           – reusable card visual (art + live HP bar/element/cost/selection)
      HandController.cs     – fanned hand layout + hand card clicks
      UiSpriteFactory.cs    – procedural rounded-rect/circle/shadow sprites
      UiFx.cs, ArtLibrary.cs, TmpFontService.cs – small helpers
  Resources/Cards/*.png     – card faces rendered from the artist SVGs
  Resources/Elements/*.png  – element icons
  Scenes/BattleScene.unity  – built by the menu item
```

## TextMeshPro

TMP package 3.0.7 is in the manifest. If TMP Essential Resources were never imported
(`Assets/TextMesh Pro/` missing), the game automatically generates a runtime fallback font from
Unity's built-in `LegacyRuntime.ttf`, so text always renders. Recommended:
**Window → TextMeshPro → Import TMP Essential Resources**.

## Automated playtest (validation run)

A headless driver (agent tooling, not part of the game build) installs a periodic
`EditorApplication` callback that plays a full match through the public `BattleManager`
API while a tick-logger records every game event. Two full matches were run to
**GameOver**:

| Run | Coin | Result | Notable event-trace evidence |
|---|---|---|---|
| 1 | Player first | **KAMU MENANG 3–0** | 15 attacks, Tumbal Anak nullified a Fire attack on Babi Ngepet (the current rule also consumes one copy), bench + Mbok Sumi free-retreat used |
| 2 | Bot first | **BOT MENANG 3–1** | Bot benched Warga Desa/Pasukan Obor/Mbok Sumi, Warga Desa dealt 60 (2× super-effective) KOs, damage chain 150→90→30→KO, auto-promotion after each KO, correct `Butuh 1 energi untuk mundur.` denial, score pips hit 3 |

- Editor console: **0 errors** during both runs.
- Screenshots captured during play: setup (coin-flip mid-animation), first turn,
  one per attack, game over (`%LOCALAPPDATA%\Temp\opencode\unity-shots\d3-*.png`).
- The bot coroutine uses `WaitForSecondsRealtime` for pacing so its turns play at the
  same real-time rate even when the editor window is unfocused (scaled delta-time can
  crawl in that state; gameplay logic itself is not time-based).

## Known limitations

- End-of-damage animations are cosmetic and may overlap the next action; game state is always correct.
- The decorative turn banner can briefly overlap the game-over overlay in the same frame (cosmetic).
- Opening hand is auto-mulliganned only until at least one battle card appears.
- Punarma buff expires at the end of the turn if unused (interpretation).
- Knocking out a support card (only possible via Kucing Liar) scores no point (interpretation).
- The Unity MCP package (`com.coplaydev.unity-mcp`) was added to the manifest for validation;
  you can remove it from `Packages/manifest.json` if you don't want it.
