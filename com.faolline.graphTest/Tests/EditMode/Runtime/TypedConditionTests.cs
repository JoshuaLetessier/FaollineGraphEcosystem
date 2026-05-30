using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TypedConditionTests
    {
        private static BaseContext CtxInt(string key, int v)    { var c = new BaseContext(); c.Set<int>(key, v);    return c; }
        private static BaseContext CtxFloat(string key, float v){ var c = new BaseContext(); c.Set<float>(key, v);  return c; }
        private static BaseContext CtxStr(string key, string v) { var c = new BaseContext(); c.Set<string>(key, v); return c; }

        [Test]
        public void IntCondition_GreaterOrEqual_Works()
        {
            var cond = ScriptableObject.CreateInstance<TestIntCondition>();
            cond.ParameterKey = "score"; cond.Operator = ComparisonOperator.GreaterOrEqual; cond.ExpectedValue = 3;
            try
            {
                Assert.IsTrue(cond.Evaluate(CtxInt("score", 5)));
                Assert.IsTrue(cond.Evaluate(CtxInt("score", 3)));
                Assert.IsFalse(cond.Evaluate(CtxInt("score", 1)));
            }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void IntCondition_MissingKey_ReturnsFalseWithWarning()
        {
            var cond = ScriptableObject.CreateInstance<TestIntCondition>();
            cond.ParameterKey = "absent"; cond.Operator = ComparisonOperator.Equal; cond.ExpectedValue = 0;
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"not found"));
                Assert.IsFalse(cond.Evaluate(new BaseContext()));
            }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void IntCondition_MistypedValue_ReturnsFalseWithWarning()
        {
            var cond = ScriptableObject.CreateInstance<TestIntCondition>();
            cond.ParameterKey = "flag"; cond.Operator = ComparisonOperator.Equal; cond.ExpectedValue = 1;
            var ctx = new BaseContext(); ctx.Set<bool>("flag", true);
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"not an int"));
                Assert.IsFalse(cond.Evaluate(ctx));
            }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void FloatCondition_Less_Works()
        {
            var cond = ScriptableObject.CreateInstance<TestFloatCondition>();
            cond.ParameterKey = "hp"; cond.Operator = ComparisonOperator.Less; cond.ExpectedValue = 0.5f;
            try
            {
                Assert.IsTrue(cond.Evaluate(CtxFloat("hp", 0.2f)));
                Assert.IsFalse(cond.Evaluate(CtxFloat("hp", 0.9f)));
            }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void StringCondition_EqualityAndNegate_Work()
        {
            var cond = ScriptableObject.CreateInstance<TestStringCondition>();
            cond.ParameterKey = "name"; cond.ExpectedValue = "hero"; cond.Negate = false;
            try
            {
                Assert.IsTrue(cond.Evaluate(CtxStr("name", "hero")));
                Assert.IsFalse(cond.Evaluate(CtxStr("name", "villain")));

                cond.Negate = true;
                Assert.IsTrue(cond.Evaluate(CtxStr("name", "villain")));
                Assert.IsFalse(cond.Evaluate(CtxStr("name", "hero")));
            }
            finally { Object.DestroyImmediate(cond); }
        }
    }
}
