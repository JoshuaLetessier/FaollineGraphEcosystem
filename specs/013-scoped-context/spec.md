# Feature Specification: Global & Local Execution Contexts

**Feature Branch**: `013-scoped-context`

**Created**: 2026-06-04

**Status**: Draft

**Input**: User description: "GraphCore execution gets two value contexts: a persistent **global** context that lives for the whole run, and a transient **local** context that exists only while a sub-graph flagged 'opens a scope' runs. Reads resolve local-first then fall through to global; writes are routed to the variable's declared home (global vs local), so a write to a global from inside a scoped sub-graph persists while temporary local writes are discarded when the sub-graph ends. A new append-only flag on the sub-graph node selects this behaviour, alongside the existing inherit / fresh-context behaviours. Scopes are not nested — scope-opening sub-graphs run sequentially, never one inside another."

## User Scenarios & Testing *(mandatory)*

GraphCore is a developer-facing foundation library. Its "users" are the **graph authors** who
assemble graphs in the editor and the **downstream lib developers** (gameflow, questsystem, …)
who build on top of it. The value here is a clean split between values that must **persist for the
whole run** (global) and **temporary working values** that should vanish when a self-contained
sub-graph (e.g. a "scene") finishes — without that sub-graph being cut off from the global values
it needs to read and update.

### User Story 1 - A scoped sub-graph keeps its temporary values local (Priority: P1)

A graph author has a reusable sub-graph (for example, a temporary "scene") that sets and mutates
several working variables while it runs. Those working variables are scratch state — they must not
survive the sub-graph. The author marks the sub-graph node as **opening a scope**. When execution
enters that sub-graph a fresh local context is created; every value the sub-graph writes that is
*not* a declared global lands in that local context; when the sub-graph ends the local context is
discarded and the run continues with no trace of those temporaries.

**Why this priority**: This is the core promise and the smallest slice that delivers value on its
own. Without "temporaries vanish on exit" there is no reason to introduce a local context. The
gameflow "scene" concept depends on exactly this.

**Independent Test**: Author a parent flow, invoke a scope-opening sub-graph that creates a couple of
local working variables, let it run to its end, and confirm those variables are gone afterwards.
Fully testable headlessly with the runner and no editor UI.

**Acceptance Scenarios**:

1. **Given** a scope-opening sub-graph that sets a local working variable `T = 5`, **When** the sub-graph is active, **Then** a read of `T` returns `5`.
2. **Given** that same sub-graph has ended, **When** the run reads `T`, **Then** it reports `T` as not present (the local context was discarded).
3. **Given** two scope-opening sub-graphs that run one after another and both use a local variable `T`, **When** the second runs, **Then** it starts with no value for `T` from the first (each gets a fresh local context).

---

### User Story 2 - A scoped sub-graph reads and durably updates global values (Priority: P1)

The same scoped sub-graph needs to *read* persistent global values (player gold, story flags) and to
*durably update* some of them — a "scene" can both keep scratch state and record that a boss was
defeated. The author does not want to copy values in and out, nor lose a global write just because it
happened inside a scope. While the sub-graph runs, any value it does not hold locally is resolved by
falling through to the global context; and a write to a variable **declared global** is routed to the
global context, so it survives the sub-graph ending.

**Why this priority**: Local isolation (US1) is only useful if the scoped sub-graph can still see and
update the global state around it. This is the half of the feature that makes a local context a
*nested* context rather than a disconnected blank one, and it is what resolves the central design
question (how a write reaches a global from inside a scope). It is P1 alongside US1 because gameflow
needs both halves for its first real flow.

**Independent Test**: Author a flow with a global `Gold = 7` and a global `BossDefeated = false`,
invoke a scope-opening sub-graph that reads `Gold`, sets a local temporary, and sets
`BossDefeated = true`; after the sub-graph ends, confirm the temporary is gone, `BossDefeated` is
`true`, and `Gold` is still readable.

**Acceptance Scenarios**:

1. **Given** a global `Gold = 7` and a scope-opening sub-graph that does not define `Gold`, **When** the sub-graph reads `Gold`, **Then** it returns `7` (resolved from global by fall-through).
2. **Given** the sub-graph writes to the global-declared `BossDefeated = true`, **When** the sub-graph ends, **Then** the run reads `BossDefeated` as `true` (the write was routed to the global context, not the discarded local one).
3. **Given** the sub-graph also wrote an undeclared scratch variable `Tmp`, **When** the sub-graph ends, **Then** `Tmp` is gone (an undeclared key defaults to the local context).

---

### User Story 3 - Existing graphs behave exactly as before (Priority: P1)

Existing graphs use one of two sub-graph context behaviours today: **inherit** the caller's context,
or run with a **fresh blank** context. Every author with an existing graph expects it to keep working
byte-for-byte after this feature lands — the scoped (global+local) behaviour must be a third, opt-in
option, never a change to the default. Graphs and saved assets authored before this feature must load
and run identically.

**Why this priority**: This is a foundation library governed by a non-negotiable Foundation Stability
principle. A regression here breaks every downstream lib simultaneously, so back-compat is as critical
(P1) as the headline feature itself.

**Independent Test**: Run the existing graphcore execution test suite unchanged and confirm it stays
green; load a graph asset authored before this feature and confirm its sub-graph nodes still default
to their original inherit/fresh behaviour with no scope opened.

**Acceptance Scenarios**:

1. **Given** a sub-graph node that inherits the parent context (existing behaviour), **When** it runs, **Then** no local context is opened and writes affect the inherited context exactly as before.
2. **Given** a sub-graph node configured for a fresh blank context (existing behaviour), **When** it runs, **Then** it starts with an empty context exactly as before.
3. **Given** a graph asset serialized before this feature existed, **When** it is loaded, **Then** every sub-graph node retains its prior behaviour and none is silently switched to "scoped".

---

### Edge Cases

- **Reading a key that exists nowhere**: A read for a key absent from both the local and the global context MUST behave exactly like a missing key does today (the existing "try-get returns false / typed-get reports not found" contract is unchanged).
- **Same key declared global but also written locally**: When the local context holds a key that also exists globally, the local value wins for reads while the scope is open; discarding the local context re-exposes the global value. (Authors are expected not to reuse the same name for both a global and a scratch value; doing so is an author-side mistake, not a runtime error.)
- **Undeclared key**: A write to a key with no declared home defaults to the **local** context — the safe default, so an unplanned write cannot accidentally pollute persistent global state.
- **Scope-opening sub-graph that ends immediately**: A local context opened for a sub-graph that reaches its end with no writes MUST still be opened and discarded cleanly, leaving global state untouched.
- **Step-back / checkpoint across a scope boundary**: When execution steps back (undo / go-to-checkpoint) across the point where the local context was opened or discarded, the restored state MUST reflect both contexts as they were at that moment — a discarded local value must not reappear, and a since-opened local context must not linger.
- **Change notifications**: A value change in either context MUST continue to fire existing change-notification subscribers; observers MUST NOT be silently dropped because a write targeted the local context.
- **A scope-opening sub-graph invoked while a local context is already active** (out of supported v1 usage — scenes are sequential, never nested): the defined v1 behaviour is that the active local context is discarded and replaced by a fresh one; there is no second nested level. See FR-011.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The runtime MUST provide two value contexts: a persistent **global context** that lives for the entire run, and a transient **local context** that exists only while a scope-opening sub-graph runs.
- **FR-002**: The runner MUST create a fresh local context when it enters a sub-graph flagged to open a scope, and MUST discard that local context when that sub-graph ends, in lockstep with entering/leaving that sub-graph.
- **FR-003**: A **read** for a key MUST resolve from the local context first (when one is active) and then fall through to the global context; the first match wins.
- **FR-004**: A **write** MUST be routed to the variable's **declared home** — the global context for variables declared global, the local context otherwise. A key with **no declaration MUST default to the local context**.
- **FR-005**: Discarding the local context MUST remove only the values held in it; values in the global context MUST be untouched.
- **FR-006**: A write to a **global-declared** variable performed from inside a scope-opening sub-graph MUST **persist after that sub-graph ends** (it lands in the global context, never in the discarded local context). *This is the resolution of the previously-open "how does a scoped write reach a global" question: routing by declaration, not by key-prefix convention.*
- **FR-007**: The sub-graph node MUST carry a new, **append-only** flag that selects the scope-opening behaviour. Adding it MUST NOT rename, remove, reorder, or change the meaning of any existing field, and MUST default to "off" so deserialized pre-existing nodes keep their prior behaviour.
- **FR-008**: The existing sub-graph context behaviours — **inherit the parent context** and **fresh blank context** — MUST remain available and unchanged; "scoped" (global+local) MUST be an additional, mutually distinct third behaviour, never a redefinition of the existing two.
- **FR-009**: Existing per-key **change-notification** behaviour MUST continue to function for writes to either context; the observable notification behaviour MUST NOT regress relative to today.
- **FR-010**: **Step-back, checkpoint restore, and history snapshots** MUST capture and restore both contexts so that restoring a prior point reproduces the exact state (which local context was active, if any, and the values in both) that existed at that point.
- **FR-011**: Scope-opening sub-graphs are assumed **sequential, never nested** (no scope-opening sub-graph runs inside another). Entering a scope-opening sub-graph while a local context is already active is **outside supported v1 usage**; the defined behaviour is that the active local context is discarded and replaced by a fresh one (no second nested level). Generalising to nested local contexts is reserved as a future, backward-compatible (semver MINOR) enhancement.
- **FR-012**: All additions MUST be **backward compatible at the public-API and data-contract level**, qualifying as a semver MINOR change (new optional capability, no breaking change), consistent with the Foundation Stability principle.
- **FR-013**: The mechanism MUST remain **domain-agnostic**: it MUST encode only the universal notions of global-vs-local value lifetime and visibility, with no knowledge of any downstream concept (scene, encounter, quest, dialogue, …). Naming a local context's *usage* (e.g. "scene") belongs to downstream libs.

### Key Entities

- **Global context**: The single, persistent set of values that exists for the entire run. Holds variables declared global; survives the opening and discarding of every local context.
- **Local context**: A transient set of values created when a scope-opening sub-graph is entered and discarded when it ends. Holds the sub-graph's scratch/temporary values (and any undeclared keys), shadows global values for reads while active, and is removed (with its values) on exit.
- **Variable home (declaration)**: The property of a declared variable that fixes where its writes land — global or local. Determines write routing (FR-004); an undeclared key defaults to local.
- **Scope-opening sub-graph node**: A sub-graph invocation marked (via the new append-only flag) to open a local context on entry and discard it on exit. Distinct from the existing inherit-context and fresh-context sub-graph behaviours.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a scope-opening sub-graph ends, **100%** of its local/temporary values are gone from the run's view — verified by automated scenarios for US1.
- **SC-002**: A scoped sub-graph correctly resolves **every** global value it does not hold locally — fall-through read correctness is **100%** across the US2 scenarios.
- **SC-003**: A write to a global-declared variable from inside a scope-opening sub-graph **persists in 100%** of cases after the sub-graph ends — verified by the US2 durable-global-write scenarios.
- **SC-004**: The pre-existing graphcore execution test suite passes **with zero regressions**, and every pre-existing sub-graph behaviour (inherit / fresh) is **byte-for-byte unchanged** — no existing test is modified to accommodate the feature.
- **SC-005**: Stepping back across a scope boundary reproduces the prior state with **100%** fidelity (no discarded local value reappears; no opened local context lingers) — verified by the history/step-back edge-case scenarios.
- **SC-006**: The change is classifiable as a **semver MINOR** with no breaking public-API or data-contract change — confirmed by the per-PR semver assessment gate.

## Assumptions

- **No nested scopes (confirmed)**: Scope-opening sub-graphs run sequentially, never one inside another. This is the deciding simplification that lets the model be two flat contexts (global + local) instead of a scope stack with arbitrary depth.
- **Write routing by declaration**: A variable's global-vs-local home is fixed by its declaration; writes go to that home, and an undeclared key defaults to the local context. This replaces the earlier candidate "reserved key prefix" convention and keeps generic actions fully generic (they only ever call set/get with a key — the context routes). **Operationally, "declared global" means the key is present in the global context** — i.e. it was declared as a host/root-graph parameter (or already lives in global); there is no per-parameter global/local flag on the variable declaration itself.
- **Audience**: The direct beneficiaries are graph authors and downstream-lib developers; there is no end-user UI surface introduced beyond the new opt-in flag on the sub-graph node.
- **Supported value types** are unchanged (the existing bool / int / float / string set); contexts change *where and how long* a value lives, not the set of storable types.
- **Default behaviour is unchanged**: a sub-graph that is not flagged to open a scope behaves exactly as it does today; the new flag defaults to off.
- **Save/restore serialises the global context only**: the read-only parameter snapshot used for persistence exposes the **global** context exclusively; transient local-context (scratch) values are **never** written to a save, even if a save/checkpoint occurs while a scope is active. Persistence is handled by the existing save lib (`com.faolline.savesystem.core`), which serialises that global key→value set. (In-memory step-back/history, by contrast, *does* capture the local overlay — see FR-010 — because it must restore the exact runtime state.)
- **No new external dependencies** are introduced; the feature lives within the existing graphcore runtime (context + runner) and the existing sub-graph node data, consistent with the constitution's no-hard-external-coupling rule.
