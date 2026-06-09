# Phase 0 — Research: gameflow driver cross-scene hardening

## R1 — Persistence shape: `DontDestroyOnLoad` in `Awake` + single-driver guard + static `Active`

**Decision**: A serialized `_persistAcrossScenes` (default `false`). In `Awake`, when set: if a
`GraphFlowDriver.Active` already exists and isn't this one, this instance is a duplicate per-scene copy and
**destroys its own GameObject** (the original keeps running the flow); otherwise it sets `Active = this` and
calls `DontDestroyOnLoad(gameObject)`. `OnDestroy` clears `Active` if it owned it (in addition to `Stop`).

**Rationale**: The host bridge that drives scene transitions must outlive them — this is the exact failure the
dogfood hit (a single-mode load destroyed the driver mid-flow). Default OFF because a driver may legitimately
be single-scene and `DontDestroyOnLoad` is surprising as a default. The dedup guard bakes in the consumer's
hand-written `GameFlowBootstrap` (which they needed because they embed a driver in every scene so each scene
is editor-runnable): without it, each scene load would stack another persistent driver.

**Alternatives**: *a packaged `GameFlowBootstrap` component (option B)* — rejected by the user in favor of a
flag (option A). *plain `DontDestroyOnLoad`, no dedup* — rejected: leaves duplicate drivers and an ambiguous
`Active`.

## R2 — Setting the flag before `Awake` in tests (inactive-GameObject pattern)

**Decision**: Because `AddComponent<GraphFlowDriver>()` runs `Awake` immediately (in play mode), a test that
needs `persistAcrossScenes` honored creates the GameObject **inactive**, adds the component, sets
`PersistAcrossScenes`/`BootOnStart`/`Graph`, then `SetActive(true)` — so `Awake` runs with the flag already
set and performs `DontDestroyOnLoad`.

**Rationale**: The persist decision is correctly an `Awake`-time read of the serialized flag (so the inspector
case works). The inactive-GameObject pattern is the standard way to configure serialized fields before the
Unity lifecycle runs.

**Alternatives**: *a runtime "make persistent now" method* — rejected: persistence is an `Awake`-time
property; a post-`Awake` toggle would be a second, confusing path.

## R3 — The real cross-scene test mechanism (committed scenes + edit-time registration)

**Decision**: Commit two minimal **empty** scenes under `Tests/PlayMode/Scenes/`
(`GameFlowCrossSceneA/B.unity`), generated once at **edit time** (a small `-executeMethod` helper using
`EditorSceneManager.NewScene` + `SaveScene`, which is allowed in edit mode). The PlayMode test's
`[OneTimeSetUp]` (`#if UNITY_EDITOR`) **registers** these already-existing scenes into
`EditorBuildSettings.scenes` (and the teardown removes them); the test then loads them with the **real**
`UnitySceneLoader` → `SceneManager.LoadScene(..., Single)`.

**Rationale**: Creating a scene is disallowed during play mode (the slice-1 attempt failed there), but
*registering an already-existing* scene into Build Settings from a `UNITY_EDITOR`-guarded setup is fine, and
keeps the ecosystem's committed Build Settings clean (no permanent test-scene entries). Loading real scenes is
the only way to actually destroy the old scene — which is the destruction the slice-1/2 stub never did, hence
the bug. **Fallback** if play-mode registration proves unreliable: permanently register the two test scenes in
the ecosystem `EditorBuildSettings` (committed) — still works, slightly less clean.

**Alternatives**: *keep using the stub loader* — rejected: that is exactly what masked the bug. *create scenes
in `[OneTimeSetUp]`* — rejected: disallowed in play mode.

## R4 — Waiting-state query derived from runner state (no new runner API)

**Decision**: `IsWaitingForSignal => running && Runner.State == WaitingForSignal`;
`CurrentAwaitSignal => IsWaitingForSignal ? Runner.CurrentNode?.AwaitSignalName ?? "" : ""`. Read-only,
computed from the existing runner state.

**Rationale**: No graphcore change needed — the runner already tracks `State` + `CurrentNode`. This just
surfaces it on the driver so a late-subscribing scene script (one that loaded after `OnWaitingForSignal` fired)
can recover the parked state without reaching into `Runner.CurrentNode` itself (what the consumer had to do).

## R5 — `OnWaitingForTime` is a pure re-expose

**Decision**: Add `event Action<BaseNodeData,float> OnWaitingForTime`; subscribe an internal handler to
`Runner.OnWaitingForTime` in the driver's existing subscribe/unsubscribe pair and re-raise. Mirror the
existing `OnWaitingForSignal` wiring exactly.

**Rationale**: The runner already raises `OnWaitingForTime(node, duration)`; the driver simply forgot to
forward it. Symmetric, zero new semantics.
