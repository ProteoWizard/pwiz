/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Hibernate.Query
{
    /// <summary>
    /// Answers questions about how to pivot on a particular column
    /// </summary>
    public abstract class PivotType
    {
        public static readonly Identifier REPLICATE_NAME_ID
            = new Identifier("ResultFile", // Not L10N
                             "Replicate",  // Not L10N
                             "Replicate"); // Not L10N

        public static readonly PivotType REPLICATE = new ReplicatePivotType();
        public static readonly PivotType ISOTOPE_LABEL = new IsotopeLabelPivotType();
        public static readonly PivotType REPLICATE_ISOTOPE_LABEL = new ReplicateIsotopeLabelPivotType();

        /// <summary>
        /// Returns the column whose values supply the names going across horizontally
        /// in the crosstab.
        /// </summary>
        public abstract IList<ReportColumn> GetCrosstabHeaders(IList<ReportColumn> columns);
        /// <summary>
        /// Returns the set of columns which, along with the column returned by GetCrosstabHeader,
        /// uniquely identifier rows in the table.
        /// </summary>
        public abstract IList<ReportColumn> GetGroupByColumns(IList<ReportColumn> columns);
        /// <summary>
        /// Returns true if the column is one which should be duplicated horizontally in the
        /// crosstab.  That is, its value potentially depends on the value in the column 
        /// GetCrosstabHeader.
        /// </summary>
        public abstract bool IsCrosstabValue(Type table, String column);

        /// <summary>
        /// Given a list of columns, returns true if the specified other column is redundant in
        /// terms of specifying a unique index on a table.
        /// </summary>
        protected static bool StartsWith(IEnumerable<ReportColumn> list, ReportColumn key)
        {
            foreach (ReportColumn id in list)
            {
                if (key.Table == id.Table && key.Column.StartsWith(id.Column))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Given two lists, each of which uniquely identify rows in a table, returns a list
        /// with redundant entries removed.
        /// </summary>
        public static ICollection<ReportColumn> Intersect(
            ICollection<ReportColumn> list1,
            ICollection<ReportColumn> list2)
        {
            if (list1.Count == 0)
            {
                return list2;
            }
            if (list2.Count == 0)
            {
                return list1;
            }
            HashSet<ReportColumn> result = new HashSet<ReportColumn>();
            foreach (ReportColumn id in list1)
            {
                if (StartsWith(list2, id))
                {
                    result.Add(id);
                }
            }
            foreach (ReportColumn id in list2)
            {
                if (StartsWith(list1, id))
                {
                    result.Add(id);
                }
            }
            return result;
        }

        protected static bool Contains(IEnumerable<ReportColumn> columns, Type table)
        {
            return columns.Contains(column => table == column.Table);
        }

        protected static IEnumerable<Identifier> AddPrefix(IEnumerable<Identifier> source, Identifier prefix)
        {
            List<Identifier> result = new List<Identifier>();
            foreach (Identifier id in source)
            {
                result.Add(new Identifier(prefix, id));
            }
            return result;
        }

        protected static bool IsResultTable(Type table)
        {
            return ReportColumn.GetTableType(table) == TableType.result;
        }
    }
    /// <summary>
    /// PivotType for pivoting on Replicate Name.  This pivot type can only be applied to
    /// Results tables.
    /// </summary>
    public class ReplicatePivotType : PivotType
    {
        public override IList<ReportColumn> GetCrosstabHeaders(IList<ReportColumn> columns)
        {
            int i = columns.IndexOf(column => IsResultTable(column.Table));
            if (i != -1)
            {
                return new[] {new ReportColumn(columns[i].Table, REPLICATE_NAME_ID)};
            }
            return new ReportColumn[0];
        }

        public override IList<ReportColumn> GetGroupByColumns(IList<ReportColumn> columns)
        {
            var result = new List<ReportColumn>();
            if (Contains(columns, typeof(DbProteinResult)))
            {
                result.Add(new ReportColumn(typeof(DbProteinResult), "Protein")); // Not L10N
            }
            else if (Contains(columns, typeof(DbPeptideResult)))
            {
                result.Add(new ReportColumn(typeof(DbPeptideResult), "Peptide")); // Not L10N
            } 
            else if (Contains(columns, typeof(DbPrecursorResult)))
            {
                result.Add(new ReportColumn(typeof(DbPrecursorResult), "Precursor")); // Not L10N
                result.Add(new ReportColumn(typeof(DbPrecursorResult), "OptStep")); // Not L10N
            }
            else if (Contains(columns, typeof(DbTransitionResult)))
            {
                result.Add(new ReportColumn(typeof(DbTransitionResult), "Transition")); // Not L10N
                result.Add(new ReportColumn(typeof(DbTransitionResult), "PrecursorResult", "OptStep")); // Not L10N
            }
            return result;
        }

        public override bool IsCrosstabValue(Type table, String name)
        {
            if (!IsResultTable(table))
            {
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Pivot Type for pivoting on the isotope label.  This pivot can only be applied to
    /// DbPrecursor and DbTransition tables, as well as their associated Results tables.
    /// </summary>
    public class IsotopeLabelPivotType : PivotType
    {
        public override IList<ReportColumn> GetCrosstabHeaders(IList<ReportColumn> columns)
        {
            if (columns.Count > 0)
            {
                if (columns.Contains(column => IsPrecursorType(column.Table)))
                    return new[] { new ReportColumn(typeof(DbPrecursor), "IsotopeLabelType") }; // Not L10N
                if (columns.Contains(column => IsTransitionType(column.Table)))
                    return new[] { new ReportColumn(typeof(DbTransition), "Precursor", "IsotopeLabelType") }; // Not L10N
            }
            return new ReportColumn[0];
        }

        private static bool IsPrecursorType(Type table)
        {
            return table == typeof (DbPrecursor) ||
                   table == typeof (DbPrecursorResult);
        }

        private static bool IsTransitionType(Type table)
        {
            return table == typeof(DbTransition) ||
                   table == typeof(DbTransitionResult);
        }

        public override IList<ReportColumn> GetGroupByColumns(IList<ReportColumn> columns)
        {
            var result = new List<ReportColumn>();
            // ReSharper disable NonLocalizedString
            if (Contains(columns, typeof (DbPrecursor)))
            {
                result.Add(new ReportColumn(typeof (DbPrecursor), "Peptide"));
                result.Add(new ReportColumn(typeof (DbPrecursor), "Charge"));
            } 
            else if (Contains(columns, typeof (DbTransition)))
            {
                result.Add(new ReportColumn(typeof (DbTransition), "ProductCharge"));
                result.Add(new ReportColumn(typeof (DbTransition), "FragmentIon"));
                result.Add(new ReportColumn(typeof (DbTransition), "Losses"));
                result.Add(new ReportColumn(typeof (DbTransition), "Precursor", "Peptide"));
                result.Add(new ReportColumn(typeof (DbTransition), "Precursor", "Charge"));
            }

            if (Contains(columns, typeof(DbPrecursorResult)))
            {
                result.Add(new ReportColumn(typeof (DbPrecursorResult), REPLICATE_NAME_ID));
                result.Add(new ReportColumn(typeof (DbPrecursorResult), "OptStep"));
            } 
            else if (Contains(columns, typeof(DbTransitionResult)))
            {
                result.Add(new ReportColumn(typeof(DbTransitionResult), REPLICATE_NAME_ID));
                result.Add(new ReportColumn(typeof(DbTransitionResult), "PrecursorResult", "OptStep"));
            }
            // ReSharper restore NonLocalizedString

            return result;
        }
        // ReSharper disable NonLocalizedString
        public static readonly IList<String> PRECURSOR_CROSSTAB_VALUES 
            = new ReadOnlyCollection<String>(
                new []
                     {
                         "IsotopeLabelType",
                         "NeutralMass",
                         "Mz",
                         "CollisionEnergy",
                         "DeclusteringPotential",
                         "ModifiedSequence",
                         "Note"
                     }
                );

        public static readonly IList<String> TRANSITION_CROSSTAB_VALUES
            = new ReadOnlyCollection<String>(
                new[]
                    {
                        "ProductNeutralMass",
                        "ProductMz",
                        "Note"
                    }
                );
        // ReSharper restore NonLocalizedString

        public override bool IsCrosstabValue(Type table, String column)
        {
            if (table == typeof(DbPrecursorResult) || table == typeof(DbPrecursorResultSummary) ||
                table == typeof(DbTransitionResult) || table == typeof(DbTransitionResultSummary))
            {
                return true;
            }
            if (table == typeof(DbPrecursor))
            {
                return PRECURSOR_CROSSTAB_VALUES.Contains(column);
            }
            if (table == typeof(DbTransition))
            {
                return TRANSITION_CROSSTAB_VALUES.Contains(column);
            }
            return false;
        }
    }
    public class ReplicateIsotopeLabelPivotType : PivotType
    {
        public override IList<ReportColumn> GetCrosstabHeaders(IList<ReportColumn> columns)
        {
            var result = new List<ReportColumn>();
            result.AddRange(REPLICATE.GetCrosstabHeaders(columns));
            result.AddRange(ISOTOPE_LABEL.GetCrosstabHeaders(columns));
            return result;
        }

        public override IList<ReportColumn> GetGroupByColumns(IList<ReportColumn> columns)
        {
            var result = new List<ReportColumn>();
            if (Contains(columns, typeof(DbPrecursorResult)) || Contains(columns, typeof(DbTransitionResult)))
            {
                result.AddRange(ISOTOPE_LABEL.GetGroupByColumns(columns));
            }
            else
            {
                result.AddRange(REPLICATE.GetGroupByColumns(columns));
                result.AddRange(ISOTOPE_LABEL.GetGroupByColumns(columns));
            }
            return result;
        }

        public override bool IsCrosstabValue(Type table, string column)
        {
            return REPLICATE.IsCrosstabValue(table, column) || ISOTOPE_LABEL.IsCrosstabValue(table, column);
        }
    }
}
