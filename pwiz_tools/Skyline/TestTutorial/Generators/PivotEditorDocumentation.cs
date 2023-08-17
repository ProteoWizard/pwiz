using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;

namespace pwiz.SkylineTestTutorial.Generators
{

    [TestClass]
    public class PivotEditorDocumentation : DocumentationGeneratorTest
    {
        [TestMethod]
        public void GeneratePivotEditorDocumentation()
        {
            TestFilesZip = "https://skyline.gs.washington.edu/tutorials/GroupedStudies1.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            CoverShotName = "PivotEditor";
            var image = TakeScreenShot(SkylineWindow);
            ResXResourceWriter.AddResource("MainPage", "![Skyline Window](MyPicture.png)");
            ResXResourceWriter.AddResource("MyPicture", image);
            ResXResourceWriter.Close();
            // Console.Out.WriteLine("{0}", _resourceStringWriter);

            var html = Markdig.Markdown.ToHtml("![Skyline Window](MyPicture.png \"The Skyline Window\")");
            var htmlWithEmbeddedImages = ReplaceImage(html, "MyPicture.png", image);
            Console.Out.WriteLine(htmlWithEmbeddedImages);
            RunUI(() =>
            {
                DocumentationViewer documentationViewer = new DocumentationViewer(false);
                documentationViewer.DocumentationHtml = htmlWithEmbeddedImages;
                documentationViewer.Show(SkylineWindow);
            });
            PauseTest();
        }

        private string ReplaceImage(string html, string name, Image image)
        {
            var memoryStream = new MemoryStream();
            image.Save(memoryStream, ImageFormat.Png);
            var newImageSrc = "data:image/png;base64," + Convert.ToBase64String(memoryStream.ToArray());
            return html.Replace(name, newImageSrc);

        }
    }
}
