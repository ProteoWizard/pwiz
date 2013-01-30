/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.Diagnostics.Contracts;
using pwiz.Common.Collections;

namespace pwiz.Topograph.Model.Data
{
    [Pure]
    public class WorkspaceData
    {
        public WorkspaceData()
        {
        }

        public WorkspaceData(WorkspaceData workspaceData)
        {
            DbWorkspaceId = workspaceData.DbWorkspaceId;
            LastChangeLogId = workspaceData.LastChangeLogId;
            Peptides = workspaceData.Peptides;
            MsDataFiles = workspaceData.MsDataFiles;
            PeptideAnalyses = workspaceData.PeptideAnalyses;
            TracerDefs = workspaceData.TracerDefs;
            Settings = workspaceData.Settings;
            Modifications = workspaceData.Modifications;
        }

        public long? DbWorkspaceId { get; private set; }
        public WorkspaceData SetDbWorkspaceId(long? value)
        {
            return new WorkspaceData(this){DbWorkspaceId = value};
        }
        public long? LastChangeLogId { get; private set; }
        public WorkspaceData SetLastChangeLogId(long? value)
        {
            return new WorkspaceData(this){LastChangeLogId = value};
        }
        public ImmutableSortedList<long, PeptideData> Peptides { get; private set; }
        public WorkspaceData SetPeptides(ImmutableSortedList<long, PeptideData> value)
        {
            return new WorkspaceData(this){Peptides = value};
        }
        public ImmutableSortedList<long, MsDataFileData> MsDataFiles { get; private set; }
        public WorkspaceData SetMsDataFiles(ImmutableSortedList<long, MsDataFileData> value)
        {
            return new WorkspaceData(this){MsDataFiles = value};
        }
        public ImmutableSortedList<long, PeptideAnalysisData> PeptideAnalyses { get; private set; }
        public WorkspaceData SetPeptideAnalyses(ImmutableSortedList<long, PeptideAnalysisData> value)
        {
            return new WorkspaceData(this){PeptideAnalyses = value};
        }
        public ImmutableSortedList<string, TracerDefData> TracerDefs { get; private set; }
        public WorkspaceData SetTracerDefs(ImmutableSortedList<string, TracerDefData> value)
        {
            return new WorkspaceData(this){TracerDefs = value};
        }
        public ImmutableSortedList<string, string> Settings { get; private set; }
        public WorkspaceData SetSettings(ImmutableSortedList<string, string> value)
        {
            return new WorkspaceData(this){Settings = value};
        }
        public ImmutableSortedList<string, double> Modifications { get; private set; }
        public WorkspaceData SetModifications(ImmutableSortedList<string, double> value)
        {
            return new WorkspaceData(this){Modifications = value};
        }

        protected bool Equals(WorkspaceData other)
        {
            return DbWorkspaceId == other.DbWorkspaceId
                && LastChangeLogId == other.LastChangeLogId 
                && Equals(Peptides, other.Peptides) 
                && Equals(MsDataFiles, other.MsDataFiles) 
                && Equals(PeptideAnalyses, other.PeptideAnalyses)
                && Equals(TracerDefs, other.TracerDefs)
                && Equals(Settings, other.Settings)
                && Equals(Modifications, other.Modifications);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((WorkspaceData) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = DbWorkspaceId.GetHashCode();
                hashCode = (hashCode*397) ^ LastChangeLogId.GetHashCode();
                hashCode = (hashCode*397) ^ (Peptides != null ? Peptides.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (MsDataFiles != null ? MsDataFiles.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PeptideAnalyses != null ? PeptideAnalyses.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (TracerDefs != null ? TracerDefs.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Settings != null ? Settings.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Modifications != null ? Modifications.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
