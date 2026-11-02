# Bank Heist Game

A game project exploring NPC behavior, squad commands and role-based objectives in a bank-heist scenario. Play as a robber, a police officer or a bystander, with a different objective for each role.

**[Watch the gameplay demonstration on YouTube](https://youtu.be/P8hvw4A--zM)**

The repository contains a **Unity C# project** and a separate **HTML/CSS/JavaScript browser prototype**. They are distinct implementations of the same game concept.

![Unity bank scene](unity/Preview-Bank.png)

## Gameplay and technical features

- NPC commanders and followers, squad orders and leadership changes.
- Role-specific objectives: steal and escape, stop the robbers or rescue civilians.
- Combat, health, ammunition, grenades and win/loss conditions.
- Customer queues and reactions to an alarm.
- State preserved while moving between scenes during a mission.

The Unity project uses C#, uGUI, the Built-in Render Pipeline and NavMesh navigation. The browser prototype uses plain JavaScript with a separate game engine and interface layer.

## Open the Unity project

1. Clone the repository:

   ```powershell
   git clone https://github.com/AsafZiv1908/bank-heist-game.git
   ```

2. Add the `unity/` folder in Unity Hub.
3. Open it with **Unity 6000.5.7f1**, the version recorded in `ProjectSettings/ProjectVersion.txt`.
4. Open `Assets/Scenes/MainMenu.unity` and press **Play**.

See the [Unity guide](unity/README.md) for controls, objectives, script responsibilities and Windows build instructions. Unity caches, local editor settings and compiled builds are excluded from Git.

## Run the browser prototype

With Python 3 installed, run this command from the repository root:

```powershell
python -m http.server 8000 --directory web
```

Open [localhost:8000](http://localhost:8000). No JavaScript package installation is required. Google Fonts are loaded when internet access is available.

## Source map

| Location | Purpose |
| --- | --- |
| [Unity scripts](unity/Assets/Scripts/) | Player controls, NPC behavior, session state and UI |
| [Unity editor scripts](unity/Assets/Editor/) | Demo scene generation, builds and Play Mode checks |
| [Unity scenes](unity/Assets/Scenes/) | Menu, street, bank, vault, alley and sewers |
| [Browser engine](web/engine.js) | Simulation, movement, combat and objectives |
| [Browser interface](web/app.js) | Rendering, input and UI |
| [Browser tests](tests/browser-game.cjs) | Simulation and local asset checks |

## Verification

Run the existing browser checks with Node.js from the repository root:

```powershell
node tests/browser-game.cjs
```

All **14 checks** passed during portfolio preparation, covering role initialization, scene-state persistence, objectives, combat, NPC behavior, navigation and static asset references. Both browser JavaScript files also passed syntax checks.

The Unity source was preserved without modification. A fresh Unity editor or build test was not performed. Its included QA report describes an earlier test run.

## Version scope

The supplied files included a compiled Windows release labelled V33, but did not include its matching source directory. This repository contains the available earlier Unity source and the browser prototype; it does not claim to reproduce V33. The Unity guide describes the included editable version.
