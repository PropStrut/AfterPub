# CLAUDE.md — AfterPub

> DRAFT v0.4. Items marked **[PROPOSED]** were suggested by Claude and not yet confirmed.
> Anything that says "verify" or "to confirm" refers to a detail that must be checked
> against current documentation or real testing before relying on it.
> "Open Questions" (last section) lists what is still undecided.

## 1. Purpose

Microsoft Publisher is end-of-life and its `.pub` format is proprietary. **AfterPub** is a
Windows desktop app that helps people migrate their `.pub` files to open, lasting formats.

- Primary output: high-quality **PDF** (a faithful visual copy).
- Ultimate goal: get documents into an **editable** format. PDF alone is not practically
  editable, so the app also supports an optional editable output stage.
- Audience: the author first, but built to be useful to other people, so it should be
  polished, documented, and forgiving. Different users will edit in different tools.

## 2. Core User Experience

The main window is **one list** (a `DataGridView`). Each row is one source `.pub` file.

- Leftmost columns: **source folder** and **source file name**.
- Columns to the right describe the converted file(s), grouped per output type
  (for example PDF, ODG).
- Columns are **optional**. The user chooses which are visible (right-click the header);
  the choice is saved in settings. Candidate columns:
  - Status per output type (see section 3.3)
  - Output date and size; source date and size
  - Engine used
  - Page count
  - Confidence score (section 5)
  - Missing fonts: count, and names (section 6)
  - Warnings
- Sort by any column. **[PROPOSED]** Filter and group by folder or status.

Actions:

1. **Scan** a folder: current directory only, or recursively through sub-directories.
2. **Convert** the unconverted files (all rows, or a selected subset), with progress and
   per-file success/failure reporting. One bad file must never abort the batch.
3. **Cancel** a running batch (section 3.4).

The UI must stay responsive during scans and conversions: do the work off the UI thread.
Publisher automation is COM-based, so take care with threading (verify).

## 3. Conversion Architecture

Conversion is **pluggable at two points**: the engine that reads `.pub`, and the output
target. Both are selectable by the user.

### 3.1 Engines (`IConversionEngine`)

| Engine | Needs | Notes |
|---|---|---|
| **Publisher automation** | Microsoft Publisher installed | Best fidelity. Detect availability by checking whether the `Publisher.Application` COM ProgID is registered. Use late binding (`Type.GetTypeFromProgID`) so there is no compile-time Office dependency. Export via `Document.ExportAsFixedFormat(pbFixedFormatTypePDF, filename, ...)` — confirmed against Microsoft's Publisher VBA reference; available since Publisher 2007. |
| **LibreOffice** | LibreOffice installed **by the user** | Uses libmspub-based import, run headless: `soffice --headless --convert-to pdf --outdir "<dir>" "<file>"` — confirmed syntax. **Must** launch with its own profile via `-env:UserInstallation=file:///<path>`: without this, a conversion can silently fail or misbehave if the user already has their own LibreOffice open (a real, commonly reported gotcha, not just a defensive precaution). On Windows, launch via `Process` and wait with `Process.WaitForExit()` rather than assuming the call blocks. Fidelity varies per document — see the known risk noted below. |

**LibreOffice is not bundled** (decided). The app only detects an existing install:
first a path set in settings, then standard install locations. Launch it with its own
profile folder using `-env:UserInstallation=...` (confirmed switch, see table above) so
the app does not collide with a LibreOffice window the user already has open. Hand it
many files per launch where possible, since startup is slow.

**Known risk, observed in testing:** on a real Publisher 2010 file, LibreOffice's import
handled a 2-page spread correctly and rendered 7 of 8 pages well, but silently dropped
the entire contents of one text box on the remaining page. The box was plain (not
grouped, not rotated, no unusual fill) but mixed a larger title with bold and underlined
body text — several distinct text-formatting runs in one box. The leading hypothesis is
that libmspub trips on a specific mixed-run-formatting pattern and drops the box's text
rather than one run within it, but the exact trigger is unconfirmed. This is why the
confidence score (section 5) includes a per-page "extracted text looks abnormally short
for this page" check: it is a general safeguard that would have caught this specific
failure without knowing its cause. Worth revisiting with more test files once the app can
batch-process them.

At startup the app detects which engines are available and defaults to the best one
(Publisher if present, otherwise LibreOffice). The user can override this in settings.
If neither engine is found, show a plain message saying at least one is required, naming
the LibreOffice download page in text (the app itself makes no network calls). The engine
settings should show what was detected and where.

**Engine fallback (optional setting).** If the selected engine cannot open a file and the
other engine is available, try the other engine once. Default: **off [PROPOSED]**.
If both fail, mark the row failed and show both reasons. A file converted through
fallback gets a warning ("converted with fallback engine"), which counts against its
confidence score. The engine used is always recorded.

Old files: use the selected engine. If a file will not open (for example a very old
Publisher version that LibreOffice's import cannot read), report that file as failed with
a clear reason and continue with the batch.

### 3.2 Outputs

1. **PDF (always available).** User options for quality and what to preserve, for
   example screen / standard / print-ready, font embedding, image resolution. The UI shows
   only the options the chosen engine actually supports.
2. **Editable companion (optional).** **PDF -> LibreOffice Draw (ODG)** via headless
   import (`soffice --headless --convert-to odg:draw_pdf_import --outdir "<dir>"
   "<file.pdf>"` — filter name still unconfirmed against a real conversion; syntax
   pattern is confirmed). Observed to work better than importing the `.pub` directly.
   Known limits: text becomes separate positioned boxes; master pages and text flow
   are lost. This stage **requires LibreOffice** regardless of which engine produced
   the PDF (a PDF from Publisher or from LibreOffice both feed it the same way).
   **Implemented (v1):** `RowConverter` (Core/Conversion) orchestrates this per row.
   Whether the intermediate PDF is a kept, real output or a deleted scratch file
   follows directly from whether PDF is itself a checked target — no separate
   setting needed. A scratch PDF is named after the source (not a random name) so
   LibreOffice's `--convert-to`, which names its output after its input, produces an
   ODG with the correct base name. **Not yet built:** disabling the ODG checkbox in
   the UI when LibreOffice isn't detected — right now it can be checked regardless,
   and simply fails per-row with a clear message if LibreOffice is unavailable at
   convert time. Other targets (e.g. Scribus's `.sla`) are being added next; the
   output target stays pluggable for that reason.
3. **Extracted assets (later phase).** Text and original images at full resolution.
4. **Structured export (future idea).** With Publisher automation, walk the document
   object model (pages, shapes, text frames, fonts, positions) and write structured data
   that could feed other converters.

Constraints: **no online dependencies, no subscription products, no Microsoft Word as a
target.** The app must work fully offline.

### 3.3 What "converted" means

- Status is tracked **per output type**. A source row counts as **fully converted** only
  when **every enabled output target exists**. Enabling ODG later makes previously
  finished rows partly converted, which is correct.
- Write each output to a **temporary name and rename it on success**, so an existing
  output file always means a complete one. A crash must never leave a partial file that
  looks converted.
- **Out of date:** flag a row when the source changed after conversion. Prefer comparing
  the source's stored size and modified time (from the record file, section 7) over
  comparing file dates, which copying and cloud sync can disturb. Out-of-date rows can be
  ticked for inclusion in the next batch. **[PROPOSED]**
- **Separate output folder:** mirror the source folder structure under the output folder
  so `A/Flyer.pub` and `B/Flyer.pub` never collide.
- **Pre-existing output files** that the app did not create (no record entry): show as
  converted, marked "not created by this app". The overwrite setting decides what happens
  on a re-run.

### 3.4 Existing outputs, cancelling, failures

- **When an output already exists, the default is to ask each time.** Do not prompt in
  the middle of a batch for every file. Before a batch starts, count the existing outputs
  and ask once: overwrite all, skip all, or decide one by one. Other settings values:
  always skip, always overwrite.
  **Implemented (v1):** the batch runs over whatever rows the person has ticked in the
  list. Before running, the app checks which ticked rows already have a PDF output and,
  per the `OverwriteBehavior` setting, either proceeds (Overwrite), silently excludes
  them (Skip), or — for Ask, the default — shows a small custom dialog with three
  choices: Overwrite All, Skip These, Cancel. Built as a custom dialog rather than
  `MessageBox` because WinForms' built-in button sets don't support custom labels.
  **Deferred:** the granular per-file Yes / No / Yes-to-all / No-to-all / Cancel flow
  described above is not built. The same result is reachable today by ticking fewer
  rows and converting in more than one pass; revisit only if that proves annoying in
  practice.
- **Cancel** (mid-batch cancel/abort) is **not yet built** — the current batch loop runs
  to completion once started. Cancel is offered in two forms once it exists: **Cancel**
  finishes the file currently converting, then stops; **Abort now** kills the engine
  process immediately and discards the partial output. Unfinished rows stay "not
  converted". End every batch with a summary of how many finished, failed, or were
  skipped (**implemented**: the summary message box already reports converted, failed,
  and skipped counts).
- **Failures never stop the batch.** Each problem becomes a status with a reason in the
  Warnings column. Based on the author's actual file collection:
  - **Expected, must be handled well:**
    - Very old Publisher versions (Publisher 2010 confirmed present). Publisher itself
      opens these fine; the risk is on the LibreOffice path, whose `.pub` import may be
      less complete for older files. See the known text-loss risk noted in section 3.1.
    - 2-page spreads. LibreOffice imported spreads correctly in testing. The scoring
      logic (section 5) must treat a spread's page-count mapping as expected, not a
      warning sign.
  - **Not expected in the author's files — handle defensively only, no special UI or
    tuning needed:** password-protected files, missing linked images, mail-merge
    documents, locked/read-only files, network paths. Still must not crash or hang the
    batch (a timeout and a clear failure status is enough), but none of these need
    dedicated testing or UI treatment for v1.
  - **Scanning skips what it cannot or should not read.** **Implemented (v1):**
    - *Protected folders.* The scan uses `EnumerationOptions` with `IgnoreInaccessible`
      and skips anything carrying the `System` attribute (for example
      `System Volume Information` and `$RECYCLE.BIN` at a drive root), so scanning a
      whole drive does not abort on a protected folder. Hidden folders are not skipped,
      and reparse points are not skipped, so cloud-sync placeholders (OneDrive and
      similar) stay visible. Skipped folders are currently silent: `Scan` returns only
      the rows. **Not yet built:** reporting skipped folders to the UI or log, which
      would need `Scan` to return the rows plus a list of what was skipped.
    - *SSH public keys.* Files ending in `.pub` can also be SSH public keys, which are
      not Publisher documents and are excluded from the list. This is a content check
      (`SshKeyDetector`), not a folder rule, because keys get copied out of `.ssh`
      folders (backups, downloads, repos). It reads the first 64 bytes: the OLE
      signature (`D0 CF 11 E0 A1 B1 1A E1`, used by Publisher files) means the file is
      never treated as a key; text beginning `ssh-`, `ecdsa-sha2-`, `sk-ssh-`,
      `sk-ecdsa-`, `-----BEGIN `, or `---- BEGIN SSH2 PUBLIC KEY` marks it as a key and
      it is skipped silently. Anything else is **listed**, because hiding a genuine
      document is worse than showing a stray file. A file that cannot be read is also
      listed, so it fails visibly at conversion time.

## 4. Scope by Version

- **v1:** everything in this document not marked v2 or "later".
- **v2:** command-line mode (see section 10), font-install flow and library-wide font
  summary (section 6), visual-comparison scoring (section 5), asset extraction, other
  editable targets.
- **Core must stay free of UI code** so a command-line front end can be added later
  without rework.

## 5. Confidence Score (v1: simple checks)

Each conversion gets a **0-100 confidence score**. It is a triage indicator, not proof of
correctness; there is no ground truth for "how good is this conversion". The score must be
explainable: a detail view or tooltip lists what raised or lowered it.

v1 checks (cheap, objective, offline):

- Output page count matches the source page count (when the engine can report the
  source's). **2-page spreads:** do not treat a spread's page-count mapping as a mismatch;
  confirm the exact mapping (one wide page vs. two pages) once real conversions can be
  inspected, and adjust the check accordingly.
- **Per-page text length looks reasonable** relative to the page's other pages/visual
  content, not just "not entirely blank" — added specifically because testing found a
  page with a fully rendered layout but one silently empty text box (see the known
  LibreOffice risk in section 3.1). A blank page and a page missing one box both fail this
  check the same way.
- Engine warnings and errors.
- Output file is not suspiciously small.
- Fonts missing or substituted (section 6).
- Converted through the fallback engine (section 3.1).
- For the editable stage (PDF -> ODG): text extracted from the ODG compared with text
  extracted from the PDF.

Later (v2): render pages of both outputs to images and compare visual similarity.

Scoring weights should live in one place in code and be covered by unit tests.

## 6. Missing Fonts

Missing-font information is **lost in conversion** (the engine substitutes a font and the
output PDF lists only the substitute), so it must be captured from the *source* at
conversion time and compared against the fonts installed on the machine.

v1:

- Capture the fonts the source used:
  - Publisher automation: read font names from the document's text (exact object-model
    calls, e.g. per-`TextRange`/`Font.Name`, still to confirm once building against a
    real document).
  - LibreOffice route: read font names from the ODG produced from the `.pub`.
- Compare against installed fonts, using `System.Drawing.Text.InstalledFontCollection`
  (confirmed available and current; its `.Families` gives installed `FontFamily` names).
  This is a WinForms-era GDI+ API, which is fine since AfterPub is a WinForms app, but
  confirm it can be called from Core's background logic without UI-thread issues — some
  `System.Drawing.Text` classes have historically had thread-affinity quirks. Match on
  font **family** name; expect edge cases (bold/italic variants, "Narrow" faces, per-user
  vs. system-wide installs, theme fonts, embedded fonts). Test with real files.
- Show two optional columns: **Missing fonts (count)** and **Missing fonts (names)**.
- Feed missing fonts into the confidence score.
- Store the full list of fonts the source used, plus the subset missing at conversion time.

v2 (planned, not v1):

- Notify the user at conversion time and give them a chance to install a font.
- Because the used-fonts list is stored, compare it with fonts installed *now* and suggest
  re-converting files whose missing fonts have since been installed, without re-reading
  the `.pub`.
- A library-wide summary such as "Font X is missing and affects N files".

## 7. Persistence (no database)

**No database.** Most columns are read live from the file system on every scan and are
never stored: whether an output exists, its date and size, and whether it is older than
the source.

Only information that is lost in conversion is recorded, at conversion time:

1. **Per-folder record file** (`.afterpub.json`, one per scanned folder).
   Plain, human-readable JSON. Entries are keyed by path relative to the folder and store
   the source's size and modified time (to detect stale records), plus timestamp, engine
   used, confidence score and breakdown, fonts used, fonts missing at conversion, and
   warnings. Include a schema version field. **[PROPOSED]**
2. **Missing-fonts report** (human-readable courtesy file), named
   `<OutputBaseName>-MISSING-FONTS.txt`, created next to the output file **only when
   fonts are missing**. Plain `.txt`. It is **write-only**: the app generates it and never
   reads it back. It is regenerated on each conversion.
3. **Log file.** A single log, appended to, with a header line for each run and a size cap
   so it cannot grow forever. **Location is a user setting.** Default: the top folder the
   conversion was started from. If that folder is not writable, fall back to a folder next
   to the exe, then the temp folder, and say so in the status bar.

The record file and the missing-fonts report are **user settings** (on/off each).

Graceful degradation:

- If the record file is missing or turned off, nothing breaks. Status, dates, and sizes
  still work; recorded columns (score, fonts, engine, warnings) show "unknown" until the
  file is converted again, or show data only for the current session.
- The scanner must ignore all of the app's own generated files (record, reports, log).

## 8. Settings (user-configurable)

Stored in a plain file next to the executable (portable-friendly):
`afterpub.settings.json`. **Implemented (v1)**, via `AppSettings`/`AppSettingsStore`
in Core, with everything below marked done actually wired into the UI's load/save:

- Conversion engine (auto / Publisher / LibreOffice) and the LibreOffice path — **done**
- Engine fallback on failure (on/off, default off) — not yet built (Auto already
  picks the best available engine; retrying the *other* engine after the *chosen*
  one fails mid-conversion is still open, see section 3.1)
- Output targets to produce (PDF, ODG, ...) — **done**
- PDF quality and preservation options — not yet built
- **Output location:** next to each source file (the author's own default) **or** a
  separate output folder. The list view reads outputs from whichever is configured.
  — **done**
- Scan mode default (current folder / recursive) — **done**
- Overwrite behavior for existing outputs (ask / skip / overwrite; default ask) —
  **done** (batch-level ask/skip/overwrite; granular per-file choice deferred, see
  section 3.4)
- Write per-folder record file (on/off)
- Write missing-fonts report (on/off)
- Log file location
- Visible list columns

## 9. Technology, Layout, and Distribution

- **Language:** C#. **Runtime: .NET 10.** **UI:** Windows Forms with a `DataGridView`.
  There is no WinForms visual designer in VS Code, so the UI is written in code; keep it
  small and well organized.
- **Distribution:** portable app: a **self-contained single-file** Windows build, no
  installer and no registry dependence. An installer may be added later.
- **Offline:** no network access required or used.
- **IDE:** Visual Studio Code with the .NET 10 SDK and the C# extensions.
- Target platform: Windows 10/11, x64 **[PROPOSED]**.

Solution layout:

```
AfterPub/
  CLAUDE.md
  .editorconfig
  src/AfterPub.App/      WinForms UI only; as thin as possible
  src/AfterPub.Core/     all logic: scanning, engines, records, settings, score
  tests/AfterPub.Tests/  xUnit tests referencing Core
```

- All three projects target `net10.0-windows` (Windows-only features such as COM
  automation and font enumeration are used).
- Commands: `dotnet build`, `dotnet test`. Portable build later, roughly:
  `dotnet publish src/AfterPub.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
  (verify).

## 10. Command-Line Mode (v2)

Not in v1. When added, it should be a separate small console executable sharing Core (a
windowed WinForms exe behaves badly in a terminal: prompt returns immediately, exit codes
and piping are unreliable). Expected capabilities: list/scan with optional `--json`,
convert, the same options as the settings with the command line overriding, meaningful
exit codes, and a dry-run. Packaging would then need to ship both exes (a shared-runtime
folder rather than a single file). Decide when v2 starts.

## 11. Code Style (enforce via `.editorconfig`)

- **Allman braces** (the .NET convention): opening and closing braces each on their own
  line.
- **4-space indentation** for C# code (the .NET convention). `.csproj`/`.json`/`.xml`/
  `.yml` files stay at 2 spaces, per their own ecosystem's usual convention.
- **Explicit types**; avoid `var`.
- **Medium comment density:** explain intent and non-obvious decisions; do not narrate
  obvious code.
- Modern C# naming: `PascalCase` for types, methods, properties; `camelCase` for locals
  and parameters; `_camelCase` for private fields; interfaces prefixed `I`.
- Keep it simple. Prefer straightforward code over clever abstractions.

## 12. Testing

- Unit tests are required for non-UI logic: scanning (flat and recursive), per-type
  converted / out-of-date detection, output-path resolution (including mirrored folders),
  settings handling, record-file read/write (including missing or corrupt files),
  font-comparison logic, score calculation, and the overwrite pre-check logic.
- Engines sit behind `IConversionEngine` so tests use fakes. The unit test suite must not
  require Publisher or LibreOffice.
- Test framework: xUnit **[PROPOSED]**, in a separate test project.
- Integration tests with real `.pub` files are welcome but optional (skipped when the
  engine or sample files are unavailable).
- Scanner tests: `SshKeyDetectorTests` cover the key formats, the OLE signature, and
  unreadable files. **Not yet written:** a test that a recursive scan skips a
  `System`-attributed folder (`Scan_Recursive_SkipsSystemFolders`).

## 13. Behavior Rules for Claude When Working in This Repo

- Ask before adding dependencies, and prefer none.
- Never add network calls or telemetry.
- Never modify or delete a user's source `.pub` files. The app is read-only on sources.
- Do not overwrite existing outputs without honoring the user's overwrite setting.
- Handle per-file failures gracefully, report them, and log details.
- Keep UI code out of Core.
- Run the tests before calling a task done.

## 14. Suggested Phases

1. Scaffold solution, `.editorconfig`, test project.
2. Scan and per-type status logic with tests; list view with the optional-column framework.
3. Settings file and record file (read/write), log file.
4. Engine detection and the Publisher automation engine -> PDF, including font capture.
5. LibreOffice engine -> PDF (detection, separate profile folder).
6. Editable output stage (PDF -> ODG). — **done**
7. Batch handling: overwrite pre-check (**done**, batch-level only — see section 3.4),
   cancel/abort (not yet built), temp-name-then-rename (not yet built — PDF/ODG are
   written directly to their final path today), engine fallback (not yet built).
8. Confidence score (simple checks) and missing-fonts report.
9. Settings UI, polish, portable packaging.
10. v2 items (section 4).

**In progress beyond the original phase list:** adding Scribus as a third engine
(parallel to Publisher/LibreOffice, usable for plain PDF too) with `.sla` as an
optional output target. Scribus reads `.pub` via the same libmspub library
LibreOffice uses; real-world testing (section 15) found import quality comparable
to LibreOffice's, including the same known page-specific text-loss risk (section
3.1), plus Scribus additionally imports Publisher's off-page scratch-area content,
which the author considers a genuine improvement, not a bug to fix. Scribus's
headless automation is architecturally different from LibreOffice's simple
`--convert-to` flag — it runs a Python script via `scribus -g -py script.py --
file`, using the documented Scripter API (`openDoc()` / `PDFfile()` / `save()` /
`closeDoc()`) — and headless mode has a known quirk where the GUI can briefly flash
and not every dialog is suppressed, which the existing timeout-and-kill pattern
(already built for LibreOffice) should cover but has not yet been tested against.

## 15. Open Questions

- **Edge-case priorities:** resolved (section 3.4). The author's own files include no
  password protection, no missing linked images, no mail-merge, and no locked/read-only
  files, but do include very old (Publisher 2010) files and 2-page spreads.
- **Sample files:** a set of about 8-12 representative `.pub` files for testing (a simple
  flyer, a multi-page newsletter, a spread, image-heavy, unusual/missing fonts, and more
  old-version files), ideally with the PDF that Publisher itself produces as a reference.
  Keep private files out of the repo.
- ~~**Name check:** confirm "AfterPub" is free on GitHub, domains, and trademark
  databases.~~ **Resolved.** Free on GitHub; USPTO search returned no conflicts; the
  plain `.com` domain is taken and the author is comfortable proceeding without it (a
  different domain or TLD could be checked later if a web presence is ever wanted).
- **Remaining unconfirmed details** (narrower now than a general "verify"):
  - ~~`Application.Visible` property on Publisher automation~~ **Found broken by real
    testing, fixed.** Publisher's `Application` object has no `Visible` property at
    all — it lives on `ActiveWindow` instead. Fixed by removing the early call and
    instead trying `ActiveWindow.Visible = false` *after* `Open`, best-effort.
  - ~~`pbFixedFormatTypePDF` constant~~ **Found wrong by real testing, fixed.** It is
    `2`, not `1` — `1` is actually `pbFixedFormatTypeXPS`, confirmed against
    Microsoft's reference table.
  - ~~`Document.Close` parameters~~ **Found wrong by real testing, fixed.** Unlike
    Word's `Close(SaveChanges, ...)`, Publisher's `Document.Close` takes no
    arguments at all — calling it with one threw a parameter-count COM error. Fixed
    by calling `Close()` with no arguments, in both the normal path and cleanup.
  - **`Open` and `ExportAsFixedFormat` are now confirmed working**: once `Close`'s
    argument count was fixed, both prior calls had already executed successfully
    (the `Close` failure happened after the PDF was already written to disk — worth
    remembering as a general lesson: a failure late in a call sequence doesn't mean
    earlier side effects didn't happen).
  - Whether Publisher ever needs `ActiveWindow` to be re-fetched per document (it is
    currently fetched once right after `Open` and not touched again) — not yet an
    issue, noted in case multi-document batches behave differently.
  - The exact Publisher object-model calls for reading a run's font name from document
    text (the export call itself is confirmed, section 3.1).
  - ~~The LibreOffice filter name for headless PDF -> ODG conversion~~ **Found wrong
    by real testing, fixed.** `odg:draw_pdf_import` failed every time with an
    "impl_store... failed" IO/parameter error. The bug: `draw_pdf_import` is an
    *import* filter (which component opens the PDF), but it was placed in
    `--convert-to`'s filter slot, which expects an *export* filter — no export
    filter by that name exists, so writing the ODG failed. Fixed by using plain
    `--convert-to odg` (ODG is Draw's native export format, no filter needed) and
    moving `draw_pdf_import` to its own `--infilter=draw_pdf_import` argument,
    matching the documented pattern (`soffice --infilter="X_pdf_import"
    --convert-to FORMAT ...`). PDF already opens into Draw by default, so the
    explicit `--infilter` is mostly belt-and-braces.
  - How to suppress Publisher's UI alerts (missing fonts, etc.) during automation, and
    how to detect and kill a stuck engine process on a timeout.
  - Whether `InstalledFontCollection` enumeration needs to run on a UI thread when called
    from Core.
  - The exact page-count mapping LibreOffice uses for a 2-page spread, so the confidence
    score's page-count check can be tuned correctly instead of guessing.
  - Whether every Publisher version the app might meet begins with the OLE signature.
    The SSH key check does not depend on it (unknown content is listed, not hidden),
    but confirming against old real files would let the check be tightened. Publisher
    2010 is the oldest checked so far (verify with `Format-Hex -Count 8`).
    