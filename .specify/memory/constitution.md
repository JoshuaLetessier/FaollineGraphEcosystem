<!--
SYNC IMPACT REPORT
==================
Version change: [UNVERSIONED] → 1.0.0
Modified principles: None (initial creation)
Added sections:
  - Core Principles (6 principles)
  - Development Standards
  - Review & Quality Gates
  - Governance
Templates updated:
  - .specify/templates/plan-template.md ✅
  - .specify/templates/spec-template.md ✅
  - .specify/templates/tasks-template.md ✅
Deferred TODOs:
  - RATIFICATION_DATE: set to today (2026-05-27) as this is the initial adoption
-->

# GraphCore Constitution
## `com.faolline.graphcore`

## Core Principles

### I. Foundation Stability (NON-NEGOTIABLE)

GraphCore is the shared foundation of the entire Faolline graph ecosystem. Every lib in
the ecosystem (`dialoguesystem`, `gameflow`, `questsystem`, etc.) depends on it. A breaking
change in graphcore breaks every downstream lib simultaneously.

GraphCore MUST be treated as a public API from day one. Semver MUST be strictly enforced:
- MAJOR: any breaking change to a public API, data contract, or interface signature
- MINOR: new public API, new optional field, new built-in node type
- PATCH: bug fix, documentation, internal refactor with no public API change

No public API MAY be removed without a deprecation cycle of at least one minor version.
`BaseNodeData` fields are append-only — rename, removal, and reordering are prohibited.
`INodeExecutor` signatures are frozen — new methods MUST have a default implementation.

### II. Universal Abstractions Only

GraphCore MUST only encode what is universally true of every graph-based system:
a start, intermediate statements, choices, an end, conditions, actions, and progression.

GraphCore has zero knowledge of any lib that depends on it. It MUST never reference
`dialoguesystem`, `gameflow`, `questsystem`, or any other ecosystem lib — directly
or transitively.

Semantic meaning (speakers, quest objectives, skill costs) belongs exclusively in the
libs above graphcore. If a concept only makes sense in one domain, it does not belong
in graphcore.

### III. Specification-First

Every feature MUST have a written specification (`spec.md`) reviewed and approved before
any implementation begins. Specifications MUST include: user stories with acceptance
scenarios, functional requirements, success criteria, and explicit assumptions.

Implementation without a specification is prohibited. The specification is the contract;
the code is its fulfillment.

### IV. Test-Driven Development (NON-NEGOTIABLE)

Tests MUST be written before implementation. The Red-Green-Refactor cycle is mandatory:
- Write the test → confirm it fails for the right reason → implement → confirm it passes.
- Tests MUST be run via Coplay MCP (`run_tests`) to confirm failure before implementation begins.
- EditMode tests only — `BaseRunner` is headless; PlayMode tests are never required for core.

Shipping code without prior failing tests for new behaviour is a constitution violation.
If implementation is written before tests, the implementation MUST be deleted and rewritten
after the tests are confirmed failing.

### V. Simplicity (YAGNI)

The simplest solution that satisfies the specification MUST be chosen. Abstractions and
design patterns MUST only be introduced when a concrete, present requirement justifies them.

Every deviation from the simplest path MUST be justified in `plan.md` with a concrete
reason and a description of the simpler alternative that was rejected.

If a concept already exists in graphcore, downstream libs MUST use it — never reimplement
or shadow it. If something feels like it belongs in graphcore rather than a lib, move it up.

### VI. Cross-lib Compatibility via SubGraph Only

Ecosystem libs MUST NOT depend on each other. The only mechanism for one lib's graph to
invoke another lib's graph is `SubGraphNodeData`, which holds a `BaseGraph` reference —
never a typed lib-specific reference.

Cycle detection between graphs is mandatory at both edit time (DFS in the editor) and
runtime (execution stack check). A `GraphCycleException` MUST be raised before any
cyclic execution begins.

---

## Development Standards

- **Branching**: Feature branches MUST follow `###-feature-name`. Direct commits to `main`
  are prohibited.
- **Commits**: Each logical unit of work MUST be committed atomically. Commits MUST NOT
  mix unrelated changes.
- **Naming**: Classes `PascalCase`, interfaces `IPascalCase`, private fields `_camelCase`,
  events `OnPascalCase`. All `NodeType` identifiers MUST be `const string` — no magic strings.
- **Error prefix**: All `Debug.LogError` calls MUST use the `[GraphCore]` prefix.
- **Documentation**: All public APIs MUST have XML `<summary>` documentation.
  Non-obvious design decisions MUST be documented in `CLAUDE.md`.
- **Dependencies**: New dependencies MUST be justified in the specification. `com.unity.graphview`
  is allowed (Editor only). `com.unity.localization` is forbidden — graphcore has no text.
  No ecosystem libs. No `MonoBehaviour` in Runtime core. No `UnityEvent` — C# `Action<T>` only.
- **Structure**: One class per file. No `#region`. No inline CSS in node views — USS only.
  `partial` classes are permitted for `BaseGraphView` only.

---

## Review & Quality Gates

- **Pre-implementation gate**: `spec.md` MUST exist and be approved before `plan.md` is
  created. `plan.md` MUST pass the Constitution Check before task generation.
- **Pre-merge gate (Coplay MCP sequence)**:
  1. `validate_script` — all modified files, zero errors
  2. `unity_reflect` — all GraphView/Unity APIs verified before use
  3. `manage_packages` — all asmdef references resolve
  4. `run_tests` — full EditMode suite, all green
  5. `read_console` — zero errors; warnings justified with inline comments
- **Post-delivery gate**: Each user story MUST be validated against its acceptance scenarios
  before the next story begins.
- **Semver gate**: Every PR MUST include a semver assessment. PRs that modify a public API
  without a semver rationale MUST be rejected.
- **Constitution compliance**: All PRs MUST verify adherence to these principles.
  Non-compliant changes MUST be flagged before merge.

---

## Governance

This constitution supersedes all other project practices for `com.faolline.graphcore`.
Conflicts MUST be resolved in favour of the constitution unless an amendment is ratified.

**Amendment procedure**:
1. Propose the amendment with rationale in a pull request against this file.
2. Amendment MUST be approved by the project lead.
3. A migration plan MUST accompany any amendment that affects downstream libs.
4. Version MUST be incremented:
   - MAJOR: backward-incompatible governance change or principle removal/redefinition.
   - MINOR: new principle or materially expanded guidance.
   - PATCH: clarifications, wording fixes, or non-semantic refinements.

**Compliance review**: Constitution compliance MUST be verified at each PR review and at
the start of each new feature's planning phase (Constitution Check in `plan.md`).

**Version**: 1.0.0 | **Ratified**: 2026-05-27 | **Last Amended**: 2026-05-27
