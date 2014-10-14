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

namespace pwiz.Skyline.Model.Lib.BlibData
{
    public class DbLibInfo
    {
        public const int INITIAL_LIBRARY_REVISION = 1;
        public const int SCHEMA_VERSION_CURRENT = 3; 
        public virtual string LibLSID { get; set; }
        public virtual string CreateTime { get; set; }
        public virtual int NumSpecs { get; set; }
        /// <summary>
        /// Revision number of the library.  Libraries start at revision 1,
        /// and that number gets increased if more stuff is added to the library.
        /// </summary>
        public virtual int MajorVersion { get; set; }
        /// <summary>
        /// Schema version of the library:
        /// Version 1 added the "RetentionTimes" table.
        /// Version 2 added the ion mobility columns to RetentionTimes 
        /// and, also for redundant libraries, to the RefSpectra table.
        /// Version 3 adds ion mobility high energy drift time offset
        /// </summary>
        public virtual int MinorVersion { get; set; }
    }
}
