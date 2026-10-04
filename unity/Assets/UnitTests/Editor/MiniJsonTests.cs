using System;
using System.Collections.Generic;
using Assets.Scripts.Tts;
using NUnit.Framework;

namespace Valkyrie.UnitTests
{
    [TestFixture]
    public class MiniJsonTests
    {
        [Test]
        public void Parse_Object_ReturnsAllValueTypes()
        {
            var result = (Dictionary<string, object>)MiniJson.Parse(
                "{\"a\": 1, \"b\": [1, 2.5, -3e2], \"c\": \"x\\ny\\u0041\", \"d\": true, \"e\": false, \"f\": null}");

            Assert.AreEqual(1.0, result["a"]);
            CollectionAssert.AreEqual(new object[] { 1.0, 2.5, -300.0 }, (List<object>)result["b"]);
            Assert.AreEqual("x\nyA", result["c"]);
            Assert.AreEqual(true, result["d"]);
            Assert.AreEqual(false, result["e"]);
            Assert.IsNull(result["f"]);
        }

        [Test]
        public void Parse_NestedArrays_PreservesStructure()
        {
            var result = (List<object>)MiniJson.Parse("[[[0.5, -0.25]], []]");

            var inner = (List<object>)((List<object>)result[0])[0];
            CollectionAssert.AreEqual(new object[] { 0.5, -0.25 }, inner);
            Assert.IsEmpty((List<object>)result[1]);
        }

        [Test]
        public void Parse_EmptyObject_ReturnsEmptyDictionary()
        {
            var result = (Dictionary<string, object>)MiniJson.Parse(" { } ");

            Assert.IsEmpty(result);
        }

        [Test]
        public void Parse_TrailingContent_Throws()
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse("1 2"));
        }

        [Test]
        public void Parse_InvalidValue_Throws()
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\": x}"));
        }

        [Test]
        public void Parse_MissingColon_Throws()
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\" 1}"));
        }
    }
}
