﻿/*
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
using System.ComponentModel;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [InvariantDisplayName(nameof(ResultFile))]
    public class ResultFile : SkylineObject, IComparable
    {
        private readonly CachedValue<ChromFileInfo> _chromFileInfo;
        public ResultFile(Replicate replicate, ChromFileInfoId chromFileInfoId, int optStep) : base(replicate.DataSchema)
        {
            Replicate = replicate;
            ChromFileInfoId = chromFileInfoId;
            _chromFileInfo = CachedValue.Create(DataSchema, () => Replicate.ChromatogramSet.GetFileInfo(ChromFileInfoId));
            OptimizationStep = optStep;
        }

        [Browsable(false)]
        public ChromFileInfoId ChromFileInfoId { get; private set; }
        [Browsable(false)]
        public ChromFileInfo ChromFileInfo { get { return _chromFileInfo.Value; } }
        [Browsable(false)]
        public int OptimizationStep { get; private set; }
        [HideWhen(AncestorsOfAnyOfTheseTypes = new []{typeof(SkylineDocument), typeof(Replicate)})]
        public Replicate Replicate { get; private set; }
        public string FileName {
            get { return ChromFileInfo.FilePath.GetFileName(); }
        }

        public string FilePath
        {
            get { return ChromFileInfo.FilePath.GetFilePath(); }
        }

        public string SampleName
        {
            get { return ChromFileInfo.FilePath.GetSampleName(); }
        }
        public override string ToString()
        {
            return ChromFileInfo.FilePath.ToString();
        }
        public DateTime? ModifiedTime { get { return ChromFileInfo.FileWriteTime; } }
        public DateTime? AcquiredTime { get { return ChromFileInfo.RunStartTime; } }

        [Importable]
        public double? ExplicitGlobalStandardArea
        {
            get { return ChromFileInfo.ExplicitGlobalStandardArea; }
            set
            {
                if (Equals(ExplicitGlobalStandardArea, value))
                {
                    return;
                }
                var newFileInfos = Replicate.ChromatogramSet.MSDataFileInfos.ToArray();
                for (int iFile = 0; iFile < newFileInfos.Length; iFile++)
                {
                    if (ReferenceEquals(newFileInfos[iFile].FileId, ChromFileInfoId))
                    {
                        newFileInfos[iFile] = newFileInfos[iFile].ChangeExplicitGlobalStandardArea(value);
                    }
                }

                Replicate.ChangeChromatogramSet(
                    EditColumnDescription(nameof(ExplicitGlobalStandardArea), value), 
                    Replicate.ChromatogramSet.ChangeMSDataFileInfos(newFileInfos));
            }
        }

        public double? TicArea { get { return ChromFileInfo.TicArea; } }

        public eIonMobilityUnits IonMobilityUnits { get { return ChromFileInfo.IonMobilityUnits; } }

        public TChromInfo FindChromInfo<TChromInfo>(Results<TChromInfo> chromInfos) where TChromInfo : ChromInfo
        {
            if (null == chromInfos || chromInfos.Count <= Replicate.ReplicateIndex)
            {
                return null;
            }
            var chromInfoList = chromInfos[Replicate.ReplicateIndex];
            return chromInfoList.FirstOrDefault(chromInfo => ReferenceEquals(ChromFileInfoId, chromInfo.FileId) && GetOptStep(chromInfo) == OptimizationStep);
        }

        public static int GetOptStep(ChromInfo chromInfo)
        {
            var transitionChromInfo = chromInfo as TransitionChromInfo;
            if (null != transitionChromInfo)
            {
                return transitionChromInfo.OptimizationStep;
            }
            var transitionGroupChromInfo = chromInfo as TransitionGroupChromInfo;
            if (null != transitionGroupChromInfo)
            {
                return transitionGroupChromInfo.OptimizationStep;
            }
            return 0;
        }

        public Results<TChromInfo> ChangeChromInfo<TChromInfo>(Results<TChromInfo> chromInfos, Func<TChromInfo, TChromInfo> changeFunc)
            where TChromInfo : ChromInfo
        {
            var chromInfoList = chromInfos[Replicate.ReplicateIndex];
            for (int i = 0; i < chromInfoList.Count; i++)
            {
                if (ReferenceEquals(chromInfoList[i].FileId, ChromFileInfoId) && GetOptStep(chromInfoList[i]) == OptimizationStep)
                {
                    return chromInfos.ChangeAt(Replicate.ReplicateIndex, 
                        chromInfoList.ChangeAt(i, changeFunc(chromInfoList[i])));
                }
            }
            throw new InvalidOperationException();
        }

        public int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            var resultFile = (ResultFile) obj;
            int replicateCompare = Replicate.CompareTo(resultFile.Replicate);
            if (0 != replicateCompare)
            {
                return replicateCompare;
            }
            return string.Compare(FileName, resultFile.FileName, StringComparison.CurrentCultureIgnoreCase);
        }

        public double GetTotalArea(IsotopeLabelType isotopeLabelType)
        {
            return DataSchema.GetReplicateSummaries().GetTotalArea(Replicate.ReplicateIndex, isotopeLabelType);
        }

        public ResultFileKey ToFileKey()
        {
            return new ResultFileKey(Replicate.ReplicateIndex, ChromFileInfoId, OptimizationStep);
        }

        [InvariantDisplayName("ResultFileLocator")]
        public string Locator
        {
            get { return GetLocator(); }
        }

        public string SampleId
        {
            get { return ChromFileInfo.SampleId; }
        }

        public string InstrumentSerialNumber
        {
            get { return ChromFileInfo.InstrumentSerialNumber; }
        }

        public override ElementRef GetElementRef()
        {
            var sibling = ResultFileRef.PROTOTYPE.ChangeParent(Replicate.GetElementRef());
            int fileIndex = Replicate.ChromatogramSet.IndexOfId(ChromFileInfoId);
            return sibling.ListChildrenOfParent(SrmDocument).Skip(fileIndex).FirstOrDefault();
        }
    }
}
