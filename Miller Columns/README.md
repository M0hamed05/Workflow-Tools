# Miller Columns Explorer

A fast, lightweight, and modern **Miller Columns** (macOS Finder-style cascading columns) file explorer built natively for Windows 10 and 11 using **C# WPF (.NET 8)** and **Win32 Shell P/Invoke**.

![Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D7?logo=windows)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?logo=dotnet)
![Dependencies](https://img.shields.io/badge/Dependencies-Zero%20External%20Packages-success)
![Binary Size](<https://img.shields.io/badge/Executable-~280%20KB%20(Single--File)-blue>)

---

## ✨ Features

### 🗂️ Cascading Miller Columns

- Navigate nested folder hierarchies effortlessly across horizontal cascading columns.
- **Smooth Column Slide Animations:** Animated forward and backward column transitions powered by WPF cubic easing.
- **Smart Column Persistence:** Selecting files (single or multiple) leaves subsequent opened columns intact, allowing you to drag and drop files directly into folders opened in columns to the right.

### 🟦 Standard Windows Multi-Selection & Marquee

- **Rubber-band (Marquee) Drag Box:** Click and drag on empty space or row margins to draw the classic Windows semi-transparent blue selection box (`#0078D7`).
- **Standard Keyboard & Mouse Controls:** Full support for `Ctrl + Click` (toggle item), `Shift + Click` (range selection), and `Ctrl + A` (select all in active column).
- **Virtualization-Safe:** Selected states remain synchronized with the view models even when scrolling virtualized lists.
- **Explorer-Grade Drag Behavior:** Clicking an already-selected item among multiple items preserves the selection on mouse press, enabling seamless multi-item drag & drop.

### 🔄 Drag & Drop & Clipboard

- **Cross-Column Drag & Drop:** Drag single or multiple files/folders between columns, into specific folders, or into sidebar destinations.
- **Windows Explorer Interoperability:** Drag items to and from external Windows Explorer windows, Desktop, and third-party apps.
- **Full Clipboard Integration:**
  - `Ctrl + C`: Copy selected items.
  - `Ctrl + X`: Cut selected items.
  - `Ctrl + V`: Paste items into the active column or selected directory.

### ↩️ Ctrl + Z (Undo Support)

- **Tracked Undo Service:**
  - Reverses file move operations back to their original locations.
  - Safely deletes copied files by sending them to the Windows Recycle Bin using native `SHFileOperation` (`FO_DELETE` with `FOF_ALLOWUNDO`).
- **Windows Shell Fallback:** Automatically falls back to native Windows Shell Undo (`Shell.Application.Undo()`) when applicable.

### 📋 Native Windows Shell Context Menu

- Full right-click menu powered by `IShellFolder` and `IContextMenu` COM interfaces.
- Supports single and multiple file selections with all standard Windows options (Cut, Copy, Delete, Rename, Properties, Open with, etc.).
- Right-clicking empty space brings up the directory background menu (Paste, New Folder, etc.).
- Convenient **"Copy Path"** / **"Copy Paths"** menu option.

### 🎨 Dark & Light Theme with Auto-Detection

- Automatically adapts to the Windows system theme on first launch.
- Dynamic theme switching button with animated icon transitions.
- Fully styled scrollbars, hover states, selection accents, and contrast-adapted vector icons.

### 📂 Collapsible Sidebar

- Smooth sliding animation between full sidebar (220px) and minimized icon-only rail (50px).
- Quick access to **Favorites** (Home, Desktop, Documents, Downloads, Projects) and all mounted **Drives**.
- Sidebar collapse/expand state is remembered across app sessions.

### ⚡ Blazing Fast & Ultra-Lightweight

- **Zero Third-Party Dependencies:** Uses pure .NET 8 BCL and native Win32 APIs.
- Standalone single-file binary is under **300 KB**.
- Recycled UI virtualization ensures smooth 60 FPS scrolling even with directories containing tens of thousands of files.
- Asynchronous filesystem scanning and multi-stage icon extraction prevent UI thread freezes.

---

## ⌨️ Keyboard Shortcuts

| Shortcut        | Action                                         |
| :-------------- | :--------------------------------------------- |
| `Ctrl + C`      | Copy selected file(s) / folder(s)              |
| `Ctrl + X`      | Cut selected file(s) / folder(s)               |
| `Ctrl + V`      | Paste clipboard contents into active directory |
| `Ctrl + Z`      | Undo last file move or copy operation          |
| `Ctrl + A`      | Select all items in the active column          |
| `Ctrl + Click`  | Toggle selection for individual items          |
| `Shift + Click` | Select a continuous range of items             |
| `Double Click`  | Open file with default Windows application     |
| `Escape`        | Cancel active marquee (blue box) selection     |

---

## 🛠️ Requirements & Building

### Prerequisites

- Windows 10 (version 1809 or higher) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build Debug

```powershell
dotnet build
```

### Run

```powershell
dotnet run
```

### Publish Single-File Release Executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The resulting standalone executable will be located in the `publish` directory (~280 KB).

---

## 📁 Project Architecture

```
Miller Columns/
├── App.xaml / App.xaml.cs          # Application entry point and global resources
├── MainWindow.xaml / .cs           # Miller columns UI, animations, mouse drag & marquee
├── MillerColumns.csproj            # .NET 8 WPF project file
├── app.ico                         # Application icon
├── Models/
│   ├── FileItem.cs                 # File & directory data models
│   └── SessionData.cs              # Session state representation (paths, theme, sidebar)
├── ViewModels/
│   ├── ViewModelBase.cs            # INotifyPropertyChanged implementation
│   ├── MainViewModel.cs            # Primary coordinator, navigation & commands
│   ├── ColumnViewModel.cs          # Column state, async loading & FileSystemWatcher
│   ├── FileItemViewModel.cs        # Item representation & two-way selection binding
│   ├── SidebarItemViewModel.cs     # Favorites & drive entries
│   └── RelayCommand.cs             # ICommand implementation
└── Services/
    ├── FileUndoService.cs          # Ctrl+Z undo tracking & shell fallback
    ├── ShellContextMenu.cs        # Win32 IContextMenu & IShellFolder interop
    ├── IconHelper.cs               # Native Win32 SHGetFileInfo icon extraction & caching
    ├── ThemeService.cs             # DWM title bar theme sync & dark mode detection
    └── SessionService.cs           # State serialization to %APPDATA%
```
