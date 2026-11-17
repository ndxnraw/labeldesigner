# Offline barcode catalogue

Extend the existing local ZXing based implementation, inspired by the categories at
https://barcode.tec-it.com/en/ (no remote encoding service or copied assets).
Preserve existing IDs and output, Framework 4.8 and .NET 10, and XML compatibility.

1. Add regression coverage before implementation: six linear formats (UPC-E,
   Code 93, Code 39 Full ASCII, Codabar, MSI without checksum, Plessey), plus
   Aztec, PDF417, GS1 QR and GS1 Data Matrix.
2. Centralize available formats, samples, hints and family filtering. Validate
   alphabets, checksum and GS1 AI syntax before rendering. Keep GS1 data XML safe.
3. Add a searchable chooser with explicit sample insertion, and expose every
   format in existing editors. Preserve rectangular PDF417 geometry.
4. Test serialization, decoding, output, invalid inputs and UI; run the full
   standard suites on both targets. Document limitations and clean-clone handoff.

MSI does not add a checksum; Plessey includes its native CRC. Full ASCII accepts
printable ASCII (32–126) because project XML cannot store arbitrary control codes.
GS1 2D input uses explicit parenthesized AIs from the existing supported AI list;
FNC1 and variable-field separators are generated internally. Postal, medical,
composite and payment templates remain outside this increment.

Completed: 21 catalogue choices, all 31 suites passed on both runtimes, final
281-check catalogue regression passed on each runtime. Source review findings
(PDF417 preflight and QR percent escaping) fixed and regression covered; GS1
Data Matrix short-field corruption addressed by deterministic ECC200 ASCII.
Updated user/handoff/license/checksum/packaging documentation. No publication.
