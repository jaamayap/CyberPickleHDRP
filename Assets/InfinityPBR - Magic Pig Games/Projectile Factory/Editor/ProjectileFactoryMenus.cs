using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicPigGames.ProjectileFactory
{
    public class ProjectileFactoryMenus : MonoBehaviour
    {
        private const string ProjectileFactoryString = "Projectile Factory/";

        private const string DocumentationString = ProjectileFactoryString + "\u238b Documentation &, Tutorials";

        private const string DiscordString = ProjectileFactoryString + "\u238b Discord Support";

        private const string SetProjectileLayerString =
            ProjectileFactoryString + "Helpers/Set Layer of all Projectiles";
        
        private const string SetTrailAutoDestroyFalseString =
            ProjectileFactoryString + "Helpers/Set Trail Auto Destroy to False";
        
        private const string RemoveColliders =
            ProjectileFactoryString + "Helpers/Remove Colliders";

        private const string SpawnOnImpactString = ProjectileFactoryString + "Shortcuts/Selections " +
                                                   "--> Spawn On Impact Object (Behavior)";

        private const string CreateObjectAtSpawnPositionString = ProjectileFactoryString + "Shortcuts/Selections " +
                                                                 "--> Create Object at Spawn Position (Spawn Behavior Modification)";


        private static float _setProjectileLayerProgress;

        [MenuItem("Window/" + DocumentationString, false, 1)]
        [MenuItem("Assets/Create/" + DocumentationString, false, 1)]
        private static void LinkToSupportURL()
        {
            var url = "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-overview";
            Application.OpenURL(url);
        }

        [MenuItem("Window/" + DiscordString, false, 1)]
        [MenuItem("Assets/Create/" + DiscordString, false, 1)]
        private static void LinkToDiscordURL()
        {
            var url = "https://discord.com/invite/cmZY2tH";
            Application.OpenURL(url);
        }

        [MenuItem("Window/" + SetProjectileLayerString, false, 99)]
        private static void SetAllProjectilesLayer()
        {
            // Check if required layers exist
            string[] requiredLayers = { "Projectile", "Post Processing" };
            var missingLayers = CheckForMissingLayers(requiredLayers);

            if (missingLayers.Any())
            {
                // Prompt to add missing layers
                var message = $"Projectile Factory recommends having a '{string.Join("' and '", missingLayers)}' " +
                              "layer(s). Would you like to add those first?\n\nThis is a manual process, which " +
                              "we can't do for you via script. The \"Layers\" window is usually in the top right " +
                              "corner -- open the popup and select \"Add Layer\".";
                ConfirmAddLayersDialog.ShowDialog(message, () =>
                    {
                        //OpenLayerWindow();
                        Debug.Log("The \"Layers\" window is usually in the top right corner of the Unity " +
                                  "editor, under the \"Layers\" tab. Click the \"Add Layer\" button to add the missing " +
                                  "layers.");
                        //ShowLayerSelectionDialog();
                    },
                    ShowLayerSelectionDialog);
            }
            else
            {
                // If no layers are missing, proceed directly
                ShowLayerSelectionDialog();
            }
        }

        private static List<string> CheckForMissingLayers(string[] requiredLayers)
        {
            return requiredLayers.Where(layer => LayerMask.NameToLayer(layer) == -1).ToList();
        }

        private static void OpenLayerWindow()
        {
            EditorApplication.ExecuteMenuItem("Edit/Layers...");
        }

        private static void ShowLayerSelectionDialog()
        {
            LayerSelectionDialog.ShowDialog(
                "Set Layers for All Projectiles?",
                "This will set the layer to all Projectile prefabs in your project, including children.",
                "Projectile",
                SetProjectilePrefabsLayer,
                () => Debug.Log("Layer setting was cancelled.")
            );
        }

        private static void SetProjectilePrefabsLayer(string selectedLayer)
        {
            _setProjectileLayerProgress = 0f;
            // Start a progress bar
            EditorUtility.DisplayProgressBar("Setting Layers for Projectiles",
                "Setting layers for all Projectile prefabs...", _setProjectileLayerProgress);

            var layerId = LayerMask.NameToLayer(selectedLayer);

            // Check if the result is a valid layer
            if (layerId == -1)
            {
                Debug.LogWarning($"Layer '{selectedLayer}' is not a valid layer. Please enter a valid layer name.");
                return;
            }

            // Get a list of all prefab assets in the project
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");

            var prefabsWithProjectiles = 0;
            // Check each prefab for a Projectile component on the parent
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                // Check if the prefab has a Projectile component on the parent
                if (prefab.GetComponent<Projectile>() != null)
                {
                    prefabsWithProjectiles++;
                    SetLayerForPrefabAndChildren(prefab, layerId);
                }

                _setProjectileLayerProgress += 1f / prefabGuids.Length;
                EditorUtility.DisplayProgressBar("Setting Layers for Projectiles",
                    "Setting layers for all Projectile prefabs...", _setProjectileLayerProgress);
            }

            Debug.Log(
                $"Updated {prefabsWithProjectiles} prefabs with Projectile components to layer '{selectedLayer}'.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        private static void SetLayerForPrefabAndChildren(GameObject prefab, int layerId)
        {
            var projectile = prefab.GetComponent<Projectile>();
            if (projectile != null) SetLayerRecursively(prefab, layerId);
        }

        private static void SetLayerRecursively(GameObject gameObject, int layerId)
        {
            gameObject.layer = layerId;
            foreach (Transform child in gameObject.transform) SetLayerRecursively(child.gameObject, layerId);
        }

        [MenuItem("Window/" + SpawnOnImpactString, false, 99)]
        [MenuItem("Assets/Create/" + SpawnOnImpactString, false, 50)]
        private static void MakeSpawnOnImpactObjectFromSelections()
        {
            // Ensure the user has selected at least one object
            if (Selection.objects.Length == 0)
            {
                Debug.LogWarning("No objects selected. Please select one or more prefabs in the Project view.");
                return;
            }

            var createdCount = 0;
            var newSelections = new Object[Selection.objects.Length];

            for (var i = 0; i < Selection.objects.Length; i++)
            {
                var selectedObject = Selection.objects[i];

                // Ensure the selected object is a prefab
                if (!PrefabUtility.IsPartOfAnyPrefab(selectedObject))
                {
                    Debug.LogWarning($"Selected object {selectedObject.name} is not a prefab.");
                    continue;
                }

                var assetPath = AssetDatabase.GetAssetPath(selectedObject);
                var directory = Path.GetDirectoryName(assetPath);

                // Create a new instance of the SpawnObjectOnTriggerOrCollision scriptable object
                var spawnObject = ScriptableObject.CreateInstance<SpawnObjectOnTriggerOrCollision>();
                spawnObject.objectToSpawn = (GameObject)selectedObject;

                // Create the new asset in the same location as the selected prefab
                var newAssetPath = Path.Combine(directory, $"{selectedObject.name}.asset");
                AssetDatabase.CreateAsset(spawnObject, newAssetPath);

                // Add the new object to the selection array
                newSelections[i] = AssetDatabase.LoadAssetAtPath<SpawnObjectOnTriggerOrCollision>(newAssetPath);

                createdCount++;
            }

            // Save the created assets to disk
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the newly created objects
            Selection.objects = newSelections;

            Debug.Log($"Created {createdCount} Spawn Object On Trigger Or Collision behavior objects!");
        }
        
        [MenuItem("Window/" + RemoveColliders, false, 99)]
        private static void RemoveCollidersFromObjects()
        {
            // Ensure the user has selected at least one object
            if (Selection.objects.Length == 0)
            {
                Debug.LogWarning("No objects selected. Please select one or more prefabs in the Project view.");
                return;
            }
            
            foreach (var selectedObject in Selection.objects)
            {
                // Ensure the selected object is a prefab
                if (!PrefabUtility.IsPartOfAnyPrefab(selectedObject))
                {
                    Debug.LogWarning($"Selected object {selectedObject.name} is not a prefab.");
                    continue;
                }
                
                // Get all colliders in the prefab
                var colliders = ((GameObject)selectedObject).GetComponentsInChildren<Collider>();
                
                // Remove all colliders
                foreach (var collider in colliders)
                {
                    Object.DestroyImmediate(collider);
                }
                
                // Debug.Log the results for this Object
                Debug.Log($"Removed {colliders.Length} Colliders from {selectedObject.name}");
            }

            // Save the created assets to disk
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        [MenuItem("Window/" + SetTrailAutoDestroyFalseString, false, 99)]
        private static void SetTrailAutoDestroyFalse()
        {
            // Ensure the user has selected at least one object
            if (Selection.objects.Length == 0)
            {
                Debug.LogWarning("No objects selected. Please select one or more prefabs in the Project view.");
                return;
            }

            foreach (var selectedObject in Selection.objects)
            {
                // Get all TrailRenderers in the prefab/non-prefab object
                var trailRenderers = ((GameObject)selectedObject).GetComponentsInChildren<TrailRenderer>();

                // Set auto destruct to false
                foreach (var trailRenderer in trailRenderers)
                {
                    trailRenderer.autodestruct = false;
                }

                // Debug.Log the results for this Object
                Debug.Log(
                    $"Set {trailRenderers.Length} TrailRenderers to autodestruct = false for {selectedObject.name}");
            }

            // Save the created assets to disk
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Window/" + CreateObjectAtSpawnPositionString, false, 99)]
        [MenuItem("Assets/Create/" + CreateObjectAtSpawnPositionString, false, 50)]
        private static void MakeCreateObjectAtSpawnPosition()
        {
            // Ensure the user has selected at least one object
            if (Selection.objects.Length == 0)
            {
                Debug.LogWarning("No objects selected. Please select one or more prefabs in the Project view.");
                return;
            }

            var createdCount = 0;
            var newSelections = new Object[Selection.objects.Length];

            for (var i = 0; i < Selection.objects.Length; i++)
            {
                var selectedObject = Selection.objects[i];

                // Ensure the selected object is a prefab
                if (!PrefabUtility.IsPartOfAnyPrefab(selectedObject))
                {
                    Debug.LogWarning($"Selected object {selectedObject.name} is not a prefab.");
                    continue;
                }

                var assetPath = AssetDatabase.GetAssetPath(selectedObject);
                var directory = Path.GetDirectoryName(assetPath);

                // Create a new instance of the SpawnObjectOnTriggerOrCollision scriptable object
                var spawnObject = ScriptableObject.CreateInstance<CreateObjectAtSpawnPosition>();
                spawnObject.objectToCreate = (GameObject)selectedObject;

                // Create the new asset in the same location as the selected prefab
                var newAssetPath = Path.Combine(directory, $"{selectedObject.name}.asset");
                AssetDatabase.CreateAsset(spawnObject, newAssetPath);

                // Add the new object to the selection array
                newSelections[i] = AssetDatabase.LoadAssetAtPath<CreateObjectAtSpawnPosition>(newAssetPath);

                createdCount++;
            }

            // Save the created assets to disk
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the newly created objects
            Selection.objects = newSelections;

            Debug.Log($"Created {createdCount} Create Object At Spawn Position Spawn Behavior Modifications!");
        }

        public static class ConfirmAddLayersDialog
        {
            public static void ShowDialog(string message, Action onYes, Action onNo)
            {
                if (EditorUtility.DisplayDialog("Add Missing Layers?", message, "Yes", "No"))
                    onYes.Invoke();
                else
                    onNo.Invoke();
            }
        }
    }
}