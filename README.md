**English** · [Español](README.es.md)

# 🏎️ Apex Rivals — `archive/blocking`

> **Archived branch.** The playable prototype of Apex Rivals delivered for the first partial exam, kept with its full development history.

This was the main working branch while the first playable version was built and blocked out, between **1 and 14 September 2026**, through **pull requests 55 to 72**. It is no longer developed: the finished game is on [`main`](../../tree/main).

## What the prototype contains

| Area | Prototype |
| --- | --- |
| Circuit | One greyboxed (blocking) circuit with ordered checkpoints, start/finish line and obstacles |
| Race | Countdown, one lap, live position, finish and results |
| AI | One experimental rival following a racing line |
| Cars | Vanguard and Striker |
| Garage | Car selection with preview, Engine and Handling upgrades, currency |
| Progression | Rewards by finishing position |
| Save | Versioned JSON player profile with backup |
| UI | Main Menu, Settings, Garage, HUD, Pause and Results |
| Input | Keyboard and Xbox-compatible gamepad |

## History

| Pull requests | Milestone |
| --- | --- |
| 55 – 57 | Project structure, runtime core and wheel vehicle |
| 58 – 60 | Vehicle selection, progression, upgrades, save system, scene flow, main menu and settings |
| 61 – 63 | AI driver, race progression and application bootstrap |
| 64 – 67 | Production cars and main menu / garage experience |
| 69 – 70 | Race scene presentation and bootstrap integration |
| 71 – 72 | Test coverage and HUD fixes |

The branch also keeps the first code-only skeleton of the project (1 – 5 September), merged into this history.

## Open it

Open the `ApexRivals` folder with Unity **6000.3.5f2**, then play `Assets/ApexRivals/Scenes/Bootstrap.unity` (or open `Race.unity` to look at the greybox circuit).

## Branches

| Branch | Purpose |
| --- | --- |
| [`main`](../../tree/main) | Current version (1.0) |
| [`release/1.0`](../../tree/release/1.0) | Version 1.0 with full history |
| `archive/blocking` | This branch: first-partial prototype |

---

**Apex Rivals** · Binary Suns Studio · Emiliano Bazán-Zapata · Motores de Desarrollo I · UTN FRBA
