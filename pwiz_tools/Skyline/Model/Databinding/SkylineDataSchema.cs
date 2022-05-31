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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using SkylineTool;

namespace pwiz.Skyline.Model.Databinding
{
    public class SkylineDataSchema : DataSchema
    {
        private readonly IDocumentContainer _documentContainer;
        private readonly HashSet<IDocumentChangeListener> _documentChangedEventHandlers 
            = new HashSet<IDocumentChangeListener>();
        private readonly CachedValue<ImmutableSortedList<ResultKey, Replicate>> _replicates;
        private readonly CachedValue<IDictionary<ResultFileKey, ResultFile>> _resultFiles;
        private readonly CachedValue<ElementRefs> _elementRefCache;
        private readonly CachedValue<AnnotationCalculator> _annotationCalculator;
        private readonly CachedValue<NormalizedValueCalculator> _normalizedValueCalculator;

        private SrmDocument _batchChangesOriginalDocument;
        private List<EditDescription> _batchEditDescriptions;

        private SrmDocument _document;
        public SkylineDataSchema(IDocumentContainer documentContainer, DataSchemaLocalizer dataSchemaLocalizer) : base(dataSchemaLocalizer)
        {
            _documentContainer = documentContainer;
            _document = _documentContainer.Document;
            ChromDataCache = new ChromDataCache();

            _replicates = CachedValue.Create(this, CreateReplicateList);
            _resultFiles = CachedValue.Create(this, CreateResultFileList);
            _elementRefCache = CachedValue.Create(this, () => new ElementRefs(Document));
            _annotationCalculator = CachedValue.Create(this, () => new AnnotationCalculator(this));
            _normalizedValueCalculator = CachedValue.Create(this, () => new NormalizedValueCalculator(Document));
        }

        public override string DefaultUiMode
        {
            get
            {
                return UiModes.FromDocumentType(ModeUI);
            }
        }

        public SrmDocument.DOCUMENT_TYPE ModeUI
        {
            get
            {
                if (SkylineWindow != null)
                {
                    return SkylineWindow.ModeUI;
                }

                if (_documentContainer.Document.DocumentType == Program.ModeUI)
                {
                    return _documentContainer.Document.DocumentType;
                }

                return SrmDocument.DOCUMENT_TYPE.mixed;
            }
        }

        protected override bool IsScalar(Type type)
        {
            return base.IsScalar(type) || type == typeof(IsotopeLabelType) || type == typeof(DocumentLocation) ||
                   type == typeof(SampleType) || type == typeof(GroupIdentifier) || type == typeof(StandardType) ||
                   type == typeof(NormalizationMethod) || type == typeof(RegressionFit) ||
                   type == typeof(AuditLogRow.AuditLogRowText) || type == typeof(AuditLogRow.AuditLogRowId);
        }

        public override bool IsRootTypeSelectable(Type type)
        {
            if (typeof(ListItem).IsAssignableFrom(type))
            {
                return false;
            }
            return base.IsRootTypeSelectable(type) && type != typeof(SkylineDocument);
        }

        public override IEnumerable<PropertyDescriptor> GetPropertyDescriptors(Type type)
        {
            return base.GetPropertyDescriptors(type)
                .Concat(GetAnnotations(type))
                .Concat(GetRatioProperties(type))
                .Concat(GetListProperties(type))
                .Concat(GetFeatureProperties(type));
        }

        public IEnumerable<PropertyDescriptor> GetAnnotations(Type type)
        {
            if (null == type)
            {
                return new AnnotationPropertyDescriptor[0];
            }
            var annotationTargets = GetAnnotationTargets(type);
            if (annotationTargets.IsEmpty)
            {
                return new AnnotationPropertyDescriptor[0];
            }
            var properties = new List<PropertyDescriptor>();
            foreach (var annotationDef in Document.Settings.DataSettings.AnnotationDefs)
            {
                var intersectTargets = annotationDef.AnnotationTargets.Intersect(annotationTargets);
                if (!intersectTargets.IsEmpty)
                {
                    properties.Add(MakeLookupPropertyDescriptor(annotationDef, new AnnotationPropertyDescriptor(this, annotationDef, true)));
                }
            }
            return properties;
        }

        public IEnumerable<PropertyDescriptor> GetListProperties(Type type)
        {
            if (!typeof(ListItem).IsAssignableFrom(type))
            {
                return new AnnotationPropertyDescriptor[0];
            }
            var listName = ListItemTypes.INSTANCE.GetListName(type);
            if (string.IsNullOrEmpty(listName))
            {
                return new AnnotationPropertyDescriptor[0];
            }
            var listData = Document.Settings.DataSettings.FindList(listName);
            if (listData == null)
            {
                return new AnnotationPropertyDescriptor[0];
            }

            return listData.ListDef.Properties.Select(annotationDef =>
                MakeLookupPropertyDescriptor(annotationDef,
                    new ListColumnPropertyDescriptor(this, listData.ListDef.Name, annotationDef)));

        }

        private AnnotationDef.AnnotationTargetSet GetAnnotationTargets(Type type)
        {
            return AnnotationDef.AnnotationTargetSet.OfValues(
                type.GetCustomAttributes(true)
                    .OfType<AnnotationTargetAttribute>()
                    .Select(attr => attr.AnnotationTarget));
        }

        public IEnumerable<RatioPropertyDescriptor> GetRatioProperties(Type type)
        {
            return RatioPropertyDescriptor.ListProperties(Document, type);
        }

        public IEnumerable<FeaturePropertyDescriptor> GetFeatureProperties(Type type)
        {
            return FeaturePropertyDescriptor.ListProperties(type, DataSchemaLocalizer.Language);
        }

        public SrmDocument Document
        {
            get
            {
                return _document;
            }
        }
        public void Listen(IDocumentChangeListener listener)
        {
            lock (_documentChangedEventHandlers)
            {
                bool firstListener = _documentChangedEventHandlers.Count == 0;
                if (!_documentChangedEventHandlers.Add(listener))
                {
                    throw new ArgumentException(@"Listener already added");
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
                    throw new ArgumentException(@"Listener not added");
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
            using (QueryLock.CancelAndGetWriteLock())
            {
                _document = _documentContainer.Document;
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

        public ChromDataCache ChromDataCache { get; private set; }
        public ElementRefs ElementRefs { get { return _elementRefCache.Value; } }

        public AnnotationCalculator AnnotationCalculator
        {
            get { return _annotationCalculator.Value; }
        }

        public NormalizedValueCalculator NormalizedValueCalculator
        {
            get { return _normalizedValueCalculator.Value; }
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
                    return new AnnotationPropertyDescriptor(this, annotationDef, false);
                }
            }

            return null;
        }

        public override string GetColumnDescription(ColumnDescriptor columnDescriptor)
        {
            String description = base.GetColumnDescription(columnDescriptor);
            if (!string.IsNullOrEmpty(description))
            {
                return description;
            }
            var columnCaption = GetColumnCaption(columnDescriptor);
            return ColumnToolTips.ResourceManager.GetString(columnCaption.GetCaption(DataSchemaLocalizer.INVARIANT));
        }

        public override IColumnCaption GetInvariantDisplayName(string uiMode, Type type)
        {
            if (typeof(ListItem).IsAssignableFrom(type))
            {
                return ColumnCaption.UnlocalizableCaption(ListItemTypes.INSTANCE.GetListName(type));
            }
            return base.GetInvariantDisplayName(uiMode, type);
        }

        public override string GetTypeDescription(string uiMode, Type type)
        {
            if (typeof(ListItem).IsAssignableFrom(type))
            {
                return string.Format(Resources.SkylineDataSchema_GetTypeDescription_Item_in_list___0__, ListItemTypes.INSTANCE.GetListName(type));
            }
            return base.GetTypeDescription(uiMode, type);
        }

        public ImmutableSortedList<ResultKey, Replicate> ReplicateList { get { return _replicates.Value; } }
        public IDictionary<ResultFileKey, ResultFile> ResultFileList { get { return _resultFiles.Value; } }

        public static DataSchemaLocalizer GetLocalizedSchemaLocalizer()
        {
            return new DataSchemaLocalizer(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture, ColumnCaptions.ResourceManager);
        }

        public void BeginBatchModifyDocument()
        {
            if (null != _batchChangesOriginalDocument)
            {
                throw new InvalidOperationException();
            }
            if (!ReferenceEquals(_document, _documentContainer.Document))
            {
                DocumentChangedEventHandler(_documentContainer, new DocumentChangedEventArgs(_document));
            }
            _batchChangesOriginalDocument = _document;
            _batchEditDescriptions = new List<EditDescription>();
        }

        public void CommitBatchModifyDocument(string description, DataGridViewPasteHandler.BatchModifyInfo batchModifyInfo)
        {
            if (null == _batchChangesOriginalDocument)
            {
                throw new InvalidOperationException();
            }
            string message = Resources.DataGridViewPasteHandler_EndDeferSettingsChangesOnDocument_Updating_settings;
            if (SkylineWindow != null)
            {
                SkylineWindow.ModifyDocument(description, document =>
                {
                    VerifyDocumentCurrent(_batchChangesOriginalDocument, document);
                    using (var longWaitDlg = new LongWaitDlg
                    {
                        Message = message
                    })
                    {
                        SrmDocument newDocument = document;
                        longWaitDlg.PerformWork(SkylineWindow, 1000, progressMonitor =>
                        {
                            var srmSettingsChangeMonitor = new SrmSettingsChangeMonitor(progressMonitor,
                                message);
                            newDocument = _document.EndDeferSettingsChanges(_batchChangesOriginalDocument,
                                srmSettingsChangeMonitor);
                        });
                        return newDocument;
                    }
                }, GetAuditLogFunction(batchModifyInfo));
            }
            else
            {
                VerifyDocumentCurrent(_batchChangesOriginalDocument, _documentContainer.Document);
                if (!_documentContainer.SetDocument(
                    _document.EndDeferSettingsChanges(_batchChangesOriginalDocument, null),
                    _batchChangesOriginalDocument))
                {
                    throw new InvalidOperationException(Resources
                        .SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
                }
            }
            _batchChangesOriginalDocument = null;
            _batchEditDescriptions = null;
            DocumentChangedEventHandler(_documentContainer, new DocumentChangedEventArgs(_document));
        }

        private Func<SrmDocumentPair, AuditLogEntry> GetAuditLogFunction(
            DataGridViewPasteHandler.BatchModifyInfo batchModifyInfo)
        {
            if (batchModifyInfo == null)
            {
                return null;
            }
            return docPair =>
            {
                if (EqualExceptAuditLog(docPair.OldDoc, docPair.NewDoc))
                {
                    return AuditLogEntry.SKIP;
                }
                MessageType singular, plural;
                var detailType = MessageType.set_to_in_document_grid;
                Func<EditDescription, object[]> getArgsFunc = descr => new object[]
                {
                    descr.AuditLogParseString, descr.ElementRefName,
                    CellValueToString(descr.Value)
                };

                switch (batchModifyInfo.BatchModifyAction)
                {
                    case DataGridViewPasteHandler.BatchModifyAction.Paste:
                        singular = MessageType.pasted_document_grid_single;
                        plural = MessageType.pasted_document_grid;
                        break;
                    case DataGridViewPasteHandler.BatchModifyAction.Clear:
                        singular = MessageType.cleared_document_grid_single;
                        plural = MessageType.cleared_document_grid;
                        detailType = MessageType.cleared_cell_in_document_grid;
                        getArgsFunc = descr => new[]
                            {(object) descr.ColumnCaption.GetCaption(DataSchemaLocalizer), descr.ElementRefName};
                        break;
                    case DataGridViewPasteHandler.BatchModifyAction.FillDown:
                        singular = MessageType.fill_down_document_grid_single;
                        plural = MessageType.fill_down_document_grid;
                        break;
                    default:
                        return null;
                }

                var entry = AuditLogEntry.CreateCountChangeEntry(singular, plural, docPair.NewDocumentType,
                    _batchEditDescriptions,
                    descr => MessageArgs.Create(descr.ColumnCaption.GetCaption(DataSchemaLocalizer)),
                    null).ChangeExtraInfo(batchModifyInfo.ExtraInfo + Environment.NewLine);

                entry = entry.Merge(batchModifyInfo.EntryCreator.Create(docPair));

                return entry.AppendAllInfo(_batchEditDescriptions.Select(descr => new MessageInfo(detailType, docPair.NewDocumentType,
                    getArgsFunc(descr))).ToList());
            };
        }

        public void RollbackBatchModifyDocument()
        {
            _batchChangesOriginalDocument = null;
            _batchEditDescriptions = null;
            _document = _documentContainer.Document;
        }

        private static string CellValueToString(object value)
        {
            if (value == null)
                return string.Empty;

            // TODO: only allow reflection for all info? Okay to use null for decimal places?
            bool unused;
            return DiffNode.ObjectToString(true, value, null, out unused);
        }

        public void ModifyDocument(EditDescription editDescription, Func<SrmDocument, SrmDocument> action, Func<SrmDocumentPair, AuditLogEntry> logFunc = null)
        {
            if (_batchChangesOriginalDocument == null)
            {
                if (SkylineWindow != null)
                {
                    if (SkylineWindow.SequenceTree == null)
                    {
                        // The SequenceTree can be null if we are in the process of restoring a .view
                        // We should ignore any change that happens during that.
                        return;
                    }
                    SkylineWindow.ModifyDocument(editDescription.GetUndoText(DataSchemaLocalizer), action,
                        logFunc ?? (docPair => LogEntryFromEditDescription(editDescription, docPair)));
                }
                else
                {
                    var doc = _documentContainer.Document;
                    if (!_documentContainer.SetDocument(action(doc), doc))
                    {
                        throw new InvalidOperationException(Resources
                            .SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
                    }
                }
                return;
            }
            VerifyDocumentCurrent(_batchChangesOriginalDocument, _documentContainer.Document);
            _batchEditDescriptions.Add(editDescription);
            _document = action(_document.BeginDeferSettingsChanges());
        }

        private void VerifyDocumentCurrent(SrmDocument expectedCurrentDocument, SrmDocument actualCurrentDocument)
        {
            if (!ReferenceEquals(expectedCurrentDocument, actualCurrentDocument))
            {
                throw new InvalidOperationException(Resources.SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
            }
        }

        private ImmutableSortedList<ResultKey, Replicate> CreateReplicateList()
        {
            var srmDocument = Document;
            if (!srmDocument.Settings.HasResults)
            {
                return ImmutableSortedList<ResultKey, Replicate>.EMPTY;
            }
            return ImmutableSortedList<ResultKey, Replicate>.FromValues(
                Enumerable.Range(0, srmDocument.Settings.MeasuredResults.Chromatograms.Count)
                    .Select(replicateIndex =>
                    {
                        var replicate = new Replicate(this, replicateIndex);
                        return new KeyValuePair<ResultKey, Replicate>(new ResultKey(replicate, 0), replicate);
                    }), Comparer<ResultKey>.Default);
        }
 
        private IDictionary<ResultFileKey, ResultFile> CreateResultFileList()
        {
            return ReplicateList.Values.SelectMany(
                    replicate =>
                        replicate.ChromatogramSet.MSDataFileInfos.Select(
                            chromFileInfo => new ResultFile(replicate, chromFileInfo.FileId, 0)))
                .ToDictionary(resultFile => new ResultFileKey(resultFile.Replicate.ReplicateIndex,
                    resultFile.ChromFileInfoId, resultFile.OptimizationStep));
        }

        public PropertyDescriptor MakeLookupPropertyDescriptor(AnnotationDef annotationDef, PropertyDescriptor innerPropertyDescriptor)
        {
            if (string.IsNullOrEmpty(annotationDef.Lookup))
            {
                return innerPropertyDescriptor;
            }
            var listLookupPropertyDescriptor = new ListLookupPropertyDescriptor(this, annotationDef.Lookup, innerPropertyDescriptor);
            var listData = listLookupPropertyDescriptor.ListData;
            if (listData == null || listData.PkColumn == null)
            {
                return innerPropertyDescriptor;
            }
            return listLookupPropertyDescriptor;
        }

        public static SkylineDataSchema MemoryDataSchema(SrmDocument document, DataSchemaLocalizer localizer)
        {
            var documentContainer = new MemoryDocumentContainer();
            documentContainer.SetDocument(document, documentContainer.Document);
            return new SkylineDataSchema(documentContainer, localizer);
        }

        public override string NormalizeUiMode(string uiMode)
        {
            if (string.IsNullOrEmpty(uiMode))
            {
                return UiModes.PROTEOMIC;
            }

            return uiMode;
        }

        public AuditLogEntry LogEntryFromEditDescription(EditDescription editDescription, SrmDocumentPair docPair)
        {
            if (EqualExceptAuditLog(docPair.OldDoc, docPair.NewDoc))
            {
                return AuditLogEntry.SKIP;
            }

            return AuditLogEntry.CreateSimpleEntry(MessageType.set_to_in_document_grid, docPair.NewDocumentType,
                editDescription.AuditLogParseString, editDescription.ElementRefName,
                CellValueToString(editDescription.Value));
        }

        public static bool EqualExceptAuditLog(SrmDocument document1, SrmDocument document2)
        {
            return document1.ChangeAuditLog(AuditLogEntry.ROOT).Equals(document2.ChangeAuditLog(AuditLogEntry.ROOT));
        }
    }
}
