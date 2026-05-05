// File: Assets/_CyberPickle/Code/DOTS/Components/XPGemTag.cs
// Namespace: CyberPickle.DOTS.Components
//
// Marker for an XP gem entity (an in-world drop awarded by killed enemies).
// XPMagnetSystem queries for this tag to drive the magnet pull and pickup.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct XPGemTag : IComponentData { }
}
