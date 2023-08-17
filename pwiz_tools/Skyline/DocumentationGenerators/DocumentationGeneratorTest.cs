using System.Drawing;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace DocumentationGenerators
{
    public abstract class DocumentationGeneratorTest : AbstractFunctionalTest
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
            Image image = null;
            RunUI(() =>
            {
                var screenShotTaker = new ScreenShotTaker();
                image = screenShotTaker.TakeScreenShot(form);
            });
            Assert.IsNotNull(image);
            return image;
        }

        private string GetImagesFolder()
        {
            var thisFile = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();
            if (string.IsNullOrEmpty(thisFile))
            {
                AssertEx.Fail("Could not source file folder");
            }

            string parentFolder = Path.GetDirectoryName(thisFile);
            Assert.IsNotNull(parentFolder, "Unable to get parent folder of {0}", thisFile);
            return Path.Combine(parentFolder, "Images");
        }

    }
}
