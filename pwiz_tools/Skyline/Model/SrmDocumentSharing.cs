using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Ionic.Zip;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Proteome;

namespace pwiz.Skyline.Model
{
    internal class SrmDocumentSharing
    {
        public const string EXT = "zip";

        public SrmDocumentSharing(SrmDocument document, string documentPath, string sharedPath, bool completeSharing)
        {
            Document = document;
            DocumentPath = documentPath;
            SharedPath = sharedPath;
            CompleteSharing = completeSharing;
        }

        public SrmDocument Document { get; private set; }
        public string DocumentPath { get; private set; }
        public string SharedPath { get; private set; }
        public bool CompleteSharing { get; private set; }

        private ILongWaitBroker WaitBroker { get; set; }
        private int CountEntries { get; set; }

        private string DefaultMessage
        {
            get
            {
                return string.Format("Compressing files for sharing archive {0}",
                                     Path.GetFileName(SharedPath));
            }
        }

        public void Share(ILongWaitBroker waitBroker)
        {
            WaitBroker = waitBroker;
            WaitBroker.ProgressValue = 0;
            WaitBroker.Message = DefaultMessage;

            using (var zip = new ZipFile())
            {
                if (CompleteSharing)
                    ShareComplete(zip);
                else
                    ShareMinimal(zip);
            }
        }

        private void ShareComplete(ZipFile zip)
        {
            // If complete sharing, just zip up existing files
            var pepSettings = Document.Settings.PeptideSettings;
            if (Document.Settings.HasBackgroundProteome)
                zip.AddFile(pepSettings.BackgroundProteome.BackgroundProteomeSpec.DatabasePath, "");
            foreach (var librarySpec in pepSettings.Libraries.LibrarySpecs)
                zip.AddFile(librarySpec.FilePath, "");
            ShareDataAndView(zip);
            zip.AddFile(DocumentPath, "");
            Save(zip);
        }

        private void ShareMinimal(ZipFile zip)
        {
            TemporaryDirectory tempDir = null;
            try
            {
                var docOriginal = Document;
                if (Document.Settings.HasBackgroundProteome)
                {
                    // Remove any background proteome reference
                    Document = Document.ChangeSettings(Document.Settings.ChangePeptideSettings(
                        set => set.ChangeBackgroundProteome(BackgroundProteome.NONE)));
                }
                if (Document.Settings.HasLibraries)
                {
                    // Minimize all libraries in a temporary directory, and add them
                    tempDir = new TemporaryDirectory();
                    Document = BlibDb.MinimizeLibraries(Document, tempDir.DirPath, Path.GetFileNameWithoutExtension(DocumentPath));
                    foreach (var librarySpec in Document.Settings.PeptideSettings.Libraries.LibrarySpecs)
                        zip.AddFile(Path.Combine(tempDir.DirPath, Path.GetFileName(librarySpec.FilePath)), "");
                }

                ShareDataAndView(zip);
                if (ReferenceEquals(docOriginal, Document))
                    zip.AddFile(DocumentPath, "");
                else
                {
                    // If minimizing changed the document, then serialize and archive the new document
                    var stringWriter = new XmlStringWriter();
                    using (var writer = new XmlTextWriter(stringWriter) { Formatting = Formatting.Indented })
                    {
                        XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                        ser.Serialize(writer, Document);
                        zip.AddEntry(Path.GetFileName(DocumentPath), "", stringWriter.ToString(), Encoding.UTF8);
                    }
                }
                Save(zip);
            }
            finally
            {
                if (tempDir != null)
                {
                    try
                    {
                        tempDir.Dispose();
                    }
                    catch (IOException x)
                    {
                        throw new IOException(string.Format("Failure removing temporary directory {0}.\n{1}", tempDir.DirPath, x.Message));
                    }
                }
            }
        }

        private void ShareDataAndView(ZipFile zip)
        {
            string pathCache = ChromatogramCache.FinalPathForName(DocumentPath, null);
            if (File.Exists(pathCache))
                zip.AddFile(pathCache, "");
            string viewPath = SkylineWindow.GetViewFile(DocumentPath);
            if (File.Exists(viewPath))
                zip.AddFile(viewPath, "");
        }

        private void Save(ZipFile zip)
        {
            CountEntries = zip.Entries.Count;

            using (var saver = new FileSaver(SharedPath))
            {
                zip.SaveProgress += SrmDocumentSharing_SaveProgress;
                zip.Save(saver.SafeName);
                WaitBroker.ProgressValue = 100;
                saver.Commit();
            }
        }

        private void SrmDocumentSharing_SaveProgress(object sender, SaveProgressEventArgs e)
        {
            if (WaitBroker != null)
            {
                double percentCompressed = (e.TotalBytesToTransfer > 0 ?
                    1.0 * e.BytesTransferred / e.TotalBytesToTransfer : 0);
                int progressValue = (int)Math.Round((e.EntriesSaved + percentCompressed) * 100 / CountEntries);

                if (progressValue != WaitBroker.ProgressValue)
                {
                    WaitBroker.ProgressValue = progressValue;
                    WaitBroker.Message = (e.CurrentEntry != null ?
                        string.Format("Compressing {0}", e.CurrentEntry.FileName) : DefaultMessage);
                }
            }
        }
    }
}
