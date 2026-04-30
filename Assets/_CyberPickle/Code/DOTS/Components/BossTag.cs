// File: Assets/_CyberPickle/Code/DOTS/Components/BossTag.cs
// Namespace: CyberPickle.DOTS.Components
//
// Marker component flagging an enemy entity as a boss. Added by the
// EnemyAuthoring Baker only when EnemyData.isBoss is true.
// Future systems use this to:
//   - Show a boss health bar HUD
//   - Trigger boss music swell on spawn
//   - Scale contact damage / handle phase transitions
//   - Award special drops / achievement progress on death

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct BossTag : IComponentData { }
}
