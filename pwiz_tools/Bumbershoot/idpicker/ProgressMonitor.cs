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
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Stopwatch = System.Diagnostics.Stopwatch;
using IDPicker.DataModel;
using pwiz.CLI.util;

namespace IDPicker
{
    public class ProgressUpdateEventArgs : CancelEventArgs
    {
        public string Message {get;set;}
        public int Current {get;set;}
        public int Total {get;set;}
    }

    public class ProgressMonitor
    {
        public event EventHandler<ProgressUpdateEventArgs> ProgressUpdate;

        private Stopwatch stopwatch = new Stopwatch();

        /*public void UpdateProgress (object sender, IterationEventArgs e)
        {
            if (ProgressUpdate == null)
                return;

            int iteration = e.IterationIndex + 1;

            if (iteration == 1)
                stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var progressUpdate = new ProgressUpdateEventArgs();

            if (e.IterationCount > 0)
            {
                progressUpdate.Total = 1000;
                progressUpdate.Current = e.IterationCount == 0 ? 0 : Math.Min(progressUpdate.Total, (int) Math.Round((double) iteration / e.IterationCount * 1000.0));
                double progressRate = stopwatch.Elapsed.TotalSeconds > 0 ? iteration / stopwatch.Elapsed.TotalSeconds : 0;
                long iterationsRemaining = e.IterationCount - iteration;
                TimeSpan timeRemaining = progressRate == 0 ? TimeSpan.Zero
                                                           : TimeSpan.FromSeconds(iterationsRemaining / progressRate);
                progressUpdate.Message = String.Format("{0} ({1}/{2}) - {3} per second, {4}h{5}m{6}s remaining",
                                                       e.Message,
                                                       iteration,
                                                       e.IterationCount,
                                                       (long) progressRate,
                                                       timeRemaining.Hours,
                                                       timeRemaining.Minutes,
                                                       timeRemaining.Seconds);
            }
            else
            {
                progressUpdate.Total = 0;
                progressUpdate.Current = iteration;
                progressUpdate.Message = String.Format("{0} ({1})",
                                                       e.Message,
                                                       iteration);
            }

            ProgressUpdate(this, progressUpdate);
            e.Cancel = progressUpdate.Cancel;
        }*/

        public void UpdateProgress (object sender, MergerWrapper.MergingProgressEventArgs e)
        {
            if (e.MergingException != null)
                Program.HandleException(e.MergingException);

            if (ProgressUpdate == null)
                return;

            if (e.MergedFiles == 0)
                stopwatch = Stopwatch.StartNew();

            var progressUpdate = new ProgressUpdateEventArgs();

            progressUpdate.Total = e.TotalFiles;
            progressUpdate.Current = e.MergedFiles;
            double progressRate = stopwatch.Elapsed.TotalSeconds > 0 ? e.MergedFiles / stopwatch.Elapsed.TotalSeconds : 0;
            long bytesRemaining = e.TotalFiles - e.MergedFiles;
            TimeSpan timeRemaining = progressRate == 0 ? TimeSpan.Zero
                                                       : TimeSpan.FromSeconds(bytesRemaining / progressRate);
            progressUpdate.Message = String.Format("Merging results... ({0}/{1}) - {2} per second, {3}h{4}m{5}s remaining",
                                                   e.MergedFiles,
                                                   e.TotalFiles,
                                                   Math.Round(progressRate, 1),
                                                   timeRemaining.Hours,
                                                   timeRemaining.Minutes,
                                                   timeRemaining.Seconds);

            ProgressUpdate(this, progressUpdate);
            e.Cancel = progressUpdate.Cancel;
        }

        public void UpdateProgress (object sender, Qonverter.QonversionProgressEventArgs e)
        {
            if (ProgressUpdate == null)
                return;

            var progressUpdate = new ProgressUpdateEventArgs();

            progressUpdate.Total = e.TotalAnalyses;
            progressUpdate.Current = e.QonvertedAnalyses;

            progressUpdate.Message = String.Format("Calculating Q values... ({2}: {0}/{1})",
                                                   e.QonvertedAnalyses,
                                                   e.TotalAnalyses,
                                                   e.Message);

            ProgressUpdate(this, progressUpdate);
            e.Cancel = progressUpdate.Cancel;
        }

        public void UpdateProgress (object sender, DataFilter.FilteringProgressEventArgs e)
        {
            var progressUpdate = new ProgressUpdateEventArgs();

            progressUpdate.Total = e.TotalFilters;
            progressUpdate.Current = e.CompletedFilters;

            progressUpdate.Message = String.Format("{0} ({1}/{2})",
                                                   e.FilteringStage,
                                                   e.CompletedFilters,
                                                   e.TotalFilters);

            ProgressUpdate(this, progressUpdate);
            e.Cancel = progressUpdate.Cancel;
        }

        public void UpdateProgress(object sender, Util.PrecacheProgressUpdateEventArgs e)
        {
            if (ProgressUpdate == null)
                return;

            if (e.PercentComplete == 0)
                stopwatch = Stopwatch.StartNew();

            var progressUpdate = new ProgressUpdateEventArgs { Total = 1000 };

            progressUpdate.Current = (int) Math.Round(e.PercentComplete * 1000);
            double progressRate = stopwatch.Elapsed.TotalSeconds > 0 ? progressUpdate.Current / stopwatch.Elapsed.TotalSeconds : 0;
            long bytesRemaining = progressUpdate.Total - progressUpdate.Current;
            TimeSpan timeRemaining = progressRate == 0 ? TimeSpan.Zero
                                                       : TimeSpan.FromSeconds(bytesRemaining / progressRate);
            progressUpdate.Message = String.Format("Precaching idpDB... {0}m{1}s remaining",
                                                   timeRemaining.Minutes,
                                                   timeRemaining.Seconds);

            ProgressUpdate(this, progressUpdate);
            e.Cancel = progressUpdate.Cancel;
        }
    }
}
