using System.Runtime.Remoting.Messaging;
using InfinityPBR;
using UnityEditor;
using UnityEngine;
using static MagicPigGames.ProjectileFactory.ProjectileFactoryEditorUtilities;


namespace MagicPigGames.ProjectileFactory
{
    [CustomEditor(typeof(ProjectileSpawner), true)]
    public class ProjectileSpawnerEditor : InfinityEditor
    {
        private static readonly string[] MenuOptions =
        {
            "Setup", "Trajectory", "Spawner Events", "Projectile Events"
        };

        private static readonly string[] SetupMenuOptions =
        {
            "Projectiles", "Observers", "Spawn Points"
        };

        private SerializedProperty observersListProperty;
        private SerializedProperty OnNewProjectileSelectedProperty;
        private SerializedProperty OnNewSpawnPointSelectedProperty;
        private SerializedProperty OnShowTrajectoryStartProperty;
        private SerializedProperty OnShowTrajectoryStopProperty;

        // Spawner Events
        private SerializedProperty OnSpawnProjectileProperty;
        private SerializedProperty OnStopProjectileProperty;
        private SerializedProperty ProjectileOnCollisionEnterProperty;
        private SerializedProperty ProjectileOnCollisionExitProperty;
        private SerializedProperty ProjectileOnCollisionStayProperty;
        private SerializedProperty ProjectileOnDoDestroyProperty;
        private SerializedProperty ProjectileOnDoDisableProperty;
        private SerializedProperty ProjectileOnDoEnableProperty;
        private SerializedProperty ProjectileOnGetFromPoolProperty;

        // Projectile Events
        private SerializedProperty ProjectileOnLaunchProperty;
        private SerializedProperty ProjectileOnProjectileSpawnerSetProperty;
        private SerializedProperty ProjectileOnProjectileStoppedProperty;
        private SerializedProperty ProjectileOnResetProperty;
        private SerializedProperty ProjectileOnReturnToPoolProperty;
        private SerializedProperty ProjectileOnTriggerEnterProperty;
        private SerializedProperty ProjectileOnTriggerExitProperty;
        private SerializedProperty ProjectileOnTriggerStayProperty;

        // Lists
        private SerializedProperty projectilesListProperty;
        private SerializedProperty spawnPointsListProperty;
        private ProjectileSpawner Target { get; set; }

        private void OnEnable()
        {
            // Lists
            projectilesListProperty = serializedObject.FindProperty("projectiles");
            observersListProperty = serializedObject.FindProperty("observers");
            spawnPointsListProperty = serializedObject.FindProperty("spawnPoints");

            // Spawner Events
            OnSpawnProjectileProperty = serializedObject.FindProperty("OnSpawnProjectile");
            OnStopProjectileProperty = serializedObject.FindProperty("OnStopProjectile");
            OnShowTrajectoryStartProperty = serializedObject.FindProperty("OnShowTrajectoryStart");
            OnShowTrajectoryStopProperty = serializedObject.FindProperty("OnShowTrajectoryStop");
            OnNewSpawnPointSelectedProperty = serializedObject.FindProperty("OnNewSpawnPointSelected");
            OnNewProjectileSelectedProperty = serializedObject.FindProperty("OnNewProjectileSelected");

            // Projectile Events
            ProjectileOnLaunchProperty = serializedObject.FindProperty("ProjectileOnLaunch");
            ProjectileOnProjectileStoppedProperty = serializedObject.FindProperty("ProjectileOnProjectileStopped");
            ProjectileOnTriggerEnterProperty = serializedObject.FindProperty("ProjectileOnTriggerEnter");
            ProjectileOnTriggerExitProperty = serializedObject.FindProperty("ProjectileOnTriggerExit");
            ProjectileOnTriggerStayProperty = serializedObject.FindProperty("ProjectileOnTriggerStay");
            ProjectileOnReturnToPoolProperty = serializedObject.FindProperty("ProjectileOnReturnToPool");
            ProjectileOnGetFromPoolProperty = serializedObject.FindProperty("ProjectileOnGetFromPool");
            ProjectileOnCollisionEnterProperty = serializedObject.FindProperty("ProjectileOnCollisionEnter");
            ProjectileOnCollisionExitProperty = serializedObject.FindProperty("ProjectileOnCollisionExit");
            ProjectileOnCollisionStayProperty = serializedObject.FindProperty("ProjectileOnCollisionStay");
            ProjectileOnDoDestroyProperty = serializedObject.FindProperty("ProjectileOnDoDestroy");
            ProjectileOnDoDisableProperty = serializedObject.FindProperty("ProjectileOnDoDisable");
            ProjectileOnDoEnableProperty = serializedObject.FindProperty("ProjectileOnDoEnable");
            ProjectileOnResetProperty = serializedObject.FindProperty("ProjectileOnReset");
            ProjectileOnProjectileSpawnerSetProperty =
                serializedObject.FindProperty("ProjectileOnProjectileSpawnerSet");
        }

        public override void OnInspectorGUI()
        {
            SetTarget();

            serializedObject.Update();
            ProjectileHeader();
            RuntimeOptions();

            Undo.RecordObject(Target, "Basic Projectile Change");
            SpawnerBody();

            if (!GUI.changed) return;
            EditorUtility.SetDirty(Target);
            serializedObject.ApplyModifiedProperties();
        }

        private void RuntimeOptions()
        {
            if (!Application.isPlaying) return;
            
            Space();
            Header2("Runtime Test", true);
            
            StartRow();
            if (Target.IsSpawning)
                BackgroundColor(Color.black);
            if (Button("Spawn Projectile", "This will spawn a projectile.", 120) && !Target.IsSpawning)
                Target.SpawnProjectile();
            ResetColor();

            if (!Target.IsSpawning)
                BackgroundColor(Color.black);
            if (Button("Stop Projectile", "This will stop the projectile.", 120) && Target.IsSpawning)
                Target.StopProjectile();
            ResetColor();
            if (Button("<<", "This will select the previous projectile.", 25))
                Target.NextProjectile(-1);
            if (Button(">>", "This will select the next projectile.", 25))
                Target.NextProjectile();
            EndRow();
        }

        private void SpawnerBody()
        {
            DrawRequired();
            GreyLine();

            ToolbarMenuMain(MenuOptions, "Projectile Spawner Toolbar");

            if (MenuOptions[GetInt("Projectile Spawner Toolbar")] == "Setup")
                DrawSetup();
            else if (MenuOptions[GetInt("Projectile Spawner Toolbar")] == "Trajectory")
                DrawTrajectory();
            else if (MenuOptions[GetInt("Projectile Spawner Toolbar")] == "Spawner Events")
                DrawSpawnerEvents();
            else if (MenuOptions[GetInt("Projectile Spawner Toolbar")] == "Projectile Events")
                DrawProjectileEvents();

            if (GetBool("Draw Default Inspector Spawner"))
            {
                Space();
                DrawDefaultInspector();
            }
        }

        private void DrawSetup()
        {
            Space();
            StartRow();
            Label($"Collision Mask {symbolInfo}",
                "This is the default collision mask for projectiles spawned by this spawner. " +
                "Individual projectiles may override this.", 150);
            Target.collisionMask = LayerMaskField(Target.collisionMask, 250);
            EndRow();

            StartRow();
            Label($"Target {symbolInfo}",
                "This is the target projectiles will be given. You can update this at runtime, and " +
                "projectiles can also receive new targets when they are active.", 150);
            var newTarget = Object(Target.Target, typeof(GameObject), 250, true) as GameObject;
            if (newTarget != Target.Target)
                Target.SetTarget(newTarget);
            EndRow();

            // September 8, 2024: I tried this, and turns out it may actually cause folks more headaches, so I'm turning
            // it off for now.
            Target.spawnPositionLookAtTarget = false;
            /*
            StartRow();
            Target.spawnPositionLookAtTarget = LeftCheck($"Spawn Position Look At Target {symbolInfo}", "When true, if a target is set, the spawn position will " +
                "look toward the target at the time of launch.", Target.spawnPositionLookAtTarget);
            EndRow();
            */
            
            StartRow();
            Target.setLayerOfProjectile = LeftCheck($"Set Layer of Projectile {symbolInfo}", "Optionally set the layer of the " +
                "projectile at launch. Useful for separating out the projectiles to ensure they don't hit friendly objects, as an example.", Target.setLayerOfProjectile);
            EndRow();
            if (Target.setLayerOfProjectile)
            {
                StartRow();
                Label($"Projectile Layer", 150);
                Target.projectileLayer = LayerField(Target.projectileLayer, 150);
                EndRow();
            }

            Space();
            ShowHelpBox("Add available projectiles here. You can add these at runtime as well. The " +
                        "spawner will need at least one projectile to operate properly.");
            if (Target.projectiles.Count == 0)
            {
                StartRow();
                IconError();
                LabelError("At least one projectile is required.", 250);
                EndRow();
            }

            DrawList(serializedObject, "projectiles");

            Space();
            ShowHelpBox("These observers will be added to all projectiles spawned by this spawner.");
            DrawList(serializedObject, "observers");

            Space();
            ShowHelpBox(
                "The SpawnPointManager determines how multiple spawn points are selected. It is not required if " +
                "you only have one Spawn Point. If you have multiple spawn points, you must have one or the first point " +
                "will always be used.");
            StartRow();
            if (Target.spawnPointManager == null && Target.spawnPoints.Count > 1)
                IconWarning();
            Label($"Spawn Point Manager {symbolInfo}",
                "Assign the Spawn Point Manager you'd like to use to control how multiple spawn points " +
                "are selected.", 150);
            Target.spawnPointManager =
                Object(Target.spawnPointManager, typeof(SpawnPointManager), 250) as SpawnPointManager;
            EndRow();
            ShowHelpBox("Your spawner can have multiple spawn points. Use a Spawn Point Manager to determine how " +
                        "to switch between multiple spawn points. You can also add spawn points at runtime. At least " +
                        "one spawn point is required.");
            if (Target.spawnPoints.Count == 0)
            {
                StartRow();
                IconError();
                LabelError("At least one spawn point is required.", 250);
                EndRow();
            }

            DrawList(serializedObject, "spawnPoints");

            Space();
            LeftCheckSetBool("Draw Default Inspector Spawner", "Draw Default Inspector");
        }

        private void DrawTrajectory()
        {
            ShowHelpBox("Trajectory is optional. If populated, this will be the default trajectory " +
                        "for the spawner. Individual projectiles can override this and provide their own " +
                        "specific trajectory.");

            StartRow();
            Label($"Default Trajectory Behavior {symbolInfo}", "This is the default trajectory for this " +
                                                               "spawner. Individual projectiles can override this.",
                250);
            Target.defaultTrajectoryBehavior =
                Object(Target.defaultTrajectoryBehavior, typeof(TrajectoryBehavior), 250) as TrajectoryBehavior;
            EndRow();

            Target.showTrajectoryWithoutProjectile = LeftCheck($"Show Without Projectile {symbolInfo}"
                , "If true, the trajectory will be shown even if " +
                  "there is no projectile assigned."
                , Target.showTrajectoryWithoutProjectile, 250);

            Space();
            StartRow();
            ButtonOpenClose("Draw Trajectory Runtime");
            Header3("Runtime Values");
            EndRow();
            if (GetBool("Draw Trajectory Runtime"))
            {
                ShowHelpBox("These values are generally set at runtime. They are used to determine " +
                            "if the trajectory should be shown, and if it should always be shown.");
                Target.alwaysShowTrajectory = LeftCheck($"Always Show Trajectory {symbolInfo}"
                    , "When true, the trajectory should always show."
                    , Target.alwaysShowTrajectory, 250);
                Target.showTrajectory = LeftCheck($"Show Trajectory {symbolInfo}"
                    , "The trajectory will show when this value is true."
                    , Target.showTrajectory, 250);
            }
        }

        private void DrawSpawnerEvents()
        {
            ShowHelpBox("These events are called by the spawner during its own lifecycle.");
            DrawEventBox(OnSpawnProjectileProperty, "On Launch");
            DrawEventBox(OnStopProjectileProperty, "On Projectile Stopped");
            DrawEventBox(OnShowTrajectoryStartProperty, "On Show Trajectory Start");
            DrawEventBox(OnShowTrajectoryStopProperty, "On Show Trajectory Stop");
            DrawEventBox(OnNewProjectileSelectedProperty, "On New Projectile Selected");
            DrawEventBox(OnNewSpawnPointSelectedProperty, "On New Spawn Point Selected");
        }

        private void DrawProjectileEvents()
        {
            ShowHelpBox("These events are called by the projectile during its own lifecycle. The " +
                        "values set here will be copied to each projectile when they are spawned, so the " +
                        "events will be fired by the projectile itself, NOT by the spawner.");

            DrawEventBox(ProjectileOnLaunchProperty, "On Launch");
            DrawEventBox(ProjectileOnProjectileStoppedProperty, "On Projectile Stopped");

            DrawEventBox(ProjectileOnReturnToPoolProperty, "On Return To Pool");
            DrawEventBox(ProjectileOnGetFromPoolProperty, "On Get From Pool");

            DrawEventBox(ProjectileOnCollisionEnterProperty, "On Collision Enter");
            DrawEventBox(ProjectileOnCollisionExitProperty, "On Collision Exit");
            DrawEventBox(ProjectileOnCollisionStayProperty, "On Collision Stay");

            DrawEventBox(ProjectileOnTriggerEnterProperty, "On Trigger Enter");
            DrawEventBox(ProjectileOnTriggerExitProperty, "On Trigger Exit");
            DrawEventBox(ProjectileOnTriggerStayProperty, "On Trigger Stay");

            DrawEventBox(ProjectileOnDoDestroyProperty, "On Do Destroy");
            DrawEventBox(ProjectileOnDoDisableProperty, "On Do Disable");
            DrawEventBox(ProjectileOnDoEnableProperty, "On Do Enable");
            DrawEventBox(ProjectileOnResetProperty, "On Reset");

            DrawEventBox(ProjectileOnProjectileSpawnerSetProperty, "On Projectile Spawner Set");
        }

        private void DrawRequired()
        {
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
            GreyLine();
            Header2($"{Target.name}", true);
            LabelGrey($"{Target.GetType()}");
            LeftCheckSetBool("ShowSpawnerHelp", "Show Help Boxes");
        }

        private void SetTarget()
        {
            Target = (ProjectileSpawner)target;
        }

        private void ShowHelpBox(string message)
        {
            if (!EditorPrefs.GetBool("ShowSpawnerHelp", true)) return;

            MessageBox(message);
        }
    }
}