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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.replicate)]
    public class Replicate : SkylineObject, ILinkValue, IComparable
    {
        private static readonly ChromatogramSet EMPTY_CHROMATOGRAM_SET = (ChromatogramSet) new ChromatogramSet(
            XmlNamedElement.NAME_INTERNAL, new MsDataFileUri[0]).ChangeName(string.Empty);
        private readonly CachedValue<ChromatogramSet> _chromatogramSet;
        public Replicate(SkylineDataSchema dataSchema, int replicateIndex) : base(dataSchema)
        {
            ReplicateIndex = replicateIndex;
            _chromatogramSet = CachedValue.Create(DataSchema, FindChromatogramSet);
        }

        [Browsable(false)]
        public int ReplicateIndex { get; private set; }

        [Browsable(false)]
        public ChromatogramSet ChromatogramSet { get { return _chromatogramSet.Value; } }
        public void ChangeChromatogramSet(EditDescription editDescription, ChromatogramSet chromatogramSet)
        {
            ModifyDocument(editDescription, document =>
                {
                    var measuredResults = document.Settings.MeasuredResults;
                    var chromatograms = measuredResults.Chromatograms.ToArray();
                    chromatograms[ReplicateIndex] = chromatogramSet;
                    measuredResults = measuredResults.ChangeChromatograms(chromatograms);
                    return document.ChangeMeasuredResults(measuredResults);
                }
            );
        }

        [InvariantDisplayName("ReplicateName")]
        public string Name
        {
            get { return ChromatogramSet.Name; }
        }

        public override string ToString()
        {
            return Name;
        }

        private ChromatogramSet FindChromatogramSet()
        {
            var results = SrmDocument.Settings.MeasuredResults;
            if (results == null || results.Chromatograms.Count <= ReplicateIndex)
            {
                return EMPTY_CHROMATOGRAM_SET;
            }
            return results.Chromatograms[ReplicateIndex];
        }

        public override object GetAnnotation(AnnotationDef annotationDef)
        {
            return ChromatogramSet.Annotations.GetAnnotation(annotationDef);
        }

        public override void SetAnnotation(AnnotationDef annotationDef, object value)
        {
            ChangeChromatogramSet(EditDescription.SetAnnotation(annotationDef, value), 
                ChromatogramSet.ChangeAnnotations(ChromatogramSet.Annotations.ChangeAnnotation(annotationDef, value)));
        }

        private void LinkValueOnClick(object sender, EventArgs args)
        {
            var skylineWindow = DataSchema.SkylineWindow;
            if (null == skylineWindow)
            {
                return;
            }
            skylineWindow.SelectedResultsIndex = ReplicateIndex;
        }
        object ILinkValue.Value { get { return this; } }
        EventHandler ILinkValue.ClickEventHandler { get { return LinkValueOnClick; } }
        public int CompareTo(object o)
        {
            if (null == o)
            {
                return 1;
            }
            return ReplicateIndex.CompareTo(((Replicate) o).ReplicateIndex);
        }

        [Obsolete]
        public string ReplicatePath { get { return "/"; } } // Not L10N

        [HideWhen(AncestorOfType = typeof(ResultFile))]
        public IList<ResultFile> Files
        {
            get
            {
                return ChromatogramSet.MSDataFileInfos.Select(
                    chromFileInfo => new ResultFile(this, chromFileInfo.FileId, 0)).ToArray();
            }
        }

        [DataGridViewColumnType(typeof(SampleTypeDataGridViewColumn))]
        public SampleType SampleType
        {
            get
            {
                return ChromatogramSet.SampleType;
            }
            set
            {
                ChangeChromatogramSet(EditDescription.SetColumn("SampleType", value), // Not L10N
                    ChromatogramSet.ChangeSampleType(value));
            }
        }

        public double? AnalyteConcentration
        {
            get { return ChromatogramSet.AnalyteConcentration; }
            set
            {
                ChangeChromatogramSet(EditDescription.SetColumn("AnalyteConcentration", value), // Not L10N
                    ChromatogramSet.ChangeAnalyteConcentration(value));
            }
        }
    }
}
