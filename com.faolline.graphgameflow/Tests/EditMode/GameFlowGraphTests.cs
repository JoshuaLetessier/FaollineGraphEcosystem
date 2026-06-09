using System;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// The creatable gameflow graph asset: a BaseGraph (so the slice-1 driver accepts it unchanged) that is
    /// available from Assets > Create.
    /// </summary>
    public class GameFlowGraphTests
    {
        [Test]
        public void GameFlowGraph_IsBaseGraph()
        {
            Assert.IsTrue(typeof(BaseGraph).IsAssignableFrom(typeof(GameFlowGraph)),
                "GameFlowGraph must be a BaseGraph so GraphFlowDriver accepts it unchanged.");
        }

        [Test]
        public void GameFlowGraph_HasCreateAssetMenu_WithExpectedMenuName()
        {
            var attr = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(
                typeof(GameFlowGraph), typeof(CreateAssetMenuAttribute));
            Assert.IsNotNull(attr, "GameFlowGraph must be creatable from Assets > Create.");
            Assert.AreEqual("GraphGameFlow/Game Flow Graph", attr.menuName);
        }
    }
}
