/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using pwiz.Common.DataBinding.RowSources;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public abstract class SkylineObjectList<TKey, TItem> : BindingListSupport<TItem>, ICloneableList<TKey, TItem> where TItem : SkylineObject
    {
        private IDocumentChangeListener _documentChangeListener;
        protected IDictionary<TKey, int> _keyIndexes 
            = new Dictionary<TKey, int>();

        protected SkylineObjectList(SkylineDataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }

        public SrmDocument SrmDocument {get { return DataSchema.Document; }}

        [Browsable(false)]
        public SkylineDataSchema DataSchema { get; private set; }

        protected override void BeforeFirstListenerAdded()
        {
            Debug.Assert(null == _documentChangeListener);
            _documentChangeListener = new DocumentChangeListener(this);
            DataSchema.Listen(_documentChangeListener);
            base.BeforeFirstListenerAdded();
        }

        protected override void AfterLastListenerRemoved()
        {
            base.AfterLastListenerRemoved();
            Debug.Assert(null != _documentChangeListener);
            DataSchema.Unlisten(_documentChangeListener);
            _documentChangeListener = null;
        }

        private void DocumentOnChanged(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            OnDocumentChanged();
        }

        protected void OnDocumentChanged()
        {
            var newKeys = ListKeys();
            var newList = new List<TItem>();
            var newKeyIndexes = new Dictionary<TKey, int>();
            foreach (var key in newKeys)
            {
                int oldIndex;
                TItem node;
                if (!_keyIndexes.TryGetValue(key, out oldIndex))
                {
                    var idPath = key;
                    node = ConstructItem(idPath);
                }
                else
                {
                    node = this[oldIndex];
                }
                newKeyIndexes.Add(key, newKeyIndexes.Count);
                newList.Add(node);
            }
            if (!newList.SequenceEqual(this))
            {
                Clear();
                foreach (var item in newList)
                {
                    Add(item);
                }
                _keyIndexes = newKeyIndexes;
            }
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        protected abstract IList<TKey> ListKeys();        
        protected abstract TItem ConstructItem(TKey key);

        public int IndexOfKey(TKey key)
        {
            int index;
            if (ReferenceEquals(null, key))
            {
                return -1;
            }
            if (_keyIndexes.TryGetValue(key, out index))
            {
                return index;
            }
            return -1;
        }
        public abstract TKey GetKey(TItem value);
        public abstract IList<TItem> DeepClone();
        IEnumerable ICloneableList.DeepClone()
        {
            return DeepClone();
        }

        private class DocumentChangeListener : IDocumentChangeListener
        {
            private readonly SkylineObjectList<TKey, TItem> _skylineObjectList;
            public DocumentChangeListener(SkylineObjectList<TKey, TItem> skylineObjectList)
            {
                _skylineObjectList = skylineObjectList;
            }

            public void DocumentOnChanged(object sender, DocumentChangedEventArgs args)
            {
                _skylineObjectList.DocumentOnChanged(sender, args);
            }
        }
    }
}
