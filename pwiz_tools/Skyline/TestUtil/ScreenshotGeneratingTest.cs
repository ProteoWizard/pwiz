using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace pwiz.SkylineTestUtil
{
    public abstract class ScreenshotGeneratingTest : AbstractFunctionalTestEx
    {
        private HashSet<string> _savedImages = new HashSet<string>();

        protected StringWriter _resourceStringWriter;
        protected ScreenshotGeneratingTest()
        {
            DocumentationStringBuilder = new StringBuilder();
            _resourceStringWriter = new StringWriter();
            ResXResourceWriter = new ResXResourceWriter(_resourceStringWriter);
        }

        protected StringBuilder DocumentationStringBuilder { get; }
        protected ResXResourceWriter ResXResourceWriter { get; }

        public Bitmap TakeScreenShot(Control form)
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

            string grandParentFolder = Path.GetDirectoryName(Path.GetDirectoryName(thisFile));
            Assert.IsNotNull(grandParentFolder, "Unable to get grandparent folder of {0}", thisFile);
            return Path.Combine(grandParentFolder, "Documentation\\Tutorials\\Markdown\\" + CoverShotName + "\\media");
        }

        public void SaveImage(Image image, ImageFormat imageFormat, string filename)
        {
            Assert.IsTrue(_savedImages.Add(filename), "{0} has already been saved", filename);
            var imagesFolder = GetImagesFolder();
            Assert.IsTrue(Directory.Exists(imagesFolder), "Folder {0} does not exist", imagesFolder);
            string fullPath = Path.Combine(imagesFolder, filename);
            try
            {
                image.Save(fullPath, imageFormat);
            }
            catch (Exception e)
            {
                throw new IOException("Error saving to " + fullPath, e);
            }
        }

        public void SaveScreenshot(Control form, string filename)
        {
            var image = TakeScreenShot(form);
            SaveImage(image, ImageFormat.Png, filename + ".png");
        }

        public void RunUISaveScreenshot(Control form, string filename)
        {
            RunUI(() =>
            {
                SaveScreenshot(form, filename);
            });
        }
    }
}
