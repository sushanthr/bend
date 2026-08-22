# Bend workspace shell

This folder contains the interactive design reference for Bend's revised workspace shell.

## Open the composition

Open `index.html` in a browser. Use the controls above the mockup to compare:

- Balanced workspace
- Editor focus
- Agent + editor

## Design boundaries

The composition is grounded in Bend's existing editor, tabs, document search, settings, themes, status bar, and terminal.

Planned additions are:

- File explorer
- Repository search
- Git/source-control panel
- Git diff tabs in the editor
- A right-side terminal hosting the user's choice of Codex, Copilot, or Claude CLI

The agent area intentionally represents a native CLI terminal. Bend owns the panel header, provider selection, sizing, and theme; the selected CLI owns everything inside the terminal surface.

## Iteration workflow

Update `index.html` first when exploring layout, sizing, panel states, or visual language. Record decisions and unresolved questions in [`NOTES.md`](NOTES.md). Translate stable decisions into production XAML only after the interaction is understood.

Do not treat placeholder filenames, Git changes, code, or terminal output in the mockup as implemented behavior.
