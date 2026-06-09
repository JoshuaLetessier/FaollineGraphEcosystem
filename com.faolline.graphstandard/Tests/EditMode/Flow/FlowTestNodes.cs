using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>Test enter-action: adds an item to a context collection when executed.</summary>
    public class FlowAddToCollectionAction : BaseAction
    {
        public string Key;
        public string Item;
        public override void Execute(BaseContext context) => context.AddToCollection(Key, Item);
    }

    /// <summary>Test edge condition: passes when a named bool context parameter is true.</summary>
    public class FlowBoolCondition : BaseCondition
    {
        public string Key;
        public override bool Evaluate(BaseContext context) => context.TryGet<bool>(Key, out var b) && b;
    }
}
