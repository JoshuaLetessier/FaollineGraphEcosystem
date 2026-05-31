# Quickstart: graphdialoguesystem (MVP)

**Feature**: `010-graphdialoguesystem-mvp` | **Date**: 2026-05-31

Manual validation walkthrough once the package is implemented. Mirrors the starterGraph flow, adapted to
the dialogue domain. All steps are also covered by the automated EditMode suite.

## A. Author a dialogue (US1)

1. `Assets > Create > GraphDialogue > Dialogue Graph` → name it `SampleDialogue`.
2. Double-click it: the **Dialogue Graph Editor** window opens (titled by asset name).
3. Right-click the canvas → add **Start**, two **Line**, a **Choice**, and two **End** nodes.
4. Select the first Line node → inspector → set **Speaker Key** (e.g. `npc_mayor`), **Text Key**
   (e.g. `dlg.intro`), leave Expression `neutral`.
5. Select the Choice node → **Add Choice** twice → label them (Display Text Keys `dlg.opt.yes`,
   `dlg.opt.no`); optionally attach a condition to one option.
6. Drag edges: Start → Line1 → Choice; Choice option "yes" → Line2 → End(Completed); option "no" →
   End(Aborted).
7. **Save** (Ctrl+S). Close and reopen the window → confirm every node, field, option, order, color,
   and connection is identical (FR-008 / SC-002).
8. Open a second dialogue asset → confirm the first window is untouched (FR-009).

## B. Provide translations (US4)

1. Create a CSV table asset with header `Key,en,fr` and rows for `dlg.intro`, `dlg.opt.yes`,
   `dlg.opt.no`, and the speaker name key (e.g. `speaker.mayor.name`).
2. In the dialogue settings, leave the **default CSV provider** selected and set locale `en`.
3. (Optional) If `com.unity.localization` is installed, switch the provider to the **Unity adapter**
   pointing at a String Table collection with the same keys.

## C. Play it back (US2/US3)

1. Click **Run** in the toolbar. The console logs the first line: speaker display name + resolved text
   in the active locale.
2. Click **▶ Continue** to advance through lines.
3. At the choice, click **Choose** → pick an available option. A condition-gated option that fails its
   condition is listed but not selectable (FR-015 / US3).
4. Continue to an End node → console logs `Graph ended: <EndReason>` once (FR-017).
5. Click **← GoBack** → the runner restores the previous node and the prior context values (FR-026).
6. Switch locale to `fr` and Run again → the same graph reports French text, no graph edits (SC-004).

## D. Sub-dialogue + cycle safety (US2)

1. Add a **SubGraph** node; set its **Target Graph** to a second dialogue asset.
2. Run: on entering the sub-dialogue node, the child plays; on its End, the parent resumes (FR-019).
3. Try to set a sub-graph target that points back to an ancestor → the editor refuses it with a cycle
   warning (FR-020); at runtime such a cycle raises `GraphCycleException`.

## E. Headless check (US2, automated)

The EditMode suite builds an in-memory `DialogueGraph` + `DialoguePlayer` with a CSV provider and:

- asserts the first `LineStep` speaker/text per locale;
- advances, asserts the `ChoiceStep` options + availability;
- chooses, advances to end, asserts a single `EndStep` with the right `EndReason`;
- flips a value via an effect and re-checks option availability;
- steps back and asserts restored context;
- resolves the same graph through both providers.

## Done when

- All EditMode tests green (SC-009).
- Sample dialogue plays start→end in two locales (SC-003/SC-004).
- `git diff` shows zero changes under `com.faolline.graphcore/` (SC-008).
