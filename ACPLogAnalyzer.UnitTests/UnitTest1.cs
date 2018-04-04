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
        public void ParseRepeat_CorrectString_True()
        {
            static string mytest = "starting target reepeat";
            static List<string> testString = new List<string>(mytest);

            Log log = new Log("foo", testString);

            //LineLower = "starting target reepeat";
            Assert.IsTrue(ACPLogAnalyzer.Log.ParseRepeat());
            //Assert.IsTrue(true);

        }
    }
}
