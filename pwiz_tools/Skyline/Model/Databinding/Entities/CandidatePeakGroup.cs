/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [InvariantDisplayName(nameof(CandidatePeakGroup))]
    public class CandidatePeakGroup : SkylineObject, ILinkValue
    {
        private CandidatePeakGroupData _data;
        private PrecursorResult _precursorResult;
        public CandidatePeakGroup(PrecursorResult precursorResult, CandidatePeakGroupData data) : base(precursorResult.DataSchema)
        {
            _data = data;
            _precursorResult = precursorResult;
        }

        [Format(Formats.RETENTION_TIME)]
        public double PeakGroupRetentionTime
        {
            get
            {
                return _data.RetentionTime;
            }
        }

        [Format(Formats.RETENTION_TIME)]
        public double PeakGroupStartTime
        {
            get
            {
                return _data.MinStartTime;
            }
        }

        [Format(Formats.RETENTION_TIME)]
        public double PeakGroupEndTime
        {
            get
            {
                return _data.MaxEndTime;
            }
        }

        public bool Chosen
        {
            get { return _data.Chosen; }
            set
            {
                if (value == Chosen)
                {
                    return;
                }
                if (!value)
                {
                    ModifyDocument(
                        EditDescription.Message(_precursorResult.GetElementRef(),
                            Resources.CandidatePeakGroup_Chosen_Remove_Peak),
                        doc =>
                        {
                            foreach (var precursor in GetComparableGroup())
                            {
                                doc = RemovePeak(doc, precursor);
                            }

                            return doc;
                        });
                }
                else
                {
                    ModifyDocument(
                        EditDescription.Message(_precursorResult.GetElementRef(),
                            Resources.CandidatePeakGroup_Chosen_Choose_peak),
                        doc =>
                        {
                            foreach (var precursor in GetComparableGroup())
                            {
                                var retentionTime = GetChromPeakRetentionTime(precursor);
                                if (retentionTime.HasValue)
                                {
                                    doc = ChoosePeak(doc, precursor, retentionTime.Value);
                                }
                                else
                                {
                                    doc = RemovePeak(doc, precursor);
                                }
                            }

                            return doc;
                        });
                }
            }
        }

        /// <summary>
        /// Returns the retention time of one of the ChromPeak's in this peak group.
        /// </summary>
        private double? GetChromPeakRetentionTime(TransitionGroupDocNode transitionGroupDocNode)
        {
            if (!_data.PeakIndex.HasValue)
            {
                return null;
            }
            float tolerance = (float)SrmDocument.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var peptideDocNode = _precursorResult.Precursor.Peptide.DocNode;
            var chromatogramSet = _precursorResult.GetResultFile().Replicate.ChromatogramSet;
            var filePath = _precursorResult.GetResultFile().ChromFileInfo.FilePath;
            var optimizableRegression = _precursorResult.GetResultFile().Replicate.ChromatogramSet.OptimizationFunction;
            ChromatogramGroupInfo[] chromatogramGroupInfos = null;
            SrmDocument.Settings.MeasuredResults?.TryLoadChromatogram(chromatogramSet,
                peptideDocNode, transitionGroupDocNode, tolerance,
                out chromatogramGroupInfos);
            chromatogramGroupInfos = chromatogramGroupInfos ?? Array.Empty<ChromatogramGroupInfo>();
            var chromatogramGroupInfo = chromatogramGroupInfos.FirstOrDefault(info =>
                Equals(info.FilePath, filePath));
            if (chromatogramGroupInfo == null)
            {
                return null;
            }
            foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
            {
                var chromatogramInfo = chromatogramGroupInfo.GetTransitionInfo(transitionDocNode, tolerance, optimizableRegression);
                if (chromatogramInfo != null)
                {
                    ChromPeak peak;
                    try
                    {
                        peak = chromatogramInfo.GetPeak(_data.PeakIndex.Value);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (!peak.IsEmpty)
                    {
                        return peak.RetentionTime;
                    }
                }
            }

            return null;
        }

        public override string ToString()
        {
            return string.Format(Resources.CandidatePeakGroup_ToString___0___1__, 
                PeakGroupStartTime.ToString(Formats.RETENTION_TIME),
                PeakGroupEndTime.ToString(Formats.RETENTION_TIME));
        }

        public void LinkValueOnClick(object sender, EventArgs args)
        {
            var skylineWindow = DataSchema.SkylineWindow;
            if (null == skylineWindow)
            {
                return;
            }

            var precursorResult = _precursorResult;
            precursorResult.LinkValueOnClick(sender, args);
            var chromatogramGraph = skylineWindow.GetGraphChrom(precursorResult.GetResultFile().Replicate.Name);
            if (chromatogramGraph != null)
            {
                chromatogramGraph.ZoomToPeak(PeakGroupStartTime, PeakGroupEndTime);
            }
        }
        EventHandler ILinkValue.ClickEventHandler
        {
            get
            {
                return LinkValueOnClick;
            }
        }

        object ILinkValue.Value => this;

        public PeakGroupScore PeakScores
        {
            get { return _data.Score; }
        }

        public CandidatePeakGroupData GetCandidatePeakGroupData()
        {
            return _data;
        }

        private IEnumerable<TransitionGroupDocNode> GetComparableGroup()
        {
            var peptideDocNode = _precursorResult.Precursor.Peptide.DocNode;
            var precursorDocNode = _precursorResult.Precursor.DocNode;
            if (precursorDocNode.RelativeRT == RelativeRT.Unknown)
            {
                return peptideDocNode.TransitionGroups.Where(tg => Equals(tg.LabelType, precursorDocNode.LabelType));
            }

            return peptideDocNode.TransitionGroups.Where(tg => tg.RelativeRT != RelativeRT.Unknown);
        }

        private SrmDocument RemovePeak(SrmDocument document, TransitionGroupDocNode precursor)
        {
            var identityPath =
                new IdentityPath(_precursorResult.Precursor.Peptide.IdentityPath, precursor.TransitionGroup);
            var resultFile = _precursorResult.GetResultFile();
            return document.ChangePeak(identityPath, resultFile.Replicate.Name,
                resultFile.ChromFileInfo.FilePath, null, null, null, UserSet.TRUE, null, false);
        }

        private SrmDocument ChoosePeak(SrmDocument document, TransitionGroupDocNode precursor, double retentionTime)
        {
            var identityPath =
                new IdentityPath(_precursorResult.Precursor.Peptide.IdentityPath, precursor.TransitionGroup);
            var resultFile = _precursorResult.GetResultFile();
            return document.ChangePeak(identityPath, resultFile.Replicate.Name,
                resultFile.ChromFileInfo.FilePath, null, retentionTime, UserSet.TRUE);
        }
    }
}
