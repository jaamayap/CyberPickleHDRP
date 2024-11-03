// File: Assets/Code/Core/Config/ConfigRegistry.cs
//
// Purpose: Central registry for all game configuration ScriptableObjects.
// Loads and manages access to all configuration assets using the Addressables system.
// Ensures configs are loaded before any manager initialization.
//
// Created: 2024-02-11
// Updated: 2024-02-11

// File: Assets/Code/Core/Config/ConfigRegistry.cs

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using CyberPickle.Core.Management;

namespace CyberPickle.Core.Config
{
    public class ConfigRegistry : Manager<ConfigRegistry>
    {
        private readonly Dictionary<Type, ScriptableObject> configs = new Dictionary<Type, ScriptableObject>();
        private readonly Dictionary<Type, AsyncOperationHandle> operationHandles = new Dictionary<Type, AsyncOperationHandle>();
        private bool isInitialized = false;

        private const string BOOT_CONFIG_KEY = "config/boot";
        private const string GAME_CONFIG_KEY = "config/game";

        protected override void OnManagerAwake()
        {
            Debug.Log("<color=yellow>[ConfigRegistry] Initializing...</color>");
        }

        public async Task InitializeAsync()
        {
            if (isInitialized)
            {
                Debug.LogWarning("[ConfigRegistry] Already initialized!");
                return;
            }

            try
            {
                await LoadCoreConfigs();
                await LoadUIConfigs();

                isInitialized = true;
                Debug.Log("<color=green>[ConfigRegistry] All configs loaded successfully!</color>");
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[ConfigRegistry] Failed to initialize: {e.Message}</color>");
                throw;
            }
        }

        private async Task LoadCoreConfigs()
        {
            Debug.Log("[ConfigRegistry] Loading core configs...");

            try
            {
                await LoadConfigAsync<BootConfig>(BOOT_CONFIG_KEY);
                await LoadConfigAsync<GameConfig>(GAME_CONFIG_KEY);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to load core configs: {e.Message}", e);
            }
        }

        private async Task LoadUIConfigs()
        {
            Debug.Log("[ConfigRegistry] Loading UI configs...");

            try
            {
                var loadOperation = Addressables.LoadAssetsAsync<ScriptableObject>(
                    "config-ui",
                    (config) =>
                    {
                        // This is called for each loaded asset
                        RegisterConfig(config);
                    }
                );

                operationHandles[typeof(ScriptableObject)] = loadOperation;
                await loadOperation.Task;

                if (loadOperation.Status != AsyncOperationStatus.Succeeded)
                {
                    throw new Exception($"UI configs load operation failed");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConfigRegistry] UI configs failed to load: {e.Message}");
                // Don't throw here - UI configs aren't critical
            }
        }

        private async Task<T> LoadConfigAsync<T>(string key) where T : ScriptableObject
        {
            try
            {
                var loadOperation = Addressables.LoadAssetAsync<T>(key);
                operationHandles[typeof(T)] = loadOperation;

                await loadOperation.Task;

                if (loadOperation.Status != AsyncOperationStatus.Succeeded)
                {
                    throw new Exception($"Failed to load config at key: {key}");
                }

                var config = loadOperation.Result;
                RegisterConfig(config);
                return config;
            }
            catch (Exception e)
            {
                throw new Exception($"Error loading config {typeof(T).Name}: {e.Message}", e);
            }
        }

        private void RegisterConfig<T>(T config) where T : ScriptableObject
        {
            var type = config.GetType();
            configs[type] = config;
            Debug.Log($"[ConfigRegistry] Registered config: {type.Name}");
        }

        public T GetConfig<T>() where T : ScriptableObject
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("[ConfigRegistry] Attempted to get config before initialization!");
            }

            if (configs.TryGetValue(typeof(T), out ScriptableObject config))
            {
                return config as T;
            }

            throw new Exception($"[ConfigRegistry] Config not found for type: {typeof(T).Name}");
        }

        protected override void OnManagerDestroyed()
        {
            foreach (var handle in operationHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            operationHandles.Clear();
            configs.Clear();
        }
    }
}
