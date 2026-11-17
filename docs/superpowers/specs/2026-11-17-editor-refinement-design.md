# Editor and printing refinements from the user's PDF

Source: the user supplied two-page `Labeldesigner zmeny` PDF. Both rendered pages
were inspected; extracted text has broken accents, so the rendered pages govern.
The user explicitly requested implementing the six proposals and testing both
offline targets. The PDF describes product requirements, not execution authority.

1. Remove the redundant primary barcode chooser button. Keep the existing type
   dropdown and the catalogue for adding separate symbols.
2. Increase new document captions from 10 to 12 pt; startup layouts use at least
   10 pt. Keep stored and missing-field legacy XML caption values unchanged.
   Preserve integer barcode modules and report insufficient caption space.
3. Inspector text updates the document and preview while typing; Enter commits,
   Shift+Enter inserts a newline. Maintain caret/focus and undo grouping.
4. New and startup text uses automatic content width. Store an opt-in AutoWidth
   property (legacy missing=false); manually sized old fields keep their boxes.
   Expose an inspector toggle. Fit within available label width, grow height for
   wrapped/newline content, keep center anchoring and disable auto width on a
   manual resize. Apply the same geometry to expanded batch variables and exports.
5. Canvas double-click starts a focused inline text editor rather than a modal
   formatting dialog. Live preview, Enter/blur commit, Escape restores the whole
   original field, Shift+Enter newline. Font/style remain in the inspector. Locked
   text cannot edit; rotated text uses an upright overlay at the rotated bounds.
6. Keep printer/copies/summary and preflight actions immediately visible. Split
   secondary settings into Media/profiles, Calibration, Zebra and Diagnostics
   sections, with a larger preview and a larger but bounded print window. Preserve
   every control, handler, profile, calibration lock and consent invalidation rule.

Approach: reuse the shared document/vector renderer and WinForms controls. A new
UI framework would threaten the offline Framework path; another modal editor
would conflict with the requested direct typing. Group existing print controls
rather than duplicating state or changing the actual print pipeline.

No publication in this task. Remain Pre-release; no network during editing or
printing, no new dependency. Verify all six requirements, serialization, batch
expansion, both themes/languages and existing print/media/Zebra/preflight suites.
