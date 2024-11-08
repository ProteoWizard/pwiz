/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class ReintegrateDlgSettings : AuditLogOperationSettings<ReintegrateDlgSettings>,
        IAuditLogComparable
    {
        public override MessageInfo MessageInfo
        {
            get
            {
                return new MessageInfo(MessageType.reintegrated_peaks, SrmDocument.DOCUMENT_TYPE.none,
                    PeakScoringModel.Name);
            }
        }

        public ReintegrateDlgSettings(IPeakScoringModel peakScoringModel, bool reintegrateAll,
            bool reintegrateQCutoff,
            double? cutoff, bool overwriteManualIntegration)
        {
            PeakScoringModel = peakScoringModel;
            ReintegrateAll = reintegrateAll;
            ReintegrateQCutoff = reintegrateQCutoff;
            Cutoff = cutoff;
            OverwriteManualIntegration = overwriteManualIntegration;
        }

        [TrackChildren] public IPeakScoringModel PeakScoringModel { get; private set; }

        [Track] public bool ReintegrateAll { get; private set; }
        [Track] public bool ReintegrateQCutoff { get; private set; }
        [Track] public double? Cutoff { get; private set; }
        [Track] public bool OverwriteManualIntegration { get; private set; }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return new ReintegrateDlgSettings(null, false, false, null, false);
        }
    }
}