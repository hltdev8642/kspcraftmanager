using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using MiniJSON;

namespace KSPCraftManager
{
    /// <summary>
    /// Represents a craft listing from the KerbalX API.
    /// </summary>
    public class KerbalXCraftListing
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string CraftType { get; set; } // "rocket" or "spaceplane"
        public int Downloads { get; set; }
        public int Likes { get; set; }
        public float Rating { get; set; }
        public int PartCount { get; set; }
        public float Mass { get; set; }
        public float Cost { get; set; }
        public string KspVersion { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedAt { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string DownloadUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public string KerbalxUrl { get; set; }
    }

    /// <summary>
    /// Result of a KerbalX API search or listing request.
    /// </summary>
    public class KerbalXResult
    {
        public List<KerbalXCraftListing> Crafts { get; set; } = new List<KerbalXCraftListing>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// HTTP client for the KerbalX REST API.
    /// Provides methods for authentication, listing, searching, downloading crafts,
    /// and managing the user's hangar.
    /// 
    /// KerbalX API base: https://kerbalx.com/api/
    /// Documentation: https://kerbalx.com/api/docs
    /// </summary>
    public class KerbalXAPI : IManager
    {
        private static KerbalXAPI _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static KerbalXAPI Instance => _instance ?? (_instance = new KerbalXAPI());

        private const string ApiBaseUrl = "https://kerbalx.com/api/";
        private const string ApiDocsUrl = "https://kerbalx.com/api/docs";
        private const int RequestTimeoutSeconds = 30;

        private string _apiKey;
        private bool _isAuthenticated;
        private MonoBehaviour _coroutineHost;

        /// <summary>
        /// Fired when authentication state changes.
        /// </summary>
        public event Action<bool> OnAuthStateChanged;

        /// <summary>
       /// Fired when a KerbalX API request completes.
        /// </summary>
        public event Action<string, KerbalXResult> OnRequestCompleted;

        /// <summary>
        /// Whether the user is authenticated with KerbalX.
        /// </summary>
        public bool IsAuthenticated => _isAuthenticated && !string.IsNullOrEmpty(_apiKey);

        /// <summary>
        /// The current API key in use.
        /// </summary>
        public string ApiKey => _apiKey;

        private KerbalXAPI() { }

        /// <summary>
        /// Loads the API key from settings.
        /// </summary>
        public void Initialize()
        {
            _apiKey = SettingsManager.Instance.KerbalXApiKey;
            _isAuthenticated = !string.IsNullOrEmpty(_apiKey);
            _coroutineHost = KSPCraftManager.Instance;

            Debug.Log($"[KSPCraftManager] KerbalXAPI initialized. Authenticated: {_isAuthenticated}");
        }

        /// <summary>
        /// Cleanup.
        /// </summary>
        public void Shutdown()
        {
            // Nothing to clean up for HTTP
        }

        // ── Authentication ───────────────────────────────────────────────

        /// <summary>
        /// Sets the API key and persists it to settings.
        /// </summary>
        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
            _isAuthenticated = !string.IsNullOrEmpty(_apiKey);
            SettingsManager.Instance.KerbalXApiKey = _apiKey;
            SettingsManager.Instance.Save();
            OnAuthStateChanged?.Invoke(_isAuthenticated);
        }

        /// <summary>
        /// Clears the current API key and deauthenticates.
        /// </summary>
        public void ClearApiKey()
        {
            SetApiKey(string.Empty);
        }

        // ── API Requests ─────────────────────────────────────────────────

        /// <summary>
        /// Fetches the user's own crafts from their KerbalX hangar.
        /// </summary>
        public void FetchMyCrafts(int page = 1, int perPage = 50)
        {
            _coroutineHost.StartCoroutine(RequestMyCrafts(page, perPage));
        }

        /// <summary>
        /// Searches public crafts on KerbalX.
        /// </summary>
        public void SearchCrafts(string query, int page = 1, int perPage = 50,
            string craftType = null, int? minParts = null, int? maxParts = null,
            string sort = "downloads")
        {
            _coroutineHost.StartCoroutine(RequestSearch(query, page, perPage, craftType, minParts, maxParts, sort));
        }

        /// <summary>
        /// Fetches details for a specific craft by ID.
        /// </summary>
        public void FetchCraftDetails(string craftId)
        {
            _coroutineHost.StartCoroutine(RequestCraftDetails(craftId));
        }

        /// <summary>
        /// Downloads a .craft file from KerbalX.
        /// </summary>
        public void DownloadCraft(string downloadUrl, string savePath, Action<bool, string> callback)
        {
            _coroutineHost.StartCoroutine(RequestDownload(downloadUrl, savePath, callback));
        }

        // ── Coroutine Implementations ────────────────────────────────────

        /// <summary>
        /// Requests the user's own crafts.
        /// </summary>
        private IEnumerator RequestMyCrafts(int page, int perPage)
        {
            string url = $"{ApiBaseUrl}crafts?page={page}&per_page={perPage}";
            using (UnityWebRequest req = CreateRequest(url))
            {
                yield return req.SendWebRequest();

                KerbalXResult result = ProcessResponse(req);
                OnRequestCompleted?.Invoke("my_crafts", result);
            }
        }

        /// <summary>
        /// Searches public crafts.
        /// </summary>
        private IEnumerator RequestSearch(string query, int page, int perPage,
            string craftType, int? minParts, int? maxParts, string sort)
        {
            StringBuilder urlBuilder = new StringBuilder();
            urlBuilder.Append($"{ApiBaseUrl}crafts/search?q={UnityWebRequest.EscapeURL(query)}");
            urlBuilder.Append($"&page={page}&per_page={perPage}");

            if (!string.IsNullOrEmpty(craftType))
                urlBuilder.Append($"&type={craftType}");

            if (minParts.HasValue)
                urlBuilder.Append($"&min_parts={minParts.Value}");

            if (maxParts.HasValue)
                urlBuilder.Append($"&max_parts={maxParts.Value}");

            if (!string.IsNullOrEmpty(sort))
                urlBuilder.Append($"&sort={sort}");

            using (UnityWebRequest req = CreateRequest(urlBuilder.ToString()))
            {
                yield return req.SendWebRequest();

                KerbalXResult result = ProcessResponse(req);
                OnRequestCompleted?.Invoke("search", result);
            }
        }

        /// <summary>
        /// Fetches details for a specific craft.
        /// </summary>
        private IEnumerator RequestCraftDetails(string craftId)
        {
            string url = $"{ApiBaseUrl}crafts/{craftId}";
            using (UnityWebRequest req = CreateRequest(url))
            {
                yield return req.SendWebRequest();

                KerbalXResult result = ProcessSingleCraftResponse(req, craftId);
                OnRequestCompleted?.Invoke("craft_details", result);
            }
        }

        /// <summary>
        /// Downloads a craft file and saves it to disk.
        /// </summary>
        private IEnumerator RequestDownload(string downloadUrl, string savePath, Action<bool, string> callback)
        {
            // The download URL might be relative or need the API base
            string fullUrl = downloadUrl.StartsWith("http") ? downloadUrl : ApiBaseUrl.TrimEnd('/') + downloadUrl;

            using (UnityWebRequest req = UnityWebRequest.Get(fullUrl))
            {
                req.timeout = RequestTimeoutSeconds;
                if (!string.IsNullOrEmpty(_apiKey))
                    req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

                yield return req.SendWebRequest();

                if (!req.isNetworkError && !req.isHttpError)
                {
                    try
                    {
                        byte[] data = req.downloadHandler.data;
                        File.WriteAllBytes(savePath, data);
                        callback?.Invoke(true, savePath);
                    }
                    catch (Exception ex)
                    {
                        callback?.Invoke(false, $"Failed to write file: {ex.Message}");
                    }
                }
                else
                {
                    callback?.Invoke(false, req.error);
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Creates a UnityWebRequest with common headers.
        /// </summary>
        private UnityWebRequest CreateRequest(string url)
        {
            UnityWebRequest req = UnityWebRequest.Get(url);
            req.timeout = RequestTimeoutSeconds;
            req.SetRequestHeader("Accept", "application/json");
            req.SetRequestHeader("User-Agent", "KSPCraftManager/1.0");

            if (!string.IsNullOrEmpty(_apiKey))
                req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

            Debug.Log($"[KSPCraftManager] KerbalX request: GET {url}");
            return req;
        }

        /// <summary>
        /// Processes a JSON response from the KerbalX API using MiniJSON.
        /// </summary>
        private KerbalXResult ProcessResponse(UnityWebRequest req)
        {
            KerbalXResult result = new KerbalXResult();

            if (req.isNetworkError || req.isHttpError)
            {
                result.Success = false;
                result.ErrorMessage = req.error;

                // Try to read response body for more details (usually JSON error)
                string body = req.downloadHandler?.text;
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var errJson = Json.Deserialize(body) as Dictionary<string, object>;
                        if (errJson != null && errJson.TryGetValue("error", out object errMsg))
                            result.ErrorMessage = errMsg.ToString();
                    }
                    catch { }
                    Debug.LogWarning($"[KSPCraftManager] KerbalX API error: {req.error} | Body: {body}");
                }
                else
                {
                    Debug.LogWarning($"[KSPCraftManager] KerbalX API error: {req.error}");
                }
                return result;
            }

            try
            {
                string jsonText = req.downloadHandler.text;
                Dictionary<string, object> json = Json.Deserialize(jsonText) as Dictionary<string, object>;
                if (json == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Invalid JSON response from KerbalX";
                    Debug.LogWarning($"[KSPCraftManager] KerbalX invalid JSON: {jsonText?.Substring(0, Mathf.Min(200, jsonText?.Length ?? 0))}");
                    return result;
                }

                result.Success = true;

                // Parse craft listings
                if (json.TryGetValue("crafts", out object craftsObj))
                {
                    List<object> craftsList = craftsObj as List<object>;
                    if (craftsList != null)
                    {
                        foreach (object item in craftsList)
                        {
                            Dictionary<string, object> craftDict = item as Dictionary<string, object>;
                            if (craftDict != null)
                            {
                                KerbalXCraftListing listing = ParseCraftListing(craftDict);
                                if (listing != null)
                                    result.Crafts.Add(listing);
                            }
                        }
                    }
                }

                // Parse pagination
                if (json.TryGetValue("total_count", out object tc)) result.TotalCount = Convert.ToInt32(tc);
                if (json.TryGetValue("current_page", out object cp)) result.CurrentPage = Convert.ToInt32(cp);
                if (json.TryGetValue("total_pages", out object tp)) result.TotalPages = Convert.ToInt32(tp);

                // Handle single-craft response
                if (result.Crafts.Count == 0 && json.ContainsKey("id"))
                {
                    KerbalXCraftListing single = ParseCraftListing(json);
                    if (single != null)
                        result.Crafts.Add(single);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Parse error: {ex.Message}";
                Debug.LogWarning($"[KSPCraftManager] KerbalX response parse error: {ex}");
            }

            return result;
        }

        /// <summary>
        /// Processes a single-craft detail response.
        /// </summary>
        private KerbalXResult ProcessSingleCraftResponse(UnityWebRequest req, string craftId)
        {
            KerbalXResult result = new KerbalXResult();

            if (req.isNetworkError || req.isHttpError)
            {
                result.Success = false;
                result.ErrorMessage = req.error;
                return result;
            }

            try
            {
                string jsonText = req.downloadHandler.text;
                Dictionary<string, object> json = Json.Deserialize(jsonText) as Dictionary<string, object>;
                if (json == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Invalid JSON response";
                    return result;
                }

                result.Success = true;

                KerbalXCraftListing listing = ParseCraftListing(json);
                if (listing != null)
                {
                    result.Crafts.Add(listing);
                    result.TotalCount = 1;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Parse error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Parses a single craft listing from a JSON dictionary using MiniJSON.
        /// </summary>
        private KerbalXCraftListing ParseCraftListing(Dictionary<string, object> json)
        {
            try
            {
                KerbalXCraftListing listing = new KerbalXCraftListing
                {
                    Id = SafeGetString(json, "id"),
                    Name = SafeGetString(json, "name"),
                    Author = SafeGetString(json, "author"),
                    Description = SafeGetString(json, "description"),
                    CraftType = SafeGetString(json, "craft_type"),
                    DownloadUrl = SafeGetString(json, "download_url"),
                    ThumbnailUrl = SafeGetString(json, "thumbnail_url"),
                    KerbalxUrl = SafeGetString(json, "kerbalx_url"),
                    KspVersion = SafeGetString(json, "ksp_version"),
                    UpdatedAt = SafeGetString(json, "updated_at"),
                    CreatedAt = SafeGetString(json, "created_at"),
                    Downloads = SafeGetInt(json, "downloads"),
                    Likes = SafeGetInt(json, "likes"),
                    Rating = SafeGetFloat(json, "rating"),
                    PartCount = SafeGetInt(json, "part_count"),
                    Mass = SafeGetFloat(json, "mass"),
                    Cost = SafeGetFloat(json, "cost")
                };

                // Parse tags
                if (json.TryGetValue("tags", out object tagsObj) && tagsObj is List<object> tagsList)
                {
                    foreach (object tagItem in tagsList)
                    {
                        string tag = tagItem?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(tag))
                            listing.Tags.Add(tag);
                    }
                }

                return listing;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KSPCraftManager] Failed to parse KerbalX listing: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Safely gets a string value from a JSON dictionary.
        /// </summary>
        private string SafeGetString(Dictionary<string, object> json, string key)
        {
            if (json.TryGetValue(key, out object val) && val != null)
                return val.ToString();
            return string.Empty;
        }

        /// <summary>
        /// Safely gets an int value from a JSON dictionary.
        /// </summary>
        private int SafeGetInt(Dictionary<string, object> json, string key)
        {
            if (json.TryGetValue(key, out object val) && val != null)
            {
                // MiniJSON may return long or double
                if (val is int iv) return iv;
                if (val is long lv) return (int)lv;
                if (val is double dv) return (int)dv;
                if (int.TryParse(val.ToString(), out int parsed)) return parsed;
            }
            return 0;
        }

        /// <summary>
        /// Safely gets a float value from a JSON dictionary.
        /// </summary>
        private float SafeGetFloat(Dictionary<string, object> json, string key)
        {
            if (json.TryGetValue(key, out object val) && val != null)
            {
                if (val is float fv) return fv;
                if (val is double dv) return (float)dv;
                if (float.TryParse(val.ToString(), out float parsed)) return parsed;
            }
            return 0f;
        }
    }
}
