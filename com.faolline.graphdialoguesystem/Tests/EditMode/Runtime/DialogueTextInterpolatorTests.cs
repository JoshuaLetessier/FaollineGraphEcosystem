using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Tests
{
    public class DialogueTextInterpolatorTests
    {
        private static BaseContext Ctx()
        {
            var c = new BaseContext();
            c.Set("name", "Bob");
            c.Set("score", 5);
            c.Set("ratio", 1.5f);
            c.Set("flag", true);
            return c;
        }

        [Test]
        public void SubstitutesStringToken()
            => Assert.AreEqual("Hi Bob!", DialogueTextInterpolator.Interpolate("Hi {name}!", Ctx()));

        [Test]
        public void FormatsNumbersInvariant()
            => Assert.AreEqual("5 / 1.5", DialogueTextInterpolator.Interpolate("{score} / {ratio}", Ctx()));

        [Test]
        public void UnknownToken_IsLeftLiteral()
            => Assert.AreEqual("Hi {missing}", DialogueTextInterpolator.Interpolate("Hi {missing}", Ctx()));

        [Test]
        public void TrimsTokenWhitespace()
            => Assert.AreEqual("Bob", DialogueTextInterpolator.Interpolate("{ name }", Ctx()));

        [Test]
        public void EscapedBraces_BecomeLiteral()
            => Assert.AreEqual("{x}", DialogueTextInterpolator.Interpolate("{{x}}", Ctx()));

        [Test]
        public void NoBraces_ReturnsInput()
            => Assert.AreEqual("plain text", DialogueTextInterpolator.Interpolate("plain text", Ctx()));

        [Test]
        public void NullOrEmpty_AreSafe()
        {
            Assert.IsNull(DialogueTextInterpolator.Interpolate(null, Ctx()));
            Assert.AreEqual("", DialogueTextInterpolator.Interpolate("", Ctx()));
            Assert.AreEqual("{x}", DialogueTextInterpolator.Interpolate("{x}", null));
        }

        // Integration: a context value feeds a {token} in the resolved line text. The interpolator reads tokens
        // by RAW string key, so the context is seeded on the raw "name" key (the raw-island channel).
        [Test]
        public void Player_InterpolatesLineTextFromContext()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });

            var provider = new CsvLocalizationProvider("Key,en\nline_l,Hi {name}!\n", "en");
            var ctx = new DialogueContext();
            ctx.Set<string>("name", "Bob");
            var player = new DialoguePlayer(g, ctx, provider);
            LineStep line = null;
            player.OnLine += s2 => line = s2;
            try
            {
                player.Start();
                Assert.IsNotNull(line);
                Assert.AreEqual("Hi Bob!", line.ResolvedText);
            }
            finally { Object.DestroyImmediate(g); }
        }
    }
}
