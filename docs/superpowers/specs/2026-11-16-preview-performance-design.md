# Print preview performance — seventh package

The owner requested measured improvements to print-preview performance and
responsiveness, preserving both offline Framework 4.8 and modern .NET 10 Windows.
The baseline is published source commit 7e0b8b2. Product remains Pre-release.

Measure before optimizing: add inexpensive per-request stage timing, then run the
same standalone benchmark sequentially on both runtimes against instrumented
baseline binaries. Retain those binaries/results under ignored build/phase7/before.
Benchmark cold/first and reopened windows, repeated preview, orientation changes,
page navigation, calibrated media and UI heartbeat. Separate shell construction,
UI capture, queue wait, discovery, setup, inspection, rendering, delivery and UI
application; report median/p95, memory and GDI resources. No physical jobs.

Optimize only demonstrated costs. Keep driver access on the existing bounded STA
worker. Reuse immutable session data/artwork where safe; bound caches by entry
count and bytes, dispose native resources, reject stale deliveries and invalidate
on document/job recalculation and relevant printer/media/DEVMODE changes. Printer
refresh must discard stale capability data. Actual submit must refresh relevant
driver facts and rerun approval checks, even if preview reused work. Never cache
an approved report as proof of current printer state. Preserve exact calibration,
clipping, page dimensions, frozen values and independent per-media settings.

Both runtimes must use the same behavior, tests and benchmark scenarios. Add
regression tests for each cache invalidation/ownership rule, stale requests,
preview/uncached image equality, fresh submit checks and bounded memory/handles.
Run complete suites on both targets and package self-tests; compare before/after
sequentially on the same machine and driver, not older concurrent test timings.
Use operation counts for deterministic regression gates; report wall-clock values
without machine-independent promises. Update handoff/user/testing documentation
and save a standalone comparison report. Publication is a separate user action.
