using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Handles the KerbalX browsing tab UI and manages the remote craft list.
    /// Integrates with KerbalXAPI for data and handles download + import flow.
    /// </summary>
    public class KerbalXBrowser
    {
        private static KerbalXBrowser _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static KerbalXBrowser Instance => _instance ?? (_instance = new KerbalXBrowser());

        /// <summary>
        /// Current list of remote crafts.
        /// </summary>
        public List<KerbalXCraftListing> RemoteCrafts { get; private set; } = new List<KerbalXCraftListing>();

        /// <summary>
        /// Currently selected remote craft.
        /// </summary>
        public KerbalXCraftListing SelectedCraft { get; set; }

        /// <summary>
        /// Whether a request is in progress.
        /// </summary>
        public bool IsLoading { get; set; }

        /// <summary>
        /// Error message from the last request.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Current page of results.
        /// </summary>
        public int CurrentPage { get; set; } = 1;

        /// <summary>
        /// Total pages available.
        /// </summary>
        public int TotalPages { get; set; } = 1;

        /// <summary>
        /// Search query text.
        /// </summary>
        public string SearchQuery { get; set; } = string.Empty;

        /// <summary>
        /// Filter by craft type: "rocket", "spaceplane", or "" for all.
        /// </summary>
        public string FilterType { get; set; } = string.Empty;

        /// <summary>
        /// Sort order: "downloads", "likes", "rating", "updated", "name".
        /// </summary>
        public string SortOrder { get; set; } = "downloads";

        /// <summary>
        /// Whether to show only the user's own crafts.
        /// </summary>
        public bool ShowMyCrafts { get; set; }

        /// <summary>
        /// Fired when the remote craft list changes.
        /// </summary>
        public event Action OnCraftsUpdated;

        /// <summary>
        /// Fired when a download completes.
        /// </summary>
        public event Action<string, bool> OnDownloadComplete; // craftName, success

        // Download tracking
        private readonly Dictionary<string, bool> _downloading = new Dictionary<string, bool>();

        private KerbalXBrowser() { }

        /// <summary>
        /// Initializes the browser and hooks into KerbalXAPI events.
        /// </summary>
        public void Initialize()
        {
            KerbalXAPI.Instance.OnRequestCompleted += HandleApiResponse;
            Debug.Log("[KSPCraftManager] KerbalXBrowser initialized.");
        }

        /// <summary>
        /// Unhooks from events.
        /// </summary>
        public void Shutdown()
        {
            if (KerbalXAPI.Instance != null)
                KerbalXAPI.Instance.OnRequestCompleted -= HandleApiResponse;
        }

        /// <summary>
        /// Performs a search or fetches crafts based on current state.
        /// </summary>
        public void Refresh()
        {
            if (!KerbalXAPI.Instance.IsAuthenticated)
            {
                ErrorMessage = "Not authenticated. Set your KerbalX API key in Settings.";
                OnCraftsUpdated?.Invoke();
                return;
            }

            IsLoading = true;
            ErrorMessage = null;

            if (ShowMyCrafts)
            {
                KerbalXAPI.Instance.FetchMyCrafts(CurrentPage, 50);
            }
            else
            {
                KerbalXAPI.Instance.SearchCrafts(
                    SearchQuery,
                    CurrentPage,
                    50,
                    string.IsNullOrEmpty(FilterType) ? null : FilterType,
                    null, null,
                    SortOrder
                );
            }
        }

        /// <summary>
        /// Downloads a remote craft to the local Ships folder.
        /// </summary>
        public void DownloadCraft(KerbalXCraftListing listing)
        {
            if (_downloading.ContainsKey(listing.Id) && _downloading[listing.Id])
                return;

            _downloading[listing.Id] = true;

            // Determine save path
            string craftType = listing.CraftType == "spaceplane" ? "SPH" : "VAB";
            string kspRoot = GetKspRoot();
            string saveDir = Path.Combine(kspRoot, "Ships", craftType);
            string savePath = Path.Combine(saveDir, SanitizeFileName(listing.Name) + ".craft");

            if (!Directory.Exists(saveDir))
                Directory.CreateDirectory(saveDir);

            if (string.IsNullOrEmpty(listing.DownloadUrl))
            {
                _downloading[listing.Id] = false;
                OnDownloadComplete?.Invoke(listing.Name, false);
                return;
            }

            KerbalXAPI.Instance.DownloadCraft(listing.DownloadUrl, savePath, (success, message) =>
            {
                _downloading[listing.Id] = false;
                OnDownloadComplete?.Invoke(listing.Name, success);

                if (success)
                {
                    // Refresh local craft list
                    CraftDataManager.Instance.RefreshCraftList();
                }
            });
        }

        /// <summary>
        /// Checks if a craft is currently being downloaded.
        /// </summary>
        public bool IsDownloading(KerbalXCraftListing listing)
        {
            return _downloading.ContainsKey(listing.Id) && _downloading[listing.Id];
        }

        /// <summary>
        /// Converts a KerbalXCraftListing to a CraftInfo for display in the unified list.
        /// </summary>
        public CraftInfo ToCraftInfo(KerbalXCraftListing listing)
        {
            CraftInfo info = new CraftInfo
            {
                Name = listing.Name,
                Description = listing.Description,
                Type = listing.CraftType == "spaceplane" ? "SPH" : "VAB",
                PartCount = listing.PartCount,
                TotalMass = listing.Mass,
                TotalCost = listing.Cost,
                Source = CraftSource.KerbalX,
                KerbalXId = listing.Id,
                KerbalXAuthor = listing.Author,
                KerbalXDownloads = listing.Downloads,
                KerbalXLikes = listing.Likes,
                KerbalXRating = listing.Rating,
                KerbalXUpdated = listing.UpdatedAt,
                KspVersion = listing.KspVersion,
                PartNames = new List<string>(),
                RequiredMods = new List<string>(),
                Tags = new List<string>(listing.Tags)
            };
            return info;
        }

        // ── API Response Handler ─────────────────────────────────────────

        private void HandleApiResponse(string requestType, KerbalXResult result)
        {
            IsLoading = false;

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage;
                OnCraftsUpdated?.Invoke();
                return;
            }

            CurrentPage = result.CurrentPage;
            TotalPages = result.TotalPages > 0 ? result.TotalPages : 1;

            // Convert to CrafInfo-equivalent listings
            RemoteCrafts = result.Crafts;
            OnCraftsUpdated?.Invoke();
        }

        // ── Navigation ───────────────────────────────────────────────────

        /// <summary>
        /// Goes to the next page of results.
        /// </summary>
        public void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                Refresh();
            }
        }

        /// <summary>
        /// Goes to the previous page of results.
        /// </summary>
        public void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                Refresh();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private string GetKspRoot()
        {
            string modPath = KSPCraftManager.ModPath;
            string gameData = Path.GetDirectoryName(modPath);
            return Path.GetDirectoryName(gameData) ?? modPath;
        }

        private string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).TrimEnd('.');
        }
    }
}
