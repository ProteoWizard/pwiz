//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IDPicker.DataModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IDPicker;
using pwiz.Common.Collections;


namespace Test
{
    /// <summary>
    /// Summary description for TestUtil
    /// </summary>
    [TestClass]
    public class TestUtil
    {
        public TestUtil (){}
        public TestContext TestContext { get; set; }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        [TestInitialize()]
        public void TestInitialize()
        {
            Directory.SetCurrentDirectory(TestContext.TestDeploymentDir);
        }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        [TestCategory("Utility")]
        public void TestContainsOrContainedBy()
        {
            FilterString filterString;
            string[] input, output;
            
            filterString = new FilterString("ABC");
            input = new string[] {"ABCD", "EFG", "HIJK", "CBA", "A B C", "abc", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC"};
            output = new string[] { "ABCD", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC" };
            CollectionAssert.AreEqual(output, input.Where(o => o.ContainsOrIsContainedBy(filterString)).ToArray());

            filterString = new FilterString("ABC;EFG;abc");
            input = new string[] { "ABCD", "EFG", "HIJK", "CBA", "A B C", "abc", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC" };
            output = new string[] { "ABCD", "EFG", "abc", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC" };
            CollectionAssert.AreEqual(output, input.Where(o => o.ContainsOrIsContainedBy(filterString)).ToArray());

            filterString = new FilterString("A B C;23;A;CB");
            input = new string[] { "ABCD", "EFG", "HIJK", "CBA", "A B C", "abc", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC" };
            output = new string[] { "ABCD", "CBA", "A B C", " ABC ", "1ABC[23]", "A", "B", "C", "AB" };
            CollectionAssert.AreEqual(output, input.Where(o => o.ContainsOrIsContainedBy(filterString)).ToArray());

            filterString = new FilterString("'A'");
            input = new string[] { "ABCD", "EFG", "HIJK", "CBA", "A B C", "abc", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC" };
            output = new string[] { "A" };
            CollectionAssert.AreEqual(output, input.Where(o => o.ContainsOrIsContainedBy(filterString)).ToArray());

            filterString = new FilterString("'23';\"BC\";HI");
            input = new string[] { "ABCD", "EFG", "HIJK", "CBA", "A B C", "abc", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC" };
            output = new string[] { "HIJK", "BC" };
            CollectionAssert.AreEqual(output, input.Where(o => o.ContainsOrIsContainedBy(filterString)).ToArray());

            filterString = new FilterString("'1ABC[23]';'[AB]'");
            input = new string[] { "ABCD", "EFG", "HIJK", "CBA", "A B C", "abc", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC" };
            output = new string[] { "1ABC[23]" };
            CollectionAssert.AreEqual(output, input.Where(o => o.ContainsOrIsContainedBy(filterString)).ToArray());

            filterString = new FilterString("[BC];\\d\\S+\\[\\d+\\]");
            input = new string[] { "ABCD", "EFG", "HIJK", "CBA", "A B C", "abc", " ABC ", "1ABC[23]", "A", "B", "C", "AB", "BC" };
            output = new string[] { "ABCD", "CBA", "A B C", " ABC ", "1ABC[23]", "B", "C", "AB", "BC" };
            CollectionAssert.AreEqual(output, input.Where(o => o.ContainsOrIsContainedBy(filterString)).ToArray());
        }

        [TestMethod]
        [TestCategory("Utility")]
        public void TestGetCommonFilename ()
        {
            string[] input;

            Assert.AreEqual("D:/testfoo.idpDB", Util.GetCommonFilename("D:/testfoo.pepXML"));
            Assert.AreEqual("D:/test.foo.idpDB", Util.GetCommonFilename("D:/test.foo.pepXML"));
            Assert.AreEqual("D:/test.idpDB", Util.GetCommonFilename("D:/test.pep.xml"));
            Assert.AreEqual("D:/test.foo.idpDB", Util.GetCommonFilename("D:/test.foo.pep.xml"));
            Assert.AreEqual("D:/test.mzid.idpDB", Util.GetCommonFilename("D:/test.mzid.xml"));
            Assert.AreEqual("D:/test.foo.mzid.idpDB", Util.GetCommonFilename("D:/test.foo.mzid.xml"));
            Assert.AreEqual("D:/test.idpDB", Util.GetCommonFilename("D:/test.idpDB"));
            Assert.AreEqual("D:/test.foo.idpDB", Util.GetCommonFilename("D:/test.foo.idpDB"));

            input = new string[] {"D:/testfoo.pepXML", "D:/testbar.pepXML"};
            Assert.AreEqual("D:/test.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "D:/test/some/more/foo.pepXML", "D:/test/some/thing/bar.pepXML" };
            Assert.AreEqual("D:/test/some/some.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "D:/test/some/more/foo.pepXML", "D:/test/some/thing/bar.pep.xml" };
            Assert.AreEqual("D:/test/some/some.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "D:/test/some/more/foo.pep.XML", "D:/test/some/thing/bar.pep.xml" };
            Assert.AreEqual("D:/test/some/some.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "foo.pepXML", "foo.idpDB" };
            Assert.AreEqual("foo.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "foo", "bar" };
            StringAssert.StartsWith(Util.GetCommonFilename(input), "idpicker-analysis-");

            input = new string[] { "D:/foo.pepXML", "D:/bar.pepXML" };
            StringAssert.StartsWith(Util.GetCommonFilename(input), "D:/idpicker-analysis-");

            input = new string[] { "D:/foo.pep.xml", "D:/bar.pepXML" };
            StringAssert.StartsWith(Util.GetCommonFilename(input), "D:/idpicker-analysis-");
        }

        [TestMethod]
        [TestCategory("Utility")]
        public void TestUniqueSubstring()
        {
            string[] input;
            string uniqueSubstring;

            input = new string[] { "abc", "cde" };
            Util.UniqueSubstring("a", input, out uniqueSubstring);
            Assert.AreEqual("a", uniqueSubstring);
            
            Util.UniqueSubstring("c", input, out uniqueSubstring);
            Assert.AreEqual("c", uniqueSubstring);

            Util.UniqueSubstring("bc", input, out uniqueSubstring);
            Assert.AreEqual("bc", uniqueSubstring);

            input = new string[] { "abc", "bcde" };
            Util.UniqueSubstring("a", input, out uniqueSubstring);
            Assert.AreEqual("a", uniqueSubstring);

            Util.UniqueSubstring("c", input, out uniqueSubstring);
            Assert.AreEqual("c", uniqueSubstring);

            Util.UniqueSubstring("bc", input, out uniqueSubstring);
            Assert.AreEqual("bc", uniqueSubstring);

            Util.UniqueSubstring("cd", input, out uniqueSubstring);
            Assert.AreEqual("cd", uniqueSubstring);

            input = new string[] { "a1b", "a1c", "a1d", "a1e" };
            Util.UniqueSubstring("a1b", input, out uniqueSubstring);
            Assert.AreEqual("b", uniqueSubstring);

            Util.UniqueSubstring("a1c", input, out uniqueSubstring);
            Assert.AreEqual("c", uniqueSubstring);

            Util.UniqueSubstring("a1cd", input, out uniqueSubstring);
            Assert.AreEqual("cd", uniqueSubstring);

            input = new string[] { "201201-555", "201202-555", "201203-555", "201201-444", "201202-444", "201203-444" };
            Util.UniqueSubstring("201201-555", input, out uniqueSubstring);
            Assert.AreEqual("1-555", uniqueSubstring);

            Util.UniqueSubstring("201202-555", input, out uniqueSubstring);
            Assert.AreEqual("2-555", uniqueSubstring);

            Util.UniqueSubstring("201202-444", input, out uniqueSubstring);
            Assert.AreEqual("2-444", uniqueSubstring);

            Util.UniqueSubstring("201203-444", input, out uniqueSubstring);
            Assert.AreEqual("3-444", uniqueSubstring);
        }

        /// <summary>
        /// A very limited parser of SQLite CREATE TABLE statements intended to make it easier to drop columns:
        /// (i.e. rename table, create new table without the dropped columns, insert old table's data into new table, delete old table)
        /// </summary>
        string dropColumns(SQLiteConnection con, string tableName, params string[] columnNames)
        {
            string createTableStatement = con.ExecuteQuery(String.Format("SELECT sql FROM sqlite_master WHERE tbl_name='{0}'", tableName)).First().GetString(0).Replace("\n", "");
            var tableAndColumns = Regex.Match(createTableStatement, @"^CREATE TABLE (.+?)\((.*?)\)$");
            if (!tableAndColumns.Success)
                throw new Exception("failed to find table name or columns in create table statement: " + createTableStatement);

            string table = tableAndColumns.Groups[1].Captures[0].Value;
            string columns = tableAndColumns.Groups[2].Captures[0].Value;
            const string sqliteTypePattern = @"(?:\w*TEXT\w*)" +
                                             @"|(?:\w*CHAR\w*)" +
                                             @"|(?:\w*CLOB\w*)" +
                                             @"|(?:\w*INT\w*)" +
                                             @"|(?:\w*BLOB\w*)" +
                                             @"|(?:\w*REAL\w*)" +
                                             @"|(?:\w*FLOA\w*)" +
                                             @"|(?:\w*DOUB\w*)" +
                                             @"|(?:\w*BOOL\w*)" +
                                             @"|(?:\w*DECI\w*)" +
                                             @"|(?:\w*DATE\w*)" +
                                             @"|(?:\w*NUM\w*)";
            const string sqliteColumnConstraints = @"(?:CONSTRAINT\s*\S+\s*)?" +
                                                   @"(?:PRIMARY KEY)" +
                                                   @"|(?:AUTOINCREMENT)" +
                                                   @"|(?:NOT NULL)" +
                                                   @"|(?:UNIQUE)";
            string sqliteColumnDefinition = String.Format("(?:(?:(\"[^\"]+)\"(?:\\s+(?:{0}))(?:\\s+(?:{1}))*)|(?:\\s*([^,]+)(?:\\s+(?:{0}))(?:\\s+(?:{1}))*))", sqliteTypePattern, sqliteColumnConstraints);
            string sqliteColumnDefinitionList = String.Format("({0})+", sqliteColumnDefinition);
            var columnMatches = Regex.Matches(columns, sqliteColumnDefinitionList, RegexOptions.IgnoreCase);
            if (columnMatches.Count == 0)
                throw new Exception("failed to find any columns in the create table statement: " + createTableStatement);
            var keptColumnDefinitions = new List<string>();
            var keptColumnNames = new List<string>();
            foreach (Match columnMatch in columnMatches)
            {
                string columnDefinition = columnMatch.Value;
                string columnName = columnMatch.Groups[3].Value;
                if (columnNames.All(o => columnName.Trim('"') != o))
                {
                    keptColumnDefinitions.Add(columnDefinition);
                    keptColumnNames.Add(columnName);
                }
            }
            string newColumnDefinitions = String.Join(", ", keptColumnDefinitions);
            string newColumnNames = String.Join(", ", keptColumnNames);
            string newTableName = String.Format("\"{0}_new\"", table.Trim('"', ' '));
            string newCreateTableStatement = createTableStatement.Replace("CREATE TABLE " + table, "CREATE TABLE " + newTableName).Replace(columns, newColumnDefinitions);
            return String.Format(@"{3};
                                 INSERT INTO {0} ({2}) SELECT {2} FROM {1};
                                 DROP TABLE {1}; ALTER TABLE {0} RENAME TO {1};",
                                 newTableName, tableName, newColumnNames, newCreateTableStatement);
        }

        void testModelFile(TestModel testModel, string filename)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(filename))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                Assert.AreEqual(SchemaUpdater.CurrentSchemaRevision, session.UniqueResult<int>("SELECT SchemaRevision FROM About"));

                testModel.TestOverallCounts();
                testModel.TestSanity();
                testModel.TestProteins();
                testModel.TestPeptides();
                testModel.TestPeptideInstances();
                testModel.TestSpectrumSourceGroups();
                testModel.TestSpectrumSources();
                testModel.TestSpectra();
                testModel.TestAnalyses();
                testModel.TestPeptideSpectrumMatches();
                testModel.TestModifications();
            }
        }

        void testFilterFile(string filename)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(filename))
            using (var session = sessionFactory.OpenStatelessSession())
            {
                Assert.AreEqual(SchemaUpdater.CurrentSchemaRevision, Convert.ToInt32(session.CreateSQLQuery("SELECT SchemaRevision FROM About").UniqueResult()));
            }
        }

        class DowngradeInfo
        {
            public DowngradeInfo(string rawSql, string tableToDropColumnsFrom = null, params string[] droppedColumns)
            {
                RawSql = rawSql;
                TableToDropColumnsFrom = tableToDropColumnsFrom;
                DroppedColumns = droppedColumns;
            }

            public string RawSql { get; private set; }
            public string TableToDropColumnsFrom { get; private set; }
            public string[] DroppedColumns { get; private set; }
        }

        void downgradeToRevision(string filename, int revision)
        {
            using (var connection = new SQLiteConnection(String.Format("Data Source={0};Version=3", filename)))
            {
                connection.Open();

                var comparer = new ComparisonForwarder<int>((a, b) => b.CompareTo(a));
                var downgradeStatementMap = new SortedList<int, List<DowngradeInfo>>(comparer)
                {
                    // 18: swapping N/C channels for TMT reporter ions 127 and 129
                    {17, new List<DowngradeInfo> {new DowngradeInfo(null, "ProteinMetadata", "Hash")}},
                    {16, new List<DowngradeInfo> {new DowngradeInfo(null, "FilterHistory", "PrecursorMzTolerance")}},
                    //{15, "DROP TABLE IsobaricSampleMapping"},
                    {14, new List<DowngradeInfo> {new DowngradeInfo(null, "SpectrumSource", "QuantitationSettings")}},
                    {13, new List<DowngradeInfo> {new DowngradeInfo("DROP TABLE PeptideModificationProbability")}},
                    {12, new List<DowngradeInfo> {new DowngradeInfo("DROP TABLE XICMetrics; CREATE TABLE XICMetrics (PsmId INTEGER PRIMARY KEY, PeakIntensity NUMERIC, PeakArea NUMERIC, PeakSNR NUMERIC, PeakTimeInSeconds NUMERIC);")}},
                    {11, new List<DowngradeInfo> {new DowngradeInfo(null, "FilterHistory", "GeneGroups", "Genes")}},
                    {10, new List<DowngradeInfo> {new DowngradeInfo("DROP TABLE XICMetrics; DROP TABLE IF EXISTS XICMetricsSettings")}},
                    {9, new List<DowngradeInfo> {new DowngradeInfo(null, "FilterHistory", "GeneLevelFiltering", "DistinctMatchFormat")}},
                    {8, new List<DowngradeInfo>
                        {
                            new DowngradeInfo(null, "Protein", "GeneId", "GeneGroup"),
                            new DowngradeInfo(null, "ProteinMetadata", "TaxonomyId", "GeneName", "Chromosome", "GeneFamily", "GeneDescription")
                        }
                    },

                    {7, new List<DowngradeInfo>
                        {
                            new DowngradeInfo("ALTER TABLE FilterHistory RENAME TO FilteringCriteria;",
                                              "FilterHistory",
                                              "Clusters", "ProteinGroups", "Proteins", "DistinctPeptides", "DistinctMatches", "FilteredSpectra", "ProteinFDR", "PeptideFDR", "SpectrumFDR")
                        }
                    },

                    {4, new List<DowngradeInfo>
                        {
                            new DowngradeInfo(@"CREATE TABLE NewSpectrumSource (Id INTEGER PRIMARY KEY, Name TEXT, URL TEXT, Group_ INT, MsDataBytes BLOB, TotalSpectraMS1 INT, TotalIonCurrentMS1 NUMERIC, TotalSpectraMS2 INT, TotalIonCurrentMS2 NUMERIC, QuantitationMethod INT);
                                                INSERT INTO NewSpectrumSource SELECT ss.Id, Name, URL, Group_, MsDataBytes, TotalSpectraMS1, TotalIonCurrentMS1, TotalSpectraMS2, TotalIonCurrentMS2, QuantitationMethod FROM SpectrumSource ss, SpectrumSourceMetadata ssmd WHERE ss.Id=ssmd.Id;
                                                DROP TABLE SpectrumSource;
                                                ALTER TABLE NewSpectrumSource RENAME TO SpectrumSource;
                                                DROP TABLE SpectrumSourceMetadata;
                                                DROP TABLE DistinctMatchQuantitation;
                                                CREATE TABLE DistinctMatchQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);")
                        }
                    },

                    {3, new List<DowngradeInfo> {new DowngradeInfo(@"DROP TABLE SpectrumQuantitation;
                                                                     DROP TABLE DistinctMatchQuantitation;
                                                                     DROP TABLE PeptideQuantitation;
                                                                     DROP TABLE ProteinQuantitation;",
                                                                   "SpectrumSource",
                                                                   "TotalSpectraMS1", "TotalIonCurrentMS1", "TotalSpectraMS2", "TotalIonCurrentMS2", "QuantitationMethod")
                    }},

                    {2, new List<DowngradeInfo> {new DowngradeInfo(@"CREATE TABLE NewSpectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);
                                                                     INSERT INTO NewSpectrum SELECT Id, Source, Index_, NativeID, PrecursorMZ, 0 FROM Spectrum;
                                                                     DROP TABLE Spectrum;
                                                                     ALTER TABLE NewSpectrum RENAME TO Spectrum;")
                    }},

                    {1, new List<DowngradeInfo> {new DowngradeInfo(@"CREATE TABLE NewPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);
                                                                     INSERT INTO NewPeptideSpectrumMatch SELECT Id, Spectrum, Analysis, Peptide, QValue, ObservedNeutralMass, ObservedNeutralMass-MolecularWeightError, MonoisotopicMassError, MolecularWeightError, Rank, Charge FROM PeptideSpectrumMatch;
                                                                     DROP TABLE PeptideSpectrumMatch;
                                                                     ALTER TABLE NewPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch;
                                                                     DROP TABLE About;")
                    }}
                };
                for (int i = 0; i < downgradeStatementMap.Count && downgradeStatementMap.Keys[i] > revision; ++i)
                {
                    foreach (var downgradeInfo in downgradeStatementMap.Values[i])
                    {
                        var sql = new StringBuilder();
                        if (downgradeInfo.TableToDropColumnsFrom != null && downgradeInfo.DroppedColumns != null)
                            sql.Append(dropColumns(connection, downgradeInfo.TableToDropColumnsFrom, downgradeInfo.DroppedColumns));
                        if (downgradeInfo.RawSql != null)
                            sql.Append(downgradeInfo.RawSql);
                        connection.ExecuteNonQuery(sql.ToString());
                    }
                }

                if (revision > 0)
                    connection.ExecuteNonQuery("UPDATE About SET SchemaRevision = " + revision.ToString());
            }
        }

        [TestMethod]
        [TestCategory("Model")]
        public void TestSchemaUpdater()
        {
            Assert.AreEqual(18, SchemaUpdater.CurrentSchemaRevision);

            var testModel = new TestModel() { TestContext = TestContext };
            TestModel.ClassInitialize(TestContext);
            testModel.TestInitialize();
            testModelFile(testModel, "testModel.idpDB");

            string filename = null;

            // test all revisions without a data filter applied
            // we don't need to test upgrade from 5 to 6; it simply forces reapplication of the basic filters
            // we don't need to test upgrade from 4 to 5; it's a simple null value fix

            filename = "testModel-v4.idpDB";
            File.Copy("testModel.idpDB", filename, true);
            downgradeToRevision(filename, 4);
            testModelFile(testModel, filename);

            filename = "testModel-v3.idpDB";
            File.Copy("testModel.idpDB", filename, true);
            downgradeToRevision(filename, 3);
            testModelFile(testModel, filename);

            filename = "testModel-v2.idpDB";
            File.Copy("testModel.idpDB", filename, true);
            downgradeToRevision(filename, 2);
            testModelFile(testModel, filename);

            filename = "testModel-v1.idpDB";
            File.Copy("testModel.idpDB", filename, true);
            downgradeToRevision(filename, 1);
            testModelFile(testModel, filename);

            filename = "testModel-v0.idpDB";
            File.Copy("testModel.idpDB", filename, true);
            downgradeToRevision(filename, 0);
            testModelFile(testModel, filename);


            // test all revisions with a data filter applied (only check that the update worked this time)

            File.Copy("testModel.idpDB", "testFilter.idpDB", true);
            var dataFilter = new DataFilter { MaximumQValue = 1 };
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testFilter.idpDB"))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                dataFilter.ApplyBasicFilters(session);
            }

            filename = "testFilter-v4.idpDB";
            File.Copy("testFilter.idpDB", filename, true);
            downgradeToRevision(filename, 4);
            testFilterFile(filename);

            filename = "testFilter-v3.idpDB";
            File.Copy("testFilter.idpDB", filename, true);
            downgradeToRevision(filename, 3);
            testFilterFile(filename);

            filename = "testFilter-v2.idpDB";
            File.Copy("testFilter.idpDB", filename, true);
            downgradeToRevision(filename, 2);
            testFilterFile(filename);

            filename = "testFilter-v1.idpDB";
            File.Copy("testFilter.idpDB", filename, true);
            downgradeToRevision(filename, 1);
            testFilterFile(filename);

            filename = "testFilter-v0.idpDB";
            File.Copy("testFilter.idpDB", filename, true);
            downgradeToRevision(filename, 0);
            testFilterFile(filename);
        }

        [TestMethod]
        [TestCategory("Utility")]
        public void TestIsPathOnFixedDrive()
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives.Where(o => o.DriveType == DriveType.Fixed))
                Assert.IsTrue(Util.IsPathOnFixedDrive(drive.RootDirectory.Name));
            foreach (var drive in drives.Where(o => o.DriveType != DriveType.Fixed))
                Assert.IsFalse(Util.IsPathOnFixedDrive(drive.RootDirectory.Name));
            Assert.IsFalse(Util.IsPathOnFixedDrive(@"\\this\is\not\a\real\path\but\it\is\UNC\so\it\doesn't\matter"));
            Assert.IsFalse(Util.IsPathOnFixedDrive(@"http://this/is/not/a/real/path/but/it/is/URI/so/it/doesn't/matter"));

            Assert.AreEqual(Util.IsPathOnFixedDrive(Directory.GetCurrentDirectory()),
                            Util.IsPathOnFixedDrive(@"this/path/is/assumed/to/in/the/working/directory"));
        }

        [TestMethod]
        [TestCategory("Utility")]
        public void TestLinqExtensions()
        {
            IList<int> intList = new List<int>       { 6,  3,  4, 42, 123, 666};
            IList<double> dblList = new List<double> {.1, .2, .3, .4,  .5,  .6};
            intList.Sort(dblList);
            Assert.IsTrue(intList.SequenceEqual(new List<int> {     3,  4,  6, 42, 123, 666 }));
            Assert.IsTrue(dblList.SequenceEqual(new List<double> { .2, .3, .1, .4,  .5,  .6 }));

            Assert.AreEqual(1, intList.BinarySearch(4));
            Assert.AreEqual(~2, intList.BinarySearch(5));

            Assert.AreEqual(5, intList.LowerBound(666));
            Assert.AreEqual(6, intList.LowerBound(667));
            Assert.AreEqual(4, intList.LowerBound(45));
        }
    }
}
