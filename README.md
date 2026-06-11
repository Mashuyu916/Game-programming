# Escape from the Thunder Prison

A 2D endless-runner game built with Unity.

Play as the last samurai carrying the stolen **Skyfire Core** through a
collapsing sky realm. The Thunder Lord has ordered the storm itself to hunt
you down. Run, jump, collect ancient powers, and survive long enough to reach
the legendary Sky Gate.

## Story

The Thunder Lord stole the Skyfire Core and sealed the realm beneath an
endless storm. A lone samurai broke into the Thunder Prison and reclaimed the
living flame, but escape is now the only path forward.

Every bolt in the sky is closing in. The Core grows stronger as the samurai
survives, revealing short story moments throughout the run. Reach the Sky Gate
and carry its fire beyond the storm.

## Features

- Procedurally recycled endless-runner terrain
- Increasing speed and difficulty over time
- Lightning chase hazard
- Health, damage, revive, and game-over systems
- Persistent coin wallet between runs
- Pre-run shop with starting boosts
- Fruit pickups that grant healing, double jump, or cloud flight
- Timed ability indicators and pickup feedback
- Player idle, run, attack, and movement animation support
- Non-blocking story milestones during gameplay
- Time, score, best score, and story-based run results
- Title screen and in-game instructions

## Controls

| Action | Input |
| --- | --- |
| Start the game or run | `Enter`, `Space`, or `K` |
| Jump | `Space` or `K` |
| Double jump | Press jump again while airborne when the ability is active |
| Move during cloud flight | `W` / `S` |
| Close the shop or instructions | `Esc` |

The character runs automatically during the endless-runner mode.

## Pickups

| Pickup | Effect |
| --- | --- |
| Green fruit | Restores 30 HP |
| Orange fruit | Enables double jump for 14 seconds |
| Red fruit | Enables cloud flight for 10 seconds |
| Coin | Adds currency to the persistent wallet |

## Shop

Coins collected during runs can be spent before the next attempt.

| Boost | Cost | Effect |
| --- | ---: | --- |
| Shield | 15 coins | Grants temporary protection at the start |
| Double Jump | 20 coins | Starts the run with double jump |
| Cloud Flight | 35 coins | Starts the run with controllable flight |
| Revive | 45 coins | Prevents one failed run |

Purchased boosts are consumed when the next run begins.

## Story Progression

Story messages appear without pausing gameplay:

- **30 seconds:** the Thunder Lord discovers the escape
- **60 seconds:** the Skyfire Core begins to awaken
- **90 seconds:** the Sky Gate draws near
- **120 seconds:** the Thunder Prison is broken

The result screen changes according to the player's progress through the
escape.

## Requirements

- Unity `2022.3.62f3c1` or another compatible Unity 2022.3 LTS release
- A desktop platform supported by the Unity Editor

## Run the Project

1. Clone the repository:

   ```bash
   git clone https://github.com/Mashuyu916/Game-programming.git
   ```

2. Open Unity Hub.
3. Select **Add project from disk**.
4. Choose the cloned repository folder.
5. Open `Assets/TitleScene.unity`.
6. Press the **Play** button in the Unity Editor.

The configured build scenes are:

1. `Assets/TitleScene.unity`
2. `Assets/1.unity`

## Project Structure

```text
Assets/
|-- Animations/                 Player animation clips and controller
|-- Art/                        Character, environment, and tileset art
|-- Editor/                     Unity editor setup utilities
|-- Resources/                  Runtime-loaded UI and gameplay images
|-- Scenes/                     Additional Unity scenes
|-- Scripts/
|   |-- TitleScreenUI.cs        Title screen and instructions
|   `-- unity-2d-equipment/
|       |-- EndlessRunner2D.cs  Main runner, UI, shop, and story flow
|       |-- PlayerMovement2D.cs Player movement and double jump
|       |-- PlayerFlight2D.cs   Timed cloud-flight ability
|       |-- RunnerCoinPickup2D.cs
|       |-- RunnerFruitPickup2D.cs
|       `-- RunnerLightningChase2D.cs
|-- TitleScene.unity
`-- 1.unity
```

## Asset Credits

- **Samurai 2D Pixel Art** by Mattz Art:
  https://xzany.itch.io/samurai-2d-pixel-art
- **Brackeys' Platformer Bundle**, published by Brackeys; world tileset by
  RottingPixels:
  https://brackeysgames.itch.io/brackeys-platformer-bundle

Detailed asset usage and license notes are available in
[REFERENCES.md](REFERENCES.md).

## Development Notes

Unity-generated directories such as `Library/`, `Temp/`, `Logs/`, and build
output are excluded from version control. Do not commit downloaded source
archives or generated editor caches.
