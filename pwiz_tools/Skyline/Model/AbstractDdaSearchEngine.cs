using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model
{
    public abstract class AbstractDdaSearchEngine
    {
        public abstract string[] FragmentIons { get; }
        public abstract string EngineName { get; }
        public abstract Bitmap SearchEngineLogo { get; }
        protected string[] SpectrumFileNames { get; set; }
        protected string[] FastaFileNames { get; set; }

        public abstract void SetPrecursorMassTolerance(double mass, string unit);
        public abstract void SetFragmentIonMassTolerance(double mass, string unit);
        public abstract void SetFragmentIons(string ions);
        public abstract void SetEnzyme(Enzyme enzyme, int maxMissedCleavages);

        public delegate void NotificationEventHandler(object sender, MessageEventArgs e);
        public abstract event NotificationEventHandler SearchProgessChanged;

        public class MessageEventArgs : EventArgs
        {

            public string Message { get; set; }
        }

        public abstract bool Run(CancellationTokenSource cancelToken);

        public void SetSpectrumFiles(string[] searchFilenames)
        {
            SpectrumFileNames = searchFilenames;
        }


        public void SetFastaFiles(string fastFile)
        {
            //todo check multi-fasta support
            FastaFileNames = new string[] {fastFile};
        }

        public abstract void SaveModifications(Dictionary<StaticMod, bool> fixedAndVariableModifs);

    }
}
