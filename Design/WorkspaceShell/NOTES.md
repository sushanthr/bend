# Bend workspace design notes

This file records design topics, their current resolution, and questions that remain open. Update it whenever the HTML composition changes materially.

## Application identity and title bar

**Resolution:** Show only **Bend** at the top-left. Remove the separate `B` tile and the speculative `bend / main` indicator with its green dot.

The Bend label opens the application menu. Document tabs share the compact top region, while native window controls remain at the far right.

## File commands

**Resolution:** Preserve the original file commands, but remove their permanent oversized toolbar labels.

- **New file:** Bend menu and `Ctrl+N`; also available through the `+` action at the end of the document tabs.
- **Open file:** Bend menu and `Ctrl+O`.
- **Open folder:** Bend menu; becomes important when the file explorer is implemented.
- **Save:** Bend menu and `Ctrl+S`.
- **Save as:** Bend menu and `Ctrl+Shift+S`.
- **Save with encoding:** Bend menu. This gives the original `Save+` behavior a more explicit name.
- **Recent files:** Nested entry in the Bend menu.

An unsaved or modified document displays a dot in its tab. New unsaved files should use names such as `Untitled 1`.

## Search and replace

**Resolution:** The title-bar control says **Search** and advertises `Ctrl+F`. It represents search within the active document, matching Bend's current functionality.

Find opens contextually within the editor. Replace expands from that interface through `Ctrl+H`; it is no longer a permanent top-level label. Repository-wide search belongs to the left Search panel after that feature is implemented.

## Left activity area

**Resolution:** Use Files, Search, Source Control, and Settings. Diff is not a separate permanent activity because changed files and commit comparisons open as editor tabs from Source Control.

Files, repository search, and Git are planned additions. Settings is grounded in Bend's existing theme, font, word-wrap, formatting, and status-bar preferences.

## Git identity and status

**Resolution:** Do not show repository or branch identity in the title bar. Once Git is implemented, show the active branch and change count in the lower-left status bar and within the Source Control panel where useful.

Avoid ambiguous health dots or sync indicators until they map to a defined application state.

## Agent panel

**Resolution:** The right panel hosts the native terminal UI of the user's selected Codex, Copilot, or Claude CLI.

Bend owns only the panel header, provider selection, restart/collapse actions, terminal process, working directory, and theme integration. Bend does not create a separate chat, plan, context-chip, or change-review interface around the CLI.

## Bottom panel

**Resolution:** Show Terminal only. Bend already implements terminal hosting and theme integration. Problems, Output, and Debug Console should not appear until corresponding diagnostics or task systems exist.

The terminal header contains the shell/session selector, new-session action, maximize/collapse action, and close action.

## Status bar

**Resolution:** Only show states Bend can calculate. Current candidates are line and column, encoding, and language mode. Add branch and change count after Git integration.

Do not display placeholder errors, warnings, synchronization, or agent activity.

## Workspace compositions

The HTML currently demonstrates:

- **Balanced workspace:** left panel, editor, agent CLI, and terminal are visible.
- **Editor focus:** side panels collapse and the bottom terminal reduces to its header.
- **Agent + editor:** the left panel collapses and the selected CLI receives more width.

## Open questions

- Should clicking the active activity icon collapse its panel, or should there be a dedicated collapse control?
- Should the `+` tab action always create a blank file, or open a small New menu once project templates exist?
- Should Save with encoding remain a direct menu item or become a submenu of Save As?
- What minimum widths should trigger automatic collapse of the left or agent panes?
