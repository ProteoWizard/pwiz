/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.ProteomeDatabase.API;

namespace pwiz.ProteomeDatabase.Util
{
    /// <summary>
    /// Holder of the file path to a proteome database.
    /// </summary>
    public class ProteomeDbPath
    {
        public ProteomeDbPath(string path)
        {
            FilePath = path;
        }

        public string FilePath { get; private set; }

        /// <summary>
        /// Opens a proteome database (creates a session factory, etc.).  The ProteomeDb
        /// returned by this method must be Dispose'd.
        /// </summary>
        public ProteomeDb OpenProteomeDb()
        {
            return ProteomeDb.OpenProteomeDb(FilePath);
        }
    }
}
