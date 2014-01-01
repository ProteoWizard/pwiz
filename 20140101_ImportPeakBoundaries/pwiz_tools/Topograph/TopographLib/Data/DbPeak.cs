/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Data
{
    public class DbPeak : DbEntity<DbPeak>
    {
        public virtual DbPeptideFileAnalysis PeptideFileAnalysis { get; set; }
        public virtual int PeakIndex { get; set; }
        public virtual String Name { get; set; }
        public virtual TracerFormula TracerFormula { get { return TracerFormula.Parse(Name); } }
        public virtual double StartTime { get; set; }
        public virtual double EndTime { get; set; }
        public virtual double Width { get { return EndTime - StartTime; } }
        public virtual double Area { get; set; }
    }
}
