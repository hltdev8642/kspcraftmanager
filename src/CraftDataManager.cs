using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Represents a parsed craft with extracted metadata.
    /// </summary>
    public class CraftInfo
    {
        /// <summary>Full path to the .craft file.</summary>
        public string FilePath { get; set; }

        /// <summary>Craft name (from the "ship" field).</summary>
        public string Name { get; set; }

        /// <summary>Craft description.</summary>
        public string Description { get; set; }

        /// <summary>VAB or SPH.</summary>
        public string Type { get; set; } = "VAB";

        /// <summary>KSP version the craft was made in.</summary>
        public string KspVersion { get; set; }

        /// <summary>File last write time.</summary>
        public DateTime LastModified { get; set; }

        /// <summary>File creation time.</summary>
        public DateTime Created { get; set; }

        /// <summary>File size in bytes.</summary>
        public long FileSize { get; set; }

        /// <summary>Total part count.</summary>
        public int PartCount { get; set; }

        /// <summary>Total vessel mass in tonnes (wet).</summary>
        public double TotalMass { get; set; }

        /// <summary>Dry mass in tonnes (less fuel).</summary>
        public double DryMass { get; set; }

        /// <summary>Total vessel cost in funds.</summary>
        public double TotalCost { get; set; }

        /// <summary>Craft dimensions: width, height, length.</summary>
        public Vector3 Size { get; set; }

        /// <summary>Number of crew the craft can carry.</summary>
        public int CrewCapacity { get; set; }

        /// <summary>List of part names used in the craft.</summary>
        public List<string> PartNames { get; set; } = new List<string>();

        /// <summary>List of mods required (part name prefixes indicating mod origin).</summary>
        public List<string> RequiredMods { get; set; } = new List<string>();

        /// <summary>Parent subfolder name under Ships/VAB or Ships/SPH.</summary>
        public string Subfolder { get; set; } = string.Empty;

        /// <summary>Whether this craft has been favorited.</summary>
        public bool IsFavorite { get; set; }

        /// <summary>Tags assigned to this craft.</summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>Relative path from Ships/{type}/ for display.</summary>
        public string RelativePath => string.IsNullOrEmpty(Subfolder) ? Name : Subfolder + "/" + Name;

        /// <summary>Source indicator: Local or KerbalX.</summary>
        public CraftSource Source { get; set; } = CraftSource.Local;

        /// <summary>KerbalX craft ID (if from KerbalX).</summary>
        public string KerbalXId { get; set; } = string.Empty;

        /// <summary>KerbalX author name.</summary>
        public string KerbalXAuthor { get; set; } = string.Empty;

        /// <summary>KerbalX download count.</summary>
        public int KerbalXDownloads { get; set; }

        /// <summary>KerbalX like count.</summary>
        public int KerbalXLikes { get; set; }

        /// <summary>KerbalX rating.</summary>
        public float KerbalXRating { get; set; }

        /// <summary>KerbalX last updated date.</summary>
        public string KerbalXUpdated { get; set; } = string.Empty;
    }

    /// <summary>
    /// Source of a craft listing.
    /// </summary>
    public enum CraftSource
    {
        Local,
        KerbalX
    }

    /// <summary>
    /// Manages local craft file scanning, metadata extraction, folder operations,
    /// recently-opened tracking, and provides filtered/sorted craft lists.
    /// </summary>
    public class CraftDataManager : IManager
    {
        private static CraftDataManager _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static CraftDataManager Instance => _instance ?? (_instance = new CraftDataManager());

        /// <summary>
        /// All loaded local craft entries.
        /// </summary>
        public List<CraftInfo> AllCrafts { get; } = new List<CraftInfo>();

        /// <summary>
        /// Recently opened crafts (most recent first).
        /// </summary>
        public List<CraftInfo> RecentlyOpened { get; } = new List<CraftInfo>();

        private const int MaxRecentItems = 20;
        private const string RecentFilePath = "KSPCraftManagerRecent.cfg";
        private FileSystemWatcher _vabWatcher;
        private FileSystemWatcher _sphWatcher;
        private float _nextWatcherRefresh;
        private const float WatcherDebounceSeconds = 2f;

        // Known stock part name patterns to exclude from mod detection
        private static readonly HashSet<string> StockModPrefixes = new HashSet<string>
        {
            "mk1", "mk2", "mk3", "mark1", "mark2", "mark3",
            "fuelTank", "RCS", "struts", "connector",
            "small", "large", "medium", "adapter",
            "engine", "turbo", "jet", "rocket",
            "landing", "parachute", "decal", "flag",
            "light", "ladder", "battery", "solar",
            "probe", "command", "pod", "cockpit",
            "wheel", "gear", "wing", "fin",
            "noseCone", "fairing", "heatShield",
            "science", "antenna", "sensor", "bay",
            "coupler", "decoupler", "hitch", "docking",
            "structural", "truss", "girder", "panel",
            "radial", "stack", "inline", "mount"
        };

        private CraftDataManager() { }

        /// <summary>
        /// Scans the Ships directories and loads all craft metadata.
        /// </summary>
        public void Initialize()
        {
            RefreshCraftList();
            SetupFileWatchers();
            LoadRecentlyOpened();
            Debug.Log("[KSPCraftManager] CraftDataManager initialized.");
        }

        /// <summary>
        /// Stops file watchers and saves state.
        /// </summary>
        public void Shutdown()
        {
            StopFileWatchers();
            SaveRecentlyOpened();
        }

        // ── Craft Scanning ───────────────────────────────────────────────

        /// <summary>
        /// Rescans the Ships/VAB and Ships/SPH directories for .craft files.
        /// Returns a reference to the internal list.
        /// </summary>
        public List<CraftInfo> RefreshCraftList()
        {
            AllCrafts.Clear();

            // Determine KSP root by walking up from GameData
            string gameDataPath = KSPCraftManager.ModPath;
            string kspRoot = Path.GetFullPath(Path.Combine(gameDataPath, "..", ".."));

            string shipsVab = Path.Combine(kspRoot, "Ships", "VAB");
            string shipsSph = Path.Combine(kspRoot, "Ships", "SPH");

            if (Directory.Exists(shipsVab))
                ScanDirectory(shipsVab, "VAB", AllCrafts);
            else
                Debug.LogWarning($"[KSPCraftManager] Ships/VAB directory not found: {shipsVab}");

            if (Directory.Exists(shipsSph))
                ScanDirectory(shipsSph, "SPH", AllCrafts);
            else
                Debug.LogWarning($"[KSPCraftManager] Ships/SPH directory not found: {shipsSph}");

            // Re-apply favorites and tags
            ApplyFavoritesAndTags();

            Debug.Log($"[KSPCraftManager] Found {AllCrafts.Count} local crafts.");
            return AllCrafts;
        }

        /// <summary>
        /// Recursively scans a directory for .craft files and parses them.
        /// </summary>
        private void ScanDirectory(string directory, string craftType, List<CraftInfo> results)
        {
            try
            {
                foreach (string file in Directory.GetFiles(directory, "*.craft", SearchOption.AllDirectories))
                {
                    try
                    {
                        CraftInfo info = ParseCraftFile(file, craftType);
                        if (info != null)
                            results.Add(info);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[KSPCraftManager] Failed to parse {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Error scanning {directory}: {ex}");
            }
        }

        /// <summary>
        /// Parses a .craft file and extracts metadata.
        /// Uses KSP's ConfigNode parser for reliable reading.
        /// </summary>
        private CraftInfo ParseCraftFile(string filePath, string defaultType)
        {
            if (!File.Exists(filePath))
                return null;

            FileInfo fi = new FileInfo(filePath);
            CraftInfo info = new CraftInfo
            {
                FilePath = filePath,
                Name = Path.GetFileNameWithoutExtension(filePath),
                LastModified = fi.LastWriteTime,
                Created = fi.CreationTime,
                FileSize = fi.Length,
                Type = defaultType,
                Source = CraftSource.Local
            };

            // Determine subfolder relative to Ships/{type}/
            string shipsDir = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(filePath), ".."));
            string subDir = Path.GetDirectoryName(filePath);
            if (subDir != null && subDir.Length > shipsDir.Length)
            {
                info.Subfolder = subDir.Substring(shipsDir.Length + 1);
            }

            try
            {
                // Use KSP's ConfigNode parser
                ConfigNode craftNode = ConfigNode.Load(filePath);
                if (craftNode == null)
                {
                    // Fallback: manual parse if ConfigNode fails
                    ParseCraftFileManual(filePath, info);
                    return info;
                }

                // Extract ship name from the root node value
                string shipName = craftNode.GetValue("ship");
                if (!string.IsNullOrEmpty(shipName))
                    info.Name = shipName;

                info.Description = craftNode.GetValue("description") ?? string.Empty;
                info.KspVersion = craftNode.GetValue("version") ?? "Unknown";

                string typeStr = craftNode.GetValue("type");
                if (!string.IsNullOrEmpty(typeStr))
                    info.Type = typeStr;

                // Parse size
                string sizeStr = craftNode.GetValue("size");
                if (!string.IsNullOrEmpty(sizeStr))
                {
                    string[] parts = sizeStr.Split(',');
                    if (parts.Length >= 3 &&
                        float.TryParse(parts[0].Trim(), out float sw) &&
                        float.TryParse(parts[1].Trim(), out float sh) &&
                        float.TryParse(parts[2].Trim(), out float sl))
                    {
                        info.Size = new Vector3(sw, sh, sl);
                    }
                }

                // Parse parts
                ConfigNode[] partNodes = craftNode.GetNodes("PART");
                int partCount = partNodes.Length;

                // Also count any sub-nodes that might hold parts (e.g., in FAR or other mod contexts)
                if (partCount == 0)
                {
                    // Try recursive search
                    partCount = CountPartsRecursive(craftNode);
                }

                info.PartCount = partCount;

                // Extract part names and detect mods
                foreach (ConfigNode partNode in partNodes)
                {
                    string partName = partNode.GetValue("part");
                    if (string.IsNullOrEmpty(partName)) continue;

                    // Strip the trailing underscore + id (e.g., "mk1pod_12345" -> "mk1pod")
                    int underscoreIdx = partName.LastIndexOf('_');
                    string basePartName = underscoreIdx > 0 ? partName.Substring(0, underscoreIdx) : partName;
                    info.PartNames.Add(basePartName);

                    // Detect mod origin
                    string modName = DetectModFromPart(basePartName);
                    if (modName != null && !info.RequiredMods.Contains(modName))
                        info.RequiredMods.Add(modName);
                }

                // Calculate mass and cost from part configurations
                CalculateMassAndCost(info);

                // Count crew capacity
                info.CrewCapacity = CalculateCrewCapacity(partNodes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] ConfigNode parse failed for {filePath}: {ex.Message}. Trying manual parse.");
                ParseCraftFileManual(filePath, info);
            }

            return info;
        }

        /// <summary>
        /// Fallback manual parser for .craft files when ConfigNode.Load fails.
        /// </summary>
        private void ParseCraftFileManual(string filePath, CraftInfo info)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                int partCount = 0;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();

                    if (trimmed.StartsWith("ship ="))
                        info.Name = trimmed.Substring(6).Trim().Trim('"');

                    else if (trimmed.StartsWith("description ="))
                        info.Description = trimmed.Substring(13).Trim().Trim('"');

                    else if (trimmed.StartsWith("type ="))
                        info.Type = trimmed.Substring(6).Trim().Trim('"');

                    else if (trimmed.StartsWith("version ="))
                        info.KspVersion = trimmed.Substring(9).Trim().Trim('"');

                    else if (trimmed.StartsWith("size ="))
                    {
                        string sizeVal = trimmed.Substring(6).Trim();
                        string[] parts = sizeVal.Split(',');
                        if (parts.Length >= 3 &&
                            float.TryParse(parts[0].Trim(), out float sw) &&
                            float.TryParse(parts[1].Trim(), out float sh) &&
                            float.TryParse(parts[2].Trim(), out float sl))
                        {
                            info.Size = new Vector3(sw, sh, sl);
                        }
                    }
                    else if (trimmed.StartsWith("part ="))
                    {
                        partCount++;
                        string partVal = trimmed.Substring(6).Trim().Trim('"');
                        int underscoreIdx = partVal.LastIndexOf('_');
                        string basePartName = underscoreIdx > 0 ? partVal.Substring(0, underscoreIdx) : partVal;
                        info.PartNames.Add(basePartName);

                        string modName = DetectModFromPart(basePartName);
                        if (modName != null && !info.RequiredMods.Contains(modName))
                            info.RequiredMods.Add(modName);
                    }
                }

                info.PartCount = partCount;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Manual parse failed for {filePath}: {ex}");
                info.PartCount = 0;
            }
        }

        /// <summary>
        /// Counts PART nodes recursively in a ConfigNode tree.
        /// </summary>
        private int CountPartsRecursive(ConfigNode node)
        {
            int count = node.GetNodes("PART").Length;
            foreach (ConfigNode child in node.GetNodes())
            {
                count += CountPartsRecursive(child);
            }
            return count;
        }

        /// <summary>
        /// Detects the mod a part belongs to by checking known mod name prefixes.
        /// Uses the loaded part database from KSP.
        /// </summary>
        private string DetectModFromPart(string partName)
        {
            // Check the stock prefixes first
            string lowerPart = partName.ToLowerInvariant();
            foreach (string prefix in StockModPrefixes)
            {
                if (lowerPart.StartsWith(prefix))
                    return null; // Stock part
            }

            // Try to find the part in KSP's PartLoader
            if (PartLoader.Instance != null)
            {
                AvailablePart availablePart = PartLoader.getPartInfoByName(partName);
                if (availablePart != null && availablePart.partConfig != null)
                {
                    // Check for MODULE or PARTUPGRADE that indicates mod origin
                    string mod = availablePart.partConfig.GetValue("mod");
                    if (!string.IsNullOrEmpty(mod))
                        return mod;
                }
            }

            // Heuristic: check if part name contains underscores or dots indicating mod prefix
            // Common KSP mod naming: modname.partname or modname_partname
            if (lowerPart.Contains("."))
            {
                string mod = lowerPart.Split('.')[0];
                return CultureFirstCharUpper(mod);
            }

            return null; // Assume stock if we can't determine
        }

        /// <summary>
        /// Capitalizes the first character of a string.
        /// </summary>
        private string CultureFirstCharUpper(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>
        /// Calculates mass and cost for a craft by summing part configurations.
        /// Uses KSP's PartLoader for accurate values.
        /// </summary>
        private void CalculateMassAndCost(CraftInfo info)
        {
            double totalMass = 0;
            double totalCost = 0;

            foreach (string partName in info.PartNames)
            {
                try
                {
                    AvailablePart availablePart = PartLoader.getPartInfoByName(partName);
                    if (availablePart != null)
                    {
                        totalMass += availablePart.partPrefab.mass;
                        totalCost += availablePart.cost;
                    }
                    else
                    {
                        // Estimate from name heuristics
                        totalMass += 0.1;
                        totalCost += 100;
                    }
                }
                catch
                {
                    totalMass += 0.1;
                    totalCost += 100;
                }
            }

            info.TotalMass = Math.Round(totalMass, 3);
            info.TotalCost = Math.Round(totalCost, 1);

            // Estimate dry mass as 60% of total (rough approximation — fuel is ~40% of mass for many rockets)
            info.DryMass = Math.Round(totalMass * 0.6, 3);
        }

        /// <summary>
        /// Calculates total crew capacity across all parts.
        /// </summary>
        private int CalculateCrewCapacity(ConfigNode[] partNodes)
        {
            int capacity = 0;
            foreach (ConfigNode partNode in partNodes)
            {
                string partName = partNode.GetValue("part");
                if (string.IsNullOrEmpty(partName)) continue;

                int underscoreIdx = partName.LastIndexOf('_');
                string baseName = underscoreIdx > 0 ? partName.Substring(0, underscoreIdx) : partName;

                AvailablePart availablePart = PartLoader.getPartInfoByName(baseName);
                if (availablePart?.partPrefab != null)
                {
                    PartModule[] modules = availablePart.partPrefab.GetComponents<PartModule>();
                    for (int i = 0; i < modules.Length; i++)
                    {
                        PartModule pm = modules[i];
                        if (pm.moduleName == "ModuleCrewPart")
                        {
                            var crewCapField = pm.Fields["crewCapacity"];
                            if (crewCapField != null)
                            {
                                try { capacity += Convert.ToInt32(crewCapField.GetValue(pm)); } catch { }
                            }
                        }
                    }
                }
            }
            return capacity;
        }

        // ── Favorites and Tags ───────────────────────────────────────────

        /// <summary>
        /// Re-applies favorite and tag data from FavoritesManager and TagManager.
        /// </summary>
        private void ApplyFavoritesAndTags()
        {
            foreach (CraftInfo craft in AllCrafts)
            {
                craft.IsFavorite = FavoritesManager.Instance.IsFavorite(craft.FilePath);
                craft.Tags = new List<string>(TagManager.Instance.GetTagsForCraft(craft.FilePath));
            }
        }

        /// <summary>
        /// Forces re-application of favorite/tag data to all crafts.
        /// Call after favorites or tags change.
        /// </summary>
        public void RefreshFavoritesAndTags()
        {
            ApplyFavoritesAndTags();
        }

        // ── Folder Management ────────────────────────────────────────────

        /// <summary>
        /// Creates a new subfolder in the specified Ships directory.
        /// </summary>
        public bool CreateFolder(string craftType, string folderName)
        {
            try
            {
                string kspRoot = GetKspRoot();
                string path = Path.Combine(kspRoot, "Ships", craftType, folderName);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to create folder: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Renames a subfolder in the specified Ships directory.
        /// </summary>
        public bool RenameFolder(string craftType, string oldName, string newName)
        {
            try
            {
                string kspRoot = GetKspRoot();
                string oldPath = Path.Combine(kspRoot, "Ships", craftType, oldName);
                string newPath = Path.Combine(kspRoot, "Ships", craftType, newName);
                if (Directory.Exists(oldPath) && !Directory.Exists(newPath))
                {
                    Directory.Move(oldPath, newPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to rename folder: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a subfolder and all crafts within it.
        /// </summary>
        public bool DeleteFolder(string craftType, string folderName)
        {
            try
            {
                string kspRoot = GetKspRoot();
                string path = Path.Combine(kspRoot, "Ships", craftType, folderName);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to delete folder: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Moves a craft file to a different subfolder.
        /// </summary>
        public bool MoveCraft(CraftInfo craft, string targetType, string targetSubfolder)
        {
            try
            {
                string kspRoot = GetKspRoot();
                string targetDir = Path.Combine(kspRoot, "Ships", targetType, targetSubfolder);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                string destPath = Path.Combine(targetDir, Path.GetFileName(craft.FilePath));
                if (File.Exists(destPath))
                    return false;

                File.Move(craft.FilePath, destPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to move craft: {ex}");
                return false;
            }
        }

        // ── Recently Opened ──────────────────────────────────────────────

        /// <summary>
        /// Records a craft as recently opened.
        /// </summary>
        public void RecordRecentlyOpened(CraftInfo craft)
        {
            RecentlyOpened.RemoveAll(c => c.FilePath == craft.FilePath);
            RecentlyOpened.Insert(0, craft);
            if (RecentlyOpened.Count > MaxRecentItems)
                RecentlyOpened.RemoveAt(RecentlyOpened.Count - 1);
            SaveRecentlyOpened();
        }

        /// <summary>
        /// Saves the recently-opened list to a config file.
        /// </summary>
        private void SaveRecentlyOpened()
        {
            try
            {
                ConfigNode node = new ConfigNode("RECENTLY_OPENED");
                foreach (CraftInfo craft in RecentlyOpened)
                {
                    ConfigNode entry = node.AddNode("CRAFT");
                    entry.AddValue("path", craft.FilePath);
                }
                string path = Path.Combine(KSPCraftManager.PluginDataPath, RecentFilePath);
                node.Save(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to save recently opened: {ex}");
            }
        }

        /// <summary>
        /// Loads the recently-opened list from a config file.
        /// </summary>
        private void LoadRecentlyOpened()
        {
            try
            {
                string path = Path.Combine(KSPCraftManager.PluginDataPath, RecentFilePath);
                if (!File.Exists(path)) return;

                ConfigNode node = ConfigNode.Load(path);
                if (node == null) return;

                foreach (ConfigNode entry in node.GetNodes("CRAFT"))
                {
                    string craftPath = entry.GetValue("path");
                    if (!string.IsNullOrEmpty(craftPath) && File.Exists(craftPath))
                    {
                        CraftInfo info = AllCrafts.Find(c => c.FilePath == craftPath);
                        if (info != null && !RecentlyOpened.Contains(info))
                            RecentlyOpened.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to load recently opened: {ex}");
            }
        }

        // ── Search & Filter ──────────────────────────────────────────────

        /// <summary>
        /// Filters and sorts the craft list based on the provided criteria.
        /// </summary>
        public List<CraftInfo> GetFilteredCrafts(
            string searchText = null,
            string craftType = null,
            List<string> tags = null,
            bool? favoritesOnly = null,
            CraftSource? source = null,
            float? minMass = null,
            float? maxMass = null,
            float? minCost = null,
            float? maxCost = null,
            int? minParts = null,
            int? maxParts = null,
            string sortField = "name",
            string sortDirection = "asc")
        {
            IEnumerable<CraftInfo> query = AllCrafts;

            // Source filter
            if (source.HasValue)
                query = query.Where(c => c.Source == source.Value);

            // Type filter (VAB/SPH)
            if (!string.IsNullOrEmpty(craftType))
                query = query.Where(c => c.Type.Equals(craftType, StringComparison.OrdinalIgnoreCase));

            // Favorites filter
            if (favoritesOnly == true)
                query = query.Where(c => c.IsFavorite);

            // Text search
            if (!string.IsNullOrEmpty(searchText))
            {
                string lower = searchText.ToLowerInvariant();
                query = query.Where(c =>
                    c.Name.ToLowerInvariant().Contains(lower) ||
                    (c.Description ?? "").ToLowerInvariant().Contains(lower) ||
                    c.PartNames.Any(p => p.ToLowerInvariant().Contains(lower)) ||
                    c.RequiredMods.Any(m => m.ToLowerInvariant().Contains(lower)) ||
                    c.KerbalXAuthor.ToLowerInvariant().Contains(lower)
                );
            }

            // Tag filter
            if (tags != null && tags.Count > 0)
            {
                query = query.Where(c => tags.All(t => c.Tags.Contains(t)));
            }

            // Mass filter
            if (minMass.HasValue)
                query = query.Where(c => c.TotalMass >= minMass.Value);
            if (maxMass.HasValue)
                query = query.Where(c => c.TotalMass <= maxMass.Value);

            // Cost filter
            if (minCost.HasValue)
                query = query.Where(c => c.TotalCost >= minCost.Value);
            if (maxCost.HasValue)
                query = query.Where(c => c.TotalCost <= maxCost.Value);

            // Part count filter
            if (minParts.HasValue)
                query = query.Where(c => c.PartCount >= minParts.Value);
            if (maxParts.HasValue)
                query = query.Where(c => c.PartCount <= maxParts.Value);

            // Sorting
            bool ascending = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);
            switch (sortField.ToLowerInvariant())
            {
                case "name":
                    query = ascending ? query.OrderBy(c => c.Name) : query.OrderByDescending(c => c.Name);
                    break;
                case "date":
                case "modified":
                case "lastmodified":
                    query = ascending ? query.OrderBy(c => c.LastModified) : query.OrderByDescending(c => c.LastModified);
                    break;
                case "partcount":
                case "parts":
                    query = ascending ? query.OrderBy(c => c.PartCount) : query.OrderByDescending(c => c.PartCount);
                    break;
                case "mass":
                    query = ascending ? query.OrderBy(c => c.TotalMass) : query.OrderByDescending(c => c.TotalMass);
                    break;
                case "cost":
                    query = ascending ? query.OrderBy(c => c.TotalCost) : query.OrderByDescending(c => c.TotalCost);
                    break;
                case "size":
                    query = ascending ? query.OrderBy(c => c.Size.magnitude) : query.OrderByDescending(c => c.Size.magnitude);
                    break;
                case "popularity":
                case "downloads":
                    query = ascending ? query.OrderBy(c => c.KerbalXDownloads) : query.OrderByDescending(c => c.KerbalXDownloads);
                    break;
                default:
                    query = query.OrderBy(c => c.Name);
                    break;
            }

            return query.ToList();
        }

        /// <summary>
        /// Gets the list of subfolders for a given craft type.
        /// </summary>
        public List<string> GetSubfolders(string craftType)
        {
            HashSet<string> folders = new HashSet<string>();
            foreach (CraftInfo craft in AllCrafts)
            {
                if (craft.Type.Equals(craftType, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(craft.Subfolder))
                {
                    folders.Add(craft.Subfolder);
                }
            }
            return folders.OrderBy(f => f).ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Gets the KSP root directory by walking up from GameData.
        /// </summary>
        private string GetKspRoot()
        {
            string modPath = KSPCraftManager.ModPath;
            // modPath is GameData/KSPCraftManager, go up 2 levels
            string gameData = Path.GetDirectoryName(modPath);
            return Path.GetDirectoryName(gameData) ?? modPath;
        }

        /// <summary>
        /// Duplicates a craft with a new name in the same directory.
        /// </summary>
        public bool DuplicateCraft(CraftInfo source, string newName)
        {
            try
            {
                string dir = Path.GetDirectoryName(source.FilePath);
                string ext = Path.GetExtension(source.FilePath);
                string destPath = Path.Combine(dir ?? "", newName + ext);

                if (File.Exists(destPath))
                    return false;

                File.Copy(source.FilePath, destPath);

                // Update the ship name inside the new file
                string[] lines = File.ReadAllLines(destPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("ship ="))
                    {
                        lines[i] = "ship = " + newName;
                        break;
                    }
                }
                File.WriteAllLines(destPath, lines);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to duplicate craft: {ex}");
                return false;
            }
        }

        // ── File System Watchers ─────────────────────────────────────────

        /// <summary>
        /// Sets up FileSystemWatchers to detect changes to Ships directories.
        /// </summary>
        private void SetupFileWatchers()
        {
            try
            {
                string kspRoot = GetKspRoot();
                string vabPath = Path.Combine(kspRoot, "Ships", "VAB");
                string sphPath = Path.Combine(kspRoot, "Ships", "SPH");

                if (Directory.Exists(vabPath))
                {
                    _vabWatcher = new FileSystemWatcher(vabPath, "*.craft")
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
                    };
                    _vabWatcher.Changed += OnCraftFileChanged;
                    _vabWatcher.Created += OnCraftFileChanged;
                    _vabWatcher.Deleted += OnCraftFileChanged;
                    _vabWatcher.Renamed += OnCraftFileRenamed;
                }

                if (Directory.Exists(sphPath))
                {
                    _sphWatcher = new FileSystemWatcher(sphPath, "*.craft")
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
                    };
                    _sphWatcher.Changed += OnCraftFileChanged;
                    _sphWatcher.Created += OnCraftFileChanged;
                    _sphWatcher.Deleted += OnCraftFileChanged;
                    _sphWatcher.Renamed += OnCraftFileRenamed;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to set up file watchers: {ex}");
            }
        }

        /// <summary>
        /// Stops and disposes file watchers.
        /// </summary>
        private void StopFileWatchers()
        {
            _vabWatcher?.Dispose();
            _sphWatcher?.Dispose();
        }

        /// <summary>
        /// Called when a craft file changes on disk. Debounces and triggers a refresh.
        /// </summary>
        private void OnCraftFileChanged(object sender, FileSystemEventArgs e)
        {
            _nextWatcherRefresh = Time.realtimeSinceStartup + WatcherDebounceSeconds;
        }

        /// <summary>
        /// Called when a craft file is renamed.
        /// </summary>
        private void OnCraftFileRenamed(object sender, RenamedEventArgs e)
        {
            _nextWatcherRefresh = Time.realtimeSinceStartup + WatcherDebounceSeconds;
        }

        /// <summary>
        /// Called every frame by the main plugin to process debounced watcher events.
        /// </summary>
        public void Update()
        {
            if (_nextWatcherRefresh > 0 && Time.realtimeSinceStartup >= _nextWatcherRefresh)
            {
                _nextWatcherRefresh = 0;
                RefreshCraftList();
            }
        }
    }
}
