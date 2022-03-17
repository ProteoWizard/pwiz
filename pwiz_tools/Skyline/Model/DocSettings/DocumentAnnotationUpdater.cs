/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    public class DocumentAnnotationUpdater
    {
        private IDictionary<AnnotationDef.AnnotationTarget, AnnotationUpdater> _annotationUpdaters;
        private TransitionResultUpdater _transitionResultUpdater;
        private PrecursorResultUpdater _precursorResultUpdater;

        public static SrmDocument UpdateAnnotations(SrmDocument document, IProgressMonitor progressMonitor, IProgressStatus status)
        {
            if (document.Settings.DataSettings.AnnotationDefs.All(def => def.Expression == null))
            {
                return document;
            }

            if (progressMonitor != null)
            {
                progressMonitor.UpdateProgress(status.ChangeMessage(Resources.DocumentAnnotationUpdater_UpdateAnnotations_Updating_calculated_annotations));
            }
            DocumentAnnotationUpdater updater = new DocumentAnnotationUpdater(document, progressMonitor);
            return updater.UpdateDocument(document);
        }

        public DocumentAnnotationUpdater(SrmDocument document, IProgressMonitor progressMonitor)
        {
            SkylineDataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
            _annotationUpdaters = new Dictionary<AnnotationDef.AnnotationTarget, AnnotationUpdater>();
            var calculatedAnnotations = document.Settings.DataSettings.AnnotationDefs
                .Where(def => null != def.Expression).ToArray();
            foreach (AnnotationDef.AnnotationTarget target in Enum.GetValues(typeof(AnnotationDef.AnnotationTarget)))
            {
                var annotations = ImmutableList.ValueOf(calculatedAnnotations.Where(def => def.AnnotationTargets.Contains(target)));
                if (annotations.Count == 0)
                {
                    continue;
                }
                _annotationUpdaters[target] = new AnnotationUpdater(annotations);
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

        public IProgressMonitor ProgressMonitor { get; private set; }
        public SkylineDataSchema SkylineDataSchema { get; private set; }

        public SrmDocument UpdateDocument(SrmDocument document)
        {
            if (document.Settings.HasResults && _annotationUpdaters.ContainsKey(AnnotationDef.AnnotationTarget.replicate))
            {
                var newMeasuredResults = UpdateReplicateAnnotations(document.MeasuredResults);
                document = document.ChangeMeasuredResults(newMeasuredResults);
            }

            var newChildren = document.MoleculeGroups.Select(UpdateMoleculeGroup).ToArray();
            document = (SrmDocument)document.ChangeChildren(newChildren);
            return document;
        }


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
            CheckCancelled();
            AnnotationUpdater updater;
            var identityPath = new IdentityPath(peptideGroupDocNode.PeptideGroup);
            if (_annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.protein, out updater))
            {
                var protein = new Protein(SkylineDataSchema, identityPath);
                var annotations = updater.UpdateAnnotations(peptideGroupDocNode.Annotations, protein);
                if (!Equals(annotations, peptideGroupDocNode.Annotations))
                {
                    peptideGroupDocNode = (PeptideGroupDocNode)peptideGroupDocNode.ChangeAnnotations(annotations);
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
                peptideGroupDocNode = (PeptideGroupDocNode)peptideGroupDocNode.ChangeChildren(newChildren);
            }

            return peptideGroupDocNode;
        }

        public PeptideDocNode UpdateMolecule(IdentityPath parent, PeptideDocNode peptideDocNode)
        {
            CheckCancelled();
            AnnotationUpdater updater;
            _annotationUpdaters.TryGetValue(AnnotationDef.AnnotationTarget.peptide, out updater);
            var identityPath = new IdentityPath(parent, peptideDocNode.Peptide);
            if (updater != null)
            {
                var peptide = new Databinding.Entities.Peptide(SkylineDataSchema, new IdentityPath(parent, peptideDocNode.Peptide));
                var annotations = updater.UpdateAnnotations(peptideDocNode.Annotations, peptide);
                if (!Equals(annotations, peptideDocNode.Annotations))
                {
                    peptideDocNode = (PeptideDocNode)peptideDocNode.ChangeAnnotations(annotations);
                }
            }

            if (!RecursePrecursors)
            {
                return peptideDocNode;
            }

            var newChildren = peptideDocNode.TransitionGroups.Select(tg => UpdatePrecursor(identityPath, tg)).ToArray();
            if (!ArrayUtil.ReferencesEqual(peptideDocNode.Children, newChildren))
            {
                peptideDocNode = (PeptideDocNode)peptideDocNode.ChangeChildren(newChildren);
            }

            return peptideDocNode;
        }

        public TransitionGroupDocNode UpdatePrecursor(IdentityPath parent, TransitionGroupDocNode precursorDocNode)
        {
            CheckCancelled();
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
                precursorDocNode = (TransitionGroupDocNode)precursorDocNode.ChangeChildren(newChildren);
            }

            return precursorDocNode;
        }

        public TransitionDocNode UpdateTransition(IdentityPath parent, TransitionDocNode transitionDocNode)
        {
            CheckCancelled();
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

        private class AnnotationUpdater
        {
            public AnnotationUpdater(IEnumerable<AnnotationDef> annotationDefs)
            {
                AnnotationDefs = ImmutableList.ValueOf(annotationDefs);
            }
            public Annotations UpdateAnnotations(Annotations annotations, SkylineObject skylineObject)
            {
                foreach (var annotationDef in AnnotationDefs)
                {
                    annotations =
                        annotations.ChangeAnnotation(annotationDef, skylineObject.GetAnnotation(annotationDef));
                }

                return annotations;
            }

            public ImmutableList<AnnotationDef> AnnotationDefs { get; private set; }
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
                        var resultKey = new ResultKey(replicate, fileIndex);
                        TResult resultObject;
                        if (resultObjects.TryGetValue(resultKey, out resultObject))
                        {
                            var newAnnotations =
                                AnnotationUpdater.UpdateAnnotations(GetAnnotations(chromInfo), resultObject);
                            chromInfo = ChangeAnnotations(chromInfo, newAnnotations);
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

        private void CheckCancelled()
        {
            if (ProgressMonitor != null && ProgressMonitor.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }
    }
}
