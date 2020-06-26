using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model
{
    public abstract class AbstractDdaSearchEngine
    {
        public abstract string[] FragmentIons { get; }
        public abstract string EngineName { get; }
        public abstract Bitmap SearchEngineLogo { get; }
        protected MsDataFileUri[] SpectrumFileNames { get; set; }
        protected string[] FastaFileNames { get; set; }

        public abstract void SetPrecursorMassTolerance(MzTolerance mzTolerance);
        public abstract void SetFragmentIonMassTolerance(MzTolerance mzTolerance);
        public abstract void SetFragmentIons(string ions);
        public abstract void SetEnzyme(Enzyme enzyme, int maxMissedCleavages);

        public delegate void NotificationEventHandler(object sender, MessageEventArgs e);
        public abstract event NotificationEventHandler SearchProgressChanged;

        public class MessageEventArgs : EventArgs
        {
            public string Message { get; set; }
        }

        public abstract bool Run(CancellationTokenSource cancelToken);

        public void SetSpectrumFiles(MsDataFileUri[] searchFilenames)
        {
            SpectrumFileNames = searchFilenames;
        }

        /// <summary>
        /// Returns the search result (e.g. pepXML, mzIdentML) filenpath corresponding with the given searchFilepath.
        /// </summary>
        /// <param name="searchFilepath">The raw data filepath (e.g. RAW, WIFF, mzML)</param>
        public string GetSearchResultFilepath(MsDataFileUri searchFilepath)
        {
            return Path.ChangeExtension(searchFilepath.GetFilePath(), @".mzid");
        }

        public void SetFastaFiles(string fastFile)
        {
            //todo check multi-fasta support
            FastaFileNames = new string[] {fastFile};
        }

        public abstract void SaveModifications(IList<StaticMod> modifications);
    }
}
