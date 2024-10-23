using System;

namespace MagicPigGames.ProjectileFactory
{
    /*
     * This attribute is used to show a field in the Projectile Editor.
     */

    [AttributeUsage(AttributeTargets.Field)]
    public class ShowInProjectileEditor : Attribute
    {
        public ShowInProjectileEditor(string fieldLabel)
        {
            FieldLabel = fieldLabel;
        }

        public string FieldLabel { get; private set; }
    }
}