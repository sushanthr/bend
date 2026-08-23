# Folder Tree and Files Panel implementation plan

## Purpose

Build a reusable WPF `FolderTree` control, then use it in the Files activity pane to display the contents of Bend's current workspace folder. The interaction model and visual density should feel familiar to users of the VS Code Explorer while remaining consistent with Bend's existing WorkspaceShell themes and document-tab implementation.

The first production version must let a user choose a folder, browse its descendants, create and rename entries, move/copy/paste/delete entries, copy paths, reveal entries in File Explorer, and open files in Bend's editor. It must remain responsive for large folders, tolerate filesystem errors, and provide complete mouse, keyboard, tooltip, focus, and screen-reader behavior.

This document supersedes the Files-pane deferral in `plan.md`. Source Control may reuse the visual tree foundation later, but it must supply a different command policy: filesystem authoring commands belong to Explorer, while repository actions belong to Source Control.

## Current implementation and constraints

- `MainWindow.xaml` contains a 240 logical-pixel activity pane with a title and a placeholder message. `FilesActivityButton` only calls `ToggleActivityPane("files", "FILES")`.
- `OpenFolder_Click` currently copies `Environment.CurrentDirectory` into `WorkspacePathText` and opens the placeholder pane. It does not show a folder picker or establish durable workspace state.
- Documents are represented by `Tab` instances created and owned by `MainWindow`. `Tab.OpenFile(path)` performs the actual load; `AddNewTabWithFiles` and `CommandOpen` contain overlapping tab-creation logic.
- A file selected from the tree must use the same document lifecycle as the native Open command, including encoding detection, file watchers, recent-session behavior, dirty tabs, status values, and errors.
- This is a .NET Framework 4.8 WPF application using an old-style `.csproj`; every new `.cs`, `.xaml`, and resource file must be added explicitly to `Bend.csproj`.
- The control must use semantic dynamic resources from the WorkspaceShell theme. Do not hard-code colors that only work in Dark mode.

## Product decisions for the first version

### Workspace definition

Use one canonical `CurrentFolderPath` in `MainWindow` (or a small `WorkspaceState` class), distinct from the active document's containing directory.

- **Open folder…** displays a real folder-selection dialog. On success, normalize the selected path with `Path.GetFullPath`, set it as `CurrentFolderPath`, load it into the Files panel, update `WorkspacePathText`, select Files in the activity rail, and open the side pane.
- Canceling the picker changes nothing.
- Opening or saving an individual file does not silently replace the workspace. This prevents the tree from unexpectedly jumping when the user switches editor tabs.
- On first launch with no explicitly chosen workspace, use `Environment.CurrentDirectory` as the initial workspace only if it exists and can be read. Label this behavior as a fallback, not as a folder selection.
- Persist the last successfully selected folder in `PersistantStorage`; restore it on launch when it still exists. If it is missing or inaccessible, retain the path for the empty/error message but do not crash or repeatedly prompt.
- Version one supports one folder root. Multi-root workspaces are deferred.

### File activation

A normal left click on a file row opens that file in the editor area. This is intentionally stronger than VS Code's optional single-click preview behavior: Bend has no preview-tab concept, so every successful activation creates or selects a normal durable tab.

Extract a single `OpenDocument(string fullPath)` method from the behavior currently embedded in `CommandOpen` and use it for the Open dialog, tree activation, shell/IPC opens where practical, and future search/source-control activation.

`OpenDocument` must:

1. Normalize the path and verify that it identifies an existing file.
2. Compare paths case-insensitively using Windows path semantics. If that file is already open, select its existing tab instead of opening a duplicate.
3. Reuse the active untitled tab only when its document is empty, has no filename, and has no unsaved content; otherwise create a new tab.
4. Hide the prior editor, add/select the target tab, and call the existing `Tab.OpenFile` load path.
5. If loading fails, remove only a newly created empty tab, preserve the previous selection, display an actionable error, and leave keyboard focus in a sensible place.
6. On success, update the tab title, recent-file/session data, encoding/language/status information, and focus the editor.

Clicking a folder never opens a document. It toggles expansion. Clicking a disclosure arrow only toggles expansion and does not change editor focus.

## Proposed component structure

Keep filesystem presentation separate from document ownership. The tree must not know about Bend tabs or `TextEditor`.

### `Controls/FolderTree.xaml` and `.xaml.cs`

A reusable `UserControl` containing the styled tree and its loading, empty, and root-error presentations. Its public contract should be small:

| Member | Type | Purpose |
|---|---|---|
| `RootPath` | dependency property, `string` | Normalized directory represented by the control. A change cancels old loading and replaces the root. |
| `SelectedPath` | read-only dependency property, `string` | Full path of the selected item, for commands and future Source Control integration. |
| `ShowRoot` | dependency property, `bool` | Files pane sets `false`, so children appear beneath its separate workspace header. Other consumers may show the root row. |
| `FileInvoked` | routed event | Raised with the normalized full path after a file row is activated. |
| `SelectedItems` | read-only collection | Selected nodes used by Explorer commands. Version one may enforce single selection while keeping the command API collection-shaped. |
| `CommandProvider` | interface/dependency property | Supplies context-menu commands and enablement for the current host; Explorer and Source Control provide different policies. |
| `RefreshAsync()` | method | Re-enumerates the root while preserving expansion and selection when possible. |
| `RevealPathAsync(path)` | future-compatible method | Expands ancestors and selects a path; implementation may be deferred until needed. |

Do not expose `TreeViewItem` instances or Bend-specific commands in this API. The control owns hierarchy, selection, expansion, inline editing presentation, and keyboard routing. It does not decide whether a node can be deleted, staged, reverted, or opened as a diff.

### Host-specific command policy

Use a command-provider interface (for example `IFolderTreeCommandProvider`) instead of embedding one universal menu in `FolderTree`. The provider receives the clicked node, current selection, root, and invocation location, then returns ordered command descriptors with label, icon, gesture text, enabled state, separator grouping, and execution callback/command.

This keeps shared tree behavior honest:

| Concern | Shared `FolderTree` | Files/Explorer provider | Source Control provider |
|---|---|---|---|
| Rows, indentation, expansion, selection, focus | Owns | Uses | Uses/adapts |
| Lazy child loading | Owns for directory hierarchy | Uses physical filesystem | Usually not used; SCM supplies logical groups/changes |
| Inline rename editor | Provides presentation hook | Enables for filesystem nodes | Disabled; repository actions do not rename files |
| Clipboard/file mutation | No knowledge | Cut, Copy, Paste, Delete, New File/Folder | Never exposes these merely because paths are present |
| Primary activation | Raises invocation | Open file in editor | Open working-tree file or diff, according to SCM item kind |
| Context menu | Renders descriptors | Explorer commands | Stage/Unstage, Open Changes, Discard, etc. in a later Git plan |
| Decorations | Provides slots | Optional file icon/dirty editor marker | Git status letter/color/group supplied by SCM |

The Source Control view will probably be a logical tree (`Changes`, `Staged Changes`, merge conflicts) rather than a literal folder hierarchy. Reuse the row template, selection/focus mechanics, command rendering, and accessibility patterns, but do not force SCM data into `FolderTreeNode` or give it Explorer's mutation menu. If necessary, extract a lower-level generic `TreeList` visual primitive after the Files implementation proves which behavior is truly shared.

### `FolderTreeNode`

A lightweight presentation model implementing `INotifyPropertyChanged`:

- `Name`, `FullPath`, `NodeKind` (`File`, `Directory`, optionally `SymbolicLink`/`ReparsePoint`), and `Extension`.
- `Children` as `ObservableCollection<FolderTreeNode>`.
- `IsExpanded`, `IsSelected`, `IsLoading`, `IsLoaded`, `HasLoadError`, and a short `LoadErrorMessage`.
- `CanExpand` independent of whether children have been loaded. Use a placeholder child or a custom `HasItems` template so a collapsed folder can show a disclosure chevron without recursively scanning it.
- `IconKey` or an icon-category value rather than a concrete brush/image, allowing themes and later icon packs to map visuals.

Avoid storing file contents, `FileInfo`/`DirectoryInfo` objects, or editor references. Full paths are the stable identity for refresh reconciliation.

### `IFileSystemTreeService`

Put enumeration and normalization behind a small interface so behavior can be tested without constructing WPF controls:

- `EnumerateChildrenAsync(directoryPath, cancellationToken)` returns directories and files as immutable descriptors.
- Enumeration occurs off the UI thread, but `ObservableCollection` updates are marshaled to the dispatcher.
- Catch errors per directory (`UnauthorizedAccessException`, `DirectoryNotFoundException`, `PathTooLongException`, `IOException`, `SecurityException`) and return a displayable failure rather than failing the entire root.
- Do not follow directory reparse points by default. Show them as non-expandable or explicitly marked entries to prevent cycles. This can be revisited with cycle detection later.
- Ignore `.` and `..`; do not apply implicit repository-specific exclusions in version one.

### `FilesPanel.xaml` and `.xaml.cs`

A focused `UserControl` that supplies the Files-pane chrome and hosts `FolderTree`:

- Receives/binds `CurrentFolderPath`.
- Raises `OpenFolderRequested`, `RefreshRequested`, and forwards `FileInvoked` without opening tabs itself.
- Owns the workspace header, action buttons, empty state, root error, and tree visibility.
- Does not own persistence or native dialogs; `MainWindow` coordinates those concerns.

`MainWindow` handles `FileInvoked` and calls `OpenDocument(path)`. This boundary lets Source Control reuse tree rows or tree styling without inheriting editor/tab logic.

## Files panel visual specification

### Pane sizing and layout

- Retain the current default width of 240 logical pixels, with a recommended minimum of 170 and maximum of 600 when resizing is added.
- Add a narrow right-edge `GridSplitter` in the same milestone if the side-pane width is already intended to persist. The visible separator remains 1 px; the hit target should be at least 5 logical pixels.
- The pane contains, top to bottom: activity title row, workspace section header, tree/empty/error body.
- Use the existing `ShellChromeBrush`, `ShellBorderBrush`, `ShellMutedBrush`/semantic equivalent, selection, accent, and focus resources. Add semantic tree brushes only if existing keys cannot express hover, selection, and inactive selection cleanly.

### Activity title row

- Height: 42 px, matching the current shell row.
- Label: `EXPLORER`, matching VS Code terminology, 11 px, left-aligned at 14 px. A trailing ellipsis is reserved for infrequent panel-level commands such as `Open Folder…` and `Refresh`; it is not required in the first pass.
- Do not place file-operation buttons in this top activity-title row. Keep it visually quiet.
- Icon buttons are 28–32 px hit targets, have no persistent border, show a subtle hover background, and expose tooltips and automation names.
- Actions may remain hidden until the row/pane has pointer hover, but must become visible on keyboard focus within the header and remain reachable by Tab.

### Workspace section header

- Height: 22 px minimum with a compact VS Code-like section treatment.
- Show a downward/right disclosure chevron followed by the root folder's leaf name in uppercase or semibold shell text. The full normalized path appears in a tooltip and accessibility help text.
- At the right edge show exactly three primary actions, in this order: **New File**, **New Folder**, and **Collapse Folders**. These act on the selected directory, or on the selected file's parent; with no selection they act on the workspace root. Collapse Folders recursively collapses UI nodes but does not alter the filesystem.
- Clicking the row collapses/expands the entire section without discarding child expansion state.
- The header context menu may contain `Open Folder…`, `Refresh`, `Paste`, `Copy Path`, and `Copy Relative Path`. Paste targets the workspace root.
- For long root names, trim with character ellipsis. Never force the pane wider.

### Tree rows

- Row height: 22 px at 100% text scaling, allowed to grow with system text scaling; never clip glyphs or focus outlines.
- Indentation: 8 px per hierarchy level after the root, plus a fixed disclosure area. Use one layout calculation so icons and labels align at all depths.
- Chevron: right-facing when collapsed, down-facing when expanded; reserve its width for files so filenames align with folders. Use vector geometry or a font glyph that scales cleanly.
- Icon: 16 px slot. Use simple theme-aware folder-open/folder-closed and generic-file icons initially. Extension-specific icons are optional and must not block the control.
- Label: filename or folder name only, single line, ellipsized. Tooltip shows the full path when trimmed and may always show it for consistency.
- Row background spans the full pane width, including indentation, matching VS Code's broad hover/selection target.
- Hover uses a subtle theme-specific surface. Selected uses `ShellSelectionBrush`; selection must also have a visible focus cue when the tree owns keyboard focus. Inactive selection remains distinguishable without looking focused.
- The disclosure glyph, icon, and text inherit disabled/muted state for unavailable items, but errors are also expressed with an icon and accessible text rather than color alone.
- Directories sort before files. Within each group use case-insensitive natural ordering so `file2` precedes `file10`; use current UI culture for display ordering while path identity remains ordinal-ignore-case.
- Hidden and system entries are shown in version one, matching the literal contents of the selected folder. A later setting may dim or hide them; do not silently omit them now.
- While creating or renaming, replace the row label with a compact text box using the selection background and a clear focus outline. Select the editable basename by default; for files, select the name without the final extension so typing preserves the extension unless the user deliberately selects it.

### Loading, empty, and error states

- While a directory is first expanded, immediately show a single indented `Loading…` row or compact progress indicator. Disable repeated expansion work and allow collapse while loading continues/cancels.
- An empty directory expands to an `(empty)` muted row or remains expanded with an accessible empty announcement. Prefer an explicit row so the expansion does not appear broken.
- A child-directory enumeration failure appears beneath that directory as one non-selectable error row with concise text such as `Unable to read this folder`. Its tooltip contains the safe exception message; never expose a stack trace in the UI.
- A missing/inaccessible workspace root replaces the tree with a centered compact explanation and buttons for `Open Folder…` and `Retry` where retry can help.
- When no workspace is configured, show `No folder open` plus a primary `Open Folder…` action. Do not render a fake tree.
- Refresh keeps the old tree visible until replacement data is ready when possible. If refresh fails, preserve the last known tree and show a non-modal error/status message.

## Explorer commands and file-operation UX

### Command placement

Keep the always-visible surface deliberately small:

- Workspace header: **New File**, **New Folder**, **Collapse Folders** only.
- File context menu: **Open in Editor**, separator, **Cut**, **Copy**, separator, **Copy Path**, **Copy Relative Path**, separator, **Rename…**, **Delete**, separator, **Open in File Explorer**.
- Folder context menu: **New File**, **New Folder**, separator, **Cut**, **Copy**, **Paste**, separator, **Copy Path**, **Copy Relative Path**, separator, **Rename…**, **Delete**, separator, **Open in File Explorer**.
- Empty tree space/root context menu: **New File**, **New Folder**, **Paste**, separator, **Copy Path**, **Copy Relative Path**, separator, **Open in File Explorer**.

`Paste` is meaningful only on a directory, the workspace header/root, or empty tree space. Do not show a disabled Paste item in a file's menu. `Open in Editor` is file-only. `Rename…` is retained because renaming is part of the requested editing workflow, even though it is not a toolbar action.

Menu order, separators, labels, gesture text, and enablement must be identical whether opened by pointer or keyboard. Prefer hiding commands that cannot apply to the node kind; disable commands when they normally apply but are temporarily unavailable (for example, Paste with an empty clipboard).

### Command targets

- **New File/New Folder**: selected directory; if a file is selected, its parent directory; otherwise workspace root.
- **Cut/Copy/Rename/Delete/Copy Path/Copy Relative Path**: the selected node. Right-click changes selection to the row under the pointer before resolving the target.
- **Paste**: selected directory, or selected file's parent when invoked from a general keyboard shortcut; context menus only offer Paste on directory/root targets.
- **Open in File Explorer**: for a file, launch Explorer with the file selected; for a folder, open that folder.
- **Open in Editor**: call the same `OpenDocument(path)` used by normal file activation.

Version one should use single selection. Shape the operation service around a list of source paths so multi-select can be added later without redesigning Cut/Copy/Delete.

### New file and new folder

1. Resolve and expand the target directory, cancel any other inline edit, insert a temporary row in the correct directory, and focus its edit box.
2. Start with an empty name. `Enter` validates and creates; `Escape` cancels and removes the temporary row; losing focus commits only when valid, otherwise keep editing and show the validation message.
3. Reject empty/whitespace-only names, `.`/`..`, invalid Windows filename characters, reserved device names, trailing spaces/periods, and names that already exist using case-insensitive comparison. Explain the problem inline without a modal dialog.
4. New files are zero-byte files. After successful creation, keep the node selected and open it in the editor. New folders remain selected and expanded, ready for another create operation.
5. Create through a filesystem operation service, then reconcile with watcher events by path/operation identifier so the optimistic row is not duplicated.
6. If creation fails, keep the proposed name in edit mode and show a concise access/I/O error. No half-created UI row may remain after cancellation.

### Rename

- `F2` or `Rename…` starts inline editing on the selected node. Only one inline editor may exist at a time.
- Preselect the basename excluding the final extension for files; select the full name for folders. Left/Right and text-editing shortcuts stay inside the text box rather than navigating the tree.
- `Enter` commits and `Escape` restores the original name. An unchanged name exits without touching disk.
- Apply the same filename validation as creation and reject collisions before attempting the move.
- Use `File.Move`/`Directory.Move` semantics and support case-only renames on Windows with a safe intermediate-name strategy when required. The intermediate path must be unique and rolled back on failure.
- When an open file is renamed, update that tab's `FullFileName`, title, tooltip, file watcher, recent-session path, and status metadata without losing document contents or dirty state. Renaming a directory must update paths and watchers for every open tab beneath it.
- If updating open-document identity cannot be made atomic, block the rename with a clear explanation rather than leaving tabs attached to stale paths.

### Cut, copy, and paste

- Use the Windows file-drop clipboard formats (`FileDrop`) so operations interoperate with File Explorer. Store the preferred drop effect (`Move` for Cut, `Copy` for Copy) and plain-text paths where appropriate. Do not depend only on an in-process clipboard object.
- `Ctrl+X` marks the selected row as cut with a muted/partially transparent visual. `Ctrl+C` removes any prior cut decoration and copies the selected path. Losing clipboard ownership clears the cut decoration.
- `Ctrl+V` pastes into the resolved target directory. Copy creates a new entry; Cut moves the entry and clears clipboard/cut state only after complete success.
- Prevent moving/copying a directory into itself or one of its descendants. Normalize paths before containment tests.
- Name conflicts open a focused confirmation UI offering **Replace**, **Skip**, and **Cancel** for a single item. Design the operation result to support future **Apply to all** for multi-selection. Default to the non-destructive choice.
- A same-directory copy needs a deterministic non-conflicting name such as `name - Copy.ext`, then `name - Copy (2).ext`. A same-directory cut is a no-op with a status message.
- Perform potentially slow directory copies/moves asynchronously with progress and cancellation. Do not freeze the WPF dispatcher. Clearly report partial completion and refresh both source and destination directories.
- Pasting external Explorer items is allowed when all clipboard paths can be validated. Pasting text that merely resembles a path is not.

### Copy Path and Copy Relative Path

- **Copy Path** places the normalized absolute path on the text clipboard without surrounding quotes.
- **Copy Relative Path** uses `CurrentFolderPath` as the base and copies a path with no leading separator. Use backslashes on Windows for consistency with Explorer and the rest of Bend.
- The workspace root's relative path is `.`. Entries outside the workspace do not offer Copy Relative Path.
- Copy failures caused by clipboard contention should retry briefly on the UI thread using dispatcher delays, then show a non-modal error.

### Delete

- `Delete` or the context-menu command opens a Bend-themed confirmation naming the selected file/folder. State explicitly that non-empty folders include all descendants.
- Default focus is **Cancel**. Confirm with a clearly labeled **Delete** action; `Escape` cancels.
- Prefer moving to the Windows Recycle Bin so deletion is recoverable. If recycle is unavailable, ask separately before permanent deletion and state that it cannot be undone.
- Before deleting a file open in Bend, check its document. If dirty, use the existing Save/Don't Save/Cancel protection before filesystem deletion. Never discard dirty editor content solely because the file was deleted from Explorer.
- For a folder, evaluate all open tabs beneath it and resolve dirty-document prompts before starting deletion. Canceling any prompt cancels the entire operation.
- After success, close clean tabs whose backing files were deleted, select the nearest surviving sibling/parent, and announce the result. On failure, preserve nodes until a refresh confirms actual disk state.

### Operation coordination

Introduce `IFileOperationService` for create, rename, copy, move, recycle/delete, validation, conflict decisions, and structured results. The service must distinguish complete success, cancellation, validation failure, total failure, and partial success. `FilesPanel` coordinates presentation; `FolderTree` only enters/exits inline-edit states and refreshes affected nodes.

Serialize conflicting operations on the same paths. Suppress or coalesce matching `FileSystemWatcher` events while an app-initiated operation is being reconciled, but never disable the watcher globally. Every command must revalidate existence, containment, and destination immediately before mutation because tree nodes can be stale.

## Detailed interaction behavior

### Mouse and pointer

- Single left click on a folder row selects it and toggles expansion. Single left click on a file selects it and immediately invokes `FileInvoked`.
- Double-click on a file must not create two requests; suppress the second activation for the same gesture. Double-click on a folder produces the same final expansion state as one intentional toggle, not toggle-twice.
- Clicking the chevron toggles a directory without invoking any other row action.
- Right click first selects the item under the pointer, then opens the host command provider's context menu. The Explorer menu follows the command placement specified above; Source Control supplies its own menu. Any external process failure is reported non-modally.
- Keep drag-and-drop out of the first implementation. Rows must not show drag cursors or drop adorners.

### Keyboard

Follow standard WPF/VS Code tree expectations:

- `Up`/`Down`: move to the previous/next visible row.
- `Right`: expand a collapsed directory; on an expanded directory move to its first child. It does nothing destructive on a file.
- `Left`: collapse an expanded directory; otherwise move to its parent.
- `Enter`: invoke a file or toggle a directory.
- `Space`: select/toggle a directory; on a file it may invoke only if this is made consistent with the final WPF automation behavior. Prefer `Enter` for file activation.
- `Home`/`End`: move to the first/last visible item. `Page Up`/`Page Down` scroll and move selection by a viewport.
- Typing characters performs incremental name search among visible rows with a short reset timeout.
- `F5`: refresh the tree while focus is inside the Files panel.
- `Ctrl+X`, `Ctrl+C`, and `Ctrl+V`: cut, copy, and paste filesystem entries using the targets and enablement rules above. When an inline editor owns focus, these retain normal text-editing meaning.
- `F2`: rename the selected filesystem entry. `Delete`: request confirmed deletion.
- `Shift+F10` or the Menu key: open the selected item's context menu.
- `Escape`: close an open context menu; otherwise return focus to the editor only if that matches the established shell-level Escape behavior.

Opening a file transfers focus to the editor after the document is loaded. Folder expansion retains focus and selection in the tree. When reopening the Files pane, restore focus to the previously selected visible node if the user keyboard-navigates into it; otherwise focus the first row.

### Activity-pane behavior

- Clicking the inactive Files activity opens the existing pane and preserves tree expansion, selection, and vertical scroll offset for the session.
- Clicking the active Files activity collapses the pane but does not dispose the tree or cancel already useful cached node data.
- Switching to Search or Source Control swaps pane content while preserving Files state. Replace the single placeholder text with a content host selected by `activeActivity` rather than reconstructing `FilesPanel` on every toggle.
- When a file opens, keep the Files pane open. Selection remains on that file and the active editor tab changes.
- The activity button exposes selected/pressed state visually and through UI Automation; current code should be extended beyond only tracking a string and column width.

## Enumeration, refresh, and filesystem change behavior

### Lazy loading

Load only the root's immediate children at workspace selection. Load descendants the first time each directory expands. This avoids recursive startup scans and keeps network/removable folders usable.

- Each load captures a generation identifier and cancellation token. Results from an old workspace or superseded refresh are discarded.
- Apply results in one dispatcher batch where practical to avoid hundreds of layout passes.
- Keep a bounded cache only for nodes reachable from the current root. Replacing the root releases all prior nodes and watcher resources.
- Preserve expanded paths, selected path, and scroll position across manual refresh when those paths still exist.

### Sorting and reconciliation

Centralize comparison logic. Refresh should reconcile by normalized full path so unchanged node objects retain `IsExpanded` and `IsSelected`. Additions appear in sorted position; removals disappear; a case-only rename updates the display name while preserving identity as far as Windows permits.

### Live changes

Add live updates only after lazy loading and manual refresh are stable.

- Prefer one `FileSystemWatcher` rooted at the workspace with subdirectories enabled, but treat it as an invalidation signal rather than an authoritative event stream.
- Debounce bursts (roughly 150–300 ms), coalesce paths, and refresh only the affected loaded directory where possible.
- Handle watcher overflow by scheduling a full refresh and showing a subtle `Refreshing files…` status, not by losing updates silently.
- Marshal all watcher results onto the dispatcher and dispose the watcher when the workspace changes or the window closes.
- Do not let tree watching interfere with the existing per-tab external-change watchers in `Tab`; they serve different purposes.
- Watcher support may be phase two of this milestone. Manual Refresh is required for the first usable increment.

## Accessibility and theming

- Give the control and workspace section meaningful automation names: `Files`, `Folder tree for {folder}`, `Expanded/Collapsed`, and `File/Folder` item type.
- Ensure WPF automation peers expose hierarchy level, expand/collapse state, selection, and invoke behavior. Add a custom peer only where the stock `TreeView`/`TreeViewItem` peer is insufficient.
- Announce root-load failure and file-open failure through the existing status mechanism plus visible text. Avoid focus-stealing message boxes for recoverable enumeration errors.
- Maintain a visible 1–2 px focus cue independent of selection color. Verify contrast for normal, muted, hover, selected, inactive-selected, and error states in Light, Dark, and Green themes.
- Respect Windows text scaling and high contrast. Avoid pixel-sized raster icons and color-only file/folder distinction.
- Tooltips must not be required to understand primary actions; they supplement ellipsized paths and icon buttons.

## Error handling and safety

- Validate that every invoked path is still inside or equal to the current root before acting on it, except for explicitly represented reparse-point targets. A stale node may be rejected and refreshed.
- Treat filenames as display text, never as markup or command-line fragments.
- Pass explicit argument values when revealing paths in Explorer; quote safely and do not build a shell command string.
- A file that disappears between enumeration and activation produces a concise `File no longer exists` message, removes/refreshes the stale row, and preserves the current document.
- Access denied, sharing violations, invalid encoding, and unsupported/oversized files use the existing `Tab.OpenFile` outcome where possible, augmented with path-specific user feedback.
- Never overwrite an existing dirty tab when opening from the tree.
- Do not follow reparse points during recursive copy/delete traversal unless the operation explicitly treats the link itself as the entry. Never recurse through a link target.

## Implementation sequence

### Phase 1: extract and test document opening

1. Extract `OpenDocument(path)` from `CommandOpen` without changing native-dialog behavior.
2. Add already-open path detection and explicit success/failure reporting.
3. Route the existing Open command through it.
4. Add tests or a small testable coordinator for empty-untitled reuse, new-tab creation, duplicate selection, load failure rollback, and case-insensitive path matching.

### Phase 2: establish workspace state

1. Add `CurrentFolderPath` and one update method that synchronizes persistence, `WorkspacePathText`, and `FilesPanel.RootPath`.
2. Replace `OpenFolder_Click` with a real owned folder picker.
3. Restore a valid last folder at startup and present missing/inaccessible states safely.
4. Preserve existing individual-file Open and Save As behavior; neither implicitly changes the workspace.

### Phase 3: build the reusable tree

1. Add `FolderTree`, `FolderTreeNode`, descriptors, and `IFileSystemTreeService` under a focused `Controls/FolderTree` or `Workspace` folder.
2. Implement cancellation-aware root and lazy-child enumeration.
3. Implement stable sorting, loading/empty/error pseudo-rows, refresh reconciliation, and state preservation.
4. Add the routed `FileInvoked` event and keyboard/mouse activation guards.
5. Add new sources and XAML pages to `Bend.csproj`.

### Phase 4: compose the Files panel

1. Replace the generic side-pane placeholder with an activity content host.
2. Add `FilesPanel` with title row, workspace header, action buttons, empty/error views, and `FolderTree`.
3. Wire file invocation to `MainWindow.OpenDocument` and keep the pane open after activation.
4. Add selected activity visuals and preserve Files state while switching activities.
5. Add a resize splitter and persisted pane width if included in the broader shell milestone.

### Phase 5: filesystem authoring commands

1. Add `IFileOperationService`, Windows filename validation, structured operation results, and conflict handling.
2. Implement New File, New Folder, and Rename with shared inline-edit presentation and open-tab path coordination.
3. Implement Windows-interoperable Cut/Copy/Paste, cut decoration, containment checks, asynchronous directory operations, and conflict prompts.
4. Implement absolute/relative path copying, Explorer reveal, Recycle Bin deletion, dirty-tab safeguards, and focus/selection recovery.
5. Add `ExplorerTreeCommandProvider`; verify that `FolderTree` contains no hard-coded Explorer or Source Control command set.

### Phase 6: polish and live refresh

1. Add context menus, Explorer reveal, copy-path commands, natural sorting, incremental keyboard search, and tooltips.
2. Add debounced `FileSystemWatcher` invalidation and overflow recovery.
3. Complete automation behavior, screen-reader announcements, high-contrast styles, and DPI/text-scaling fixes.
4. Profile large and slow directories and remove UI-thread enumeration or excessive collection churn.

## Verification plan

### Automated tests

Test filesystem logic against temporary directory trees, never repository fixtures:

- Empty root; files only; folders only; mixed children; deep hierarchy.
- Directory-first, case-insensitive natural ordering.
- Lazy loading does not enumerate grandchildren before expansion.
- Cancellation and generation checks prevent an old root from populating a new tree.
- Refresh preserves surviving expansion/selection and adds/removes/renames nodes correctly.
- Access denied, missing folder, disappearing child, long/invalid path, I/O failure, and reparse-point handling.
- `OpenDocument` chooses reuse/create/select behavior correctly and rolls back a failed new tab.
- A double-click gesture raises only one file-open request.
- Creation/rename validation covers collisions, reserved names, invalid characters, trailing spaces/periods, case-only renames, and disappearing parents.
- Cut/copy/paste covers Windows clipboard formats, same-folder operations, descendant prevention, conflicts, cancellation, and partial failure.
- Absolute and workspace-relative paths are correct for root, nested files, similarly prefixed sibling paths, and paths outside the root.
- Delete coordination protects dirty open files and folders containing dirty open files; canceled prompts cause no filesystem mutation.
- Explorer and Source Control command providers expose distinct commands and enablement for the same row/selection primitives.

Abstract error injection behind `IFileSystemTreeService`; tests should not depend on successfully changing Windows ACLs.

### Manual UI matrix

Run in Light, Dark, and Green themes, at 100%, 150%, and 200% display/text scaling, using mouse and keyboard:

1. Launch with no persisted folder, a valid folder, a missing folder, and an inaccessible folder.
2. Open Folder and cancel; select a local, network, removable, empty, large, and deeply nested folder.
3. Expand/collapse nodes, use chevrons, scroll, incremental search, Home/End, and context menus.
4. Click a file with no tab, an empty untitled tab, a dirty untitled tab, another document open, and the same file already open.
5. Confirm each click opens/selects exactly one editor tab and transfers focus to the editor.
6. Trigger deleted-file, locked-file, access-denied, invalid-encoding, and load-failure paths; verify existing work is preserved.
7. Refresh after adding, deleting, and renaming children externally. Repeat with watcher updates enabled and with a burst large enough to exercise coalescing.
8. Collapse/reopen Files and switch Files → Search → Source Control → Files; verify expansion, selection, and scroll state persist.
9. Resize the side pane and main window to minimum width; verify labels ellipsize, rows do not overlap, and splitter hit testing does not cover the editor.
10. Use Narrator to verify item name, file/folder role, level, selection, expand/collapse state, errors, and icon-button names.
11. Create and rename files/folders with valid, duplicate, invalid, reserved, case-only, and access-denied names; verify inline focus and Escape cancellation.
12. Cut/copy/paste within the workspace and to/from Windows File Explorer, including same-folder copy, collision, large directory, cancellation, and move-into-descendant attempts.
13. Copy absolute and relative paths for the root and nested entries; verify exact clipboard text.
14. Delete clean, dirty-open, read-only, non-empty, locked, and externally removed entries; verify Recycle Bin behavior, safe defaults, and partial-failure reporting.

## Definition of done

- Open Folder selects and persists a real workspace folder and the Files pane displays its immediate contents without blocking the UI.
- Folders expand lazily, sort before files, and expose clear loading, empty, and failure states.
- A single click on any file row opens it in the editor area or selects its existing tab; it never duplicates a tab or overwrites dirty work.
- File-open failures preserve the previously active document and provide actionable feedback.
- Mouse and keyboard tree navigation behave consistently, and folder expansion does not steal focus into the editor.
- The pane retains useful state across activity switching and uses no fake data or nonfunctional actions.
- The tree is usable with keyboard-only input, Narrator, high contrast, all Bend themes, text scaling, and high DPI.
- Enumeration is cancellable and off the UI thread; large folders and expected filesystem exceptions do not freeze or terminate Bend.
- The workspace header contains only New File, New Folder, and Collapse Folders; mutation and path commands live in the appropriate context menus and keyboard bindings.
- Create, rename, cut, copy, paste, copy path, copy relative path, delete, Explorer reveal, and Open in Editor obey their target, validation, dirty-document, and failure rules.
- The shared tree does not leak Explorer commands into Source Control; each host supplies its own activation and command policy.
- New code is covered at the service/coordinator level and the manual verification matrix passes.

## Explicitly deferred

- Dragging files/folders and multi-selection. Cut/copy/paste and single-selection operations are included.
- Preview tabs, double-click-to-pin semantics, editor split/drop targets, and multiple editor groups.
- Multi-root workspaces and saved workspace files.
- Explorer exclude patterns, `.gitignore` filtering, compact-folder chains, decorations, diagnostics badges, and extension-specific icon packs.
- Repository status, staged/unstaged grouping, diffs, branch operations, and all other Source Control behavior.
- Remote filesystem providers and elevation flows.

These features should influence clean extension points, but the initial UI must not show controls or states that imply they already work.
