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

namespace pwiz.Skyline.Model.Hibernate.Query
{
    /// <summary>
    /// Answers questions about how to pivot on a particular column
    /// </summary>
    public abstract class PivotType
    {
        public static readonly Identifier REPLICATE_NAME_COLUMN 
            = new Identifier("ResultFile", "Replicate", "Replicate");

        public static readonly PivotType REPLICATE = new ReplicatePivotType();
        public static readonly PivotType ISOTOPE_LABEL = new IsotopeLabelPivotType();

        /// <summary>
        /// Returns the column whose values supply the names going across horizontally
        /// in the crosstab.
        /// </summary>
        public abstract Identifier GetCrosstabHeader(Type table);
        /// <summary>
        /// Returns the set of columns which, along with the column returned by GetCrosstabHeader,
        /// uniquely identifier rows in the table.
        /// </summary>
        public abstract ICollection<Identifier> GetGroupByColumns(Type table);
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
        private static bool Contains(IEnumerable<Identifier> list, Identifier key)
        {
            foreach (Identifier id in list)
            {
                if (key.StartsWith(id))
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
        public static ICollection<Identifier> Intersect(
            ICollection<Identifier> list1, 
            ICollection<Identifier> list2)
        {
            if (list1.Count == 0)
            {
                return list2;
            }
            if (list2.Count == 0)
            {
                return list1;
            }
            HashSet<Identifier> result = new HashSet<Identifier>();
            foreach (Identifier id in list1)
            {
                if (Contains(list2, id))
                {
                    result.Add(id);
                }
            }
            foreach (Identifier id in list2)
            {
                if (Contains(list1, id))
                {
                    result.Add(id);
                }
            }
            return result;
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
            return table == typeof(DbPeptideResult)
                   || table == typeof(DbPrecursorResult)
                   || table == typeof(DbProteinResult)
                   || table == typeof(DbTransitionResult);
        }
    }
    /// <summary>
    /// PivotType for pivoting on Replicate Name.  This pivot type can only be applied to
    /// Results tables.
    /// </summary>
    public class ReplicatePivotType : PivotType
    {
        public override Identifier GetCrosstabHeader(Type table)
        {
            if (IsResultTable(table))
            {
                return REPLICATE_NAME_COLUMN;
            }
            return null;
        }

        public override ICollection<Identifier> GetGroupByColumns(Type table)
        {
            List<Identifier> result = new List<Identifier>();
            if (table == typeof(DbProteinResult))
            {
                result.Add(new Identifier("Protein"));
            }
            else if (table == typeof(DbPeptideResult))
            {
                result.Add(new Identifier("Peptide"));
            } 
            else if (table == typeof(DbPrecursorResult))
            {
                result.Add(new Identifier("Precursor"));
                result.Add(new Identifier("OptStep"));
            }
            else if (table == typeof(DbTransitionResult))
            {
                result.Add(new Identifier("Transition"));
                result.Add(new Identifier("PrecursorResult", "OptStep"));
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
        public override Identifier GetCrosstabHeader(Type table)
        {
            if (table == typeof(DbPrecursor))
            {
                return new Identifier("IsotopeLabelType");
            }
            if (table == typeof(DbTransition) || table == typeof(DbPrecursorResult))
            {
                return new Identifier("Precursor", GetCrosstabHeader(typeof(DbPrecursor)));
            }
            if (table == typeof(DbTransitionResult))
            {
                return new Identifier("Transition", GetCrosstabHeader(typeof(DbTransition)));
            }
            return null;
        }

        public override ICollection<Identifier> GetGroupByColumns(Type table)
        {
            List<Identifier> result = new List<Identifier>();
            if (table == typeof (DbPrecursor))
            {
                result.Add(new Identifier("Peptide"));
                result.Add(new Identifier("Charge"));
            } 
            else if (table == typeof (DbTransition))
            {
                result.Add(new Identifier("ProductCharge"));
                result.Add(new Identifier("FragmentIon"));
                result.AddRange(AddPrefix(
                    GetGroupByColumns(typeof(DbPrecursor)), new Identifier("Precursor")));
            } 
            else if (table == typeof(DbPrecursorResult))
            {
                result.AddRange(AddPrefix(
                    GetGroupByColumns(typeof(DbPrecursor)), new Identifier("Precursor")));
                result.Add(REPLICATE_NAME_COLUMN);
                result.Add(new Identifier("OptStep"));
            } 
            else if (table == typeof(DbTransitionResult))
            {
                result.AddRange(AddPrefix(
                    GetGroupByColumns(typeof(DbTransition)), new Identifier("Transition")));
                result.Add(REPLICATE_NAME_COLUMN);
                result.Add(new Identifier("PrecursorResult", "OptStep"));
            }
            return result;
        }

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
            = new ReadOnlyCollection<String>(new[] {"ProductNeutralMass", "ProductMz", "Note"});
        public override bool IsCrosstabValue(Type table, String column)
        {
            if (table == typeof(DbPrecursorResult) || table == typeof(DbTransitionResult))
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
}
