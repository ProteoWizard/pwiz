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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class WorkspaceVersion
    {
        public int ChromatogramPeakVersion { get; private set; }
        public int EnrichmentVersion { get; private set; }
        public int MassVersion { get; private set; }
        public WorkspaceVersion IncChromatogramPeakVersion()
        {
            return new WorkspaceVersion
                       {
                           ChromatogramPeakVersion = ChromatogramPeakVersion + 1,
                           EnrichmentVersion = EnrichmentVersion,
                           MassVersion = MassVersion,
                       };
        }
        public WorkspaceVersion IncEnrichmentVersion()
        {
            return new WorkspaceVersion
                       {
                           ChromatogramPeakVersion = ChromatogramPeakVersion,
                           EnrichmentVersion = EnrichmentVersion + 1,
                           MassVersion = MassVersion
                       };
        }
        public WorkspaceVersion IncMassVersion()
        {
            return new WorkspaceVersion
                       {
                           ChromatogramPeakVersion = ChromatogramPeakVersion,
                           EnrichmentVersion = EnrichmentVersion,
                           MassVersion = MassVersion + 1,
                       };
        }

        public bool Equals(WorkspaceVersion other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.ChromatogramPeakVersion == ChromatogramPeakVersion 
                && other.EnrichmentVersion == EnrichmentVersion 
                && other.MassVersion == MassVersion;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (WorkspaceVersion)) return false;
            return Equals((WorkspaceVersion) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = ChromatogramPeakVersion;
                result = (result*397) ^ EnrichmentVersion;
                result = (result*397) ^ MassVersion;
                return result;
            }
        }
        public bool ChromatogramsValid(WorkspaceVersion currentVersion)
        {
            return MassVersion >= currentVersion.MassVersion;
        }
        public bool PeaksValid(WorkspaceVersion currentVersion)
        {
            return ChromatogramsValid(currentVersion) &&
                   ChromatogramPeakVersion >= currentVersion.ChromatogramPeakVersion;
        }
        public bool DistributionsValid(WorkspaceVersion currentVersion)
        {
            return PeaksValid(currentVersion) 
                && EnrichmentVersion >= currentVersion.EnrichmentVersion;
        }
    }
}
