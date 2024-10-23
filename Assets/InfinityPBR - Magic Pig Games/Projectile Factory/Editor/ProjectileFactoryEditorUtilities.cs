using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static InfinityPBR.InfinityEditor;
using Object = UnityEngine.Object;

namespace MagicPigGames.ProjectileFactory
{
    public static class ProjectileFactoryEditorUtilities
    {
        public const string ProjectileDocsURL =
            "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/projectile";

        public static List<Projectile> ProjectileObjects { get; private set; } = new();
        public static List<ProjectileData> ProjectileDataObjects { get; private set; } = new();
        public static List<SpawnBehavior> SpawnBehaviorObjects { get; private set; } = new();
        public static List<SpawnBehaviorModification> SpawnBehaviorModificationObjects { get; private set; } = new();
        public static List<ProjectileBehavior> ProjectileBehaviorObjects { get; private set; } = new();
        public static List<ProjectileObserver> ProjectileObserverObjects { get; private set; } = new();
        public static List<ObserverObjectObserver> ObserverObjectObserverObjects { get; private set; } = new();

        public static List<T> CacheObjects<T>() where T : Object
        {
            var i = 0;
            var guids1 = AssetDatabase.FindAssets($"t:{typeof(T)}", null);
            var allObjects = new T[guids1.Length];

            foreach (var guid1 in guids1)
            {
                allObjects[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid1));
                i++;
            }

            //Debug.Log($"Cached {allObjects.Length} {typeof(T)} objects.");
            return allObjects.OrderBy(x => x.name).ToList();
        }

        public static List<T> CacheObjectsWithProjectileComponent<T>() where T : Object
        {
            var time = Time.realtimeSinceStartup;
            // Get a list of all prefab game objects in the scene
            var allObjects = new List<T>();
            var gameObjectGuids = AssetDatabase.FindAssets("t:Prefab");

            foreach (var guid in gameObjectGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                var component = prefab.GetComponent<T>();
                if (component != null)
                    allObjects.Add(component);
            }

            Debug.Log($"Cached {allObjects.Count} {typeof(T)} objects.");
            Debug.Log($"Operation took {Time.realtimeSinceStartup - time} seconds.");
            return allObjects.OrderBy(x => x.name).ToList();
        }

        public static void CacheProjectiles()
        {
            ProjectileObjects = CacheObjectsWithProjectileComponent<Projectile>();
        }

        public static void CacheProjectileDataObjects()
        {
            ProjectileDataObjects = CacheObjects<ProjectileData>();
        }

        public static void CacheSpawnBehaviorObjects()
        {
            SpawnBehaviorObjects = CacheObjects<SpawnBehavior>();
        }

        public static void CacheSpawnBehaviorModificationObjects()
        {
            SpawnBehaviorModificationObjects = CacheObjects<SpawnBehaviorModification>();
        }

        public static void CacheProjectileBehaviorObjects()
        {
            ProjectileBehaviorObjects = CacheObjects<ProjectileBehavior>();
        }

        public static void CacheProjectileObserverObjects()
        {
            ProjectileObserverObjects = CacheObjects<ProjectileObserver>();
        }

        public static void CacheObserverObjectObserverObjects()
        {
            ObserverObjectObserverObjects = CacheObjects<ObserverObjectObserver>();
        }

        public static void DisplayFieldValue(FieldInfo field, Object modification, int index)
        {
            // Get field value
            var value = field.GetValue(modification);
            var label = field.GetCustomAttribute<ShowInProjectileEditor>().FieldLabel;

            // Check for Header attribute and append its value to the label if it exists.
            var header = field.GetCustomAttribute<HeaderAttribute>();
            if (header != null)
            {
                if (index > 0)
                    Space();
                Label($"{header.header}", true, false, true);
            }

            // Check for a tooltip, if so, add the info symbol to the label.
            var tooltip = field.GetCustomAttribute<TooltipAttribute>();
            var tooltipValue = "";
            if (tooltip != null)
            {
                label = $"{label} {symbolInfo}";
                tooltipValue = tooltip.tooltip;
            }

            // Handle specific field types
            if (field.FieldType == typeof(bool))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Check((bool)value);
                EndRow();

                //var newValue = EditorGUILayout.Toggle(label, (bool)value);
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(int))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Int((int)value);
                EndRow();
                //var newValue = EditorGUILayout.IntField(label, (int)value);
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(float))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Float((float)value);
                EndRow();

                //var newValue = EditorGUILayout.FloatField(label, (float)value);
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(string))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = TextField((string)value);
                EndRow();

                //var newValue = EditorGUILayout.TextField(label, (string)value);
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(Vector3))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Vector3Field((Vector3)value);
                EndRow();

                //var newValue = EditorGUILayout.Vector3Field(label, (Vector3)value);
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(GameObject))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Object((GameObject)value, typeof(GameObject)) as GameObject;
                EndRow();

                //var newValue = EditorGUILayout.ObjectField(label, (GameObject)value, typeof(GameObject), true) as GameObject;
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(LayerMask))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = LayerMaskField((LayerMask)value, 150);
                EndRow();
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(AnimationCurve))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Curve((AnimationCurve)value);
                EndRow();
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(Color))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = ColorField((Color)value);
                EndRow();
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(Vector2))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Vector2Field((Vector2)value);
                EndRow();

                //var newValue = EditorGUILayout.Vector2Field(label, (Vector2)value);
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(Vector4))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Vector4Field((Vector4)value);
                EndRow();

                //var newValue = EditorGUILayout.Vector4Field(label, (Vector4)value);
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(AudioClip))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Object((AudioClip)value, typeof(AudioClip)) as AudioClip;
                EndRow();

                //var newValue = EditorGUILayout.ObjectField(label, (AudioClip)value, typeof(AudioClip), true) as AudioClip;
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(Animation))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Object((Animation)value, typeof(Animation)) as Animation;
                EndRow();

                //var newValue = EditorGUILayout.ObjectField(label, (Animation)value, typeof(Animation), true) as Animation;
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(Sprite))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Object((Sprite)value, typeof(Sprite)) as Sprite;
                EndRow();

                //var newValue = EditorGUILayout.ObjectField(label, (Sprite)value, typeof(Sprite), true) as Sprite;
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(Texture2D))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = Object((Texture2D)value, typeof(Texture2D)) as Texture2D;
                EndRow();

                //var newValue = EditorGUILayout.ObjectField(label, (Texture2D)value, typeof(Texture2D), true) as Texture2D;
                field.SetValue(modification, newValue);
            }
            else if (field.FieldType == typeof(Enum))
            {
                StartRow();
                Label(label, tooltipValue);
                var newValue = EnumPopup((Enum)value);
                EndRow();
                field.SetValue(modification, newValue);
            }
            else
            {
                LabelGrey(
                    $"<i>{field.GetCustomAttribute<ShowInProjectileEditor>().FieldLabel} field type not supported: View the object in the Inspector to make changes.</i>",
                    false, true, true);
            }
        }
    }
}