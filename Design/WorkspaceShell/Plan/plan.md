# Bend WorkspaceShell implementation plan

## Goal

Move Bend from its legacy toolbar-and-overlay UI to the WorkspaceShell **Editor Focus** composition while preserving existing editing behavior. The first production milestone is the screenshot state: compact Bend title/menu, title-bar document search, activity rail, document tabs, editor, collapsed terminal header, and status bar.

The Files, repository Search, and Source Control activity buttons only need to select and open an empty left pane. The split-editor action is excluded. Until Git support exists, the bottom-left workspace indicator displays the current folder path rather than a branch.

The HTML catalog in this folder is the visual contract. Choose a surface from its Preview control to inspect every proposed state.

## Inventory and target mapping

| Existing surface | Current implementation | Target surface | First-pass behavior |
|---|---|---|---|
| Main window chrome | `MainWindow.xaml` logo, tab band, command labels, custom controls | Compact title bar with Bend menu, Search control, native window actions | Fully implement |
| Document tabs | Runtime `TabTitle` controls in `TabBar` | Compact file-type tab, dirty dot, close action, trailing new-file action | Preserve open, select, close, reorder, drag, dirty state |
| Editor | `TextCoreControl.TextEditor` hosted in `Editor` | Central editor below tabs and breadcrumbs | Preserve editor and its context menu |
| Document find | Permanent `FindText` in legacy menu band | Title-bar Search opens editor find bar | Fully implement current find behavior |
| Find and replace | `FindAndReplace.xaml` floating window | Expandable editor find bar | Preserve Find, Replace, Replace All, case, regex, selection, history, next/previous |
| Settings | `Settings.xaml` page reached by animated window rotation | Workspace settings view | Preserve all settings; remove 3-D transition |
| Go to Line | `GotoLine.xaml` | Shell-styled modal | Preserve Ctrl+G, validation, Enter/Escape |
| File encoding | `FileEncoding.xaml` | Shell-styled modal | Preserve supported encodings and warning mode |
| Unsaved work | `SaveChangesMessageBox.xaml` | Shell-styled three-action modal | Preserve Save / Don't save / Cancel semantics |
| Alerts and confirmations | `StyledMessageBox.xaml` | Shared shell dialog | Preserve OK/Cancel variants and file-change prompts |
| Tab context menu | `TabContextMenu` in `MainWindow.xaml` | Compact themed context menu | Preserve Refresh, Close, Close Others, Encoding, Copy Path, Open Folder, Reopen Session |
| Editor context menu | `EditorContextMenu` in `MainWindow.xaml` | Compact themed context menu | Preserve Cut/Copy/Paste, Undo/Redo, Go to Line, Record |
| Terminal | `TerminalControl` in `MainDockBottomPanel` | Collapsed TERMINAL header, expandable panel | Preserve process hosting, resize, theme, focus, toggle |
| Activity panes | Not implemented | Files, Search, Source Control empty pane | Selection and open/collapse behavior only |
| Workspace identity | Not represented consistently | Bottom-left current folder path | Use active file folder; otherwise process/current workspace folder |
| Empty editor | Pattern background when no document is active | Quiet Bend empty state | Open file, New file, and file-drop affordances |
| Tab drag feedback | `TabDragVisual.xaml` snapshot window | Lightweight new-tab-style drag ghost | Preserve reorder/drop behavior |
| File picker surfaces | Windows open/save dialogs | Native Windows dialogs | Keep native; set owner/title/filter consistently |
| Theme variants | `Themes/Light.xml`, `Dark.xml`, `Green.xml` | Same semantic shell across all three | Fully theme every shell surface and popup |

## Shell structure

Replace the current overlapping grids and fixed offsets with one root grid:

1. Title bar, 48 px logical height.
2. Workspace row, star-sized.
3. Bottom panel, 34 px collapsed or persisted expanded height.
4. Status bar, 24 px logical height when enabled.

The workspace row contains an activity rail, an optional left pane, and the editor host. The editor host contains tabs, optional breadcrumbs, the editor content, and contextual overlays such as find/replace. Avoid moving the `TextEditor` between visual parents during ordinary state changes; toggle sibling shell regions instead so editor selection, focus, and rendering state survive.

## Semantic theme layer

Add shell-specific resources instead of binding layout chrome directly to editor/terminal colors. Define at least:

- `ShellBackgroundBrush`, `ShellChromeBrush`, `ShellPanelBrush`
- `ShellBorderBrush`, `ShellTextBrush`, `ShellMutedTextBrush`
- `ShellSelectionBrush`, `ShellAccentBrush`, `ShellAccentTextBrush`
- `EditorBackgroundBrush`, `TerminalBackgroundBrush`
- control styles for icon buttons, tabs, menu items, inputs, check boxes, radio buttons, combo boxes, dialogs, and context menus

Populate those keys in Light, Dark, and Green theme dictionaries. Initially map them to the nearest existing colors documented in `../NOTES.md`, then remove one-off literal colors from UI XAML. Keep syntax colors and TextCore rendering resources unchanged.

## State model

Introduce a small shell state owned by `MainWindow` (or a focused view model if the project is moved toward MVVM):

- `ActiveActivity`: None, Files, Search, SourceControl, Settings
- `IsLeftPaneOpen`
- `IsTerminalOpen`
- `TerminalExpandedHeight`
- `IsFindOpen` and `IsReplaceExpanded`
- `CurrentFolderPath`
- active tab metadata: display name, full path, dirty state, detected language, encoding, line, column

Clicking an inactive Files/Search/Source Control button selects it and opens the placeholder pane. Clicking the active button closes the pane and clears the active activity. Settings opens in the editor workspace; returning restores the previously active document without reconstructing it. Persist only useful layout preferences (terminal open/height and optionally pane width), not transient dialogs or search text.

## Implementation phases

### 1. Protect existing behavior

- Build the solution and record a manual baseline for new/open/save/save-as, file drop, recent/session restore, tab close/reorder, external file changes, find/replace, Go to Line, encoding, settings, zoom, context menus, terminal, and shutdown prompts.
- Add focused tests around any logic extracted from `MainWindow.xaml.cs`, especially current-folder resolution and shell state transitions.
- Keep command bindings and keyboard gestures working while visuals are replaced.

### 2. Create shared shell resources

- Add a shell resource dictionary and reusable WPF styles.
- Add equivalent keys to every bundled theme.
- Replace custom glyph labels with consistent vector/path or Segoe Fluent Icons resources that work at 100–200% DPI.
- Establish keyboard focus, hover, pressed, disabled, selected, and high-contrast-visible states.

### 3. Rebuild the main grid to Editor Focus

- Replace `MainWindowGrid` fixed margins with the four-row shell grid.
- Implement the title bar and Bend menu. Move existing file commands into it without duplicating command handlers.
- Preserve maximize/restore drag behavior and resize hit targets. Prefer native window chrome integration where practical; otherwise test Snap Layouts and DPI boundaries explicitly.
- Remove the split-view icon from production XAML.
- Create the activity rail and empty placeholder pane.
- Host the existing tabs and TextEditor in the new editor region.
- Add the current folder path to the status bar. Resolve from the active document path, falling back to the process current directory. Update on tab selection, open, save-as, and external rename.

### 4. Modernize document tabs

- Restyle `TabTitle` rather than rewriting tab/document ownership.
- Show a file-type abbreviation, filename, dirty dot, and close affordance.
- Keep middle-click/close behavior if currently supported and retain the complete path as tooltip/accessibility help.
- Rebuild `TabDragVisual` as the compact ghost shown in the catalog; do not capture and render a large editor snapshot.
- Add a trailing plus button that invokes the existing New command.

### 5. Integrate find and replace

- Make the title-bar Search control invoke the existing Find command and focus the find input.
- Move the `FindAndReplace` controls into an editor overlay/user control; reuse existing `FindOptions` and `Tab` search APIs.
- Ctrl+F opens find; Ctrl+H opens find with replace expanded; Enter/Shift+Enter navigate; Escape closes and restores editor focus.
- Display match count/status locally while continuing to support status announcements. Keep regex timeout and invalid-search messages visible.
- Remove the old floating find window after parity is verified.

### 6. Terminal panel

- Keep `TerminalControl` and its process behavior intact.
- Add the new header with session label, new-session placeholder/action only if supported, expand/collapse, and close.
- Retain the splitter only while expanded and persist a safe minimum/maximum height.
- Verify keyboard focus returns to the editor after collapse and that Tab/input capture remains correct.

### 7. Settings and secondary surfaces

- Restyle and reorganize `Settings.xaml` into Editor, Application, and About & shortcuts sections without changing persisted setting keys.
- Preserve: indent type/width, formatting marks, word wrap, preserve indent, font, animation preference, status bar, smooth scrolling, syntax highlighting, reopen files, diagnostics, themes, context-menu integration, PATH integration, updates, version, and shortcuts.
- Replace the rotation animation with a simple workspace navigation transition; honor the animation preference/reduced-motion expectations.
- Create one shared dialog shell and migrate Go to Line, File Encoding, Unsaved Changes, and Styled Message Box content into it.
- Restyle both context menus with shared menu resources.
- Keep native Open/Save dialogs and set the Bend window as owner.

### 8. Empty, error, and edge states

- Add the all-tabs-closed state shown in the catalog.
- Specify truncation/tooltips for long tab names, paths, search strings, and status values.
- Ensure panes collapse safely at the minimum supported window width; never cover the editor with an invisible hit target.
- Retain external modification/deletion prompts and error detail scrolling.
- Check unsaved Untitled files, read-only files, inaccessible paths, invalid encodings, terminal start failure, and theme-switch while dialogs are open.

### 9. Remove legacy shell code

- Delete the permanent Save/Open/Save+ labels and legacy `MenuBand` only after command parity.
- Remove old find, settings rotation, dialog chrome, snapshot drag, fixed-margin layout code, and unused image assets only after repository-wide reference checks.
- Split oversized `MainWindow.xaml.cs` regions into shell, document command, and terminal helpers where this can be done without changing application behavior.

## Surface-specific acceptance criteria

### Editor Focus milestone

- The initial window matches the Editor Focus composition at common sizes and 100%, 150%, and 200% scaling.
- Editor, tabs, title bar, activity rail, collapsed terminal header, and optional status bar do not overlap.
- No Files/Search/Git functionality is implied beyond opening an empty pane.
- No split-view action appears.
- Bottom-left displays an actual current folder path, not sample Git data.
- Existing shortcuts continue to invoke the correct operation.

### Search and editor

- Find/replace works per active tab and survives tab switching according to current behavior.
- Match navigation, selection-only replace, match case, regex, replace, and replace all have parity.
- Line/column, encoding, and language update from the active document.

### Dialogs and menus

- Every dialog is owned, centered, theme-aware, keyboard navigable, and usable without a mouse.
- Enter invokes the safe/default action; Escape cancels where cancellation exists.
- Destructive or data-loss choices are worded explicitly and are not the accidental default.
- Context menu commands and enable/disable states match the active tab/editor state.

### Themes and accessibility

- Light, Dark, and Green themes cover every popup and transient surface.
- Text and interactive states have sufficient contrast; state is not communicated by color alone.
- Controls expose useful automation names, logical tab order, visible focus, and tooltips for icon-only actions.
- Layout remains usable with Windows text scaling and keyboard navigation.

## Verification matrix

Run these checks in each bundled theme and at least at 1280×720 and 1920×1080:

1. Launch with no session, one file, multiple files, and a restored session.
2. New, open, drag/drop, save, save as, save with encoding, and close.
3. Dirty indicator and each Save/Don't Save/Cancel path.
4. Tab select, close, close others, reorder, drag feedback, external change, and external deletion.
5. Ctrl+F, Ctrl+H, Enter, Shift+Enter, Escape, invalid regex, no results, replace, and replace all.
6. Ctrl+G with valid, boundary, invalid, and empty values.
7. Activity buttons open the correct empty pane, switch panes, and collapse when reselected.
8. Terminal open, close, resize, type, process exit, startup failure, and focus return.
9. Every settings value persists and immediately updates all open tabs where expected.
10. Context menus, application menu, native file dialogs, alerts, and confirmation dialogs.
11. Window minimize, maximize, restore, Snap, resize, multi-monitor move, and DPI transition.
12. Status bar hidden/shown; long current folder; Untitled tab; unsupported/unknown file type.

## Suggested change sequence

Keep commits reviewable and behavior-preserving:

1. Semantic theme resources and control styles.
2. Root shell/grid plus title bar and menu.
3. Activity rail, placeholder pane, and current-folder status.
4. Tab styling and drag feedback.
5. Find/replace integration.
6. Terminal header/panel styling.
7. Settings migration.
8. Shared dialogs and context menus.
9. Empty/error states, accessibility, DPI fixes.
10. Legacy cleanup and full regression pass.

## Explicitly deferred

- File tree/explorer data and file operations inside the Files pane.
- Repository-wide search.
- Git discovery, branch/change status, commit UI, and diffs.
- Split editor/view.
- Agent/AI panels from the broader WorkspaceShell proposal.
- Multiple terminal session management unless it is already supported by the terminal host.

These deferrals should be represented honestly: empty panes may name their future purpose, but must not show fake files, Git counts, branches, search results, or controls that appear operational.
