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
using System.Globalization;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using SkylineTool;

namespace pwiz.Skyline.Model.Databinding
{
    public class SkylineDataSchema : DataSchema
    {
        private readonly IDocumentContainer _documentContainer;
        private readonly HashSet<IDocumentChangeListener> _documentChangedEventHandlers 
            = new HashSet<IDocumentChangeListener>();
        public SkylineDataSchema(IDocumentContainer documentContainer, DataSchemaLocalizer dataSchemaLocalizer) : base(dataSchemaLocalizer)
        {
            _documentContainer = documentContainer;
        }

        public SkylineDataSchema Clone()
        {
            var container = new MemoryDocumentContainer();
            container.SetDocument(Document, container.Document);
            return new SkylineDataSchema(container, DataSchemaLocalizer);
        }

        protected override bool IsScalar(Type type)
        {
            return base.IsScalar(type) || type == typeof(IsotopeLabelType) || type == typeof(DocumentLocation) || type == typeof(SampleType) || type==typeof(GroupIdentifier);
        }

        public override bool IsRootTypeSelectable(Type type)
        {
            return base.IsRootTypeSelectable(type) && type != typeof(SkylineDocument);
        }

        public override IEnumerable<PropertyDescriptor> GetPropertyDescriptors(Type type)
        {
            return base.GetPropertyDescriptors(type).Concat(GetAnnotations(type)).Concat(GetRatioProperties(type));

        }

        public IEnumerable<PropertyDescriptor> GetAnnotations(Type type)
        {
            if (null == type)
            {
                return new PropertyDescriptor[0];
            }
            var annotationTargets = GetAnnotationTargets(type);
            if (annotationTargets.IsEmpty)
            {
                return new PropertyDescriptor[0];
            }
            var properties = new List<PropertyDescriptor>();
            foreach (var annotationDef in Document.Settings.DataSettings.AnnotationDefs)
            {
                if (annotationDef.AnnotationTargets.Intersect(annotationTargets).IsEmpty)
                {
                    continue;
                }
                properties.Add(new AnnotationPropertyDescriptor(annotationDef, true));
            }
            return properties;
        }

        private AnnotationDef.AnnotationTargetSet GetAnnotationTargets(Type type)
        {
            return AnnotationDef.AnnotationTargetSet.OfValues(
                type.GetCustomAttributes(true)
                    .OfType<AnnotationTargetAttribute>()
                    .Select(attr => attr.AnnotationTarget));
        }

        public IEnumerable<PropertyDescriptor> GetRatioProperties(Type type)
        {
            return RatioPropertyDescriptor.ListProperties(Document, type);
        }

        public SrmDocument Document
        {
            get
            {
                var documentUiContainer = _documentContainer as IDocumentUIContainer;
                if (null == documentUiContainer)
                {
                    return _documentContainer.Document;
                }
                else
                {
                    return documentUiContainer.DocumentUI;
                }
            }
        }
        public void Listen(IDocumentChangeListener listener)
        {
            lock (_documentChangedEventHandlers)
            {
                bool firstListener = _documentChangedEventHandlers.Count == 0;
                if (!_documentChangedEventHandlers.Add(listener))
                {
                    throw new ArgumentException("Listener already added"); // Not L10N
                }
                if (firstListener)
                {
                    var documentUiContainer = _documentContainer as IDocumentUIContainer;
                    if (null == documentUiContainer)
                    {
                        _documentContainer.Listen(DocumentChangedEventHandler);
                    }
                    else
                    {
                        documentUiContainer.ListenUI(DocumentChangedEventHandler);
                    }
                }
            }
        }

        public void Unlisten(IDocumentChangeListener listener)
        {
            lock (_documentChangedEventHandlers)
            {
                if (!_documentChangedEventHandlers.Remove(listener))
                {
                    throw new ArgumentException("Listener not added"); // Not L10N
                }
                if (_documentChangedEventHandlers.Count == 0)
                {
                    var documentUiContainer = _documentContainer as IDocumentUIContainer;
                    if (null == documentUiContainer)
                    {
                        _documentContainer.Unlisten(DocumentChangedEventHandler);
                    }
                    else
                    {
                        documentUiContainer.UnlistenUI(DocumentChangedEventHandler);
                    }
                }
            }
        }

        private void DocumentChangedEventHandler(object sender, DocumentChangedEventArgs args)
        {
            IList<IDocumentChangeListener> listeners;
            lock (_documentChangedEventHandlers)
            {
                listeners = _documentChangedEventHandlers.ToArray();
            }
            foreach (var listener in listeners)
            {
                listener.DocumentOnChanged(sender, args);
            }
        }

        public SkylineWindow SkylineWindow { get { return _documentContainer as SkylineWindow; } }

        private ReplicateSummaries _replicateSummaries;
        public ReplicateSummaries GetReplicateSummaries()
        {
            ReplicateSummaries replicateSummaries;
            if (null == _replicateSummaries)
            {
                replicateSummaries = new ReplicateSummaries(Document);
            }
            else
            {
                replicateSummaries = _replicateSummaries.GetReplicateSummaries(Document);
            }
            return _replicateSummaries = replicateSummaries;
        }

        public override PropertyDescriptor GetPropertyDescriptor(Type type, string name)
        {
            var propertyDescriptor = base.GetPropertyDescriptor(type, name);
            if (null != propertyDescriptor)
            {
                return propertyDescriptor;
            }
            if (null == type)
            {
                return null;
            }
            propertyDescriptor = RatioPropertyDescriptor.GetProperty(Document, type, name);
            if (null != propertyDescriptor)
            {
                return propertyDescriptor;
            }
            if (name.StartsWith(AnnotationDef.ANNOTATION_PREFIX))
            {
                var annotationTargets = GetAnnotationTargets(type);
                if (!annotationTargets.IsEmpty)
                {
                    var annotationDef = new AnnotationDef(name.Substring(AnnotationDef.ANNOTATION_PREFIX.Length),
                        annotationTargets, AnnotationDef.AnnotationType.text, new string[0]);
                    return new AnnotationPropertyDescriptor(annotationDef, false);
                }
            }

            return null;
        }

        public static DataSchemaLocalizer GetLocalizedSchemaLocalizer()
        {
            return new DataSchemaLocalizer(CultureInfo.CurrentCulture, ColumnCaptions.ResourceManager);
        }
    }
}
