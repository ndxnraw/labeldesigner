# Managed updates — eighth package / Pre-release

User approved explicitly confirmed updates, both runtimes and offline work.
Repository is now public (owner clarification verified); no tokens or credentials.
No network on startup or timers. Only the update manager's Check/Download actions
contact the fixed ndxnraw/labeldesigner Pre-release release over HTTPS.

## Identity and trust

Keep product informational version Pre-release. Each ZIP contains a strict XML
package-manifest.xml: source commit, runtime family and complete file SHA256 list.
Its content-derived PackageId identifies repacks even at the same source commit.
Generate UPDATE-INDEX.xml after both ZIPs; pin commit, family, size, ZIP hash and
PackageId. Online check pins the GitHub release asset IDs and server SHA256 digests,
verifies the index digest, and requires agreement between index, asset metadata
and extracted package. No latest/stable/numeric product versions.

Offline import uses a locally downloaded ZIP and matching SHA256SUMS file, plus
explicit confirmation of trusted origin. A checksum verifies integrity, not an
independent publisher signature. No signing certificate is introduced or claimed.

## Prepare, activate, return

Use verified side-by-side installations under LocalAppData/NDLabelMaker/Updates,
separate per runtime family. Never overwrite the running application or its unknown
files. This also preserves portable projects and avoids executable-lock replacement.
Extract to a unique temporary directory; reject traversal, absolute/ADS/reserved
paths, symlinks/reparse entries, duplicates/case collisions, undeclared files,
expanded-size/entry limits and wrong platform. Verify every extracted file before
atomic directory promotion. Cancellation/failure removes only owned temporary data.

After preparation, a separate confirmation explains restart. Respect unsaved-document
Save/Cancel. Back up startup preferences, layout, profiles and workspace templates
locally before activation, excluding active recovery locks; never modify originals.
Atomically save activation history only for validated packages. Retain current and
previous installations. On subsequent launches, only previously authorized roots
may redirect to the selected validated installation; this reads local state only.
Rollback requires confirmation, verifies the previous package, switches activation
and restarts; shared settings are retained (backup remains available for recovery).
No elevation, forced process termination, target-directory deletion or silent install.

## Verification

HTTP transport is injectable; simulate offline, redirects, cancellation, truncation,
hash mismatch, changed mutable release, wrong family, zip bombs/paths, locked files,
failed preparation/activation, rollback and local redirect. Never launch untrusted
fixture executables; use a launch callback. Exercise real UI consent and unsaved
document cancellation, all four languages, both full suites and packaged self-tests.
Update build/package/CI manifests and clean-clone handoff documentation. Publication
is a separate user action; first adoption of this updater requires the eighth ZIP.
