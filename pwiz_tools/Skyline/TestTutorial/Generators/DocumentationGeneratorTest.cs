using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial.Generators
{
    public abstract class DocumentationGeneratorTest : AbstractFunctionalTestEx
    {
        protected StringWriter _resourceStringWriter;
        protected DocumentationGeneratorTest()
        {
            DocumentationStringBuilder = new StringBuilder();
            _resourceStringWriter = new StringWriter();
            ResXResourceWriter = new ResXResourceWriter(_resourceStringWriter);
        }

        protected StringBuilder DocumentationStringBuilder { get; }
        protected ResXResourceWriter ResXResourceWriter { get; }

        public Image TakeScreenShot(Form form)
        {
            var screenShotTaker = new ScreenShotTaker();
            var image = screenShotTaker.TakeScreenShot(form);
            Assert.IsNotNull(image);
            return image;
        }

        public string GetImagesFolder()
        {
            var thisFile = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();
            if (string.IsNullOrEmpty(thisFile))
            {
                AssertEx.Fail("Could not source file folder");
            }

            string grandParentFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)));
            Assert.IsNotNull(grandParentFolder, "Unable to get great-grandparent folder of {0}", thisFile);
            return Path.Combine(grandParentFolder, "Documentation\\Tutorials\\Markdown\\" + CoverShotName + "\\Images");
        }

        public void SaveImage(Image image, ImageFormat imageFormat, string filename)
        {
            var imagesFolder = GetImagesFolder();
            Assert.IsTrue(Directory.Exists(imagesFolder), "Folder {0} does not exist", imagesFolder);
            image.Save(Path.Combine(imagesFolder, filename), imageFormat);
        }

        public void SaveScreenshot(Form form, string filename)
        {
            var image = TakeScreenShot(form);
            SaveImage(image, ImageFormat.Png, filename);
        }
    }
}
