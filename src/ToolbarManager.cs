using System;
using System.IO;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;

namespace KSPCraftManager
{
    /// <summary>
    /// Manages the stock ApplicationLauncher toolbar button and optional Blizzy's Toolbar support.
    /// Fires events when the button is toggled.
    /// </summary>
    public class ToolbarManager : IManager
    {
        private static ToolbarManager _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static ToolbarManager Instance => _instance ?? (_instance = new ToolbarManager());

        /// <summary>
        /// Fired when the toolbar button is clicked.
        /// </summary>
        public event Action OnToolbarToggle;

        private ApplicationLauncherButton _stockButton;
        private Texture2D _toolbarIcon;
        private bool _isReady;

        // Blizzy's Toolbar fields (reflection-based to avoid hard dependency)
        private Type _toolbarTypesType;
        private Type _iToolbarType;
        private Type _iButtonType;
        private object _toolbarInstance;
        private object _blizzyButton;
        private bool _blizzyAvailable;

        private const string ToolbarTypesFullName = "Toolbar.ToolbarTypes";
        private const string IToolbarFullName = "Toolbar.IToolbar";
        private const string IButtonFullName = "Toolbar.IButton";

        private ToolbarManager() { }

        /// <summary>
        /// Loads the toolbar icon texture and creates the toolbar button(s).
        /// </summary>
        public void Initialize()
        {
            LoadIcon();
            DetectBlizzyToolbar();
            GameEvents.onGUIApplicationLauncherReady.Add(CreateStockButton);
            Debug.Log("[KSPCraftManager] ToolbarManager initialized.");
        }

        /// <summary>
        /// Removes toolbar buttons and cleans up.
        /// </summary>
        public void Shutdown()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(CreateStockButton);
            RemoveStockButton();
            RemoveBlizzyButton();
        }

        // ── Stock Toolbar ────────────────────────────────────────────────

        /// <summary>
        /// Creates the stock ApplicationLauncher button.
        /// Called when the application launcher is ready.
        /// </summary>
        private void CreateStockButton()
        {
            if (_stockButton != null || _toolbarIcon == null) return;

            try
            {
                _stockButton = ApplicationLauncher.Instance.AddModApplication(
                    OnStockToggleOn,
                    OnStockToggleOff,
                    null, null, null, null,
                    ApplicationLauncher.AppScenes.ALWAYS,
                    _toolbarIcon
                );
                _isReady = true;
                Debug.Log("[KSPCraftManager] Stock toolbar button created.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to create stock toolbar button: {ex}");
            }
        }

        /// <summary>
        /// Removes the stock toolbar button.
        /// </summary>
        private void RemoveStockButton()
        {
            if (_stockButton != null && ApplicationLauncher.Instance != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(_stockButton);
                _stockButton = null;
            }
        }

        private void OnStockToggleOn()
        {
            OnToolbarToggle?.Invoke();
        }

        private void OnStockToggleOff()
        {
            OnToolbarToggle?.Invoke();
        }

        /// <summary>
        /// Sets the stock button state without firing events.
        /// </summary>
        public void SetStockButtonState(bool trueIfOn)
        {
            if (_stockButton != null)
            {
                _stockButton.SetTrue(false);
                _stockButton.SetFalse(false);
                if (trueIfOn)
                    _stockButton.SetTrue(true);
                else
                    _stockButton.SetFalse(true);
            }
        }

        // ── Blizzy's Toolbar ─────────────────────────────────────────────

        /// <summary>
        /// Attempts to detect and integrate Blizzy's Toolbar via reflection.
        /// No hard dependency — silently fails if toolbar is absent.
        /// </summary>
        private void DetectBlizzyToolbar()
        {
            try
            {
                _toolbarTypesType = AssemblyLoader.loadedAssemblies
                    .FirstOrDefault(a => a.name == "Toolbar")
                    ?.assembly?.GetType(ToolbarTypesFullName);

                if (_toolbarTypesType == null)
                {
                    Debug.Log("[KSPCraftManager] Blizzy's Toolbar not detected.");
                    return;
                }

                _iToolbarType = AssemblyLoader.loadedAssemblies
                    .FirstOrDefault(a => a.name == "Toolbar")
                    ?.assembly?.GetType(IToolbarFullName);

                _iButtonType = AssemblyLoader.loadedAssemblies
                    .FirstOrDefault(a => a.name == "Toolbar")
                    ?.assembly?.GetType(IButtonFullName);

                if (_iToolbarType == null || _iButtonType == null)
                {
                    Debug.Log("[KSPCraftManager] Blizzy's Toolbar types not found.");
                    return;
                }

                // Get the Toolbar.Toolbar.Instance singleton
                var instanceProperty = _toolbarTypesType.GetProperty("Instance");
                if (instanceProperty == null)
                {
                    Debug.Log("[KSPCraftManager] Blizzy's Toolbar has no Instance property.");
                    return;
                }

                _toolbarInstance = instanceProperty.GetValue(null, null);
                if (_toolbarInstance == null)
                {
                    Debug.Log("[KSPCraftManager] Blizzy's Toolbar instance is null.");
                    return;
                }

                // Create button via IButton.Add()
                var addMethod = _iToolbarType.GetMethod("Add");
                if (addMethod == null)
                {
                    Debug.Log("[KSPCraftManager] Blizzy's Toolbar has no Add method.");
                    return;
                }

                _blizzyButton = addMethod.Invoke(_toolbarInstance, new object[] {
                    "KSPCraftManager",  // namespace
                    "mainButton",       // id
                    "KSP Craft Manager" // title
                });

                if (_blizzyButton != null)
                {
                    // Set texture
                    var textureProp = _iButtonType.GetProperty("TexturePath");
                    if (textureProp != null)
                    {
                        // Save icon to Textures folder for Blizzy to pick up
                        string blizzyPath = Path.Combine(KSPCraftManager.ModPath, "Textures", "toolbar_icon_blizzy");
                        textureProp.SetValue(_blizzyButton, "KSPCraftManager/Textures/toolbar_icon_blizzy", null);
                    }

                    // Set tooltip
                    var tooltipProp = _iButtonType.GetProperty("ToolTip");
                    if (tooltipProp != null)
                        tooltipProp.SetValue(_blizzyButton, "KSP Craft Manager", null);

                    // Set visibility
                    var visibleProp = _iButtonType.GetProperty("Visible");
                    if (visibleProp != null)
                        visibleProp.SetValue(_blizzyButton, true, null);

                    // Hook click event
                    var onToggleEvent = _iButtonType.GetEvent("OnClick");
                    if (onToggleEvent != null)
                    {
                        // Create a delegate pointing to our handler
                        var handlerType = onToggleEvent.EventHandlerType;
                        var handlerDelegate = Delegate.CreateDelegate(handlerType, this, nameof(OnBlizzyButtonClick));
                        onToggleEvent.AddEventHandler(_blizzyButton, handlerDelegate);
                    }

                    _blizzyAvailable = true;
                    Debug.Log("[KSPCraftManager] Blizzy's Toolbar button created.");
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[KSPCraftManager] Blizzy's Toolbar integration unavailable: {ex.Message}");
                _blizzyAvailable = false;
            }
        }

        /// <summary>
        /// Called when Blizzy's Toolbar button is clicked.
        /// </summary>
        private void OnBlizzyButtonClick(object sender, EventArgs e)
        {
            OnToolbarToggle?.Invoke();
        }

        /// <summary>
        /// Removes the Blizzy's Toolbar button.
        /// </summary>
        private void RemoveBlizzyButton()
        {
            if (_blizzyButton != null && _iToolbarType != null)
            {
                try
                {
                    var removeMethod = _iToolbarType.GetMethod("Remove");
                    removeMethod?.Invoke(_toolbarInstance, new[] { _blizzyButton });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[KSPCraftManager] Error removing Blizzy button: {ex}");
                }
                _blizzyButton = null;
            }
        }

        // ── Icon Loading ─────────────────────────────────────────────────

        /// <summary>
        /// Loads the toolbar icon from the Textures folder.
        /// Falls back to a generated texture if the file is missing.
        /// </summary>
        private void LoadIcon()
        {
            string iconPath = Path.Combine(KSPCraftManager.ModPath, "Textures", "toolbar_icon.png");
            if (File.Exists(iconPath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(iconPath);
                    _toolbarIcon = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                    _toolbarIcon.LoadImage(bytes);
                    Debug.Log("[KSPCraftManager] Toolbar icon loaded from file.");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[KSPCraftManager] Failed to load toolbar icon: {ex}");
                }
            }

            // Generate a fallback icon (simple colored square with "CM" text)
            _toolbarIcon = GenerateFallbackIcon();
            Debug.Log("[KSPCraftManager] Using fallback toolbar icon.");
        }

        /// <summary>
        /// Generates a simple fallback toolbar icon.
        /// </summary>
        private Texture2D GenerateFallbackIcon()
        {
            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    bool border = x == 0 || x == 31 || y == 0 || y == 31;
                    bool crossHair = (x >= 14 && x <= 17 && y >= 6 && y <= 25) ||
                                     (x >= 6 && x <= 25 && y >= 14 && y <= 17);
                    if (border)
                        tex.SetPixel(x, y, new Color(0.8f, 0.6f, 0.2f));
                    else if (crossHair)
                        tex.SetPixel(x, y, new Color(1f, 0.85f, 0.3f));
                    else
                        tex.SetPixel(x, y, new Color(0.15f, 0.15f, 0.15f, 0f));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
