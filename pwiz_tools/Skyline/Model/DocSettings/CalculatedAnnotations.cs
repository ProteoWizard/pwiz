using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    public class CalculatedAnnotations
    {
        private static readonly AnnotationDef.AnnotationTargetSet ANNOTATION_TARGET_SET_DOCNODES =
            AnnotationDef.AnnotationTargetSet.OfValues(
                AnnotationDef.AnnotationTarget.protein,
                AnnotationDef.AnnotationTarget.peptide,
                AnnotationDef.AnnotationTarget.precursor,
                AnnotationDef.AnnotationTarget.transition,
                AnnotationDef.AnnotationTarget.precursor_result,
                AnnotationDef.AnnotationTarget.transition_result);
        private static readonly AnnotationDef.AnnotationTargetSet ANNOTATION_TARGET_SET_ALL
            = AnnotationDef.AnnotationTargetSet.OfValues(
                Enum.GetValues(typeof(AnnotationDef.AnnotationTarget)).Cast<AnnotationDef.AnnotationTarget>());

        public static SrmDocument UpdateDocument(SrmDocument document)
        {
            var calculatedAnnotations = GetCalculatedAnnotations(document, ANNOTATION_TARGET_SET_ALL);
            if (calculatedAnnotations == null)
            {
                return document;
            }

            var newMeasuredResults = calculatedAnnotations.UpdateReplicateAnnotations(document.Settings.MeasuredResults);
            if (!ReferenceEquals(document.Settings.MeasuredResults, newMeasuredResults))
            {
                document = document.ChangeMeasuredResults(newMeasuredResults);
            }

            var newChildren = document.MoleculeGroups.Select(mg => calculatedAnnotations.UpdateMoleculeGroup(mg)).ToArray();
            if (!ReferenceEquals(document.Children, newChildren))
            {
                document = (SrmDocument) document.ChangeChildren(newChildren);
            }

            return document;
        }

        public static MeasuredResults UpdateMeasuredResults(SrmDocument document, MeasuredResults measuredResults)
        {
            if (measuredResults == null)
            {
                return null;
            }

            var calculateAnnotations = GetCalculatedAnnotations(document,
                AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate));
            if (calculateAnnotations == null)
            {
                return measuredResults;
            }

            return calculateAnnotations.UpdateReplicateAnnotations(measuredResults);
        }

        public static IList<DocNode> UpdateMoleculeGroups(SrmDocument document,
            IList<DocNode> moleculeGroups)
        {
            var calculateAnnotations = GetCalculatedAnnotations(document, ANNOTATION_TARGET_SET_DOCNODES);
            if (calculateAnnotations == null)
            {
                return moleculeGroups;
            }

            var newNodes = moleculeGroups.Cast<PeptideGroupDocNode>().Select(calculateAnnotations.UpdateMoleculeGroup).ToArray();
            if (ArrayUtil.ReferencesEqual(moleculeGroups, newNodes))
            {
                return moleculeGroups;
            }

            return newNodes;
        }

        public static CalculatedAnnotations GetCalculatedAnnotations(SrmDocument document,
            AnnotationDef.AnnotationTargetSet targets)
        {
            var calculatedAnnotations =
                document.Settings.DataSettings.AnnotationDefs
                    .Where(annotationDef => !string.IsNullOrEmpty(annotationDef.Expression) && !annotationDef.AnnotationTargets.Intersect(targets).IsEmpty)
                    .ToArray();
            if (calculatedAnnotations.Length == 0)
            {
                return null;
            }
            return new CalculatedAnnotations(document, targets);
        }


        public const string ERROR_VALUE = @"#ERROR#";
        private IDictionary<AnnotationDef.AnnotationTarget, AnnotationUpdater> _annotationUpdaters;
        private TransitionResultUpdater _transitionResultUpdater;
        private PrecursorResultUpdater _precursorResultUpdater;

        public CalculatedAnnotations(SrmDocument document, AnnotationDef.AnnotationTargetSet annotationTargetSet)
        {
            SrmDocument = document;
            SkylineDataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
            _annotationUpdaters = new Dictionary<AnnotationDef.AnnotationTarget, AnnotationUpdater>();
            var calculatedAnnotations = document.Settings.DataSettings.AnnotationDefs
                .Where(def => !string.IsNullOrEmpty(def.Expression)).ToArray();
            foreach (var target in annotationTargetSet)
            {
                var annotations = ImmutableList.ValueOf(calculatedAnnotations.Where(def => def.AnnotationTargets.Contains(target)));
                if (annotations.Count == 0)
                {
                    continue;
                }
                _annotationUpdaters[target] = AnnotationUpdater.MakeAnnotationUpdater(SkylineDataSchema, RowTypeFromAnnotationTarget(target), annotations);
            }

            AnnotationUpdater transitionResultAnnotationUpdater;
            if (_annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.transition_result,
                out transitionResultAnnotationUpdater))
            {
                _transitionResultUpdater = new TransitionResultUpdater
                {
                    AnnotationUpdater = transitionResultAnnotationUpdater,
                    SkylineDataSchema = SkylineDataSchema
                };
            }

            AnnotationUpdater precursorResultAnnotationUpdater;
            if (_annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.precursor_result,
                out precursorResultAnnotationUpdater))
            {
                _precursorResultUpdater = new PrecursorResultUpdater
                {
                    AnnotationUpdater = precursorResultAnnotationUpdater,
                    SkylineDataSchema = SkylineDataSchema
                };
            }

            RecurseTransitions = _annotationUpdaters.ContainsKey(AnnotationDef.AnnotationTarget.transition)
                                 || transitionResultAnnotationUpdater != null;
            RecursePrecursors =
                RecurseTransitions || _annotationUpdaters.ContainsKey(AnnotationDef.AnnotationTarget.precursor)
                                   || precursorResultAnnotationUpdater != null;
            RecurseMolecules =
                RecursePrecursors || _annotationUpdaters.ContainsKey(AnnotationDef.AnnotationTarget.peptide);
        }

        public SrmDocument SrmDocument { get; private set; }
        public SkylineDataSchema SkylineDataSchema { get; private set; }
        public AnnotationDef.AnnotationTargetSet AnnotationTargetSet { get; private set; }
        public bool RecurseMoleculeGroups { get; private set; }
        public bool RecurseMolecules { get; private set; }
        public bool RecursePrecursors { get; private set; }
        public bool RecurseTransitions { get; private set; }

        public MeasuredResults UpdateReplicateAnnotations(MeasuredResults measuredResults)
        {
            if (measuredResults == null)
            {
                return null;
            }

            AnnotationUpdater updater;
            if (!_annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.replicate, out updater))
            {
                return measuredResults;
            }

            var chromatograms = measuredResults.Chromatograms.ToArray();
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromatogramSet = chromatograms[replicateIndex];
                var replicate = new Replicate(SkylineDataSchema, replicateIndex);
                var annotations = updater.UpdateAnnotations(chromatogramSet.Annotations, replicate);
                if (!Equals(annotations, chromatogramSet.Annotations))
                {
                    chromatograms[replicateIndex] = chromatogramSet.ChangeAnnotations(annotations);
                }
            }

            if (ArrayUtil.ReferencesEqual(measuredResults.Chromatograms, chromatograms))
            {
                return measuredResults;
            }

            return measuredResults.ChangeChromatograms(chromatograms);
        }

        public PeptideGroupDocNode UpdateMoleculeGroup(PeptideGroupDocNode peptideGroupDocNode)
        {
            AnnotationUpdater updater;
            var identityPath = new IdentityPath(peptideGroupDocNode.PeptideGroup);
            if (_annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.protein, out updater))
            {
                var protein = new Protein(SkylineDataSchema, identityPath);
                var annotations = updater.UpdateAnnotations(peptideGroupDocNode.Annotations, protein);
                if (!Equals(annotations, peptideGroupDocNode.Annotations))
                {
                    peptideGroupDocNode = (PeptideGroupDocNode) peptideGroupDocNode.ChangeAnnotations(annotations);
                }
            }

            if (!RecurseMolecules)
            {
                return peptideGroupDocNode;
            }


            var newChildren = peptideGroupDocNode.Molecules
                .Select(peptideDocNode => UpdateMolecule(identityPath, peptideDocNode)).ToArray();
            if (!ArrayUtil.ReferencesEqual(peptideGroupDocNode.Children, newChildren))
            {
                peptideGroupDocNode = (PeptideGroupDocNode) peptideGroupDocNode.ChangeChildren(newChildren);
            }

            return peptideGroupDocNode;
        }

        public PeptideDocNode UpdateMolecule(IdentityPath parent, PeptideDocNode peptideDocNode)
        {
            AnnotationUpdater updater;
            _annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.peptide, out updater);
            var identityPath = new IdentityPath(parent, peptideDocNode.Peptide);
            if (updater != null)
            {
                var peptide = new Databinding.Entities.Peptide(SkylineDataSchema, new IdentityPath(parent, peptideDocNode.Peptide));
                var annotations = updater.UpdateAnnotations(peptideDocNode.Annotations, peptide);
                if (!Equals(annotations, peptideDocNode.Annotations))
                {
                    peptideDocNode = (PeptideDocNode) peptideDocNode.ChangeAnnotations(annotations);
                }
            }

            if (!RecursePrecursors)
            {
                return peptideDocNode;
            }

            var newChildren = peptideDocNode.TransitionGroups.Select(tg => UpdatePrecursor(identityPath, tg)).ToArray();
            if (!ArrayUtil.ReferencesEqual(peptideDocNode.Children, newChildren))
            {
                peptideDocNode = (PeptideDocNode) peptideDocNode.ChangeChildren(newChildren);
            }

            return peptideDocNode;
        }

        public TransitionGroupDocNode UpdatePrecursor(IdentityPath parent, TransitionGroupDocNode precursorDocNode)
        {
            AnnotationUpdater updater;
            _annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.precursor, out updater);
            IdentityPath identityPath = new IdentityPath(parent, precursorDocNode.TransitionGroup);
            if (updater != null || _precursorResultUpdater != null)
            {
                var precursor = new Precursor(SkylineDataSchema, identityPath);
                if (updater != null)
                {
                    var annotations = updater.UpdateAnnotations(precursorDocNode.Annotations, precursor);
                    if (!Equals(annotations, precursorDocNode.Annotations))
                    {
                        precursorDocNode = (TransitionGroupDocNode)precursorDocNode.ChangeAnnotations(annotations);
                    }
                }

                if (_precursorResultUpdater != null)
                {
                    var newResults = _precursorResultUpdater.Update(precursorDocNode.Results, precursor.Results);
                    precursorDocNode = precursorDocNode.ChangeResults(newResults);
                }
            }

            if (!RecurseTransitions)
            {
                return precursorDocNode;
            }

            var newChildren = precursorDocNode.Transitions
                .Select(transition => UpdateTransition(identityPath, transition)).ToArray();
            if (!ArrayUtil.ReferencesEqual(precursorDocNode.Children, newChildren))
            {
                precursorDocNode = (TransitionGroupDocNode) precursorDocNode.ChangeChildren(newChildren);
            }

            return precursorDocNode;
        }

        public TransitionDocNode UpdateTransition(IdentityPath parent, TransitionDocNode transitionDocNode)
        {
            AnnotationUpdater updater;
            _annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.transition, out updater);
            IdentityPath identityPath = new IdentityPath(parent, transitionDocNode.Transition);
            var transition = new Databinding.Entities.Transition(SkylineDataSchema, identityPath);
            if (updater != null)
            {
                var annotations = updater.UpdateAnnotations(transitionDocNode.Annotations, transition);
                if (!Equals(annotations, transitionDocNode.Annotations))
                {
                    transitionDocNode = (TransitionDocNode)transitionDocNode.ChangeAnnotations(annotations);
                }
            }

            if (_precursorResultUpdater != null)
            {
                var newResults = _transitionResultUpdater.Update(transitionDocNode.Results, transition.Results);
                transitionDocNode = transitionDocNode.ChangeResults(newResults);
            }

            return transitionDocNode;
        }

        public static Type RowTypeFromAnnotationTarget(AnnotationDef.AnnotationTarget annotationTarget)
        {
            switch (annotationTarget)
            {
                case AnnotationDef.AnnotationTarget.protein:
                    return typeof(Protein);
                case AnnotationDef.AnnotationTarget.peptide:
                    return typeof(Databinding.Entities.Peptide);
                case AnnotationDef.AnnotationTarget.precursor:
                    return typeof(Precursor);
                case AnnotationDef.AnnotationTarget.transition:
                    return typeof(Databinding.Entities.Transition);
                case AnnotationDef.AnnotationTarget.replicate:
                    return typeof(Replicate);
                case AnnotationDef.AnnotationTarget.precursor_result:
                    return typeof(PrecursorResult);
                case AnnotationDef.AnnotationTarget.transition_result:
                    return typeof(TransitionResult);
                default:
                    return null;
            }
        }

        private static ColumnDescriptor GetColumnDescriptor(ColumnDescriptor columnDescriptor,
            AnnotationDef annotationDef)
        {
            PropertyPath propertyPath;
            try
            {
                propertyPath = PropertyPath.Parse(annotationDef.Expression);
            }
            catch (Exception)
            {
                return null;
            }
            return ResolvePath(columnDescriptor, propertyPath);
        }

        private static ColumnDescriptor ResolvePath(ColumnDescriptor rootColumn, PropertyPath propertyPath)
        {
            if (propertyPath.IsRoot)
            {
                return rootColumn;
            }

            if (propertyPath.IsLookup)
            {
                return null;
            }

            var parent = ResolvePath(rootColumn, propertyPath.Parent);
            if (parent == null)
            {
                return null;
            }
            return parent.ResolveChild(propertyPath.Name);
        }

        private class AnnotationUpdater
        {
            public Annotations UpdateAnnotations(Annotations annotations, SkylineObject skylineObject)
            {
                var rowItem = new RowItem(skylineObject);
                for (int iAnnotation = 0; iAnnotation < AnnotationDefs.Count; iAnnotation++)
                {
                    var columnDescriptor = ColumnDescriptors[iAnnotation];
                    object annotationValue;
                    if (columnDescriptor == null)
                    {
                        annotationValue = @"#NAME#";
                    }
                    else
                    {
                        try
                        {
                            annotationValue = columnDescriptor.GetPropertyValue(rowItem, null);
                        }
                        catch (Exception)
                        {
                            annotationValue = @"#ERROR#";
                        }
                    }

                    annotations = annotations.ChangeAnnotation(AnnotationDefs[iAnnotation], annotationValue);
                }

                return annotations;
            }

            public ImmutableList<AnnotationDef> AnnotationDefs { get; private set; }
            public ImmutableList<ColumnDescriptor> ColumnDescriptors { get; private set; }

            public static AnnotationUpdater MakeAnnotationUpdater(SkylineDataSchema dataSchema, Type objectType,
                IEnumerable<AnnotationDef> annotationDefs)
            {
                var rootColumn = ColumnDescriptor.RootColumn(dataSchema, objectType);
                var result = new AnnotationUpdater();
                result.AnnotationDefs = ImmutableList.ValueOf(annotationDefs);
                result.ColumnDescriptors = ImmutableList.ValueOf(result.AnnotationDefs
                    .Select(annotationDef => GetColumnDescriptor(rootColumn, annotationDef)));
                return result;
            }
        }

        private abstract class ResultUpdater<TItem, TResult> where TItem : ChromInfo where TResult : SkylineObject
        {
            public SkylineDataSchema SkylineDataSchema { get; set; }
            public AnnotationUpdater AnnotationUpdater { get; set; }
            public Results<TItem> Update(Results<TItem> results, IDictionary<ResultKey, TResult> resultObjects)
            {
                if (results == null)
                {
                    return null;
                }

                var newChromInfos = new List<IList<TItem>>();
                for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
                {
                    var replicate = new Replicate(SkylineDataSchema, replicateIndex);
                    var list = new List<TItem>();
                    for (int fileIndex = 0; fileIndex < results[replicateIndex].Count; fileIndex++)
                    {
                        var chromInfo = results[replicateIndex][fileIndex];
                        if (chromInfo != null)
                        {
                            var resultKey = new ResultKey(replicate, fileIndex);
                            TResult resultObject;
                            if (resultObjects.TryGetValue(resultKey, out resultObject))
                            {
                                var newAnnotations =
                                    AnnotationUpdater.UpdateAnnotations(GetAnnotations(chromInfo), resultObject);
                                chromInfo = ChangeAnnotations(chromInfo, newAnnotations);
                            }
                        }

                        list.Add(chromInfo);
                    }
                    newChromInfos.Add(list);
                }

                return Results<TItem>.Merge(results, newChromInfos);
            }

            protected abstract Annotations GetAnnotations(TItem item);
            protected abstract TItem ChangeAnnotations(TItem item, Annotations newAnnotations);
        }

        private class TransitionResultUpdater : ResultUpdater<TransitionChromInfo, TransitionResult>
        {
            protected override Annotations GetAnnotations(TransitionChromInfo item)
            {
                return item.Annotations;
            }

            protected override TransitionChromInfo ChangeAnnotations(TransitionChromInfo item, Annotations newAnnotations)
            {
                return item.ChangeAnnotations(newAnnotations);
            }
        }

        private class PrecursorResultUpdater : ResultUpdater<TransitionGroupChromInfo, PrecursorResult>
        {
            protected override Annotations GetAnnotations(TransitionGroupChromInfo item)
            {
                return item.Annotations;
            }

            protected override TransitionGroupChromInfo ChangeAnnotations(TransitionGroupChromInfo item, Annotations newAnnotations)
            {
                return item.ChangeAnnotations(newAnnotations);
            }
        }
    }
}
