/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Properties;
// ReSharper disable VirtualMemberCallInConstructor

namespace pwiz.Skyline.Model.Irt
{
    public interface IPeptideData
    {
        Target Target { get; }
    }

    public abstract class DbAbstractPeptide : DbEntity, IPeptideData
    {
        private Target _peptideModSeq;

        protected DbAbstractPeptide()
        {
        }

        protected DbAbstractPeptide(DbAbstractPeptide other)
        {
            _peptideModSeq = other._peptideModSeq;
        }

        // For NHibernate use
        public virtual string PeptideModSeq
        {
            get { return _peptideModSeq.ToSerializableString(); }
            set
            {
                _peptideModSeq = Target.FromSerializableString(value);
            }
        }

        public virtual Target ModifiedTarget
        {
            get { return _peptideModSeq; }
            set
            {
                // Always round-trip the Target to its serialized form, which might truncate the masses for small molecules.
                _peptideModSeq = Target.FromSerializableString(value.ToSerializableString());
            }
        }

        public virtual Target Target { get { return ModifiedTarget; } }

        public virtual Target GetNormalizedModifiedSequence()
        {
            return _peptideModSeq;
        }
    }

    public class DbIrtPeptide : DbAbstractPeptide
    {
        public override Type EntityClass
        {
            get { return typeof(DbIrtPeptide); }
        }

        /*
        CREATE TABLE RetentionTimes (
            Id id INTEGER PRIMARY KEY autoincrement not null,
            PeptideModSeq VARCHAR(200),
            iRT REAL,
            Standard BIT,
            TimeSource INT null
        )
        */
        // public virtual long? ID { get; set; } // in DbEntity
        public virtual double Irt { get; set; }
        public virtual bool Standard { get; set; }
        public virtual int? TimeSource { get; set; } // null = unknown, 0 = scan, 1 = peak


        /// <summary>
        /// For NHibernate only
        /// </summary>
        protected DbIrtPeptide()
        {            
        }

        public DbIrtPeptide(DbIrtPeptide other) : base(other)
        {
            Id = other.Id;
            Irt = other.Irt;
            Standard = other.Standard;
            TimeSource = other.TimeSource;
        }

        public DbIrtPeptide(Target seq, double irt, bool standard, TimeSource timeSource)
            : this(seq, irt, standard, (int) timeSource)
        {            
        }

        public DbIrtPeptide(Target seq, double irt, bool standard, int? timeSource)
        {
            ModifiedTarget = seq;
            Irt = irt;
            Standard = standard;
            TimeSource = timeSource;
        }

        public static List<DbIrtPeptide> MakeUnique(List<DbIrtPeptide> irtPeptides)
        {
            var uniqueIrtPeptidesDict = new Dictionary<Target, DbIrtPeptide>();
            foreach (var irtPeptide in irtPeptides)
            {
                DbIrtPeptide duplicateIrtPeptide;
                if (irtPeptide.Standard || !uniqueIrtPeptidesDict.TryGetValue(irtPeptide.ModifiedTarget, out duplicateIrtPeptide))
                {
                    uniqueIrtPeptidesDict[irtPeptide.ModifiedTarget] = irtPeptide;
                }
            }
            return uniqueIrtPeptidesDict.Values.ToList();
        }

        public struct Conflict
        {
            public Conflict(DbIrtPeptide newPeptide, DbIrtPeptide existingPeptide) : this()
            {
                ExistingPeptide = existingPeptide;
                NewPeptide = newPeptide;
            }

            public DbIrtPeptide ExistingPeptide { get; private set; }
            public DbIrtPeptide NewPeptide { get; private set; }
        }

        public static List<DbIrtPeptide> FindNonConflicts(
            IList<DbIrtPeptide> oldPeptides, 
            IList<DbIrtPeptide> newPeptides, 
            IProgressMonitor progressMonitor,
            out IList<Conflict> conflicts)
        {
            var progressPercent = 0;
            IProgressStatus status = new ProgressStatus(Resources.DbIrtPeptide_FindNonConflicts_Adding_iRT_values_for_imported_peptides);
            progressMonitor?.UpdateProgress(status);
            var peptidesNoConflict = new List<DbIrtPeptide>();
            conflicts = new List<Conflict>();
            var dictOld = oldPeptides.ToDictionary(pep => pep.ModifiedTarget);
            var dictNew = newPeptides.ToDictionary(pep => pep.ModifiedTarget);
            for (var i = 0; i < newPeptides.Count; i++)
            {
                var newPeptide = newPeptides[i];
                // A conflict occurs only when there is another peptide of the same sequence, and different iRT
                if (!dictOld.TryGetValue(newPeptide.ModifiedTarget, out var oldPeptide) || Math.Abs(newPeptide.Irt - oldPeptide.Irt) < IRT_MIN_DIFF )
                {
                    peptidesNoConflict.Add(newPeptide);
                }
                else
                {
                    conflicts.Add(new Conflict(newPeptide, oldPeptide));
                }
                if (progressMonitor != null)
                {
                    if (progressMonitor.IsCanceled)
                        return null;
                    var progressNew = i * 100 / newPeptides.Count;
                    if (progressPercent != progressNew)
                    {
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(progressNew));
                        progressPercent = progressNew;
                    }
                }
            }
            peptidesNoConflict.AddRange(oldPeptides.Where(oldPeptide => !dictNew.ContainsKey(oldPeptide.ModifiedTarget)));
            return peptidesNoConflict;
        }

        public const double IRT_MIN_DIFF = 0.001;

        #region object overrides

        public virtual bool Equals(DbIrtPeptide other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   Equals(other.ModifiedTarget, ModifiedTarget) &&
                   other.Irt.Equals(Irt) &&
                   other.Standard.Equals(Standard) &&
                   other.TimeSource.Equals(TimeSource);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DbIrtPeptide);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (ModifiedTarget != null ? ModifiedTarget.GetHashCode() : 0);
                result = (result*397) ^ Irt.GetHashCode();
                result = (result*397) ^ Standard.GetHashCode();
                result = (result*397) ^ (TimeSource.HasValue ? TimeSource.Value : 0);
                return result;
            }
        }

        #endregion
    }
}
