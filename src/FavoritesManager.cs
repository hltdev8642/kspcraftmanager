using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Manages the favorites system. Stores favorite craft file paths
    /// in a persistent ConfigNode file. Supports both local and KerbalX crafts.
    /// </summary>
    public class FavoritesManager : IManager
    {
        private static FavoritesManager _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static FavoritesManager Instance => _instance ?? (_instance = new FavoritesManager());

        private const string FavoritesFileName = "KSPCraftManagerFavorites.cfg";
        private HashSet<string> _favoritePaths;
        private string _filePath;

        /// <summary>
        /// Fired when the favorites list changes.
        /// </summary>
        public event Action OnFavoritesChanged;

        private FavoritesManager() { }

        /// <summary>
        /// Loads favorites from the config file.
        /// </summary>
        public void Initialize()
        {
            _filePath = Path.Combine(KSPCraftManager.PluginDataPath, FavoritesFileName);
            _favoritePaths = new HashSet<string>();
            Load();
            Debug.Log($"[KSPCraftManager] FavoritesManager initialized with {_favoritePaths.Count} favorites.");
        }

        /// <summary>
        /// Saves favorites to disk.
        /// </summary>
        public void Shutdown()
        {
            Save();
        }

        /// <summary>
        /// Returns true if the given craft path is favorited.
        /// </summary>
        public bool IsFavorite(string craftPath)
        {
            return _favoritePaths.Contains(craftPath);
        }

        /// <summary>
        /// Adds a craft to favorites.
        /// </summary>
        public void AddFavorite(string craftPath)
        {
            if (_favoritePaths.Add(craftPath))
            {
                Save();
                OnFavoritesChanged?.Invoke();
            }
        }

        /// <summary>
        /// Removes a craft from favorites.
        /// </summary>
        public void RemoveFavorite(string craftPath)
        {
            if (_favoritePaths.Remove(craftPath))
            {
                Save();
                OnFavoritesChanged?.Invoke();
            }
        }

        /// <summary>
        /// Toggles favorite status for a craft. Returns the new state.
        /// </summary>
        public bool ToggleFavorite(string craftPath)
        {
            if (_favoritePaths.Contains(craftPath))
            {
                RemoveFavorite(craftPath);
                return false;
            }
            else
            {
                AddFavorite(craftPath);
                return true;
            }
        }

        /// <summary>
        /// Returns all favorite craft paths.
        /// </summary>
        public HashSet<string> GetAllFavorites()
        {
            return new HashSet<string>(_favoritePaths);
        }

        /// <summary>
        /// Returns the count of favorites.
        /// </summary>
        public int FavoriteCount => _favoritePaths.Count;

        /// <summary>
        /// Loads favorites from the config file.
        /// </summary>
        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;

                ConfigNode node = ConfigNode.Load(_filePath);
                if (node == null) return;

                ConfigNode[] entries = node.GetNodes("FAVORITE");
                foreach (ConfigNode entry in entries)
                {
                    string path = entry.GetValue("path");
                    if (!string.IsNullOrEmpty(path))
                        _favoritePaths.Add(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to load favorites: {ex}");
            }
        }

        /// <summary>
        /// Saves favorites to the config file.
        /// </summary>
        private void Save()
        {
            try
            {
                ConfigNode root = new ConfigNode("FAVORITES");
                foreach (string path in _favoritePaths)
                {
                    ConfigNode entry = root.AddNode("FAVORITE");
                    entry.AddValue("path", path);
                }
                root.Save(_filePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to save favorites: {ex}");
            }
        }

        /// <summary>
        /// Removes any favorites that point to files that no longer exist.
        /// </summary>
        public void CleanupOrphanedFavorites()
        {
            int removed = _favoritePaths.RemoveWhere(p => !File.Exists(p));
            if (removed > 0)
            {
                Save();
                OnFavoritesChanged?.Invoke();
                Debug.Log($"[KSPCraftManager] Removed {removed} orphaned favorites.");
            }
        }
    }
}
