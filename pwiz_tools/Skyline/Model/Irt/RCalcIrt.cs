/*
 * Original author: John Chilton <jchilton .at. uw.edu>,
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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    [XmlRoot("irt_calculator")]
    public class RCalcIrt : RetentionScoreCalculatorSpec
    {
        public static readonly RCalcIrt NONE = new RCalcIrt("None", string.Empty); // Not L10N

        public const double MIN_IRT_TO_TIME_CORRELATION = 0.99;
        public const double MIN_PEPTIDES_PERCENT = 0.80;
        public const int MIN_PEPTIDES_COUNT = 8;

        public static int MinStandardCount(int expectedCount)
        {
            return expectedCount <= MIN_PEPTIDES_COUNT
                ? expectedCount
                : Math.Max(MIN_PEPTIDES_COUNT, (int) (expectedCount*MIN_PEPTIDES_PERCENT));
        }

        public static bool IsAcceptableStandardCount(int expectedCount, int actualCount)
        {
            return actualCount >= MinStandardCount(expectedCount);
        }

        private IrtDb _database;

        public RCalcIrt(string name, string databasePath)
            : base(name)
        {
            DatabasePath = databasePath;
        }

        public string DatabasePath { get; private set; }

        public IEnumerable<KeyValuePair<string, double>> PeptideScores
        {
            get { return _database != null ? _database.PeptideScores : new KeyValuePair<string, double>[0]; }
        }

        public bool IsNone
        {
            get { return Name == NONE.Name; }
        }

        public override bool IsUsable
        {
            get { return _database != null; }
        }

        public override RetentionScoreCalculatorSpec Initialize(IProgressMonitor loadMonitor)
        {
            if (_database != null)
                return this;

            var database = IrtDb.GetIrtDb(DatabasePath, loadMonitor);
            // Check for the case where an exception was handled by the progress monitor
            if (database == null)
                return null;
            return ChangeDatabase(database);
        }

        public override string PersistencePath
        {
            get { return DatabasePath; }
        }

        /// <summary>
        /// Saves the database to a new directory with only the standards and peptides used
        /// in a given document.
        /// </summary>
        /// <param name="pathDestDir">The directory to save to</param>
        /// <param name="document">The document for which peptides are to be kept</param>
        /// <returns>The full path to the file saved</returns>
        public override string PersistMinimized(string pathDestDir, SrmDocument document)
        {
            RequireUsable();

            string persistPath = Path.Combine(pathDestDir, Path.GetFileName(PersistencePath) ?? string.Empty);  // ReSharper
            using (var fs = new FileSaver(persistPath))
            {
                var irtDbMinimal = IrtDb.CreateIrtDb(fs.SafeName);

                // Calculate the minimal set of peptides needed for this document
                var dbPeptides = _database.GetPeptides().ToList();
                var persistPeptides = dbPeptides.Where(pep => pep.Standard).Select(NewPeptide).ToList();
                var dictPeptides = dbPeptides.Where(pep => !pep.Standard).ToDictionary(pep => pep.PeptideModSeq);
                foreach (var nodePep in document.Peptides)
                {
                    string modifiedSeq = document.Settings.GetSourceTextId(nodePep);
                    DbIrtPeptide dbPeptide;
                    if (dictPeptides.TryGetValue(modifiedSeq, out dbPeptide))
                    {
                        persistPeptides.Add(NewPeptide(dbPeptide));
                        // Only add once
                        dictPeptides.Remove(modifiedSeq);
                    }
                }

                irtDbMinimal.AddPeptides(persistPeptides);
                fs.Commit();
            }

            return persistPath;
        }

        private DbIrtPeptide NewPeptide(DbIrtPeptide dbPeptide)
        {
            return new DbIrtPeptide(dbPeptide.PeptideModSeq,
                                    dbPeptide.Irt,
                                    dbPeptide.Standard,
                                    dbPeptide.TimeSource);
        }

        public static bool TryGetRegressionLine(IList<double> listIndependent, IList<double> listDependent, out RegressionLine line)
        {
            var minPoints = (int) Math.Round(Math.Max(MIN_PEPTIDES_COUNT, listIndependent.Count*MIN_PEPTIDES_PERCENT));
            return TryGetRegressionLine(listIndependent, listDependent, minPoints, out line);
        }

        public static bool TryGetRegressionLine(IList<double> listIndependent, IList<double> listDependent, int minPoints, out RegressionLine line)
        {
            line = null;
            if (listIndependent.Count != listDependent.Count || listIndependent.Count < minPoints)
                return false;

            double correlation;
            while (true)
            {
                var statIndependent = new Statistics(listIndependent);
                var statDependent = new Statistics(listDependent);
                line = new RegressionLine(statDependent.Slope(statIndependent), statDependent.Intercept(statIndependent));
                correlation = statDependent.R(statIndependent);

                if (correlation >= MIN_IRT_TO_TIME_CORRELATION || listIndependent.Count <= minPoints)
                    break;

                var furthest = 0;
                var maxDistance = 0.0;
                for (var i = 0; i < listDependent.Count; i++)
                {
                    var distance = Math.Abs(line.GetY(listDependent[i]) - listIndependent[i]);
                    if (distance > maxDistance)
                    {
                        furthest = i;
                        maxDistance = distance;
                    }
                }

                listIndependent.RemoveAt(furthest);
                listDependent.RemoveAt(furthest);
            }

            return correlation >= MIN_IRT_TO_TIME_CORRELATION;
        }

        public override IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides, out int minCount)
        {
            RequireUsable();

            var returnStandard = peptides.Where(_database.IsStandard).Distinct().ToArray();
            var returnCount = returnStandard.Length;
            var databaseCount = _database.StandardPeptideCount;

            if (!IsAcceptableStandardCount(databaseCount, returnCount))
                throw new IncompleteStandardException(this);

            minCount = MinStandardCount(databaseCount);
            return returnStandard;
        }

        public override IEnumerable<string> GetStandardPeptides(IEnumerable<string> peptides)
        {
            int minCount;
            return ChooseRegressionPeptides(peptides, out minCount);
        }

        public override double? ScoreSequence(string seq)
        {
            if (_database != null)
                return _database.ScoreSequence(seq);
            return null;
        }

        public override double UnknownScore
        {
            get
            {
                RequireUsable();

                return _database.UnknownScore;
            }
        }

        private void RequireUsable()
        {
            if (!IsUsable)
                throw new InvalidOperationException(Resources.RCalcIrt_RequireUsable_Unexpected_use_of_iRT_calculator_before_successful_initialization_);
        }

        public IEnumerable<string> GetStandardPeptides()
        {
            return _database.StandardPeptides;
        }

        #region Property change methods

        public RCalcIrt ChangeDatabasePath(string path)
        {
            return ChangeProp(ImClone(this), im => im.DatabasePath = path);
        }

        public RCalcIrt ChangeDatabase(IrtDb database)
        {
            return ChangeProp(ImClone(this), im => im._database = database);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private RCalcIrt()
        {
        }

        enum ATTR
        {
            database_path
        }

        public static RCalcIrt Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new RCalcIrt());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            DatabasePath = reader.GetAttribute(ATTR.database_path);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.database_path, DatabasePath);
        }

        #endregion

        #region object overrrides

        public bool Equals(RCalcIrt other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other._database, _database) && Equals(other.DatabasePath, DatabasePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as RCalcIrt);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ DatabasePath.GetHashCode();
                result = (result*397) ^ (_database != null ? _database.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public class IncompleteStandardException : CalculatorException
    {
        //This will only be thrown by ChooseRegressionPeptides so it is OK to have an error specific to regressions.
        private static readonly string ERROR =
            Resources.IncompleteStandardException_ERROR_The_calculator__0__requires_all_of_its_standard_peptides_in_order_to_determine_a_regression_;

        public RetentionScoreCalculatorSpec Calculator { get; private set; }

        public IncompleteStandardException(RetentionScoreCalculatorSpec calc)
            : base(String.Format(ERROR, calc.Name))
        {
            Calculator = calc;
        }
    }

    public class DatabaseNotConnectedException : CalculatorException
    {
        private static readonly string DBERROR =
            Resources.DatabaseNotConnectedException_DBERROR_The_database_for_the_calculator__0__could_not_be_opened__Check_that_the_file__1__was_not_moved_or_deleted_;

        private readonly RetentionScoreCalculatorSpec _calculator;
        public RetentionScoreCalculatorSpec Calculator { get { return _calculator; } }

        public DatabaseNotConnectedException(RCalcIrt calc)
            : base(string.Format(DBERROR, calc.Name, calc.DatabasePath))
        {
            _calculator = calc;
        }
    }

}