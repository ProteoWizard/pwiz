using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Takes views which select from <see cref="Peptides"/>, <see cref="Precursors"/>, or <see cref="Transitions"/>, 
    /// <see cref="ReplicateList"/>  and (if possible) transforms them so that they are selecting from
    /// <see cref="SkylineDocument"/>.
    /// This is so that the DocumentGrid customize view form can be simplified.
    /// </summary>
    public class DocumentViewTransformer : IViewTransformer
    {
        // ReSharper disable NonLocalizedString
        private static readonly PropertyPath Proteins = PropertyPath.Root.Property("Proteins").LookupAllItems();
        private static readonly PropertyPath Peptides 
            = Proteins.Property("Peptides").LookupAllItems();

        private static readonly PropertyPath PeptidesPrecursors
            = Peptides.Property("Precursors").LookupAllItems();

        private static readonly PropertyPath PeptidesPrecursorsTransitions
            = PeptidesPrecursors.Property("Transitions").LookupAllItems();

        private static readonly PropertyPath Replicates
            = PropertyPath.Root.Property("Replicates").LookupAllItems();

        private static readonly PropertyPath ResultFiles
            = Replicates.Property("Files").LookupAllItems();

        private static readonly PropertyPath PeptideResults
            = Peptides.Property("Results").LookupAllItems().Property("Value");

        private static readonly PropertyPath PrecursorResults
            = PeptidesPrecursors.Property("Results").LookupAllItems().Property("Value");

        private static readonly PropertyPath TransitionResults
            = PeptidesPrecursorsTransitions.Property("Results").LookupAllItems().Property("Value");
        // ReSharper restore NonLocalizedString

        public KeyValuePair<ViewInfo, IEnumerable<PropertyPath>> TransformView(ViewInfo view, IEnumerable<PropertyPath> propertyPaths)
        {
            var newView = MakeIntoDocumentView(view, ref propertyPaths);
            return new KeyValuePair<ViewInfo, IEnumerable<PropertyPath>>(newView, propertyPaths);
        }

        public KeyValuePair<ViewInfo, IEnumerable<PropertyPath>> UntransformView(ViewInfo view, IEnumerable<PropertyPath> propertyPaths)
        {
            var newView = ConvertFromDocumentView(view, ref propertyPaths);
            return new KeyValuePair<ViewInfo, IEnumerable<PropertyPath>>(newView, propertyPaths);
        }

        public ViewInfo MakeIntoDocumentView(ViewInfo viewInfo, ref IEnumerable<PropertyPath> propertyPaths)
        {
            IList<KeyValuePair<PropertyPath, PropertyPath>> mapping;
            if (viewInfo.ParentColumn.PropertyType == typeof(Entities.Peptide))
            {
                mapping = MappingFromPeptides();
            }
            else if (viewInfo.ParentColumn.PropertyType == typeof (Precursor))
            {
                mapping = MappingFromPrecursors();
            }
            else if (viewInfo.ParentColumn.PropertyType == typeof (Entities.Transition))
            {
                mapping = MappingFromTransitions();
            }
            else if (viewInfo.ParentColumn.PropertyType == typeof (Protein))
            {
                mapping = MappingFromProteins();
            }
            else if (viewInfo.ParentColumn.PropertyType == typeof (Replicate))
            {
                mapping = MappingFromReplicates();
            }
            else
            {
                return viewInfo;
            }
            var viewSpec = viewInfo.GetViewSpec();
            viewSpec = MapViewSpec(mapping, viewSpec, typeof(SkylineDocument));
            if (null != propertyPaths)
            {
                propertyPaths = ImmutableList.ValueOf(propertyPaths.Select(path => MapPropertyPath(mapping, path)));
            }
            if (Equals(viewSpec.SublistId, SkylineViewContext.GetReplicateSublist(viewInfo.ParentColumn.PropertyType)))
            {
                viewSpec = viewSpec.SetSublistId(SkylineViewContext.GetReplicateSublist(typeof(SkylineDocument)));
            }
            return new ViewInfo(viewInfo.DataSchema, typeof(SkylineDocument), viewSpec);
        }

        public ViewInfo ConvertFromDocumentView(ViewInfo viewInfo, ref IEnumerable<PropertyPath> propertyPaths)
        {
            if (viewInfo.ParentColumn.PropertyType != typeof (SkylineDocument))
            {
                return viewInfo;
            }
            var viewSpec = viewInfo.GetViewSpec();
            IList<KeyValuePair<PropertyPath, PropertyPath>> mapping;
            Type newType;
            if (AnyColumnsWithPrefix(viewSpec, PeptidesPrecursorsTransitions))
            {
                mapping = ReverseMapping(MappingFromTransitions());
                newType = typeof (Entities.Transition);
            }
            else if (AnyColumnsWithPrefix(viewSpec, PeptidesPrecursors))
            {
                mapping = ReverseMapping(MappingFromPrecursors());
                newType = typeof (Precursor);
            }
            else if (AnyColumnsWithPrefix(viewSpec, Peptides))
            {
                mapping = ReverseMapping(MappingFromPeptides());
                newType = typeof (Entities.Peptide);
            }
            else if (AnyColumnsWithPrefix(viewSpec, Proteins))
            {
                mapping = ReverseMapping(MappingFromProteins());
                newType = typeof (Protein);
            }
            else if (AnyColumnsWithPrefix(viewSpec, Replicates))
            {
                mapping = ReverseMapping(MappingFromReplicates());
                newType = typeof (Replicate);
            }
            else
            {
                return viewInfo;
            }
            viewSpec = MapViewSpec(mapping, viewSpec, newType);
            if (null != propertyPaths)
            {
                propertyPaths = propertyPaths.Select(path => MapPropertyPath(mapping, path));
            }
            if (Equals(viewSpec.SublistId, SkylineViewContext.GetReplicateSublist(typeof(SkylineDocument))))
            {
                viewSpec = viewSpec.SetSublistId(SkylineViewContext.GetReplicateSublist(newType));
            }
            return new ViewInfo(viewInfo.DataSchema, newType, viewSpec);
        }

        private static ViewSpec MapViewSpec(IEnumerable<KeyValuePair<PropertyPath, PropertyPath>> mapping, ViewSpec viewSpec, Type newRowType)
        {
            viewSpec = viewSpec.SetColumns(viewSpec.Columns.Select(
                col=>col.SetPropertyPath(MapPropertyPath(mapping, col.PropertyPath))));
            viewSpec = viewSpec.SetFilters(viewSpec.Filters.Select(
                filter => filter.SetColumnId(MapPropertyPath(mapping, filter.ColumnId))));
            //viewSpec = viewSpec.SetSublistId(MapPropertyPath(mapping, viewSpec.SublistId));
            viewSpec = viewSpec.SetRowType(newRowType);
            return viewSpec;
        }

        private static IList<KeyValuePair<PropertyPath, PropertyPath>> ReverseMapping(
            IEnumerable<KeyValuePair<PropertyPath, PropertyPath>> mapping)
        {
            var reverse = new List<KeyValuePair<PropertyPath, PropertyPath>>();
            foreach (var kvp in mapping)
            {
                reverse.Insert(0, Kvp(kvp.Value, kvp.Key));
            }
            return reverse;
        } 

        private static bool AnyColumnsWithPrefix(ViewSpec viewSpec, PropertyPath propertyPath)
        {
            return viewSpec.Columns.Any(col => col.PropertyPath.StartsWith(propertyPath))
                   || viewSpec.Filters.Any(filter => filter.ColumnId.StartsWith(propertyPath));
        }

        private static PropertyPath MapPropertyPath(IEnumerable<KeyValuePair<PropertyPath, PropertyPath>> mapping,
            PropertyPath propertyPath)
        {
            foreach (var kvp in mapping)
            {
                if (propertyPath.StartsWith(kvp.Key))
                {
                    return ReplacePrefix(kvp.Key, kvp.Value, propertyPath);
                }
            }
            return propertyPath;
        }

        // ReSharper disable NonLocalizedString
        private static IList<KeyValuePair<PropertyPath, PropertyPath>> MappingFromProteins()
        {
            PropertyPath resultFiles = PropertyPath.Root.Property("Results").LookupAllItems().Property("Value");
            PropertyPath replicates = resultFiles.Property("Replicate");
            return new List<KeyValuePair<PropertyPath, PropertyPath>>
            {
                Kvp(replicates, Replicates),
                Kvp(resultFiles, ResultFiles),
                Kvp(PropertyPath.Root, Proteins),
            };
        }

        private static IList<KeyValuePair<PropertyPath, PropertyPath>> MappingFromPeptides()
        {
            PropertyPath resultFiles = PropertyPath.Root.Property("Results").LookupAllItems().Property("Value").Property("ResultFile");
            PropertyPath replicates = resultFiles.Property("Replicate");

            return new List<KeyValuePair<PropertyPath, PropertyPath>>
            {
                Kvp(PropertyPath.Root.Property("Protein"), Proteins),
                Kvp(replicates, Replicates),
                Kvp(resultFiles, ResultFiles),
                Kvp(PropertyPath.Root, Peptides),
            };
        }

        private static IList<KeyValuePair<PropertyPath, PropertyPath>> MappingFromPrecursors()
        {
            PropertyPath peptide = PropertyPath.Root.Property("Peptide");
            PropertyPath results = PropertyPath.Root.Property("Results").LookupAllItems().Property("Value");
            PropertyPath peptideResult = results.Property("PeptideResult");
            PropertyPath resultFile = peptideResult.Property("ResultFile");
            PropertyPath replicate = resultFile.Property("Replicate");
            return new List<KeyValuePair<PropertyPath, PropertyPath>>
            {
                Kvp(peptide.Property("Protein"), Proteins),
                Kvp(peptide, Peptides),
                Kvp(replicate, Replicates),
                Kvp(resultFile, ResultFiles),
                Kvp(peptideResult, PeptideResults),
                Kvp(results, PrecursorResults),
                Kvp(PropertyPath.Root, PeptidesPrecursors),
            };
        }

        private static IList<KeyValuePair<PropertyPath, PropertyPath>> MappingFromTransitions()
        {
            PropertyPath precursor = PropertyPath.Root.Property("Precursor");
            PropertyPath peptide = precursor.Property("Peptide");
            PropertyPath protein = peptide.Property("Protein");
            PropertyPath transitionResult = PropertyPath.Root.Property("Results").LookupAllItems().Property("Value");
            PropertyPath precursorResult = transitionResult.Property("PrecursorResult");
            PropertyPath peptideResult = precursorResult.Property("PeptideResult");
            PropertyPath resultFile = peptideResult.Property("ResultFile");
            PropertyPath replicate = resultFile.Property("Replicate");

            return new List<KeyValuePair<PropertyPath, PropertyPath>>
            {
                Kvp(protein, Proteins),
                Kvp(peptide, Peptides),
                Kvp(precursor, PeptidesPrecursors),
                Kvp(replicate, Replicates),
                Kvp(resultFile, ResultFiles),
                Kvp(peptideResult, PeptideResults),
                Kvp(precursorResult, PrecursorResults),
                Kvp(transitionResult, TransitionResults),
                Kvp(PropertyPath.Root, PeptidesPrecursorsTransitions),
            };
        }

        private static IList<KeyValuePair<PropertyPath, PropertyPath>> MappingFromReplicates()
        {
            return new List<KeyValuePair<PropertyPath, PropertyPath>>
            {
                Kvp(PropertyPath.Root.Property("Files").LookupAllItems(), ResultFiles),
                Kvp(PropertyPath.Root, Replicates),
            };
        }
        // ReSharper restore NonLocalizedString

        private static PropertyPath ReplacePrefix(PropertyPath oldPrefix, PropertyPath newPrefix, PropertyPath tail)
        {
            if (Equals(oldPrefix, tail))
            {
                return newPrefix;
            }
            return tail.SetParent(ReplacePrefix(oldPrefix, newPrefix, tail.Parent));
        }

        private static KeyValuePair<T1, T2> Kvp<T1,T2>(T1 key, T2 value)
        {
            return new KeyValuePair<T1, T2>(key, value);
        }
    }
}
