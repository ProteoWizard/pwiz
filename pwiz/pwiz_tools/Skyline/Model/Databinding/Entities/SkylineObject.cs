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
using System.ComponentModel;
using System.Diagnostics;
using pwiz.Common.DataBinding.RowSources;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class SkylineObject : PropertyChangedSupport
    {
        private IDocumentChangeListener _documentChangeListener;
        public SkylineObject(SkylineDataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }
        [Browsable(false)]
        public SkylineDataSchema DataSchema { get; private set; }

        [Browsable(false)]
        protected SrmDocument SrmDocument
        {
            get { return DataSchema.Document; }
        }

        protected override void BeforeFirstListenerAdded()
        {
            base.BeforeFirstListenerAdded();
            Debug.Assert(null == _documentChangeListener);
            DataSchema.Listen(_documentChangeListener = new DocumentChangeListener(this));
        }

        protected override void AfterLastListenerRemoved()
        {
            Debug.Assert(null != _documentChangeListener);
            DataSchema.Unlisten(_documentChangeListener);
            _documentChangeListener = null;
            base.AfterLastListenerRemoved();
        }

        private void DocumentOnChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                OnDocumentChanged();
            }
            catch (Exception exception)
            {
                Trace.TraceError("Exception in DocumentOnChanged {0}.  ", exception);
            }
        }

        protected virtual void OnDocumentChanged()
        {
        }

        public virtual object GetAnnotation(AnnotationDef annotationDef)
        {
            return null;
        }
        public virtual void SetAnnotation(AnnotationDef annotationDef, object value)
        {
        }
        protected void ModifyDocument(Func<SrmDocument, SrmDocument> action)
        {
            var skylineWindow = DataSchema.SkylineWindow;
            if (skylineWindow == null)
            {
                throw new InvalidOperationException();
            }
            skylineWindow.ModifyDocument("Edit", action);
        }

        private class DocumentChangeListener : IDocumentChangeListener
        {
            private readonly SkylineObject _skylineObject;
            public DocumentChangeListener(SkylineObject skylineObject)
            {
                _skylineObject = skylineObject;
            }

            public void DocumentOnChanged(object sender, DocumentChangedEventArgs args)
            {
                _skylineObject.DocumentOnChanged(sender, args);
            }
        }
    }
}
