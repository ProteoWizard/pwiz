using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DocumentationGenerators
{

    [TestClass]
    public class PivotEditorTutorial : DocumentationGeneratorTest
    {
        [TestMethod]
        public void GeneratePivotEditorTutorial()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var image = TakeScreenShot(SkylineWindow);
            ResXResourceWriter.AddResource("SkylineWindow", image);
            ResXResourceWriter.Close();
            Console.Out.WriteLine("{0}", _resourceStringWriter);
        }
    }
}
