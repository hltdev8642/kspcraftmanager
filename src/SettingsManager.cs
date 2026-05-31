using System;
using System.IO;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Manages persistent configuration for the mod using KSP ConfigNode files.
    /// Stores all user-adjustable settings and provides defaults.
    /// </summary>
    public class SettingsManager : IManager
    {
        private static SettingsManager _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static SettingsManager Instance => _instance ?? (_instance = new SettingsManager());

        private const string SettingsFileName = "KSPCraftManagerSettings.cfg";
        private string _settingsFilePath;
        private ConfigNode _settingsNode;

        // ── Settings fields ──────────────────────────────────────────────

        /// <summary>Auto-sync interval in minutes for KerbalX. Zero disables.</summary>
        public int KerbalXSyncIntervalMinutes { get; set; } = 30;

        /// <summary>Thumbnail JPEG quality (1-100).</summary>
        public int ThumbnailQuality { get; set; } = 85;

        /// <summary>Thumbnail maximum pixel size (square).</summary>
        public int ThumbnailSize { get; set; } = 256;

        /// <summary>Default sort field for craft listings.</summary>
        public string DefaultSortField { get; set; } = "name";

        /// <summary>Default sort direction: asc or desc.</summary>
        public string DefaultSortDirection { get; set; } = "asc";

        /// <summary>Whether to use grid view (vs list view).</summary>
        public bool UseGridView { get; set; } = true;

        /// <summary>X position of the main GUI window.</summary>
        public float WindowX { get; set; } = 100f;

        /// <summary>Y position of the main GUI window.</summary>
        public float WindowY { get; set; } = 100f;

        /// <summary>Width of the main GUI window.</summary>
        public float WindowWidth { get; set; } = 900f;

        /// <summary>Height of the main GUI window.</summary>
        public float WindowHeight { get; set; } = 650f;

        /// <summary>Active tab index in the main GUI.</summary>
        public int ActiveTab { get; set; } = 0;

        /// <summary>KerbalX API key for authentication.</summary>
        public string KerbalXApiKey { get; set; } = string.Empty;

        // ── Lifecycle ────────────────────────────────────────────────────

        private SettingsManager() { }

        /// <summary>
        /// Loads settings from the config file, or creates defaults if none exists.
        /// </summary>
        public void Initialize()
        {
            _settingsFilePath = Path.Combine(KSPCraftManager.PluginDataPath, SettingsFileName);
            _settingsNode = new ConfigNode();

            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    ConfigNode loaded = ConfigNode.Load(_settingsFilePath);
                    if (loaded != null)
                    {
                        _settingsNode = loaded;
                        Deserialize();
                        Debug.Log("[KSPCraftManager] Settings loaded successfully.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[KSPCraftManager] Failed to load settings: {ex}. Using defaults.");
                }
            }

            // No settings file — serialize defaults
            Serialize();
            Save();
            Debug.Log("[KSPCraftManager] Default settings created.");
        }

        /// <summary>
        /// Saves current settings to disk.
        /// </summary>
        public void Shutdown()
        {
            Save();
        }

        // ── Serialization ────────────────────────────────────────────────

        /// <summary>
        /// Reads settings values from the internal ConfigNode.
        /// </summary>
        private void Deserialize()
        {
            int tmpInt;
            float tmpFloat;
            bool tmpBool;
            string tmpStr;

            if (TryGetValue("KerbalXSyncIntervalMinutes", out tmpInt)) KerbalXSyncIntervalMinutes = tmpInt;
            if (TryGetValue("ThumbnailQuality", out tmpInt)) ThumbnailQuality = tmpInt;
            if (TryGetValue("ThumbnailSize", out tmpInt)) ThumbnailSize = tmpInt;
            if (TryGetValue("DefaultSortField", out tmpStr)) DefaultSortField = tmpStr;
            if (TryGetValue("DefaultSortDirection", out tmpStr)) DefaultSortDirection = tmpStr;
            if (TryGetValue("UseGridView", out tmpBool)) UseGridView = tmpBool;
            if (TryGetValue("WindowX", out tmpFloat)) WindowX = tmpFloat;
            if (TryGetValue("WindowY", out tmpFloat)) WindowY = tmpFloat;
            if (TryGetValue("WindowWidth", out tmpFloat)) WindowWidth = tmpFloat;
            if (TryGetValue("WindowHeight", out tmpFloat)) WindowHeight = tmpFloat;
            if (TryGetValue("ActiveTab", out tmpInt)) ActiveTab = tmpInt;
            if (TryGetValue("KerbalXApiKey", out tmpStr)) KerbalXApiKey = tmpStr;
        }

        /// <summary>
        /// Writes current settings values to the internal ConfigNode.
        /// </summary>
        private void Serialize()
        {
            _settingsNode.SetValue("KerbalXSyncIntervalMinutes", KerbalXSyncIntervalMinutes.ToString(), true);
            _settingsNode.SetValue("ThumbnailQuality", ThumbnailQuality.ToString(), true);
            _settingsNode.SetValue("ThumbnailSize", ThumbnailSize.ToString(), true);
            _settingsNode.SetValue("DefaultSortField", DefaultSortField, true);
            _settingsNode.SetValue("DefaultSortDirection", DefaultSortDirection, true);
            _settingsNode.SetValue("UseGridView", UseGridView.ToString(), true);
            _settingsNode.SetValue("WindowX", WindowX.ToString("F1"), true);
            _settingsNode.SetValue("WindowY", WindowY.ToString("F1"), true);
            _settingsNode.SetValue("WindowWidth", WindowWidth.ToString("F1"), true);
            _settingsNode.SetValue("WindowHeight", WindowHeight.ToString("F1"), true);
            _settingsNode.SetValue("ActiveTab", ActiveTab.ToString(), true);
            _settingsNode.SetValue("KerbalXApiKey", KerbalXApiKey, true);
        }

        /// <summary>
        /// Writes the current configuration to the settings file on disk.
        /// </summary>
        public void Save()
        {
            try
            {
                Serialize();
                _settingsNode.Save(_settingsFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to save settings: {ex}");
            }
        }

        /// <summary>
        /// Helper: tries to parse an int value from the config node.
        /// </summary>
        private bool TryGetValue(string key, out int target)
        {
            string val = _settingsNode.GetValue(key);
            if (val != null && int.TryParse(val, out int parsed))
            {
                target = parsed;
                return true;
            }
            target = 0;
            return false;
        }

        /// <summary>
        /// Helper: tries to parse a float value from the config node.
        /// </summary>
        private bool TryGetValue(string key, out float target)
        {
            string val = _settingsNode.GetValue(key);
            if (val != null && float.TryParse(val, out float parsed))
            {
                target = parsed;
                return true;
            }
            target = 0f;
            return false;
        }

        /// <summary>
        /// Helper: tries to parse a bool value from the config node.
        /// </summary>
        private bool TryGetValue(string key, out bool target)
        {
            string val = _settingsNode.GetValue(key);
            if (val != null && bool.TryParse(val, out bool parsed))
            {
                target = parsed;
                return true;
            }
            target = false;
            return false;
        }

        /// <summary>
        /// Helper: reads a string value from the config node.
        /// </summary>
        private bool TryGetValue(string key, out string target)
        {
            string val = _settingsNode.GetValue(key);
            if (val != null)
            {
                target = val;
                return true;
            }
            target = null;
            return false;
        }
    }
}
