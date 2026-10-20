# TCG Battle Prototype — Unity Starter

This is a small, code-only Unity prototype based on the supplied requirement sheet. It uses placeholder stats and runtime-generated UI so you can test the battle flow before any final art is ready.

## Included
- Two 20-card decks: Babi Ngepet (Dark) and Warga (Fire)
- Placeholder HP and ATK generated when a new match starts
- Five cards dealt to each side at setup
- Player selects an Active card from the starting hand; the bot selects one automatically
- One Active slot and up to three Bench slots per side
- A random coin flip determines who takes the first turn
- One energy gained at the start of each turn
- One card drawn at the start of each turn
- Attack costs energy; Fire deals 2x damage to Dark; Dark deals normal damage to Fire
- Retreating costs energy and switches the Active card with a Bench card
- Knockouts award one point; first to three points wins
- A simple bot and restart button
- Runtime-created UI: no external assets or packages required

## Setup (project is ready to run)
This folder is already a complete Unity project targeting Unity 2022.3 LTS:
- `Assets/Scripts/BattlePrototype.cs` — all game code
- `Assets/Scenes/BattlePrototype.unity` — a scene with the `BattlePrototype` component already attached

1. Open Unity Hub → Projects → Add → select this folder (it opens with Unity 2022.3).
2. Open `Assets/Scenes/BattlePrototype.unity` if it is not already open.
3. Press Play.

The script builds its own Canvas and UI at runtime and even attaches itself automatically when the scene does not contain it. No Canvas, EventSystem, sprites, or prefabs need to be created manually.

## Controls
- Click a card in your hand to select your starting Active card.
- Click cards in your hand to add them to your Bench (up to three).
- Click a Bench card button to switch it with your Active card (costs energy).
- Attack uses the Active card's ATK and energy cost.
- End Turn passes control to the bot.
- Play Again restarts the match.

## Placeholder balance
- HP is randomly generated from 70–110.
- ATK is randomly generated from 20–40.
- Attack cost: 1 energy.
- Retreat cost: 1 energy.
- Energy starts at 0 and increases by 1 at the start of each turn.

These defaults are easy to change in the BattlePrototype component's Inspector.

## Current prototype assumptions / limitations
- The bot AI is intentionally simple and only attacks when it can.
- The prototype is intended as a code starter and should be play-tested in the target Unity version. It is not yet a polished or production-ready game.
- The requirement says a player immediately loses if they have no Active card. To avoid losing during the moment an Active card is knocked out, the prototype first promotes a Bench card; if no Bench card exists, the match ends. It does not automatically replace a knocked-out Active from hand.
- Cards are placeholder runtime objects. Art can later be assigned by replacing the generated UI with prefabs or SpriteRenderer-based card views while keeping the battle logic.
