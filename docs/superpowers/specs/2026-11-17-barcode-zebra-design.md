# Readable EAN captions, separate GTIN-14 and Zebra darkness

Owner requested readable defaults, separate EAN-14/ITF-14, and Zebra darkness.
Owner clarified EAN-14 means GTIN-14 encoded as GS1-128 with AI (01).

- Preserve existing Kind=14 as ITF-14, including bearer bars and old project output.
  Add Kind=114 for the new GTIN-14 / GS1-128 (01) choice, check/complete the GTIN
  check digit, encode FNC1 + 01 + digits, show (01) in the caption only. Keep generic
  Code128/GS1 functionality and old XML intact.
- New documents use a 10pt caption; startup layout stops shrinking captions to 4pt
  and uses an 8pt floor. Explicit sizes in old documents and missing-field legacy
  defaults remain unchanged. Fit startup code width to the caption where possible;
  impossible small labels retain warnings instead of silently shrinking to illegibility.
- Add explicit Zebra ZPL printing to media profiles. Windows printing remains the
  default. ZPL DPI and optional darkness 0–30 (tenths) are profile settings. A numeric
  darkness cannot be portably injected into a vendor's private GDI DEVMODE; send it
  with the rendered label in one RAW ZPL spool job, never a separate pre-job command.
- Reuse document page rendering, calibration, clipping and output review. Include
  transport/DPI/darkness in approval identity. Prepare bounded raster/ZPL before
  opening the spool job. Partial writes, errors and cancellation abort the owned job.
  No native printer command or physical test print is authorized during development.
- Explain that RAW ZPL uses the selected dimensions, DPI and application calibration;
  other driver finishing/media settings are not applied. An explicit darkness changes
  the printer's setting and may affect later jobs until changed again. No pretend
  job-local restore or guessed vendor DEVMODE offsets.
- Both .NET Framework4.8 offline and .NET10 remain supported. Pre-release stays the
  product name. No publication in this implementation task.

References: GS1 GTIN carrier guidance, Zebra darkness ~SD/^MD documentation, Microsoft
RAW WritePrinter/StartDocPrinter documentation. Physical label readability, darkness
and driver/media behavior require a later user-controlled trial on the actual Zebra.
