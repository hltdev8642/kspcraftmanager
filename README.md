# KSP Craft Manager

A powerful, feature-rich craft loader and manager for Kerbal Space Program 1.12.x.
Surpasses KerbalXMod in functionality, offering an integrated in-game GUI for
browsing, organizing, and managing both local and KerbalX-hosted crafts.

---

## ✨ Features

### 📂 Craft Management
- **Local Craft Browser** — Tree-view navigation of `Ships/VAB/` and `Ships/SPH/` directories with thumbnails, sorting by name, date, or file size
- **KerbalX Remote Browser** — Browse your KerbalX hangar, search public crafts, view stats (downloads, likes, rating)
- **Favorites System** — Star/favorite crafts across local and remote sources; dedicated Favorites tab
- **Tagging System** — Assign custom tags (e.g. "Lifter", "Duna", "SSTO") with full auto-generation from craft metadata
- **Folder Management** — Create, rename, delete subfolders from the UI; move crafts between folders
- **Version History** — Automatic snapshots when crafts change; browse and restore previous versions
- **Recently Opened** — Quick-access list of recently loaded/edited crafts

### 🎨 UI/UX
- **Stock Toolbar Integration** — `ApplicationLauncher` button with Blizzy's Toolbar support (optional)
- **Tabbed Main Window** — Resizable, draggable window with Local / KerbalX / Favorites / Search tabs
- **Thumbnail Generation** — Auto-generated craft previews with procedural fallbacks
- **Search & Filter** — Real-time filtering by name, part name, tag, author; filter by part count, mass, cost ranges
- **Sort Controls** — Sort by name, part count, mass, cost, date modified, or KerbalX popularity
- **Grid / List View Toggle** — Switch between thumbnail grid and compact list view

### 🌐 KerbalX Integration
- **Full API Sync** — Authenticate with KerbalX API key; browse your hangar or search public crafts
- **One-Click Download** — Download crafts from KerbalX directly into the correct Ships folder
- **KerbalX Search** — Search public crafts by query, type, part count range; sort by downloads, likes, or rating

### 📋 Metadata Display
- **Craft Details Panel** — Sidebar showing name, author, description, part count, mass (dry/wet), cost, size, crew capacity
- **Part List** — Expandable grouped part list with quantities
- **Mod Dependency Detection** — Scans craft for mod-origin parts; shows which mods are installed vs missing

### ⚡ Quality of Life
- **One-Click Load** — Load craft directly into VAB or SPH (bypasses stock load dialog)
- **Craft Duplicate** — Duplicate a craft with a new name
- **Craft Diff / Comparison** — Side-by-side comparison of two crafts (stats, parts, differences)
- **Mod Dependency Checker** — On-demand check showing all mod requirements with install status
- **Export / Share** — Export craft with metadata JSON sidecar for sharing

### ⚙️ Settings
- **Full Settings Window** — Tabbed UI: General (sync, sorting), KerbalX (API key), Display (thumbnails, layout)
- **Persistent Configuration** — All settings saved via ConfigNode to `settings.cfg`

---

## 📦 Installation

1. **Download** the latest release from [GitHub Releases](https://github.com/your-repo/KSPCraftManager/releases)
2. **Extract** the `GameData/KSPCraftManager/` folder into your KSP `GameData/` directory
3. **Launch KSP** — the mod will appear as a toolbar button (green crosshair icon)
4. **Configure** — click the toolbar button to open the main window, then ⚙ for settings

### Requirements
- **Kerbal Space Program 1.12.x** (1.10+ should work but untested)
- **Blizzy's Toolbar** (optional, for toolbar integration)
- **AVC (Add-on Version Checker)** (optional, for update notifications)

---

## 🔧 Building from Source

### Prerequisites
- Visual Studio 2019+ or JetBrains Rider
- .NET Framework 4.8 SDK
- KSP 1.12.x installation with managed assemblies

### Setup
1. Clone the repository
2. Open `KSPCraftManager.csproj` in your IDE
3. Update the KSP assembly reference paths in the `.csproj` to point to your KSP installation:
   ```xml
   <HintPath>..\..\..\KSP_Data\Managed\Assembly-CSharp.dll</HintPath>
   ```
4. Build (Debug or Release) — the DLL will be copied to `GameData/KSPCraftManager/Plugins/` automatically

### Project Structure
```
KSPCraftManager/
├── GameData/KSPCraftManager/
│   ├── Plugins/
│   │   └── KSPCraftManager.dll
│   ├── Textures/
│   │   └── toolbar_icon.png (optional, 32x32 PNG)
│   ├── Sounds/
│   └── KSPCraftManager.version
├── src/
│   ├── KSPCraftManager.cs        — Main entry point / KSPAddon
│   ├── IManager.cs               — Manager lifecycle interface
│   ├── ToolbarManager.cs         — Stock + Blizzy's toolbar
│   ├── SettingsManager.cs        — ConfigNode settings persistence
│   ├── SettingsWindow.cs         — Settings GUI window
│   ├── CraftDataManager.cs       — Local craft scanning & parsing
│   ├── CraftCache.cs             — Thumbnail generation & caching
│   ├── FavoritesManager.cs       — Favorites system
│   ├── TagManager.cs             — Tagging with auto-generation
│   ├── KerbalXAPI.cs             — KerbalX REST API client
│   ├── KerbalXBrowser.cs         — KerbalX browsing UI logic
│   ├── CraftDetailsPanel.cs      — Craft details display panel
│   ├── CraftComparer.cs          — Side-by-side craft comparison
│   ├── VersionHistory.cs         — Version tracking & restore
│   ├── ModDependencyChecker.cs   — Mod dependency detection
│   └── CraftManagerGUI.cs        — Main GUI window
├── KSPCraftManager.csproj
└── README.md
```

---

## 🎮 Usage

### Opening the Manager
Click the toolbar button (crosshair icon) in the stock toolbar, or press the configured hotkey.

### Tabs
| Tab | Description |
|-----|-------------|
| **Local** | Browse all crafts in `Ships/VAB/` and `Ships/SPH/` |
| **KerbalX** | Browse your KerbalX hangar or search public crafts |
| **Favorites** | View only your favorited crafts |
| **Search** | Search across all sources |

### Actions
- **Left-click** a craft to select it and view details
- **Double-click** a craft to load it into the editor
- **Right-click** a craft for the context menu (Load, Duplicate, Compare, Export, Favorite, Tags, Check Mods, Move, Delete)
- **Drag** the window title bar to reposition
- **Resize** the window by dragging any edge/corner

### KerbalX Setup
1. Go to Settings → KerbalX tab
2. Enter your KerbalX API key (get it from https://kerbalx.com/settings/api)
3. Click "Save" — the KerbalX tab will now show your crafts

---

## 🧩 Compatibility

- **KSP 1.12.x** — Fully supported
- **KSP 1.10–1.11** — Likely compatible (untested)
- **Blizzy's Toolbar** — Optional; detected automatically
- **CKAN** — Listing planned
- **AVC** — Version file included

---

## 📝 License

This project is licensed under the MIT License. See the LICENSE file for details.

---

## 🙏 Credits

- **Squad** for making KSP
- **KerbalX** for the craft sharing platform and API
- **Community** for feedback and inspiration
