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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//


#include "SchemaUpdater.hpp"
#include "sqlite3.h"
#include "sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/misc/SHA1.h"
#include "pwiz/utility/misc/unit.hpp"
#include "IdpSqlExtensions.hpp"


using namespace pwiz::util;
namespace sqlite = sqlite3pp;


BEGIN_IDPICKER_NAMESPACE

const int CURRENT_SCHEMA_REVISION = 18;

namespace SchemaUpdater {

namespace {

    vector<double> blobToDoubleArray(const sqlite::query::rows& row, size_t elementCount, int index)
    {
        REQUIRE(elementCount == row.column_bytes(index) / sizeof(double));
        const void* blob = row.get<const void*>(index);
        const double* valueArray = reinterpret_cast<const double*>(blob);
        return vector<double>(valueArray, valueArray + elementCount);
    }

    TEST_CASE("DistinctDoubleArraySum and DistinctTukeyBiweightAverage tests") {
        sqlite::database db(":memory:");
        db.load_extension("IdpSqlExtensions");

        db.execute("CREATE TABLE test (Group_ INT, Values_ BLOB)");

        sqlite::command insertTestValues(db, "INSERT INTO test (Group_, Values_) VALUES (?, ?)");

        vector<vector<double> > testValues{
            vector<double> { 1.0, 2.0, 3.0, 4.0 },  // 2  4  6  8
            vector<double> { 2.0, 4.0, 8.0, 16.0 }, // 4  8 16 32
            vector<double> { 3.0, 6.0, 9.0, 14.0 }, // 6 12 18 28
            vector<double> { 3.0, 6.0, 9.0, 14.0 } // duplicate values ignored
        };
        for (int group = 1; group <= 2; ++group)
            for (size_t row = 0; row < testValues.size(); ++row)
            {
                vector<double> valueCopy(testValues[row]);
                for (double& d : valueCopy) d *= group; // give each group unique values (base values * group id)

                insertTestValues.bind(1, group);
                insertTestValues.bind(2, static_cast<void*>(&valueCopy[0]), valueCopy.size() * sizeof(double));
                insertTestValues.execute();
                insertTestValues.reset();
            }

        SUBCASE("plain sum") {
            auto values = blobToDoubleArray(*sqlite::query(db, "SELECT DISTINCT_DOUBLE_ARRAY_SUM(Values_) FROM test").begin(), 4, 0);
            CHECK(values == vector<double> { 6.0 + 12, 12.0 + 24, 20.0 + 40, 34.0 + 68 });
        }

        SUBCASE("plain sum by group") {
            sqlite::query q(db, "SELECT DISTINCT_DOUBLE_ARRAY_SUM(Values_) FROM test GROUP BY Group_ ORDER BY Group_");
            auto itr = q.begin();
            auto values = blobToDoubleArray(*itr, 4, 0); ++itr;
            CHECK(values == vector<double> { 6.0, 12.0, 20.0, 34.0 });

            values = blobToDoubleArray(*itr, 4, 0); ++itr;
            CHECK(values == vector<double> { 12.0, 24.0, 40.0, 68.0 });
        }

        SUBCASE("Tukey Biweight average") {
            auto values = blobToDoubleArray(*sqlite::query(db, "SELECT DISTINCT_DOUBLE_ARRAY_TUKEY_BIWEIGHT_AVERAGE(Values_) FROM test").begin(), 4, 0);
            CHECK(values == ~(vector<double> { 2.586556, 5.173098, 9.297351, 16.29861 }));
        }

        SUBCASE("Tukey Biweight average by group") {
            sqlite::query q(db, "SELECT DISTINCT_DOUBLE_ARRAY_TUKEY_BIWEIGHT_AVERAGE(Values_) FROM test GROUP BY Group_ ORDER BY Group_");
            auto itr = q.begin();
            auto values = blobToDoubleArray(*itr, 4, 0); ++itr;
            CHECK(values == ~(vector<double> { 2.0, 4.0, 8.479601, 14.959201 }));

            values = blobToDoubleArray(*itr, 4, 0); ++itr;
            CHECK(values == ~(vector<double> { 4.0, 8.0, 16.9592, 29.9184 }));
        }

        SUBCASE("Tukey Biweight log average") {
            auto values = blobToDoubleArray(*sqlite::query(db, "SELECT DISTINCT_DOUBLE_ARRAY_TUKEY_BIWEIGHT_LOG_AVERAGE(Values_) FROM test").begin(), 4, 0);
            CHECK(values == ~(vector<double> { 0.9408912, 1.6340384, 2.1640926, 2.6651840 }));
        }
    }

    TEST_CASE("GroupConcatEx tests") {
        sqlite::database db(":memory:");
        db.load_extension("IdpSqlExtensions");

        db.execute("CREATE TABLE test (Group_ INT, Values_ TEXT)");

        sqlite::command insertTestValues(db, "INSERT INTO test (Group_, Values_) VALUES (?, ?)");

        vector<string> testValues{ "ABC", "XYZ", "123" }; // ABC_2, XYZ_2, 123_3

        for (int group = 1; group <= 2; ++group)
            for (size_t row = 0; row < testValues.size(); ++row)
            {
                string valueCopy(testValues[row]);
                valueCopy = valueCopy + "_" + lexical_cast<string>(group); // give each group unique values (base values + group id)

                insertTestValues.bind(1, group);
                insertTestValues.bind(2, valueCopy);
                insertTestValues.execute();
                insertTestValues.reset();
            }
        insertTestValues.binder() << 1 << ""; // group with an empty string
        insertTestValues.execute();
        insertTestValues.reset();
        insertTestValues.binder() << 2 << sqlite::ignore; // group with a null string
        insertTestValues.execute();
        insertTestValues.reset();
        insertTestValues.binder() << 3 << ""; // entirely empty group
        insertTestValues.execute();
        insertTestValues.reset();
        insertTestValues.binder() << 4 << sqlite::ignore; // entirely null group
        insertTestValues.execute();
        insertTestValues.reset();

        // NB: empty strings should be included in group_concat, null strings should not

        SUBCASE("global group_concat with default separator") {
            CHECK(sqlite::query(db, "SELECT GROUP_CONCAT(Values_) FROM test").begin()->get<string>(0) == "ABC_1,XYZ_1,123_1,ABC_2,XYZ_2,123_2,,");
        }

        SUBCASE("global group_concat with default separator") {
            sqlite::query q(db, "SELECT GROUP_CONCAT(Values_) FROM test GROUP BY Group_ ORDER BY Group_");
            auto itr = q.begin();
            auto values = itr->get<boost::optional<string> >(0); ++itr;
            CHECK(*values == "ABC_1,XYZ_1,123_1,");

            values = itr->get<boost::optional<string> >(0); ++itr;
            CHECK(*values == "ABC_2,XYZ_2,123_2");

            values = itr->get<boost::optional<string> >(0); ++itr;
            CHECK(*values == "");

            values = itr->get<boost::optional<string> >(0); ++itr;
            CHECK(values.is_initialized() == false); // null is translated to empty std::string

            CHECK(itr == q.end());
        }


        SUBCASE("global group_concat with ; separator") {
            IDPicker::setGroupConcatSeparator(";");
            CHECK(sqlite::query(db, "SELECT GROUP_CONCAT(Values_) FROM test").begin()->get<string>(0) == "ABC_1;XYZ_1;123_1;ABC_2;XYZ_2;123_2;;");
        }

        SUBCASE("global group_concat with ; separator") {
            IDPicker::setGroupConcatSeparator(";");
            sqlite::query q(db, "SELECT GROUP_CONCAT(Values_) FROM test GROUP BY Group_ ORDER BY Group_");
            auto itr = q.begin();
            auto values = itr->get<boost::optional<string> >(0); ++itr;
            CHECK(*values == "ABC_1;XYZ_1;123_1;");

            values = itr->get<boost::optional<string> >(0); ++itr;
            CHECK(*values == "ABC_2;XYZ_2;123_2");

            values = itr->get<boost::optional<string> >(0); ++itr;
            CHECK(*values == "");

            values = itr->get<boost::optional<string> >(0); ++itr;
            CHECK(values.is_initialized() == false); // null is translated to empty std::string

            CHECK(itr == q.end());
        }
    }

    TEST_CASE("SortUnmappedLast tests") {

        sqlite::database db(":memory:");
        db.load_extension("IdpSqlExtensions");

        IDPicker::setGroupConcatSeparator(",");

        CHECK("Bar,Baz,Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Foo,Baz,Bar')").begin()->get<string>(0));
        CHECK("Bar,Baz,Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Foo,Bar,Baz')").begin()->get<string>(0));
        CHECK("Bar,Baz,Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Bar,Baz,Foo')").begin()->get<string>(0));

        CHECK("Bar,Foo,Unmapped_Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Foo,Bar,Unmapped_Foo')").begin()->get<string>(0));
        CHECK("Bar,Foo,Unmapped_Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Foo,Unmapped_Foo,Bar')").begin()->get<string>(0));
        CHECK("Bar,Foo,Unmapped_Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Unmapped_Foo,Bar,Foo')").begin()->get<string>(0));
        CHECK("Bar,Foo,Unmapped_Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Unmapped_Foo,Foo,Bar')").begin()->get<string>(0));

        CHECK("Bar,Unmapped_Baz,Unmapped_Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Bar,Unmapped_Baz,Unmapped_Foo')").begin()->get<string>(0));
        CHECK("Bar,Unmapped_Baz,Unmapped_Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Unmapped_Baz,Bar,Unmapped_Foo')").begin()->get<string>(0));
        CHECK("Bar,Unmapped_Baz,Unmapped_Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Unmapped_Baz,Unmapped_Foo,Bar')").begin()->get<string>(0));
        CHECK("Bar,Unmapped_Baz,Unmapped_Foo" == sqlite::query(db, "SELECT SORT_UNMAPPED_LAST('Unmapped_Foo,Unmapped_Baz,Bar')").begin()->get<string>(0));
    }

void update_17_to_18(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 17, CURRENT_SCHEMA_REVISION, "updating schema version")

    sqlite::command cmd(db, "UPDATE SpectrumQuantitation SET TMT_ReporterIonIntensities = ? WHERE Id = ?");
    sqlite::query q(db, "SELECT Id, TMT_ReporterIonIntensities FROM SpectrumQuantitation");

    for(sqlite::query::rows row : q)
    {
        sqlite3_int64 id = row.get<sqlite3_int64>(0);
        const void* blob = row.get<const void*>(1);
        double* valueArray = reinterpret_cast<double*>(const_cast<void*>(blob));

        if (row.column_bytes(1) / sizeof(double) != 10)
            continue;

        // swap channels 1/2 and 5/6
        std::swap(*(valueArray + 1), *(valueArray + 2));
        std::swap(*(valueArray + 5), *(valueArray + 6));

        cmd.bind(1, reinterpret_cast<void*>(valueArray), 10 * sizeof(double));
        cmd.bind(2, id);
        cmd.execute();
        cmd.reset();
    }

    //update_17_to_18(db, ilr, vacuumNeeded);
}

TEST_CASE("update_17_to_18") {
    sqlite::database db(":memory:");
    db.load_extension("IdpSqlExtensions");

    db.execute("CREATE TABLE SpectrumQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC)");

    sqlite::command insertTestValues(db, "INSERT INTO SpectrumQuantitation (Id, TMT_ReporterIonIntensities) VALUES (?, ?)");

    vector<double> tmtValues { 126, 127.0, 127.1, 128.0, 128.1, 129.0, 129.1, 130.0, 130.1, 131 };
    sqlite::statement::blob tmtBlob(tmtValues.data(), sizeof(double) * tmtValues.size());
    insertTestValues.binder() << 1 << tmtBlob; insertTestValues.step(); insertTestValues.reset();
    insertTestValues.binder() << 2 << tmtBlob; insertTestValues.step(); insertTestValues.reset();
    insertTestValues.binder() << 3 << tmtBlob; insertTestValues.step(); insertTestValues.reset();

    bool vacuumNeeded = false;
    update_17_to_18(db, NULL, vacuumNeeded);

    sqlite::query q(db, "SELECT TMT_ReporterIonIntensities FROM SpectrumQuantitation");
    for (sqlite::query::rows row : q)
    {
        auto values = blobToDoubleArray(row, 10, 0);
        CHECK(values == vector<double> { 126, 127.1, 127.0, 128.0, 128.1, 129.1, 129.0, 130.0, 130.1, 131 });
    }
}


void update_16_to_17(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 16, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("ALTER TABLE ProteinMetadata ADD COLUMN Hash BLOB");
    sqlite::command cmd(db, "UPDATE ProteinMetadata SET Hash = ? WHERE Id = ?");
    sqlite::query q(db, "SELECT Id, Sequence FROM ProteinData");
    sqlite3_int64 proId;
    string sequence;
    CSHA1 hasher;
    char hash[20];

    for(sqlite::query::rows row : q)
    {
        row.getter() >> proId >> sequence;

        hasher.Reset();
        hasher.Update(reinterpret_cast<unsigned char*>(&sequence[0]), sequence.length());
        hasher.Final();
        hasher.GetHash(reinterpret_cast<unsigned char*>(hash));

        cmd.bind(1, reinterpret_cast<void*>(hash), 20);
        cmd.bind(2, proId);
        cmd.execute();
        cmd.reset();
    }

    update_17_to_18(db, ilr, vacuumNeeded);
}


void update_15_to_16(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 15, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("ALTER TABLE FilterHistory ADD COLUMN PrecursorMzTolerance TEXT");

    update_16_to_17(db, ilr, vacuumNeeded);
}

void update_14_to_15(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 14, CURRENT_SCHEMA_REVISION, "updating schema version")
    db.execute("CREATE TABLE IF NOT EXISTS IsobaricSampleMapping (GroupId INTEGER PRIMARY KEY, Samples TEXT)");

    update_15_to_16(db, ilr, vacuumNeeded);
}

void update_13_to_14(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 13, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("ALTER TABLE SpectrumSource ADD COLUMN QuantitationSettings TEXT");
    
    try
    {
        sqlite::command cmd(db, "UPDATE SpectrumSource SET "
                                "TotalSpectraMS1 = ?, TotalIonCurrentMS1 = 0, "
                                "TotalSpectraMS2 = 0, TotalIonCurrentMS2 = 0, "
                                "QuantitationMethod = ?, "
                                "QuantitationSettings = ? "
                                "WHERE Id = ?");

        sqlite::query q(db, "SELECT SourceId, TotalSpectra, Settings FROM XICMetricsSettings");
        sqlite3_int64 sourceId;
        int totalSpectra;
        string settings;

        for(sqlite::query::rows row : q)
        {
            row.getter() >> sourceId >> totalSpectra >> settings;

            cmd.binder() << totalSpectra <<
                            1 << // CONSIDER: include Embedder.hpp to access QuantitationMethod::LabelFree::value() ?
                            settings <<
                            sourceId;
            cmd.execute();
            cmd.reset();
        }

        db.execute("DROP TABLE XICMetricsSettings");
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());
    }

    update_14_to_15(db, ilr, vacuumNeeded);
}

void update_12_to_13(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 12, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("CREATE TABLE IF NOT EXISTS PeptideModificationProbability(PeptideModification INTEGER PRIMARY KEY, Probability NUMERIC)");

    update_13_to_14(db, ilr, vacuumNeeded);
}

void update_11_to_12(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 11, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("CREATE TABLE TempXIC (Id INTEGER PRIMARY KEY, DistinctMatch INTEGER, SpectrumSource INTEGER, Peptide INTEGER, PeakIntensity NUMERIC, PeakArea NUMERIC, PeakSNR NUMERIC, PeakTimeInSeconds NUMERIC)");
    try
    {
        db.execute("INSERT INTO TempXIC (DistinctMatch, SpectrumSource, Peptide, PeakIntensity, PeakArea, PeakSNR, PeakTimeInSeconds)"
                   "   SELECT dm.distinctMatchID, s.source, psm.peptide, xic.PeakIntensity, xic.PeakArea, xic.PeakSNR, xic.PeakTimeInSeconds"
                   "   FROM XICMetrics xic"
                   "   JOIN PeptideSpectrumMatch psm on psm.Id=xic.PsmId"
                   "   JOIN Spectrum s ON s.id = psm.Spectrum"
                   "   JOIN DistinctMatch dm ON dm.PsmId = psm.Id"
                   "   GROUP BY dm.DistinctMatchId, s.Source");
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // no DistinctMatch table
            throw runtime_error(e.what());
    }
    db.execute("DROP TABLE XICMetrics");
    db.execute("ALTER TABLE TempXIC RENAME TO XICMetrics");
    db.execute("CREATE INDEX XICMetrics_MatchSourcePeptide ON XICMetrics (DistinctMatch,SpectrumSource,Peptide);");

    update_12_to_13(db, ilr, vacuumNeeded);
}

void update_10_to_11(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 10, CURRENT_SCHEMA_REVISION, "updating schema version")

    // create new table with GeneGroups and Genes columns
    db.execute("CREATE TABLE FilterHistoryNew (Id INTEGER PRIMARY KEY, MaximumQValue NUMERIC, MinimumDistinctPeptides INT, MinimumSpectra INT,  MinimumAdditionalPeptides INT,\n"
                "                               MinimumSpectraPerDistinctMatch INT, MinimumSpectraPerDistinctPeptide INT, MaximumProteinGroupsPerPeptide INT,\n"
                "                               GeneLevelFiltering INT, DistinctMatchFormat TEXT,\n"
                "                               Clusters INT, ProteinGroups INT, Proteins INT, GeneGroups INT, Genes INT, DistinctPeptides INT, DistinctMatches INT, FilteredSpectra INT,\n"
                "                               ProteinFDR NUMERIC, PeptideFDR NUMERIC, SpectrumFDR NUMERIC)");

    // insert old values into new table (with default values for new columns)
    db.execute("INSERT INTO FilterHistoryNew SELECT Id, MaximumQValue, MinimumDistinctPeptides, MinimumSpectra, MinimumAdditionalPeptides,\n"
                "                                    MinimumSpectraPerDistinctMatch, MinimumSpectraPerDistinctPeptide, MaximumProteinGroupsPerPeptide,\n"
                "                                    GeneLevelFiltering, DistinctMatchFormat,\n"
                "                                    Clusters, ProteinGroups, Proteins, 0, 0, DistinctPeptides, DistinctMatches, FilteredSpectra,\n"
                "                                    ProteinFDR, PeptideFDR, SpectrumFDR"
                "                             FROM FilterHistory");

    // drop old table and rename new table
    db.execute("DROP TABLE FilterHistory; ALTER TABLE FilterHistoryNew RENAME TO FilterHistory");

    update_11_to_12(db, ilr, vacuumNeeded);
}


void update_9_to_10(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 9, CURRENT_SCHEMA_REVISION, "updating schema version")

    // add XICMetrics
    db.execute("CREATE TABLE IF NOT EXISTS XICMetrics (PsmId INTEGER PRIMARY KEY, PeakIntensity NUMERIC, PeakArea NUMERIC, PeakSNR NUMERIC, PeakTimeInSeconds NUMERIC);");
    db.execute("CREATE TABLE IF NOT EXISTS XICMetricsSettings (SourceId INTEGER PRIMARY KEY, TotalSpectra INT, Settings STRING);");


    update_10_to_11(db, ilr, vacuumNeeded);
}


void update_8_to_9(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 8, CURRENT_SCHEMA_REVISION, "updating schema version")

    // create new table with GeneLevelFiltering and DistinctMatchFormat columns
    db.execute("CREATE TABLE FilterHistoryNew (Id INTEGER PRIMARY KEY, MaximumQValue NUMERIC, MinimumDistinctPeptides INT, MinimumSpectra INT,  MinimumAdditionalPeptides INT,\n"
                "                               MinimumSpectraPerDistinctMatch INT, MinimumSpectraPerDistinctPeptide INT, MaximumProteinGroupsPerPeptide INT,\n"
                "                               GeneLevelFiltering INT, DistinctMatchFormat TEXT,\n"
                "                               Clusters INT, ProteinGroups INT, Proteins INT, DistinctPeptides INT, DistinctMatches INT, FilteredSpectra INT,\n"
                "                               ProteinFDR NUMERIC, PeptideFDR NUMERIC, SpectrumFDR NUMERIC)");

    // insert old values into new table (with default values for new columns)
    db.execute("INSERT INTO FilterHistoryNew SELECT Id, MaximumQValue, MinimumDistinctPeptidesPerProtein, MinimumSpectraPerProtein, MinimumAdditionalPeptidesPerProtein,\n"
                "                                    MinimumSpectraPerDistinctMatch, MinimumSpectraPerDistinctPeptide, MaximumProteinGroupsPerPeptide,\n"
                "                                    0, '1 0 1 1.0000000',\n"
                "                                    Clusters, ProteinGroups, Proteins, DistinctPeptides, DistinctMatches, FilteredSpectra,\n"
                "                                    ProteinFDR, PeptideFDR, SpectrumFDR"
                "                             FROM FilterHistory");

    // drop old table and rename new table
    db.execute("DROP TABLE FilterHistory; ALTER TABLE FilterHistoryNew RENAME TO FilterHistory");

    update_9_to_10(db, ilr, vacuumNeeded);
}

void update_7_to_8(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 7, CURRENT_SCHEMA_REVISION, "updating schema version")

    try
    {
        // add gene columns to protein tables
        db.execute("ALTER TABLE Protein ADD COLUMN GeneId TEXT");
        db.execute("ALTER TABLE Protein ADD COLUMN GeneGroup INT");
        db.execute("ALTER TABLE ProteinMetadata ADD COLUMN TaxonomyId INT");
        db.execute("ALTER TABLE ProteinMetadata ADD COLUMN GeneName TEXT");
        db.execute("ALTER TABLE ProteinMetadata ADD COLUMN Chromosome TEXT");
        db.execute("ALTER TABLE ProteinMetadata ADD COLUMN GeneFamily TEXT");
        db.execute("ALTER TABLE ProteinMetadata ADD COLUMN GeneDescription TEXT");

        db.execute("ALTER TABLE UnfilteredProtein ADD COLUMN GeneId TEXT");
        db.execute("ALTER TABLE UnfilteredProtein ADD COLUMN GeneGroup INT");
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such") && !bal::contains(e.what(), "duplicate column")) // column or table
            throw runtime_error(e.what());
    }

    update_8_to_9(db, ilr, vacuumNeeded);
}

void update_6_to_7(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 6, CURRENT_SCHEMA_REVISION, "updating schema version")

    // refactor FilteringCriteria table as FilterHistory table
    db.execute("CREATE TABLE IF NOT EXISTS FilterHistory (Id INTEGER PRIMARY KEY, "
                                                         "MaximumQValue NUMERIC, "
                                                         "MinimumDistinctPeptidesPerProtein INT, "
                                                         "MinimumSpectraPerProtein INT, "
                                                         "MinimumAdditionalPeptidesPerProtein INT, "
                                                         "MinimumSpectraPerDistinctMatch INT, "
                                                         "MinimumSpectraPerDistinctPeptide INT, "
                                                         "MaximumProteinGroupsPerPeptide INT, "
                                                         "Clusters INT, "
                                                         "ProteinGroups INT, "
                                                         "Proteins INT, "
                                                         "DistinctPeptides INT, "
                                                         "DistinctMatches INT, "
                                                         "FilteredSpectra INT, "
                                                         "ProteinFDR NUMERIC, "
                                                         "PeptideFDR NUMERIC, "
                                                         "SpectrumFDR NUMERIC"
                                                        ");");

    // delete previous layouts that are no longer valid since a new IPersistentForm (FilterHistoryForm) was added
    db.execute("DELETE FROM LayoutProperty");

    try
    {
        // if the database is currently filtered (FilteringCriteria exists), get the current filter settings
        sqlite::query q(db, "SELECT MaximumQValue, MinimumDistinctPeptidesPerProtein,"
                                   "MinimumSpectraPerProtein, MinimumAdditionalPeptidesPerProtein,"
                                   "MinimumSpectraPerDistinctMatch, MinimumSpectraPerDistinctPeptide,"
                                   "MaximumProteinGroupsPerPeptide "
                            "FROM FilteringCriteria");
        sqlite::query::iterator qItr = q.begin();
        if (qItr == q.end())
            return;
        double maxQValue;
        int minPeptidesPerProtein, minSpectraPerProtein, minAdditionalPeptides;
        int minSpectraPerMatch, minSpectraPerPeptide, maxProteinGroups;
        qItr->getter() >> maxQValue >> minPeptidesPerProtein >> minSpectraPerProtein >> minAdditionalPeptides >>
                          minSpectraPerMatch >> minSpectraPerPeptide >> maxProteinGroups;

        // and the summary counts based on that filter for the new FilterHistory table
        q.prepare("SELECT COUNT(DISTINCT pro.Cluster), "
                         "COUNT(DISTINCT pro.ProteinGroup), "
                         "COUNT(DISTINCT pro.Id), "
                         "SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) "
                  "FROM Protein pro");
        int clusters, proteinGroups, proteins;
        float decoyProteins;
        q.begin()->getter() >> clusters >> proteinGroups >> proteins >> decoyProteins;
        float proteinFDR = 2 * decoyProteins / proteins;
        
        q.prepare("SELECT COUNT(*) FROM Peptide"); int distinctPeptides = q.begin()->get<int>(0);
        q.prepare("SELECT COUNT(DISTINCT DistinctMatchId) FROM DistinctMatch"); int distinctMatches = q.begin()->get<int>(0);
        q.prepare("SELECT COUNT(*) FROM Spectrum"); int filteredSpectra = q.begin()->get<int>(0);
        
        // get the count of peptides that are unambiguously targets or decoys (# of Proteins = # of Decoys OR # of Decoys = 0)
        q.prepare("SELECT COUNT(Peptide)"
                  "FROM (SELECT pep.Id AS Peptide, "
                               "COUNT(DISTINCT pro.Id) AS Proteins, "
                               "SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) AS Decoys, "
                               "CASE WHEN SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS IsDecoy "
                        "FROM Peptide pep "
                        "JOIN PeptideInstance pi ON pep.Id=pi.Peptide "
                        "JOIN Protein pro ON pi.Protein=pro.Id "
                        "GROUP BY pep.Id "
                        "HAVING Proteins=Decoys OR Decoys=0 "
                       ") "
                  "GROUP BY IsDecoy "
                  "ORDER BY IsDecoy");
        vector<int> peptideLevelDecoys;
        for(sqlite::query::rows row : q)
            peptideLevelDecoys.push_back(row.get<int>(0));

        // without both targets and decoys, FDR can't be calculated
        float peptideFDR = 0;
        if (peptideLevelDecoys.size() == 2)
            peptideFDR = 2.0 * peptideLevelDecoys[1] / (peptideLevelDecoys[0] + peptideLevelDecoys[1]);
        
        // get the count of spectra that are unambiguously targets or decoys (# of Proteins = # of Decoys OR # of Decoys = 0)
        q.prepare("SELECT COUNT(Spectrum)"
                  "FROM (SELECT psm.Spectrum, "
                               "COUNT(DISTINCT pro.Id) AS Proteins, "
                               "SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) AS Decoys, "
                               "CASE WHEN SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS IsDecoy "
                        "FROM PeptideSpectrumMatch psm "
                        "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
                        "JOIN Protein pro ON pi.Protein=pro.Id "
                        "GROUP BY psm.Spectrum "
                        "HAVING Proteins=Decoys OR Decoys=0 "
                       ") "
                  "GROUP BY IsDecoy "
                  "ORDER BY IsDecoy");
        vector<int> spectrumLevelDecoys;
        for(sqlite::query::rows row : q)
            spectrumLevelDecoys.push_back(row.get<int>(0));

        // without both targets and decoys, FDR can't be calculated
        float spectrumFDR = 0;
        if (spectrumLevelDecoys.size() == 2)
            spectrumFDR = 2.0 * spectrumLevelDecoys[1] / (spectrumLevelDecoys[0] + spectrumLevelDecoys[1]);

        q.finish();

        sqlite::command insertFilter(db, "INSERT INTO FilterHistory (Id, MaximumQValue, MinimumDistinctPeptidesPerProtein,"
                                                                    "MinimumSpectraPerProtein, MinimumAdditionalPeptidesPerProtein,"
                                                                    "MinimumSpectraPerDistinctMatch, MinimumSpectraPerDistinctPeptide,"
                                                                    "MaximumProteinGroupsPerPeptide,"
                                                                    "Clusters, ProteinGroups, Proteins,"
                                                                    "DistinctPeptides, DistinctMatches, FilteredSpectra,"
                                                                    "ProteinFDR, PeptideFDR, SpectrumFDR"
                                                                   ") VALUES (1,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        insertFilter.binder() << maxQValue << minPeptidesPerProtein << minSpectraPerProtein << minAdditionalPeptides <<
                                 minSpectraPerMatch << minSpectraPerPeptide << maxProteinGroups <<
                                 clusters << proteinGroups << proteins <<
                                 distinctPeptides << distinctMatches << filteredSpectra <<
                                 proteinFDR << peptideFDR << spectrumFDR;
        insertFilter.execute();

        db.execute("DROP TABLE FilteringCriteria");
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());
    }

    update_7_to_8(db, ilr, vacuumNeeded);
}

void update_5_to_6(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 5, CURRENT_SCHEMA_REVISION, "updating schema version")

    // force the basic filters to be reapplied
    db.execute("DROP TABLE IF EXISTS FilteringCriteria");

    update_6_to_7(db, ilr, vacuumNeeded);
}

void update_4_to_5(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 4, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("UPDATE SpectrumSource SET QuantitationMethod = IFNULL(QuantitationMethod, 0),"
               "                          TotalSpectraMS1 = IFNULL(TotalSpectraMS1, 0),"
               "                          TotalSpectraMS2 = IFNULL(TotalSpectraMS2, 0),"
               "                          TotalIonCurrentMS1 = IFNULL(TotalIonCurrentMS1, 0),"
               "                          TotalIonCurrentMS2 = IFNULL(TotalIonCurrentMS2, 0)");

    update_5_to_6(db, ilr, vacuumNeeded);
}

void update_3_to_4(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 3, CURRENT_SCHEMA_REVISION, "updating schema version")

    try
    {
        db.execute("CREATE TABLE SpectrumSourceMetadata (Id INTEGER PRIMARY KEY, MsDataBytes BLOB);"
                   "INSERT INTO SpectrumSourceMetadata SELECT Id, MsDataBytes FROM SpectrumSource;"
                   "CREATE TABLE NewSpectrumSource (Id INTEGER PRIMARY KEY, Name TEXT, URL TEXT, Group_ INT, TotalSpectraMS1 INT, TotalIonCurrentMS1 NUMERIC, TotalSpectraMS2 INT, TotalIonCurrentMS2 NUMERIC, QuantitationMethod INT);"
                   "INSERT INTO NewSpectrumSource SELECT Id, Name, URL, Group_, TotalSpectraMS1, TotalIonCurrentMS1, TotalSpectraMS2, TotalIonCurrentMS2, QuantitationMethod FROM SpectrumSource;"
                   "DROP TABLE SpectrumSource;"
                   "ALTER TABLE NewSpectrumSource RENAME TO SpectrumSource;"
                   "DROP TABLE DistinctMatchQuantitation;"
                   "CREATE TABLE DistinctMatchQuantitation (Id TEXT PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);");
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "exists")) // table already exists
            throw runtime_error(e.what());
    }

    update_4_to_5(db, ilr, vacuumNeeded);
}

void update_2_to_3(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 2, CURRENT_SCHEMA_REVISION, "updating schema version")

    // add empty quantitation tables and quantitative columns to SpectrumSource
    db.execute("CREATE TABLE IF NOT EXISTS SpectrumQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS DistinctMatchQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS PeptideQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS ProteinQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "ALTER TABLE SpectrumSource ADD COLUMN TotalSpectraMS1 INT;"
               "ALTER TABLE SpectrumSource ADD COLUMN TotalIonCurrentMS1 NUMERIC;"
               "ALTER TABLE SpectrumSource ADD COLUMN TotalSpectraMS2 INT;"
               "ALTER TABLE SpectrumSource ADD COLUMN TotalIonCurrentMS2 NUMERIC;"
               "ALTER TABLE SpectrumSource ADD COLUMN QuantitationMethod INT;");

    // continue updating schema
    update_3_to_4(db, ilr, vacuumNeeded);
}

void update_1_to_2(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 1, CURRENT_SCHEMA_REVISION, "updating schema version")

    try
    {
        {
            sqlite::query q(db, "SELECT Id FROM UnfilteredSpectrum LIMIT 1");
            q.begin()->get<int>(0);
        }

        // if UnfilteredSpectrum exists, add an empty ScanTimeInSeconds column
        db.execute("CREATE TABLE NewSpectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);"
                   "INSERT INTO NewSpectrum SELECT Id, Source, Index_, NativeID, PrecursorMZ, 0 FROM UnfilteredSpectrum;"
                   "DROP TABLE UnfilteredSpectrum;"
                   "ALTER TABLE NewSpectrum RENAME TO UnfilteredSpectrum;");
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());
    }

    // add an empty ScanTimeInSeconds column
    db.execute("CREATE TABLE NewSpectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);"
               "INSERT INTO NewSpectrum SELECT Id, Source, Index_, NativeID, PrecursorMZ, 0 FROM Spectrum;"
               "DROP TABLE Spectrum;"
               "ALTER TABLE NewSpectrum RENAME TO Spectrum;");

    // continue updating schema
    update_2_to_3(db, ilr, vacuumNeeded);
}

void update_0_to_1(sqlite::database& db, const IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 0, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("CREATE TABLE About (Id INTEGER PRIMARY KEY, SoftwareName TEXT, SoftwareVersion TEXT, StartTime DATETIME, SchemaRevision INT);"
               "INSERT INTO About VALUES (1, 'IDPicker', '3.0', datetime('now'), " + lexical_cast<string>(CURRENT_SCHEMA_REVISION) + ");");

    try
    {
        {
            sqlite::query q(db, "SELECT Id FROM UnfilteredProtein LIMIT 1");
            q.begin()->get<int>(0);
        }

        // if UnfilteredProtein exists but UnfilteredSpectrum does not, create the filtered Spectrum table
        try
        {
            sqlite::query q(db, "SELECT Id FROM UnfilteredSpectrum LIMIT 1");
            q.begin()->get<int>(0);
        }
        catch (sqlite::database_error& e)
        {
            if (!bal::contains(e.what(), "no such")) // column or table
                throw runtime_error(e.what());

            db.execute("ALTER TABLE Spectrum RENAME TO UnfilteredSpectrum;"
                       "CREATE TABLE Spectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC);"
                       "INSERT INTO Spectrum SELECT * FROM UnfilteredSpectrum WHERE Id IN (SELECT Spectrum FROM PeptideSpectrumMatch);");
        }

        // if UnfilteredProtein exists, replace the UnfilteredPeptideSpectrumMatch's MonoisotopicMass/MolecularWeight columns with a single ObservedNeutralMass column
        try
        {
            sqlite::query q(db, "SELECT ObservedNeutralMass FROM UnfilteredPeptideSpectrumMatch LIMIT 1");
            q.begin()->get<double>(0);
        }
        catch (sqlite::database_error& e)
        {
            if (!bal::contains(e.what(), "no such")) // column or table
                throw runtime_error(e.what());

            db.execute("CREATE TABLE NewPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
                       "INSERT INTO NewPeptideSpectrumMatch SELECT Id, Spectrum, Analysis, Peptide, QValue, MonoisotopicMass, MonoisotopicMassError, MolecularWeightError, Rank, Charge FROM UnfilteredPeptideSpectrumMatch;"
                       "DROP TABLE UnfilteredPeptideSpectrumMatch;"
                       "ALTER TABLE NewPeptideSpectrumMatch RENAME TO UnfilteredPeptideSpectrumMatch;");
        }
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());
    }

    // replace PeptideSpectrumMatch's MonoisotopicMass/MolecularWeight columns with a single ObservedNeutralMass column
    try
    {
        sqlite::query q(db, "SELECT ObservedNeutralMass FROM PeptideSpectrumMatch LIMIT 1");
        q.begin()->get<double>(0);
    }
    catch (sqlite::database_error& e)
    {
        if (!bal::contains(e.what(), "no such")) // column or table
            throw runtime_error(e.what());

        db.execute("CREATE TABLE NewPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
                   "INSERT INTO NewPeptideSpectrumMatch SELECT Id, Spectrum, Analysis, Peptide, QValue, MonoisotopicMass, MonoisotopicMassError, MolecularWeightError, Rank, Charge FROM PeptideSpectrumMatch;"
                   "DROP TABLE PeptideSpectrumMatch;"
                   "ALTER TABLE NewPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch;");
    }

    // continue updating schema
    update_1_to_2(db, ilr, vacuumNeeded);
}

} // namespace


bool update(const string& idpDbFilepath, const IterationListenerRegistry* ilr)
{
    sqlite::database db(idpDbFilepath);

    db.execute("PRAGMA journal_mode=OFF;"
        "PRAGMA synchronous=OFF;"
        "PRAGMA cache_size=50000;"
        IDPICKER_SQLITE_PRAGMA_MMAP);

    return update(db.connected());
}

bool update(sqlite3* idpDbConnection, const IterationListenerRegistry* ilr)
{
    int schemaRevision;
    sqlite::database db(idpDbConnection, false);

    try
    {
        sqlite::query q(db, "SELECT SchemaRevision FROM About");
        schemaRevision = q.begin()->get<int>(0);
    }
    catch (sqlite::database_error&)
    {
        schemaRevision = 0;
    }

    //sqlite::transaction transaction(db);
    bool vacuumNeeded = false;

    if (schemaRevision == 0)
        update_0_to_1(db, ilr, vacuumNeeded);
    else if (schemaRevision == 1)
        update_1_to_2(db, ilr, vacuumNeeded);
    else if (schemaRevision == 2)
        update_2_to_3(db, ilr, vacuumNeeded);
    else if (schemaRevision == 3)
        update_3_to_4(db, ilr, vacuumNeeded);
    else if (schemaRevision == 4)
        update_4_to_5(db, ilr, vacuumNeeded);
    else if (schemaRevision == 5)
        update_5_to_6(db, ilr, vacuumNeeded);
    else if (schemaRevision == 6)
        update_6_to_7(db, ilr, vacuumNeeded);
    else if (schemaRevision == 7)
        update_7_to_8(db, ilr, vacuumNeeded);
    else if (schemaRevision == 8)
        update_8_to_9(db, ilr, vacuumNeeded);
    else if (schemaRevision == 9)
        update_9_to_10(db, ilr, vacuumNeeded);
    else if (schemaRevision == 10)
        update_10_to_11(db, ilr, vacuumNeeded);
    else if (schemaRevision == 11)
        update_11_to_12(db, ilr, vacuumNeeded);
    else if (schemaRevision == 12)
        update_12_to_13(db, ilr, vacuumNeeded);
    else if (schemaRevision == 13)
        update_13_to_14(db, ilr, vacuumNeeded);
    else if (schemaRevision == 14)
        update_14_to_15(db, ilr, vacuumNeeded);
    else if (schemaRevision == 15)
        update_15_to_16(db, ilr, vacuumNeeded);
    else if (schemaRevision == 16)
        update_16_to_17(db, ilr, vacuumNeeded);
    else if (schemaRevision == 17)
        update_17_to_18(db, ilr, vacuumNeeded);
    else if (schemaRevision > CURRENT_SCHEMA_REVISION)
        throw runtime_error("[SchemaUpdater::update] unable to update schema revision " +
                            lexical_cast<string>(schemaRevision) +
                            "; the latest compatible revision is " +
                            lexical_cast<string>(CURRENT_SCHEMA_REVISION) +
                            "; you need a newer version of IDPicker to open this file");
    else
    {
        ITERATION_UPDATE(ilr, CURRENT_SCHEMA_REVISION-1, CURRENT_SCHEMA_REVISION, "schema is current; no update necessary")
        return false; // no update needed
    }

    //transaction.commit();

    // update the schema revision
    db.execute("UPDATE About SET SchemaRevision = " + lexical_cast<string>(CURRENT_SCHEMA_REVISION));
    
    if (vacuumNeeded)
    {
        // necessary for schema updates that change column names using UPDATE SQLITE_MASTER
        try
        {
            db.execute("VACUUM");
        }
        catch (sqlite::database_error&) {}
    }

    return true; // an update was done
}


bool isValidFile(const string& idpDbFilepath)
{
    try
    {
        string uncCompatiblePath = getSQLiteUncCompatiblePath(idpDbFilepath);
        sqlite::database db(uncCompatiblePath);

        // in a valid file, this will throw "already exists"
        db.execute("CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY)");
    }
    catch (sqlite3pp::database_error& e)
    {
        if (bal::contains(e.what(), "already exists"))
            return true;
    }

    // creating the table or any other exception indicates an invalid file
    return false;
}


string getSQLiteUncCompatiblePath(const string& path)
{
    return bal::starts_with(path, "\\\\") ? "\\" + path : path;
}


void createUserSQLiteFunctions(sqlite3* idpDbConnection)
{
    sqlite::database db(idpDbConnection, false);
    db.load_extension("IdpSqlExtensions");
}


} // namespace SchemaUpdater
END_IDPICKER_NAMESPACE
