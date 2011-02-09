//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
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

        public void UpdateProgress (object sender, Parser.ParsingProgressEventArgs e)
        {
            if (e.ParsingException != null)
                throw new InvalidOperationException("Parsing error: make sure decoy prefixes match", e.ParsingException);

            if (ProgressUpdate == null)
                return;

            if (e.ParsedBytes == 0)
                stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var progressUpdate = new ProgressUpdateEventArgs();

            progressUpdate.Total = 1000;
            progressUpdate.Current = e.TotalBytes == 0 ? 0 : Math.Min(progressUpdate.Total, (int) Math.Round((double) e.ParsedBytes / e.TotalBytes * 1000.0));
            double progressRate = stopwatch.Elapsed.TotalSeconds > 0 ? e.ParsedBytes / stopwatch.Elapsed.TotalSeconds : 0;
            long bytesRemaining = e.TotalBytes - e.ParsedBytes;
            TimeSpan timeRemaining = progressRate == 0 ? TimeSpan.Zero
                                                       : TimeSpan.FromSeconds(bytesRemaining / progressRate);
            progressUpdate.Message = String.Format("{0} ({1}/{2}) - {3} per second, {4}h{5}m{6}s remaining",
                                                   e.ParsingStage,
                                                   Util.GetFileSizeByteString(e.ParsedBytes),
                                                   Util.GetFileSizeByteString(e.TotalBytes),
                                                   Util.GetFileSizeByteString((long) progressRate),
                                                   timeRemaining.Hours,
                                                   timeRemaining.Minutes,
                                                   timeRemaining.Seconds);

            ProgressUpdate(this, progressUpdate);
            e.Cancel = progressUpdate.Cancel;
        }

        public void UpdateProgress (object sender, Merger.MergingProgressEventArgs e)
        {
            if (e.MergingException != null)
                throw new InvalidOperationException("parsing error", e.MergingException);

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
                                                   Math.Round(progressRate),
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
    }
}
