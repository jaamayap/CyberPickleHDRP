// File: Assets/_CyberPickle/Code/Gameplay/Audio/MusicEvent.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// Canonical enumeration of every gameplay event that the audio system
// might react to. Producers (gameplay code) fire these via
// MusicEventBus.Fire(...). Consumers (Stage 0: Debug.Log; Stage 2+: Wwise
// event posts; the conductor; the damage feedback system; UI sounds)
// subscribe to MusicEventBus.OnEvent.
//
// Why a single enum instead of N separate C# events: one decoupled bus
// means we can add new producers/consumers without rewiring half the
// codebase, and the Stage 2 Wwise hook is a single mapping table from
// MusicEvent -> Ak event name.
//
// Byte-typed for serialization stability (cards / save data may reference
// these by id without binding to enum order). Add new entries at the end;
// never reorder.
//
// Payload convention: each event documents what payload type Fire() expects
// (or null). Day-1 stub uses object boxing; future iterations replace with
// typed Fire<T> overloads to avoid GC.

namespace CyberPickle.Gameplay.Audio
{
    public enum MusicEvent : byte
    {
        // ─── Run lifecycle ───────────────────────────────────────────────
        // payload: null
        RunStart        = 0,
        // payload: null
        RunEnd          = 1,
        // payload: RunStatePhase (the new phase)
        PhaseChanged    = 2,

        // ─── Combat — outgoing ───────────────────────────────────────────
        // payload: WeaponFireData? or weapon-id string (TBD when typed structs land)
        WeaponFire      = 10,
        // payload: damage value (float) — broadly mapped, drives RTPCs not notes
        EnemyHit        = 11,
        // payload: damage value (float) on a crit
        Crit            = 12,
        // payload: enemy-id or null
        EnemyDeath      = 13,

        // ─── Combat — incoming ───────────────────────────────────────────
        // payload: damage value (float)
        PlayerHit       = 20,
        PlayerHealed    = 21,

        // ─── Progression ─────────────────────────────────────────────────
        // payload: new level (int)
        LevelUp         = 30,
        // payload: card-id (string) — fires when the user hovers a card
        CardHover       = 31,
        // payload: card-id (string) — fires when the user picks a card
        CardPicked      = 32,
        CardBanished    = 33,
        CardRerolled    = 34,

        // ─── Bosses ──────────────────────────────────────────────────────
        BossSpawn       = 40,
        // payload: phase index (int) — 1, 2, 3 as boss HP crosses thresholds
        BossPhase       = 41,
        BossDefeated    = 42,

        // ─── UI (non-musical, but routed for audio mixing) ──────────────
        ButtonHover     = 50,
        ButtonClick     = 51,
        MenuOpen        = 52,
        MenuClose       = 53,
    }
}
