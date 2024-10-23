using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using InfinityPBR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static InfinityPBR.InfinityEditor;
using static MagicPigGames.ProjectileFactory.ProjectileFactoryEditorUtilities;

namespace MagicPigGames.ProjectileFactory
{
    public class ProjectilesQuickEditor : EditorWindow
    {
        
        private List<Projectile> _projectiles;
        private List<ProjectileBehavior> _projectileBehaviors;
        private List<SpawnBehaviorModification> _spawnBehaviorModifications;
        
        [MenuItem("Window/Projectile Factory/Helpers/Projectiles Quick Editor", false, 99)]
        private static void Init()
        {
            var window = (ProjectilesQuickEditor)GetWindow(typeof(ProjectilesQuickEditor));
            window.Show();
        }

        [MenuItem("Window/Projectile Factory/Helpers/Projectiles Quick Editor", true)]
        private static bool ValidateMenu()
        {
            return Selection.activeObject != null;
            // Updated to handle more than just GameObjects
        }

        private int SelectedSpawnBehaviorModificationIndex
        {
            get => EditorPrefs.GetInt("ProjectilesQuickEditor_SelectedSpawnBehaviorModificationIndex", 0);
            set => EditorPrefs.SetInt("ProjectilesQuickEditor_SelectedSpawnBehaviorModificationIndex", value);
        }
        
        private int SelectedBehaviorIndex
        {
            get => EditorPrefs.GetInt("ProjectilesQuickEditor_SelectedBehaviorIndex", 0);
            set => EditorPrefs.SetInt("ProjectilesQuickEditor_SelectedBehaviorIndex", value);
        }
        
        private void OnEnable()
        {
            RefreshComponentCounts();
            CacheTheObjects();
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void CacheTheObjects()
        {
            // Cache the objects using ProjectileFactoryEditorUtilities
            CacheSpawnBehaviorModificationObjects();
            CacheProjectileBehaviorObjects();
            
            // Ensure the indexes are within bounds
            if (SelectedBehaviorIndex >= ProjectileBehaviorObjects.Count)
                SelectedBehaviorIndex = 0;
            if (SelectedSpawnBehaviorModificationIndex >= SpawnBehaviorModificationObjects.Count)
                SelectedSpawnBehaviorModificationIndex = 0;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }
        
        private void OnSelectionChange()
        {
            RefreshComponentCounts();
            Repaint();
        }
        
        private void OnUndoRedoPerformed()
        {
            RefreshComponentCounts();
            Repaint();
        }

        private void RefreshComponentCounts()
        {
            // Get all "Projectile" components in the selected objects
            _projectiles = new List<Projectile>();
            _projectileBehaviors = new List<ProjectileBehavior>();
            _spawnBehaviorModifications = new List<SpawnBehaviorModification>();
            
            foreach (var obj in Selection.objects)
            {
                if (obj is not GameObject gameObject) 
                    continue;
                
                var projectile = gameObject.GetComponent<Projectile>();
                if (projectile == null)
                    continue;
                    
                _projectiles.Add(projectile);
                _projectileBehaviors.AddRange(projectile.Behaviors);
                _spawnBehaviorModifications.AddRange(projectile.spawnBehaviorModifications);
            }
            
            // Make sure each entry in list is distinct
            _projectileBehaviors = new List<ProjectileBehavior>(_projectileBehaviors.Distinct().OrderBy(x => x.name));
            _spawnBehaviorModifications = new List<SpawnBehaviorModification>(_spawnBehaviorModifications.Distinct().OrderBy(x => x.name));
        }
        
        private void SetProjectilesDirty()
        {
            foreach (var projectile in _projectiles)
                EditorUtility.SetDirty(projectile);
        }

        private string[] _menuOptions = new string[]
        {
            "Behaviors",
            "Spawn Behavior Modifications"
        };

        private int SelectedMenuOption
        {
            get => EditorPrefs.GetInt("ProjectilesQuickEditor_SelectedMenuOption", 0);
            set => EditorPrefs.SetInt("ProjectilesQuickEditor_SelectedMenuOption", value);
        }
        
        private Vector2 scrollPosition;
        
        private void OnGUI()
        {
            var selectedObjectsCount = Selection.objects.Length;
            
            Header1("Projectiles Quick Editor");
            Label($"Add, remove, or replace Behaviors and Spawn Modification Behaviors on all {selectedObjectsCount} selected Projectiles.",
                false, true, true);

            Space();

            // Wrap your content in a scroll view
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            try
            {
                SelectedMenuOption = ToolbarMenu(_menuOptions, SelectedMenuOption);

                switch (SelectedMenuOption)
                {
                    case 0:
                        DrawBehaviors();
                        break;
                    case 1:
                        DrawSpawnBehaviorModifications();
                        break;
                }
            }
            finally
            {
                // Ensure the scroll view is properly ended even if an error occurs
                EditorGUILayout.EndScrollView();
            }
        }

        private string SelectedSpawnBehaviorModificationName => SpawnBehaviorModificationObjects[SelectedSpawnBehaviorModificationIndex].name;
        private string SelectedBehaviorName => ProjectileBehaviorObjects[SelectedBehaviorIndex].name;
        
        private void DrawSpawnBehaviorModifications()
        {
            StartRow();
            Label($"Spawn Behavior Modification:", 180);
            SelectedSpawnBehaviorModificationIndex = Popup(SelectedSpawnBehaviorModificationIndex, SpawnBehaviorModificationObjects.Select(x => x.name).ToArray(), 300);
            if (Button("Add to All", 120))
                AddSpawnBehaviorModificationToAll(SpawnBehaviorModificationObjects[SelectedSpawnBehaviorModificationIndex]);
            EndRow();
            
            BlackLine();
            
            foreach(var spawnBehaviorModification in _spawnBehaviorModifications)
                DrawSpawnBehaviorModification(spawnBehaviorModification);
        }

        private void DrawBehaviors()
        {
            StartRow();
            Label($"Behavior:", 100);
            SelectedBehaviorIndex = Popup(SelectedBehaviorIndex, ProjectileBehaviorObjects.Select(x => x.name).ToArray(), 300);
            if (Button("Add to All", 120))
                AddBehaviorToAll(ProjectileBehaviorObjects[SelectedBehaviorIndex]);
            EndRow();
            
            BlackLine();


            foreach(var behavior in _projectileBehaviors)
                DrawBehavior(behavior);
        }
        
        private void DrawSpawnBehaviorModification(SpawnBehaviorModification behavior)
        {
            StartRow();
            Label($"{behavior.name}", 300);
            if (Button("Remove", 75))
                RemoveAllSpawnBehaviorModifications(behavior);
            
            if (Button($"Replace", 75))
                ReplaceSpawnBehaviorModification(behavior);
            
            Label($"with {SelectedSpawnBehaviorModificationName}");
            EndRow();
        }

        private void DrawBehavior(ProjectileBehavior behavior)
        {
            StartRow();
            Label($"{behavior.name}", 300);
            if (Button("Remove", 75))
                RemoveAllBehaviors(behavior);
            
            if (Button($"Replace", 75))
                ReplaceBehavior(behavior);
            
            Label($"with {SelectedBehaviorName}");
            EndRow();
        }
        
        private void ReplaceSpawnBehaviorModification(SpawnBehaviorModification behavior)
        {
            foreach(var projectile in _projectiles)
            {
                if (!projectile.spawnBehaviorModifications.Contains(behavior))
                    continue;
                
                Undo.RecordObject(projectile, "Replace Spawn Behavior Modification");
                projectile.spawnBehaviorModifications.Remove(behavior);
                
                if (projectile.spawnBehaviorModifications.Contains(SpawnBehaviorModificationObjects[SelectedSpawnBehaviorModificationIndex]))
                    continue; 
                DoSpawnBehaviorModificationAdd(projectile, SpawnBehaviorModificationObjects[SelectedSpawnBehaviorModificationIndex]);
            }
            
            RefreshComponentCounts();
            SetProjectilesDirty();
        }
        
        private void ReplaceBehavior(ProjectileBehavior behavior)
        {
            foreach(var projectile in _projectiles)
            {
                if (!projectile.Behaviors.Contains(behavior))
                    continue;
                
                Undo.RecordObject(projectile, "Replace Behavior");
                projectile.Behaviors.Remove(behavior);
                
                if (projectile.Behaviors.Contains(ProjectileBehaviorObjects[SelectedBehaviorIndex]))
                    continue; 
                DoBehaviorAdd(projectile, ProjectileBehaviorObjects[SelectedBehaviorIndex]);
            }
            
            RefreshComponentCounts();
            SetProjectilesDirty();
        }

        private void RemoveAllSpawnBehaviorModifications(SpawnBehaviorModification behavior)
        {
            foreach (var projectile in _projectiles)
            {
                Undo.RecordObject(projectile, "Remove Behavior");
                projectile.spawnBehaviorModifications.Remove(behavior);
            }

            RefreshComponentCounts();
            SetProjectilesDirty();
        }
        
        private void RemoveAllBehaviors(ProjectileBehavior behavior)
        {
            foreach (var projectile in _projectiles)
            {
                Undo.RecordObject(projectile, "Remove Behavior");
                projectile.RemoveBehavior(behavior);
            }

            RefreshComponentCounts();
            SetProjectilesDirty();
        }
        
        private void AddSpawnBehaviorModificationToAll(SpawnBehaviorModification spawnBehaviorModificationObject)
        {
            foreach(var projectile in _projectiles)
                AddSpawnBehaviorModification(projectile, spawnBehaviorModificationObject);
            
            RefreshComponentCounts();
            SetProjectilesDirty();
        }
        
        private void AddBehaviorToAll(ProjectileBehavior behavior)
        {
            foreach(var projectile in _projectiles)
                AddBehavior(projectile, behavior);
            
            RefreshComponentCounts();
            SetProjectilesDirty();
        }
        
        private void AddSpawnBehaviorModification(Projectile projectile, SpawnBehaviorModification spawnBehaviorModificationObject)
        {
            if (projectile.spawnBehaviorModifications.Contains(spawnBehaviorModificationObject))
                return;
            
            Undo.RegisterCompleteObjectUndo(projectile, "Add Spawn Behavior Modification");
            DoSpawnBehaviorModificationAdd(projectile, SpawnBehaviorModificationObjects[SelectedSpawnBehaviorModificationIndex]);
        }
        
        private void AddBehavior(Projectile projectile, ProjectileBehavior projectileBehavior)
        {
            if (projectile.Behaviors.Contains(projectileBehavior))
                return;
            
            Undo.RegisterCompleteObjectUndo(projectile, "Add Behavior");
            DoBehaviorAdd(projectile, ProjectileBehaviorObjects[SelectedBehaviorIndex]);
        }

        private static void DoBehaviorAdd(Projectile projectile, ProjectileBehavior projectileBehavior) 
            => projectile.AddBehavior(projectileBehavior);
        
        private static void DoSpawnBehaviorModificationAdd(Projectile projectile, SpawnBehaviorModification spawnBehaviorModification) 
            => projectile.spawnBehaviorModifications.Add(spawnBehaviorModification);
        
    }

}
