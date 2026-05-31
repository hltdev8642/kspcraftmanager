using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Scans crafts for mod-origin parts and checks whether those mods
    /// are installed in the current KSP installation.
    /// </summary>
    public class ModDependencyChecker : IManager
    {
        private static ModDependencyChecker _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static ModDependencyChecker Instance => _instance ?? (_instance = new ModDependencyChecker());

        /// <summary>
        /// Cache of all installed mod names.
        /// </summary>
        private HashSet<string> _installedMods;

        /// <summary>
        /// Cache of all available part names in the current installation.
        /// </summary>
        private HashSet<string> _availablePartNames;

        /// <summary>
        /// Fired when the mod list is refreshed.
        /// </summary>
        public event Action OnModListRefreshed;

        private ModDependencyChecker() { }

        /// <summary>
        /// Scans the current KSP installation for installed mods and available parts.
        /// </summary>
        public void Initialize()
        {
            RefreshModList();
            Debug.Log($"[KSPCraftManager] ModDependencyChecker initialized. {_installedMods?.Count ?? 0} mods detected.");
        }

        /// <summary>
        /// Clears caches.
        /// </summary>
        public void Shutdown()
        {
            _installedMods?.Clear();
            _availablePartNames?.Clear();
        }

        /// <summary>
        /// Refreshes the list of installed mods and available parts.
        /// </summary>
        public void RefreshModList()
        {
            _installedMods = new HashSet<string>();
            _availablePartNames = new HashSet<string>();

            try
            {
                // Scan GameData directories for mods
                string gameDataPath = KSPCraftManager.ModPath;
                string gameDataRoot = Path.GetFullPath(Path.Combine(gameDataPath, ".."));

                if (System.IO.Directory.Exists(gameDataRoot))
                {
                    foreach (string dir in System.IO.Directory.GetDirectories(gameDataRoot))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (!dirName.Equals("Squad", StringComparison.OrdinalIgnoreCase) &&
                            !dirName.Equals("SquadExpansion", StringComparison.OrdinalIgnoreCase) &&
                            !dirName.StartsWith("."))
                        {
                            _installedMods.Add(dirName.ToLowerInvariant());
                        }
                    }
                }

                // Scan available parts from PartLoader
                if (PartLoader.Instance != null && PartLoader.LoadedPartsList != null)
                {
                    foreach (AvailablePart part in PartLoader.LoadedPartsList)
                    {
                        if (part != null && !string.IsNullOrEmpty(part.name))
                        {
                            _availablePartNames.Add(part.name.ToLowerInvariant());

                            // Try to determine mod origin
                            string modName = GetModNameFromPart(part);
                            if (modName != null)
                                _installedMods.Add(modName.ToLowerInvariant());
                        }
                    }
                }

                OnModListRefreshed?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to refresh mod list: {ex}");
            }
        }

        /// <summary>
        /// Checks if a specific mod is installed.
        /// </summary>
        public bool IsModInstalled(string modName)
        {
            if (string.IsNullOrEmpty(modName)) return true; // Stock parts are always "installed"
            return _installedMods.Contains(modName.ToLowerInvariant());
        }

        /// <summary>
        /// Checks if a specific part is available in the current installation.
        /// </summary>
        public bool IsPartAvailable(string partName)
        {
            if (string.IsNullOrEmpty(partName)) return true;
            return _availablePartNames.Contains(partName.ToLowerInvariant());
        }

        /// <summary>
        /// Checks all parts in a craft and returns a summary of mod dependency status.
        /// </summary>
        public ModDependencyResult CheckCraftDependencies(CraftInfo craft)
        {
            ModDependencyResult result = new ModDependencyResult
            {
                CraftName = craft.Name,
                TotalMods = craft.RequiredMods.Count
            };

            foreach (string mod in craft.RequiredMods)
            {
                bool installed = IsModInstalled(mod);
                result.ModStatuses.Add(new ModStatus
                {
                    ModName = mod,
                    IsInstalled = installed
                });

                if (!installed)
                    result.MissingMods.Add(mod);
            }

            // Also check parts directly
            foreach (string partName in craft.PartNames)
            {
                if (!IsPartAvailable(partName))
                {
                    result.MissingParts.Add(partName);
                }
            }

            result.AllModsInstalled = result.MissingMods.Count == 0;
            result.AllPartsAvailable = result.MissingParts.Count == 0;
            result.IsCompatible = result.AllModsInstalled && result.AllPartsAvailable;

            return result;
        }

        /// <summary>
        /// Attempts to determine the mod name from an AvailablePart.
        /// </summary>
        private string GetModNameFromPart(AvailablePart part)
        {
            try
            {
                // Check partConfig for mod field
                if (part.partConfig != null)
                {
                    string mod = part.partConfig.GetValue("mod");
                    if (!string.IsNullOrEmpty(mod))
                        return mod;
                }

                // Check manufacturer
                if (!string.IsNullOrEmpty(part.manufacturer))
                {
                    string mfr = part.manufacturer;
                    if (!mfr.Contains("Stock") && !mfr.Contains("Kerbal") && !mfr.Contains("Space Center"))
                        return mfr;
                }

                // Try to determine from part name convention (modname_partname)
                string partName = part.name ?? "";
                if (partName.Contains("."))
                {
                    string prefix = partName.Split('.')[0];
                    if (!IsStockModPrefix(prefix))
                        return prefix;
                }
                if (partName.Contains("_"))
                {
                    string prefix = partName.Split('_')[0];
                    if (!IsStockModPrefix(prefix))
                        return prefix;
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        /// <summary>
        /// Checks if a prefix looks like a stock mod prefix.
        /// </summary>
        private bool IsStockModPrefix(string prefix)
        {
            string lower = prefix.ToLowerInvariant();
            string[] knownStock = { "mk", "mark", "ksp", "stock", "squad", "kerbal" };
            return knownStock.Any(s => lower.StartsWith(s)) || lower.Length <= 2;
        }

        /// <summary>
        /// Gets the list of all detected installed mods.
        /// </summary>
        public List<string> GetInstalledMods()
        {
            return _installedMods?.OrderBy(m => m).ToList() ?? new List<string>();
        }

        /// <summary>
        /// Gets the count of installed mods.
        /// </summary>
        public int InstalledModCount => _installedMods?.Count ?? 0;
    }

    /// <summary>
    /// Result of a mod dependency check for a craft.
    /// </summary>
    public class ModDependencyResult
    {
        public string CraftName { get; set; }
        public int TotalMods { get; set; }
        public List<ModStatus> ModStatuses { get; set; } = new List<ModStatus>();
        public List<string> MissingMods { get; set; } = new List<string>();
        public List<string> MissingParts { get; set; } = new List<string>();
        public bool AllModsInstalled { get; set; } = true;
        public bool AllPartsAvailable { get; set; } = true;
        public bool IsCompatible { get; set; } = true;
    }

    /// <summary>
    /// Status of a single mod dependency.
    /// </summary>
    public class ModStatus
    {
        public string ModName { get; set; }
        public bool IsInstalled { get; set; }
    }
}
