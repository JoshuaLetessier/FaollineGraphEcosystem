# Changelog

All notable changes to **com.faolline.graphsave.savesystem** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.1.0]

### Added
- **`SaveSystemGraphStore`** — an `IGraphSaveStore` (from `com.faolline.graphsave`) backed by a UnitySaveSystem
  backend (`ISaveSystem<GraphRunSnapshot>` from `com.faolline.savesystem.core`). Wrap whichever backend you added
  (e.g. `new SaveSystemGraphStore(new JsonSaveSystem<GraphRunSnapshot>())`), then save/load `GraphRunSnapshot`s
  by slot through your save backends.

### Notes
- Optional bridge package — add only if you use UnitySaveSystem; `graphsave` core stays dependency-free. The
  bridge depends only on the save-system CORE; pick the concrete backend sub-package (json, playerprefs, …)
  yourself. 2 EditMode tests (backend delegation + a real JSON disk round-trip).
