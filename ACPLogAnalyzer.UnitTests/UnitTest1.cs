using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACPLogAnalyzer;

namespace ACPLogAnalyzer.UnitTests
{

    [TestClass]
    public class ACPLogAnalyzerTests
    {
        [TestMethod]
        public void ParseRepeat_InCorrectString_False()
        {
            //string mytest = "starting target reepeat";
            List<string> testString = new List<string>(new string[] {"starting target reepeat", "line1", "Line2"});

            Log log = new Log("foo", testString);

            //log.LineLower = "starting target reepeat";
            log.LineLower = testString[0];

            Assert.IsFalse(log.ParseRepeat());
            //Assert.IsTrue(true);
        }

        [TestMethod]
        public void ParseRepeat_CorrectString_True()
        {
            //string mytest = "starting target reepeat";
            List<string> testString = new List<string>(new string[] { "starting target repeat", "line1", "Line2" });

            Log log = new Log("foo", testString);

            //log.LineLower = "starting target reepeat";
            log.LineLower = testString[0];

            Assert.IsTrue(log.ParseRepeat());
            //Assert.IsTrue(true);
        }
    }
}
