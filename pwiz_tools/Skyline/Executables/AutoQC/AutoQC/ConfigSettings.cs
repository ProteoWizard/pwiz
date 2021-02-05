﻿/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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

namespace AutoQC
{
    public interface IConfigSettings
    {
        bool IsSelected();
        void ValidateSettings();

        /// <summary>
        /// Returns the command-line arguments to be passed to SkylineRunner.
        /// </summary>
        /// <param name="importContext">Contains information about the results file we are importing</param>
        /// <param name="toPrint">True if the arguments will be logged</param>
        /// <returns></returns>
        string SkylineRunnerArgs(ImportContext importContext, bool toPrint = false);

        /// <summary>
        /// Returns information about a process that should be run before running SkylineRunner.
        /// </summary>
        /// <param name="importContext"></param>
        /// <returns></returns>
        ProcessInfo RunBefore(ImportContext importContext);

        /// <summary>
        /// Returns information about a process that should be run after running SkylineRunner.
        /// </summary>
        /// <param name="importContext"></param>
        /// <returns></returns>
        ProcessInfo RunAfter(ImportContext importContext);
    }
}