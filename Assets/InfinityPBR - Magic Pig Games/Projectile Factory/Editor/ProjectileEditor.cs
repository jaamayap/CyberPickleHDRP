using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InfinityPBR;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static MagicPigGames.ProjectileFactory.ProjectileFactoryEditorUtilities;
using Attribute = System.Attribute;
using Object = UnityEngine.Object;

namespace MagicPigGames.ProjectileFactory
{
    [InitializeOnLoad]
    [CustomEditor(typeof(Projectile))]
    public class ProjectileEditor : InfinityEditor
    {
        private static readonly string[] MenuOptions =
        {
            "Spawn Modifications", "Behaviors", "Events", "Options"
        };

        private List<ProjectileBehavior> _filteredBehaviors = new();
        private List<SpawnBehaviorModification> _filteredSpawnBehaviorModifications = new();

        private int _selectedBehavior;
        private int _selectedBestMatchBehavior;
        private int _selectedBestMatchSpawnBehaviorModification;

        private int _selectedSpawnBehaviorModification;
        private SerializedProperty OnDoCollisionEnterProperty;
        private SerializedProperty OnDoCollisionExitProperty;
        private SerializedProperty OnDoCollisionStayProperty;
        private SerializedProperty OnDoDestroyProperty;
        private SerializedProperty OnDoDisableProperty;
        private SerializedProperty OnDoEnableProperty;
        private SerializedProperty OnDoTriggerEnterProperty;
        private SerializedProperty OnDoTriggerExitProperty;
        private SerializedProperty OnDoTriggerStayProperty;
        private SerializedProperty OnGetFromPoolProperty;

        private SerializedProperty OnLaunchProperty;
        private SerializedProperty OnProjectileSpawnerSetProperty;
        private SerializedProperty OnProjectileStoppedProperty;
        private SerializedProperty OnResetProperty;
        private SerializedProperty OnReturnToPoolProperty;

        // Cached strings for the "Best Match" feature in the Editor
        private string _bestMatchBehavior;
        private string _bestMatchSpawnModification;
        private List<string> _bestMatchBehaviors;
        private List<string> _bestMatchSpawnBehaviorModifications;
        private ProjectileBehavior _cachedBestMatchBehavior;
        private SpawnBehaviorModification _cachedBestMatchSpawnBehaviorModification;
        
        static ProjectileEditor()
        {
            EditorApplication.delayCall += CacheObjects;
        }

        private Projectile Target { get; set; }

        private string BehaviorSearch
        {
            get => EditorPrefs.GetString("Behavior", "");
            set => EditorPrefs.SetString("Behavior", value);
        }

        private string SpawnBehaviorModificationSearch
        {
            get => EditorPrefs.GetString("SpawnBehaviorModificationSearch", "");
            set => EditorPrefs.SetString("SpawnBehaviorModificationSearch", value);
        }

        private void OnEnable()
        {
            // Link the serialized property to the actual property of the script
            OnLaunchProperty = serializedObject.FindProperty("OnLaunch");
            OnProjectileStoppedProperty = serializedObject.FindProperty("OnProjectileStopped");
            OnDoTriggerEnterProperty = serializedObject.FindProperty("OnDoTriggerEnter");
            OnDoTriggerExitProperty = serializedObject.FindProperty("OnDoTriggerExit");
            OnDoTriggerStayProperty = serializedObject.FindProperty("OnDoTriggerStay");
            OnReturnToPoolProperty = serializedObject.FindProperty("OnReturnToPool");
            OnGetFromPoolProperty = serializedObject.FindProperty("OnGetFromPool");
            OnDoCollisionEnterProperty = serializedObject.FindProperty("OnDoCollisionEnter");
            OnDoCollisionExitProperty = serializedObject.FindProperty("OnDoCollisionExit");
            OnDoCollisionStayProperty = serializedObject.FindProperty("OnDoCollisionStay");
            OnDoDestroyProperty = serializedObject.FindProperty("OnDoDestroy");
            OnDoDisableProperty = serializedObject.FindProperty("OnDoDisable");
            OnDoEnableProperty = serializedObject.FindProperty("OnDoEnable");
            OnResetProperty = serializedObject.FindProperty("OnReset");
            OnProjectileSpawnerSetProperty = serializedObject.FindProperty("OnProjectileSpawnerSet");
        }

        public override void OnInspectorGUI()
        {
            SetTarget();

            ProjectileHeader();
            //GreyLine();
            Undo.RecordObject(Target, "Basic Projectile Change");
            ProjectileBody();

            if (!GUI.changed) return;
            EditorUtility.SetDirty(Target);
            serializedObject.ApplyModifiedProperties();
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
            LeftCheckSetBool("ShowProjectileHelp", "Show Help Boxes");
        }

        private void ProjectileBody()
        {
            Undo.RecordObject(Target, "Change Projectile Data");
            DrawProjectileData();
            GreyLine();
            DrawSpawnBehavior();
            GreyLine();

            //ToolbarMenuMain(new[] { "Projectile Data", "Spawn Behavior", "Spawn Modifications", "Behaviors", "Options"}, "Projectile Toolbar");
            ToolbarMenuMain(MenuOptions, "Projectile Toolbar");

            if (MenuOptions[GetInt("Projectile Toolbar")] == "Spawn Modifications")
                DrawSpawnBehaviorModifications();
            else if (MenuOptions[GetInt("Projectile Toolbar")] == "Behaviors")
                DrawBehaviors();
            else if (MenuOptions[GetInt("Projectile Toolbar")] == "Options")
                DrawOptions();
            else if (MenuOptions[GetInt("Projectile Toolbar")] == "Events")
                DrawEvents();

            if (GetBool("Draw Default Inspector Projectile"))
            {
                Space();
                DrawDefaultInspector();
            }
        }

        private void DrawEvents()
        {
            Space();
            serializedObject.Update();

            DrawEventBox(OnLaunchProperty, "On Launch");
            DrawEventBox(OnProjectileStoppedProperty, "On Projectile Stopped");
            DrawEventBox(OnReturnToPoolProperty, "On Return To Pool");
            DrawEventBox(OnGetFromPoolProperty, "On Get From Pool");
            DrawEventBox(OnDoCollisionEnterProperty, "On Do Collision Enter");
            DrawEventBox(OnDoCollisionExitProperty, "On Do Collision Exit");
            DrawEventBox(OnDoCollisionStayProperty, "On Do Collision Stay");
            DrawEventBox(OnDoTriggerEnterProperty, "On Do Trigger Enter");
            DrawEventBox(OnDoTriggerExitProperty, "On Do Trigger Exit");
            DrawEventBox(OnDoTriggerStayProperty, "On Do Trigger Stay");
            DrawEventBox(OnDoDestroyProperty, "On Do Destroy");
            DrawEventBox(OnDoDisableProperty, "On Do Disable");
            DrawEventBox(OnDoEnableProperty, "On Do Enable");
            DrawEventBox(OnResetProperty, "On Reset");
            DrawEventBox(OnProjectileSpawnerSetProperty, "On Projectile Spawner Set");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawOptions()
        {
            Space();
            // FACTORY MANAGER
            StartVerticalBox();
            Header3("Factory Manager");
            Label(
                $"{textNormal}The Factory Manager is a static Scriptable Object available in all scenes, allowing Global " +
                "Observers to watch all registered projectiles. Check the documentation for more details and " +
                $"examples.{textColorEnd}", false, true, true);
            Target.registerWithProjectileFactoryManager = LeftCheck("Register with Factory Manager",
                Target.registerWithProjectileFactoryManager);
            EndVerticalBox();
            Space();

            // OBJECT POOL
            StartVerticalBox();
            Header3("Object Pooling");
            Label($"{textNormal}Each projectile can optionally use the Object Pooling system. This is useful for " +
                  "performance, especially in mobile games. Check the documentation for more details and " +
                  $"examples.{textColorEnd}", false, true, true);
            Target.useObjectPool = LeftCheck("Use Object Pool", Target.useObjectPool);
            EndVerticalBox();
            Space();

            // TRAJECTORY
            StartVerticalBox();
            Header3("Pre Launch Trajectory");
            Label(
                $"{textNormal}Select a TrajectoryBehavior to display pre-launch. This will override any TrajectoryBehavior " +
                "assigned to the ProjectileSpawner. If you wish to display an \"Active\" trajectory that displays when " +
                $"a Projectile is in the world, add that as a normal Behavior.{textColorEnd}", false, true, true);
            StartRow();
            Label("Pre Launch Trajectory", 150);
            Target.preLaunchTrajectory =
                Object(Target.preLaunchTrajectory, typeof(TrajectoryBehavior)) as TrajectoryBehavior;
            EndRow();
            EndVerticalBox();
            Space();

            // OPTIONS
            StartVerticalBox();
            Header3("Options");
            StartRow();
            Target.lookForwardAtSpawn = LeftCheck($"Look Forward at Spawn {symbolInfo}",
                "When true, the Projectile will look forward relative to the " +
                "Projectile Spawner Spawn Point. Generally this is ideal.", Target.lookForwardAtSpawn);
            EndRow();
            StartRow();
            Target.overrideCollisionMask = LeftCheck($"Override Collision Mask {symbolInfo}",
                "The Projectile Spawner CollisionMask will be replaced by " +
                "this mask if true.", Target.overrideCollisionMask);
            EndRow();
            StartRow();
            Label("Collision Mask", 150);
            Target.collisionMask = LayerMaskField(Target.collisionMask, 150);
            EndRow();

            Space();
            StartRow();
            Label("Best Match Algorithm", 150);
            StringSimilarity.BestMatchAlgorithm = (StringSimilarity.SimilarityAlgorithm) EnumPopup(StringSimilarity.BestMatchAlgorithm, 200);
            EndRow();
            
            StartRow();
            Label("Best Match Threshold", 150);
            var newThreshold = Float(StringSimilarity.BestMatchThreshold, 50);
            newThreshold = Mathf.Clamp(newThreshold, 0.0f, 100f);
            if (newThreshold != StringSimilarity.BestMatchThreshold)
            {
                StringSimilarity.BestMatchThreshold = newThreshold;
                CacheObjects();
            }
            EndRow();

            Space();
            StartRow();
            //Label("Draw Default Inspector", 150);
            LeftCheckSetBool("Draw Default Inspector Projectile", "Draw Default Inspector");
            EndRow();
            EndVerticalBox();
            Space();
        }

        private void DrawBehaviors()
        {
            Space();
            ShowHelpBox("Behaviors are scripts which are attached to the projectile " +
                        "and are called at various points in the projectile's life cycle. " +
                        "You can create your own behaviors by inheriting from ProjectileBehavior, " +
                        "and adding them to the projectile here.");

            if (ProjectileBehaviorObjects.Count == 0)
            {
                MessageBox("There are no ProjectileBehavior objects in your project! This should not happen, since " +
                           "Projectile Factory ships with default ProjectileBehavior objects. If you have deleted those, you may " +
                           "re-import the package to get them back, or create your own ProjectileBehavior objects.",
                    MessageType.Error);
                return;
            }

            for (var i = 0; i < Target.behaviors.Count; i++)
            {
                var behavior = Target.behaviors[i];
                DrawBehaviorRow(behavior, i);
            }

            Space();

            ShowFieldChangeWarning();
            LabelGrey($"{symbolImportant} <i>Behaviors are called in the order shown.</i>", false, true, true);
            Space();

            DrawAddNewBehavior();
        }

        private void DrawAddNewBehavior()
        {
            BackgroundColor(Color.yellow);
            StartVerticalBox();
            StartRow();
            Label("Add new Behavior", true);
            EndRow();

            ShowSelectBehavior();

            Space();
            ShowSearchBehavior();
            
            Space();
            ShowBestMatchBehavior();
            
            EndVerticalBox();
            ResetColor();
        }

        private void ShowSearchBehavior()
        {
            StartRow();
            Label("Search:", 75);
            var cachedSearch = BehaviorSearch;
            BehaviorSearch = TextField(BehaviorSearch, 200);
            if (_filteredBehaviors.Count == 0)
                LabelError("No objects found");
            else
                Label($"{_filteredBehaviors.Count} results");
            if (cachedSearch != BehaviorSearch)
            {
                BehaviorSearch.TrimSpaces();
                _filteredBehaviors = ProjectileBehaviorObjects
                    .Where(x => x.name.ToLower().Contains(BehaviorSearch.ToLower())).ToList();
                _selectedBehavior = 0;
            }

            EndRow();

            if (_filteredBehaviors.Count == 0)
            {
                LabelError("No objects found");
                return;
            }

            StartRow();
            if (_filteredBehaviors[_selectedBehavior] != null)
                PingButton(_filteredBehaviors[_selectedBehavior]);
            _selectedBehavior = Popup(_selectedBehavior, _filteredBehaviors.Select(x => x.name).ToArray(), 300);
            if (Button("Add", 50)) Target.AddBehavior(_filteredBehaviors[_selectedBehavior], false);
            EndRow();
            if (_filteredBehaviors[_selectedBehavior] != null)
                Label($"<i>{_filteredBehaviors[_selectedBehavior].InternalDescription}</i>", false, true, true);
        }

        private void ShowSelectBehavior()
        {
            StartRow();
            Label("Select:", 75);
            var selectedNewObject = Object(null, typeof(ProjectileBehavior), 200) as ProjectileBehavior;
            if (selectedNewObject != null)
            {
                Target.AddBehavior(selectedNewObject, false);
                ExitGUI();
            }

            EndRow();
        }

        private void ShowBestMatchBehavior()
        {
            StartRow();
            Label($"Best Match: {symbolInfo}", "Searches behaviors based on the name of the Projectile Object, and returns the " +
                                               "closest matches. If objects and behaviors are named with similar patterns, the ones " +
                                               "unique to this Projectile will populate here.", 100);
            EndRow();
            
            if (_bestMatchBehaviors.Count == 0)
            {
                LabelError("No objects found above match threshold. Adjust threshold in the \"Options\" menu.", 250, false, true, true);
                return;
            }
            
            if (_cachedBestMatchBehavior == null)
                CacheBestMatchBehaviorObject();
            
            if (_cachedBestMatchBehavior != null)
            {
                StartRow();
                PingButton(_cachedBestMatchBehavior);
                var cachedSelection = _selectedBestMatchBehavior;
                _selectedBestMatchBehavior = Popup(_selectedBestMatchBehavior, _bestMatchBehaviors.ToArray(), 300);
                if (cachedSelection != _selectedBestMatchBehavior)
                    CacheBestMatchBehaviorObject();
                if (Button("Add", 50)) Target.AddBehavior(_cachedBestMatchBehavior, false);
                EndRow();
                
                Label($"<i>{_cachedBestMatchBehavior.InternalDescription}</i>", false, true, true);
            }
        }

        private void CacheBestMatchBehaviorObject() 
            => _cachedBestMatchBehavior = ProjectileBehaviorObjects
                .FirstOrDefault(x => x.name == _bestMatchBehaviors[_selectedBestMatchBehavior]);

        private void DrawBehaviorRow(ProjectileBehavior behavior, int index)
        {
            // If Behavior is null, remove that index
            if (behavior == null)
            {
                Debug.LogWarning($"Behavior at index {index} is null and will be removed.");
                Target.behaviors.RemoveAt(index);
                return;
            }

            StartVerticalBox();

            StartRow();

            // ICON DISPLAY
            var iconName = behavior.InternalIcon;
            // Find the icon in Resources
            if (iconName != null)
            {
                ContentColorIf(ShowBehaviorDetails(behavior.name), Color.white, Color.grey);
                var icon = Resources.Load<Texture2D>($"Icons/{iconName}");
                if (icon != null && GUILayout.Button(icon, GUILayout.Width(50), GUILayout.Height(50)))
                    EditorPrefs.SetBool($"ShowBehaviorDetails-{behavior.name}", !ShowBehaviorDetails(behavior.name));
                ResetColor();
            }
            else
            {
                Label("", 50, 50);
            }

            // NAME AND BUTTONS
            StartVertical();
            StartRow();
            Label($"{behavior.name}", 220, true, true, true);
            EndRow();
            FlexibleSpace();
            StartRow();
            MoveDownButton(() => { MoveItemDown(Target.behaviors, index); }, index, Target.behaviors.Count);
            MoveUpButton(() => { MoveItemUp(Target.behaviors, index); }, index, Target.behaviors.Count);

            PingButton(behavior);
            ReplaceDuplicateBehavior(behavior, index);
            if (XButton())
            {
                Target.behaviors.Remove(behavior);
                EditorUtility.SetDirty(Target);
                ExitGUI();
            }

            EndRow();
            EndVertical();

            // DESCRIPTION
            StartVertical();
            FlexibleSpace();
            Label($"{textNormal}<size=10><i>{behavior.InternalDescription}</i></size>{textColorEnd}", false, true,
                true);
            //MessageBox($"{behavior.InternalDescription}");
            FlexibleSpace();
            EndVertical();

            EndRow();
            Space();

            // If we aren't showing the details, close the box and return
            if (!ShowBehaviorDetails(behavior.name))
            {
                EndVerticalBox();
                return;
            }

            ShowExposedFields(behavior);

            EndVerticalBox();
        }

        private void DrawProjectileData()
        {
            ShowHelpBox("The ProjectileData object contains the basic data for the projectile, " +
                        "such as \"speed\" and \"damage\". You will need to create a class which inherits " +
                        "from ProjectileData to customize how this dats is handled.\n\n" +
                        "For example, you likely will need to modify how the \"Damage\" value is calculated, " +
                        "or even the \"Speed\", which may be based on other factors unique to your game.");

            Space();
            if (ProjectileDataObjects.Count == 0)
            {
                MessageBox(
                    "WARNING: There are no ProjectileData objects in your project! This should not happen, since " +
                    "Projectile Factory ships with a default ProjectileData object. If you have deleted it, you should " +
                    "re-import the package to get it back, or create your own ProjectileData object.",
                    MessageType.Error);
                return;
            }

            StartRow();
            var exposedFieldsKey = "Show Projectile Data Exposed Fields";
            ButtonOpenClose(exposedFieldsKey);
            var tempDataObject = Target.ProjectileData;
            Label($"Projectile Data {symbolInfo}",
                "Choose the ProjectileData object attached to this Projectile, and " +
                "edit the exposed fields here, or when inspecting the ProjectileData " +
                "object itself.", 200, true, true, true);
            FlexibleSpace();
            var newProjectileData = Object(Target.ProjectileData, typeof(ProjectileData)) as ProjectileData;
            ReplaceDuplicateProjectileData(Target.ProjectileData);
            if (tempDataObject != newProjectileData)
            {
                Target.SetProjectileData(newProjectileData);
                EditorUtility.SetDirty(Target); // Mark the object as dirty before exiting
                ExitGUI();
            }
            EndRow();

            if (GetBool(exposedFieldsKey))
                if (ShowExposedFields(Target.ProjectileData) > 0)
                    ShowFieldChangeWarning();
        }
        
        private void DrawObservers()
        {
            Header3("Observers");
            ShowHelpBox("Observers will watch this projectile and can do actions at various life cycle events. The " +
                        "observers on the Projectile Spawner will be automatically added to all Projectiles created by that " +
                        "spawner. You can also add additional Observers here, which are children of the Projectile.\n\nAny " +
                        "child object with an observer will be added to this list automatically.");

            Space();
            if (Target.Observers.Count == 0)
                LabelGrey("No observers attached to this Projectile.");
            foreach (var observer in Target.Observers) DrawObserver(observer);
        }

        private void DrawObserver(ProjectileObserver observer)
        {
            StartRow();
            Label($"{observer.name}");
            EndRow();
        }

        private void ShowFieldChangeWarning()
        {
            LabelGrey($"{symbolImportant} <i>Field changes made here apply to the " +
                      "Projectile Data object. Some fields may not be exposed " +
                      "in this view.</i>", false, true, true);
        }

        private void ShowHelpBox(string message)
        {
            if (!EditorPrefs.GetBool("ShowProjectileHelp", true)) return;

            MessageBox(message);
        }

        private void DrawSpawnBehavior()
        {
            ShowHelpBox("The SpawnBehavior determines how a projectile is " +
                        "spawned. Many are included in Projectile Factory, and you can " +
                        "create your own by inheriting from SpawnBehavior, in order to " +
                        "provide custom spawning behavior unique to your project.");

            if (SpawnBehaviorObjects.Count == 0)
            {
                MessageBox(
                    "WARNING: There are no SpawnBehavior objects in your project! This should not happen, since " +
                    "Projectile Factory ships with default SpawnBehavior objects. If you have deleted those, you should " +
                    "re-import the package to get them back, or create your own SpawnBehavior object.",
                    MessageType.Error);
                return;
            }

            StartRow();
            var exposedFieldsKey = "Show Spawn Behavior Exposed Fields";
            ButtonOpenClose(exposedFieldsKey);
            Label($"Spawn Behavior {symbolInfo}", "Choose the SpawnBehavior object attached to this Projectile, and " +
                                                  "edit the exposed fields here, or when inspecting the SpawnBehavior " +
                                                  "object itself.", 200, true, true, true);
            FlexibleSpace();
            var tempDataObject = Target.SpawnBehavior;
            var newSpawnBehavior = Object(Target.SpawnBehavior, typeof(SpawnBehavior)) as SpawnBehavior;
            ReplaceDuplicateSpawnBehavior(Target.SpawnBehavior);
            if (tempDataObject != newSpawnBehavior)
            {
                Target.SetSpawnBehavior(newSpawnBehavior);
                EditorUtility.SetDirty(Target); // Mark the object as dirty before exiting
                ExitGUI();
            }

            EndRow();

            if (GetBool(exposedFieldsKey))
                if (ShowExposedFields(Target.SpawnBehavior) > 0)
                    ShowFieldChangeWarning();
        }

        private void DrawSpawnBehaviorModifications()
        {
            Space();
            ShowHelpBox("Spawn Behavior Modifications are optional behaviors which are called after the " +
                        "spawn behavior, but before any other behaviors. This allows for additional " +
                        "spawn logic unique to this object, such as attaching to the parent, or other custom behavior. " +
                        "These are only called once.");

            if (SpawnBehaviorModificationObjects.Count == 0)
            {
                MessageBox(
                    "There are no SpawnBehaviorModification objects in your project! This should not happen, since " +
                    "Projectile Factory ships with default SpawnBehaviorModification objects. If you have deleted those, you may " +
                    "re-import the package to get them back, or create your own SpawnBehaviorModification object.\n\nThese " +
                    "are optional.", MessageType.Info);
                return;
            }

            for (var i = 0; i < Target.spawnBehaviorModifications.Count; i++)
            {
                var modification = Target.spawnBehaviorModifications[i];
                DrawSpawnBehaviorModificationRow(modification, i);
            }

            if (Target.spawnBehaviorModifications.Count > 0)
            {
                Space();
                ShowFieldChangeWarning();
                LabelGrey($"{symbolImportant} <i>Modifications are called in the order shown.</i>", false, true, true);
                Space();
            }

            DrawAddNewSpawnBehaviorModification();
        }

        private bool ShowSpawnBehaviorModificationDetails(string modName)
        {
            return EditorPrefs.GetBool($"ShowSpawnBehaviorModificationDetails-{modName}", false);
        }

        private bool ShowBehaviorDetails(string modName)
        {
            return EditorPrefs.GetBool($"ShowBehaviorDetails-{modName}", false);
        }

        private void DrawSpawnBehaviorModificationRow(SpawnBehaviorModification modification, int index)
        {
            // If Modification is null, remove that index
            if (modification == null)
            {
                Target.spawnBehaviorModifications.RemoveAt(index);
                return;
            }

            StartVerticalBox();

            // ICON
            StartRow();
            var iconName = modification.InternalIcon;
            // Find the icon in Resources
            if (iconName != null)
            {
                ContentColorIf(ShowSpawnBehaviorModificationDetails(modification.name), Color.white, Color.grey);
                var icon = Resources.Load<Texture2D>($"Icons/{iconName}");
                if (icon != null && GUILayout.Button(icon, GUILayout.Width(50), GUILayout.Height(50)))
                    EditorPrefs.SetBool($"ShowSpawnBehaviorModificationDetails-{modification.name}",
                        !ShowSpawnBehaviorModificationDetails(modification.name));
                ResetColor();
            }
            else
            {
                Label("", 50, 50);
            }

            // NAME AND BUTTONS
            StartVertical();
            StartRow();
            //Header2($"{modification.name} {symbolInfo}", $"{modification.InternalDescription}");
            Label($"{modification.name}", 220, true, true, true);
            EndRow();
            FlexibleSpace();
            StartRow();
            MoveDownButton(() => { MoveItemDown(Target.spawnBehaviorModifications, index); }, index,
                Target.spawnBehaviorModifications.Count);
            MoveUpButton(() => { MoveItemUp(Target.spawnBehaviorModifications, index); }, index,
                Target.spawnBehaviorModifications.Count);

            PingButton(modification);
            ReplaceDuplicateSpawnBehaviorModification(modification, index);
            if (XButton())
            {
                Target.spawnBehaviorModifications.Remove(modification);
                EditorUtility.SetDirty(Target);
                ExitGUI();
            }

            EndRow();
            EndVertical();

            // DESCRIPTION
            StartVertical();
            FlexibleSpace();
            Label($"{textNormal}<size=10><i>{modification.InternalDescription}</i></size>{textColorEnd}", false, true,
                true);
            //MessageBox($"{behavior.InternalDescription}");
            FlexibleSpace();
            EndVertical();

            EndRow();
            Space();

            // If we aren't showing the details, close the box and return
            if (!ShowSpawnBehaviorModificationDetails(modification.name))
            {
                EndVerticalBox();
                return;
            }

            ShowExposedFields(modification);

            EndVerticalBox();
        }

        private int ShowExposedFields(Object obj)
        {
            if (obj == null) return 0;
            var exposedFieldsList = obj.GetType().GetFields()
                .Where(field => Attribute.IsDefined(field, typeof(ShowInProjectileEditor)))
                .ToList();

            if (exposedFieldsList.Count == 0)
            {
                LabelGrey("<i>No fields to display.</i>", false, true, true);
                return 0;
            }

            StartVerticalBox();

            var anyFieldChanged = false;
            for (var index = 0; index < exposedFieldsList.Count; index++)
            {
                var field = exposedFieldsList[index];
                EditorGUI.BeginChangeCheck(); // Start checking for changes
                DisplayFieldValue(field, obj, index);

                if (EditorGUI.EndChangeCheck()) // End checking for changes
                {
                    Undo.RecordObject(obj, "Field Value Change");
                    anyFieldChanged = true;
                }
            }

            if (anyFieldChanged) EditorUtility.SetDirty(obj);

            EndVerticalBox();

            return exposedFieldsList.Count;
        }


        private void CloneAndReplaceAsset<T>(T obj, Action<T> replacementAction) where T : Object
        {
            if (!Button("Replace w/ Copy", 110)) return;

            TextInputDialog.ShowDialog(
                "Replace with a Duplicate?",
                "This will create a new object with the same values as this " +
                "one, in the same location. Enter a new name for the duplicate:",
                $"{obj.name} copy",
                result =>
                {
                    var newObject = Instantiate(obj);
                    newObject.name = result;

                    replacementAction(newObject);

                    var originalPath = AssetDatabase.GetAssetPath(obj);
                    var directory = Path.GetDirectoryName(originalPath);
                    var newAssetPath = Path.Combine(directory, $"{result}.asset");
                    newAssetPath = AssetDatabase.GenerateUniqueAssetPath(newAssetPath);

                    AssetDatabase.CreateAsset(newObject, newAssetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                },
                () => { });
        }

        private void ReplaceDuplicateSpawnBehaviorModification(SpawnBehaviorModification obj, int index)
        {
            CloneAndReplaceAsset(obj, newObj =>
            {
                Target.spawnBehaviorModifications.Insert(index, newObj);
                Target.spawnBehaviorModifications.Remove(obj);
            });
        }

        private void ReplaceDuplicateProjectileData(ProjectileData obj)
        {
            CloneAndReplaceAsset(obj, newObj => { Target.SetProjectileData(newObj); });
        }

        private void ReplaceDuplicateSpawnBehavior(SpawnBehavior obj)
        {
            CloneAndReplaceAsset(obj, newObj => { Target.SetSpawnBehavior(newObj); });
        }

        private void ReplaceDuplicateBehavior(ProjectileBehavior obj, int index)
        {
            CloneAndReplaceAsset(obj, newObj =>
            {
                Target.behaviors.Insert(index, newObj);
                Target.behaviors.Remove(obj);
            });
        }

        private void DrawAddNewSpawnBehaviorModification()
        {
            BackgroundColor(Color.yellow);
            StartVerticalBox();
            StartRow();
            Label("Add new Modification", true);
            EndRow();

            ShowSelectSpawnBehaviorModification();

            Space();
            ShowSearchSpawnBehaviorModification();
            
            Space();
            ShowBestMatchSpawnBehaviorModification();
            
            EndVerticalBox();

            ResetColor();
        }

        private void ShowSearchSpawnBehaviorModification()
        {
            StartRow();
            Label("Search:", 75);
            var cachedSearch = SpawnBehaviorModificationSearch;
            SpawnBehaviorModificationSearch = TextField(SpawnBehaviorModificationSearch, 200);
            if (_filteredSpawnBehaviorModifications.Count == 0)
                LabelError("No objects found");
            else
                Label($"{_filteredSpawnBehaviorModifications.Count} results");
            if (cachedSearch != SpawnBehaviorModificationSearch)
            {
                SpawnBehaviorModificationSearch.TrimSpaces();
                _filteredSpawnBehaviorModifications = SpawnBehaviorModificationObjects
                    .Where(x => x.name.ToLower().Contains(SpawnBehaviorModificationSearch.ToLower())).ToList();
                _selectedSpawnBehaviorModification = 0;
            }

            EndRow();

            if (_filteredSpawnBehaviorModifications.Count == 0)
                return;

            StartRow();
            if (_filteredSpawnBehaviorModifications[_selectedSpawnBehaviorModification] != null)
                PingButton(_filteredSpawnBehaviorModifications[_selectedSpawnBehaviorModification]);
            _selectedSpawnBehaviorModification = Popup(_selectedSpawnBehaviorModification,
                _filteredSpawnBehaviorModifications.Select(x => x.name).ToArray(), 300);
            if (Button("Add", 50))
                Target.AddSpawnBehaviorModification(
                    _filteredSpawnBehaviorModifications[_selectedSpawnBehaviorModification]);
            EndRow();
            if (_filteredSpawnBehaviorModifications[_selectedSpawnBehaviorModification] != null)
                Label(
                    $"<i>{_filteredSpawnBehaviorModifications[_selectedSpawnBehaviorModification].InternalDescription}</i>",
                    false, true, true);
        }

        private void ShowSelectSpawnBehaviorModification()
        {
            StartRow();
            Label("Select:", 75);
            var selectedNewObject = Object(null, typeof(SpawnBehaviorModification), 200) as SpawnBehaviorModification;
            if (selectedNewObject != null)
            {
                Target.AddSpawnBehaviorModification(selectedNewObject);
                ExitGUI();
            }

            EndRow();
        }

        private void ShowBestMatchSpawnBehaviorModification()
        {
            StartRow();
            Label($"Best Match: {symbolInfo}", "Searches behaviors based on the name of the Projectile Object, and returns the " +
                                               "closest matches. If objects and behaviors are named with similar patterns, the ones " +
                                               "unique to this Projectile will populate here.", 100);
            EndRow();
            
            if (_bestMatchSpawnBehaviorModifications.Count == 0)
            {
                LabelError("No objects found above match threshold. Adjust threshold in the \"Options\" menu.", 250, false, true, true);
                return;
            }
            
            if (_cachedBestMatchSpawnBehaviorModification == null)
                CacheBestMatchSpawnBehaviorModificationObject();
            
            if (_cachedBestMatchSpawnBehaviorModification != null)
            {
                StartRow();
                PingButton(_cachedBestMatchSpawnBehaviorModification);
                var cachedSelection = _selectedBestMatchSpawnBehaviorModification;
                _selectedBestMatchSpawnBehaviorModification = Popup(_selectedBestMatchSpawnBehaviorModification, _bestMatchSpawnBehaviorModifications.ToArray(), 300);
                if (cachedSelection != _selectedBestMatchSpawnBehaviorModification)
                    CacheBestMatchSpawnBehaviorModificationObject();
                if (Button("Add", 50)) Target.AddSpawnBehaviorModification(_cachedBestMatchSpawnBehaviorModification);
                EndRow();
                
                Label($"<i>{_cachedBestMatchSpawnBehaviorModification.InternalDescription}</i>", false, true, true);
            }
        }

        private void CacheBestMatchSpawnBehaviorModificationObject() 
            => _cachedBestMatchSpawnBehaviorModification = SpawnBehaviorModificationObjects
                .FirstOrDefault(x => x.name == _bestMatchSpawnBehaviorModifications[_selectedBestMatchSpawnBehaviorModification]);

        private static void CacheObjects()
        {
            CacheProjectileDataObjects();
            CacheSpawnBehaviorObjects();
            CacheSpawnBehaviorModificationObjects();
            CacheProjectileBehaviorObjects();
        }

        private void FilterCache()
        {
            CacheFilteredSpawnBehaviorModifications();
            CacheFilteredBehaviors();
        }

        private void CacheFilteredSpawnBehaviorModifications()
        {
            SpawnBehaviorModificationSearch.TrimSpaces();
            _filteredSpawnBehaviorModifications = SpawnBehaviorModificationObjects
                .Where(x => x.name.ToLower().Contains(SpawnBehaviorModificationSearch.ToLower())).ToList();
            _selectedSpawnBehaviorModification = 0;
        }

        private void CacheFilteredBehaviors()
        {
            BehaviorSearch.TrimSpaces();
            _filteredBehaviors = ProjectileBehaviorObjects
                .Where(x => x.name.ToLower().Contains(BehaviorSearch.ToLower())).ToList();
            _selectedBehavior = 0;
        }

        private void SetTarget()
        {
            if (Target == null)
            {
                CacheObjects();
                FilterCache();
                //CacheObservers();
            }

            Target = (Projectile)target;

            CacheBestMatches();
        }

        private void CacheBestMatches()
        {
            _bestMatchBehavior = StringSimilarity.FindBestMatch(Target.gameObject.name, ProjectileBehaviorObjects.Select(x => x.name).ToList(), StringSimilarity.BestMatchAlgorithm);
            _bestMatchSpawnModification = StringSimilarity.FindBestMatch(Target.gameObject.name, SpawnBehaviorModificationObjects.Select(x => x.name).ToList(), StringSimilarity.BestMatchAlgorithm);

            //_selectedBestMatchBehavior = 0;
            //_selectedBestMatchSpawnBehaviorModification = 0;
            _bestMatchBehaviors = StringSimilarity.FindBestMatches(Target.gameObject.name, ProjectileBehaviorObjects
                .Select(x => x.name).ToList(), StringSimilarity.BestMatchThreshold, StringSimilarity.BestMatchAlgorithm); // Threshold can be adjusted
            _bestMatchSpawnBehaviorModifications = StringSimilarity.FindBestMatches(Target.gameObject.name, SpawnBehaviorModificationObjects
                .Select(x => x.name).ToList(), StringSimilarity.BestMatchThreshold, StringSimilarity.BestMatchAlgorithm); // Threshold can be adjusted
        }

        private void CacheObservers()
        {
            var observers = Target.GetComponentsInChildren<ProjectileObserver>().ToList();
            foreach (var observer in observers)
            {
                if (Target.Observers.Contains(observer)) continue;
                Target.Observers.Add(observer);
            }
        }
    }
}