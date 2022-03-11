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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public abstract class SkylineDocNode : SkylineObject, ILinkValue
    {
        protected SkylineDocNode(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema)
        {
            IdentityPath = identityPath;
        }

        [Browsable(false)]
        public IdentityPath IdentityPath { get; private set; }

        public void LinkValueOnClick(object sender, EventArgs args)
        {
            var skylineWindow = DataSchema.SkylineWindow;
            if (null == skylineWindow)
            {
                return;
            }
            skylineWindow.SelectedPath = IdentityPath;
        }

        EventHandler ILinkValue.ClickEventHandler
        {
            get { return LinkValueOnClick; }
        }
        object ILinkValue.Value { get { return this; } }

        protected IDictionary<ResultKey, TResult> MakeChromInfoResultsMap<TChromInfo, TResult>(
            Results<TChromInfo> results, Func<ResultFile, TResult> newResultFunc) where TChromInfo : ChromInfo
        {
            if (results == null)
            {
                return ImmutableSortedList<ResultKey, TResult>.EMPTY.AsDictionary();
            }
            var replicates = DataSchema.ReplicateList;
            var resultFiles = DataSchema.ResultFileList;
            if (replicates.Count == results.Count && results.All(r=>r.Count == 1 && r[0] != null))
            {
                // If every replicate has exactly one result, then we can reuse the keys in "DataSchema.ReplicateList".
                var newValues = new List<TResult>(replicates.Count);
                for (int replicateIndex = 0; replicateIndex < replicates.Count; replicateIndex++)
                {
                    var chromInfo = results[replicateIndex][0];
                    var optStep = ResultFile.GetOptStep(chromInfo);
                    ResultFile resultFile = null;
                    if (optStep == 0)
                    {
                        resultFiles.TryGetValue(new ResultFileKey(replicateIndex, chromInfo.FileId, optStep), out resultFile);
                    }
                    if (resultFile == null)
                    {
                        resultFile = new ResultFile(replicates.Values[replicateIndex], chromInfo.FileId, optStep);
                    }
                    newValues.Add(newResultFunc(resultFile));
                }
                return replicates.ReplaceValues(newValues).AsDictionary();
            }
            var newEntries = new List<KeyValuePair<ResultKey, TResult>>();
            for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
            {
                var replicate = new Replicate(DataSchema, replicateIndex);
                var files = results[replicateIndex];
                for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
                {
                    var chromInfo = files[fileIndex];
                    var key = fileIndex == 0 ? replicates.Keys[replicateIndex] : new ResultKey(replicate, fileIndex);
                    int optStep = ResultFile.GetOptStep(chromInfo);
                    ResultFile resultFile = null;
                    if (optStep == 0)
                    {
                        resultFiles.TryGetValue(new ResultFileKey(replicateIndex, chromInfo.FileId, optStep), out resultFile);
                    }
                    if (resultFile == null)
                    {
                        resultFile = new ResultFile(replicates.Values[replicateIndex], chromInfo.FileId, optStep);
                    }
                    newEntries.Add(new KeyValuePair<ResultKey, TResult>(key, newResultFunc(resultFile)));
                }
            }
            return ImmutableSortedList<ResultKey, TResult>.FromValues(newEntries, Comparer<ResultKey>.Default)
                .AsDictionary();
        }

        protected bool Equals(SkylineDocNode other)
        {
            return Equals(DataSchema, other.DataSchema)
                && IdentityPath.Equals(other.IdentityPath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SkylineDocNode) obj);
        }

        public override int GetHashCode()
        {
            int result = DataSchema.GetHashCode();
            result = result * 397 ^ IdentityPath.GetHashCode();
            return result;
        }

        public abstract string GetDeleteConfirmation(int nodeCount);

        public static string GetGenericDeleteConfirmation(int nodeCount)
        {
            return string.Format(Resources.SkylineDocNode_GetGenericDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__things_, nodeCount);
        }

        protected abstract NodeRef NodeRefPrototype { get; }
        protected abstract Type SkylineDocNodeType { get; }
    }

    public abstract class SkylineDocNode<TDocNode> : SkylineDocNode where TDocNode : DocNode
    {
        private readonly CachedValue<TDocNode> _docNode;
        protected SkylineDocNode(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
            _docNode = CachedValue.Create(dataSchema, FindDocNode);
            _docNode.GetValue();
        }
        [Browsable(false)]
        public TDocNode DocNode
        {
            get { return GetDocNode(); }
        }

        public TDocNode GetDocNode()
        {
            return _docNode.Value;
        }

        private TDocNode FindDocNode()
        {
            return FindDocNodeInDoc(DataSchema.Document) ?? CreateEmptyNode();
        }

        public TDocNode FindDocNodeInDoc(SrmDocument document)
        {
            return (TDocNode) document.FindNode(IdentityPath);
        }

        /// <summary>
        /// In case the node does not exist in the document before GetDocNode ever got called.
        /// </summary>
        protected abstract TDocNode CreateEmptyNode();

        public void ChangeDocNode(EditDescription editDescription, Func<TDocNode, TDocNode> newDocNode)
        {
            ModifyDocument(editDescription.ChangeElementRef(GetElementRef()), document => (SrmDocument) document.ReplaceChild(IdentityPath.Parent, newDocNode(FindDocNodeInDoc(document))));
        }

        public override object GetAnnotation(AnnotationDef annotationDef)
        {
            return DataSchema.AnnotationCalculator.GetAnnotation(annotationDef, SkylineDocNodeType, this, DocNode.Annotations);
        }

        public override void SetAnnotation(AnnotationDef annotationDef, object value)
        {
            ChangeDocNode(EditDescription.SetAnnotation(annotationDef, value),
                docNode => (TDocNode) docNode.ChangeAnnotations(docNode.Annotations.ChangeAnnotation(annotationDef, value)));
        }
        public override string ToString()
        {
            return DocNode.ToString();
        }

        public sealed override ElementRef GetElementRef()
        {
            return DataSchema.ElementRefs.GetNodeRef(IdentityPath);
        }
    }
}
