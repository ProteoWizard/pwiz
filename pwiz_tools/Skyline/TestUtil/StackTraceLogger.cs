/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Text;
using pwiz.Skyline;
using pwiz.Skyline.Model;


namespace pwiz.SkylineTestUtil
{

    /// <summary>
    /// Class which writes the current stack trace to console, unless the
    /// stack trace is found to contain an optional filter string.
    /// </summary>
    public abstract class StackTraceLogger
    {
        private readonly string _filterText;

        protected StackTraceLogger(string filterText)
        {
            _filterText = filterText; // Don't log if stack trace includes this text
        }

        protected void LogStack(Func<string> logMessage)
        {
            var stackTrace = new StackTrace(1, true).ToString();
            if (string.IsNullOrEmpty(_filterText) || !stackTrace.Contains(_filterText))
            {
                Console.WriteLine(logMessage());
                Console.WriteLine(stackTrace);
            }
        }
    }


    /// <summary>
    /// Implementation of StackTraceLogger that logs any document change due to SkylineWindow.SetDocument()
    /// during lifetime of object.
    /// </summary>
    public class DocChangeLogger : StackTraceLogger, IDisposable
    {
        public DocChangeLogger(string filterText = null) : base(filterText)
        {
            Program.MainWindow.LogChange = LogChange; // Invoke LogChange function on every call to SkylineWindow.SetDocument()
        }
        
        private void LogChange(SrmDocument docNew, SrmDocument docOriginal)
        {
            LogStack(() => LogMessage(docNew));
        }

        private static string LogMessage(SrmDocument doc)
        {
            var sb = new StringBuilder();
            sb.Append(string.Format(@"Setting document revision {0}", doc.RevisionIndex));
            if (!doc.IsLoaded)
            {
                foreach (var desc in doc.NonLoadedStateDescriptions)
                    sb.AppendLine().Append(desc);
            }
            return sb.ToString();
        }
        
        public void Dispose()
        {
            Program.MainWindow.LogChange = null;
        }
    }


    /// <summary>
    /// Implementation of DocChangeLogger that explicitly avoids logging document changes
    /// associated with ImportFasta. The idea being that when you surround code with this
    /// in a using() block, you'll get nothing in the log if the only doc change that happens
    /// is due to ImportFasta. Any unexpected changes get logged. (This was used to figure
    /// out a race condition where ProteinMetadataBackgroundLoader might or might not
    /// complete in time to bump the document revision number, causing sporadic test failures -
    /// see UniquePeptidesDialogTest.cs for an example).
    /// </summary>
    public class ImportFastaDocChangeLogger : DocChangeLogger
    {
        public ImportFastaDocChangeLogger() :
            base("SkylineWindow.ImportFasta") // Do not log any stack traces that mention SkylineWindow.ImportFasta
        {
        }
    }

}
