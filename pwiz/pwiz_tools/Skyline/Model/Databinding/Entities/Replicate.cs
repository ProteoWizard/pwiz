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
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    // ReSharper disable LocalizableElement
    [AnnotationTarget(AnnotationDef.AnnotationTarget.replicate)]
    public class Replicate : SkylineObject, ILinkValue, IComparable
    {
        public Replicate(SkylineDataSchema dataSchema, int replicateIndex) : base(dataSchema)
        {
            ReplicateIndex = replicateIndex;
            ChromatogramSet = SrmDocument.Settings.MeasuredResults.Chromatograms[replicateIndex];
        }

        [Browsable(false)]
        public int ReplicateIndex { get; private set; }

        [Browsable(false)]
        public ChromatogramSet ChromatogramSet { get; private set; }
        public void ChangeChromatogramSet(ChromatogramSet chromatogramSet)
        {
            ModifyDocument(document =>
                {
                    var measuredResults = document.Settings.MeasuredResults;
                    var chromatograms = measuredResults.Chromatograms.ToArray();
                    chromatograms[ReplicateIndex] = chromatogramSet;
                    measuredResults = measuredResults.ChangeChromatograms(chromatograms);
                    return document.ChangeMeasuredResults(measuredResults);
                }
            );
        }

        [DisplayName("ReplicateName")]
        public string Name
        {
            get { return ChromatogramSet.Name; }
        }

        public override string ToString()
        {
            return Name;
        }

        protected override void OnDocumentChanged()
        {
            base.OnDocumentChanged();
            var results = SrmDocument.Settings.MeasuredResults;
            if (results == null || results.Chromatograms.Count <= ReplicateIndex)
            {
                return;
            }
            var newChromatogramSet = results.Chromatograms[ReplicateIndex];
            if (Equals(newChromatogramSet, ChromatogramSet))
            {
                return;
            }
            ChromatogramSet = newChromatogramSet;
            FirePropertyChanged(new PropertyChangedEventArgs(null));
        }

        public override object GetAnnotation(AnnotationDef annotationDef)
        {
            return ChromatogramSet.Annotations.GetAnnotation(annotationDef);
        }

        public override void SetAnnotation(AnnotationDef annotationDef, object value)
        {
            ChangeChromatogramSet(ChromatogramSet.ChangeAnnotations(ChromatogramSet.Annotations.ChangeAnnotation(annotationDef, value)));
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
        public string ReplicatePath { get { return "/"; } }

        [HideWhen(AncestorOfType = typeof(ResultFile))]
        public IList<ResultFile> Files
        {
            get
            {
                return ChromatogramSet.MSDataFileInfos.Select(
                    chromFileInfo => new ResultFile(this, chromFileInfo.FileId, 0)).ToArray();
            }
        }
    }
}
