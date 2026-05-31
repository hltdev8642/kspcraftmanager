using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Renders a detailed information panel for a selected craft.
    /// Shows name, author, description, part count, mass, cost, size,
    /// crew capacity, part list, and mod dependencies.
    /// </summary>
    public class CraftDetailsPanel
    {
        private static CraftDetailsPanel _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static CraftDetailsPanel Instance => _instance ?? (_instance = new CraftDetailsPanel());

        /// <summary>
        /// Currently selected craft for details display.
        /// </summary>
        public CraftInfo SelectedCraft { get; set; }

        /// <summary>
        /// Whether the part list is expanded.
        /// </summary>
        public bool ShowPartList { get; set; }

        /// <summary>
        /// Whether the mod dependencies list is expanded.
        /// </summary>
        public bool ShowModDeps { get; set; }

        /// <summary>
        /// Scroll position for the details panel.
        /// </summary>
        public Vector2 ScrollPosition { get; set; }

        /// <summary>
        /// Scroll position for the part list.
        /// </summary>
        public Vector2 PartListScroll { get; set; }

        private readonly GUIStyle _labelStyle;
        private readonly GUIStyle _valueStyle;
        private readonly GUIStyle _headerStyle;
        private readonly GUIStyle _sectionStyle;
        private readonly GUIStyle _tagStyle;

        // Stats cache for part grouping
        private Dictionary<string, int> _groupedParts;
        private bool _partsDirty = true;

        private CraftDetailsPanel()
        {
            _labelStyle = new GUIStyle
            {
                normal = { textColor = Color.gray },
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 1, 1)
            };

            _valueStyle = new GUIStyle
            {
                normal = { textColor = Color.white },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 1, 1)
            };

            _headerStyle = new GUIStyle
            {
                normal = { textColor = new Color(1f, 0.8f, 0.2f) },
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 2, 3, 3)
            };

            _sectionStyle = new GUIStyle
            {
                normal = { textColor = new Color(0.5f, 0.8f, 1f) },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(2, 2, 3, 1)
            };

            _tagStyle = new GUIStyle
            {
                normal = { textColor = new Color(0.4f, 0.9f, 0.4f), background = MakeBackground(new Color(0.1f, 0.3f, 0.1f)) },
                fontSize = 11,
                padding = new RectOffset(4, 4, 1, 1),
                margin = new RectOffset(2, 2, 1, 1)
            };
        }

        /// <summary>
        /// Selects a craft for detailed display.
        /// </summary>
        public void SelectCraft(CraftInfo craft)
        {
            if (SelectedCraft != craft)
            {
                SelectedCraft = craft;
                _partsDirty = true;
                ScrollPosition = Vector2.zero;
                PartListScroll = Vector2.zero;
                ShowPartList = false;
                ShowModDeps = false;
            }
        }

        /// <summary>
        /// Draws the details panel within the specified rectangle.
        /// </summary>
        public void Draw(Rect panelRect)
        {
            if (SelectedCraft == null)
            {
                GUI.Label(panelRect, "Select a craft to view details", new GUIStyle
                {
                    normal = { textColor = Color.gray },
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter
                });
                return;
            }

            GUILayout.BeginArea(panelRect, new GUIStyle { padding = new RectOffset(6, 6, 4, 4) });
            ScrollPosition = GUILayout.BeginScrollView(ScrollPosition);

            // ── Craft Name ──
            GUILayout.Label(SelectedCraft.Name, _headerStyle);

            // ── Source indicator ──
            if (SelectedCraft.Source == CraftSource.KerbalX)
            {
                GUILayout.Label($"From KerbalX by {SelectedCraft.KerbalXAuthor}", new GUIStyle(_labelStyle)
                {
                    normal = { textColor = new Color(0.3f, 0.7f, 1f) }
                });
            }

            GUILayout.Space(4);

            // ── Metadata Grid ──
            DrawMetadataRow("Type", SelectedCraft.Type);
            DrawMetadataRow("Parts", SelectedCraft.PartCount.ToString("N0"));
            DrawMetadataRow("Mass (wet)", SelectedCraft.TotalMass.ToString("F2") + " t");
            DrawMetadataRow("Mass (dry)", SelectedCraft.DryMass.ToString("F2") + " t");
            DrawMetadataRow("Cost", "₧" + SelectedCraft.TotalCost.ToString("N0"));
            DrawMetadataRow("Size", $"{SelectedCraft.Size.x:F1} x {SelectedCraft.Size.y:F1} x {SelectedCraft.Size.z:F1} m");
            DrawMetadataRow("Crew Capacity", SelectedCraft.CrewCapacity.ToString());
            DrawMetadataRow("KSP Version", SelectedCraft.KspVersion ?? "Unknown");

            if (SelectedCraft.Source == CraftSource.KerbalX)
            {
                DrawMetadataRow("Downloads", SelectedCraft.KerbalXDownloads.ToString("N0"));
                DrawMetadataRow("Likes", SelectedCraft.KerbalXLikes.ToString("N0"));
                DrawMetadataRow("Rating", SelectedCraft.KerbalXRating.ToString("F1") + " / 5");
            }

            // ── Description ──
            if (!string.IsNullOrEmpty(SelectedCraft.Description))
            {
                GUILayout.Space(6);
                GUILayout.Label("Description", _sectionStyle);
                GUILayout.Label(SelectedCraft.Description, new GUIStyle(_labelStyle)
                {
                    wordWrap = true,
                    fontSize = 12
                });
            }

            // ── Tags ──
            if (SelectedCraft.Tags.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("Tags", _sectionStyle);
                GUILayout.BeginHorizontal();
                foreach (string tag in SelectedCraft.Tags.Take(10))
                {
                    GUILayout.Label(tag, _tagStyle);
                }
                if (SelectedCraft.Tags.Count > 10)
                {
                    GUILayout.Label($"+{SelectedCraft.Tags.Count - 10} more", _labelStyle);
                }
                GUILayout.EndHorizontal();
            }

            // ── Part List (expandable) ──
            GUILayout.Space(6);
            if (GUILayout.Button($"Parts ({SelectedCraft.PartCount})", GUILayout.Height(22)))
            {
                ShowPartList = !ShowPartList;
                if (ShowPartList) BuildPartGroups();
            }

            if (ShowPartList)
            {
                PartListScroll = GUILayout.BeginScrollView(PartListScroll, GUILayout.Height(150));
                if (_groupedParts != null)
                {
                    foreach (var kvp in _groupedParts.OrderByDescending(k => k.Value))
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"  {kvp.Key}", _labelStyle);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"x{kvp.Value}", _valueStyle);
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndScrollView();
            }

            // ── Mod Dependencies (expandable) ──
            if (SelectedCraft.RequiredMods.Count > 0)
            {
                GUILayout.Space(4);
                if (GUILayout.Button($"Mod Dependencies ({SelectedCraft.RequiredMods.Count})", GUILayout.Height(22)))
                {
                    ShowModDeps = !ShowModDeps;
                }

                if (ShowModDeps)
                {
                    foreach (string mod in SelectedCraft.RequiredMods)
                    {
                        bool isInstalled = ModDependencyChecker.Instance.IsModInstalled(mod);
                        Color modColor = isInstalled ? new Color(0.4f, 0.9f, 0.4f) : new Color(1f, 0.4f, 0.4f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"  {mod}", new GUIStyle(_labelStyle) { normal = { textColor = modColor } });
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(isInstalled ? "✓ Installed" : "✗ Missing", _labelStyle);
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws a key-value metadata row.
        /// </summary>
        private void DrawMetadataRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", _labelStyle, GUILayout.Width(110));
            GUILayout.Label(value, _valueStyle);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Groups parts by name and counts occurrences.
        /// </summary>
        private void BuildPartGroups()
        {
            _groupedParts = new Dictionary<string, int>();
            foreach (string partName in SelectedCraft.PartNames)
            {
                if (_groupedParts.ContainsKey(partName))
                    _groupedParts[partName]++;
                else
                    _groupedParts[partName] = 1;
            }
            _partsDirty = false;
        }

        /// <summary>
        /// Creates a 1x1 texture for use as a GUI background.
        /// </summary>
        private Texture2D MakeBackground(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
