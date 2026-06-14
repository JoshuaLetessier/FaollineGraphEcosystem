using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    public class ParameterDataTests
    {
        // ParameterType enum (T009)

        [Test]
        public void ParameterType_HasExactlySevenValues()
        {
            // The four primitives plus the three serialization-friendly Unity value types.
            // Object/GameObject references are intentionally excluded (they need a stable-id scheme).
            Assert.AreEqual(7, System.Enum.GetValues(typeof(ParameterType)).Length,
                "ParameterType must have exactly 7 values (bool/int/float/string + Vector2/Vector3/Color).");
        }

        [Test]
        public void ParameterType_HasCorrectIntegerValues()
        {
            Assert.AreEqual(0, (int)ParameterType.Bool);
            Assert.AreEqual(1, (int)ParameterType.Int);
            Assert.AreEqual(2, (int)ParameterType.Float);
            Assert.AreEqual(3, (int)ParameterType.String);
            Assert.AreEqual(4, (int)ParameterType.Vector2);
            Assert.AreEqual(5, (int)ParameterType.Vector3);
            Assert.AreEqual(6, (int)ParameterType.Color);
        }

        // ParameterData (T011, US4 T052-T053)

        [Test]
        public void ParameterData_IsSerializable()
        {
            var attrs = typeof(ParameterData).GetCustomAttributes(
                typeof(System.SerializableAttribute), false);
            Assert.IsTrue(attrs.Length > 0, "ParameterData must be marked [Serializable].");
        }

        [Test]
        public void ParameterData_Key_Type_DefaultValue_GetSet()
        {
            var param = new ParameterData
            {
                Key = "MyFloat",
                Type = ParameterType.Float,
                DefaultValue = "3.14"
            };
            Assert.AreEqual("MyFloat", param.Key);
            Assert.AreEqual(ParameterType.Float, param.Type);
            Assert.AreEqual("3.14", param.DefaultValue);
        }

        [Test]
        public void ParameterData_AllFourTypes_SerializeDistinctly()
        {
            var b = new ParameterData { Key = "Flag",  Type = ParameterType.Bool,   DefaultValue = "false" };
            var i = new ParameterData { Key = "Score", Type = ParameterType.Int,    DefaultValue = "0"     };
            var f = new ParameterData { Key = "Speed", Type = ParameterType.Float,  DefaultValue = "1.0"   };
            var s = new ParameterData { Key = "Name",  Type = ParameterType.String, DefaultValue = "Hero"  };

            Assert.AreEqual(ParameterType.Bool,   b.Type);
            Assert.AreEqual(ParameterType.Int,    i.Type);
            Assert.AreEqual(ParameterType.Float,  f.Type);
            Assert.AreEqual(ParameterType.String, s.Type);

            Assert.AreNotEqual(b.Type, i.Type);
            Assert.AreNotEqual(b.Type, f.Type);
            Assert.AreNotEqual(b.Type, s.Type);
        }
    }
}
