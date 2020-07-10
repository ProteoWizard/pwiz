/*
 * Original author: Viktoria Dorfer <viktoria.dorfer .at. fh-hagenberg.at>,
 *                  Bioinformatics Research Group, University of Applied Sciences Upper Austria
 *
 * Copyright 2020 University of Applied Sciences Upper Austria
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
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

        public abstract void SetModifications(IEnumerable<StaticMod> modifications, int maxVariableMods);
    }
}
