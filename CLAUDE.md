# CLAUDE.md — AfterPub

> v0.9. Describes AfterPub as it is built, in the present tense; section 14 lists what is not
> built yet, what is still unconfirmed, and what to do next. **[PROPOSED]** marks an idea
> suggested by Claude that the author has not confirmed. "Verify" means a detail to check
> against current documentation or real testing before relying on it.

## 1. Purpose

Microsoft Publisher is end-of-life and its `.pub` format is proprietary. **AfterPub** is a
Windows desktop app that **finds `.pub` files in a folder tree and shows which of them
already have matching derivative files (PDF, ODG, SLA), so you can see at a glance what is
still missing.** Converting the missing ones is the secondary feature.

- **Scanning is the strength.** A scan always looks for every `.pub` file and always
  reports PDF, ODG and SLA status for each, whatever the person intends to convert. It
  works with no conversion engine installed at all.
- **Conversion fills the gaps.** Primary output: high-quality **PDF** (a faithful visual
  copy). Ultimate goal: get documents into an **editable** format. PDF alone is not
  practically editable, so the app also supports optional editable outputs (ODG, SLA).
- **Engine quality is uneven, and the UI says so.** Publisher (COM) gives perfect PDFs.
  LibreOffice and Scribus read `.pub` through the same library (libmspub); their results
  are rough (section 3.1). They are best-effort, labelled in the UI, and not worth much
  more engine work.
- Audience: the author first, but built to be useful to other people, so it should be
  polished, documented, and forgiving. Different users will edit in different tools.
- **Publisher's retirement shapes the priorities.** Microsoft 365 subscribers lost
  access to Publisher on October 1, 2026; copies from a perpetual license still install
  and run, without support. The Publisher engine therefore serves only people who still
  have it, including the author, who can use it while it lasts to make reference PDFs of
  their own collection. For everyone else, the LibreOffice and Scribus engines, and the
  quality of the libmspub import they share, are the product.

## 2. Core User Experience

The main window is **one list** (a `DataGridView`). Each row is one source `.pub` file.

**Window layout, top to bottom:**

1. The **scan row**, deliberately the most prominent part of the window: a large folder box,
   Browse, Include subfolders, and a large bold **Scan** button.
2. The **filter row** (Show / Path contains / count), directly under it.
3. A **Conversion Settings** bar that opens and closes a collapsible area holding output
   location, the **Convert to** formats, the PDF engine, and the overwrite choice. The area
   is **collapsed by default** (its open/closed state is not saved). While it is closed the
   bar summarizes the choices that matter (what gets converted, which engine, and what
   happens to existing files), so a hidden setting never surprises the person.
4. The Convert selected, Select not converted, Cancel and Abort now buttons.
5. The grid, which takes all remaining height.

Opening or closing the settings area grows or shrinks the window by the same amount, so
the grid keeps its size. The window never grows past the usable screen height: it slides up
if its bottom edge would leave the screen, and on a tall window the grid gives up the
difference. Closing the area gives back exactly the height it gained.

**Title bar.** It shows the two shortcut keys, for example
`AfterPub            F1 About  ·  F2 Dark mode` (the F2 text names the mode it switches to).
During a scan it shows scan progress instead.

**Grid columns.**

- **Folder** and **File Name**, then a **.pub** column showing the source's date and size.
- One status column each for **PDF, ODG and SLA**. These are always present and always
  filled in by a scan, whatever is ticked under **Convert to**. A cell shows the output's
  **date and size** when the file exists and is **blank** when it does not. After a
  LibreOffice or Scribus conversion the engine is appended, for example `(Scribus)`; a
  failed conversion reads `Failed: <reason>`. An output older than its `.pub` starts with
  a warning sign (⚠) and is drawn in a warning color (section 3.3).
- A row has no overall "converted" verdict: the person reads the columns and judges for
  themselves. There is no row-header column.
- Dates use the short date format of the person's Windows region settings.

**Sorting and filtering.**

- Click any header except the tick box to sort. The `.pub` column sorts by the source's
  date and the three output columns by the output's date; ascending puts missing outputs
  first, then existing ones by date. Folder and File Name compare as text, ignoring case,
  each using the other as tie-breaker.
- A new scan starts sorted by **Folder** ascending, with the arrow showing, so the order
  never depends on the file system's enumeration order. The chosen sort and filter survive
  a re-scan.
- The **Show** list offers All files, Missing PDF, Missing ODG, Missing SLA, Missing any,
  and Out of date. **Path contains** filters on the full path. A "Showing X of Y" count
  sits beside them.
- Rows hidden by a filter are unticked, and select-all, Select not converted and Convert
  act on visible rows only.
- Each grid row carries its `ConversionRow` in its `Tag`, so no code depends on row
  positions.

**Right-click** on a **PDF, ODG or SLA** cell that has a file offers **Open** (ODG with the
detected LibreOffice, SLA with the detected Scribus, otherwise the Windows default app) and
**Show in folder**. There is deliberately **no** menu on the `.pub` column: the app has no
"open" action for source documents.

**Keys.** F1 opens About; F2 switches light/dark mode (section 8). Both are temporary
homes until a menu exists.

**Actions.**

1. **Scan** a folder, current directory only or recursively. It runs on a background thread,
   shows the number found so far in the title bar, and **Cancel** stops it, leaving the
   previous grid contents. A scan that finds nothing says so.
2. **Convert** the selected rows to the formats ticked under **Convert to**, with progress
   and per-file success/failure reporting. One bad file never aborts the batch.
3. **Cancel** or **Abort now** a running batch (section 3.4).

The UI stays responsive during scans and conversions: work runs off the UI thread.
Publisher automation is COM-based, so take care with threading (verify).

## 3. Conversion Architecture

Conversion is **pluggable at two points**: the engine that reads `.pub`, and the output
target. Both are selectable by the user.

### 3.1 Engines (`IConversionEngine`)

| Engine | Needs | Notes |
|---|---|---|
| **Publisher automation** | Microsoft Publisher installed | Best fidelity. Availability is whether the `Publisher.Application` COM ProgID is registered (no launch). Late binding (`Type.GetTypeFromProgID`) gives no compile-time Office dependency. Export is `Document.ExportAsFixedFormat(pbFixedFormatTypePDF, filename)`; available since Publisher 2007. Works with 32-bit Office. |
| **LibreOffice** | LibreOffice installed **by the user** | Libmspub-based import, run headless: `soffice --headless --convert-to pdf --outdir "<dir>" "<file>"`. **Must** launch with its own profile via `-env:UserInstallation=file:///<path>`: without it a conversion can silently fail if the user already has LibreOffice open. Launched with `Process` and waited on with `WaitForExit`; 90-second timeout. |
| **Scribus** | Scribus installed **by the user** | Same libmspub import, run headless through a Python script: `Scribus.exe -g -py <script>` using the Scripter API (`openDoc()`, `PDFfile()`, `save()`, `closeDoc()`). Paths reach the script as environment variables (`AFTERPUB_INPUT`, `AFTERPUB_OUTPUT`, `AFTERPUB_RESULT`), not arguments. The script writes a result file (`OK` or `ERROR: ...`); the engine stops waiting as soon as it appears, gives Scribus 5 seconds to exit, and kills it if it lingers or after 90 seconds. Detection (`ScribusLocator`): a path from settings first, then any `Scribus*` folder under `Program Files` / `Program Files (x86)` containing `Scribus.exe`, highest version wins (compared numerically). Unlike the other engines it also imports Publisher's off-page scratch-area content, as an extra final page; the author considers that an improvement. |

**LibreOffice and Scribus are not bundled.** The app only detects an existing install:
first a path set in settings, then standard install locations.

**Auto** tries the engines in this order: Publisher, then LibreOffice, then Scribus (the
order lives in one place, `EngineResolver`). The user can pick one explicitly instead. If
no engine is found, a plain message says at least one is required and names the LibreOffice
and Scribus download pages in text (the app itself makes no network calls). The engine
settings show what was detected and where. A file an engine cannot open (for example a very
old Publisher version) is reported as failed with a clear reason, and the batch continues.

**Known limits of the libmspub engines (LibreOffice and Scribus).** Text loss is a limit of
the shared library, not of either program, so **Publisher is the only engine that
preserves everything.**

- On a real Publisher 2010 file, LibreOffice imported a 2-page spread correctly and
  rendered 7 of 8 pages well, but silently dropped the entire contents of one text box (plain,
  not grouped or rotated, but with several distinct text-formatting runs). The leading
  hypothesis is that libmspub trips on a mixed-run-formatting pattern; the trigger is
  unconfirmed.
- Scribus dropped text from a page of a second, ordinary file too, and on the stress file it
  kept an empty text box and mis-sized some image borders.
- On the stress file (9 pages in Scribus: the original 8 plus the scratch-area page),
  Scribus produced a PDF of about 183 MB against about 3.3 MB from Publisher. Image
  settings did not change this meaningfully, and the cause was not found. An ordinary file
  gave a reasonable size.
- The UI therefore labels each PDF made by LibreOffice or Scribus with the engine (for
  example `(Scribus)` after the date and size), and the batch
  summary warns that these PDFs should be checked against the originals. The label is not
  persisted: a later rescan shows the plain date and size until the record file (section 7)
  exists.
- The planned safeguard is the confidence score's per-page text check (section 5).

### 3.2 Outputs

1. **PDF (always available).** Written by the chosen engine. Quality and preservation
   options are not built (section 14).
2. **ODG (optional editable companion).** **PDF -> LibreOffice Draw** via headless import:
   `soffice --headless --infilter=draw_pdf_import --convert-to odg --outdir "<dir>"
   "<file.pdf>"`. It works better than importing the `.pub` directly. Known limits: text
   becomes separate positioned boxes; master pages and text flow are lost. This stage
   **requires LibreOffice** regardless of which engine produced the PDF.
   `RowConverter` (Core/Conversion) orchestrates it per row. Whether the intermediate PDF is
   a kept, real output or a deleted scratch file follows from whether PDF is itself ticked.
   A scratch PDF lives in a folder of its own under the system temp directory
   (`AfterPubScratch\<unique>`), named after the source so the ODG has the right base name,
   and the folder is removed afterwards. It can therefore never be, or overwrite, a real PDF
   next to the source. The ODG checkbox is not disabled when LibreOffice is missing: the
   row fails with a clear message at convert time.
3. **SLA (optional, Scribus's native format).** Saved straight from the imported `.pub` with
   `saveDocAs()`, not from a PDF, so asking for SLA never creates a PDF and an SLA failure
   never affects the PDF or ODG results (`RowConverter` calls an `IPubToSlaConverter`,
   implemented by `ScribusConversionEngine`). The SLA checkbox is enabled only when
   Scribus is detected. Quality is limited by libmspub. **Idea, not built:** generating the
   `.sla` from Publisher's PDF instead, as ODG is, which may look better for people who still
   have Publisher; untested.
4. **Later:** extracted assets (text and original images at full resolution), and a
   structured export (with Publisher automation, walk the document object model and write
   structured data that could feed other converters).

Constraints: **no online dependencies, no subscription products, no Microsoft Word as a
target.** The app works fully offline.

### 3.3 What "converted" means

- Status is tracked **per output type** and read live from the file system. A scan always
  reports PDF, ODG and SLA for every `.pub` found, independent of the **Convert to**
  checkboxes, which only decide what a conversion creates. A scan checks only whether a
  file exists, so it needs no engine. Outputs the app did not create are treated the same
  as ones it did.
- There is **no overall "fully converted" status** in the UI. The only place a rule is
  needed is **Select not converted**, which ticks every row missing an output for any format
  currently ticked under **Convert to**. (`ConversionRow.IsFullyConverted` still exists in
  Core and covers all three outputs for scanned rows; the UI does not use it.)
- After each file is converted, the app re-reads that row's outputs from disk, so Select not
  converted, sorting, filtering and the overwrite check never use stale scan data.
- **Nothing partial is ever left in place.** Every engine writes to a private temporary
  folder (unique per conversion, under the system temp directory) and calls
  `OutputFileMover.MoveIntoPlace` only after success, so an existing output file always means
  a complete one, and a good file that is already there is replaced only at the very end. The
  temporary folder is always deleted, including after a failure, timeout, cancel or abort.
  On the same drive the move is a rename; on another drive or a network share the file is
  first copied to a temporary name (`*.afterpub-tmp`) beside the destination and then
  renamed, because a plain cross-drive move is a copy followed by a delete.
- **Out of date** is judged from file dates. An output that exists but is more than two
  seconds older than its `.pub` (`ConversionRow.IsOutOfDate`) is shown with ⚠ and a warning
  color, with an explanation in the tooltip, and the **Show** filter has an "Out of date"
  choice. A missing output is "missing", never "out of date". Because copying and cloud sync
  can move dates, this is a hint to look, not proof. **Select not converted does not tick
  out-of-date rows**: re-making hundreds of good PDFs because a sync shifted dates would be
  a costly surprise; use the filter and the tick boxes instead. The stored size-and-time
  comparison from the record file (section 7) would be more reliable.
- **Separate output folder:** the source folder structure is mirrored under the output
  folder, so `A/Flyer.pub` and `B/Flyer.pub` never collide.

### 3.4 Batches, cancelling, failures

- **Existing outputs: the default is to ask, once, up front.** The batch runs over the rows
  the person has ticked. Before it starts, the app checks which ticked rows already have an
  output file in **any format ticked under Convert to**, and the overwrite choice in the
  Conversion Settings area decides (it always starts as Ask and is not saved): Overwrite
  proceeds, Skip silently excludes those rows, and Ask (the default)
  shows a small custom dialog (custom because `MessageBox` cannot take custom button labels).
  The dialog offers Overwrite All, Skip These and Cancel, names each format that already has
  files and how many (for example `ODG: 5 files`), says that Overwrite All replaces them,
  and makes **Skip These the Enter key's default**, so a stray Enter never destroys anything.
  Skipping works per file, not per format: a file with an existing ODG is skipped entirely,
  even if its PDF is missing and PDF is also ticked. A granular per-file Yes / No / Yes-to-all
  flow is not built; ticking fewer rows and converting in more than one pass gives the same
  result.
- **Cancel and Abort now.** Two buttons next to Convert, enabled only while a batch runs.
  **Cancel** finishes the file currently converting, then stops. **Abort now** stops the
  engine process immediately: LibreOffice and Scribus run as processes the app starts and
  register them with a shared `ProcessTracker`. For Publisher, the engine registers only the
  Publisher process it started itself (found by comparing running `MSPUB` processes before
  and after launching), so Abort now never touches a Publisher the user already had open.
  Because every engine writes to a temporary folder (section 3.3), an abort leaves nothing
  partial behind. Unfinished rows stay as they were, and every batch ends with a summary of
  converted, failed, skipped and cancelled/aborted counts. Abort now is confirmed working in
  a small real batch with every engine.
- **Slow-file notice.** If a file is still converting after ten seconds, its row reads
  "Still working... (an engine may be waiting on a dialog)", for any engine.
- **A stuck Publisher stops the batch.** Each Publisher file has a 3-minute timeout, after
  which the process the engine started is stopped and the file fails with a clear message.
  If **3 files in a row** time out (`PublisherConversionEngine.MaxConsecutiveTimeouts`), the
  engine gives up: the batch stops, the remaining rows stay ticked, and the summary says
  Publisher did not respond and suggests checking its license or sign-in window, or choosing
  another engine. Any file that finishes, successfully or with an ordinary error, resets the
  count, so one bad document never stops a run; only a Publisher that stops answering does.
  The author believes they have seen a stuck conversion stopped cleanly in real use, but it
  is hard to tell and it has not been tested deliberately, and the three-in-a-row stop has no
  unit test because it needs a real Publisher that hangs.
- **The engine never harms the user's own Publisher.** It stops, hides or quits only a
  Publisher process that appeared while it was starting its own instance. If automation
  ever attaches to a Publisher the user already had open, that one is left alone.
- **Selection helpers.** A select-all checkbox in the first column's header (three states:
  none, some, all) and the **Select not converted** button (section 3.3). Rows start unticked
  after a scan.
- **Failures never stop the batch** (except the stuck-Publisher case above). Each problem
  becomes a status with a reason. Based on the author's actual file collection:
  - **Expected, must be handled well:** very old Publisher versions (Publisher 2010
    confirmed present; Publisher itself opens these fine, the risk is on the libmspub
    path), and 2-page spreads (LibreOffice imported spreads correctly; the confidence
    score must treat a spread's page-count mapping as expected, not a warning sign).
  - **Not expected in the author's files, so handled defensively only:** password-protected
    files, missing linked images, mail-merge documents, locked/read-only files, network
    paths. They must not crash or hang the batch (a timeout and a clear failure status is
    enough) but need no dedicated testing or UI treatment for v1.
- **Scanning skips what it cannot or should not read.**
  - *Protected folders.* The scan uses `EnumerationOptions` with `IgnoreInaccessible` and
    skips anything carrying the `System` attribute (for example `System Volume Information`
    and `$RECYCLE.BIN`), so scanning a whole drive does not abort on a protected folder.
    **Hidden files and folders are scanned**, and reparse points are not skipped, so
    cloud-sync placeholders stay visible. Skipped folders are never reported to the person.
  - *SSH public keys.* Files ending in `.pub` can also be SSH public keys, which are not
    Publisher documents. They are skipped **silently and never named in the UI or logs**, so
    key files are not exposed. `SshKeyDetector` makes this a content check on the first
    bytes, not a folder rule, because keys get copied out of `.ssh` folders. A file with the
    OLE signature (used by Publisher files) is never treated as a key; anything not clearly a
    key is **listed**, because hiding a genuine document is worse than showing a stray file.
  - *Responsiveness.* `IPubFileScanner.Scan` takes a `CancellationToken` and an
    `IProgress<int>`, and the UI runs it with `Task.Run`.

## 4. Scope by Version

- **v1:** everything in this document not marked v2 or "later".
- **v2:** command-line mode (section 10), font-install flow and library-wide font summary
  (section 6), visual-comparison scoring (section 5), asset extraction, other editable
  targets.
- **Core must stay free of UI code** so a command-line front end can be added later without
  rework.

## 5. Confidence Score (v1: simple checks; not built)

Each conversion gets a **0-100 confidence score**. It is a triage indicator, not proof of
correctness; there is no ground truth for "how good is this conversion". The score must be
explainable: a detail view or tooltip lists what raised or lowered it.

v1 checks (cheap, objective, offline):

- Output page count matches the source page count (when the engine can report the source's).
  **2-page spreads:** do not treat a spread's page-count mapping as a mismatch; confirm the
  exact mapping (one wide page vs. two pages) once real conversions can be inspected.
- **Per-page text length looks reasonable** relative to the page's other pages and visual
  content, not just "not entirely blank". This exists because testing found a page with a
  fully rendered layout but one silently empty text box (section 3.1).
- Engine warnings and errors; output file not suspiciously small.
- Fonts missing or substituted (section 6).
- Converted through the fallback engine, if fallback is ever built (section 14).
- For the editable stage (PDF -> ODG): text extracted from the ODG compared with text
  extracted from the PDF.

Later (v2): render pages of both outputs to images and compare visual similarity.

Scoring weights live in one place in code and are covered by unit tests.

## 6. Missing Fonts (not built)

Missing-font information is **lost in conversion** (the engine substitutes a font and the
output PDF lists only the substitute), so it must be captured from the *source* at
conversion time and compared against the fonts installed on the machine.

v1:

- Capture the fonts the source used: with Publisher automation, read font names from the
  document's text (exact object-model calls still to confirm against a real document); on
  the LibreOffice route, read them from the ODG produced from the `.pub`.
- Compare against installed fonts using `System.Drawing.Text.InstalledFontCollection`
  (confirmed available; `.Families` gives installed `FontFamily` names). Confirm it can be
  called from Core's background logic without UI-thread issues. Match on font **family**
  name; expect edge cases (bold/italic variants, "Narrow" faces, per-user vs. system-wide
  installs, theme fonts, embedded fonts). Test with real files.
- Two optional columns: **Missing fonts (count)** and **Missing fonts (names)**.
- Feed missing fonts into the confidence score, and store the full list of fonts the source
  used plus the subset missing at conversion time.

v2:

- Notify the user at conversion time and give them a chance to install a font.
- Compare the stored used-fonts list with fonts installed *now* and suggest re-converting
  files whose missing fonts have since been installed, without re-reading the `.pub`.
- A library-wide summary such as "Font X is missing and affects N files".

## 7. Persistence (no database)

**No database.** Most columns are read live from the file system on every scan and are
never stored: whether an output exists, its date and size, and whether it is older than the
source.

Only information that is lost in conversion is to be recorded, at conversion time. None of
this is built yet:

1. **Per-folder record file** (`.afterpub.json`, one per scanned folder). Plain,
   human-readable JSON. Entries are keyed by path relative to the folder and store the
   source's size and modified time (to detect stale records), plus timestamp, engine used,
   confidence score and breakdown, fonts used, fonts missing at conversion, and warnings.
   Include a schema version field. **[PROPOSED]**
2. **Missing-fonts report** (human-readable courtesy file), named
   `<OutputBaseName>-MISSING-FONTS.txt`, created next to the output file **only when fonts
   are missing**. Plain `.txt`, **write-only** (the app never reads it back), regenerated on
   each conversion.
3. **Log file.** A single log, appended to, with a header line for each run and a size cap.
   **Location is a user setting.** Default: the top folder the conversion was started from;
   if that folder is not writable, fall back to a folder next to the exe, then the temp
   folder, and say so in the status bar.

The record file and the missing-fonts report are **user settings** (on/off each).

Graceful degradation:

- If the record file is missing or turned off, nothing breaks. Status, dates, and sizes
  still work; recorded columns (score, fonts, engine, warnings) show "unknown" until the file
  is converted again.
- The scanner must ignore all of the app's own generated files (record, reports, log).

## 8. Settings

Stored in a plain file next to the executable (portable-friendly): `afterpub.settings.json`,
via `AppSettings`/`AppSettingsStore` in Core. A settings file written by an older version
still loads, with defaults for anything it lacks.

**Saved and wired into the UI:**

- Conversion engine (auto / Publisher / LibreOffice / Scribus) and the optional LibreOffice
  and Scribus paths.
- **Convert to** targets (PDF, ODG, SLA): which formats a conversion creates. They do not
  affect what a scan reports.
- **Output location:** next to each source file (the author's own default) **or** a separate
  output folder. The list reads outputs from whichever is configured.
- Scan mode default (current folder / recursive).
- **Color mode (light / dark)**, saved as `ColorMode` (`AppColorMode`, in Core, free of
  WinForms types). `Program.cs` reads it and calls `Application.SetColorMode` before any
  window exists, because WinForms applies the mode when windows are created and switching
  in a running app leaves some controls in the old colors. **F2** saves the other mode and
  offers to restart. The grid's header and lines are given dark colors explicitly in dark
  mode; the select-all glyph in the header is drawn by `CheckBoxRenderer` and stays
  light-themed. Anything that hard-codes colors must be checked in both modes.

**Deliberately not saved:** the overwrite choice (Ask / Skip / Overwrite). Every start of the
app begins with **Ask**, so a replace-everything choice never carries over to a later
session. `AppSettings` has no such property, so it is never written to the file, and an older
file that still has the line is read without error and loses the line the next time settings
are saved.

**Not built:** engine fallback on failure (section 14), PDF quality and preservation options,
record file on/off, missing-fonts report on/off, log file location, visible list columns.

## 9. Technology, Layout, and Distribution

- **Language:** C#. **Runtime: .NET 10.** **UI:** Windows Forms with a `DataGridView`. There
  is no WinForms visual designer in VS Code, so the UI is written in code; keep it small and
  well organized.
- **Distribution:** portable app: a **self-contained single-file** Windows build, no
  installer and no registry dependence. An installer may be added later.
- **Offline:** no network access required or used.
- **IDE:** Visual Studio Code with the .NET 10 SDK and the C# extensions.
- **Target platform:** Windows 10 and 11, x64. Dark mode works on both.

Solution layout:

```
AfterPub/
  CLAUDE.md
  .editorconfig
  Directory.Build.props      author, copyright and repository metadata
  src/AfterPub.App/          WinForms UI only; as thin as possible
  src/AfterPub.Core/         all logic
    Engines/                 engine classes, EngineResolver, ProcessTracker, OutputFileMover, ...
    Conversion/              RowConverter and conversion results
    Scanning/                scanner, ScanOptions, output path resolution, SshKeyDetector
    Settings/                AppSettings, AppSettingsStore, AppColorMode
  tests/AfterPub.Tests/      xUnit tests referencing Core
```

- All three projects target `net10.0-windows` (Windows-only features such as COM automation
  and font enumeration are used).
- Commands: `dotnet build`, `dotnet test`. Portable build later, roughly:
  `dotnet publish src/AfterPub.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
  (verify).

## 10. Command-Line Mode (v2)

Not in v1. When added, it should be a separate small console executable sharing Core (a
windowed WinForms exe behaves badly in a terminal: prompt returns immediately, exit codes and
piping are unreliable). Expected capabilities: list/scan with optional `--json`, convert,
the same options as the settings with the command line overriding, meaningful exit codes, and
a dry-run. Packaging would then need to ship both exes (a shared-runtime folder rather than a
single file). Decide when v2 starts.

## 11. Code Style (enforced via `.editorconfig`)

- **Allman braces** (the .NET convention): opening and closing braces each on their own line.
- **4-space indentation** for C# code. `.csproj`/`.json`/`.xml`/`.yml` files stay at 2
  spaces, per their own ecosystem's usual convention.
- **Explicit types**; avoid `var`. Private fields are `_camelCase` and are accessed as
  `this._field`.
- **Medium comment density:** explain intent and non-obvious decisions; do not narrate
  obvious code.
- Modern C# naming: `PascalCase` for types, methods, properties; `camelCase` for locals and
  parameters; interfaces prefixed `I`.
- Keep it simple. Prefer straightforward code over clever abstractions.
- **File-type naming:** lowercase with a leading dot in prose, code and filenames (`.pub`,
  `.pdf`, `.odg`, `.sla`); uppercase without a dot in UI labels (`PDF`, `ODG`, `SLA`).

## 12. Testing

- Unit tests (xUnit, in a separate test project) are required for non-UI logic: scanning
  (flat and recursive, hidden and system folders, progress, cancellation), per-type
  converted and out-of-date detection, output-path resolution (including mirrored folders),
  settings handling (including missing and corrupt files, and older files without newer
  values), engine resolution and detection, process tracking, row conversion (including that
  a scratch PDF never touches a real one), the safe file move, and the overwrite pre-check
  logic. The record file, font comparison and score calculation get tests when they are
  built.
- Engines sit behind `IConversionEngine` so tests use fakes. The unit test suite must not
  require Publisher, LibreOffice or Scribus.
- Real engine runs are covered by manual testing only. Integration tests with real `.pub`
  files are welcome but optional (skipped when the engine or sample files are unavailable).

## 13. Behavior Rules for Claude When Working in This Repo

- Ask before adding dependencies, and prefer none.
- Never add network calls or telemetry.
- Never modify or delete a user's source `.pub` files. The app is read-only on sources.
- Do not overwrite existing outputs without honoring the user's overwrite setting.
- Handle per-file failures gracefully, report them, and log details.
- Keep UI code out of Core.
- Run the tests before calling a task done.

## 14. Status and Open Items

**Built:** the scan and grid (always-on PDF/ODG/SLA status, sorting, filtering, out-of-date
warning, right-click open), conversion to PDF with three engines, ODG, and SLA, the overwrite
pre-check, Cancel and Abort now, the Publisher timeout and three-in-a-row stop, safe
temp-folder file moves, the collapsible settings area, dark mode, and the About box.

**Not built:**

- Optional grid columns (engine used, page count, score, missing fonts, warnings, path),
  with the right-click header chooser and saved visibility.
- Engine fallback: if the chosen engine cannot open a file, try the other once. Default:
  **off [PROPOSED]**. A file converted this way would get a warning that counts against its
  confidence score, and both failure reasons are shown if both fail.
- PDF quality and preservation options (the UI would show only what the chosen engine
  supports).
- The record file, which would also persist engine labels and allow a more reliable
  out-of-date check (section 7); the missing-fonts report and columns; the log file.
- The confidence score (section 5).
- Orphan outputs (a PDF with no matching `.pub`): not shown, and undecided whether wanted.
- Disabling the ODG checkbox when LibreOffice is not detected.
- The granular per-file overwrite flow (section 3.4).
- Extracted assets, structured export, command-line mode, portable packaging, and the
  README (section 15).

**Priorities after Publisher's retirement** (proposed, in order):

1. Use the Publisher engine now to make reference PDFs of the author's whole collection,
   while Publisher still runs. These double as the reference set for testing the open-source
   engines.
2. The confidence score, because it is the only protection against the open-source engines
   silently dropping text when no Publisher reference exists.
3. Reproduce the libmspub text-loss bug with a small `.pub` made while Publisher is
   available, and report it upstream; a fix there helps every user of LibreOffice and
   Scribus.
4. Investigate whether any tool can recover text separately (a plain-text companion file),
   so content is never lost even when a text box is dropped from the output.

**Open questions (unconfirmed):**

- **Sample files:** a set of about 8-12 representative `.pub` files for testing (a simple
  flyer, a multi-page newsletter, a spread, image-heavy, unusual or missing fonts, more
  old-version files), ideally with the PDF that Publisher itself produces as a reference.
  Keep private files out of the repo.
- Whether Publisher automation (`Activator.CreateInstance`) starts a new Publisher process or
  attaches to one the user already has open, and what a missing-font dialog does during
  automation. The engine is written to be safe either way (section 3.4). Test: open a document
  in Publisher, run a conversion, and check the user's window is neither hidden nor closed.
- How to suppress Publisher's UI alerts (missing fonts and so on) during automation.
- Whether Publisher needs `ActiveWindow` re-fetched per document (it is fetched once after
  `Open`); only relevant if multi-document batches behave differently.
- The exact Publisher object-model calls for reading a run's font name from document text.
- Whether `InstalledFontCollection` enumeration needs the UI thread when called from Core.
- The exact page-count mapping LibreOffice uses for a 2-page spread, so the score's page-count
  check can be tuned.
- Whether every Publisher version begins with the OLE signature (`D0 CF 11 E0 A1 B1 1A E1`).
  The SSH key check does not depend on it, but confirming against old real files would let the
  check be tightened. Verify with `Format-Hex -Path <file> -Count 8`.
- Why one stress file produced a ~183 MB PDF through Scribus.
- Whether any open-source tool can read the text that libmspub drops.

**Decided:** the name "AfterPub" is free on GitHub and has no USPTO conflicts; the plain
`.com` domain is taken and the author is comfortable proceeding without it.

**Lessons from real testing (Publisher automation and LibreOffice):**

- Publisher's `Application` has no `Visible` property; hiding is tried on `ActiveWindow`
  after `Open`, best-effort.
- `pbFixedFormatTypePDF` is `2`; `1` is XPS.
- Publisher's `Document.Close` takes no arguments. A failure late in a call sequence does not
  mean earlier effects did not happen: the PDF had already been written when `Close` failed.
- `draw_pdf_import` is an *import* filter and belongs on `--infilter`; putting it inside
  `--convert-to` (which expects an export filter) made LibreOffice fail writing the ODG.

## 15. Notes for the README (to include when the README is written)

There is no README yet. These are points already decided that it should cover:

- **Lead with scanning** (section 1): find `.pub` files in a folder tree and show which have
  matching PDF, ODG and SLA files. Conversion is the secondary feature.
- **Use the same Microsoft disclaimer wording as the About box**, in the README and in the
  repository description: "AfterPub is an independent project and is not affiliated with,
  endorsed by, or sponsored by Microsoft. Microsoft and Publisher are trademarks of the
  Microsoft group of companies."
- **Why the "PDF engine" group is sometimes grayed out.** It is enabled only when **PDF or
  ODG** is ticked under **Convert to**. ODG is made from a PDF, so a PDF engine is needed for
  it; SLA is saved directly from the `.pub` by Scribus with no PDF step, so with nothing
  ticked, or only SLA, the engine choice has no effect. This is deliberate; there is no UI
  change planned for it, so the README should explain it.
- **Engine quality differs**: Publisher gives faithful PDFs; LibreOffice and Scribus read
  `.pub` through the same library (libmspub) and are best-effort, so their output should be
  checked against the originals (section 3.1).
- **The app never modifies source `.pub` files** and has no "open" action for them (right-click
  opens only PDF, ODG and SLA files), and nothing is replaced in a conversion until a new
  file is complete (sections 2, 3.3 and 13).
