# Bank Heist 3D — Unity project

A small, first-person 3D bank-heist game with intentionally simple models and a basic English interface. The project uses the Built-in Render Pipeline, C#, uGUI and Unity's NavMesh. It does not require paid assets or a plugin.

## Open and play

1. Use **Unity 6000.5.7f1**, the editor version installed on this computer.
2. In Unity Hub choose **Add → Add project from disk** and select this folder (the folder containing `Assets`, `Packages` and `ProjectSettings`). You can also double-click `Open in Unity.cmd` on this computer.
3. Open `Assets/Scenes/MainMenu.unity`.
4. Press the Unity **Play** button. Select Robber, Police / Guard, or Bystander and click **Start Game**.
5. Click the Game view to control the player. Press **Tab** to release the mouse for the squad buttons. **Esc** pauses the game.

The scenes and Canvas controls are already saved as Unity assets. They are not a browser game or screenshots. You can inspect and edit their GameObjects in the Hierarchy and their properties in the Inspector.

## Controls

| Input | Action |
| --- | --- |
| WASD / arrow keys | Move |
| Mouse | Look and aim |
| Left click | Fire |
| R | Reload (12-round magazine, unlimited spare ammo) |
| G | Throw a grenade (3 grenades, 5-second player cooldown) |
| E | Use a door, collect gold, help a customer, or escape |
| 1 | Squad follows the commander |
| 2 | Squad attacks enemies |
| 3 | Squad throws grenades at nearby enemies |
| 4 | Squad retreats to the entrance |
| 5 | Squad holds its current position |
| Tab | Toggle cursor to click the UI |
| Left Shift | Run |
| Esc | Pause / resume |

The **Select teammate** button cycles between the whole squad and individual surviving teammates. Customers can follow, retreat and hold; they cannot shoot or throw grenades.

## Objectives

- **Robber:** enter the bank, reach the vault, and collect at least $2,000. Return to the lobby and use the **REAR EXIT** on the right side near the vault. Cross the back alley and press E at the open manhole. Walk along the sewer tunnel and press E at the green **ESCAPE** marker. The crew then wins.
- **Police / Guard:** lead two officers and stop all three robbers. Their NPC commander tries to reach the vault, take gold and escape through the rear door and sewers.
- **Bystander:** enter the bank, press E near two customers, and lead them back to the street. Press E at the **CIVILIAN SAFE ZONE** with the customers nearby.

You lose when the player dies, the mission timer reaches ten minutes, or the enemy crew completes its escape. The bystander also loses if fewer than two customers can still be saved. The UI reports hits, deaths, commander changes, and team victory or defeat.

## Scenes

| Scene | Purpose |
| --- | --- |
| MainMenu | Working role selector and controls panel |
| Street | Starting area, bank entrance and civilian safe zone |
| Bank | Lobby, customer queue, tellers, vault door and rear exit |
| Vault | Gold tables and safe deposit cabinets |
| BackAlley | Rear bank entrance, dumpsters and sewer manhole |
| Sewers | Final escape tunnel |

`HeistSession` is kept with `DontDestroyOnLoad`. Player and NPC health, gold, ammo, grenades, squad orders, rescue status and deaths persist across scene loads. Actors are recreated from those saved states. This is **in-session** persistence; closing the game starts a fresh mission.

## Main scripts

| Script | Responsibility |
| --- | --- |
| HeistSession.cs | Persistent state, objectives, transitions, victory / defeat and offscreen NPC travel |
| SceneController.cs | Loads baked navigation, instantiates actors, saves their positions |
| PlayerController.cs | First-person movement, shooting, interaction and grenades |
| Actor.cs | NPC commander / follower logic, combat, formation and hit reactions |
| QueueController.cs | Actual FIFO customer queue and teller service |
| Interactable.cs | Doors, loot, customer evacuation and sewer escape |
| GameHUD.cs | Health, money, ammo, squad orders, pause and result screens |
| MenuController.cs | Role selection and start buttons |
| Grenade.cs | Grenade physics, blast effects and generated game sounds |
| HeistProjectBuilder.cs | Editor utility that builds the editable demonstration assets |
| HeistPlayTests.cs | Integration checks executed in Unity Play Mode |

NPCs use baked NavMesh data, local avoidance, different avoidance priorities and spaced formation targets. If an NPC commander dies, a surviving team member takes command. Customer models lie down when the alarm starts, and stand up when the bystander helps them.

## Editing

- Level objects are under **Environment - editable level geometry** in each scene.
- HUD and menu objects are under **Canvas - editable game interface**. Buttons have real persistent `On Click` callbacks.
- Player and NPC prefabs are under `Assets/BankHeist/Prefabs`.
- Materials and original small textures are under `Assets/BankHeist/Materials` and `Textures`.
- Tweak movement speed, NPC damage and attack range in the prefab Inspector.
- `Scene Controller - spawns and navigation` contains the spawn references and NavMesh asset.
- All six scenes are included in Build Profiles.

**Bank Heist → Rebuild Demo Scenes and Prefabs** recreates the original demo assets and overwrites edits to those generated scenes/prefabs. It is not run automatically when opening the project. Use it only when you intentionally want to reset the demo layout.

## Build

Use **Bank Heist → Build Windows Game**. The executable is placed in `Builds/Windows/BankHeist3D.exe`. Alternatively use Unity's Build Profiles and select Windows with the six included scenes.

All models, textures and sound effects here are simple original project assets. Reference screenshots were used to guide the first-person viewpoint, bank setting and basic HUD, not copied into the game. This is a small student-style prototype, not a recreation of PAYDAY's commercial graphics or assets.
