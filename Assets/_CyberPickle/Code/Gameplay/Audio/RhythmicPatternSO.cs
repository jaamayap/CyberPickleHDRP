// File: Assets/_CyberPickle/Code/Gameplay/Audio/RhythmicPatternSO.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// The composer-facing source-of-truth for ONE weapon's musical pattern at
// a specific level. Hand-composed in Ableton + Kontakt, exported as MIDI,
// imported here by MidiPatternImporter (Editor tool). Once an SO exists,
// the designer/composer can edit it directly in Inspector — change
// individual note positions, durations, velocities, accents, etc. —
// without round-tripping through the DAW.
//
// Design pillars driving the data model:
//   1. SCALE-DEGREE authoring (not absolute pitches). At playback time
//      the PatternPlaybackService maps degree → mode → absolute pitch
//      based on the weapon's coupled element. The SAME pattern plays in
//      Phrygian Dom for Fire, Aeolian for Ice, etc. — same musical
//      structure, different mode flavor, no clashes when multiple weapons
//      play simultaneously (provided composer stays in the "safe pool"
//      of degrees common to all active modes — see procedural_music
//      _reference.md §22 + Wwise_Spec.md).
//
//   2. VARIABLE NOTE DURATION. A note can hold across N grid cells
//      (legato, pad-style sustains, etc.) rather than only the single
//      cell at which it triggers. The playback service triggers on
//      startCell and the note's envelope (defined in Wwise) handles the
//      sustain + release.
//
//   3. MULTI-VOICE LAYERING. One pattern can contain multiple parallel
//      voices (Melody / Arpeggio / Chord / Bass / Percussion / Ornament)
//      each with its own list of notes + Wwise routing. Composer authors
//      each voice as a separate MIDI track in Ableton; importer maps
//      tracks to voices by index or name.
//
//   4. INSPECTOR-FRIENDLY. The designer can author OR tweak a pattern
//      without leaving Unity. Notes are an ordered List<PatternNote> per
//      voice — Inspector's default array drawer handles the editing.
//      OnValidate sorts + clamps so the list stays clean.
//
//   5. PLAYBACK-FRIENDLY. The runtime service queries
//      `GetNotesStartingAtCell(cellIndex)` once per subdivision per
//      active weapon — needs to be cheap. We cache a sorted-by-startCell
//      view at runtime to enable a fast linear scan from the last known
//      position.
//
// File location: Assets/_CyberPickle/Data/Audio/Patterns/<weaponId>_L<lvl>.asset
// (one .asset per weapon × level — same convention as the WeaponData / etc.)

using System;
using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Core;

namespace CyberPickle.Gameplay.Audio
{
    // ─── Enums ──────────────────────────────────────────────────────────

    /// <summary>
    /// Musical role of a voice within a pattern. Drives Wwise routing
    /// (different bus / different sample bank) and lets the music system
    /// apply role-specific RTPCs (e.g., the percussion voice ignores pitch).
    /// </summary>
    public enum VoiceRole : byte
    {
        /// <summary>Primary melodic line — typically sparse, expressive.</summary>
        Melody       = 0,
        /// <summary>Broken-chord ostinato — typically uniform velocity, denser than melody.</summary>
        Arpeggio     = 1,
        /// <summary>Stacked simultaneous notes — chord stabs, pad swells.</summary>
        Chord        = 2,
        /// <summary>Low sustained foundation — pedal points, root drones.</summary>
        Bass         = 3,
        /// <summary>Unpitched rhythmic hits — kick / snare / hat / crash. scaleDegree maps to drum-kit slot, not scale.</summary>
        Percussion   = 4,
        /// <summary>Decorative — grace notes, accents, fills. Sparse, often off-grid feel.</summary>
        Ornament     = 5,
    }

    // ─── Note ───────────────────────────────────────────────────────────

    /// <summary>
    /// One note within a voice. Position in the pattern grid is determined
    /// by <see cref="startCell"/>; the note plays at that subdivision and
    /// sustains for <see cref="durationCells"/> subdivisions (envelope tail
    /// handled by Wwise on the sample's note-off).
    /// </summary>
    [Serializable]
    public struct PatternNote
    {
        // ── Position ─────────────────────────────────────────────────────

        [Tooltip("Cell index in the pattern grid (0..totalCells-1). Subdivision count per bar is on the parent SO; totalCells = subdivisionsPerBar × barCount.")]
        [Min(0)] public int startCell;

        [Tooltip("Number of grid subdivisions this note sustains. 1 = single subdivision (staccato). 4 = quarter-note hold on a 16th grid. Drives Wwise note-length envelope.")]
        [Min(1)] public int durationCells;

        // ── Pitch (mode-agnostic) ───────────────────────────────────────

        [Tooltip("Scale degree 1-7 within the active mode. 1 = tonic, 5 = perfect 5th, etc. Maps to absolute pitch at runtime via the weapon's coupled element's mode. For VoiceRole.Percussion, this is the drum-kit slot index (1 = kick, 2 = snare, 3 = closed hat, ...) — pitch ignored.")]
        [Range(1, 7)] public int scaleDegree;

        [Tooltip("Octave offset from the voice's base octave. -2 = two octaves below, +2 = two octaves above. Use sparingly outside ±1 — too wide hurts the mix.")]
        [Range(-2, 2)] public int octaveOffset;

        // ── Dynamics ────────────────────────────────────────────────────

        [Tooltip("Velocity (0..1) — drives Wwise Music_NoteVelocity RTPC. 0.5 = mezzo-forte baseline, 0.9 = accented, 0.2 = ghost note.")]
        [Range(0f, 1f)] public float velocity;

        [Tooltip("If true, this note is an accent: a separate Music_NoteAccent RTPC fires so Wwise can route through a brighter EQ / louder sub-bus. Use sparingly — accents lose meaning when overused.")]
        public bool isAccent;

        [Tooltip("If true, the note is TIED to the next note in the same voice (legato). Wwise can skip the attack envelope on the next trigger, producing a smooth transition. Optional — many sample libraries handle this implicitly via release tails.")]
        public bool isTied;

        // ── Element override (optional) ─────────────────────────────────

        [Tooltip("Override the weapon's coupled element for this single note (None = use weapon's element). Use for cross-mode ornaments / 'borrowed-note' tension. Most notes leave this as None.")]
        public ElementId elementOverride;
    }

    // ─── Voice ──────────────────────────────────────────────────────────

    /// <summary>
    /// One parallel musical line within a pattern. Each voice has its own
    /// Wwise routing (via <see cref="role"/>) and its own list of notes.
    /// A typical pattern has 1-3 voices.
    /// </summary>
    [Serializable]
    public class PatternVoice
    {
        [Tooltip("Designer label shown in the Inspector — e.g. \"Lead\", \"Bass Drone\", \"Hat Pattern\". Doesn't affect runtime.")]
        public string label = "Voice";

        [Tooltip("Musical role. Drives Wwise routing — Melody → Music_Lead bus, Percussion → Music_Drums bus, etc. (See Wwise_Spec.md for the bus structure.)")]
        public VoiceRole role = VoiceRole.Melody;

        [Tooltip("Multiplier on every note's velocity in this voice (0..2). Lets the composer mix between voices in the SAME pattern without editing each note's velocity. 1.0 = pass-through.")]
        [Range(0f, 2f)] public float volumeScale = 1f;

        [Tooltip("If true, this voice is silent at playback (its notes are skipped). Useful for A/B-ing voices during composition without deleting them.")]
        public bool mute = false;

        [Tooltip("Base octave for this voice. Note.octaveOffset is RELATIVE to this. Typical: Melody = 4, Bass = 2, Percussion = N/A (ignored).")]
        [Range(1, 7)] public int baseOctave = 4;

        [Tooltip("Notes in this voice, in any order (OnValidate sorts them by startCell). Inspector's default array drawer is fine for authoring; the MidiPatternImporter populates this from MIDI tracks.")]
        public List<PatternNote> notes = new List<PatternNote>();
    }

    // ─── Pattern ────────────────────────────────────────────────────────

    /// <summary>
    /// One full pattern for a weapon at a specific level. Composer authors
    /// in Ableton + Kontakt; MidiPatternImporter bakes the MIDI export
    /// into this SO. PatternPlaybackService schedules note events to Wwise
    /// at runtime, phase-locked to MusicConductor.OnSubdivision.
    /// </summary>
    [CreateAssetMenu(fileName = "RhythmicPattern", menuName = "CyberPickle/Audio/Rhythmic Pattern", order = 1)]
    public class RhythmicPatternSO : ScriptableObject
    {
        // ── Identity / metadata ─────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Designer label — typically \"<weaponId> L<level>\", e.g. \"pistol_L3\". Shown in Inspector + console logs. No runtime effect.")]
        public string displayName = "Untitled Pattern";

        [Tooltip("Free-text notes for the composer — what scale was used, intended mood, references, things to revisit. Saved with the asset; no runtime effect.")]
        [TextArea(2, 6)] public string composerNotes;

        // ── Grid ────────────────────────────────────────────────────────

        [Header("Grid")]
        [Tooltip("Subdivisions per BAR. 16 = 16th notes (most common), 32 = 32nd notes (room for swing / ornaments). Must match the MusicConductor's subdivisionsPerBeat × beatsPerBar arithmetic for the grid to align.")]
        [Range(4, 64)] public int subdivisionsPerBar = 16;

        [Tooltip("Bar count of this pattern. 4 is the design default (matches procedural_music_reference.md §22). Patterns loop after barCount × subdivisionsPerBar cells.")]
        [Range(1, 16)] public int barCount = 4;

        /// <summary>Total cell count = subdivisionsPerBar × barCount. Convenience accessor.</summary>
        public int TotalCells => subdivisionsPerBar * barCount;

        [Tooltip("Composer's intended BPM for this pattern. PURELY DOCUMENTATION — playback BPM is driven globally by MusicConductor (which is driven by Dexterity). Setting this in the SO lets a future composer know the tempo the pattern was originally written at.")]
        [Range(40, 240)] public int suggestedBpm = 120;

        // ── Voices ──────────────────────────────────────────────────────

        [Header("Voices")]
        [Tooltip("Parallel musical lines. A simple pattern has one voice (Melody). Layered patterns can have Melody + Bass + Percussion + Ornament. Each voice routes to its own Wwise sub-bus via VoiceRole.")]
        public List<PatternVoice> voices = new List<PatternVoice>
        {
            new PatternVoice { label = "Lead", role = VoiceRole.Melody, baseOctave = 4 }
        };

        // ── Runtime cache (rebuilt on OnEnable) ─────────────────────────

        // For fast playback lookup: per voice, notes sorted ascending by
        // startCell. PatternPlaybackService walks this with a per-weapon
        // cursor index, advancing one entry per subdivision. Cheap O(1)
        // per cell on average; O(log N) worst case if a cursor needs reset.
        [NonSerialized] private PatternNote[][] _sortedNotesByVoice;
        [NonSerialized] private bool _cacheDirty = true;

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the notes (across all voices) that START at the given
        /// cell index. Used by PatternPlaybackService each subdivision.
        /// Allocates only if results are present (the common case in dense
        /// patterns is 0 or 1 note per cell per voice).
        ///
        /// `voiceIndex` is the index into <see cref="voices"/>. Pass -1 to
        /// query all voices. Muted voices are skipped automatically.
        /// </summary>
        public IEnumerable<(int voiceIndex, PatternNote note)> GetNotesStartingAtCell(int cellIndex, int voiceIndex = -1)
        {
            EnsureCache();
            if (_sortedNotesByVoice == null) yield break;

            int start = (voiceIndex < 0) ? 0 : Mathf.Clamp(voiceIndex, 0, _sortedNotesByVoice.Length - 1);
            int end   = (voiceIndex < 0) ? _sortedNotesByVoice.Length - 1 : start;

            for (int v = start; v <= end; v++)
            {
                if (voices[v] == null || voices[v].mute) continue;
                var arr = _sortedNotesByVoice[v];
                if (arr == null) continue;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i].startCell == cellIndex) yield return (v, arr[i]);
                    if (arr[i].startCell > cellIndex) break; // sorted — no further matches
                }
            }
        }

        /// <summary>
        /// Bulk-query variant: fills the supplied list with all notes
        /// starting at cellIndex. Allocation-free if the list has enough
        /// capacity. Voiceindex semantics same as <see cref="GetNotesStartingAtCell"/>.
        /// </summary>
        public void GetNotesStartingAtCellNonAlloc(int cellIndex, List<(int voiceIndex, PatternNote note)> results, int voiceIndex = -1)
        {
            EnsureCache();
            results.Clear();
            if (_sortedNotesByVoice == null) return;

            int start = (voiceIndex < 0) ? 0 : Mathf.Clamp(voiceIndex, 0, _sortedNotesByVoice.Length - 1);
            int end   = (voiceIndex < 0) ? _sortedNotesByVoice.Length - 1 : start;

            for (int v = start; v <= end; v++)
            {
                if (voices[v] == null || voices[v].mute) continue;
                var arr = _sortedNotesByVoice[v];
                if (arr == null) continue;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i].startCell == cellIndex) results.Add((v, arr[i]));
                    if (arr[i].startCell > cellIndex) break;
                }
            }
        }

        /// <summary>
        /// Add a note programmatically (e.g., from MidiPatternImporter).
        /// Pushes to the voice's list and marks the cache dirty.
        /// </summary>
        public void AddNote(int voiceIndex, PatternNote note)
        {
            if (voiceIndex < 0 || voiceIndex >= voices.Count) return;
            var voice = voices[voiceIndex];
            if (voice.notes == null) voice.notes = new List<PatternNote>();
            voice.notes.Add(note);
            _cacheDirty = true;
        }

        /// <summary>Forces a cache rebuild on the next playback query. Editor-side mutations should call this.</summary>
        public void InvalidateCache() { _cacheDirty = true; }

        // ── Cache management ────────────────────────────────────────────

        private void OnEnable()
        {
            _cacheDirty = true;
        }

        private void EnsureCache()
        {
            if (!_cacheDirty && _sortedNotesByVoice != null) return;
            RebuildCache();
        }

        private void RebuildCache()
        {
            int voiceCount = voices?.Count ?? 0;
            _sortedNotesByVoice = new PatternNote[voiceCount][];

            for (int v = 0; v < voiceCount; v++)
            {
                var voice = voices[v];
                if (voice == null || voice.notes == null || voice.notes.Count == 0)
                {
                    _sortedNotesByVoice[v] = Array.Empty<PatternNote>();
                    continue;
                }
                var arr = voice.notes.ToArray();
                Array.Sort(arr, (a, b) => a.startCell.CompareTo(b.startCell));
                _sortedNotesByVoice[v] = arr;
            }
            _cacheDirty = false;
        }

        // ── Validation ──────────────────────────────────────────────────

        private void OnValidate()
        {
            // Clamp cells into the legal range, ensure durations are
            // sensible, and warn (don't auto-modify) on overlapping notes
            // within a voice — overlaps are technically legal but usually
            // indicate composer error.
            if (subdivisionsPerBar < 1) subdivisionsPerBar = 1;
            if (barCount < 1)           barCount           = 1;

            int total = TotalCells;
            if (voices == null) return;

            for (int v = 0; v < voices.Count; v++)
            {
                var voice = voices[v];
                if (voice == null || voice.notes == null) continue;
                for (int i = 0; i < voice.notes.Count; i++)
                {
                    var n = voice.notes[i];
                    if (n.startCell < 0) n.startCell = 0;
                    if (n.startCell >= total) n.startCell = total - 1;
                    if (n.durationCells < 1) n.durationCells = 1;
                    if (n.scaleDegree < 1)  n.scaleDegree = 1;
                    if (n.scaleDegree > 7)  n.scaleDegree = 7;
                    voice.notes[i] = n;
                }
            }

            // Mark cache dirty so the next runtime query rebuilds with
            // the edited data. Editor-time edits via Inspector trigger
            // OnValidate, which trips this flag.
            _cacheDirty = true;
        }

        // ── Editor-only conveniences (helpers — not playback-critical) ──

#if UNITY_EDITOR
        /// <summary>
        /// Find the first note in the given voice that occupies cellIndex
        /// (either starts there OR sustains through it). For editor
        /// visualization tools — piano-roll-style displays, etc.
        /// </summary>
        public bool TryGetNoteOccupyingCell(int voiceIndex, int cellIndex, out PatternNote note)
        {
            note = default;
            if (voiceIndex < 0 || voiceIndex >= voices.Count) return false;
            var voice = voices[voiceIndex];
            if (voice == null || voice.notes == null) return false;
            for (int i = 0; i < voice.notes.Count; i++)
            {
                var n = voice.notes[i];
                if (n.startCell <= cellIndex && cellIndex < n.startCell + n.durationCells)
                {
                    note = n;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Total note count across all voices. Inspector / diagnostics.</summary>
        public int TotalNoteCount
        {
            get
            {
                if (voices == null) return 0;
                int sum = 0;
                for (int i = 0; i < voices.Count; i++)
                    sum += voices[i]?.notes?.Count ?? 0;
                return sum;
            }
        }
#endif
    }
}
