/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.Skyline.Controls.Spectra
{
    public class PrecursorClass : SkylineObject, ILinkValue
    {
        private TransitionGroupDocNode _transitionGroupDocNode;
        public PrecursorClass(Peptide peptide, TransitionGroupDocNode transitionGroupDocNode)
        {
            Peptide = peptide;
            _transitionGroupDocNode = transitionGroupDocNode;
        }
        public Peptide Peptide { get; }
        protected override SkylineDataSchema GetDataSchema()
        {
            return Peptide.DataSchema;
        }
        public string PrecursorAdduct
        {
            get { return _transitionGroupDocNode.PrecursorAdduct.ToString(); }
        }
        public IsotopeLabelType IsotopeLabelType
        {
            get { return _transitionGroupDocNode.LabelType; }
        }

        private bool DoesAdductMatter()
        {
            return Peptide.DocNode.TransitionGroups.Any(tg => !Equals(_transitionGroupDocNode.PrecursorAdduct, tg.PrecursorAdduct));
        }

        private bool DoesLabelTypeMatter()
        {
            return Peptide.DocNode.TransitionGroups.Any(tg => !Equals(_transitionGroupDocNode.LabelType, tg.LabelType));
        }

        public override string ToString()
        {
            var result = Peptide.ToString();
            if (DoesAdductMatter())
            {
                result += _transitionGroupDocNode.PrecursorAdduct.AsFormulaOrSigns();
            }
            if (DoesLabelTypeMatter())
            {
                result += _transitionGroupDocNode.TransitionGroup.LabelTypeText;
            }
            return result;
        }

        EventHandler ILinkValue.ClickEventHandler => LinkValueOnClick;

        object ILinkValue.Value => this;

        public void LinkValueOnClick(object sender, EventArgs eventArgs)
        {
            var skylineWindow = DataSchema.SkylineWindow;
            if (null == skylineWindow)
            {
                return;
            }

            var identityPathsToSelect = GetIdentityPaths().ToList();
            if (identityPathsToSelect.Count > 0)
            {
                skylineWindow.SequenceTree.SelectedPaths = identityPathsToSelect;
            }
        }

        public IEnumerable<IdentityPath> GetIdentityPaths()
        {
            var peptideDocNode = Peptide.DocNode;
            var matchingPrecursors = peptideDocNode.TransitionGroups.Where(tg =>
                Equals(tg.PrecursorAdduct, _transitionGroupDocNode.PrecursorAdduct) &&
                Equals(tg.LabelType, _transitionGroupDocNode.LabelType)).ToList();
            if (matchingPrecursors.Count == peptideDocNode.TransitionGroupCount)
            {
                return new[] { Peptide.IdentityPath };
            }
            return matchingPrecursors.Select(tg => new IdentityPath(Peptide.IdentityPath, tg.TransitionGroup));
        }

        public TransitionGroupDocNode GetExampleTransitionGroupDocNode()
        {
            return _transitionGroupDocNode;
        }
    }
}
