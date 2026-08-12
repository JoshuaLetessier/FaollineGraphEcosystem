using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphLogging.Tests
{
    /// <summary>
    /// A fresh, never-before-seen category defaults to enabled (whether or not a
    /// <see cref="GraphLoggingSettings"/> asset exists in the project) — these calls exercise that
    /// default-enabled path, not an absence of the asset specifically.
    /// </summary>
    public class LoggingTests
    {
        [Test]
        public void Info_NewCategory_StillLogs()
        {
            LogAssert.Expect(LogType.Log, "hello");
            Logging.Info("Some.Category", "hello");
        }

        [Test]
        public void Warning_NewCategory_StillLogs()
        {
            LogAssert.Expect(LogType.Warning, "careful");
            Logging.Warning("Some.Category", "careful");
        }

        [Test]
        public void Error_AlwaysLogs()
        {
            LogAssert.Expect(LogType.Error, "broken");
            Logging.Error("Some.Category", "broken");
        }
    }
}
