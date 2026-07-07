using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>Scalar conditions read an absent key as false SILENTLY by default; WarnOnMissing opts into a warning
    /// (consistent with the collection conditions, which are already silent on absence).</summary>
    public class ConditionWarnOnMissingTests
    {
        [Test]
        public void BoolCondition_AbsentKey_IsFalse_Silently_ByDefault()
        {
            var missing = VariableDef.Bool("missing");
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.Variable = missing; c.ExpectedValue = true;
            try { Assert.IsFalse(c.Evaluate(new BaseContext())); }   // no warning by default
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(missing); }
        }

        [Test]
        public void BoolCondition_AbsentKey_Warns_WhenOptedIn()
        {
            var missing = VariableDef.Bool("missing");
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.Variable = missing; c.ExpectedValue = true; c.WarnOnMissing = true;
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("BoolCondition.*not found"));
                Assert.IsFalse(c.Evaluate(new BaseContext()));
            }
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(missing); }
        }

        [Test]
        public void IntCondition_AbsentKey_IsFalse_Silently_ByDefault()
        {
            var missing = VariableDef.Int("missing");
            var c = ScriptableObject.CreateInstance<IntCondition>();
            c.Variable = missing; c.ExpectedValue = 1;
            try { Assert.IsFalse(c.Evaluate(new BaseContext())); }
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(missing); }
        }
    }
}
