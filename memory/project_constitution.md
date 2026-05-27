---
name: project-constitution
description: GraphCore constitution v1.0.0 ratified 2026-05-27 — 6 binding principles for com.faolline.graphcore
metadata:
  type: project
---

GraphCore Constitution v1.0.0 was ratified on 2026-05-27.

Six non-negotiable principles govern all work on `com.faolline.graphcore`:

1. **Foundation Stability** — treat as a public API from day one; strict semver; `BaseNodeData` fields append-only; `INodeExecutor` signatures frozen.
2. **Universal Abstractions Only** — encode only what is true of every graph system; zero knowledge of downstream libs (`dialoguesystem`, `gameflow`, `questsystem`).
3. **Specification-First** — `spec.md` MUST exist and be approved before any implementation begins.
4. **Test-Driven Development** — Red-Green-Refactor mandatory; tests via Coplay MCP `run_tests`; EditMode only.
5. **Simplicity (YAGNI)** — simplest solution always; every deviation justified in `plan.md`.
6. **Cross-lib Compatibility via SubGraph Only** — libs MUST NOT depend on each other; `SubGraphNodeData` holds `BaseGraph`; cycle detection mandatory.

**Why:** This is a shared foundation library — breaking changes cascade to every ecosystem lib simultaneously.

**How to apply:** Every feature suggestion, code review, and planning decision must be checked against these principles. Constitution file is at `.specify/memory/constitution.md`.
