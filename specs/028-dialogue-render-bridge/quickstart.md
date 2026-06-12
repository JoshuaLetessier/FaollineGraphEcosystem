# Quickstart — render an embedded dialogue from a gameflow host

Your host flow embeds a dialogue as a SubGraph; the host's `GraphFlowDriver` runs it. To *render* it, resolve
the host runner's current node with a `DialoguePresenter` — no `DialoguePlayer`, no resolution rewrite:

```csharp
// once
var presenter = new DialoguePresenter(localizationProvider, assets, speakerLookup);

driver.OnNodeEntered += node =>
{
    var step = presenter.Resolve(node, driver.Context);     // null for non-dialogue nodes
    switch (step)
    {
        case LineStep line:
            driver.AutoAdvance = false;                      // pause lines for reading
            ShowLine(line.ResolvedSpeakerName, line.ResolvedText);   // "continue" → driver.Advance()
            break;
        case ChoiceStep choice:
            ShowChoices(choice.Options);                      // a pick → driver.ChooseById(optionId)
            break;
        default:
            driver.AutoAdvance = true;                        // back to auto for the host flow
            break;
    }
};
```

- A **choice** pauses on its own (the driver no longer auto-resolves a `ChoiceNodeData`); pick with
  `driver.ChooseById(optionId)`.
- A **line** is paced by toggling `AutoAdvance`; your "continue" button calls `driver.Advance()`.
- The dialogue's outcome already flows through the **shared `GameFlowContext`** (SubGraph + InheritParentContext),
  so an authored action on the line writes straight into your progression — no bridge code.

`DialoguePlayer` is unchanged for standalone dialogues; it now resolves through the same presenter internally.
