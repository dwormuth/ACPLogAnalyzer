using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACPLogAnalyzer.UnitTests
{
    [TestClass]
    public class ACPLogAnalyzerTests
    {
        [TestMethod]
        public void ParseRepeat_InCorrectString_False()
        {
            List<string> testString = new List<string>(new string[] {"starting target reepeat", "line1", "Line2"});
            Log log = new Log("foo", testString)
            {
                LineLower = testString[0]
            };
            Assert.IsFalse(log.ParseRepeat());
        }

        [TestMethod]
        public void ParseRepeat_CorrectString_True()
        {
            List<string> testString = new List<string>(new string[] { "starting target repeat", "line1", "Line2" });
            Log log = new Log("foo", testString)
            {
                LineLower = testString[0]
            };
            Assert.IsTrue(log.ParseRepeat());
        }
    }
}
