# CyberPickle HDRP — Project Memory for Claude Code

Auto-loaded every session. Keep this under 200 lines (Anthropic guidance).
For deep references, link to the external docs — don't inline them.

---

## Project basics

- **Genre**: auto-shooter survivors-like (Vampire Survivors / Brotato lineage)
- **Aesthetic**: cyberpunk; protagonist is **Pik** (cybernetic pickle)
- **Engine**: Unity 6.4 + HDRP 17.4.0
- **Architecture**: hybrid MonoBehaviour + DOTS (Entities 6.4.0, Burst, Physics)
- **Differentiator**: every run sounds different — the build is literally the soundtrack (procedural music driven by stats + element + weapon level)

## Reference documents — READ THESE before touching related systems

These live in `OneDrive/Documents/6. Game/LLM Knowledge Base/`:

| Document | Touch this if working on… |
|---|---|
| `GDD_V0.6.txt` | Game design (UNCHANGED sections only — read together with `GDD_V0.7_delta.md` for current truth) |
| `GDD_V0.7_delta.md` | **Current canonical delta** — what changed since V0.6: amulets removed, Choice Tokens removed, 10-min runs, dual-axis weapons, Stage Bosses, Completion Tiers |
| `economy_design_v1.md` | Currencies (NC + CC), sinks, Mining Rig, run economy, boss-kill rewards — **read before any currency or shop code** |
| `progression_design_v1.md` | Career L60 cap, 300-node skill tree, Completion Tiers, achievements, mastery challenges — **read before any XP or skill-tree code** |
| `weapon_rarity_v1.md` | Dual-axis weapon model (Level + Rarity), rarity rolls, Luck modulation, in-level interactables, music RTPC mapping — **read before any weapon, card, or rarity code** |
| `procedural_music_reference.md` | Music theory, modes, BPM math, Wwise hierarchy, Ableton pipeline — the composer-side reference |
| `modifier_engine_design.md` | Stats, modifiers, source taxonomy, persistence, analytics — **the architecture lock for everything stat-driven** |

**Reading priority when in doubt:** GDD V0.7 delta → relevant v1 design doc → modifier_engine_design → V0.6 GDD for unchanged sections.

---

## Design pillars (LOCKED — read `GDD_V0.7_delta.md` before deviating)

These three rules are inviolable. If a feature appears to violate any, the feature is wrong.

1. **Currencies and XP unlock CONTENT and SHAPE BUILDS — they never raise the power floor.** No "+X% damage forever" meta-purchases. The richest player has more *content available*, not more *power available*.
2. **Every progression unlock introduces a new pattern (Theory of Fun).** Skill keystones are different games. New weapons play differently. New levels feel mechanically different.
3. **Every choice has opportunity cost — but no negative numbers.** Geography (path-cost in skill tree), point caps (60/300), and keystone exclusivity (one active) create cost. The UI never displays "-X%."

### Locked design quantities (cite these without re-deriving)

- **Run hard cap:** 10:00. Stage Boss spawns at 9:00. Endless mode unlocks post-Story-Complete (zero progression).
- **Equipment slots in run:** 1 starting weapon + 3 drafted = 4 weapons; 1 armor; bandwidth-budgeted implants (typ 3-5); Mining Rig (meta-only).
- **Currencies (persistent):** NC (active grind) + CC (prestige/idle). **No Choice Tokens.** Reroll/Banish/Lock now driven by Luck + skill nodes.
- **Career cap:** L60 per character. 1 skill point per level = 60 points across a 300-node tree (20% allocation). Hard cap blocks marathon-grind.
- **Weapons:** dual-axis — Level (1-5 + Evolved) for pattern complexity + Rarity (Common-Legendary) for stat multiplier (×1.0 to ×4.0). Independent axes. L5-Evolved + Legendary is the brass ring.
- **Amulets:** REMOVED. All amulet roles merged into Implants.
- **Stages:** 10 in 1.0. Each has a unique boss + difficulty tiers T1-T4. Per-level XP unlocks level-specific content.
- **Completion endpoint:** "True Cyber Pickle" — all 7 prior tiers complete (~80-120 hr).

---

## Architectural pillars (LOCKED — do not deviate without a doc update)

### Modifier engine
- **Single source of truth**: `PlayerStats` MonoBehaviour. Every system reads from `PlayerStats.Get(stat)` — never re-computes locally.
- **Sourced attribution**: every `StatModifier` carries a `sourceId` of form `<category>_<id>[_<instance>]`. Categories: `equip`, `skill`, `implant`, `identity`, `breakpoint`, `run`, `boss`, `temp`.
- **AddPercent values are DECIMAL FRACTIONS**: `value: 0.10` = +10%. **Never** `value: 10` for "+10%" (that's +1000%, real bug fixed M7.4).
- **Burst reads via singleton**: ECS systems read `PlayerStatsData` (mirror), never the MonoBehaviour. Bridge writes in LateUpdate when dirty.
- **Main-thread mutation only**: `AddModifier` / `RemoveModifiersFromSource` from main thread. Never from Burst.

### Rarity (centralized — DO NOT define new rarity enums)
- **Single source of truth**: `CyberPickle.Core.Rarity` at `Assets/_CyberPickle/Code/Core/Rarity.cs`. 5 tiers (Common/Uncommon/Rare/Epic/Legendary, byte-typed).
- **Used by**: cards, weapons, implants (M10), Mining Rig parts (M13), cosmetics, boss-drop legendaries, in-run chests.
- **NOT used by**: XP gem tiers (numeric Tier 0-4 in `EnemyXPDropChances`, conceptually aligned but mechanically separate; use `Rarity.XPGemDisplayName()` for cyberpunk-flavored display names). Achievement tiers (Bronze/Silver/Gold/Platinum, separate enum per `progression_design_v1.md` §9).
- **NEVER define a new rarity enum** (`CardRarity`, `WeaponRarity`, `ItemRarity`, etc.). If a system needs 5-tier item quality, use this enum. If it doesn't, it shouldn't have one.
- **Canonical scalars + visuals on the enum** via `RarityExtensions`: `DamageMultiplier()`, `BaseDrawWeight()`, `DisplayName()`, `DisplayColor()`, `IsCelebrated()`. Read from there — never re-derive.
- **Byte values are stable contracts** — Common=0..Legendary=4. DO NOT renumber (persisted in save data, .asset files, ECS chunks).

### Music system
- **Dexterity → tempo** (60-180 BPM)
- **Speed (movement) → master root MIDI** (±1 octave around C3)
- **Element per weapon → mode** (Fire = Phrygian Dominant, Lightning = Phrygian, Ice = Aeolian, etc.); element locked in via power-up coupling at evolution
- **Weapon family → musical role**: Projectile = melody, Explosive = percussion, Beam = harmony
- **Weapon level (1-5 + Evolved) → one hand-composed pattern per level** (4 bars × 32 subdivisions, scale-degree-based — see `procedural_music_reference.md` §22). Pattern data is custom format; composer authors via DAW + MIDI importer.
- **Weapon rarity (Common-Legendary) → distortion / compression depth** (per-slot RTPC `Music_WeaponRarity_SlotN`)
- **Power-ups carry type AND element** — same mechanical effect appears in multiple element flavors in the draft. Element = which musical mode the coupled weapon's pattern plays in.
- **Luck → mega-crit roll** on top of crit + cards-visible-per-draft (3 base, +1 per 50 Luck, cap 6)
- **Grid**: 32 subdivisions per bar; per-pattern grain (8/16/32)
- **Pattern playback**: custom `RhythmicPattern` SO + tick-driven `PatternPlaybackService` posting per-cell Wwise events with Pitch/Velocity RTPCs. NOT runtime MIDI — MIDI is composer-side authoring only via Editor importer.
- **Bus**: `MusicEventBus` is the canonical fan-out for all gameplay-audio events

### Manager<T> singletons
- Scene-bound managers MUST override: `protected override bool PersistAcrossScenes => false;`
- **Persistent (default true)**: ProfileManager, CharacterManager, EquipmentManager, AuthenticationManager, GameManager
- **Scene-bound (override to false)**: RunStateManager, RunStatsTracker, CameraManager, CharacterSelectionManager, CharacterDisplayManager, CharacterUIManager, EquipmentHubManager, MusicConductor, PerWeaponStatsTracker, LevelUpCoordinator
- Without the false override, managers leak across scenes and break references on the next state transition

### Run state machine
- One state, one transition method, one event: `RunStateManager.TransitionTo(phase)` + `OnPhaseChanged`
- Phases: Loading / Running / LevelUpPaused / Paused / GameOver
- `Time.timeScale = 1` only when Running; 0 otherwise
- UI that shows during pause MUST use `Time.unscaledDeltaTime` (or DOTween's `SetUpdate(true)`)

### Music event bus
- Static class `MusicEventBus.Fire(MusicEvent type, object payload = null)`
- Producers: gameplay code (RunStateManager, PlayerHealth, WeaponFiring, EnemyDeathSystem, PlayerXPBridge, LevelUpCoordinator)
- Consumers: MusicConductor, PerWeaponStatsTracker, LevelUpCoordinator, future Wwise integration
- **Burst code cannot Fire directly** — use the queue bridge pattern (see DamageHitReport / DamageReportQueueSingleton / DamageReportDrainSystem)

---

## Project layout

```
Assets/_CyberPickle/
├── Code/
│   ├── Core/             ← Manager<T>, GameManager, ConfigRegistry, services
│   ├── Gameplay/
│   │   ├── Audio/        ← MusicConductor, MusicEventBus, MusicEvent
│   │   ├── Combat/       ← PerWeaponStatsTracker
│   │   ├── Player/       ← PlayerHealth, PlayerStats, PlayerXPBridge, motors/bridges
│   │   ├── Progression/  ← UpgradeCardSO, UpgradePoolSO, LevelUpCoordinator
│   │   ├── RunState/     ← RunStateManager, RunStatsTracker, RunStatePhase
│   │   ├── Stats/        ← StatModifier, BaseStats, PlayerStatType (THE 14)
│   │   └── Weapons/      ← WeaponFiring, WeaponTargeting
│   ├── DOTS/
│   │   ├── Components/   ← All IComponentData (one file per component)
│   │   ├── Systems/      ← Burst ISystem + managed SystemBase
│   │   └── Bridge/       ← MonoBehaviour↔ECS bridges
│   ├── UI/
│   │   ├── HUD/          ← HudController, HealthBarUI, XpBarUI, RunTimerUI, KillCounterUI
│   │   └── Screens/      ← LevelUp, ResultsScreen, MainMenu, EquipmentHub
│   └── Characters/       ← CharacterData, CharacterSelectionManager, etc.
├── Scenes/               ← Boot, MainMenu, EquipmentHub, Game, LevelSelect, PostGame
└── Data/                 ← ScriptableObject assets (cards, weapons, characters, enemies)
```

---

## Code conventions

- **Comments**: explain *why*, not *what*. The "what" should be self-evident from naming.
- **Inspector fields**: use `[Tooltip]` for any field a designer might touch; `[Header]` to group.
- **Cross-system communication**: events / bus, not direct references where avoidable.
- **Manager<T> lifecycle**: override `OnManagerEnabled / Disabled / Destroyed`, not raw `OnEnable / OnDisable / OnDestroy`.
- **Scope of change**: mechanical fixes (foot-guns, regressions) get focused commits separate from feature work.

## Git workflow

- One milestone branch per milestone: `m7.4-hud-and-damage-feedback`, `m8-element-system`, etc.
- One PR per milestone, base = `main`.
- Commits within a milestone are FOCUSED: code-only commits separate from asset/scene commits.
- **NEVER bundle `Packages/manifest.json` or `Packages/packages-lock.json` changes with asset commits.** They get their own focused commit. (Lesson learned the hard way in M7.3.)

---

## Foot-guns and regressions (DO NOT REPEAT)

### Burst-incompatible managed calls
`System.Environment.TickCount`, `DateTime.Now`, etc. — never inside `[BurstCompile]` methods. Crashes player builds inside `__codegen__OnCreate`. Use `Unity.Mathematics.Random.CreateFromIndex(state.GlobalSystemVersion)` for Burst-safe RNG seeds.

### Auto-imported AI packages
**NEVER add `com.unity.ai.assistant` or `com.unity.ai.inference`** to `Packages/manifest.json`. They're auto-imported by Unity prompts but cause cascading EPERM errors when OneDrive holds file locks during package install. Already removed (M7.4 hotfix).

### NEVER edit manifest.json while Unity is running
**Hard rule learned in M7.4:** do not edit `Packages/manifest.json` or `Packages/packages-lock.json` while Unity Editor is open, ESPECIALLY mid-package-install. Unity's package resolver watches the file and re-runs on every save. If a resolve is already in flight (e.g., user just changed something in Package Manager), my edit triggers a second resolve that fights the first, leaving `Library/PackageCache/<pkg>@*` in a half-extracted state. Recovery requires manually removing the package from the Project window in Unity and restarting the editor.

**Process rule:** never edit `manifest.json` / `packages-lock.json` without an explicit "yes, edit the manifest" instruction from the user, even when documentation or troubleshooting threads suggest it as a fix. Suggest the change and let the user make it themselves with Unity closed. The manifest edit is one of those operations that LOOKS safe (just text in JSON) but has runtime consequences in Unity's package resolver that aren't visible to me.

### TMP shader typo on re-import
`Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl` line 25 ships from the TMP package with `texture2D atlas` (lowercase). The valid HLSL type is `Texture2D` (capital T). Breaks 11 DXR shader passes. **If TMP is re-imported, fix the typo again.**

### Cinemachine 2.x → 3.x migration
Unity 6.4's registry no longer ships Cinemachine 2.x. Code uses 3.x: `using Unity.Cinemachine;` (not `using Cinemachine;`), `CinemachineCamera` (not `CinemachineVirtualCamera`). Procedural components (CinemachineFollow, CinemachineRotationComposer, etc.) are now SEPARATE components on the same GameObject, not dropdown menus.

### OneDrive file-lock collisions
Project lives in `OneDrive/Documents/6. Game/CyberPickleHDRP/`. OneDrive aggressively grabs handles → causes EPERM during Unity package installs and `Library/PackageCache/` operations. Pause OneDrive sync during `Library/` rebuilds. Long-term: move project out of OneDrive into something like `C:\Dev\`.

### Scene-bound managers + DontDestroyOnLoad
Pre-fix, `Manager<T>` unconditionally `DontDestroyOnLoad`d every singleton. Scene-bound managers leaked across scene loads, holding references to destroyed GameObjects. **Solution**: `PersistAcrossScenes => false` override on scene-bound managers. Apply this to any new manager that has `[SerializeField]` references to scene-only objects.

### LevelUpCoordinator subscription timing
`OnEnable` runs before player spawns. `FindFirstObjectByType<PlayerXPBridge>()` returned null → coordinator never subscribed → level-up events missed. **Solution**: subscribe to `MusicEventBus.OnEvent` (process-global static, no scene timing concerns), filter by event type. Do not depend on scene-spawned components at OnEnable time.

### Time.timeScale during paused phases
UI animations during `LevelUpPaused` / `GameOver` MUST use `Time.unscaledDeltaTime` or DOTween's `.SetUpdate(true)`. `Time.deltaTime` is 0 when paused; coroutines using `WaitForSeconds` hang.

---

## Current milestone (update as work progresses)

**M7.4 — HUD + damage feedback** (in flight on branch `m7.4-hud-and-damage-feedback`):
- Day 1 ✅ HUD core (HudController, HealthBarUI, XpBarUI, RunTimerUI, KillCounterUI)
- Day 2 ✅ Backend instrumentation (PerWeaponStatsTracker, ProjectileSource, DamageHitReport queue + drain, GetModifierBreakdown)
- Day 3 (next): Hover-tooltip system — generic TooltipController + per-element hover handlers
- Day 4: Selective damage numbers + hitstop
- Day 5: Enemy state flash + element-coded particle bursts

**Next milestones** (per `GDD_V0.7_delta.md` roadmap impact):
- M7.5: polish (`IModifierSource` interface unification)
- M8: Element system + tags (ElementId, WeaponFamily on WeaponData; WeaponLoadoutRuntime singleton)
- M9: Choice Economy + Synergy Identities + Wwise Stage 2 (RTPCs from `weapon_rarity_v1.md` §7; card-type distribution)
- M10: Implants + Music Spike (bandwidth budget, implant catalog, rarity → distortion mapping)
- M11: Skill Tree (300 nodes / 60 pts authoring + UI; Career L60 cap)
- M12: Level Design Pass (4 archetypes, in-level interactables, 10 stages, T1-T4 tiers)
- M13: Completion Tiers + Achievements + Mining Rig (rig parts collection, badges)

---

## Build / dev quick reference

- **Open project**: Unity Hub → CyberPickleHDRP (Unity 6.4)
- **Player build**: File → Build Profiles → Build
- **Player log** (Windows): `%USERPROFILE%\AppData\LocalLow\DopamineRush.games\CyberPickleHDRP\Player.log`
- **Scenes in build order**: Boot → MainMenu → EquipmentHub → Game (+ LevelSelect, PostGame)
- **Editor crash logs**: same Player.log path; check for `Stacktrace ===========` markers

---

*Last updated: 2026-05-09 — initial CLAUDE.md per current Anthropic best practices*
