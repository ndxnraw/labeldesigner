# On-demand print queue status

1. Add regression cases for exact correlation, missing or ambiguous jobs,
   Windows status flags and preservation of a previous observation.
2. Pass one opaque document identifier through the approved Windows and Zebra
   print paths. Query the Windows spooler from a bounded background operation.
3. Save observations alongside existing history records and add an on-demand
   refresh action. Preserve history ordering and existing archives.
4. Verify the fake queue, print history dialog and both Windows runtimes.
