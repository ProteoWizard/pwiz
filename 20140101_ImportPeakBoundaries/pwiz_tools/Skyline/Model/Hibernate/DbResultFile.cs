/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Hibernate
{
    [DatabindingTable(RootTable = typeof(ResultFile))]
    public class DbResultFile : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbResultFile); }
        }
        public virtual DbReplicate Replicate { get; set; }
        public virtual string FileName { get; set; }
        public virtual string SampleName { get; set; }
        public virtual DateTime? ModifiedTime { get; set; }
        public virtual DateTime? AcquiredTime { get; set; }
    }
}
