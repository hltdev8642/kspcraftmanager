using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;

namespace KSPCraftManager
{
    /// <summary>
    /// Main GUI window implementation. Provides a resizable, draggable window
    /// with tabbed browsing (Local, KerbalX, Favorites), search/filter,
    /// sort controls, grid/list view toggle, and craft action buttons.
    /// </summary>
    public class CraftManagerGUI
    {
        private static CraftManagerGUI _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static CraftManagerGUI Instance => _instance ?? (_instance = new CraftManagerGUI());

        // ── Window State ─────────────────────────────────────────────────

        private Rect _windowRect;
        private bool _isVisible;
        private int _activeTab;
        private Vector2 _scrollPosition;
        private string _searchText = string.Empty;
        private string _filterType = string.Empty; // "", "VAB", "SPH"
        private string _sortField = "name";
        private string _sortDirection = "asc";
        private bool _useGridView = true;
        private bool _favoritesOnly;
        private float _minParts, _maxParts, _minMass, _maxMass;

        private const int WindowId = 782341;
        private const float DefaultWidth = 900f;
        private const float DefaultHeight = 650f;
        private const float TabHeight = 28f;
        private const float ToolbarHeight = 40f;
        private const float StatusBarHeight = 22f;
        private const float ThumbnailSize = 120f;
        private const float GridColumns = 5;

        // ── Tab identifiers ──────────────────────────────────────────────
        private readonly string[] _tabNames = { "Local", "KerbalX", "Favorites", "Search" };

        // ── Context Menu ─────────────────────────────────────────────────
        private enum ContextAction { Load, Duplicate, Compare, Export, Favorite, Tag, Delete, FolderMove }
        private CraftInfo _contextCraft;
        private bool _showContextMenu;
        private Rect _contextMenuRect;

        // ── Dialogs ──────────────────────────────────────────────────────
        private string _dialogTitle;
        private string _dialogMessage;
        private string _dialogInput;
        private bool _showDialog;
        private Action<bool, string> _dialogCallback;
        private DialogType _currentDialogType;
        private enum DialogType { RenameFolder, CreateFolder, DeleteConfirm, DuplicateCraft, ExportCraft, CompareSelect }

        // ── Folder Management ────────────────────────────────────────────
        private bool _showFolderManager;
        private string _selectedFolder;
        private string _folderType = "VAB";

        // ── Dependency Check Results ─────────────────────────────────────
        private ModDependencyResult _depResult;
        private bool _showDepResult;

        // ── KerbalX Auth ─────────────────────────────────────────────────
        private bool _showAuthDialog;

        // ── View cache ───────────────────────────────────────────────────
        private List<CraftInfo> _displayedCrafts = new List<CraftInfo>();
        private bool _needsRefresh = true;
        private float _lastRefreshTime;
        private const float RefreshCooldown = 0.5f;
        private int _selectedIndex = -1;

        private CraftManagerGUI()
        {
            _windowRect = new Rect(
                SettingsManager.Instance.WindowX,
                SettingsManager.Instance.WindowY,
                SettingsManager.Instance.WindowWidth,
                SettingsManager.Instance.WindowHeight
            );
            _activeTab = SettingsManager.Instance.ActiveTab;
            _useGridView = SettingsManager.Instance.UseGridView;
            _sortField = SettingsManager.Instance.DefaultSortField;
            _sortDirection = SettingsManager.Instance.DefaultSortDirection;

            // Hook into data changes
            FavoritesManager.Instance.OnFavoritesChanged += () => _needsRefresh = true;
            TagManager.Instance.OnTagsChanged += () => _needsRefresh = true;
            KerbalXBrowser.Instance.OnCraftsUpdated += () => _needsRefresh = true;
        }

        /// <summary>
        /// Shows the main GUI window.
        /// </summary>
        public void Show()
        {
            _isVisible = true;
            _needsRefresh = true;
            RefreshCraftList();
        }

        /// <summary>
        /// Hides the main GUI window.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            SaveWindowPosition();
        }

        /// <summary>
        /// Toggles visibility.
        /// </summary>
        public void Toggle()
        {
            if (_isVisible) Hide(); else Show();
        }

        /// <summary>
        /// Draws the main GUI window (called from OnGUI).
        /// </summary>
        public void DrawGUI()
        {
            if (!_isVisible) return;

            // Ensure the window stays within screen bounds
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - 100);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - 100);

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindowContent, "", GetWindowStyle());
        }

        /// <summary>
        /// Forces a refresh of the craft list on next draw.
        /// </summary>
        public void RequestRefresh()
        {
            _needsRefresh = true;
        }

        // ── Window Content ───────────────────────────────────────────────

        private void DrawWindowContent(int windowId)
        {
            GUILayout.BeginVertical();

            DrawTitleBar();

            DrawTabBar();

            DrawToolbar();

            DrawCraftListArea();

            DrawStatusBar();

            GUILayout.EndVertical();

            // Context menu overlay
            if (_showContextMenu)
                DrawContextMenu();

            // Dialog overlay
            if (_showDialog)
                DrawDialog();

            // Folder manager overlay
            if (_showFolderManager)
                DrawFolderManager();

            // Auth dialog overlay
            if (_showAuthDialog)
                DrawAuthDialog();

            // Dep check result overlay
            if (_showDepResult)
                DrawDepResultDialog();

            // Window dragging
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 24));
        }

        // ── Title Bar ────────────────────────────────────────────────────

        private void DrawTitleBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("KSP Craft Manager", new GUIStyle
            {
                normal = { textColor = new Color(1f, 0.8f, 0.2f) },
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 0, 4, 2)
            });

            GUILayout.FlexibleSpace();

            // Folders button
            if (GUILayout.Button("📁", GUILayout.Width(28), GUILayout.Height(22)))
                _showFolderManager = !_showFolderManager;

            // Settings button
            if (GUILayout.Button("⚙", GUILayout.Width(28), GUILayout.Height(22)))
                SettingsWindow.Instance.Toggle();

            // Close button
            if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(22)))
                Toggle();

            GUILayout.EndHorizontal();
        }

        // ── Tab Bar ──────────────────────────────────────────────────────

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _tabNames.Length; i++)
            {
                bool isActive = _activeTab == i;
                GUI.backgroundColor = isActive ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.15f, 0.15f, 0.15f);

                if (GUILayout.Button(_tabNames[i], GUILayout.Height(TabHeight), GUILayout.MinWidth(80)))
                {
                    if (_activeTab != i)
                    {
                        _activeTab = i;
                        _needsRefresh = true;
                        _selectedIndex = -1;
                        if (i == 1) // KerbalX tab
                        {
                            KerbalXBrowser.Instance.Refresh();
                        }
                        RefreshCraftList();
                    }
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        // ── Toolbar ──────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(ToolbarHeight));

            // Search box
            _searchText = GUILayout.TextField(_searchText, 50, GUILayout.Width(180), GUILayout.Height(22));
            if (GUILayout.Button("🔍", GUILayout.Width(24), GUILayout.Height(22)))
                _needsRefresh = true;

            // Type filter
            int typeIdx = _filterType == "VAB" ? 1 : _filterType == "SPH" ? 2 : 0;
            int newTypeIdx = GUILayout.Toolbar(typeIdx, new[] { "All", "VAB", "SPH" }, GUILayout.Height(22), GUILayout.Width(180));
            if (newTypeIdx != typeIdx)
            {
                _filterType = newTypeIdx == 0 ? "" : newTypeIdx == 1 ? "VAB" : "SPH";
                _needsRefresh = true;
            }

            GUILayout.Space(10);

            // Sort controls
            GUILayout.Label("Sort:", GUILayout.Width(35));
            string[] sortOptions = { "Name", "Parts", "Mass", "Cost", "Date", "Downloads" };
            int sortIdx = Array.IndexOf(sortOptions,
                _sortField == "name" ? "Name" :
                _sortField == "partcount" ? "Parts" :
                _sortField == "mass" ? "Mass" :
                _sortField == "cost" ? "Cost" :
                _sortField == "date" ? "Date" : "Downloads");
            if (sortIdx < 0) sortIdx = 0;

            int newSortIdx = GUILayout.Toolbar(sortIdx, sortOptions, GUILayout.Height(22), GUILayout.Width(360));
            if (newSortIdx != sortIdx)
            {
                _sortField = sortOptions[newSortIdx].ToLowerInvariant();
                _needsRefresh = true;
            }

            // Sort direction
            if (GUILayout.Button(_sortDirection == "asc" ? "↑" : "↓", GUILayout.Width(24), GUILayout.Height(22)))
            {
                _sortDirection = _sortDirection == "asc" ? "desc" : "asc";
                _needsRefresh = true;
            }

            GUILayout.FlexibleSpace();

            // Grid/List toggle
            if (GUILayout.Button(_useGridView ? "▦ Grid" : "☰ List", GUILayout.Width(60), GUILayout.Height(22)))
            {
                _useGridView = !_useGridView;
                SettingsManager.Instance.UseGridView = _useGridView;
            }

            GUILayout.EndHorizontal();
        }

        // ── Craft List Area ──────────────────────────────────────────────

        private void DrawCraftListArea()
        {
            if (Time.realtimeSinceStartup - _lastRefreshTime > RefreshCooldown && _needsRefresh)
            {
                RefreshCraftList();
                _needsRefresh = false;
                _lastRefreshTime = Time.realtimeSinceStartup;
            }

            float availableHeight = _windowRect.height - 80 - TabHeight - ToolbarHeight - StatusBarHeight;
            Rect listRect = new Rect(4, 74, _windowRect.width - 8, availableHeight);

            GUILayout.BeginArea(listRect);

            if (_displayedCrafts.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No crafts found. Try adjusting your search or filters.",
                    new GUIStyle { normal = { textColor = Color.gray }, fontSize = 13, alignment = TextAnchor.MiddleCenter },
                    GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                GUILayout.EndArea();
                return;
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            if (_useGridView)
                DrawGridView();
            else
                DrawListView();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws crafts in a grid layout with thumbnails.
        /// </summary>
        private void DrawGridView()
        {
            float viewWidth = _windowRect.width - 30;
            float effectiveThumbSize = ThumbnailSize + 12;
            int columns = Mathf.Max(1, Mathf.FloorToInt(viewWidth / effectiveThumbSize));

            for (int i = 0; i < _displayedCrafts.Count; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int j = 0; j < columns && i + j < _displayedCrafts.Count; j++)
                {
                    CraftInfo craft = _displayedCrafts[i + j];
                    DrawGridItem(craft, i + j);
                }
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Draws a single grid item (thumbnail + name).
        /// </summary>
        private void DrawGridItem(CraftInfo craft, int index)
        {
            bool isSelected = index == _selectedIndex;
            Rect itemRect = GUILayoutUtility.GetRect(ThumbnailSize + 16, ThumbnailSize + 36);

            // Background
            Color bgColor = isSelected ? new Color(0.25f, 0.4f, 0.6f, 0.5f) : new Color(0.1f, 0.1f, 0.12f, 0.3f);
            GUI.DrawTexture(itemRect, MakeTexture(1, 1, bgColor));

            // Favorite star
            if (craft.IsFavorite)
            {
                GUI.Label(new Rect(itemRect.x + 2, itemRect.y + 2, 16, 16), "★",
                    new GUIStyle { normal = { textColor = new Color(1f, 0.8f, 0.1f) }, fontSize = 14 });
            }

            // Thumbnail
            Texture2D thumb = CraftCache.Instance.GetThumbnail(craft);
            if (thumb != null)
            {
                GUI.DrawTexture(new Rect(itemRect.x + 8, itemRect.y + 4, ThumbnailSize, ThumbnailSize), thumb);
            }

            // Name label
            GUI.Label(new Rect(itemRect.x + 4, itemRect.y + ThumbnailSize + 6, ThumbnailSize + 8, 18),
                TruncateText(craft.Name, 16),
                new GUIStyle { normal = { textColor = Color.white }, fontSize = 10, alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip });

            // Part count
            GUI.Label(new Rect(itemRect.x + 4, itemRect.y + ThumbnailSize + 22, ThumbnailSize + 8, 12),
                $"{craft.PartCount} parts, {craft.TotalMass:F1}t",
                new GUIStyle { normal = { textColor = Color.gray }, fontSize = 9, alignment = TextAnchor.MiddleCenter });

            // Handle click
            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                _selectedIndex = index;
                CraftDetailsPanel.Instance.SelectCraft(craft);

                if (Event.current.button == 1) // Right click
                {
                    _contextCraft = craft;
                    _showContextMenu = true;
                    _contextMenuRect = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 180, 200);
                }
                else if (Event.current.button == 0 && Event.current.clickCount == 2)
                {
                    // Double-click: Load craft
                    LoadCraft(craft);
                }

                Event.current.Use();
            }
        }

        /// <summary>
        /// Draws crafts in a compact list view.
        /// </summary>
        private void DrawListView()
        {
            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(24)); // Star column
            GUILayout.Label("Name", GUILayout.Width(180));
            GUILayout.Label("Type", GUILayout.Width(50));
            GUILayout.Label("Parts", GUILayout.Width(50));
            GUILayout.Label("Mass", GUILayout.Width(60));
            GUILayout.Label("Cost", GUILayout.Width(80));
            GUILayout.Label("Mods", GUILayout.Width(80));
            GUILayout.Label("Modified", GUILayout.Width(120));
            GUILayout.Label("Source", GUILayout.Width(60));
            GUILayout.EndHorizontal();

            // Separator
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));

            // Craft rows
            for (int i = 0; i < _displayedCrafts.Count; i++)
            {
                CraftInfo craft = _displayedCrafts[i];
                DrawListRow(craft, i);
            }
        }

        /// <summary>
        /// Draws a single list row.
        /// </summary>
        private void DrawListRow(CraftInfo craft, int index)
        {
            bool isSelected = index == _selectedIndex;
            Color rowBg = isSelected ? new Color(0.2f, 0.35f, 0.55f, 0.4f) :
                (index % 2 == 0 ? new Color(0.08f, 0.08f, 0.1f, 0.2f) : Color.clear);

            GUILayout.BeginHorizontal(rowBg != Color.clear
                ? new GUIStyle { normal = { background = MakeTexture(1, 1, rowBg) } }
                : GUIStyle.none);

            // Favorite star
            GUILayout.Label(craft.IsFavorite ? "★" : "☆",
                new GUIStyle { normal = { textColor = craft.IsFavorite ? new Color(1f, 0.8f, 0.1f) : Color.gray },
                    fontSize = 14 }, GUILayout.Width(24));

            // Thumbnail small
            Texture2D thumb = CraftCache.Instance.GetThumbnail(craft);
            Rect thumbRect = GUILayoutUtility.GetRect(32, 32);
            if (thumb != null)
                GUI.DrawTexture(thumbRect, thumb);

            // Name
            if (GUILayout.Button(craft.Name, GetListLinkStyle(), GUILayout.Width(180)))
            {
                _selectedIndex = index;
                CraftDetailsPanel.Instance.SelectCraft(craft);
            }

            // Type
            GUILayout.Label(craft.Type, GUILayout.Width(50));

            // Parts
            GUILayout.Label(craft.PartCount.ToString("N0"), GUILayout.Width(50));

            // Mass
            GUILayout.Label(craft.TotalMass.ToString("F1") + "t", GUILayout.Width(60));

            // Cost
            GUILayout.Label("₧" + craft.TotalCost.ToString("N0"), GUILayout.Width(80));

            // Mods
            string mods = craft.RequiredMods.Count > 0
                ? (craft.RequiredMods.Count + (ModDependencyChecker.Instance.IsModInstalled(craft.RequiredMods[0]) ? "" : " ⚠"))
                : "stock";
            GUILayout.Label(mods, GUILayout.Width(80));

            // Modified
            GUILayout.Label(craft.LastModified.ToString("yyyy-MM-dd HH:mm"), GUILayout.Width(120));

            // Source
            GUILayout.Label(craft.Source == CraftSource.KerbalX ? "🌐" : "💻", GUILayout.Width(60));

            GUILayout.EndHorizontal();

            // Handle row click
            Rect rowRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                _selectedIndex = index;
                CraftDetailsPanel.Instance.SelectCraft(craft);

                if (Event.current.button == 1)
                {
                    _contextCraft = craft;
                    _showContextMenu = true;
                    _contextMenuRect = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 180, 200);
                }
                else if (Event.current.button == 0 && Event.current.clickCount == 2)
                {
                    LoadCraft(craft);
                }

                Event.current.Use();
            }
        }

        // ── Status Bar ──────────────────────────────────────────────────

        private void DrawStatusBar()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(StatusBarHeight));
            GUILayout.Label($"{_displayedCrafts.Count} craft(s) | Tab: {_tabNames[_activeTab]}",
                new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });
            GUILayout.FlexibleSpace();

            // Bottom-right action buttons
            if (_selectedIndex >= 0 && _selectedIndex < _displayedCrafts.Count)
            {
                CraftInfo selected = _displayedCrafts[_selectedIndex];

                if (GUILayout.Button("Load", GUILayout.Width(50), GUILayout.Height(20)))
                    LoadCraft(selected);

                if (GUILayout.Button("Duplicate", GUILayout.Width(70), GUILayout.Height(20)))
                    PromptDuplicateCraft(selected);

                if (GUILayout.Button("Compare", GUILayout.Width(70), GUILayout.Height(20)))
                    PromptCompareCraft(selected);

                if (GUILayout.Button("Export", GUILayout.Width(60), GUILayout.Height(20)))
                    ExportCraft(selected);

                if (GUILayout.Button(selected.IsFavorite ? "★" : "☆", GUILayout.Width(28), GUILayout.Height(20)))
                    ToggleFavorite(selected);

                // KerbalX tab: Download button
                if (_activeTab == 1 && selected.Source == CraftSource.KerbalX)
                {
                    KerbalXCraftListing listing = KerbalXBrowser.Instance.RemoteCrafts
                        .Find(l => l.Id == selected.KerbalXId);
                    if (listing != null)
                    {
                        bool isDl = KerbalXBrowser.Instance.IsDownloading(listing);
                        if (GUILayout.Button(isDl ? "..." : "⬇ Download", GUILayout.Width(80), GUILayout.Height(20)))
                        {
                            if (!isDl)
                                KerbalXBrowser.Instance.DownloadCraft(listing);
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        // ── Context Menu ─────────────────────────────────────────────────

        private void DrawContextMenu()
        {
            if (_contextCraft == null) return;

            // Ensure menu stays within screen bounds
            _contextMenuRect.x = Mathf.Clamp(_contextMenuRect.x, 0, Screen.width - 200);
            _contextMenuRect.y = Mathf.Clamp(_contextMenuRect.y, 0, Screen.height - 300);
            _contextMenuRect.width = 180;

            GUI.Box(_contextMenuRect, "", new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0.15f, 0.15f, 0.18f)) },
                border = new RectOffset(2, 2, 2, 2) });

            GUILayout.BeginArea(_contextMenuRect);
            GUILayout.Label(_contextCraft.Name, new GUIStyle { normal = { textColor = new Color(1f, 0.8f, 0.2f) },
                fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(6, 2, 4, 2) });
            GUILayout.Space(4);

            DrawContextItem("▶ Load", () => { LoadCraft(_contextCraft); _showContextMenu = false; });
            DrawContextItem("📋 Duplicate", () => { PromptDuplicateCraft(_contextCraft); _showContextMenu = false; });
            DrawContextItem("📊 Compare", () => { PromptCompareCraft(_contextCraft); _showContextMenu = false; });
            DrawContextItem("💾 Export", () => { ExportCraft(_contextCraft); _showContextMenu = false; });
            DrawContextItem(_contextCraft.IsFavorite ? "★ Unfavorite" : "☆ Favorite",
                () => { ToggleFavorite(_contextCraft); _showContextMenu = false; });
            DrawContextItem("🏷 Manage Tags", () => { PromptManageTags(_contextCraft); _showContextMenu = false; });

            GUILayout.Space(4);
            DrawContextItem("🔍 Check Mods", () => { CheckMods(_contextCraft); _showContextMenu = false; });
            DrawContextItem("📁 Move to Folder", () => { _showContextMenu = false; _showFolderManager = true; });

            if (_contextCraft.Source == CraftSource.Local)
            {
                GUILayout.Space(4);
                DrawContextItem("🗑 Delete", () => { PromptDeleteCraft(_contextCraft); _showContextMenu = false; });
            }

            GUILayout.EndArea();

            // Click outside to close
            if (Event.current.type == EventType.MouseDown && !_contextMenuRect.Contains(Event.current.mousePosition))
            {
                _showContextMenu = false;
                Event.current.Use();
            }
        }

        private void DrawContextItem(string label, Action action)
        {
            if (GUILayout.Button(label, new GUIStyle
            {
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f)) },
                hover = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.3f, 0.4f, 0.6f)) },
                padding = new RectOffset(8, 4, 3, 3),
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            }))
            {
                action();
            }
        }

        // ── Actions ──────────────────────────────────────────────────────

        /// <summary>
        /// Loads a craft into the appropriate editor (VAB or SPH).
        /// </summary>
        private void LoadCraft(CraftInfo craft)
        {
            try
            {
                // Record as recently opened
                CraftDataManager.Instance.RecordRecentlyOpened(craft);

                // Load the craft into the editor
                if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
                {
                    // Already in the editor — load the craft via ConfigNode
                    ConfigNode craftNode = ConfigNode.Load(craft.FilePath);
                    if (craftNode != null)
                    {
                        ShipConstruct ship = new ShipConstruct();
                        ship.LoadShip(craftNode);
                        EditorLogic.fetch.ship = ship;
                    }
                }
                else
                {
                    // Navigate to the correct editor first
                    string editorScene = craft.Type == "SPH" ? "SPH" : "VAB";
                    HighLogic.LoadScene(GameScenes.EDITOR);
                }

                // Hide the window after loading
                Hide();
                Debug.Log($"[KSPCraftManager] Loaded craft: {craft.Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to load craft {craft.Name}: {ex}");
                ShowMessageBox("Load Error", $"Failed to load craft: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles favorite status for a craft.
        /// </summary>
        private void ToggleFavorite(CraftInfo craft)
        {
            bool isFav = FavoritesManager.Instance.ToggleFavorite(craft.FilePath);
            craft.IsFavorite = isFav;
        }

        /// <summary>
        /// Exports a craft with a metadata sidecar file.
        /// </summary>
        private void ExportCraft(CraftInfo craft)
        {
            try
            {
                // Create export dialog asking for path
                _currentDialogType = DialogType.ExportCraft;
                _dialogTitle = "Export Craft";
                _dialogMessage = $"Export \"{craft.Name}\" as:";
                _dialogInput = craft.Name + "_export";
                _dialogCallback = (confirmed, input) =>
                {
                    if (confirmed && !string.IsNullOrEmpty(input))
                    {
                        DoExportCraft(craft, input);
                    }
                };
                _showDialog = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Export failed: {ex}");
                ShowMessageBox("Export Error", $"Failed to export craft: {ex.Message}");
            }
        }

        /// <summary>
        /// Performs the actual export.
        /// </summary>
        private void DoExportCraft(CraftInfo craft, string exportName)
        {
            try
            {
                string exportDir = System.IO.Path.Combine(
                    KSPCraftManager.PluginDataPath, "Exports");
                if (!System.IO.Directory.Exists(exportDir))
                    System.IO.Directory.CreateDirectory(exportDir);

                string safeName = SanitizeFileName(exportName);
                string craftPath = System.IO.Path.Combine(exportDir, safeName + ".craft");
                string metaPath = System.IO.Path.Combine(exportDir, safeName + ".meta");

                // Copy craft file
                System.IO.File.Copy(craft.FilePath, craftPath, true);

                // Write metadata JSON
                string meta = $"{{ \"name\": \"{EscapeJson(craft.Name)}\", \"type\": \"{craft.Type}\", " +
                    $"\"parts\": {craft.PartCount}, \"mass\": {craft.TotalMass}, \"cost\": {craft.TotalCost}, " +
                    $"\"mods\": [{string.Join(", ", craft.RequiredMods.Select(m => $"\"{EscapeJson(m)}\""))}], " +
                    $"\"tags\": [{string.Join(", ", craft.Tags.Select(t => $"\"{EscapeJson(t)}\""))}] }}";
                System.IO.File.WriteAllText(metaPath, meta);

                ShowMessageBox("Export Complete", $"Craft exported to:\n{craftPath}");
            }
            catch (Exception ex)
            {
                ShowMessageBox("Export Error", $"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows mod dependency check results for a craft.
        /// </summary>
        private void CheckMods(CraftInfo craft)
        {
            _depResult = ModDependencyChecker.Instance.CheckCraftDependencies(craft);
            _showDepResult = true;
        }

        /// <summary>
        /// Opens the duplicate prompt.
        /// </summary>
        private void PromptDuplicateCraft(CraftInfo craft)
        {
            _currentDialogType = DialogType.DuplicateCraft;
            _dialogTitle = "Duplicate Craft";
            _dialogMessage = $"Enter a name for the copy of \"{craft.Name}\":";
            _dialogInput = craft.Name + " (Copy)";
            _dialogCallback = (confirmed, input) =>
            {
                if (confirmed && !string.IsNullOrEmpty(input))
                {
                    if (CraftDataManager.Instance.DuplicateCraft(craft, input))
                    {
                        CraftDataManager.Instance.RefreshCraftList();
                        _needsRefresh = true;
                    }
                    else
                    {
                        ShowMessageBox("Error", $"Failed to duplicate craft. '{input}' may already exist.");
                    }
                }
            };
            _showDialog = true;
        }

        /// <summary>
        /// Opens the compare craft prompt.
        /// </summary>
        private void PromptCompareCraft(CraftInfo craft)
        {
            if (CraftComparer.Instance.IsVisible && CraftComparer.Instance.CraftA != null)
            {
                // Second craft selected
                CraftComparer.Instance.Compare(CraftComparer.Instance.CraftA, craft);
            }
            else
            {
                CraftComparer.Instance.CraftA = craft;
                CraftComparer.Instance.IsVisible = true;
                ShowMessageBox("Compare", $"\"{craft.Name}\" selected as first craft. Click 'Compare' on another craft.");
            }
        }

        /// <summary>
        /// Opens tag management for a craft.
        /// </summary>
        private void PromptManageTags(CraftInfo craft)
        {
            _currentDialogType = DialogType.DuplicateCraft; // reuse input dialog style
            _dialogTitle = "Manage Tags";
            List<string> currentTags = TagManager.Instance.GetTagsForCraft(craft.FilePath);
            _dialogMessage = $"Current tags for \"{craft.Name}\":\n{string.Join(", ", currentTags)}\n\nEnter tags (comma-separated):";
            _dialogInput = string.Join(", ", currentTags);
            _dialogCallback = (confirmed, input) =>
            {
                if (confirmed && input != null)
                {
                    List<string> newTags = input.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                    TagManager.Instance.SetTags(craft.FilePath, newTags);
                    craft.Tags = newTags;
                    _needsRefresh = true;
                }
            };
            _showDialog = true;
        }

        /// <summary>
        /// Opens delete confirmation.
        /// </summary>
        private void PromptDeleteCraft(CraftInfo craft)
        {
            _currentDialogType = DialogType.DeleteConfirm;
            _dialogTitle = "Delete Craft";
            _dialogMessage = $"Are you sure you want to delete \"{craft.Name}\"?\nThis cannot be undone.";
            _dialogInput = "";
            _dialogCallback = (confirmed, _) =>
            {
                if (confirmed)
                {
                    try
                    {
                        if (System.IO.File.Exists(craft.FilePath))
                        {
                            System.IO.File.Delete(craft.FilePath);
                            CraftDataManager.Instance.RefreshCraftList();
                            _needsRefresh = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowMessageBox("Delete Error", $"Failed to delete: {ex.Message}");
                    }
                }
            };
            _showDialog = true;
        }

        // ── Dialogs ──────────────────────────────────────────────────────

        /// <summary>
        /// Draws a simple message box overlay.
        /// </summary>
        private void ShowMessageBox(string title, string message)
        {
            _dialogTitle = title;
            _dialogMessage = message;
            _dialogInput = "";
            _dialogCallback = null;
            _showDialog = true;
            _currentDialogType = DialogType.DeleteConfirm; // just for the "OK" button
        }

        private void DrawDialog()
        {
            // Dim background
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "",
                new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0, 0, 0, 0.5f)) } });

            float dw = 400, dh = 200;
            Rect dlgRect = new Rect((Screen.width - dw) / 2, (Screen.height - dh) / 2, dw, dh);
            GUI.Box(dlgRect, "", new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f)) },
                border = new RectOffset(4, 4, 4, 4) });

            GUILayout.BeginArea(new Rect(dlgRect.x + 10, dlgRect.y + 10, dw - 20, dh - 20));

            GUILayout.Label(_dialogTitle, new GUIStyle { normal = { textColor = new Color(1f, 0.8f, 0.2f) },
                fontSize = 15, fontStyle = FontStyle.Bold });
            GUILayout.Space(8);

            GUILayout.Label(_dialogMessage, new GUIStyle { normal = { textColor = Color.white }, fontSize = 12, wordWrap = true });

            GUILayout.FlexibleSpace();

            // Input field for applicable dialog types
            bool needsInput = _currentDialogType == DialogType.DuplicateCraft ||
                              _currentDialogType == DialogType.ExportCraft ||
                              _currentDialogType == DialogType.RenameFolder ||
                              _currentDialogType == DialogType.CreateFolder;
            if (needsInput)
            {
                GUI.SetNextControlName("DialogInput");
                _dialogInput = GUILayout.TextField(_dialogInput, 100, GUILayout.Height(22));
                GUI.FocusControl("DialogInput");
            }

            GUILayout.Space(8);

            // Buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_dialogCallback != null)
            {
                if (GUILayout.Button("OK", GUILayout.Width(80), GUILayout.Height(26)))
                {
                    _dialogCallback(true, _dialogInput);
                    _showDialog = false;
                }
                GUILayout.Space(8);
            }

            if (GUILayout.Button(_dialogCallback != null ? "Cancel" : "OK", GUILayout.Width(80), GUILayout.Height(26)))
            {
                _dialogCallback?.Invoke(false, _dialogInput);
                _showDialog = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ── Folder Manager ──────────────────────────────────────────────

        private void DrawFolderManager()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "",
                new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0, 0, 0, 0.5f)) } });

            float fw = 450, fh = 350;
            Rect fmRect = new Rect((Screen.width - fw) / 2, (Screen.height - fh) / 2, fw, fh);
            GUI.Box(fmRect, "", new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f)) },
                border = new RectOffset(4, 4, 4, 4) });

            GUILayout.BeginArea(new Rect(fmRect.x + 10, fmRect.y + 10, fw - 20, fh - 20));

            GUILayout.Label("Folder Manager", new GUIStyle { normal = { textColor = new Color(1f, 0.8f, 0.2f) },
                fontSize = 15, fontStyle = FontStyle.Bold });
            GUILayout.Space(8);

            // Type selector
            GUILayout.BeginHorizontal();
            _folderType = GUILayout.Toolbar(_folderType == "VAB" ? 0 : 1, new[] { "VAB", "SPH" }, GUILayout.Height(22)) == 0
                ? "VAB" : "SPH";
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Existing folders
            GUILayout.Label("Existing folders:", new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });

            List<string> folders = CraftDataManager.Instance.GetSubfolders(_folderType);
            if (folders.Count == 0)
                GUILayout.Label("  (none)", new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });
            else
            {
                foreach (string folder in folders)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"  📁 {folder}", GUILayout.Width(250));
                    if (GUILayout.Button("Rename", GUILayout.Width(60), GUILayout.Height(20)))
                    {
                        _currentDialogType = DialogType.RenameFolder;
                        string oldName = folder;
                        _dialogTitle = "Rename Folder";
                        _dialogMessage = $"Rename \"{folder}\":";
                        _dialogInput = folder;
                        _dialogCallback = (confirmed, input) =>
                        {
                            if (confirmed && !string.IsNullOrEmpty(input))
                            {
                                CraftDataManager.Instance.RenameFolder(_folderType, oldName, input);
                                CraftDataManager.Instance.RefreshCraftList();
                                _needsRefresh = true;
                            }
                        };
                        _showDialog = true;
                    }
                    if (GUILayout.Button("Delete", GUILayout.Width(60), GUILayout.Height(20)))
                    {
                        _currentDialogType = DialogType.DeleteConfirm;
                        _dialogTitle = "Delete Folder";
                        _dialogMessage = $"Delete \"{folder}\" and all crafts inside?";
                        _dialogInput = "";
                        string delFolder = folder;
                        _dialogCallback = (confirmed, _) =>
                        {
                            if (confirmed)
                            {
                                CraftDataManager.Instance.DeleteFolder(_folderType, delFolder);
                                CraftDataManager.Instance.RefreshCraftList();
                                _needsRefresh = true;
                            }
                        };
                        _showDialog = true;
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8);

            // Create new folder
            if (GUILayout.Button("+ Create New Folder", GUILayout.Height(24)))
            {
                _currentDialogType = DialogType.CreateFolder;
                _dialogTitle = "Create Folder";
                _dialogMessage = $"Enter name for new folder in Ships/{_folderType}/:";
                _dialogInput = "";
                _dialogCallback = (confirmed, input) =>
                {
                    if (confirmed && !string.IsNullOrEmpty(input))
                    {
                        CraftDataManager.Instance.CreateFolder(_folderType, input);
                        CraftDataManager.Instance.RefreshCraftList();
                        _needsRefresh = true;
                    }
                };
                _showDialog = true;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Close", GUILayout.Height(26)))
                _showFolderManager = false;

            GUILayout.EndArea();
        }

        // ── Auth Dialog ──────────────────────────────────────────────────

        private void DrawAuthDialog()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "",
                new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0, 0, 0, 0.5f)) } });

            float aw = 400, ah = 160;
            Rect aRect = new Rect((Screen.width - aw) / 2, (Screen.height - ah) / 2, aw, ah);
            GUI.Box(aRect, "", new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f)) } });

            GUILayout.BeginArea(new Rect(aRect.x + 10, aRect.y + 10, aw - 20, ah - 20));
            GUILayout.Label("KerbalX Authentication", new GUIStyle { normal = { textColor = new Color(1f, 0.8f, 0.2f) },
                fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);
            GUILayout.Label("Enter your KerbalX API key:", new GUIStyle { normal = { textColor = Color.white } });
            GUI.SetNextControlName("AuthInput");
            string key = GUILayout.TextField(SettingsManager.Instance.KerbalXApiKey, 100, GUILayout.Height(22));
            GUI.FocusControl("AuthInput");
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save", GUILayout.Width(80), GUILayout.Height(24)))
            {
                KerbalXAPI.Instance.SetApiKey(key);
                _showAuthDialog = false;
                KerbalXBrowser.Instance.Refresh();
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(80), GUILayout.Height(24)))
                _showAuthDialog = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ── Dep Check Result Dialog ──────────────────────────────────────

        private void DrawDepResultDialog()
        {
            if (_depResult == null) return;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "",
                new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0, 0, 0, 0.5f)) } });

            float dw = 400, dh = 300;
            Rect dRect = new Rect((Screen.width - dw) / 2, (Screen.height - dh) / 2, dw, dh);
            GUI.Box(dRect, "", new GUIStyle { normal = { background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f)) } });

            GUILayout.BeginArea(new Rect(dRect.x + 10, dRect.y + 10, dw - 20, dh - 20));
            GUILayout.Label($"Mod Check: {_depResult.CraftName}",
                new GUIStyle { normal = { textColor = new Color(1f, 0.8f, 0.2f) }, fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);

            if (_depResult.IsCompatible)
            {
                GUILayout.Label("✅ All mods are installed!", new GUIStyle { normal = { textColor = new Color(0.4f, 1f, 0.4f) },
                    fontSize = 13 });
            }
            else
            {
                GUILayout.Label("⚠ Missing dependencies detected:",
                    new GUIStyle { normal = { textColor = new Color(1f, 0.6f, 0.2f) }, fontSize = 12 });
                GUILayout.Space(4);
                foreach (string missing in _depResult.MissingMods)
                {
                    GUILayout.Label($"  ❌ {missing}", new GUIStyle { normal = { textColor = new Color(1f, 0.4f, 0.4f) },
                        fontSize = 11 });
                }
                foreach (string missing in _depResult.MissingParts)
                {
                    GUILayout.Label($"  ⚠ {missing} (part)", new GUIStyle { normal = { textColor = new Color(1f, 0.7f, 0.3f) },
                        fontSize = 11 });
                }
            }

            GUILayout.Space(8);
            if (_depResult.ModStatuses.Count > 0)
            {
                GUILayout.Label("Detailed status:", new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });
                foreach (ModStatus ms in _depResult.ModStatuses)
                {
                    GUILayout.Label($"  {(ms.IsInstalled ? "✅" : "❌")} {ms.ModName}",
                        new GUIStyle { normal = { textColor = ms.IsInstalled ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f) },
                            fontSize = 11 });
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Height(26)))
                _showDepResult = false;
            GUILayout.EndArea();
        }

        // ── Core Logic ───────────────────────────────────────────────────

        /// <summary>
        /// Refreshes the displayed craft list based on current tab, filters, and sort.
        /// </summary>
        private void RefreshCraftList()
        {
            switch (_activeTab)
            {
                case 0: // Local
                    _displayedCrafts = CraftDataManager.Instance.GetFilteredCrafts(
                        searchText: _searchText,
                        craftType: string.IsNullOrEmpty(_filterType) ? null : _filterType,
                        favoritesOnly: null,
                        source: CraftSource.Local,
                        sortField: _sortField,
                        sortDirection: _sortDirection
                    );
                    break;

                case 1: // KerbalX
                    _displayedCrafts = KerbalXBrowser.Instance.RemoteCrafts
                        .Select(k => KerbalXBrowser.Instance.ToCraftInfo(k))
                        .ToList();
                    // Apply local search filter
                    if (!string.IsNullOrEmpty(_searchText))
                    {
                        string lower = _searchText.ToLowerInvariant();
                        _displayedCrafts = _displayedCrafts.Where(c =>
                            c.Name.ToLowerInvariant().Contains(lower) ||
                            c.KerbalXAuthor.ToLowerInvariant().Contains(lower)).ToList();
                    }
                    break;

                case 2: // Favorites
                    _displayedCrafts = CraftDataManager.Instance.GetFilteredCrafts(
                        searchText: _searchText,
                        craftType: string.IsNullOrEmpty(_filterType) ? null : _filterType,
                        favoritesOnly: true,
                        sortField: _sortField,
                        sortDirection: _sortDirection
                    );
                    break;

                case 3: // Search (all sources)
                    _displayedCrafts = CraftDataManager.Instance.GetFilteredCrafts(
                        searchText: _searchText,
                        craftType: string.IsNullOrEmpty(_filterType) ? null : _filterType,
                        sortField: _sortField,
                        sortDirection: _sortDirection
                    );
                    break;
            }

            // Auto-generate tags option for Local tab
            if (_activeTab == 0 && _displayedCrafts.Count > 0)
            {
                // Check if we should offer auto-tag
                // (The button is in the status bar area via the auto-tag modal)
            }
        }

        /// <summary>
        /// Triggers auto-generation of tags for all displayed crafts.
        /// </summary>
        public void AutoGenerateTagsForAll()
        {
            TagManager.Instance.AutoGenerateAllTags(_displayedCrafts);
            CraftDataManager.Instance.RefreshFavoritesAndTags();
            _needsRefresh = true;
            ShowMessageBox("Auto-Tag Complete", $"Tags generated for {_displayedCrafts.Count} crafts.");
        }

        // ── Save State ───────────────────────────────────────────────────

        /// <summary>
        /// Saves window position and active tab to settings.
        /// </summary>
        private void SaveWindowPosition()
        {
            SettingsManager.Instance.WindowX = _windowRect.x;
            SettingsManager.Instance.WindowY = _windowRect.y;
            SettingsManager.Instance.WindowWidth = _windowRect.width;
            SettingsManager.Instance.WindowHeight = _windowRect.height;
            SettingsManager.Instance.ActiveTab = _activeTab;
            SettingsManager.Instance.DefaultSortField = _sortField;
            SettingsManager.Instance.DefaultSortDirection = _sortDirection;
            SettingsManager.Instance.UseGridView = _useGridView;
            SettingsManager.Instance.Save();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private GUIStyle GetWindowStyle()
        {
            return new GUIStyle
            {
                normal = { background = MakeTexture(1, 1, new Color(0.08f, 0.08f, 0.12f, 0.95f)),
                    textColor = Color.white },
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        private GUIStyle GetListLinkStyle()
        {
            return new GUIStyle
            {
                normal = { textColor = new Color(0.5f, 0.8f, 1f) },
                hover = { textColor = new Color(0.7f, 1f, 1f) },
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 2, 2)
            };
        }

        private Texture2D MakeTexture(int w, int h, Color c)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            tex.Apply();
            return tex;
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        private string SanitizeFileName(string name)
        {
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).TrimEnd('.');
        }

        private string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        /// <summary>
        /// Opens the KerbalX authentication dialog.
        /// </summary>
        public void ShowAuthDialog()
        {
            _showAuthDialog = true;
        }

        /// <summary>
        /// Returns whether the main window is currently visible.
        /// </summary>
        public bool IsVisible => _isVisible;
    }
}
