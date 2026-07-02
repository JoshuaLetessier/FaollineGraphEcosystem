using System;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphGameFlow;

namespace Faolline.GraphDialogue.Samples.GameFlowBridge
{
    /// <summary>
    /// Adapts a <see cref="GraphFlowDriver"/> running a flow that embeds dialogue nodes (via a
    /// <c>SubGraphNodeData</c>) into an <see cref="IDialoguePlaybackSource"/>, so the same
    /// <see cref="DialoguePlaybackController"/>/<see cref="IDialogueView"/> used by standalone
    /// <see cref="DialogueDriver"/> dialogues can render flow-embedded ones too.
    /// <para>
    /// The host runner (the flow's) owns the cursor — this class never creates its own runner. It
    /// resolves each entered node with a runner-agnostic <see cref="DialoguePresenter"/>: a dialogue
    /// node raises <see cref="OnLine"/>/<see cref="OnChoices"/> and suspends the driver's own
    /// <see cref="GraphFlowDriver.AutoAdvance"/> for the duration of the dialogue segment; a
    /// non-dialogue node restores it and (if a dialogue segment was active) raises <see cref="OnEnded"/>
    /// so the view can clear.
    /// </para>
    /// <para>
    /// This file lives in a Sample, not a shipped assembly: <c>com.faolline.graphdialoguesystem</c> and
    /// <c>com.faolline.graphgameflow</c> must not depend on each other (ecosystem constitution, Principle
    /// VII — cross-lib composition only through <c>SubGraphNodeData</c> at the graph level). Import this
    /// sample only in a project that already has both packages installed.
    /// </para>
    /// </summary>
    public sealed class GraphFlowDialogueSource : IDialoguePlaybackSource
    {
        private readonly GraphFlowDriver _driver;
        private readonly DialoguePresenter _presenter;
        private bool _inDialogue;
        private bool _autoAdvanceBeforeDialogue;

        /// <inheritdoc/>
        public event Action<LineStep> OnLine;

        /// <inheritdoc/>
        public event Action<ChoiceStep> OnChoices;

        /// <inheritdoc/>
        public event Action<EndStep> OnEnded;

        /// <inheritdoc/>
        public event Action OnStuck;

        public GraphFlowDialogueSource(GraphFlowDriver driver, DialoguePresenter presenter)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _driver.OnNodeEntered += HandleNodeEntered;
            _driver.OnNodeCompleted += HandleNodeCompleted;
            _driver.OnStuck += HandleStuck;
        }

        /// <summary>Detaches from the driver. Call when the host component is destroyed.</summary>
        public void Teardown()
        {
            _driver.OnNodeEntered -= HandleNodeEntered;
            _driver.OnNodeCompleted -= HandleNodeCompleted;
            _driver.OnStuck -= HandleStuck;
        }

        /// <inheritdoc/>
        public void Advance() => _driver.Advance();

        /// <inheritdoc/>
        public void Choose(string choiceId) => _driver.ChooseById(choiceId);

        private void HandleNodeEntered(BaseNodeData node)
        {
            // A router choice node (condition branches, no player-facing DialogueChoice options) is never shown:
            // it is auto-resolved on completion (see HandleNodeCompleted), not rendered as buttons. Skip it here
            // so it neither draws a choice prompt nor ends the dialogue segment.
            if (node is ChoiceNodeData choiceNode && DialoguePresenter.IsRouter(choiceNode))
                return;

            var step = _presenter.Resolve(node, _driver.Context);

            if (step == null)
            {
                if (_inDialogue)
                {
                    _inDialogue = false;
                    _driver.AutoAdvance = _autoAdvanceBeforeDialogue;
                    OnEnded?.Invoke(new EndStep(node.Id, EndReason.Completed, null));
                }
                return;
            }

            if (!_inDialogue)
            {
                _inDialogue = true;
                _autoAdvanceBeforeDialogue = _driver.AutoAdvance;
            }
            // Dialogue nodes always pause for the view to render + the player to advance/choose —
            // never auto-resolved by the flow, mirroring how a ChoiceNodeData already pauses it.
            _driver.AutoAdvance = false;

            switch (step)
            {
                case LineStep line: OnLine?.Invoke(line); break;
                case ChoiceStep choice: OnChoices?.Invoke(choice); break;
            }
        }

        // Auto-resolve a router choice node once the runner has completed it (ChooseById requires NodeReady,
        // which OnNodeCompleted guarantees — routing on OnNodeEntered would be a no-op). Takes the first branch
        // whose condition passes; a dead router surfaces as stuck via the driver.
        private void HandleNodeCompleted(BaseNodeData node)
        {
            if (!(node is ChoiceNodeData choiceNode) || !DialoguePresenter.IsRouter(choiceNode)) return;
            var branchId = _presenter.ResolveRouterBranchId(choiceNode, _driver.Context);
            if (!string.IsNullOrEmpty(branchId))
                _driver.ChooseById(branchId);
        }

        private void HandleStuck() => OnStuck?.Invoke();
    }
}
