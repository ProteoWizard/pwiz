/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.CLI.msdata;

namespace pwiz.ProteowizardWrapper
{
    public class MsDataFileInfo
    {
        /// <summary>
        /// Open a file with ProteoWizard, run a generic predicate on it, and close the file. Useful for quick checks on the file-level metadata.
        /// (keeping the MSData's file-level containers around but closing the file would require a lot of refactoring in the CLI wrapper)
        /// </summary>
        public static T RunPredicate<T>(string filepath, Func<MSData, T> predicate, int sampleIndex = 0)
        {
            using (var msd = new MSData())
            {
                FULL_READER_LIST.read(filepath, msd, sampleIndex);
                return predicate(msd);
            }
        }

        private static readonly ReaderList FULL_READER_LIST = ReaderList.FullReaderList;
    }
}