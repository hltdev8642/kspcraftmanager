using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using KSP.UI.Screens;

namespace KSPCraftManager
{
    /// <summary>
    /// Main entry point for the KSP Craft Manager mod.
    /// Initializes all subsystems and manages the plugin lifecycle.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class KSPCraftManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the main plugin.
        /// </summary>
        public static KSPCraftManager Instance { get; private set; }

        /// <summary>
        /// Path to the plugin's data directory under GameData.
        /// </summary>
        public static string PluginDataPath { get; private set; }

        /// <summary>
        /// Path to the mod's GameData folder.
        /// </summary>
        public static string ModPath { get; private set; }

        /// <summary>
        /// Whether the main GUI window is visible.
        /// </summary>
        public bool IsWindowVisible { get; set; }

        /// <summary>
        /// All registered subsystem managers.
        /// </summary>
        private readonly List<IManager> _managers = new List<IManager>();

        /// <summary>
        /// Called by Unity when the MonoBehaviour is created.
        /// Performs one-time initialization of all mod subsystems.
        /// </summary>
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Resolve plugin paths
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            PluginDataPath = Path.GetDirectoryName(assemblyLocation);
            ModPath = Path.GetFullPath(Path.Combine(PluginDataPath, ".."));

            InitializeSubsystems();
        }

        /// <summary>
        /// Called by HotReloadKSP when this MonoBehaviour is hot-reloaded.
        /// The old component's non-reloading-assembly fields are auto-copied.
        /// We manually restore singleton, re-init subsystems, and clean up old state.
        /// </summary>
        /// <param name="old">The old KSPCraftManager component being replaced.</param>
        private void OnHotReload(MonoBehaviour old)
        {
            KSPCraftManager oldManager = old as KSPCraftManager;
            if (oldManager == null) return;

            Debug.Log("[KSPCraftManager] Hot-reload detected. Re-initializing subsystems.");

            // Clean up old component's subsystems before it gets destroyed
            oldManager.Shutdown();

            // Re-assign singleton to this new instance
            Instance = this;

            // Re-resolve paths (assembly location is from new assembly)
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            PluginDataPath = Path.GetDirectoryName(assemblyLocation);
            ModPath = Path.GetFullPath(Path.Combine(PluginDataPath, ".."));

            // Restore window visibility from old state
            IsWindowVisible = oldManager.IsWindowVisible;

            // Re-initialize all subsystems (creates fresh IManager instances)
            InitializeSubsystems();

            // Show/hide window based on restored state
            if (IsWindowVisible)
            {
                CraftManagerGUI.Instance.Show();
            }

            Debug.Log("[KSPCraftManager] Hot-reload complete.");
        }

        /// <summary>
        /// Called by HotReloadKSP when the assembly is first loaded (not a reload).
        /// </summary>
        public static void OnHotLoad()
        {
            Debug.Log("[KSPCraftManager] Assembly hot-loaded.");
        }

        /// <summary>
        /// Called by HotReloadKSP when the old assembly is being replaced.
        /// </summary>
        public static void OnHotUnload()
        {
            Debug.Log("[KSPCraftManager] Assembly hot-unloaded.");
        }

        /// <summary>
        /// Creates and initializes all mod subsystems in dependency order.
        /// </summary>
        private void InitializeSubsystems()
        {
            try
            {
                // Settings must be first — other systems depend on it
                SettingsManager.Instance.Initialize();
                _managers.Add(SettingsManager.Instance);

                // Toolbar integration
                ToolbarManager toolbar = ToolbarManager.Instance;
                toolbar.OnToolbarToggle += OnToolbarToggled;
                _managers.Add(toolbar);

                // Data managers — Favorites and Tags must come before CraftDataManager
                // since CraftDataManager.RefreshCraftList() reads them in ApplyFavoritesAndTags()
                FavoritesManager favorites = FavoritesManager.Instance;
                _managers.Add(favorites);

                TagManager tagManager = TagManager.Instance;
                _managers.Add(tagManager);

                CraftDataManager dataManager = CraftDataManager.Instance;
                _managers.Add(dataManager);

                VersionHistory versionHistory = VersionHistory.Instance;
                _managers.Add(versionHistory);

                ModDependencyChecker modChecker = ModDependencyChecker.Instance;
                _managers.Add(modChecker);

                KerbalXAPI kerbalXApi = KerbalXAPI.Instance;
                _managers.Add(kerbalXApi);

                // KerbalXBrowser (not in IManager list — initialized separately)
                KerbalXBrowser.Instance.Initialize();

                CraftCache craftCache = CraftCache.Instance;
                _managers.Add(craftCache);

                // Initialize all IManager instances
                foreach (IManager manager in _managers)
                {
                    try
                    {
                        manager.Initialize();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[KSPCraftManager] Failed to initialize {manager.GetType().Name}: {ex}");
                    }
                }

                Debug.Log("[KSPCraftManager] All subsystems initialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Critical initialization failure: {ex}");
            }
        }

        /// <summary>
        /// Called when the toolbar button is clicked. Toggles the main GUI window.
        /// </summary>
        private void OnToolbarToggled()
        {
            IsWindowVisible = !IsWindowVisible;
            if (IsWindowVisible)
            {
                CraftManagerGUI.Instance.Show();
            }
            else
            {
                CraftManagerGUI.Instance.Hide();
            }
        }

        /// <summary>
        /// Unity Update — calls per-frame updates on subsystems that need them.
        /// </summary>
        private void Update()
        {
            CraftDataManager.Instance.Update();
        }

        /// <summary>
        /// Unity OnGUI — renders the main GUI window and settings window when visible.
        /// </summary>
        private void OnGUI()
        {
            if (IsWindowVisible)
                CraftManagerGUI.Instance.DrawGUI();

            if (SettingsWindow.Instance.IsVisible)
                SettingsWindow.Instance.DrawGUI();

            // Also draw the comparison window overlay if active
            if (CraftComparer.Instance.IsVisible)
            {
                Rect compRect = new Rect(
                    Screen.width / 2f - 350, Screen.height / 2f - 250, 700, 500);
                CraftComparer.Instance.Draw(compRect);
            }
        }

        /// <summary>
        /// Called by Unity when the scene changes.
        /// </summary>
        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>
        /// Shuts down all subsystems gracefully.
        /// </summary>
        public void Shutdown()
        {
            foreach (IManager manager in _managers)
            {
                try
                {
                    manager.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[KSPCraftManager] Error shutting down {manager.GetType().Name}: {ex}");
                }
            }
            _managers.Clear();

            KerbalXBrowser.Instance.Shutdown();

            if (ToolbarManager.Instance != null)
            {
                ToolbarManager.Instance.OnToolbarToggle -= OnToolbarToggled;
            }

            Debug.Log("[KSPCraftManager] Shutdown complete.");
        }
    }
}
