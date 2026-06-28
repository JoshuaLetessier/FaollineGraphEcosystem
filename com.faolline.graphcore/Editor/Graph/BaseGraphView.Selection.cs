using UnityEditor.Experimental.GraphView;

namespace Faolline.GraphCore.Editor
{
    public abstract partial class BaseGraphView
    {
        /// <inheritdoc/>
        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            NotifySelectionChanged();
        }

        /// <inheritdoc/>
        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            NotifySelectionChanged();
        }

        /// <inheritdoc/>
        public override void ClearSelection()
        {
            base.ClearSelection();
            SelectionCleared?.Invoke();
        }

        private void NotifySelectionChanged()
        {
            int nodeCount = 0;
            BaseNodeData lastData = null;

            foreach (var item in selection)
            {
                if (item is BaseNodeView nv && nv.NodeData != null)
                {
                    nodeCount++;
                    lastData = nv.NodeData;
                }
            }

            if (nodeCount == 1)
                NodeSelected?.Invoke(lastData);
            else
                SelectionCleared?.Invoke();
        }
    }
}
