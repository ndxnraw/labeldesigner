# Print controls and deliberate wheel editing

Implement the three explicit screenshot requests without changing output logic:

- Remove the standalone Output review button. Submit still opens mandatory
  preflight and enforces errors, warnings and consent fingerprints.
- Size shared tab headers from the actual translated captions and drawing padding;
  distribute extra space or wrap to additional rows when space is insufficient.
- Replace UI numeric instances with a common NumericUpDown subclass. Wheel edits
  require a prior left click inside that control and retained focus. Losing focus
  revokes activation. Hover and keyboard-only focus must not change the value;
  blocked wheel events scroll the enclosing viewport. Buttons, typing and keyboard
  increments retain their normal behavior.

Use failing regression tests in EditorRefinementTests before implementation. Test
all four languages and enlarged fonts, parent scrolling, click/focus transitions,
decimal values, bounds and readonly fields. Keep the standard runner at 32 suites,
Framework 4.8, .NET 10 and offline operation. Verify both builds and full runners.
No GitHub publication in this task.

Completed: native child-message wheel tests and managed scroll/bounds tests pass.
All 32 suites pass on Framework and .NET 10; EditorRefinementTests has 215 checks.
Both SDK targets build without warnings/errors. Read-only independent review found
no actionable issues. Reports: build/test-results/20261118-065226 and
build/sdk-test-results/net10.0-windows/20261118-065809.
