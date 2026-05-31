using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Represents a single saved version of a craft.
    /// </summary>
    public class CraftVersion
    {
        /// <summary>Unique identifier for this version.</summary>
        public string Id { get; set; }

        /// <summary>Original craft file path.</summary>
        public string CraftPath { get; set; }

        /// <summary>Path to the backed-up version file.</summary>
        public string BackupPath { get; set; }

        /// <summary>Timestamp when this version was saved.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Optional version label/name.</summary>
        public string Label { get; set; }

        /// <summary>File size of the backed-up version.</summary>
        public long FileSize { get; set; }

        /// <summary>Part count at this version.</summary>
        public int PartCount { get; set; }

        /// <summary>Display-friendly timestamp.</summary>
        public string TimestampDisplay => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Tracks version history for local craft files.
    /// Creates backup copies when crafts are modified and allows restoring previous versions.
    /// </summary>
    public class VersionHistory : IManager
    {
        private static VersionHistory _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static VersionHistory Instance => _instance ?? (_instance = new VersionHistory());

        private const string HistoryFileName = "KSPCraftManagerHistory.cfg";
        private const string BackupFolderName = "KSPCraftManagerHistory";
        private const int MaxVersionsPerCraft = 10;

        private List<CraftVersion> _versions;
        private string _backupDir;
        private string _indexFilePath;

        private VersionHistory() { }

        /// <summary>
        /// Loads version history index and ensures backup directory exists.
        /// </summary>
        public void Initialize()
        {
            _backupDir = Path.Combine(KSPCraftManager.PluginDataPath, BackupFolderName);
            _indexFilePath = Path.Combine(KSPCraftManager.PluginDataPath, HistoryFileName);
            _versions = new List<CraftVersion>();

            if (!Directory.Exists(_backupDir))
                Directory.CreateDirectory(_backupDir);

            Load();
            Debug.Log($"[KSPCraftManager] VersionHistory initialized with {_versions.Count} version entries.");
        }

        /// <summary>
        /// Saves the version index.
        /// </summary>
        public void Shutdown()
        {
            Save();
        }

        /// <summary>
        /// Creates a version snapshot of the given craft file.
        /// Called automatically when a craft is modified.
        /// </summary>
        public CraftVersion Snapshot(CraftInfo craft, string label = null)
        {
            if (!File.Exists(craft.FilePath)) return null;

            try
            {
                // Check if we already have an identical version (same file size and mod time)
                CraftVersion latest = GetLatestVersion(craft.FilePath);
                FileInfo fi = new FileInfo(craft.FilePath);
                if (latest != null && latest.FileSize == fi.Length)
                    return latest; // No changes

                // Remove oldest versions if over the limit
                List<CraftVersion> existing = GetVersions(craft.FilePath);
                while (existing.Count >= MaxVersionsPerCraft)
                {
                    CraftVersion oldest = existing.OrderBy(v => v.Timestamp).First();
                    DeleteVersionFile(oldest);
                    _versions.Remove(oldest);
                    existing.Remove(oldest);
                }

                // Create backup
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string safeName = SanitizeFileName(craft.Name);
                string backupName = $"{safeName}_{timestamp}.craft";
                string backupPath = Path.Combine(_backupDir, backupName);

                File.Copy(craft.FilePath, backupPath, true);

                CraftVersion version = new CraftVersion
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CraftPath = craft.FilePath,
                    BackupPath = backupPath,
                    Timestamp = DateTime.Now,
                    Label = label ?? $"v{existing.Count + 1}",
                    FileSize = fi.Length,
                    PartCount = craft.PartCount
                };

                _versions.Add(version);
                Save();

                Debug.Log($"[KSPCraftManager] Version snapshot created for {craft.Name}: {version.Label}");
                return version;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to create version snapshot: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Restores a craft to a previous version.
        /// </summary>
        public bool Restore(CraftVersion version)
        {
            try
            {
                if (!File.Exists(version.BackupPath))
                {
                    Debug.LogError($"[KSPCraftManager] Backup file not found: {version.BackupPath}");
                    return false;
                }

                // Create a snapshot of the current version before restoring
                CraftInfo currentInfo = CraftDataManager.Instance.AllCrafts
                    .Find(c => c.FilePath == version.CraftPath);
                if (currentInfo != null)
                {
                    Snapshot(currentInfo, "pre-restore");
                }

                File.Copy(version.BackupPath, version.CraftPath, true);
                Debug.Log($"[KSPCraftManager] Restored {version.CraftPath} to version from {version.TimestampDisplay}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KSPCraftManager] Failed to restore version: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Gets all versions for a specific craft file.
        /// </summary>
        public List<CraftVersion> GetVersions(string craftPath)
        {
            return _versions
                .Where(v => v.CraftPath == craftPath)
                .OrderByDescending(v => v.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Gets the latest version for a craft, or null if none.
        /// </summary>
        public CraftVersion GetLatestVersion(string craftPath)
        {
            return _versions
                .Where(v => v.CraftPath == craftPath)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefault();
        }

        /// <summary>
        /// Deletes a specific version.
        /// </summary>
        public bool DeleteVersion(CraftVersion version)
        {
            DeleteVersionFile(version);
            return _versions.Remove(version);
        }

        /// <summary>
        /// Deletes all versions for a specific craft.
        /// </summary>
        public void DeleteAllVersions(string craftPath)
        {
            List<CraftVersion> toRemove = _versions.Where(v => v.CraftPath == craftPath).ToList();
            foreach (CraftVersion v in toRemove)
            {
                DeleteVersionFile(v);
                _versions.Remove(v);
            }
        }

        /// <summary>
        /// Deletes the physical backup file for a version.
        /// </summary>
        private void DeleteVersionFile(CraftVersion version)
        {
            try
            {
                if (File.Exists(version.BackupPath))
                    File.Delete(version.BackupPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to delete version file: {ex}");
            }
        }

        /// <summary>
        /// Gets version history for all crafts (grouped).
        /// </summary>
        public Dictionary<string, List<CraftVersion>> GetAllVersionGroups()
        {
            return _versions
                .GroupBy(v => v.CraftPath)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Timestamp).ToList());
        }

        /// <summary>
        /// Sanitizes a string for use in file names.
        /// </summary>
        private string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
            return sanitized.TrimEnd('.');
        }

        /// <summary>
        /// Loads version history index from disk.
        /// </summary>
        private void Load()
        {
            try
            {
                if (!File.Exists(_indexFilePath)) return;

                ConfigNode root = ConfigNode.Load(_indexFilePath);
                if (root == null) return;

                foreach (ConfigNode versionNode in root.GetNodes("VERSION"))
                {
                    CraftVersion version = new CraftVersion
                    {
                        Id = versionNode.GetValue("id") ?? Guid.NewGuid().ToString("N"),
                        CraftPath = versionNode.GetValue("craftPath") ?? "",
                        BackupPath = versionNode.GetValue("backupPath") ?? "",
                        Label = versionNode.GetValue("label") ?? "",
                        Timestamp = DateTime.MinValue,
                        FileSize = 0,
                        PartCount = 0
                    };

                    string ts = versionNode.GetValue("timestamp");
                    if (!string.IsNullOrEmpty(ts))
                    {
                        DateTime parsedTs;
                        if (DateTime.TryParse(ts, out parsedTs))
                            version.Timestamp = parsedTs;
                    }

                    string fs = versionNode.GetValue("fileSize");
                    if (!string.IsNullOrEmpty(fs))
                    {
                        long parsedFs;
                        if (long.TryParse(fs, out parsedFs))
                            version.FileSize = parsedFs;
                    }

                    string pc = versionNode.GetValue("partCount");
                    if (!string.IsNullOrEmpty(pc))
                    {
                        int parsedPc;
                        if (int.TryParse(pc, out parsedPc))
                            version.PartCount = parsedPc;
                    }

                    _versions.Add(version);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to load version history: {ex}");
            }
        }

        /// <summary>
        /// Saves version history index to disk.
        /// </summary>
        private void Save()
        {
            try
            {
                ConfigNode root = new ConfigNode("VERSION_HISTORY");
                foreach (CraftVersion version in _versions)
                {
                    ConfigNode vn = root.AddNode("VERSION");
                    vn.AddValue("id", version.Id);
                    vn.AddValue("craftPath", version.CraftPath);
                    vn.AddValue("backupPath", version.BackupPath);
                    vn.AddValue("timestamp", version.Timestamp.ToString("O"));
                    vn.AddValue("label", version.Label ?? "");
                    vn.AddValue("fileSize", version.FileSize.ToString());
                    vn.AddValue("partCount", version.PartCount.ToString());
                }
                root.Save(_indexFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to save version history: {ex}");
            }
        }
    }
}
