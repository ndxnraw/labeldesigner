# Accessible and reliable UI — ninth package / Pre-release

The owner approved the recommended ninth-package scope: keyboard access, visible
focus, readable scaled controls/dialogs, clearer failures, both runtimes and offline
operation. Work in the existing isolated codex/platform-migration worktree.

## Design

Keep native WinForms navigation and native window frames. Improve the common
ModernButton/ModernComboBox/ModernTabs controls: repaint focus changes, respect
system high-contrast colors, handle tiny/resizing bounds safely, and size wrapped
tab captions from the actual font and available width instead of a fixed header.
Expose tab selection and names through accessibility, and keep Ctrl+PageUp/Down.

Give form fields localized accessible names and update them on language/unit
changes. Add F6 / Shift+F6 navigation between document settings/layers, canvas and
properties; reveal a hidden panel when its existing F4/F8 shortcut requests it.
Never intercept text editing shortcuts or lose unsaved edits during navigation.

Make common message text keyboard-focusable, selectable and scrollable. Preserve
Yes/No/Cancel results and existing confirmation gates; show severity in text, not
only color. Keep action buttons outside the scrollable content. Preferences and
text editing use scrollable content with reachable actions and DPI-aware sizing.
Classify application errors into localized explanation/action guidance while
retaining the original detail and diagnostic reference for troubleshooting.

## Boundaries and evidence

No network, new dependencies, product version numbers or changes to document,
rendering, printing, update trust or persistence formats. Preserve Framework 4.8
offline build and .NET 10. Automated tests exercise four languages, both themes,
100/125/150/200% synthetic scaling, round-trip resize, keyboard traversal and real
dialogs. Synthetic scaling is not a physical cross-monitor DPI transition or a
screen-reader certification; physical monitors, assistive tools and printers remain
manual checks. Do not publish as part of implementation.
