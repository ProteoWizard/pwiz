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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

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
            var resultMap = new Dictionary<ResultKey, TResult>();
            if (results == null)
            {
                return resultMap;
            }
            for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
            {
                var replicate = new Replicate(DataSchema, replicateIndex);
                var files = results[replicateIndex];
                if (null == files)
                {
                    continue;
                }
                for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
                {
                    var chromInfo = files[fileIndex];
                    if (null == chromInfo)
                    {
                        continue;
                    }
                    var key = new ResultKey(replicate, fileIndex);
                    var resultFile = new ResultFile(replicate, chromInfo.FileId, ResultFile.GetOptStep(chromInfo));
                    resultMap.Add(key, newResultFunc(resultFile));
                }
            }
            return resultMap;
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
            return (TDocNode) DataSchema.Document.FindNode(IdentityPath) ?? CreateEmptyNode();
        }

        /// <summary>
        /// In case the node does not exist in the document before GetDocNode ever got called.
        /// </summary>
        protected abstract TDocNode CreateEmptyNode();

        public void ChangeDocNode(EditDescription editDescription, DocNode newDocNode)
        {
            ModifyDocument(editDescription, document => (SrmDocument) document.ReplaceChild(IdentityPath.Parent, newDocNode));
        }

        public override object GetAnnotation(AnnotationDef annotationDef)
        {
            return DocNode.Annotations.GetAnnotation(annotationDef);
        }

        public override void SetAnnotation(AnnotationDef annotationDef, object value)
        {
            ChangeDocNode(EditDescription.SetAnnotation(annotationDef, value), 
                DocNode.ChangeAnnotations(DocNode.Annotations.ChangeAnnotation(annotationDef, value)));
        }
        public override string ToString()
        {
            return DocNode.ToString();
        }
    }
}
