using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSPCraftManager
{
    /// <summary>
    /// Provides side-by-side comparison of two crafts.
    /// Compares part counts, mass, cost, parts list, and metadata.
    /// </summary>
    public class CraftComparer
    {
        private static CraftComparer _instance;
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static CraftComparer Instance => _instance ?? (_instance = new CraftComparer());

        /// <summary>
        /// First craft for comparison.
        /// </summary>
        public CraftInfo CraftA { get; set; }

        /// <summary>
        /// Second craft for comparison.
        /// </summary>
        public CraftInfo CraftB { get; set; }

        /// <summary>
        /// Whether the comparison window is visible.
        /// </summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Scroll position for the comparison view.
        /// </summary>
        public Vector2 ScrollPosition { get; set; }

        // Comparison result cache
        private ComparisonResult _cachedResult;

        private CraftComparer() { }

        /// <summary>
        /// Sets the two crafts to compare opens the comparison view.
        /// </summary>
        public void Compare(CraftInfo a, CraftInfo b)
        {
            CraftA = a;
            CraftB = b;
            _cachedResult = null;
            IsVisible = true;
            ScrollPosition = Vector2.zero;
        }

        /// <summary>
        /// Closes the comparison view.
        /// </summary>
        public void Close()
        {
            IsVisible = false;
            CraftA = null;
            CraftB = null;
        }

        /// <summary>
        /// Draws the comparison UI within the given area.
        /// </summary>
        public void Draw(Rect area)
        {
            if (CraftA == null || CraftB == null)
            {
                GUI.Label(area, "Select two crafts to compare", new GUIStyle
                {
                    normal = { textColor = Color.gray },
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                });
                return;
            }

            if (_cachedResult == null)
                _cachedResult = ComputeComparison();

            GUILayout.BeginArea(area);
            ScrollPosition = GUILayout.BeginScrollView(ScrollPosition);

            DrawHeader();

            DrawDivider();

            DrawStatComparison();

            DrawDivider();

            DrawPartComparison();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws the comparison header with craft names.
        /// </summary>
        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();

            // Craft A
            GUILayout.BeginVertical(GUILayout.Width(areaHalfWidth));
            GUILayout.Label(CraftA.Name, new GUIStyle
            {
                normal = { textColor = new Color(0.4f, 0.8f, 1f) },
                fontSize = 14,
                fontStyle = FontStyle.Bold
            });
            if (CraftA.Source == CraftSource.KerbalX)
                GUILayout.Label($"by {CraftA.KerbalXAuthor}", new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });
            GUILayout.EndVertical();

            // VS
            GUILayout.Label(" VS ", new GUIStyle
            {
                normal = { textColor = new Color(1f, 0.8f, 0.2f) },
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            });

            // Craft B
            GUILayout.BeginVertical(GUILayout.Width(areaHalfWidth));
            GUILayout.Label(CraftB.Name, new GUIStyle
            {
                normal = { textColor = new Color(1f, 0.6f, 0.4f) },
                fontSize = 14,
                fontStyle = FontStyle.Bold
            });
            if (CraftB.Source == CraftSource.KerbalX)
                GUILayout.Label($"by {CraftB.KerbalXAuthor}", new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws a divider line.
        /// </summary>
        private void DrawDivider()
        {
            GUILayout.Space(4);
            GUILayout.Box("", new GUIStyle
            {
                normal = { background = MakeTexture(1, 1, new Color(0.3f, 0.3f, 0.3f)) },
                fixedHeight = 1,
                stretchWidth = true
            });
            GUILayout.Space(4);
        }

        /// <summary>
        /// Draws the numeric stat comparison rows.
        /// </summary>
        private void DrawStatComparison()
        {
            GUILayout.Label("📊 Statistics", labelStyle(Color.white));

            if (_cachedResult == null) return;

            foreach (ComparisonRow row in _cachedResult.Rows)
            {
                DrawComparisonRow(row);
            }
        }

        /// <summary>
        /// Draws a single comparison row (label, value A, value B).
        /// </summary>
        private void DrawComparisonRow(ComparisonRow row)
        {
            GUILayout.BeginHorizontal();

            // Label
            GUILayout.Label(row.Label, new GUIStyle
            {
                normal = { textColor = Color.gray },
                fontSize = 12,
                fixedWidth = 120
            });

            // Value A
            GUIStyle styleA = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = GetDiffColor(row.DiffType, true) },
                alignment = TextAnchor.MiddleRight
            };
            GUILayout.Label(row.ValueA, styleA, GUILayout.Width(areaHalfWidth - 120));

            // Value B
            GUIStyle styleB = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = GetDiffColor(row.DiffType, false) },
                alignment = TextAnchor.MiddleLeft
            };
            GUILayout.Label(row.ValueB, styleB, GUILayout.Width(areaHalfWidth - 120));

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws the part list comparison.
        /// </summary>
        private void DrawPartComparison()
        {
            GUILayout.Space(8);
            GUILayout.Label("🔧 Parts Comparison", labelStyle(Color.white));

            // Parts unique to A
            if (_cachedResult?.OnlyInA.Count > 0)
            {
                GUILayout.Label($"Only in {CraftA.Name}:", labelStyle(new Color(0.4f, 0.8f, 1f)));
                foreach (string part in _cachedResult.OnlyInA.Take(20))
                    GUILayout.Label($"  + {part}", new GUIStyle { normal = { textColor = new Color(0.4f, 0.8f, 0.4f) }, fontSize = 11 });
                if (_cachedResult.OnlyInA.Count > 20)
                    GUILayout.Label($"  ... and {_cachedResult.OnlyInA.Count - 20} more", labelStyle(Color.gray));
            }

            // Parts unique to B
            if (_cachedResult?.OnlyInB.Count > 0)
            {
                GUILayout.Label($"Only in {CraftB.Name}:", labelStyle(new Color(1f, 0.6f, 0.4f)));
                foreach (string part in _cachedResult.OnlyInB.Take(20))
                    GUILayout.Label($"  + {part}", new GUIStyle { normal = { textColor = new Color(1f, 0.6f, 0.6f) }, fontSize = 11 });
                if (_cachedResult.OnlyInB.Count > 20)
                    GUILayout.Label($"  ... and {_cachedResult.OnlyInB.Count - 20} more", labelStyle(Color.gray));
            }

            // Common parts
            if (_cachedResult?.CommonParts.Count > 0)
            {
                GUILayout.Label($"Common parts ({_cachedResult.CommonParts.Count}):", labelStyle(Color.gray));
                foreach (string part in _cachedResult.CommonParts.Take(15))
                    GUILayout.Label($"  ✓ {part}", new GUIStyle { normal = { textColor = Color.gray }, fontSize = 11 });
                if (_cachedResult.CommonParts.Count > 15)
                    GUILayout.Label($"  ... and {_cachedResult.CommonParts.Count - 15} more", labelStyle(Color.gray));
            }
        }

        /// <summary>
        /// Computes the full comparison between CraftA and CraftB.
        /// </summary>
        private ComparisonResult ComputeComparison()
        {
            ComparisonResult result = new ComparisonResult();

            // Stats rows
            result.Rows.Add(new ComparisonRow("Type", CraftA.Type, CraftB.Type, DiffType.None));
            result.Rows.Add(new ComparisonRow("Part Count", CraftA.PartCount.ToString("N0"), CraftB.PartCount.ToString("N0"),
                CompareValues(CraftA.PartCount, CraftB.PartCount)));
            result.Rows.Add(new ComparisonRow("Mass (wet)", CraftA.TotalMass.ToString("F2") + "t", CraftB.TotalMass.ToString("F2") + "t",
                CompareValues(CraftA.TotalMass, CraftB.TotalMass)));
            result.Rows.Add(new ComparisonRow("Mass (dry)", CraftA.DryMass.ToString("F2") + "t", CraftB.DryMass.ToString("F2") + "t",
                CompareValues(CraftA.DryMass, CraftB.DryMass)));
            result.Rows.Add(new ComparisonRow("Cost", "₧" + CraftA.TotalCost.ToString("N0"), "₧" + CraftB.TotalCost.ToString("N0"),
                CompareValues(CraftA.TotalCost, CraftB.TotalCost)));
            result.Rows.Add(new ComparisonRow("Crew", CraftA.CrewCapacity.ToString(), CraftB.CrewCapacity.ToString(),
                CompareValues(CraftA.CrewCapacity, CraftB.CrewCapacity)));
            result.Rows.Add(new ComparisonRow("Size", $"{CraftA.Size.x:F1}x{CraftA.Size.y:F1}x{CraftA.Size.z:F1}m",
                $"{CraftB.Size.x:F1}x{CraftB.Size.y:F1}x{CraftB.Size.z:F1}m", DiffType.None));
            result.Rows.Add(new ComparisonRow("Mods Required", CraftA.RequiredMods.Count.ToString(), CraftB.RequiredMods.Count.ToString(),
                CompareValues(CraftA.RequiredMods.Count, CraftB.RequiredMods.Count)));

            // Part set comparison
            HashSet<string> partsA = new HashSet<string>(CraftA.PartNames.Select(p => p.ToLowerInvariant()));
            HashSet<string> partsB = new HashSet<string>(CraftB.PartNames.Select(p => p.ToLowerInvariant()));

            result.OnlyInA = partsA.Except(partsB).OrderBy(p => p).ToList();
            result.OnlyInB = partsB.Except(partsA).OrderBy(p => p).ToList();
            result.CommonParts = partsA.Intersect(partsB).OrderBy(p => p).ToList();

            return result;
        }

        /// <summary>
        /// Determines the comparison difference type based on numeric values.
        /// </summary>
        private DiffType CompareValues(double a, double b)
        {
            if (Math.Abs(a - b) < 0.001) return DiffType.None;
            if (a > b) return DiffType.Higher;
            return DiffType.Lower;
        }

        /// <summary>
        /// Gets a color representing the difference relative to the other craft.
        /// </summary>
        private Color GetDiffColor(DiffType diff, bool isFirst)
        {
            switch (diff)
            {
                case DiffType.Higher:
                    return isFirst ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
                case DiffType.Lower:
                    return isFirst ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// Creates a helper label style.
        /// </summary>
        private GUIStyle labelStyle(Color color)
        {
            return new GUIStyle
            {
                normal = { textColor = color },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(2, 2, 2, 2)
            };
        }

        /// <summary>
        /// Creates a 1x1 texture of a given color.
        /// </summary>
        private Texture2D MakeTexture(int w, int h, Color c)
        {
            Texture2D tex = new Texture2D(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            tex.Apply();
            return tex;
        }

        private float areaHalfWidth => 340f;

        // ── Comparison Models ────────────────────────────────────────────

        private enum DiffType { None, Higher, Lower }

        private class ComparisonRow
        {
            public string Label { get; }
            public string ValueA { get; }
            public string ValueB { get; }
            public DiffType DiffType { get; }
            public ComparisonRow(string label, string a, string b, DiffType diff)
            {
                Label = label; ValueA = a; ValueB = b; DiffType = diff;
            }
        }

        private class ComparisonResult
        {
            public List<ComparisonRow> Rows { get; } = new List<ComparisonRow>();
            public List<string> OnlyInA { get; set; } = new List<string>();
            public List<string> OnlyInB { get; set; } = new List<string>();
            public List<string> CommonParts { get; set; } = new List<string>();
        }
    }
}
