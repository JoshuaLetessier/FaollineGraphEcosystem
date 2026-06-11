# Phase 1 — Data Model: guarded await

Additive to graphcore `BaseNodeData` + `BaseRunner`, and graphstandard `GraphNodeBuilder`. gameflow untouched.

## BaseNodeData — added (graphcore)

| Member | Kind | Description |
|--------|------|-------------|
| `_resumeConditions` | NEW `[SerializeField] List<BaseCondition>` (default `new()`) | The resume-gate; mirrors `_entryConditions`. |
| `ResumeConditions` | NEW `public List<BaseCondition>` (get) | Exposes the list; default empty ⇒ no gate. |

`AwaitSignalName`, `EntryConditions`, `OnEnterActions`, etc. unchanged.

## BaseRunner — changed (graphcore)

```
ResumeIfAwaiting(name):
  if _state != WaitingForSignal: return
  node = CurrentNode
  if node != null AND node.AwaitSignalName == name AND ResumeConditionsPass(node):   // gate added
      ExitAndAdvance()

ResumeConditionsPass(node):                       // NEW private, mirrors EntryConditions loop
  foreach c in node.ResumeConditions:
      if c == null: warn; continue                // null-skip = pass-through
      if !c.Evaluate(_context): return false
  return true
```

`RaiseSignal`/`RaiseSignal<T>` unchanged (still record the signal on the context, then call `ResumeIfAwaiting`).

## GraphNodeBuilder — added (graphstandard)

| Member | Kind | Description |
|--------|------|-------------|
| `ResumeWhen(params BaseCondition[])` | NEW fluent | Appends non-null conditions to `Node.ResumeConditions`; returns `this`. Mirrors `When`. |

## Validation / invariants

- **INV-1**: Parked await node, resume condition false → matching `RaiseSignal` leaves `_state ==
  WaitingForSignal`, no advance (re-arm).
- **INV-2**: Same node, condition true → matching `RaiseSignal` advances (ExitAndAdvance).
- **INV-3**: Multiple resume conditions ⇒ AND (any false → no resume).
- **INV-4**: Null entry in the list ⇒ skipped (warning), not a failed gate.
- **INV-5 (back-compat)**: Empty `ResumeConditions` ⇒ matching signal resumes immediately (current behavior);
  a non-matching name is ignored regardless of conditions.
- **INV-6**: A direct host advance is not gated by resume conditions.
- **INV-7**: `Await(name).ResumeWhen(cond)` (builder) reproduces INV-1/INV-2.
- **INV-8**: graphcore append-only `0.6.0 → 0.7.0`; gameflow untouched; graphstandard `0.6.0 → 0.7.0`; all
  existing suites green.
