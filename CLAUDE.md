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
| `GDD_V0.6.txt` | Game design, roadmap, milestones, content (cards, identities, breakpoints, implants), §3.5 music vision |
| `procedural_music_reference.md` | Music theory, modes, BPM math, Wwise hierarchy, Ableton pipeline — the composer-side reference |
| `modifier_engine_design.md` | Stats, modifiers, source taxonomy, persistence, analytics — **the architecture lock for everything stat-driven** |

When in doubt about a system's design, the modifier engine doc is the second-most-important reference after the GDD.

---

## Architectural pillars (LOCKED — do not deviate without a doc update)

### Modifier engine
- **Single source of truth**: `PlayerStats` MonoBehaviour. Every system reads from `PlayerStats.Get(stat)` — never re-computes locally.
- **Sourced attribution**: every `StatModifier` carries a `sourceId` of form `<category>_<id>[_<instance>]`. Categories: `equip`, `skill`, `implant`, `identity`, `breakpoint`, `run`, `boss`, `temp`.
- **AddPercent values are DECIMAL FRACTIONS**: `value: 0.10` = +10%. **Never** `value: 10` for "+10%" (that's +1000%, real bug fixed M7.4).
- **Burst reads via singleton**: ECS systems read `PlayerStatsData` (mirror), never the MonoBehaviour. Bridge writes in LateUpdate when dirty.
- **Main-thread mutation only**: `AddModifier` / `RemoveModifiersFromSource` from main thread. Never from Burst.

### Music system
- **Dexterity → tempo** (60-180 BPM)
- **Speed (movement) → master root MIDI** (±1 octave around C3)
- **Element per weapon → mode** (Fire = Phrygian Dominant, Lightning = Phrygian, Ice = Aeolian, etc.)
- **Weapon family → musical role**: Projectile = melody, Explosive = percussion, Beam = harmony
- **Weapon level (1-5 + Evolved) → pattern complexity**
- **Weapon damage → bus distortion / compression RTPC**
- **Luck → mega-crit roll** on top of crit
- **Grid**: 32 subdivisions per bar; per-pattern grain (8/16/32)
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

**Next milestones**: M7.5 polish (`IModifierSource` interface unification) → M8 element system + tags → M9 economy + identities → M10 specials + implants + music spike → M11 archetypes + skill tree.

---

## Build / dev quick reference

- **Open project**: Unity Hub → CyberPickleHDRP (Unity 6.4)
- **Player build**: File → Build Profiles → Build
- **Player log** (Windows): `%USERPROFILE%\AppData\LocalLow\DopamineRush.games\CyberPickleHDRP\Player.log`
- **Scenes in build order**: Boot → MainMenu → EquipmentHub → Game (+ LevelSelect, PostGame)
- **Editor crash logs**: same Player.log path; check for `Stacktrace ===========` markers

---

*Last updated: 2026-05-09 — initial CLAUDE.md per current Anthropic best practices*
