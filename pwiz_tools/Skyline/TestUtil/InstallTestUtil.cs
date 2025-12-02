/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Cursor (Claude Sonnet 4) <cursor .at. anysphere.co>
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.Diagnostics;
using System.IO;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Test implementation of ISkylineProcessRunnerWrapper for unit testing
    /// </summary>
    public class TestSkylineProcessRunner : ISkylineProcessRunnerWrapper
    {
        public bool ConnectSuccess { get; set; }
        public bool UserOkRunAsAdministrator { get { return _userOkRunAsAdministrator; } set { _userOkRunAsAdministrator = value; } }
        private bool _userOkRunAsAdministrator = true; 
        public int ExitCode { get; set; }
        public string stringToWriteToWriter { get; set; }
        
        public int RunProcess(string arguments, bool runAsAdministrator, TextWriter writer,  bool createNoWindow = false, CancellationToken cancellationToken = default)
        {
            if (!UserOkRunAsAdministrator)
            {
                throw new UserMessageException(Resources.TestSkylineProcessRunner_RunProcess_The_operation_was_canceled_by_the_user_);
            }
            if (!ConnectSuccess)
                throw new IOException(Resources.TestNamedPipeProcessRunner_RunProcess_Error_running_process);
            if (!string.IsNullOrEmpty(stringToWriteToWriter))
                writer.WriteLine(stringToWriteToWriter);
            return ExitCode;
        }
    }

    /// <summary>
    /// Test implementation of IRunProcess for unit testing
    /// </summary>
    public class TestRunProcess : IRunProcess
    {
        public int ExitCode { get; set; }

        public int RunProcess(Process process)
        {
            return ExitCode;
        }
    }
}
