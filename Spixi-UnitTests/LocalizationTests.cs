using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPIXI.Lang;

namespace Spixi_UnitTests
{
    [TestClass]
    public class TestLocalization
    {
        [TestMethod]
        public void TestLanguageFiles()
        {
            Assert.IsTrue(SpixiLocalization.testLanguageFiles("en-us"));
        }
    }
}
