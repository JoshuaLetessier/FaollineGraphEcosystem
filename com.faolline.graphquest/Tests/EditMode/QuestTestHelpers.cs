using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>A test condition that holds when a context bool flag is true.</summary>
    public sealed class FlagCondition : BaseCondition
    {
        public string Key;
        public override bool Evaluate(BaseContext context)
            => context != null && context.TryGet<bool>(Key, out var v) && v;

        public static FlagCondition For(string key)
        {
            var c = CreateInstance<FlagCondition>();
            c.Key = key;
            return c;
        }
    }

    /// <summary>A test action that counts how many times it executed (for one-shot reward assertions).</summary>
    public sealed class CountingAction : BaseAction
    {
        public int Count;
        public override void Execute(BaseContext context) => Count++;
        public static CountingAction New() => CreateInstance<CountingAction>();
    }

    /// <summary>Base for graphquest EditMode tests: factory helpers that track created ScriptableObjects and a
    /// TearDown that destroys them (no leaked-object warnings).</summary>
    public abstract class QuestTestBase
    {
        private readonly List<Object> _created = new List<Object>();

        protected T Track<T>(T o) where T : Object { if (o != null) _created.Add(o); return o; }
        protected FlagCondition Flag(string key) => Track(FlagCondition.For(key));
        protected CountingAction Counter() => Track(CountingAction.New());
        protected QuestCompletedCondition QuestDone(params string[] questIds) => Track(QuestCompletedCondition.For(questIds));
        protected QuestGraph TrackGraph(QuestGraph g) => Track(g);

        [TearDown]
        public void CleanupTrackedObjects()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }
    }
}
