using System.Collections.Generic;
using System.Linq;
using InfinityPBR;
using UnityEditor;
using UnityEngine;
using static MagicPigGames.ProjectileFactory.ProjectileFactoryEditorUtilities;

namespace MagicPigGames.ProjectileFactory
{
    [InitializeOnLoad]
    [CustomEditor(typeof(ProjectileBehavior), true)]
    public class ProjectileBehaviorEditor : InfinityEditor
    {
        private readonly string _viewAllProjectilesKey = "View All Projectiles";
        private List<Projectile> _projectilesUsingThisBehavior = new();

        static ProjectileBehaviorEditor()
        {
            EditorApplication.delayCall += CacheObjects;
        }

        private ProjectileBehavior Target { get; set; }

        public override void OnInspectorGUI()
        {
            SetTarget();
            ProjectileHeader();
            //DrawUsage();
            GreyLine();
            DrawDefaultInspector();
        }

        private void DrawUsage()
        {
            if (!GetBool(_viewAllProjectilesKey))
                return;

            if (_projectilesUsingThisBehavior.Count == 0)
            {
                LabelGrey("<i>This behavior is not currently in use by any projectiles.</i>", false, true, true);
                return;
            }

            foreach (var projectile in _projectilesUsingThisBehavior)
            {
                StartRow();
                PingButton(projectile);
                Label(projectile.name);
                EndRow();
            }
        }

        private void ProjectileHeader()
        {
            StartRow();
            Label($"{textHightlight}PROJECTILE FACTORY{textColorEnd}", 150, true, false, true);
            LinkToDocs(ProjectileDocsURL);
            BackgroundColor(Color.cyan);
            if (Button($"Discord {symbolCircleArrow}"
                    , "This will open the Discord."))
                Application.OpenURL("https://discord.com/invite/cmZY2tH");
            ResetColor();
            EndRow();
            StartRow();
            GreyLine();

            // Icon
            // Display the icon of the behavior
            ContentColorIf(GetBool(_viewAllProjectilesKey), Color.white, Color.grey);
            var icon = Resources.Load<Texture2D>($"Icons/{Target.InternalIcon}");
            if (GUILayout.Button(icon, GUILayout.Width(60), GUILayout.Height(60)))
                SetBool(_viewAllProjectilesKey, !GetBool(_viewAllProjectilesKey));
            ResetColor();

            // Name / Type
            StartVertical();
            StartRow();
            PingButton(Target);
            Label($"<size=16>{Target.name}</size>", true, true, true);
            EndRow();
            //FlexibleSpace();
            //Label($"{textNormal}Used in {_projectilesUsingThisBehavior.Count} projectiles{textColorEnd}", true, true, true);
            EndVertical();

            StartVertical();
            // Description
            Label($"{textNormal}{Target.InternalDescription}{textColorEnd}", true, true, true);
            EndVertical();

            EndRow();
        }

        private static void CacheObjects()
        {
            //CacheProjectiles();
        }

        private void SetTarget()
        {
            if (Target == null)
            {
                CacheObjects();
                Target = (ProjectileBehavior)target;
                FilterCache();
            }

            Target = (ProjectileBehavior)target;
        }

        private void FilterCache()
        {
            _projectilesUsingThisBehavior = ProjectileObjects
                .Where(x => x.GetComponent<Projectile>().behaviors.Contains(Target)).ToList();
        }
    }
}