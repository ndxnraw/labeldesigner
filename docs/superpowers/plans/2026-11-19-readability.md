# Barcode readability before printing

1. Extend BarcodeCatalogTests with final-raster round trips for the requested
   formats, physical resolution and calibration, clipped and covered codes,
   batch attribution, automatic limits and cancellation.
2. Share barcode geometry with the vector renderer and track each barcode back
   to the original placement. Inspect the calibrated output and decode localized
   crops at the actual or estimated device resolution with bundled ZXing.
3. Add nonblocking issues to print preflight. Cache results per immutable plan and
   printer geometry. Provide the per-code report and an optional full batch check
   using the existing cancellable background dialog. Highlight the chosen result
   on the affected preview page.
4. Translate all controls and warnings, document the diagnostic limits, review
   the implementation and test on .NET Framework and .NET 10.
