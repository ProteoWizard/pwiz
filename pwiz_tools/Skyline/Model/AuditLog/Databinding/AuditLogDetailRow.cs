/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class AuditLogDetailRow : SkylineObject
    {
        private readonly int _detailIndex;

        public AuditLogDetailRow(AuditLogRow row, int index) : base(row.DataSchema)
        {
            AuditLogRow = row;
            _detailIndex = index;
        }

        public AuditLogRow AuditLogRow { get; private set; }

        [Format(Width = 512)]
        public string AllInfoMessage
        {
            get { return AuditLogRow.GetEntry().AllInfo[_detailIndex].ToString(); }
        }

        public string Reason
        {
            get { return AuditLogRow.GetEntry().AllInfo[_detailIndex].Reason; }
            set
            {
                var entry = AuditLogRow.GetEntry();
                var infoCopy = entry.AllInfo.ToArray();

                infoCopy[_detailIndex] = entry.AllInfo[_detailIndex].ChangeReason(value);
                entry = entry.ChangeAllInfo(infoCopy);

                if (entry.AllInfo.Count == 1)
                    entry = entry.ChangeReason(value);

                ModifyDocument(EditDescription.SetColumn("Reason", value), // Not L10N
                    doc => AuditLogRow.ChangeEntry(doc, entry));
            }
        }

        public override string ToString()
        {
            return TextUtil.SpaceSeparate(AllInfoMessage, Reason);
        }
    }
}
