# Changelog

All notable changes to **com.faolline.graphlogging** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.2.0]

### Changed
- **BREAKING: `GraphLoggingSettings` now toggles Info/Warning per GROUP (the prefix before a category's
  first `.`) instead of per category.** Closes a real gap in the previous flat per-category model: a lib
  silenced today (e.g. `"GraphSave"` toggled off) stayed silenced only for categories that had already
  logged at least once — any new `GraphSave.*` category added later reverted to the default-enabled state
  the first time it logged, forcing the user to keep revisiting the settings asset as the lib grew new
  call sites. A group now carries its own persistent `DefaultInfoEnabled`/`DefaultWarningEnabled`, and any
  category discovered later under that group inherits it automatically. `GraphLoggingSettings.Categories`
  / `CategoryEntry` are removed; replaced by `Groups` (`GroupEntry`: `Prefix`, `DefaultInfoEnabled`,
  `DefaultWarningEnabled`, `KnownCategories`) and `Overrides` (`CategoryOverride`, only present for a
  category that diverges from its group's default — self-pruned once it stops diverging). `Logging.Info/
  Warning/Error` and `IsInfoEnabled`/`IsWarningEnabled` keep the exact same call-site signature; only the
  storage/editor model changed. Existing `GraphLoggingSettings.asset` files serialize their old
  per-category data under a field name Unity no longer reads — categories rediscover their group (both
  levels enabled) the next time they log, matching this package's existing "unknown = log everything"
  default rather than silently carrying forward a stale toggle.
- `GraphLoggingSettingsEditor` inspector reworked to match: each group is a foldout with its own
  Info/Warning default toggle pair; a category row inside only needs a toggle when it overrides its
  group, and disappears back into "inherits the group" the moment it's set to match again.

## [0.1.1]

### Added
- **Optional `UnityEngine.Object context` parameter on `Info`/`Warning`/`Error`** (default `null`),
  mirroring `Debug.Log(message, context)` — pass `this` from a `MonoBehaviour` to keep click-to-ping
  working in the console. Needed once real call sites (e.g. `DialogueDriver`) that previously passed a
  context object to `Debug.LogWarning` were migrated to this facade.

### Fixed
- **`Error` no longer recurses into itself.** An ecosystem-wide mechanical migration of every remaining
  `Debug.Log`/`LogWarning`/`LogError` call site (10 more packages) to this facade accidentally rewrote
  `Logging.Error`'s own internal `Debug.LogError(message)` call into a call to `Logging.Error` itself —
  a guaranteed stack overflow on the very first `Error` call anywhere in the ecosystem. Caught by reading
  the file while investigating an unrelated compile error, before it was ever run.

## [0.1.0]

### Added
- **Initial release.** `Logging.Info/Warning/Error(category, message)` — a shared, category-based
  logging facade any package in the ecosystem can adopt instead of calling `Debug.Log`/`LogWarning`
  directly. `GraphLoggingSettings` (project-wide asset, loaded via Resources) lets the user toggle
  Info/Warning per category from `Faolline ▸ Diagnostics ▸ Log Settings`; a category appears in the
  inspector the first time it logs, no upfront registry needed. `Error` is never gated — always
  visible, matching this ecosystem's "never hide a real problem" precedent. Absence of the settings
  asset, or of a given category within it, defaults to "log everything" — adopting this facade never
  silently loses a message that used to show.
- Zero package dependencies by design: a T0 foundation utility any other T0/T1/T2 package can depend
  on directly, without pulling in `graphcore` — see `graphlocalization`, which is itself a zero-dependency
  T0 package and could not otherwise share a logging facility with `graphcore` without breaking that
  invariant.
