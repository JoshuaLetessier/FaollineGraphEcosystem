using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
// BoolCondition now lives in BOTH Faolline.GraphCore (canonical) and Faolline.GraphStandard (back-compat subclass);
// pin the name to the GraphStandard one this suite is meant to exercise.
using BoolCondition = Faolline.GraphStandard.BoolCondition;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>Scalar conditions read an absent key as false SILENTLY by default; WarnOnMissing opts into a warning
    /// (consistent with the collection conditions, which are already silent on absence).</summary>
    public class ConditionWarnOnMissingTests
    {
        [Test]
        public void BoolCondition_AbsentKey_IsFalse_Silently_ByDefault()
        {
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.ParameterKey = "missing"; c.ExpectedValue = true;
            try { Assert.IsFalse(c.Evaluate(new BaseContext())); }   // no warning by default
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void BoolCondition_AbsentKey_Warns_WhenOptedIn()
        {
            var c = ScriptableObject.CreateInstance<BoolCondition>();
            c.ParameterKey = "missing"; c.ExpectedValue = true; c.WarnOnMissing = true;
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("BoolCondition.*not found"));
                Assert.IsFalse(c.Evaluate(new BaseContext()));
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void IntCondition_AbsentKey_IsFalse_Silently_ByDefault()
        {
            var c = ScriptableObject.CreateInstance<IntCondition>();
            c.ParameterKey = "missing"; c.ExpectedValue = 1;
            try { Assert.IsFalse(c.Evaluate(new BaseContext())); }
            finally { Object.DestroyImmediate(c); }
        }
    }
}
