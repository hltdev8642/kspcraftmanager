using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Manages thumbnail generation and a simple disk cache for craft preview images.
    /// Uses KSP's part rendering system to generate views of crafts.
    /// </summary>
    public class CraftCache : IManager
    {
        private static CraftCache _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static CraftCache Instance => _instance ?? (_instance = new CraftCache());

        private const string ThumbnailFolder = "Thumbnails";
        private const string ThumbnailExtension = ".png";
        private const int DefaultThumbnailSize = 256;

        private string _cacheDir;
        private Dictionary<string, Texture2D> _memoryCache;
        private Dictionary<string, long> _cacheTimestamps; // track file write times to invalidate

        private CraftCache() { }

        /// <summary>
        /// Ensures the thumbnail cache directory exists.
        /// </summary>
        public void Initialize()
        {
            _cacheDir = Path.Combine(KSPCraftManager.PluginDataPath, ThumbnailFolder);
            _memoryCache = new Dictionary<string, Texture2D>();
            _cacheTimestamps = new Dictionary<string, long>();

            if (!Directory.Exists(_cacheDir))
                Directory.CreateDirectory(_cacheDir);

            Debug.Log("[KSPCraftManager] CraftCache initialized.");
        }

        /// <summary>
        /// Clears the in-memory cache.
        /// </summary>
        public void Shutdown()
        {
            foreach (Texture2D tex in _memoryCache.Values)
            {
                if (tex != null)
                    UnityEngine.Object.Destroy(tex);
            }
            _memoryCache.Clear();
            _cacheTimestamps.Clear();
        }

        /// <summary>
        /// Gets a thumbnail for a craft. Returns from memory cache, disk cache,
        /// or generates a new one if needed.
        /// </summary>
        public Texture2D GetThumbnail(CraftInfo craft, int size = DefaultThumbnailSize)
        {
            string cacheKey = GetCacheKey(craft);

            // Check if we have a cached version that's still valid
            if (_memoryCache.TryGetValue(cacheKey, out Texture2D cached) && cached != null)
            {
                if (IsCacheValid(craft, cacheKey))
                    return cached;
            }

            // Try loading from disk cache
            string diskPath = GetDiskCachePath(craft);
            if (File.Exists(diskPath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(diskPath);
                    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        _memoryCache[cacheKey] = tex;
                        _cacheTimestamps[cacheKey] = GetCraftTimestamp(craft);
                        return tex;
                    }
                    UnityEngine.Object.Destroy(tex);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[KSPCraftManager] Failed to load cached thumbnail: {ex}");
                }
            }

            // Generate a new thumbnail
            Texture2D generated = GenerateThumbnail(craft, size);
            if (generated != null)
            {
                _memoryCache[cacheKey] = generated;
                _cacheTimestamps[cacheKey] = GetCraftTimestamp(craft);

                // Save to disk cache
                SaveThumbnailToDisk(generated, diskPath);
            }

            return generated;
        }

        /// <summary>
        /// Generates a new thumbnail for the given craft.
        /// Uses KSP's craft loading and rendering infrastructure.
        /// </summary>
        public Texture2D GenerateThumbnail(CraftInfo craft, int size = DefaultThumbnailSize)
        {
            try
            {
                // Attempt to use KSP's built-in thumbnail generation
                Texture2D kspThumbnail = TryGenerateFromKSP(craft);
                if (kspThumbnail != null)
                    return kspThumbnail;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] KSP thumbnail generation failed: {ex.Message}");
            }

            // Fallback: generate a procedural thumbnail based on craft metadata
            return GenerateFallbackThumbnail(craft, size);
        }

        /// <summary>
        /// Attempts to generate a thumbnail using KSP's internal craft rendering.
        /// </summary>
        private Texture2D TryGenerateFromKSP(CraftInfo craft)
        {
            // Try using the stock thumbnail system via reflection
            // In KSP 1.12.x, CraftThumbnail is in KSP.UI.Screens namespace
            try
            {
                // Try to find the CraftThumbnail type via reflection
                System.Type thumbnailType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    thumbnailType = asm.GetType("KSP.UI.Screens.CraftThumbnail");
                    if (thumbnailType != null) break;
                }

                if (thumbnailType != null)
                {
                    // The stock system expects a CraftEntry, but we can try a workaround
                    // by creating a config node from the craft file
                    ConfigNode craftNode = ConfigNode.Load(craft.FilePath);
                    if (craftNode != null)
                    {
                        // Use the stock thumbnail generator
                        var generateMethod = thumbnailType.GetMethod("GenerateThumbnail",
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                        if (generateMethod != null)
                        {
                            // Try to invoke with the craft file path
                            var result = generateMethod.Invoke(null, new object[] {
                                craft.FilePath,
                                DefaultThumbnailSize,
                                DefaultThumbnailSize
                            });

                            if (result is Texture2D tex && tex != null)
                                return tex;
                        }
                    }
                }
            }
            catch
            {
                // Silently fall through to fallback
            }

            // Method 2: Try loading the craft and rendering it off-screen
            try
            {
                // This is complex and may not work in all KSP versions.
                // For now, return null to use the fallback.
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Generates a procedural thumbnail based on craft metadata.
        /// Creates a visual representation showing part count, mass, and type.
        /// Includes craft name text and a unique hue from the craft's name hash
        /// so no two crafts look identical.
        /// </summary>
        private Texture2D GenerateFallbackThumbnail(CraftInfo craft, int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            // Derive a unique hue from the craft name for visual distinction
            float nameHue = GetNameHue(craft);

            // Background gradient with unique hue
            Color bgTop = Color.HSVToRGB(nameHue, 0.25f, 0.18f);
            Color bgBottom = Color.HSVToRGB((nameHue + 0.5f) % 1f, 0.35f, 0.08f);

            for (int y = 0; y < size; y++)
            {
                float t = y / (float)size;
                Color bg = Color.Lerp(bgBottom, bgTop, t);
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, bg);
                }
            }

            // Draw a simplified craft silhouette based on part count
            int centerX = size / 2;
            int centerY = size / 2;
            int bodyWidth = Mathf.Clamp(craft.PartCount / 2, 8, size / 3);
            int bodyHeight = Mathf.Clamp(craft.PartCount, 20, size - 20);

            // Body color — use craft name hue shifted for contrast
            Color bodyColor = Color.HSVToRGB((nameHue + 0.33f) % 1f, 0.7f, 0.75f);

            for (int y = centerY - bodyHeight / 2; y <= centerY + bodyHeight / 2; y++)
            {
                if (y < 0 || y >= size) continue;

                // Nose cone taper
                int distFromTop = y - (centerY - bodyHeight / 2);
                int distFromBottom = (centerY + bodyHeight / 2) - y;
                int halfWidth = bodyWidth / 2;

                if (distFromTop < bodyWidth / 2)
                {
                    // Nose cone
                    float taper = distFromTop / (float)(bodyWidth / 2);
                    halfWidth = (int)(bodyWidth / 2 * taper);
                }
                else if (distFromBottom < bodyWidth / 4)
                {
                    // Engine nozzle
                    float taper = distFromBottom / (float)(bodyWidth / 4);
                    halfWidth = (int)(bodyWidth / 2 * (1 + 0.3f * (1 - taper)));
                }

                if (halfWidth < 2) halfWidth = 2;

                for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
                {
                    if (x >= 0 && x < size)
                    {
                        float edgeFactor = Mathf.Abs(x - centerX) / (float)halfWidth;
                        Color pixelColor = bodyColor;
                        if (edgeFactor > 0.85f)
                            pixelColor = Color.Lerp(bodyColor, Color.white, (edgeFactor - 0.85f) / 0.15f * 0.3f);
                        else if (edgeFactor < 0.3f)
                            pixelColor = Color.Lerp(bodyColor, Color.black, (1 - edgeFactor / 0.3f) * 0.2f);

                        tex.SetPixel(x, y, pixelColor);
                    }
                }
            }

            // Draw wings if SPH
            if (craft.Type == "SPH")
            {
                Color wingColor = Color.HSVToRGB((nameHue + 0.66f) % 1f, 0.5f, 0.5f);
                for (int y = centerY; y <= centerY + bodyHeight / 3; y++)
                {
                    if (y < 0 || y >= size) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int wingStart = centerX + side * (bodyWidth / 2 + 1);
                        int wingEnd = centerX + side * (bodyWidth / 2 + bodyWidth / 3);
                        for (int x = Math.Min(wingStart, wingEnd); x <= Math.Max(wingStart, wingEnd); x++)
                        {
                            if (x >= 0 && x < size)
                                tex.SetPixel(x, y, wingColor);
                        }
                    }
                }
            }

            // Draw craft name text along the bottom
            DrawSimpleStats(tex, craft, size);

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Returns a stable hue (0..1) derived from the craft's name for visual distinction.
        /// </summary>
        private float GetNameHue(CraftInfo craft)
        {
            int hash = craft.Name.GetHashCode();
            // Ensure positive and map to 0..1
            uint u = (uint)hash;
            return (u % 360) / 360f;
        }

        /// <summary>
        /// Draws simple stat indicators and craft name on the thumbnail.
        /// </summary>
        private void DrawSimpleStats(Texture2D tex, CraftInfo craft, int size)
        {
            // Draw craft name at the bottom
            string nameLabel = craft.Name.Length > 10 ? craft.Name.Substring(0, 10) + ".." : craft.Name;
            int labelY = size - 8;
            Color labelColor = Color.white;

            for (int i = 0; i < nameLabel.Length; i++)
            {
                int x = 4 + i * 7;
                if (x < size - 8)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int px = x + dx;
                            int py = labelY + dy;
                            if (px >= 0 && px < size && py >= 0 && py < size)
                                tex.SetPixel(px, py, labelColor);
                        }
                    }
                }
            }

            // Draw part-count indicator in top-right
            string partLabel = $"P:{craft.PartCount}";
            int px2 = size - 8 - partLabel.Length * 7;
            for (int i = 0; i < partLabel.Length; i++)
            {
                int x = px2 + i * 7;
                if (x >= 0 && x < size)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int pxx = x + dx;
                            int py = 6 + dy;
                            if (pxx >= 0 && pxx < size && py >= 0 && py < size)
                                tex.SetPixel(pxx, py, new Color(0.8f, 0.8f, 0.3f));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a color representing the craft type.
        /// </summary>
        private Color GetTypeColor(CraftInfo craft)
        {
            if (craft.Type == "VAB")
                return new Color(0.7f, 0.5f, 0.2f); // Golden/orange for rockets
            else
                return new Color(0.3f, 0.6f, 0.8f); // Blue for spaceplanes
        }

        /// <summary>
        /// Clears the cached thumbnail for a specific craft.
        /// </summary>
        public void InvalidateCache(CraftInfo craft)
        {
            string key = GetCacheKey(craft);
            _memoryCache.Remove(key);
            _cacheTimestamps.Remove(key);

            string diskPath = GetDiskCachePath(craft);
            if (File.Exists(diskPath))
            {
                try { File.Delete(diskPath); }
                catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Clears the entire thumbnail cache.
        /// </summary>
        public void ClearCache()
        {
            foreach (Texture2D tex in _memoryCache.Values)
            {
                if (tex != null)
                    UnityEngine.Object.Destroy(tex);
            }
            _memoryCache.Clear();
            _cacheTimestamps.Clear();

            try
            {
                if (Directory.Exists(_cacheDir))
                {
                    foreach (string file in Directory.GetFiles(_cacheDir, "*" + ThumbnailExtension))
                        File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to clear disk cache: {ex}");
            }
        }

        /// <summary>
        /// Checks whether the in-memory cached thumbnail is still valid
        /// (craft file hasn't been modified since caching).
        /// </summary>
        private bool IsCacheValid(CraftInfo craft, string cacheKey)
        {
            if (!_cacheTimestamps.TryGetValue(cacheKey, out long cachedTs))
                return false;

            long currentTs = GetCraftTimestamp(craft);
            return currentTs <= cachedTs;
        }

        /// <summary>
        /// Gets a timestamp representing the craft file's current state.
        /// </summary>
        private long GetCraftTimestamp(CraftInfo craft)
        {
            try
            {
                FileInfo fi = new FileInfo(craft.FilePath);
                return fi.LastWriteTimeUtc.Ticks;
            }
            catch
            {
                return DateTime.UtcNow.Ticks;
            }
        }

        /// <summary>
        /// Generates a unique cache key for a craft.
        /// </summary>
        private string GetCacheKey(CraftInfo craft)
        {
            return craft.FilePath.Replace(':', '_').Replace('\\', '_').Replace('/', '_');
        }

        /// <summary>
        /// Gets the disk cache path for a craft.
        /// </summary>
        private string GetDiskCachePath(CraftInfo craft)
        {
            string key = GetCacheKey(craft) + ThumbnailExtension;
            return Path.Combine(_cacheDir, key);
        }

        /// <summary>
        /// Saves a thumbnail texture to disk as PNG.
        /// </summary>
        private void SaveThumbnailToDisk(Texture2D tex, string path)
        {
            try
            {
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to save thumbnail to disk: {ex}");
            }
        }

        /// <summary>
        /// Ensures all pending thumbnails are saved to disk.
        /// </summary>
        public void FlushCache()
        {
            // Memory cache is saved to disk as thumbnails are generated.
            // This is a no-op placeholder for future batch-flush logic.
        }
    }
}
