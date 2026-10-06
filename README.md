# AfterPub

**Find your Microsoft Publisher (`.pub`) files and see which ones still need a PDF or an editable copy.**

AfterPub searches a folder (and its subfolders) for `.pub` files and shows, for each one, whether
a matching **PDF**, **ODG** (LibreOffice Draw) and **SLA** (Scribus) file already exists, so you can
see at a glance what is still missing. It can then convert the missing ones for you.

Microsoft Publisher has been retired. Microsoft 365 subscribers lost access on October 1, 2026, and
copies from a perpetual license still run but are no longer supported. If you have years of `.pub`
files, AfterPub helps you get them out before the program is gone.

> **Status:** Windows 10 and 11 (x64). Linux and macOS are planned for version 2.

<!-- Add a screenshot of the main window here. -->

## What it does

- **Scans first.** Pick a folder, optionally include subfolders, and press **Scan**. Every `.pub`
  file is listed with the date and size of its PDF, ODG and SLA files, or a blank cell where one is
  missing. Scanning needs no other software at all.
- **Sorts and filters.** Click a column header to sort. Show only files that are missing a PDF, ODG
  or SLA, or whose output is **out of date** (the `.pub` was changed after the output was made,
  marked with a warning sign), or search by part of the path.
- **Converts what is missing.** Tick the files you want, choose which formats to convert to, and
  press **Convert selected**. **Select not converted** ticks every file that is missing one of the
  formats you chose.
- **Opens results.** Right-click a PDF, ODG or SLA cell to open the file or show it in its folder.

## Your files stay safe

- **Your `.pub` files are never modified.** The app only reads them, and it has no "open" action for them.
- **Nothing is replaced until the new file is complete.** Each conversion is written to a temporary
  folder and moved into place only on success, so a failed or stopped conversion never leaves a
  half-written file or damages an existing one.
- **It asks before overwriting.** If a selected file already has an output in a format you chose,
  you are asked once, up front: overwrite, skip, or cancel. The prompt starts on **Ask** every time
  the app opens, and pressing Enter chooses the safe option (skip).
- **It stays offline.** No network access, no telemetry.
- **You can stop at any time.** **Cancel** finishes the current file and stops; **Abort now** stops
  the conversion program immediately.

## Conversion engines

AfterPub does not convert files itself. It drives programs you already have installed.

| Engine | Quality | Notes |
|---|---|---|
| **Microsoft Publisher** | Best: faithful PDFs | Only for people who still have it installed |
| **LibreOffice** | Best-effort | Free, [libreoffice.org](https://www.libreoffice.org). Not bundled with AfterPub |
| **Scribus** | Best-effort | Free, [scribus.net](https://www.scribus.net). Not bundled with AfterPub |

By default the app chooses **Auto**: Publisher if it is installed, then LibreOffice, then Scribus.
You can pick one yourself in **Conversion Settings**.

**Please check the results.** LibreOffice and Scribus read `.pub` files through the same open-source
library, and it is not perfect. In testing it silently dropped text from some pages, mis-sized some
image borders, and once produced a very large PDF. PDFs made by these two engines are labelled with
the engine's name in the grid, and a warning after each batch reminds you to compare them with your
originals. Publisher itself preserves everything.

### Formats

- **PDF**: a faithful visual copy. Made by whichever engine you chose.
- **ODG**: an editable LibreOffice Draw file. It is made from a PDF, so it needs a PDF engine and
  LibreOffice. Text becomes separate boxes, and master pages and text flow are lost.
- **SLA**: an editable Scribus file, saved directly from the `.pub` by Scribus. It needs Scribus and
  does not use a PDF.

### Why is the "PDF engine" choice sometimes grayed out?

It is available only when **PDF** or **ODG** is ticked under **Convert to**. ODG is made from a PDF,
so a PDF engine is needed for it. SLA is saved straight from the `.pub` by Scribus with no PDF step,
so with nothing ticked, or with only SLA ticked, the engine choice has no effect. This is deliberate.

## Using it

1. Press **Browse...** (or type a path) and choose the folder that holds your `.pub` files.
2. Tick **Include subfolders** if you want them searched too, then press **Scan**.
3. Look over the list. Filter it if you like.
4. Open **Conversion Settings** (collapsed by default) to choose what to convert to, where outputs
   go (next to each source file, or in a separate folder that mirrors the source folders), and which engine to use.
   While the area is closed, a summary on its bar shows your main choices.
5. Tick files (or press **Select not converted**) and press **Convert selected**.

**Keys:** **F1** opens About. **F2** switches between light and dark mode (it offers to restart the app).

Settings are saved in `afterpub.settings.json` next to the program.

## Install

<!-- Fill in after the portable build is published: where to download it, and that it is a single
     self-contained .exe that needs no installer and no .NET installation. -->

AfterPub is a portable Windows program. There is no installer.

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
```

The solution has three projects: `src/AfterPub.App` (the Windows Forms window), `src/AfterPub.Core`
(all the logic) and `tests/AfterPub.Tests` (xUnit tests). The tests do not need Publisher, LibreOffice
or Scribus. [CLAUDE.md](CLAUDE.md) describes the design in detail.

## Planned for version 2

Command-line mode; Linux and macOS support; a confidence score that flags conversions that may have
lost content; a missing-fonts check; a per-folder record of conversions; more optional columns; and
PDF quality options.

## License

Licensed under the [Apache License, Version 2.0](LICENSE). Copyright Brian Reed.

## Trademarks and affiliation

AfterPub is an independent project and is not affiliated with, endorsed by, or sponsored by
Microsoft. Microsoft and Publisher are trademarks of the Microsoft group of companies.
