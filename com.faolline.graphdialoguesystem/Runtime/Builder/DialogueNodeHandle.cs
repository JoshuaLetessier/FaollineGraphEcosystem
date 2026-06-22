using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Non-generic base of a node handle returned by <see cref="DialogueGraphBuilder"/> — just the node + owner
    /// it belongs to. Used as the target type when connecting edges. The fluent methods live on the typed
    /// <see cref="DialogueNodeHandle{TSelf}"/> so they return the concrete handle (line/choice/…) for chaining.
    /// </summary>
    public abstract class DialogueNodeHandle
    {
        internal BaseNodeData Node { get; }
        internal DialogueGraphBuilder Owner { get; }

        internal DialogueNodeHandle(DialogueGraphBuilder owner, BaseNodeData node)
        {
            Owner = owner;
            Node = node;
            owner.Track(this);
        }
    }

    /// <summary>The shared fluent wiring surface (connect, entry, id, position, conditions), self-typed so each
    /// method returns the concrete handle for chaining.</summary>
    public abstract class DialogueNodeHandle<TSelf> : DialogueNodeHandle where TSelf : DialogueNodeHandle<TSelf>
    {
        internal DialogueNodeHandle(DialogueGraphBuilder owner, BaseNodeData node) : base(owner, node) { }

        /// <summary>Connects this node to <paramref name="target"/> (default port "out").</summary>
        public TSelf To(DialogueNodeHandle target, string portName = "out")
        {
            Owner.Connect(this, target, portName);
            return (TSelf)this;
        }

        /// <summary>Marks this node as the dialogue's entry point.</summary>
        public TSelf AsEntry()
        {
            Owner.SetEntry(this);
            return (TSelf)this;
        }

        /// <summary>Overrides the auto-GUID with a stable id (call before wiring edges, which read the id).</summary>
        public TSelf Id(string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) Node.Id = id;
            return (TSelf)this;
        }

        /// <summary>Sets the node's canvas position (cosmetic; the builder auto-columns otherwise).</summary>
        public TSelf At(float x, float y)
        {
            Node.Position = new Vector2(x, y);
            return (TSelf)this;
        }

        /// <summary>Adds entry conditions gating this node.</summary>
        public TSelf When(params BaseCondition[] conditions)
        {
            if (conditions != null)
                foreach (var c in conditions)
                    if (c != null) Node.EntryConditions.Add(c);
            return (TSelf)this;
        }

        /// <summary>Appends enter-actions (run on entering the node).</summary>
        public TSelf OnEnter(params BaseAction[] actions)
        {
            if (actions != null)
                foreach (var a in actions)
                    if (a != null) Node.OnEnterActions.Add(a);
            return (TSelf)this;
        }

        /// <summary>Appends exit-actions (run on leaving the node).</summary>
        public TSelf OnExit(params BaseAction[] actions)
        {
            if (actions != null)
                foreach (var a in actions)
                    if (a != null) Node.OnExitActions.Add(a);
            return (TSelf)this;
        }

        /// <summary>Marks this node as a checkpoint for GoBackToCheckpoint.</summary>
        public TSelf Checkpoint(bool value = true)
        {
            Node.IsCheckpoint = value;
            return (TSelf)this;
        }

        /// <summary>Parks the player on this node until the named signal is raised.</summary>
        public TSelf Await(string signalName)
        {
            Node.AwaitSignalName = signalName;
            return (TSelf)this;
        }

        /// <summary>Holds on this node for the given seconds of host-fed time before advancing.</summary>
        public TSelf Wait(float seconds)
        {
            Node.WaitDuration = seconds;
            return (TSelf)this;
        }

        /// <summary>Appends resume conditions (gate checked when the awaited signal arrives).</summary>
        public TSelf ResumeWhen(params BaseCondition[] conditions)
        {
            if (conditions != null)
                foreach (var c in conditions)
                    if (c != null) Node.ResumeConditions.Add(c);
            return (TSelf)this;
        }
    }
}
