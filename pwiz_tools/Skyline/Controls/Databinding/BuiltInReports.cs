/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using JetBrains.Annotations;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Controls.Databinding
{
    public class BuiltInReports
    {
        public BuiltInReports(Model.SrmDocument document)
        {
            Document = document;
            HasCustomIons = document.CustomMolecules.Any();
            HasOnlyCustomIons = HasCustomIons && !document.Peptides.Any();
        }
        public Model.SrmDocument Document { get; }
        public bool HasCustomIons { get; }
        public bool HasOnlyCustomIons { get; }

        /// <summary>
        /// Returns the list of columns that should be shown on a report with rows of the specified type.
        /// Returns null if this class does not know anything about that type of rows.
        /// </summary>
        [CanBeNull]
        public IEnumerable<PropertyPath> GetDefaultColumns(Type rowType)
        {
            AnnotationDef.AnnotationTarget? annotationTarget = null;
            List<PropertyPath> propertyPaths = new List<PropertyPath>();
            if (rowType == typeof(TransitionResult))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.transition_result;
                propertyPaths.AddRange(new[]
                {
                    Property(nameof(TransitionResult.PrecursorResult))
                        .Property(nameof(PrecursorResult.PeptideResult)).Property(nameof(PeptideResult.ResultFile))
                        .Property(nameof(ResultFile.Replicate)),
                    Property(nameof(TransitionResult.Note)),
                    Property(nameof(TransitionResult.RetentionTime)),
                    Property(nameof(TransitionResult.Fwhm)),
                    Property(nameof(TransitionResult.StartTime)),
                    Property(nameof(TransitionResult.EndTime)),
                    Property(nameof(TransitionResult.Background)),
                    Property(nameof(TransitionResult.AreaRatio)),
                    Property(nameof(TransitionResult.Height)),
                    Property(nameof(TransitionResult.PeakRank))
                });
            }
            else if (rowType == typeof(PrecursorResult))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.precursor_result;
                propertyPaths.AddRange(new[]
                    {

                        Property(nameof(PrecursorResult.PeptideResult)).Property(nameof(PeptideResult.ResultFile))
                            .Property(nameof(ResultFile.Replicate)),
                        Property(nameof(PrecursorResult.Note)),
                        Property(nameof(PrecursorResult.PrecursorPeakFoundRatio)),
                        Property(nameof(PrecursorResult.BestRetentionTime)),
                        Property(nameof(PrecursorResult.MaxFwhm)),
                        Property(nameof(PrecursorResult.MinStartTime)),
                        Property(nameof(PrecursorResult.MaxEndTime)),
                        Property(nameof(PrecursorResult.TotalArea)),
                        Property(nameof(PrecursorResult.TotalBackground)),
                        Property(nameof(PrecursorResult.TotalAreaRatio)),
                        Property(nameof(PrecursorResult.MaxHeight)),
                        Property(nameof(PrecursorResult.LibraryDotProduct)),
                        Property(nameof(PrecursorResult.IsotopeDotProduct))
                    }
                );
            }
            else if (rowType == typeof(PeptideResult))
            {
                PropertyPath ppReplicate =
                    Property(nameof(PeptideResult.ResultFile)).Property(nameof(ResultFile.Replicate));
                propertyPaths.AddRange(new[]
                {
                    ppReplicate,
                    Property(nameof(PeptideResult.PeptidePeakFoundRatio)),
                    Property(nameof(PeptideResult.PeptideRetentionTime)),
                    Property(nameof(PeptideResult.RatioToStandard))
                });
                propertyPaths.AddRange(GetAnnotations(AnnotationDef.AnnotationTarget.replicate)
                    .Select(ppAnnotation => ppReplicate.Concat(ppAnnotation)));
            }
            else if (rowType == typeof(MultiTransitionResult))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.transition_result;
                propertyPaths.AddRange(new []
                {
                    Property(nameof(MultiTransitionResult.Note)),
                    Property(nameof(MultiTransitionResult.File))
                });
            }
            else if (rowType == typeof(MultiPrecursorResult))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.precursor_result;
                propertyPaths.AddRange(new []
                {
                    Property(nameof(MultiPrecursorResult.Note)),
                    Property(nameof(MultiPrecursorResult.File))
                });
            }
            else if (rowType == typeof(Protein))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.protein;
                propertyPaths.AddRange(new[]
                {
                    PropertyPath.Root,
                    Property(nameof(Protein.Description)),
                });
                if (!HasOnlyCustomIons)
                {
                    propertyPaths.AddRange(new[]
                    {
                        Property(nameof(Protein.Accession)),
                        Property(nameof(Protein.PreferredName)),
                        Property(nameof(Protein.Gene)),
                        Property(nameof(Protein.Species)),
                        Property(nameof(Protein.Sequence))
                    });
                }
                propertyPaths.Add(Property(nameof(Protein.Note)));
            }
            else if (rowType == typeof(Peptide))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.peptide;
                propertyPaths.AddRange(new[]
                {
                    PropertyPath.Root,
                    Property(nameof(Peptide.Protein)),

                });
                if (!HasOnlyCustomIons)
                {
                    propertyPaths.Add(Property(nameof(Peptide.ModifiedSequence)));
                }
                if (HasCustomIons)
                {
                    propertyPaths.AddRange(new[]
                    {
                        Property(nameof(Peptide.MoleculeName)),
                        Property(nameof(Peptide.MoleculeFormula))
                    });
                }
                propertyPaths.Add(Property(nameof(Peptide.StandardType)));
                if (!HasOnlyCustomIons)
                {
                    propertyPaths.AddRange(new[]
                    {
                        Property(nameof(Peptide.FirstPosition)),
                        Property(nameof(Peptide.LastPosition)),
                        Property(nameof(Peptide.MissedCleavages)),
                    });
                }
                propertyPaths.AddRange(new []
                {
                    Property(nameof(Peptide.PredictedRetentionTime)),
                    Property(nameof(Peptide.AverageMeasuredRetentionTime))
                });
                if (HasCustomIons)
                {
                    propertyPaths.AddRange(new[]
                    {
                        Property(nameof(Peptide.ExplicitRetentionTime)),
                        Property(nameof(Peptide.ExplicitRetentionTimeWindow))
                    });
                }
                propertyPaths.Add(Property(nameof(Peptide.Note)));
            }
            else if (rowType == typeof(Precursor))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.precursor;
                propertyPaths.AddRange(new[]
                {
                    PropertyPath.Root,
                    Property(nameof(Precursor.Peptide)),
                    Property(nameof(Precursor.Charge)),
                    Property(nameof(Precursor.IsotopeLabelType)),
                });
                if (HasCustomIons)
                {
                    propertyPaths.AddRange(new[]
                    {
                        Property(nameof(Precursor.IonName)),
                        Property(nameof(Precursor.IonFormula)),
                        Property(nameof(Precursor.NeutralFormula)),
                        Property(nameof(Precursor.Adduct))
                    });
                }
                propertyPaths.Add(Property(nameof(Precursor.Mz)));
                if (!HasOnlyCustomIons)
                {
                    propertyPaths.Add(Property(nameof(Precursor.ModifiedSequence)));
                }
                propertyPaths.AddRange(new[]
                {
                    Property(nameof(Precursor.PrecursorExplicitCollisionEnergy)),
                    Property(nameof(Precursor.Note)),
                    Property(nameof(Precursor.LibraryName)),
                    Property(nameof(Precursor.LibraryType)),
                    Property(nameof(Precursor.LibraryProbabilityScore))
                });
            }
            else if (rowType == typeof(Transition))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.transition;
                propertyPaths.AddRange(new[]
                {
                    PropertyPath.Root,
                    Property(nameof(Transition.Precursor)),
                    Property(nameof(Transition.ProductCharge)),
                    Property(nameof(Transition.ProductMz)),
                });
                if (!HasOnlyCustomIons)
                {
                    propertyPaths.Add(Property(nameof(Transition.FragmentIon)));
                }

                if (HasCustomIons)
                {
                    propertyPaths.AddRange(new[]
                    {
                        Property(nameof(Transition.ProductIonFormula)),
                        Property(nameof(Transition.ProductNeutralFormula)),
                        Property(nameof(Transition.ProductAdduct))
                    });
                }

                if (!HasOnlyCustomIons)
                {
                    propertyPaths.Add(Property(nameof(Transition.Losses)));
                }
                propertyPaths.AddRange(new[]
                {
                    Property(nameof(Transition.Quantitative)),
                    Property(nameof(Transition.Note))
                });
            }
            else if (rowType == typeof(Replicate))
            {
                annotationTarget = AnnotationDef.AnnotationTarget.replicate;
                propertyPaths.AddRange(new[]
                {
                    PropertyPath.Root,
                    Property(nameof(Replicate.SampleType)),
                    Property(nameof(Replicate.AnalyteConcentration))
                });
            }
            else
            {
                return null;
            }

            if (annotationTarget.HasValue)
            {
                propertyPaths.AddRange(GetAnnotations(annotationTarget.Value));
            }

            return propertyPaths;
        }

        private IEnumerable<PropertyPath> GetAnnotations(AnnotationDef.AnnotationTarget annotationTarget)
        {
            return Document.Settings.DataSettings.AnnotationDefs.Where(annotationDef =>
                annotationDef.AnnotationTargets.Contains(annotationTarget)).Select(annotationDef=>Property(AnnotationDef.ANNOTATION_PREFIX + annotationDef.Name));
        }

        private static PropertyPath Property(string name)
        {
            return PropertyPath.Root.Property(name);
        }
    } 
}
