/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util.Extensions
{
    public static class UtilProcess
    {
        public static void RunProcess(this ProcessStartInfo psi, IProgressMonitor progress, ref ProgressStatus status)
        {
            psi.RunProcess(null, null, progress, ref status);
        }

        public static void RunProcess(this ProcessStartInfo psi, string stdin)
        {
            var statusTemp = new ProgressStatus(string.Empty);
            psi.RunProcess(stdin, null, null, ref statusTemp);
        }

        public static void RunProcess(this ProcessStartInfo psi, string stdin, string messagePrefix, IProgressMonitor progress, ref ProgressStatus status)
        {
            var processRunner = new ProcessRunner
                                    {
                                        StatusPrefix = messagePrefix,
                                    };
            processRunner.Run(psi, stdin, progress, ref status);
        }
    }
}
