# GraphDialogue Roadmap

## Post-MVP Polish & Features

### Localization & Configuration
- **Global translation mode selector** (P1): UI in `LocalizationSettings` to choose between CSV (default, lightweight) or Unity Localization system-wide. All dialogue graphs automatically use the selected provider without per-asset configuration.
  - Enables projects to swap providers at project level (e.g., prototype with CSV → scale to Unity Localization)
  - No per-graph config bloat

### UI & Editor
- **Node color refresh on drag** (P2): Fix UIElements timing issue where node colors disappear when dragging nodes. Likely requires refresh hook on position change.
- **Sample builder CSV auto-load** (P2): After generating sample, auto-load the CSV into the active `CsvLocalizationProvider` so warnings disappear without manual setup.

### Persistence & Playback
- **Playback state save/restore** (P3): Serialize the dialogue history (nodes visited, context state at each step) so users can resume from checkpoints across play sessions.
- **Branching narrative UI** (P3): Higher-level authoring patterns for multi-branch stories (conditionally hide/show entire subtrees, show choice consequences upfront).

### Testing & Validation
- **PlayMode test suite** (P3): Current suite is EditMode-only (headless); add PlayMode tests for UI rendering, input handling, animation timing.

### Documentation
- **API reference** (P2): Auto-generated from xmldoc comments; user-facing guide to `DialoguePlayer`, providers, custom conditions/actions.
- **Authoring patterns guide** (P3): Best practices for large graphs (sub-dialogue reuse, parameter scoping, readable naming).

---

**P1**: Recommended for next immediate iteration (blocks scaling)  
**P2**: Nice-to-have, improves polish  
**P3**: Future enhancement, low priority
