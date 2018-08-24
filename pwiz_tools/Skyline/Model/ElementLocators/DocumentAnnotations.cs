/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.ElementLocators
{
    /// <summary>
    /// Class for importing and exporting all of the annotations in a Skyline document.
    /// </summary>
    public class DocumentAnnotations
    {
        // ReSharper disable LocalizableElement
        public const string COLUMN_LOCATOR = "ElementLocator";
        public const string COLUMN_NOTE = "Note";
        // ReSharper restore LocalizableElement
        private readonly ElementRefs _elementRefs;
        private readonly IDictionary<NodeRef, IdentityPath> _identityPaths 
            = new Dictionary<NodeRef, IdentityPath>();
        // "Note" is not supported on replicates.
        public static readonly AnnotationDef.AnnotationTargetSet NOTE_TARGETS =
            AnnotationDef.AnnotationTargetSet.OfValues(
                AnnotationDef.AnnotationTarget.protein,
                AnnotationDef.AnnotationTarget.peptide,
                AnnotationDef.AnnotationTarget.precursor,
                AnnotationDef.AnnotationTarget.transition,
                AnnotationDef.AnnotationTarget.precursor_result,
                AnnotationDef.AnnotationTarget.transition_result
            );
        public DocumentAnnotations(SrmDocument document)
        {
            _elementRefs = new ElementRefs(document);
            Document = document;
            CultureInfo = CultureInfo.InvariantCulture;
        }

        public CultureInfo CultureInfo { get; set; }

        public SrmDocument Document { get; private set; }

        public void WriteAnnotationsToFile(CancellationToken cancellationToken, string filename)
        {
            using (var writer = new StreamWriter(filename))
            {
                SaveAnnotations(cancellationToken, writer, TextUtil.SEPARATOR_CSV);
            }
        }
        
        public void SaveAnnotations(CancellationToken cancellationToken, TextWriter writer, char separator)
        {
            WriteAllAnnotations(cancellationToken, writer, separator);
        }
        
        private void WriteAllAnnotations(CancellationToken cancellationToken, TextWriter textWriter, char separator)
        {
            string strSeparator = new string(separator, 1);
            var columns = new Columns(Document.Settings.DataSettings.AnnotationDefs);
            textWriter.WriteLine(string.Join(strSeparator, columns.GetColumnHeaders().Select(header=>DsvWriter.ToDsvField(separator, header))));
            foreach (var tuple in GetAllAnnotations())
            {
                var annotations = tuple.Item2;
                if (annotations.IsEmpty)
                {
                    continue;
                }
                var row = columns.GetRow(tuple.Item1, tuple.Item2);
                textWriter.WriteLine(string.Join(strSeparator, row.Select(value=>DsvWriter.ToDsvField(separator, ValueToString(value)))));
            }
        }

        public IEnumerable<Tuple<ElementRef, Annotations>> GetAllAnnotations()
        {
            return GetAllNodeAnnotations()
                .Concat(GetAllReplicateAnnotations())
                .Concat(GetAllResultAnnotations());
        }

        private IEnumerable<Tuple<ElementRef, Annotations>> GetAllNodeAnnotations()
        {
            return GetNodeAnnotations(Document.MoleculeGroups.Select(ToIdentityPathTuple))
                .Concat(GetNodeAnnotations(EnumerateMolecules().Select(ToIdentityPathTuple)))
                .Concat(GetNodeAnnotations(EnumeratePrecursors().Select(ToIdentityPathTuple)))
                .Concat(GetNodeAnnotations(EnumerateTransitions().Select(ToIdentityPathTuple)));
        }

        private IEnumerable<Tuple<ElementRef, Annotations>> GetAllReplicateAnnotations()
        {
            var measuredResults = Document.MeasuredResults;
            if (measuredResults == null)
            {
                yield break;
            }
            foreach (var chromatogramSet in measuredResults.Chromatograms)
            {
                yield return Tuple.Create((ElementRef) ReplicateRef.FromChromatogramSet(chromatogramSet), chromatogramSet.Annotations);
            }
        }

        private IEnumerable<Tuple<ElementRef, Annotations>> GetAllResultAnnotations()
        {
            var result = Enumerable.Empty<Tuple<ElementRef, Annotations>>();
            foreach (var tuple in EnumeratePrecursors())
            {
                var precursor = tuple.Item3;
                if (null == precursor.Results)
                {
                    continue;
                }
                var precursorRef = (PrecursorRef) _elementRefs.GetNodeRef(ToIdentityPathTuple(tuple).Item1);
                var precursorResultRef =
                    (PrecursorResultRef) PrecursorResultRef.PROTOTYPE.ChangeParent(precursorRef);
                result = result.Concat(GetNodeResultAnnotations(
                    precursorResultRef,
                    precursor.Results));
            }
            foreach (var tuple in EnumerateTransitions())
            {
                var transition = tuple.Item4;
                if (null == transition.Results)
                {
                    continue;
                }
                var transitionRef = (TransitionRef) _elementRefs.GetNodeRef(ToIdentityPathTuple(tuple).Item1);
                var transitionResultRef =
                    (TransitionResultRef) TransitionResultRef.PROTOTYPE.ChangeParent(transitionRef);
                result = result.Concat(GetNodeResultAnnotations(transitionResultRef, transition.Results));
            }
            return result;
        }

        private IEnumerable<Tuple<PeptideGroupDocNode, PeptideDocNode>> EnumerateMolecules()
        {
            return Document.MoleculeGroups.SelectMany(group => group.Molecules.Select(mol => Tuple.Create(group, mol)));
        }

        private IEnumerable<Tuple<PeptideGroupDocNode, PeptideDocNode, TransitionGroupDocNode>> 
            EnumeratePrecursors()
        {
            return EnumerateMolecules().SelectMany(tuple =>
                tuple.Item2.TransitionGroups.Select(precursor => Tuple.Create(tuple.Item1, tuple.Item2, precursor)));
        }

        private IEnumerable<Tuple<PeptideGroupDocNode, PeptideDocNode, TransitionGroupDocNode, TransitionDocNode>>
            EnumerateTransitions()
        {
            return EnumeratePrecursors().SelectMany(tuple => tuple.Item3.Transitions.Select(
                transition => Tuple.Create(tuple.Item1, tuple.Item2, tuple.Item3, transition)));
        }

        private IEnumerable<Tuple<ElementRef, Annotations>> GetNodeAnnotations<TNode>(
            IEnumerable<Tuple<IdentityPath, TNode>> identityPaths) where TNode: DocNode
        {
            foreach (var tuple in identityPaths)
            {
                var identityPath = tuple.Item1;
                var docNode = tuple.Item2;
                yield return Tuple.Create((ElementRef) _elementRefs.GetNodeRef(identityPath), docNode.Annotations);
            }
        }

        private IEnumerable<Tuple<ElementRef, Annotations>> GetNodeResultAnnotations<TDocNode, TChromInfo>(
            ResultRef<TDocNode, TChromInfo> resultRef,
            Results<TChromInfo> results) where TDocNode : DocNode where TChromInfo : ChromInfo
        {
            if (null == results)
            {
                yield break;
            }
            for (int i = 0; i < results.Count; i++)
            {
                var chromatogramSet = Document.Settings.MeasuredResults.Chromatograms[i];
                foreach (var chromInfo in results[i])
                {
                    if (resultRef.GetOptimizationStep(chromInfo) != 0)
                    {
                        continue;
                    }

                    yield return Tuple.Create((ElementRef) resultRef.ChangeChromInfo(chromatogramSet, chromInfo), resultRef.GetAnnotations(chromInfo));
                }
            }
        }

        private object GetAnnotationValue(Annotations annotations, AnnotationDef annotationDef)
        {
            var value = annotations.GetAnnotation(annotationDef);
            if (false.Equals(value))
            {
                return null;
            }
            return value;
        }

        private string ValueToString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (value is double)
            {
                return ((double) value).ToString(Formats.RoundTrip, CultureInfo);
            }
            return value.ToString();
        }

        private static Tuple<IdentityPath, PeptideGroupDocNode> ToIdentityPathTuple(PeptideGroupDocNode peptideGroupDocNode)
        {
            return Tuple.Create(new IdentityPath(peptideGroupDocNode.Id), peptideGroupDocNode);
        }

        private static Tuple<IdentityPath, PeptideDocNode> ToIdentityPathTuple(
            Tuple<PeptideGroupDocNode, PeptideDocNode> tuple)
        {
            return Tuple.Create(new IdentityPath(tuple.Item1.Id, tuple.Item2.Id), tuple.Item2);
        }

        private static Tuple<IdentityPath, TransitionGroupDocNode> ToIdentityPathTuple(
            Tuple<PeptideGroupDocNode, PeptideDocNode, TransitionGroupDocNode> tuple)
        {
            return Tuple.Create(new IdentityPath(tuple.Item1.Id, tuple.Item2.Id, tuple.Item3.Id), tuple.Item3);
        }

        private static Tuple<IdentityPath, TransitionDocNode> ToIdentityPathTuple(
            Tuple<PeptideGroupDocNode, PeptideDocNode, TransitionGroupDocNode, TransitionDocNode> tuple)
        {
            return Tuple.Create(new IdentityPath(tuple.Item1.Id, tuple.Item2.Id, tuple.Item3.Id, tuple.Item4.Id), tuple.Item4);
        }

        public SrmDocument ReadAnnotationsFromFile(CancellationToken cancellationToken, string filename)
        {
            using (var streamReader = new StreamReader(filename))
            {
                var dsvReader = new DsvFileReader(streamReader, TextUtil.SEPARATOR_CSV);
                return ReadAllAnnotations(cancellationToken, dsvReader);
            }
        }

        public SrmDocument ReadAllAnnotations(CancellationToken cancellationToken, DsvFileReader fileReader)
        {
            var document = Document;
            var columns = new Columns(fileReader.FieldNames, Document.Settings.DataSettings.AnnotationDefs);
            string[] row;
            while ((row = fileReader.ReadLine()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ElementLocator elementLocator = columns.GetElementLocator(row);
                var elementRef = ElementRefs.FromObjectReference(elementLocator);
                var annotations = GetAnnotations(document, elementRef);
                var newAnnotations = columns.ReadAnnotations(CultureInfo, elementRef, annotations, row);
                if (!Equals(newAnnotations, annotations))
                {
                    document = ChangeAnnotations(document, elementRef, newAnnotations);
                }
            }
            return document;
        }

        public SrmDocument ChangeAnnotations(SrmDocument document, ElementRef elementRef, Annotations newAnnotations)
        {
            var nodeRef = elementRef as NodeRef;
            if (nodeRef != null)
            {
                return SetDocNodeAnnotations(document, nodeRef, newAnnotations);
            }
            var replicateRef = elementRef as ReplicateRef;
            if (replicateRef != null)
            {
                return SetReplicateAnnotations(document, replicateRef, newAnnotations);
            }
            var resultRef = elementRef as ResultRef;
            if (resultRef != null)
            {
                return SetResultAnnotations(document, resultRef, newAnnotations);
            }
            throw AnnotationsNotSupported(elementRef);
        }

        public SrmDocument SetDocNodeAnnotations(SrmDocument document, NodeRef nodeRef, Annotations annotations)
        {
            var identityPath = ToIdentityPath(nodeRef);
            var docNode = document.FindNode(identityPath);
            docNode = docNode.ChangeAnnotations(annotations);
            document = (SrmDocument) document.ReplaceChild(identityPath.Parent, docNode);
            return document;
        }

        public SrmDocument SetReplicateAnnotations(SrmDocument document, ReplicateRef replicateRef,
            Annotations annotations)
        {
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                throw ElementNotFoundException(replicateRef);
            }
            for (int i = 0; i < measuredResults.Chromatograms.Count; i++)
            {
                var chromSet = measuredResults.Chromatograms[i];
                if (replicateRef.Matches(chromSet))
                {
                    chromSet = chromSet.ChangeAnnotations(annotations);
                    var newChromatograms = measuredResults.Chromatograms.ToArray();
                    newChromatograms[i] = chromSet;
                    return document.ChangeMeasuredResults(measuredResults.ChangeChromatograms(newChromatograms));
                }
            }
            throw ElementNotFoundException(replicateRef);
        }

        public SrmDocument SetResultAnnotations(SrmDocument document, ResultRef resultRef, Annotations annotations)
        {
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                throw ElementNotFoundException(resultRef);
            }
            var nodeRef = (NodeRef)resultRef.Parent;
            var identityPath = ToIdentityPath(nodeRef);
            var docNode = document.FindNode(identityPath);
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromSet = measuredResults.Chromatograms[replicateIndex];
                if (resultRef.Matches(chromSet))
                {
                    var transitionGroup = docNode as TransitionGroupDocNode;
                    if (transitionGroup != null)
                    {
                        var results = transitionGroup.Results.ToArray();
                        results[replicateIndex] = SetChromInfoAnnotations(chromSet, (PrecursorResultRef) resultRef,
                            results[replicateIndex], annotations);
                        docNode = ((TransitionGroupDocNode) docNode).ChangeResults(
                            new Results<TransitionGroupChromInfo>(results));
                    }
                    else
                    {
                        var transition = docNode as TransitionDocNode;
                        if (transition != null)
                        {
                            var results = transition.Results.ToArray();
                            results[replicateIndex] = SetChromInfoAnnotations(chromSet, (TransitionResultRef) resultRef,
                                results[replicateIndex], annotations);
                            docNode = ((TransitionDocNode) docNode).ChangeResults(
                                new Results<TransitionChromInfo>(results));
                        }
                        else
                        {
                            throw AnnotationsNotSupported(resultRef);
                        }
                    }
                    return (SrmDocument) document.ReplaceChild(identityPath.Parent, docNode);
                }
            }
            throw ElementNotFoundException(resultRef);
        }

        private ChromInfoList<TChromInfo> SetChromInfoAnnotations<TDocNode, TChromInfo>(ChromatogramSet chromatogramSet,
            ResultRef<TDocNode, TChromInfo> resultRef, ChromInfoList<TChromInfo> chromInfoList,
            Annotations annotations) where TDocNode : DocNode where TChromInfo : ChromInfo
        {
            int i = IndexOfChromInfo(chromatogramSet, resultRef, chromInfoList);
            if (i < 0)
            {
                throw ElementNotFoundException(resultRef);
            }
            var newChromInfoList = chromInfoList.ToArray();
            newChromInfoList[i] = resultRef.ChangeAnnotations(newChromInfoList[i], annotations);
            return new ChromInfoList<TChromInfo>(newChromInfoList);
        }

        private IdentityPath ToIdentityPath(NodeRef nodeRef)
        {
            IdentityPath identityPath;
            if (_identityPaths.TryGetValue(nodeRef, out identityPath))
            {
                return identityPath;
            }
            identityPath = nodeRef.ToIdentityPath(Document);
            if (identityPath == null)
            {
                throw ElementNotFoundException(nodeRef);
            }
            _identityPaths.Add(nodeRef, identityPath);
            return identityPath;
        }

        private static Exception ElementNotFoundException(ElementRef elementRef)
        {
            return new InvalidDataException(string.Format(Resources.DocumentAnnotations_ElementNotFoundException_Could_not_find_element___0___, elementRef));
        }

        private static Exception AnnotationDoesNotApplyException(string name, ElementRef elementRef)
        {
            return new InvalidDataException(string.Format(Resources.DocumentAnnotations_AnnotationDoesNotApplyException_Annotation___0___does_not_apply_to_element___1___,
                name, elementRef));
        }

        private static Exception AnnotationsNotSupported(ElementRef elementRef)
        {
            throw new InvalidOperationException(String.Format(Resources.DocumentAnnotations_AnnotationsNotSupported_The_element___0___cannot_have_annotations_, elementRef));
        }

        private static Annotations SetAnnotationValue(CultureInfo cultureInfo, Annotations annotations, AnnotationDef annotationDef, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return annotations.RemoveAnnotation(annotationDef.Name);
            }
            string persistedValue;
            switch (annotationDef.Type)
            {
                case AnnotationDef.AnnotationType.number:
                    persistedValue = annotationDef.ToPersistedString(double.Parse(value, cultureInfo));
                    break;
                case AnnotationDef.AnnotationType.true_false:
                    if (false.ToString(cultureInfo) == value)
                    {
                        return annotations.RemoveAnnotation(annotationDef.Name);
                    }
                    persistedValue = annotationDef.ToPersistedString(true);
                    break;
                default:
                    persistedValue = value;
                    break;
            }
            return annotations.ChangeAnnotation(annotationDef.Name, persistedValue);
        }

        class Columns
        {
            public Columns(IList<AnnotationDef> annotationDefs)
            {
                LocatorColumn = 0;
                NoteColumn = 1;
                var annotationColumns = new AnnotationDef[annotationDefs.Count + 2];
                LocatorColumn = 0;
                NoteColumn = 1;
                annotationDefs.CopyTo(annotationColumns, 2);
                AnnotationColumns = ImmutableList.ValueOf(annotationColumns);
            }

            public Columns(IList<string> columnHeaders, IEnumerable<AnnotationDef> annotations)
            {
                var annotationsByName = annotations.ToDictionary(annotationDef => annotationDef.Name);
                // ReSharper disable CollectionNeverQueried.Local
                var annotationsRemaining = new HashSet<string>(annotationsByName.Keys);
                // ReSharper restore CollectionNeverQueried.Local
                var annotationColumns = new AnnotationDef[columnHeaders.Count];
                for (int i = 0; i < columnHeaders.Count; i++)
                {
                    var fieldName = columnHeaders[i];
                    if (fieldName == COLUMN_NOTE && !NoteColumn.HasValue)
                    {
                        NoteColumn = i;
                    }
                    else if (fieldName == COLUMN_LOCATOR && !LocatorColumn.HasValue)
                    {
                        LocatorColumn = i;
                    }
                    else
                    {
                        AnnotationDef annotationDef;
                        if (!annotationsByName.TryGetValue(fieldName, out annotationDef))
                        {
                            throw new InvalidDataException(String.Format(Resources.Columns_Columns_Unrecognized_column___0__, fieldName));
                        }
                        if (!annotationsRemaining.Remove(fieldName))
                        {
                            throw new InvalidDataException(String.Format(Resources.Columns_Columns_Duplicate_column___0__, fieldName));
                        }
                        annotationColumns[i] = annotationDef;
                    }
                }
                AnnotationColumns = ImmutableList.ValueOf(annotationColumns);
                if (!LocatorColumn.HasValue)
                {
                    throw new InvalidDataException(string.Format(Resources.Columns_Columns_Missing_column___0__, COLUMN_LOCATOR));
                }
            }
            public int? NoteColumn
            {
                get;
                private set;
            }

            public int? LocatorColumn { get; private set; }
            public ImmutableList<AnnotationDef> AnnotationColumns { get; private set; }

            public string[] GetColumnHeaders()
            {
                string[] result = new string[AnnotationColumns.Count];
                for (int i = 0; i < result.Length; i++)
                {
                    if (i == LocatorColumn)
                    {
                        result[i] = COLUMN_LOCATOR;
                    }
                    else if (i == NoteColumn)
                    {
                        result[i] = COLUMN_NOTE;
                    }
                    else if (AnnotationColumns[i] != null)
                    {
                        result[i] = AnnotationColumns[i].Name;
                    }
                }
                return result;
            }

            public object[] GetRow(ElementRef elementRef, Annotations annotations)
            {
                object[] result = new object[AnnotationColumns.Count];
                var annotationTargets = elementRef.AnnotationTargets;
                for (int i = 0; i < result.Length; i++)
                {
                    if (i == LocatorColumn)
                    {
                        result[i] = elementRef;
                    }
                    else if (i == NoteColumn)
                    {
                        result[i] = annotations.Note;
                    }
                    else if (AnnotationColumns[i] != null && !AnnotationColumns[i].AnnotationTargets.Intersect(annotationTargets).IsEmpty)
                    {
                        result[i] = annotations.GetAnnotation(AnnotationColumns[i]);
                    }
                }
                return result;
            }

            public ElementLocator GetElementLocator(IList<string> row)
            {
                return ElementLocator.Parse(row[LocatorColumn.Value]);
            }

            public Annotations ReadAnnotations(CultureInfo cultureInfo, ElementRef elementRef, Annotations annotations, IList<string> row)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    AnnotationDef.AnnotationTargetSet targets = null;
                    if (i == NoteColumn)
                    {
                        annotations = annotations.ChangeNote(row[i]);
                        targets = NOTE_TARGETS;
                    }
                    else if (AnnotationColumns[i] != null)
                    {
                        annotations = SetAnnotationValue(cultureInfo, annotations, AnnotationColumns[i], row[i]);
                        targets = AnnotationColumns[i].AnnotationTargets;
                    }
                    if (!string.IsNullOrEmpty(row[i]))
                    {
                        if (targets != null && targets.Intersect(elementRef.AnnotationTargets).IsEmpty)
                        {
                            throw AnnotationDoesNotApplyException(GetColumnHeaders()[i], elementRef);
                        }
                    }
                }
                return annotations;
            }
        }

        public Annotations GetAnnotations(SrmDocument document, ElementRef elementRef)
        {
            var nodeRef = elementRef as NodeRef;
            if (nodeRef != null)
            {
                return document.FindNode(ToIdentityPath(nodeRef)).Annotations;
            }
            var replicateRef = elementRef as ReplicateRef;
            if (replicateRef != null)
            {
                var chromatogramSet = replicateRef.FindChromatogramSet(document);
                if (chromatogramSet == null)
                {
                    throw ElementNotFoundException(elementRef);
                }
                return chromatogramSet.Annotations;
            }
            var resultRef = elementRef as ResultRef;
            if (resultRef != null)
            {
                return GetResultAnnotations(document, resultRef);
            }
            throw AnnotationsNotSupported(elementRef);
        }

        private Annotations GetResultAnnotations(SrmDocument document, ResultRef resultRef)
        {
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                throw ElementNotFoundException(resultRef);
            }
            var nodeRef = (NodeRef)resultRef.Parent;
            var identityPath = ToIdentityPath(nodeRef);
            var docNode = document.FindNode(identityPath);
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromSet = measuredResults.Chromatograms[replicateIndex];
                if (resultRef.Matches(chromSet))
                {
                    var transitionGroup = docNode as TransitionGroupDocNode;
                    if (transitionGroup != null)
                    {
                        if (transitionGroup.Results == null || transitionGroup.Results.Count <= replicateIndex)
                        {
                            throw ElementNotFoundException(resultRef);
                        }
                        int i = IndexOfChromInfo(chromSet, (PrecursorResultRef) resultRef,
                            transitionGroup.Results[replicateIndex]);
                        if (i < 0)
                        {
                            throw ElementNotFoundException(resultRef);
                        }
                        return transitionGroup.Results[replicateIndex][i].Annotations;
                    }
                    else
                    {
                        var transition = docNode as TransitionDocNode;
                        if (transition != null)
                        {
                            if (transition.Results == null || transition.Results.Count <= replicateIndex)
                            {
                                throw ElementNotFoundException(resultRef);
                            }
                            int i = IndexOfChromInfo(chromSet, (TransitionResultRef) resultRef,
                                transition.Results[replicateIndex]);
                            if (i < 0)
                            {
                                throw ElementNotFoundException(resultRef);
                            }
                            return transition.Results[replicateIndex][i].Annotations;
                        }
                        else
                        {
                            throw AnnotationsNotSupported(resultRef);
                        }
                    }
                }
            }
            throw ElementNotFoundException(resultRef);
        }

        private int IndexOfChromInfo<TDocNode, TChromInfo>(ChromatogramSet chromatogramSet,
            ResultRef<TDocNode, TChromInfo> resultRef, ChromInfoList<TChromInfo> chromInfoList) 
            where TDocNode : DocNode where TChromInfo : ChromInfo
        {
            var chromFileInfo = resultRef.FindChromFileInfo(chromatogramSet);
            for (int i = 0; i < chromInfoList.Count; i++)
            {
                var chromInfo = chromInfoList[i];
                if (!ReferenceEquals(chromInfo.FileId, chromFileInfo.Id))
                {
                    continue;
                }
                if (resultRef.GetOptimizationStep(chromInfo) != resultRef.OptimizationStep)
                {
                    continue;
                }
                return i;
            }
            return -1;
        }
    }
}
