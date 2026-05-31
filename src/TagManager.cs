using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Manages tagging of crafts. Tags are stored per-craft in a persistent
    /// ConfigNode file. Supports adding, removing, bulk operations, and
    /// extensive auto-generation of tags based on craft metadata.
    /// </summary>
    public class TagManager : IManager
    {
        private static TagManager _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static TagManager Instance => _instance ?? (_instance = new TagManager());

        private const string TagsFileName = "KSPCraftManagerTags.cfg";
        private Dictionary<string, List<string>> _craftTags; // craftPath -> tags
        private string _filePath;

        /// <summary>
        /// Fired when tags change for any craft.
        /// </summary>
        public event Action OnTagsChanged;

        private TagManager() { }

        /// <summary>
        /// Loads tags from the config file.
        /// </summary>
        public void Initialize()
        {
            _filePath = Path.Combine(KSPCraftManager.PluginDataPath, TagsFileName);
            _craftTags = new Dictionary<string, List<string>>();
            Load();
            Debug.Log($"[KSPCraftManager] TagManager initialized with {_craftTags.Count} tagged crafts.");
        }

        /// <summary>
        /// Saves tags to disk.
        /// </summary>
        public void Shutdown()
        {
            Save();
        }

        /// <summary>
        /// Gets the tags for a specific craft.
        /// </summary>
        public List<string> GetTagsForCraft(string craftPath)
        {
            if (_craftTags.TryGetValue(craftPath, out List<string> tags))
                return new List<string>(tags);
            return new List<string>();
        }

        /// <summary>
        /// Adds a tag to a craft. Returns true if the tag was added (was not already present).
        /// </summary>
        public bool AddTag(string craftPath, string tag)
        {
            string normalized = NormalizeTag(tag);
            if (string.IsNullOrEmpty(normalized)) return false;

            if (!_craftTags.TryGetValue(craftPath, out List<string> tags))
            {
                tags = new List<string>();
                _craftTags[craftPath] = tags;
            }

            if (tags.Contains(normalized))
                return false;

            tags.Add(normalized);
            Save();
            OnTagsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Removes a tag from a craft. Returns true if the tag was removed.
        /// </summary>
        public bool RemoveTag(string craftPath, string tag)
        {
            string normalized = NormalizeTag(tag);
            if (_craftTags.TryGetValue(craftPath, out List<string> tags))
            {
                if (tags.Remove(normalized))
                {
                    Save();
                    OnTagsChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Sets the exact list of tags for a craft.
        /// </summary>
        public void SetTags(string craftPath, List<string> tags)
        {
            _craftTags[craftPath] = tags.Select(NormalizeTag).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
            Save();
            OnTagsChanged?.Invoke();
        }

        /// <summary>
        /// Removes a tag from all crafts. Useful for tag cleanup.
        /// </summary>
        public void RemoveTagFromAll(string tag)
        {
            string normalized = NormalizeTag(tag);
            bool changed = false;

            foreach (string craftPath in _craftTags.Keys.ToList())
            {
                if (_craftTags[craftPath].Remove(normalized))
                    changed = true;
            }

            if (changed)
            {
                Save();
                OnTagsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Gets all unique tags across all crafts.
        /// </summary>
        public List<string> GetAllTags()
        {
            return _craftTags.Values
                .SelectMany(t => t)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        /// <summary>
        /// Gets tags with their usage counts.
        /// </summary>
        public Dictionary<string, int> GetTagUsageCounts()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (List<string> tags in _craftTags.Values)
            {
                foreach (string tag in tags)
                {
                    if (counts.ContainsKey(tag))
                        counts[tag]++;
                    else
                        counts[tag] = 1;
                }
            }
            return counts;
        }

        /// <summary>
        /// Returns all crafts that have a specific tag.
        /// </summary>
        public List<string> GetCraftsWithTag(string tag)
        {
            string normalized = NormalizeTag(tag);
            return _craftTags
                .Where(kvp => kvp.Value.Contains(normalized))
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Auto-generates tags for all crafts based on their metadata.
        /// This is an extensive analysis that examines part types, sizes,
        /// mass categories, and other heuristics.
        /// </summary>
        public void AutoGenerateAllTags(List<CraftInfo> crafts)
        {
            foreach (CraftInfo craft in crafts)
            {
                AutoGenerateTags(craft);
            }
            Save();
            OnTagsChanged?.Invoke();
            Debug.Log($"[KSPCraftManager] Auto-generated tags for {crafts.Count} crafts.");
        }

        /// <summary>
        /// Auto-generates tags for a single craft based on detailed metadata analysis.
        /// </summary>
        public List<string> AutoGenerateTags(CraftInfo craft)
        {
            List<string> generated = new List<string>();

            // ── Type-based tags ──
            if (craft.Type == "VAB")
                generated.Add("rocket");
            else if (craft.Type == "SPH")
                generated.Add("spaceplane");

            // ── Size-based tags ──
            float maxDim = Mathf.Max(craft.Size.x, craft.Size.y, craft.Size.z);
            if (maxDim < 5f)
                generated.Add("micro");
            else if (maxDim < 15f)
                generated.Add("small");
            else if (maxDim < 30f)
                generated.Add("medium");
            else if (maxDim < 60f)
                generated.Add("large");
            else
                generated.Add("超大");

            // ── Mass-based tags ──
            if (craft.TotalMass < 5)
                generated.Add("light");
            else if (craft.TotalMass < 50)
                generated.Add("medium-mass");
            else if (craft.TotalMass < 200)
                generated.Add("heavy");
            else
                generated.Add("super-heavy");

            // ── Part count categories ──
            if (craft.PartCount < 20)
                generated.Add("simple");
            else if (craft.PartCount < 80)
                generated.Add("moderate");
            else if (craft.PartCount < 200)
                generated.Add("complex");
            else
                generated.Add("ultra-complex");

            // ── Cost categories ──
            if (craft.TotalCost < 10000)
                generated.Add("budget");
            else if (craft.TotalCost > 500000)
                generated.Add("expensive");

            // ── Part-type analysis ──
            bool hasEngine = false;
            bool hasSRB = false;
            bool hasWheel = false;
            bool hasWing = false;
            bool hasDockingPort = false;
            bool hasScience = false;
            bool hasSolarPanel = false;
            bool hasLandingGear = false;
            bool hasParachute = false;
            bool hasFuelTank = false;
            bool hasRcs = false;
            bool hasReactionWheel = false;

            foreach (string partName in craft.PartNames)
            {
                string lower = partName.ToLowerInvariant();
                if (lower.Contains("engine") || lower.Contains("motor") || lower.Contains("turbo"))
                    hasEngine = true;
                if (lower.Contains("srb") || lower.Contains("solid") || lower.Contains("booster"))
                    hasSRB = true;
                if (lower.Contains("wheel") || lower.Contains("rover"))
                    hasWheel = true;
                if (lower.Contains("wing") || lower.Contains("fin") || lower.Contains("control surface") || lower.Contains("elevon"))
                    hasWing = true;
                if (lower.Contains("docking") || lower.Contains("dock") || lower.Contains("port"))
                    hasDockingPort = true;
                if (lower.Contains("science") || lower.Contains("experiment") || lower.Contains("sensor") || lower.Contains("mat") || lower.Contains("lab"))
                    hasScience = true;
                if (lower.Contains("solar") || lower.Contains("panel") && lower.Contains("solar"))
                    hasSolarPanel = true;
                if (lower.Contains("landing") || lower.Contains("gear") || lower.Contains("leg"))
                    hasLandingGear = true;
                if (lower.Contains("parachute") || lower.Contains("chute"))
                    hasParachute = true;
                if (lower.Contains("fuel") || lower.Contains("tank") && (lower.Contains("fuel") || lower.Contains("oscar") || lower.Contains("flt")))
                    hasFuelTank = true;
                if (lower.Contains("rcs") || lower.Contains("thruster"))
                    hasRcs = true;
                if (lower.Contains("reaction") || lower.Contains("wheel") && lower.Contains("reaction") || lower.Contains("sas"))
                    hasReactionWheel = true;
            }

            if (hasSRB) generated.Add("srb");
            if (hasWheel) generated.Add("rover");
            if (hasWing) generated.Add("winged");
            if (hasDockingPort) generated.Add("dock-capable");
            if (hasScience) generated.Add("science");
            if (hasSolarPanel) generated.Add("solar-powered");
            if (hasLandingGear) generated.Add("landing-gear");
            if (hasParachute) generated.Add("parachute");
            if (hasRcs) generated.Add("rcs");
            if (hasReactionWheel) generated.Add("sas-stable");
            if (hasEngine) generated.Add("powered");

            // ── Mission-type heuristics ──
            if (hasDockingPort && hasEngine)
                generated.Add("orbital");
            if (hasLandingGear && !hasWheel && craft.Type == "VAB")
                generated.Add("lander");
            if (hasWheel)
                generated.Add("ground");
            if (craft.CrewCapacity > 0)
                generated.Add("crewed");
            else
                generated.Add("probe");
            if (craft.CrewCapacity > 3)
                generated.Add("crew-transport");
            if (craft.CrewCapacity >= 6)
                generated.Add("station-crew");

            // ── Mod-based tags ──
            foreach (string mod in craft.RequiredMods)
            {
                string modTag = "mod:" + mod.ToLowerInvariant();
                if (!generated.Contains(modTag))
                    generated.Add(modTag);
            }

            // Deduplicate and normalize
            generated = generated.Distinct().Select(NormalizeTag).Where(t => !string.IsNullOrEmpty(t)).ToList();

            // Merge with existing tags (keep existing, add new auto-generated ones)
            if (!_craftTags.TryGetValue(craft.FilePath, out List<string> existing))
            {
                existing = new List<string>();
                _craftTags[craft.FilePath] = existing;
            }

            foreach (string tag in generated)
            {
                if (!existing.Contains(tag))
                    existing.Add(tag);
            }

            return existing;
        }

        /// <summary>
        /// Normalizes a tag: trims, lowercases, replaces spaces with hyphens.
        /// </summary>
        private string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            return tag.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        /// <summary>
        /// Loads tags from the config file.
        /// </summary>
        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;

                ConfigNode root = ConfigNode.Load(_filePath);
                if (root == null) return;

                foreach (ConfigNode craftNode in root.GetNodes("CRAFT"))
                {
                    string path = craftNode.GetValue("path");
                    if (string.IsNullOrEmpty(path)) continue;

                    List<string> tags = new List<string>();
                    foreach (ConfigNode tagNode in craftNode.GetNodes("TAG"))
                    {
                        string tag = tagNode.GetValue("name");
                        if (!string.IsNullOrEmpty(tag))
                            tags.Add(tag);
                    }
                    _craftTags[path] = tags;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to load tags: {ex}");
            }
        }

        /// <summary>
        /// Saves tags to the config file.
        /// </summary>
        private void Save()
        {
            try
            {
                ConfigNode root = new ConfigNode("TAGS");
                foreach (var kvp in _craftTags)
                {
                    if (kvp.Value.Count == 0) continue;

                    ConfigNode craftNode = root.AddNode("CRAFT");
                    craftNode.AddValue("path", kvp.Key);
                    foreach (string tag in kvp.Value)
                    {
                        ConfigNode tagNode = craftNode.AddNode("TAG");
                        tagNode.AddValue("name", tag);
                    }
                }
                root.Save(_filePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to save tags: {ex}");
            }
        }
    }
}
