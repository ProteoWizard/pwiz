/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Extracted from GraphSpectrum to remove UI dependency from Model.
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
using System.Collections;
using pwiz.CommonMsData;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    public sealed class SpectrumDisplayInfo : IComparable<SpectrumDisplayInfo>
    {
        public SpectrumDisplayInfo(SpectrumInfo spectrumInfo, TransitionGroupDocNode precursor, double? retentionTime = null,
            Adduct adduct = null)
        {
            SpectrumInfo = spectrumInfo;
            Precursor = precursor;
            IsBest = true;
            RetentionTime = retentionTime;
            Adduct = adduct ?? precursor?.PrecursorAdduct;
        }

        public SpectrumDisplayInfo(SpectrumInfo spectrumInfo, TransitionGroupDocNode precursor, string replicateName,
            MsDataFileUri filePath, int fileOrder, double? retentionTime, bool isBest, Adduct adduct = null)
        {
            SpectrumInfo = spectrumInfo;
            Precursor = precursor;
            ReplicateName = replicateName;
            FilePath = filePath;
            FileOrder = fileOrder;
            RetentionTime = retentionTime;
            IsBest = isBest;
            Adduct = adduct ?? precursor?.PrecursorAdduct;
        }

        public SpectrumInfo SpectrumInfo { get; }
        public TransitionGroupDocNode Precursor { get; private set; }
        /// <summary>
        /// The adduct of the library precursor the spectrum identified, which may differ from that of
        /// <see cref="Precursor"/>, since all charges of a peptide are evidence for each of its precursors.
        /// </summary>
        public Adduct Adduct { get; private set; }

        public bool IsOtherCharge
        {
            get { return Precursor != null && Adduct != null && Adduct.AdductCharge != Precursor.PrecursorCharge; }
        }

        public bool IsOtherLabelType
        {
            get { return Precursor != null && !Equals(LabelType, Precursor.LabelType); }
        }
        public string Name { get { return SpectrumInfo.Name; } }
        public IsotopeLabelType LabelType { get { return SpectrumInfo.LabelType; } }
        public string ReplicateName { get; private set; }
        public bool IsReplicateUnique { get; set; }
        public MsDataFileUri FilePath { get; private set; }
        public string FileName { get { return FilePath.GetFileName(); } }
        public int FileOrder { get; private set; }
        public double? RetentionTime { get; private set; }
        public bool IsBest { get; private set; }

        public string Identity { get { return ToString(); } }

        public SpectrumPeaksInfo SpectrumPeaksInfo { get { return SpectrumInfo.SpectrumPeaksInfo; } }
        public LibraryChromGroup LoadChromatogramData() { return SpectrumInfo.ChromatogramData; }

        public int CompareTo(SpectrumDisplayInfo other)
        {
            if (other == null) return 1;
            int i = Comparer.Default.Compare(FileOrder, other.FileOrder);
            if (i == 0)
            {
                if (RetentionTime.HasValue && other.RetentionTime.HasValue)
                    i = Comparer.Default.Compare(RetentionTime.Value, other.RetentionTime);
                else if (RetentionTime.HasValue)
                    i = 1;
                else if (other.RetentionTime.HasValue)
                    i = -1;
                else
                    i = 0;
            }

            return i;
        }

        public override string ToString()
        {
            string text;
            if (IsBest)
                text = Name;
            else if (IsReplicateUnique)
                text = string.Format(@"{0} ({1:F02} min)", ReplicateName, RetentionTime);
            else
                text = string.Format(@"{0} - {1} ({2:F02} min)", ReplicateName, FileName, RetentionTime);

            // Name the charge and label type of a spectrum from another form of the precursor
            if (IsOtherCharge)
                text = TextUtil.SpaceSeparate(text, Transition.GetChargeIndicator(Adduct));
            if (IsOtherLabelType || (IsBest && !LabelType.IsLight))
                text = TextUtil.SpaceSeparate(text, String.Format(@"({0})", LabelType));
            return text;
        }
    }
}
