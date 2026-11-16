# Print reliability and calibration — sixth package

The owner approved continuing the proposed sixth package: per-media calibration,
clear print diagnostics, equivalent behavior on both runtimes and usable scaled UI.
Keep both compatible Framework 4.8 and .NET 10 Windows builds, product Pre-release.

## Calibration contract

Preserve existing X/Y offsets; add independent X/Y scale factors defaulting to 1
when old XML omits them. Validate finite offsets within ±100 mm and factors within
0.8–1.2. Corrections affect printing and print preview only; document geometry,
saved label files and exports retain their designed dimensions. Apply scale about
the page origin, then translation in physical millimetres. Use identical corrected
geometry for clipping checks, label bounds, preview and actual print rendering.
Any changed correction invalidates the reviewed output fingerprint.

Replace the fixed calibration shortcut with a scrollable measurement wizard.
Create a target at the current output page size with three crosshairs and reference distances;
their reference distances are adaptive for small media and capped at 100 mm.
Reject pages smaller than 12 mm in either dimension. Print it through the existing
reviewed print UI using zero offsets and unit scale, preserving printer, medium,
orientation and driver clipping choice. Never auto-submit a physical print job.
Clone the full active printer/page setup, including DEVMODE, resolution and paper
source. Lock printer, media, orientation, correction and driver-property changes in
the target window. Disable driver fitting in the original window before calibration;
the measured target and corrected job must use the same driver setup.
Require explicit confirmation that this uncorrected target was measured before
applying. For anchor a, measured origin m and reference/measured span ratio s,
save scale=s and offset=a-s*m separately on each axis. Reject implausible inputs
without modifying the profile or controls. Save through the existing profile store;
other printers/media remain untouched. Reprinting and remeasurement replace the
calibration, rather than compounding it. Keep direct correction controls available.

## Diagnostics contract

Show document dimensions, configured driver media, printable area, hard margins,
orientation, page/repeat counts, correction factors, clipping mode and runtime.
Explicitly state that actual loaded media is unverified. Configured dimensions
are not evidence of a physical paper sensor reading. Offer local text export.
Persist bounded print-submit diagnostics alongside existing errors; do not include
printer/profile names, document values, paths or exception messages in that log or
the exported numeric report. Logging failure must never block printing.
Spool submission remains distinct from confirmed physical printing.

## Verification

Add one shared suite covering old-profile defaults, invalid/independent profiles,
known measurement recovery, rasterized scale/offset/origin and native clipping,
review invalidation, diagnostic privacy/rotation, real profile/UI integration and
wizard layout at 100/125/150/200% with all four languages. Run the whole standard
suite on Framework and .NET 10, SDK dual build and both packaged self-tests.
Update user/build/test/handoff docs. Physical device accuracy, real monitor DPI
transitions and Word remain explicitly manual checks. Do not publish this package
without a subsequent user instruction.
