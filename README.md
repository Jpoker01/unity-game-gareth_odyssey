# The Gareth Odyssey
 
*A 2D pixel-art platformer bringing you through the historical eras of Crete.*
 
Play as **Gareth Owens**, archaeologist and Cretan script scholar, who recovers lost artefacts in vivid dreams of the island's past to fill his museum in modern Heraklion. This repository delivers **Level 2 — Ancient Gortyna** as a complete, playable level taking place on Crete during its Roman era.
 
<p align="center">
  ### <a href="YOUR_GAME_LINK">▶️ Play it in your browser</a>
</p>

<div align="center">
  <img src="Assets/gareth_anim1.gif" alt="beginning_of_game">
</div>  

---
 
## Build Overview
 
This build focuses on one finished level rather than trying to implement the full game base on the concept. **Level 2** is with finished art, five enemies, checkpoints, a working HUD, all four difficulty settings, and a full intro to completion loop. Levels 1, 3, and 4 remain at the design stage.
 
> **Your goal:** travel through the ruins of Gortyna - from open olive groves into dense marble ruins — and recover the **Law Code of Gortyn** before your lives run out.
 
---
 
## Gameplay
 
- **Avoidance first, non-violent.** No weapons, no enemy health bars. Get through encounters with timing, watching, and smart routing.
- **Stomp option.** Land directly on an enemy to defeat it with a small upward bounce — any *other* contact knocks Gareth back and costs one life, so a mistimed stomp is just a normal hit.
- **Checkpoints (stelae).** Walk past a stele to light it and save your spot. Fall into a pit and you respawn there, minus one life. Run out of lives and the level restarts from the beginning.
- **Pickups.** Traditional Cretan foods scattered through the level act as health/score items.
- **Historical framing.** An intro panel sets the era's context; a completion panel names and describes the recovered artefact.

<p align="center">
  <img src="Assets/gareth_anim2.gif" alt="beginning_of_game">
</p>
*Hopping across floating ruins, slipping past a patrolling legionarius.*
 
<p align="center">
  <img src="Assets/gareth_anim3.gif" alt="beginning_of_game">
</p>
*A stele lights up as Gareth passes — respawn here after a fall, minus one life.*
 
---
 
## Controls
 
| Action | Input |
|---|---|
| Move | `A` / `D` or `←` / `→` |
| Jump | `Spacebar` |
| Interact (doors, plaques, switches) | `E` or Left-click |
| Tool (brush / trowel / magnifying glass) | Right-click |
 
---
 
## Enemies — Level 2
 
Five hand-tuned state machines that share only the contact/stomp rule:
 
- **Legionarius** — patrols a fixed route; raises an alert if you cross its band.
- **Sagittarius** — perched archer firing on a telegraphed timer; turns to face you (but never moves toward you).
- **Agrios Xoiros** — wild boar that charges in a straight line after a tell.
- **Macrovipera** — viper that rears and strikes, briefly extending its reach.
- **Aquila** — rare optional war eagle that swoops once along a fixed path, preceded by a shadow.
---
 
## Difficulty
 
| Mode | Lives | Notes |
|---|---|---|
| Easy | ∞ | Explore and read the history; you can't fail. |
| Medium | 5 | Balanced. |
| Hard | 3 | Demanding; enemy timings also tighten. |
| Impossible | 1 | One hit loses the artefact. |
 
Higher settings shorten Sagittarius's draw time and Agrios Xoiros's alert delay, so harder modes *feel* faster rather than just stricter.
 
![A rebel camp tucked into the cliffs](Assets/gareth2.png)
 
*A rebel camp tucked into the cliffs — explorable detail between the platforming beats.*
 
![Animated camp scene](Assets/gareth1.png)
 
*The same camp in motion: campfire, drifting cloth, in-engine sprite animation.*
 
---
 
## Tech
 
- **Engine:** Unity 6.4 (6000.4.2f1), URP set up for 2D, new Input System.
- **Art:** Licensed pixel-art packs, with two sprites (arrow, eagle shadow) generated procedurally in code.
- **Animation:** Lightweight code-driven sprite flipbooks (no Animator graphs).
- **Audio:** Era-appropriate score — Roman military brass and strings for Gortyna.
- **Version control:** Plastic SCM (Unity Version Control).
- **Platform:** PC (Windows / macOS), fully offline.
---
 
*Vertical-slice build. A three-person student project; Level 2 is the demo scene.*
