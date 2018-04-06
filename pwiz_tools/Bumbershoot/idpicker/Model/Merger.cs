//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SQLite;
using System.ComponentModel;
using System.Threading;
using System.IO;
using System.Security.AccessControl;

namespace IDPicker.DataModel
{
    public class MergerWrapper
    {
        #region Events
        public event EventHandler<MergingProgressEventArgs> MergingProgress;
        #endregion

        #region Event arguments
        public class MergingProgressEventArgs : CancelEventArgs
        {
            public int MergedFiles { get; set; }
            public int TotalFiles { get; set; }

            public Exception MergingException { get; set; }
        }
        #endregion

        /// <summary>
        /// Merge one or more idpDBs into a target idpDB.
        /// </summary>
        public MergerWrapper (string mergeTargetFilepath, IEnumerable<string> mergeSourceFilepaths)
        {
            this.mergeTargetFilepath = mergeTargetFilepath;
            this.mergeSourceFilepaths = mergeSourceFilepaths;
            totalSourceFiles = mergeSourceFilepaths.Count();
        }

        /// <summary>
        /// Merge an idpDB connection (either file or in-memory) to a target idpDB file.
        /// </summary>
        public MergerWrapper(string mergeTargetFilepath, SQLiteConnection mergeSourceConnection)
        {
            this.mergeTargetFilepath = mergeTargetFilepath;
            this.mergeSourceConnection = mergeSourceConnection;
        }

        public void Start ()
        {
            mergeException = null;
            var workerThread = new Thread(merge);
            workerThread.Start();

            while (workerThread.IsAlive)
            {
                workerThread.Join(100);
                System.Windows.Forms.Application.DoEvents();
            }

            if (mergeException != null)
                throw mergeException;
        }

        class IterationListenerProxy : pwiz.CLI.util.IterationListener
        {
            public MergerWrapper merger { get; set; }

            public override Status update(UpdateMessage updateMessage)
            {
                var result = merger.OnMergingProgress(null, updateMessage.iterationIndex, updateMessage.iterationCount);
                return result ? Status.Cancel : Status.Ok;
            }
        }

        public void merge()
        {
            try
            {
                OnMergingProgress(null, 0, mergeSourceFilepaths.Count()); // start the clock

                var ilr = new pwiz.CLI.util.IterationListenerRegistry();
                ilr.addListener(new IterationListenerProxy { merger = this }, 1);
                Merger.Merge(mergeTargetFilepath, mergeSourceFilepaths.ToList(), 8, ilr);
            }
            catch (Exception e)
            {
                OnMergingProgress(e, 1, 1);
            }
        }

        bool OnMergingProgress (Exception ex, int mergedFiles, int totalFiles)
        {
            if (MergingProgress != null)
            {
                var eventArgs = new MergingProgressEventArgs()
                {
                    MergedFiles = mergedFiles,
                    TotalFiles = totalFiles,
                    MergingException = ex
                };
                MergingProgress(this, eventArgs);
                if (ex != null)
                    mergeException = ex;
                return eventArgs.Cancel;
            }
            else if (ex != null)
                throw ex;

            return false;
        }

        int totalSourceFiles;
        string mergeTargetFilepath;
        IEnumerable<string> mergeSourceFilepaths;
        SQLiteConnection mergeSourceConnection;
        Exception mergeException;
    }
}