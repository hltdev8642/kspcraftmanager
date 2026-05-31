using System;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Settings GUI window. Provides controls for all user-configurable options
    /// including KerbalX authentication, auto-sync intervals, thumbnail quality,
    /// display preferences, and toolbar position.
    /// </summary>
    public class SettingsWindow
    {
        private static SettingsWindow _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static SettingsWindow Instance => _instance ?? (_instance = new SettingsWindow());

        private bool _isVisible;
        private Rect _windowRect;
        private Vector2 _scrollPosition;

        // Temp values for editing (apply on save)
        private int _editSyncInterval;
        private int _editThumbQuality;
        private int _editThumbSize;
        private string _editApiKey;
        private bool _editUseGridView;

        private const int SettingsWindowId = 782342;
        private const float DefaultWidth = 500f;
        private const float DefaultHeight = 450f;

        // Tabs within settings
        private int _settingsTab;
        private readonly string[] _settingsTabNames = { "General", "KerbalX", "Display" };

        private SettingsWindow()
        {
            _windowRect = new Rect(200, 150, DefaultWidth, DefaultHeight);
        }

        /// <summary>
        /// Shows or hides the settings window.
        /// </summary>
        public void Toggle()
        {
            _isVisible = !_isVisible;
            if (_isVisible)
                LoadCurrentValues();
        }

        /// <summary>
        /// Shows the settings window.
        /// </summary>
        public void Show()
        {
            _isVisible = true;
            LoadCurrentValues();
        }

        /// <summary>
        /// Hides the settings window.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
        }

        /// <summary>
        /// Whether the settings window is visible.
        /// </summary>
        public bool IsVisible => _isVisible;

        /// <summary>
        /// Loads current settings into edit fields.
        /// </summary>
        private void LoadCurrentValues()
        {
            _editSyncInterval = SettingsManager.Instance.KerbalXSyncIntervalMinutes;
            _editThumbQuality = SettingsManager.Instance.ThumbnailQuality;
            _editThumbSize = SettingsManager.Instance.ThumbnailSize;
            _editApiKey = SettingsManager.Instance.KerbalXApiKey;
            _editUseGridView = SettingsManager.Instance.UseGridView;
            _scrollPosition = Vector2.zero;
        }

        /// <summary>
        /// Draws the settings window.
        /// </summary>
        public void DrawGUI()
        {
            if (!_isVisible) return;

            _windowRect = GUI.Window(SettingsWindowId, _windowRect, DrawWindowContent, "KSP Craft Manager — Settings",
                new GUIStyle
                {
                    normal = { background = MakeTexture(1, 1, new Color(0.08f, 0.08f, 0.12f, 0.95f)),
                        textColor = Color.white },
                    border = new RectOffset(4, 4, 4, 4),
                    padding = new RectOffset(4, 4, 20, 4),
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperCenter
                });
        }

        private void DrawWindowContent(int windowId)
        {
            GUILayout.BeginVertical();

            // Tab bar
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _settingsTabNames.Length; i++)
            {
                GUI.backgroundColor = _settingsTab == i ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.15f, 0.15f, 0.15f);
                if (GUILayout.Button(_settingsTabNames[i], GUILayout.Height(24), GUILayout.MinWidth(100)))
                    _settingsTab = i;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            switch (_settingsTab)
            {
                case 0: DrawGeneralTab(); break;
                case 1: DrawKerbalXTab(); break;
                case 2: DrawDisplayTab(); break;
            }

            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            // Bottom buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save", GUILayout.Width(100), GUILayout.Height(26)))
            {
                SaveSettings();
                Hide();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(26)))
            {
                Hide();
            }

            if (GUILayout.Button("Apply", GUILayout.Width(100), GUILayout.Height(26)))
            {
                SaveSettings();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
        }

        // ── General Tab ──────────────────────────────────────────────────

        private void DrawGeneralTab()
        {
            DrawSectionHeader("Sync & Performance");

            GUILayout.BeginHorizontal();
            GUILayout.Label("KerbalX Auto-Sync Interval (minutes):", GUILayout.Width(280));
            string syncStr = GUILayout.TextField(_editSyncInterval.ToString(), 8, GUILayout.Width(60));
            int.TryParse(syncStr, out _editSyncInterval);
            if (_editSyncInterval < 0) _editSyncInterval = 0;
            GUILayout.Label("(0 = disabled)", new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            DrawSectionHeader("Default Sort");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Default Sort Field:", GUILayout.Width(180));
            string[] sortFields = { "name", "partcount", "mass", "cost", "date" };
            int sortIdx = Array.IndexOf(sortFields, SettingsManager.Instance.DefaultSortField);
            if (sortIdx < 0) sortIdx = 0;
            string[] sortLabels = { "Name", "Part Count", "Mass", "Cost", "Date" };
            int newSortIdx = GUILayout.Toolbar(sortIdx, sortLabels, GUILayout.Height(22));
            if (newSortIdx != sortIdx)
                SettingsManager.Instance.DefaultSortField = sortFields[newSortIdx];
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Default Sort Direction:", GUILayout.Width(180));
            string[] dirs = { "asc", "desc" };
            int dirIdx = SettingsManager.Instance.DefaultSortDirection == "asc" ? 0 : 1;
            int newDirIdx = GUILayout.Toolbar(dirIdx, new[] { "Ascending", "Descending" }, GUILayout.Height(22));
            if (newDirIdx != dirIdx)
                SettingsManager.Instance.DefaultSortDirection = dirs[newDirIdx];
            GUILayout.EndHorizontal();
        }

        // ── KerbalX Tab ──────────────────────────────────────────────────

        private void DrawKerbalXTab()
        {
            DrawSectionHeader("Authentication");

            GUILayout.Label("KerbalX API Key:", GUILayout.Width(200));
            _editApiKey = GUILayout.PasswordField(_editApiKey, '*', 100, GUILayout.Width(380), GUILayout.Height(22));
            GUILayout.Space(4);

            GUILayout.Label("Get your API key from: https://kerbalx.com/settings/api",
                new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });
            GUILayout.Space(8);

            if (GUILayout.Button("Test Connection", GUILayout.Width(140), GUILayout.Height(24)))
            {
                // Save temp key and trigger a fetch
                KerbalXAPI.Instance.SetApiKey(_editApiKey);
                KerbalXBrowser.Instance.Refresh();
            }

            GUILayout.Space(12);
            DrawSectionHeader("KerbalX Browsing");

            GUILayout.BeginHorizontal();
            bool syncEnabled = GUILayout.Toggle(_editSyncInterval > 0, " Enable auto-sync");
            if (syncEnabled && _editSyncInterval <= 0) _editSyncInterval = 30;
            if (!syncEnabled) _editSyncInterval = 0;
            GUILayout.EndHorizontal();
        }

        // ── Display Tab ──────────────────────────────────────────────────

        private void DrawDisplayTab()
        {
            DrawSectionHeader("Thumbnails");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Thumbnail Quality (1-100):", GUILayout.Width(200));
            _editThumbQuality = (int)GUILayout.HorizontalSlider(_editThumbQuality, 1, 100, GUILayout.Width(200));
            GUILayout.Label(_editThumbQuality.ToString(), GUILayout.Width(30));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Thumbnail Size (pixels):", GUILayout.Width(200));
            string[] sizeOptions = { "64", "128", "256", "512" };
            int sizeIdx = Array.IndexOf(sizeOptions, _editThumbSize.ToString());
            if (sizeIdx < 0) sizeIdx = 2;
            int newSizeIdx = GUILayout.Toolbar(sizeIdx, sizeOptions, GUILayout.Height(22));
            if (newSizeIdx != sizeIdx)
                int.TryParse(sizeOptions[newSizeIdx], out _editThumbSize);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("Clear Thumbnail Cache", GUILayout.Width(180), GUILayout.Height(24)))
            {
                CraftCache.Instance.ClearCache();
            }

            GUILayout.Space(12);
            DrawSectionHeader("Layout");

            GUILayout.BeginHorizontal();
            _editUseGridView = GUILayout.Toggle(_editUseGridView, " Use grid view by default");
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            DrawSectionHeader("Data Management");

            if (GUILayout.Button("Clear Recently Opened List", GUILayout.Width(220), GUILayout.Height(24)))
            {
                CraftDataManager.Instance.RecentlyOpened.Clear();
            }

            if (GUILayout.Button("Refresh Craft List Now", GUILayout.Width(220), GUILayout.Height(24)))
            {
                CraftDataManager.Instance.RefreshCraftList();
                CraftManagerGUI.Instance.RequestRefresh();
            }

            if (GUILayout.Button("Auto-Generate Tags For All Crafts", GUILayout.Width(260), GUILayout.Height(24)))
            {
                CraftManagerGUI.Instance.AutoGenerateTagsForAll();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Draws a section header label.
        /// </summary>
        private void DrawSectionHeader(string text)
        {
            GUILayout.Label(text, new GUIStyle
            {
                normal = { textColor = new Color(1f, 0.8f, 0.2f) },
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 4, 2)
            });
            GUILayout.Space(4);
        }

        /// <summary>
        /// Saves edited settings back to SettingsManager.
        /// </summary>
        private void SaveSettings()
        {
            SettingsManager.Instance.KerbalXSyncIntervalMinutes = Mathf.Clamp(_editSyncInterval, 0, 1440);
            SettingsManager.Instance.ThumbnailQuality = Mathf.Clamp(_editThumbQuality, 1, 100);
            SettingsManager.Instance.ThumbnailSize = Mathf.Clamp(_editThumbSize, 32, 1024);
            SettingsManager.Instance.KerbalXApiKey = _editApiKey;
            SettingsManager.Instance.UseGridView = _editUseGridView;
            SettingsManager.Instance.Save();

            // Apply KerbalX API key immediately
            KerbalXAPI.Instance.SetApiKey(_editApiKey);

            Debug.Log("[KSPCraftManager] Settings saved.");
        }

        /// <summary>
        /// Creates a 1x1 texture.
        /// </summary>
        private Texture2D MakeTexture(int w, int h, Color c)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            tex.Apply();
            return tex;
        }
    }
}
