using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the inline condition set, including null-safety.</summary>
    public class ConditionTests
    {
        private static T Make<T>() where T : ScriptableObject => ScriptableObject.CreateInstance<T>();

        [Test]
        public void AlwaysTrueAndFalse()
        {
            var ctx = new DialogueContext();
            Assert.IsTrue(Make<AlwaysTrueCondition>().Evaluate(ctx));
            Assert.IsFalse(Make<AlwaysFalseCondition>().Evaluate(ctx));
        }

        [Test]
        public void Bool_Compares()
        {
            var ctx = new DialogueContext();
            var k = ParameterName.Bool("k");
            ctx.Set<bool>(k, true);
            var c = Make<BoolCondition>();
            c.Parameter = k; c.ExpectedValue = true;
            Assert.IsTrue(c.Evaluate(ctx));
            c.ExpectedValue = false;
            Assert.IsFalse(c.Evaluate(ctx));
        }

        [Test]
        public void Int_Operators()
        {
            var ctx = new DialogueContext();
            var k = ParameterName.Int("k");
            ctx.Set<int>(k, 5);
            var c = Make<IntCondition>();
            c.Parameter = k; c.ExpectedValue = 3; c.Operator = ComparisonOperator.Greater;
            Assert.IsTrue(c.Evaluate(ctx));
            c.Operator = ComparisonOperator.LessOrEqual;
            Assert.IsFalse(c.Evaluate(ctx));
        }

        [Test]
        public void Float_Operators()
        {
            var ctx = new DialogueContext();
            var k = ParameterName.Float("k");
            ctx.Set<float>(k, 2.5f);
            var c = Make<FloatCondition>();
            c.Parameter = k; c.ExpectedValue = 2.5f; c.Operator = ComparisonOperator.GreaterOrEqual;
            Assert.IsTrue(c.Evaluate(ctx));
        }

        [Test]
        public void String_EqualityAndNegate()
        {
            var ctx = new DialogueContext();
            var k = ParameterName.String("k");
            ctx.Set<string>(k, "abc");
            var c = Make<StringCondition>();
            c.Parameter = k; c.ExpectedValue = "abc";
            Assert.IsTrue(c.Evaluate(ctx));
            c.Negate = true;
            Assert.IsFalse(c.Evaluate(ctx));
        }

        [Test]
        public void MissingKey_ReturnsFalse_AndWarns()
        {
            var ctx = new DialogueContext();
            var c = Make<IntCondition>();
            c.Parameter = ParameterName.Int("absent"); c.ExpectedValue = 0;
            c.WarnOnMissing = true;   // canonical reads an absent key as false SILENTLY by default; opt in to the warning
            LogAssert.Expect(LogType.Warning, new Regex("not found"));
            Assert.IsFalse(c.Evaluate(ctx));
        }
    }
}
