# GraphDialogue MVP — Quickstart Validation (T070)

After generating the sample via **Faolline ▸ GraphDialogue ▸ Generate Sample Dialogue**, verify the MVP manually in the editor.

---

## 1. Sample Created

Confirm in the project view:
- `Assets/GraphDialogueSamples/SampleDialogue.asset` (parent)
- `Assets/GraphDialogueSamples/SampleSubDialogue.asset` (child)
- `Assets/GraphDialogueSamples/SampleSpeaker_Mayor.asset`
- `Assets/GraphDialogueSamples/SampleDialogue_Strings.csv`

Double-click `SampleDialogue.asset` to open it in the **Dialogue Graph Editor**.

---

## 2. Graph Structure (Authoring — US1)

Verify the canvas shows:
- ✅ **Start node** (green)
- ✅ **Intro line node** (blue) — SpeakerKey=`npc_mayor`, TextKey=`dlg.intro`, marked **Checkpoint**
- ✅ **Choice node** (amber) — two options:
  - `dlg.opt.ask` → Sub-Dialogue
  - `dlg.opt.leave` → End
- ✅ **Sub-Dialogue node** (purple) — target: `SampleSubDialogue`
- ✅ **End node** (red)

All edges connected; entry point is the Start node.

---

## 3. Playback (US2)

**Press Run** (set locale field to `en` first).

The window emits:
1. **Line**: "Welcome traveller." (Mayor)
2. **Pauses** → Choose button enabled; options shown: "Ask about the town", "Leave"

Click **Continue**:
3. **Choice node** reached; both options available.

Click **Choose** → select "Ask about the town":
4. **Sub-dialogue enters**: "It is a quiet place." (Mayor)
5. **Continue**:
6. **End**, console logs `[GraphDialogue] Dialogue ended (Completed)`

---

## 4. Inline Conditions (US3)

**Go Back** from the end to the Choice.

Inspect the choice node in the inspector panel. Verify:
- ✅ Option labels editable
- ✅ Each option can have a **Condition** field (test hook: a `BoolCondition` on option "ask" is optional in the sample; without it, both options are always available)

The sample does **not** gate the first option, so both remain available. The entry condition on the intro line is also unchecked (always pass).

---

## 5. Inline Effects (US3)

Select the **Intro line node**. In the inspector, verify:
- ✅ **On Enter Actions** section shows a `SetBoolAction` (sets `Flag=true`)
- ✅ **On Exit Actions** section (empty in the sample)
- ✅ Checkpoint flag is checked

Run again: the `Flag` parameter is set on entry to the intro. It persists for later conditions (e.g., a follow-up gated option could check `Flag == true`).

---

## 6. Localization (US4)

With the Run session still active (**Go Back** to the start if needed), change the **locale field** in the toolbar from `en` to `fr`, then press **Run** again.

All text switches to French:
- Intro: "Bienvenue voyageur."
- Choice labels: "Se renseigner sur la ville", "Partir"
- Sub-dialogue: "C'est un endroit paisible."
- Speaker name (if set via key): "Maire"

No graph changes; only the active locale changes. ✅

---

## 7. Step-Back & Checkpoints (US3)

During playback, after reaching the Choice:
- **← GoBack**: steps back to the Intro (restores state, `Flag=true` still set)
- **⏮ Checkpoint**: jumps to the Intro (nearest checkpoint)

Both restore the context state from the checkpoint save. ✅

---

## 8. Console Validation (T071)

After running the full EditMode test suite (**Window ▸ General ▸ Test Runner ▸ EditMode**, filter `Faolline.GraphDialogue.Tests`):

- ✅ All ~30 tests green (Runtime + Editor)
- ✅ No `[GraphDialogue]` or `[GraphCore]` **errors** (only expected `[GraphDialogue]` **warnings** on missing keys in intentional tests)
- ✅ `git diff com.faolline.graphcore/` is **empty** (zero core changes)

---

## 9. Sample Builder Code (Polish)

Open the sample builder code: **Faolline.GraphDialogue.Editor ▸ DialogueSampleBuilder**.

Verify:
- ✅ Generates parent + child graphs programmatically
- ✅ Embeds conditions/actions as sub-assets (portable)
- ✅ Typed parameters added to the parent
- ✅ Checkpoint and entry actions in place

This covers SC-005 (sample covers the whole MVP surface).

---

## Summary

**Authoring (US1)**: ✅ Visual graph editor, multi-speaker, line + choice + sub-dialogue.

**Playback (US2)**: ✅ Headless `DialoguePlayer` emits LineStep/ChoiceStep/EndStep; supports sub-dialogue nesting.

**Inline Reactivity (US3)**: ✅ Entry/edge conditions gate branches; enter/exit actions mutate shared state; step-back history.

**Multi-Locale (US4)**: ✅ Same graph plays in any locale via `ILocalizationProvider` abstraction (CSV default + Unity adapter).

**Polish**: ✅ Sample builder, integration test, README, accessibility colors/hints.

---

**All MVP P1 (T001–T071) criteria met. Ready for production use or extension into US3/US4 features (persistence, branching narrative, etc.).**
