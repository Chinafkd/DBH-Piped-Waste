# DBH: Piped Sewage Processing publishing guide

## Final artifacts

- Runtime release ZIP: `Release/DBHPipedWaste-0.1.5.zip`
- Source release ZIP: `GitHub/DBH-Piped-Waste-Source-0.1.5.zip`
- Workshop upload directory: `Release/WorkshopUpload/DBHPipedWaste`
- Package ID: `Chinafkd.DBHPipedWaste`
- Mod version: `0.1.5`

The runtime ZIP is a RimWorld Mod package. Extract it so the resulting
`DBHPipedWaste` directory directly contains `About`, `1.6` and
`LoadFolders.xml`, then copy that directory into the game's `Mods` folder.

The source ZIP contains the same runtime files plus `Source`, `Docs`, build
scripts and project documentation. It intentionally excludes the repository's
generated `Release`, `GitHub` and `Demo` directories.

## Build and package

From the repository root:

```powershell
& '.\DBH Piped Waste\build_release.ps1'
```

The script rebuilds the main assembly, refreshes the clean Demo copy, validates
the required runtime files, and recreates the two ZIP archives and the
WorkshopUpload directory.

## Steam Workshop

1. Open RimWorld's Mod menu while signed in to the intended Steam account.
2. Use `Release/WorkshopUpload/DBHPipedWaste` as the upload source.
3. Confirm that `About/About.xml`, `LoadFolders.xml` and
   `1.6/Assemblies/DBHPipedWaste.dll` are directly under the Mod root, and that
   `About/Preview.png` shows the red hand-drawn before/after arrow image.
4. Declare **Harmony** and **Dubs Bad Hygiene** as required dependencies.
5. **DBH for Medieval** and **Medieval Overhaul** are optional integrations.
6. Complete the final Workshop upload confirmation as the author.

Suggested title: `DBH: Piped Sewage Processing`

Suggested short description:

> Adds a piped composter, piped biofuel refinery and underground sewage pit to
> Dubs Bad Hygiene. Production buildings draw directly from the fullest pit,
> with overflow protection, emergency extraction and optional Medieval Overhaul
> integration.

The release includes the project-owned `About/Preview.png` at 1024 x 352. It is
generated from the two in-game screenshots and is ready to use as the Workshop
preview image.

For the first upload, `About/PublishedFileId.txt` must be absent. RimWorld and
Steam will create the real Workshop item and write its ID after a successful
publish. Preserve that generated file for later updates so a new duplicate item
is not created.
