using System;
using InfinityPBR;
using UnityEditor;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CustomEditor(typeof(MonoBehaviour), true)] // This applies the custom editor to all MonoBehaviour classes
    public class ProjectileFactoryGeneralClassEditor : InfinityEditor
    {
        public override void OnInspectorGUI()
        {
            // Get the target MonoBehaviour
            var myTarget = target as MonoBehaviour;

            // Get the Custom Attribute from the class
            var documentation = (Documentation)Attribute.GetCustomAttribute(myTarget.GetType(), typeof(Documentation));

            // If the class doesn't have the DocumentationAttribute, it returns null so we need to make sure it's not null
            if (documentation != null)
            {
                StartRow();
                Label($"{textHightlight}PROJECTILE FACTORY{textColorEnd}", 150, true, false, true);
                LinkToDocs(documentation.URL);
                BackgroundColor(Color.cyan);
                if (Button($"Discord {symbolCircleArrow}"
                        , "This will open the Discord."))
                    Application.OpenURL("https://discord.com/invite/cmZY2tH");
                ResetColor();
                EndRow();
                Label($"{documentation.Description}", false, true, true);
                GreyLine();
            }

            // Call the original Inspector
            DrawDefaultInspector();
        }
    }
}