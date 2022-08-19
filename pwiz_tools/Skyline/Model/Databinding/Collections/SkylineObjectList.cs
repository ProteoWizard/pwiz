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
using System.Threading;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public abstract class SkylineObjectList<TKey, TItem> : AbstractRowSource
    {
        private IDocumentChangeListener _documentChangeListener;

        protected SkylineObjectList(SkylineDataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }

        public SrmDocument SrmDocument {get { return DataSchema.Document; }}

        [Browsable(false)]
        public SkylineDataSchema DataSchema { get; private set; }

        public CancellationToken CancellationToken { get { return DataSchema.QueryLock.CancellationToken; } }

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
            FireListChanged();
        }

        public override IEnumerable GetItems()
        {
            return ListKeys().Select(ConstructItem);
        }

        protected abstract IEnumerable<TKey> ListKeys();



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

        protected abstract TItem ConstructItem(TKey key);
    }
}
