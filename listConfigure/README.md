# Context Menu Studio (Windows 10 / 11)

A clean, modern Windows desktop application to manage and customize Windows right-click context menus, add custom cascading tool submenus (e.g., `code .`, `cmd`, `npx`), and manage unlimited registry restore points with 1-click recovery.

---

## 🌟 Key Features

### 1. Edit Context Menu (`Directory\shell` & `Directory\Background\shell`)
- **View All Options**: Scans and displays active context menu entries across both **Folders** and **Folder Background / Empty Space**.
- **Search & Filter**: Instantly search items by name, command line, or registry key. Filter by target scope.
- **Add Option**: Add standalone menu items with custom display titles, command strings, icons, and scope targeting.
- **Edit Option**: Modify existing item labels, targets, or icons.
- **Enable / Disable Toggle**: Safely disable an item without deleting it using Windows `LegacyDisable` flags.
- **Delete / Remove**: Remove unwanted context menu options cleanly.

### 2. Custom Cascading Submenu ("Custom Tools")
Solves the friction of constantly clicking the Explorer address bar, clearing the path, and typing `cmd` or `code .`.
- **Dedicated Submenu**: Creates a nested cascading right-click menu (e.g. `Custom Tools >`) containing all your favorite commands.
- **Scope Targeting**: Configured specifically to appear when **right-clicking folders** and **right-clicking empty background space inside folders** (`Directory\shell` and `Directory\Background\shell`).
- **Command Textbox & Presets**:
  - Type any command (e.g. `code .`, `cmd`, `powershell`, `npx create-react-app`, `npm run dev`, `git status`, `python -m http.server`).
  - Quick-pill preset buttons autofill commonly used commands with a single click.
- **"Run in CMD" Checkbox**:
  - **Checked**: Uses `cmd.exe /s /k "cd /d \"%V\" && <command>"` so the console window **stays open** to view logs, server output, and interact (ideal for `npx`, `python`, `npm`, `cmd`).
  - **Unchecked**: Uses `cmd.exe /s /c "cd /d \"%V\" && <command>"` to execute cleanly and close (ideal for launching GUI applications like `code .`).
- **Reordering & Management**: Move tools up/down, edit commands, and deploy to Windows with one click.
- **Live Preview Panel**: Shows a visual simulation of the right-click menu as you configure it.

### 3. Bulletproof Restore Points & Snapshots
- **Automatic Safety Backups**: Automatically snapshots your context menu registry state before *any* add, edit, delete, toggle, or deploy operation.
- **Unlimited Snapshots**: Create as many custom named restore points as you want.
- **1-Click Restore**: Safely restores registry keys to the exact chosen snapshot. Automatically creates a safety checkpoint before restoring.
- **Export as `.REG`**: Export any snapshot as a standard Windows `.reg` file for offline backup or sharing.

### 4. Windows Explorer Refresh
- Built-in **"Restart Explorer"** button restarts `explorer.exe` to flush Windows context menu cache so changes appear immediately.

---

## 🚀 How to Run

### Method 1: Using the Executable
Run the pre-compiled binary:
```powershell
.\publish\ContextMenuManager.exe
```

### Method 2: Using .NET CLI
From the project folder:
```powershell
dotnet run
```

### Method 3: Automated Verification Suite
Run the built-in automated test suite to verify registry modifications, cascading submenus, and restore points:
```powershell
dotnet run -- --test
```

---

## 📁 Technical Architecture
- **Framework**: .NET 9.0 (WPF - Windows Presentation Foundation)
- **Registry Targets**:
  - `HKEY_CURRENT_USER\Software\Classes\Directory\shell` (Folder items)
  - `HKEY_CURRENT_USER\Software\Classes\Directory\Background\shell` (Empty space items)
  - `HKEY_CLASSES_ROOT\Directory\shell` & `Directory\Background\shell` (System items inspection)
- **Restore Point Storage**: `%LocalAppData%\ContextMenuManager\RestorePoints`
