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

#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "boost/assign.hpp"
#include "Filter.hpp"
#include "SchemaUpdater.hpp"
#include "Logger.hpp"
#include "sqlite3pp.h"


using namespace pwiz::util;
using namespace boost::assign;
using namespace IDPicker;
namespace sqlite = sqlite3pp;


struct TestPSM
{
    sqlite3_int64 protein;
    sqlite3_int64 peptide;
    sqlite3_int64 spectrum;
    int offset;
    int length;
    const char* modifications;
    const char* gene;
};

typedef shared_ptr<sqlite::database> TestDatabase;


/// This function creates simply in-memory test scenarios;
/// protein, peptide, and spectrum ids are the only required attributes;
/// protein/peptide/spectrum are intended to be repeated multiple times;
/// protein length is always 100
template <size_t size>
TestDatabase testCase(const TestPSM (&testPSMs)[size])
{
    TestDatabase dbPtr(new sqlite::database(":memory:"));
    sqlite::database& db = *dbPtr;
    SchemaUpdater::createUserSQLiteFunctions(db.connected());
    
    db.execute("DROP TABLE IF EXISTS About;"
               "CREATE TABLE About (Id INTEGER PRIMARY KEY, SoftwareName TEXT, SoftwareVersion TEXT, StartTime DATETIME, SchemaRevision INT);"
               "INSERT INTO About VALUES (1, 'IDPicker', '3.0', datetime('now'), " + lexical_cast<string>(CURRENT_SCHEMA_REVISION) + ");");

    db.execute("CREATE TABLE IF NOT EXISTS SpectrumSource (Id INTEGER PRIMARY KEY, Name TEXT, URL TEXT, Group_ INT, TotalSpectraMS1 INT, TotalIonCurrentMS1 NUMERIC, TotalSpectraMS2 INT, TotalIonCurrentMS2 NUMERIC, QuantitationMethod INT);"
               "CREATE TABLE IF NOT EXISTS SpectrumSourceMetadata (Id INTEGER PRIMARY KEY, MsDataBytes BLOB);"
               "CREATE TABLE IF NOT EXISTS SpectrumSourceGroup (Id INTEGER PRIMARY KEY, Name TEXT);"
               "CREATE TABLE IF NOT EXISTS SpectrumSourceGroupLink (Id INTEGER PRIMARY KEY, Source INT, Group_ INT);"
               "CREATE TABLE IF NOT EXISTS Spectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);"
               "CREATE TABLE IF NOT EXISTS Analysis (Id INTEGER PRIMARY KEY, Name TEXT, SoftwareName TEXT, SoftwareVersion TEXT, Type INT, StartTime DATETIME);"
               "CREATE TABLE IF NOT EXISTS AnalysisParameter (Id INTEGER PRIMARY KEY, Analysis INT, Name TEXT, Value TEXT);"
               "CREATE TABLE IF NOT EXISTS Modification (Id INTEGER PRIMARY KEY, MonoMassDelta NUMERIC, AvgMassDelta NUMERIC, Formula TEXT, Name TEXT);"
               "CREATE TABLE IF NOT EXISTS Protein (Id INTEGER PRIMARY KEY, Accession TEXT, IsDecoy INT, Cluster INT, ProteinGroup INT, Length INT, GeneId TEXT, GeneGroup INT);"
               "CREATE TABLE IF NOT EXISTS ProteinData (Id INTEGER PRIMARY KEY, Sequence TEXT);"
               "CREATE TABLE IF NOT EXISTS ProteinMetadata (Id INTEGER PRIMARY KEY, Description TEXT, TaxonomyId INT, GeneName TEXT, Chromosome TEXT, GeneFamily TEXT, GeneDescription TEXT);"
               "CREATE TABLE IF NOT EXISTS Peptide (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, PeptideGroup INT, DecoySequence TEXT);"
               "CREATE TABLE IF NOT EXISTS PeptideInstance (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);"
               "CREATE TABLE IF NOT EXISTS PeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
               "CREATE TABLE IF NOT EXISTS PeptideModification (Id INTEGER PRIMARY KEY, PeptideSpectrumMatch INT, Modification INT, Offset INT, Site TEXT);"
               "CREATE TABLE IF NOT EXISTS PeptideSpectrumMatchScore (PsmId INTEGER NOT NULL, Value NUMERIC, ScoreNameId INTEGER NOT NULL, primary key (PsmId, ScoreNameId));"
               "CREATE TABLE IF NOT EXISTS PeptideSpectrumMatchScoreName (Id INTEGER PRIMARY KEY, Name TEXT UNIQUE NOT NULL);"
               "CREATE TABLE IF NOT EXISTS IntegerSet (Value INTEGER PRIMARY KEY);"
               "CREATE TABLE IF NOT EXISTS LayoutProperty (Id INTEGER PRIMARY KEY, Name TEXT, PaneLocations TEXT, HasCustomColumnSettings INT, FormProperties TEXT);"
               "CREATE TABLE IF NOT EXISTS ProteinCoverage (Id INTEGER PRIMARY KEY, Coverage NUMERIC, CoverageMask BLOB);"
               "CREATE TABLE IF NOT EXISTS SpectrumQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS DistinctMatchQuantitation (Id TEXT PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS PeptideQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS ProteinQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
               "CREATE TABLE IF NOT EXISTS QonverterSettings (Id INTEGER PRIMARY KEY, QonverterMethod INT, DecoyPrefix TEXT, RerankMatches INT, Kernel INT, MassErrorHandling INT, MissedCleavagesHandling INT, TerminalSpecificityHandling INT, ChargeStateHandling INT, ScoreInfoByName TEXT);"
               "CREATE TABLE IF NOT EXISTS FilterHistory (Id INTEGER PRIMARY KEY, MaximumQValue NUMERIC, MinimumDistinctPeptides INT, MinimumSpectra INT,  MinimumAdditionalPeptides INT, GeneLevelFiltering INT, PrecursorMzTolerance TEXT,\n"
               "                                          DistinctMatchFormat TEXT, MinimumSpectraPerDistinctMatch INT, MinimumSpectraPerDistinctPeptide INT, MaximumProteinGroupsPerPeptide INT,\n"
               "                                          Clusters INT, ProteinGroups INT, Proteins INT, GeneGroups INT, Genes INT, DistinctPeptides INT, DistinctMatches INT, FilteredSpectra INT, ProteinFDR NUMERIC, PeptideFDR NUMERIC, SpectrumFDR NUMERIC);");

    sqlite::command insertSpectrumSource(db, "INSERT INTO SpectrumSource (Id, Name, Group_) VALUES (?,?,1)");
    sqlite::command insertSpectrum(db, "INSERT INTO Spectrum (Id, Source, Index_, NativeID) VALUES (?,?,?,?)");
    sqlite::command insertProtein(db, "INSERT INTO Protein (Id, Accession, IsDecoy, Length, GeneId) VALUES (?,?,0,?,?)");
    sqlite::command insertProteinData(db, "INSERT INTO ProteinData (Id, Sequence) VALUES (?,'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA')");
    sqlite::command insertPeptideInstance(db, "INSERT INTO PeptideInstance (Id, Protein, Peptide, Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages) VALUES (?,?,?,?,?,1,1,0)");
    sqlite::command insertPeptide(db, "INSERT INTO Peptide (Id, MonoisotopicMass, MolecularWeight) VALUES (?,?,?)");
    sqlite::command insertPeptideSpectrumMatch(db, "INSERT INTO PeptideSpectrumMatch (Id, Spectrum, Analysis, Peptide, QValue, ObservedNeutralMass, MonoisotopicMassError, MolecularWeightError, Rank, Charge) VALUES (?,?,?,?,0,?,?,?,?,?)");
    sqlite::command insertPeptideModification(db, "INSERT INTO PeptideModification (Id, PeptideSpectrumMatch, Modification, Offset, Site) VALUES (?,?,?,?,?)");

    set<sqlite3_int64> sources, spectra, proteins, peptides;
    map<sqlite3_int64, set<sqlite3_int64> > peptideInstances;
    set<string> modifications;
    sqlite3_int64 peptideInstanceId = 0, psmId = 0;
    sqlite3_int64 source = 1;
    sqlite3_int64 analysis = 1;
    int charge = 1;
    for (size_t i = 0, end = sizeof(testPSMs) / sizeof(TestPSM); i < end; ++i)
    {
        const TestPSM& psm = testPSMs[i];

        if (sources.insert(source).second)
        {
            insertSpectrumSource.binder() << source << ("Source" + lexical_cast<string>(source));
            insertSpectrumSource.step();
            insertSpectrumSource.reset();
        }

        bool newSpectrum = spectra.insert(psm.spectrum).second;
        if (newSpectrum)
        {
            insertSpectrum.binder() << psm.spectrum << source << psm.spectrum << ("scan=" + lexical_cast<string>(psm.spectrum));
            insertSpectrum.step();
            insertSpectrum.reset();
        }

        bool newProtein = proteins.insert(psm.protein).second;
        if (newProtein)
        {
            insertProtein.binder() << psm.protein << ("Protein" + lexical_cast<string>(psm.protein)) << 100;
            if (psm.gene)
                insertProtein.bind(4, psm.gene);
            insertProtein.step();
            insertProtein.reset();

            insertProteinData.binder() << psm.protein;
            insertProteinData.step();
            insertProteinData.reset();
        }

        bool newPeptide = peptides.insert(psm.peptide).second;
        if (newPeptide)
        {
            insertPeptide.binder() << psm.peptide << 1234.5 << 1235.6;
            insertPeptide.step();
            insertPeptide.reset();
        }

        if (newSpectrum || newPeptide)
        {
            insertPeptideSpectrumMatch.binder() << ++psmId << psm.spectrum << analysis << psm.peptide << 1234.56 << (1234.5 - 1234.56) << (1235.6 - 1234.56) << 1 << charge;
            insertPeptideSpectrumMatch.step();
            insertPeptideSpectrumMatch.reset();
        }

        if (peptideInstances[psm.peptide].insert(psm.protein).second)
        {
            insertPeptideInstance.binder() << ++peptideInstanceId << psm.protein << psm.peptide << psm.offset << psm.length;
            insertPeptideInstance.step();
            insertPeptideInstance.reset();
        }
    }

    sqlite::command insertInteger(db, "INSERT INTO IntegerSet (Value) VALUES (?)");
    for (int i = 0; i <= 100; ++i)
    {
        insertInteger.binder() << i;
        insertInteger.step();
        insertInteger.reset();
    }

    return dbPtr;
}

template<size_t size>
string maskToString(const short(&mask)[size])
{
    ostringstream result;
    result << std::hex << std::setfill('0');
    for (size_t i=0; i < size; ++i)
        result << mask[i];
    return result.str();
}


void testCoverage()
{
    Filter::Config config;
    config.minDistinctPeptides = 1;
    config.minSpectra = 1;
    config.minAdditionalPeptides = 1;
    config.geneLevelFiltering = false;

    config.minSpectraPerDistinctMatch = 1;
    config.minSpectraPerDistinctPeptide = 1;
    config.maxProteinGroupsPerPeptide = 10;

    config.distinctMatchFormat.isAnalysisDistinct = false;
    config.distinctMatchFormat.isChargeDistinct = true;
    config.distinctMatchFormat.areModificationsDistinct = true;
    config.distinctMatchFormat.modificationMassRoundToNearest = 1.0;

    TestDatabase db;

    const double EPSILON = 0.00001;

    try
    {
        // 1 protein with 1 peptide = 10% coverage with depth 1 (protein length from testCase() is always 100)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 10 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_equal(10.0, sqlite::query(*db, "SELECT Coverage FROM ProteinCoverage WHERE Id=1").begin()->get<double>(0), EPSILON);

            //                     0                             10                            20                            30                            40                            50 ...
            //const short mask[] = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
            //                       0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            //                          0                                       10                                        (next line is 20 and 30, etc.)
            unit_assert_operator_equal("64000000" // size is 100
                                       "01000100010001000100010001000100010001000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000",
                                       sqlite::query(*db, "SELECT HEX(CoverageMask) FROM ProteinCoverage WHERE Id=1").begin()->get<string>(0));
        }

        // 1 protein with 2 adjacent peptides = 20% coverage with depth 1
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 10 },
                { 1, 2, 2, 10, 10 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_equal(20.0, sqlite::query(*db, "SELECT Coverage FROM ProteinCoverage WHERE Id=1").begin()->get<double>(0), EPSILON);

            //                          0                                       10                                        (next line is 20 and 30, etc.)
            unit_assert_operator_equal("64000000" // size is 100
                                       "01000100010001000100010001000100010001000100010001000100010001000100010001000100"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000",
                                       sqlite::query(*db, "SELECT HEX(CoverageMask) FROM ProteinCoverage WHERE Id=1").begin()->get<string>(0));
        }

        // 1 protein with 2 fully overlapping peptides = 10% coverage with depth 2
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 10 },
                { 1, 2, 2, 0, 10 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_equal(10.0, sqlite::query(*db, "SELECT Coverage FROM ProteinCoverage WHERE Id=1").begin()->get<double>(0), EPSILON);

            //                          0                                       10                                        (next line is 20 and 30, etc.)
            unit_assert_operator_equal("64000000" // size is 100
                                       "02000200020002000200020002000200020002000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000",
                                       sqlite::query(*db, "SELECT HEX(CoverageMask) FROM ProteinCoverage WHERE Id=1").begin()->get<string>(0));
        }

        // 1 protein with 2 partially overlapping peptides = 15% coverage with various depth
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 10 },
                { 1, 2, 2, 5, 10 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_equal(15.0, sqlite::query(*db, "SELECT Coverage FROM ProteinCoverage WHERE Id=1").begin()->get<double>(0), EPSILON);

            //                          0                                       10                  15                      (next line is 20 and 30, etc.)
            unit_assert_operator_equal("64000000" // size is 100
                                       "01000100010001000100020002000200020002000100010001000100010000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000",
                                       sqlite::query(*db, "SELECT HEX(CoverageMask) FROM ProteinCoverage WHERE Id=1").begin()->get<string>(0));
        }

        // 1 protein with 10 adjacent peptides = 100% coverage
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 10 },
                { 1, 2, 2, 10, 10 },
                { 1, 3, 3, 20, 10 },
                { 1, 4, 4, 30, 10 },
                { 1, 5, 5, 40, 10 },
                { 1, 6, 6, 50, 10 },
                { 1, 7, 7, 60, 10 },
                { 1, 8, 8, 70, 10 },
                { 1, 9, 9, 80, 10 },
                { 1, 10, 10, 90, 10 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_equal(100.0, sqlite::query(*db, "SELECT Coverage FROM ProteinCoverage WHERE Id=1").begin()->get<double>(0), EPSILON);

            //                          0                                       10                                        (next line is 20 and 30, etc.)
            unit_assert_operator_equal("64000000" // size is 100
                                       "01000100010001000100010001000100010001000100010001000100010001000100010001000100"
                                       "01000100010001000100010001000100010001000100010001000100010001000100010001000100"
                                       "01000100010001000100010001000100010001000100010001000100010001000100010001000100"
                                       "01000100010001000100010001000100010001000100010001000100010001000100010001000100"
                                       "01000100010001000100010001000100010001000100010001000100010001000100010001000100",
                                       sqlite::query(*db, "SELECT HEX(CoverageMask) FROM ProteinCoverage WHERE Id=1").begin()->get<string>(0));
        }

        // 2 proteins with overlapping peptides = check that coverage is independent by protein
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 10 },
                { 1, 2, 2, 5, 10 },
                { 2, 3, 3, 5, 10 },
                { 2, 4, 4, 10, 10 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_equal(15.0, sqlite::query(*db, "SELECT Coverage FROM ProteinCoverage WHERE Id=1").begin()->get<double>(0), EPSILON);
            unit_assert_equal(15.0, sqlite::query(*db, "SELECT Coverage FROM ProteinCoverage WHERE Id=2").begin()->get<double>(0), EPSILON);

            //                          0                                       10                  15                      (next line is 20 and 30, etc.)
            unit_assert_operator_equal("64000000" // size is 100
                                       "01000100010001000100020002000200020002000100010001000100010000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000",
                                       sqlite::query(*db, "SELECT HEX(CoverageMask) FROM ProteinCoverage WHERE Id=1").begin()->get<string>(0));

            unit_assert_operator_equal("64000000" // size is 100
                                       "00000000000000000000010001000100010001000200020002000200020001000100010001000100"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000"
                                       "00000000000000000000000000000000000000000000000000000000000000000000000000000000",
                                       sqlite::query(*db, "SELECT HEX(CoverageMask) FROM ProteinCoverage WHERE Id=2").begin()->get<string>(0));
        }
    }
    catch (runtime_error&)
    {
        if (db)
        {
            cerr << "Saving failed test case to assertion_failed.idpDB" << endl;
            db->save_to_file("assert_failed.idpDB");
        }
        throw;
    }
}


void testAdditionalPeptides()
{
    Filter::Config config;
    config.minDistinctPeptides = 1;
    config.minSpectra = 1;
    config.minAdditionalPeptides = 1;
    config.geneLevelFiltering = false;

    config.minSpectraPerDistinctMatch = 1;
    config.minSpectraPerDistinctPeptide = 1;
    config.maxProteinGroupsPerPeptide = 10;

    config.distinctMatchFormat.isAnalysisDistinct = false;
    config.distinctMatchFormat.isChargeDistinct = true;
    config.distinctMatchFormat.areModificationsDistinct = true;
    config.distinctMatchFormat.modificationMassRoundToNearest = 1.0;

    TestDatabase db;

    try
    {
        // 1 protein to 1 peptide to 1 spectrum = 1 additional peptide
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 }
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
        }

        // 1 protein to 1 peptide to 2 spectra = 1 additional peptide
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 1, 2 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 1;
            filter.config.minDistinctPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minDistinctPeptides = 1;
            filter.config.minSpectra = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minSpectra = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
        }

        // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minDistinctPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minDistinctPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minDistinctPeptides = 2;
            filter.config.minSpectra = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minSpectra = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
        }

        // 1 protein to 2 peptides to 2 spectra (each) = 2 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 1, 2 },
                { 1, 2, 3 },
                { 1, 2, 4 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
        }

        // 2 proteins to 1 peptide to 1 spectrum = 1 additional peptide (ambiguous protein group)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 2, 1, 1 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));
        }

        // 2 proteins to 1 peptide to 2 spectra = 1 additional peptide (ambiguous protein group)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 2, 1, 2 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
        }

        // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 2, 1, 1 },
                { 2, 2, 2 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
        }

        // 2 proteins to 2 peptides to 2 spectra (each) = 2 additional peptides (ambiguous protein group)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 1, 2 },
                { 1, 2, 3 },
                { 1, 2, 4 },
                { 2, 1, 1 },
                { 2, 1, 2 },
                { 2, 2, 3 },
                { 2, 2, 4 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));
        }

        // 1 protein to 2 peptides = 2 additional peptide
        // 1 protein to 1 of the above peptides = 0 additional peptides (subsumed protein)
        // 1 protein to the other above peptide = 0 additional peptides (subsumed protein)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 2, 1, 1 },
                { 3, 2, 2 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minDistinctPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));
        }

        // 1 protein to 2 peptides = 2 additional peptide
        // 2 proteins to 1 of the above peptides = 0 additional peptides (subsumed ambiguous protein group)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 2, 1, 1 },
                { 3, 1, 1 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));
        }

        // 2 proteins to 2 peptides = 2 additional peptide (ambiguous protein group)
        // 1 protein to 1 of the above peptides = 0 additional peptides (subsumed protein)
        // 1 protein to the other above peptide = 0 additional peptides (subsumed protein)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 2, 1, 1 },
                { 2, 2, 2 },
                { 3, 1, 1 },
                { 4, 2, 2 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));
        }

        // 2 proteins to 2 peptides = 2 additional peptides (ambiguous protein group)
        // 2 proteins to 1 of the above peptides = 0 additional peptides (subsumed ambiguous protein group)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 2, 1, 1 },
                { 2, 2, 2 },
                { 3, 1, 1 },
                { 4, 1, 1 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.config.minAdditionalPeptides = 0;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));
        }

        // 1 protein to 3 peptides = 3 additional peptides
        // 1 protein to 1 of the above peptides and 1 extra peptide = 1 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 3, 3 },
                { 2, 4, 4 },
            };

            db = testCase(testPSMs);
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(4, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 2;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 3;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));

            filter.config.minAdditionalPeptides = 4;
            filter.filter(db->connected());
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM PeptideSpectrumMatch").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT COUNT(*) FROM Protein").begin()->get<int>(0));
        }

        // 1 protein to 3 peptides = 3 additional peptides
        // 2 proteins to 1 of the above peptides and 1 extra peptide = 1 additional peptides (ambiguous protein group)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 3, 3 },
                { 2, 4, 4 },
                { 3, 3, 3 },
                { 3, 4, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // 2 proteins to 3 peptides = 3 additional peptides (ambiguous protein group)
        // 1 protein to 1 of the above peptides and 1 extra peptide = 1 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 1, 1 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 3, 3, 3 },
                { 3, 4, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // 2 proteins to 3 peptides = 3 additional peptides (ambiguous protein group)
        // 2 proteins to 1 of the above peptides and 1 extra peptide = 1 additional peptides (ambiguous protein group)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 1, 1 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 3, 3, 3 },
                { 3, 4, 4 },
                { 4, 3, 3 },
                { 4, 4, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=4").begin()->get<int>(0));
        }

        // 1 protein to 5 peptides = 5 additional peptides
        // 1 protein to 4 of the above peptides = 0 additional peptides
        // 1 protein to 3 of the above peptides and 1 extra peptides = 1 additional peptides
        // 1 protein to 2 of the above peptides and 2 extra peptides = 2 additional peptides
        // 1 protein to 1 of the above peptides and 3 extra peptides = 3 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 1, 4, 4 },
                { 1, 5, 5 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 2, 4, 4 },
                { 2, 5, 5 },
                { 3, 3, 3 },
                { 3, 4, 4 },
                { 3, 5, 5 },
                { 3, 6, 6 },
                { 4, 3, 3 },
                { 4, 4, 4 },
                { 4, 7, 7 },
                { 4, 8, 8 },
                { 5, 3, 3 },
                { 5, 9, 9 },
                { 5, 10, 10 },
                { 5, 11, 11 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(5, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=4").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=5").begin()->get<int>(0));
        }

        // 1 protein to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
        // 1 protein to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 4, 3 },
                { 2, 5, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
        }

        // 2 proteins to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
        // 1 protein to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 1, 1 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 3, 4, 3 },
                { 3, 5, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // 1 protein to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
        // 2 proteins to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 4, 3 },
                { 2, 5, 4 },
                { 3, 4, 3 },
                { 3, 5, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // 1 protein to 3 peptides, 2 of which are evidenced by ambiguous spectra = 3 additional peptides
        // 1 protein to 1 peptide evidenced by an ambiguous spectrum above and 1 extra peptide = 1 additional peptides
        // 1 protein to 1 peptide evidenced by the other ambiguous spectrum above and 1 extra peptide = 1 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 4, 2 },
                { 2, 5, 4 },
                { 3, 6, 3 },
                { 3, 7, 5 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // 1 protein to 3 peptides = 3 additional peptides
        // 1 protein to 2 of the above peptides and 1 extra = 3 additional peptides (it's a tie)
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 2, 4, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
        }

        // Pro1 -> Pep1 Pep2 Pep3 = 3 additional peptides
        // Pro2 -> Pep4 Pep2 Pep3 = 3 additional peptides
        // Pro3 -> Pep1 Pep4 Pep3 = 3 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 4, 4 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 3, 1, 1 },
                { 3, 4, 4 },
                { 3, 3, 3 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // Pro1 -> Pep1 Pep2 Pep3 = 3 additional peptides
        // Pro2 -> Pep2 Pep3 Pep4 = 3 additional peptides
        // Pro3 -> Pep1 Pep4 = 0 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 4, 4 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 3, 1, 1 },
                { 3, 4, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // Pro1 -> Pep1 Pep2 = 2 additional peptides
        // Pro2 -> Pep2 Pep3 = 0 additional peptides
        // Pro3 -> Pep3 Pep4 = 2 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 3, 3, 3 },
                { 3, 4, 4 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(2, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // Pro1 -> Pep1 Pep2 Pep3 = 3 additional peptides
        // Pro2 -> Pep3 Pep4 Pep5 = 1 additional peptides
        // Pro3 -> Pep5 Pep6 Pep7 = 3 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 3, 3 },
                { 2, 4, 4 },
                { 2, 5, 5 },
                { 3, 5, 5 },
                { 3, 6, 6 },
                { 3, 7, 7 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // Pro1 -> Pep1 Pep2 Pep3 = 3 additional peptides
        // Pro2 -> Pep2 Pep3 Pep4 = 0 additional peptides
        // Pro3 -> Pep4 Pep5 Pep6 = 3 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 2, 2 },
                { 2, 3, 3 },
                { 2, 4, 4 },
                { 3, 4, 4 },
                { 3, 5, 5 },
                { 3, 6, 6 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // test with multiple clusters
        // Pro1 -> Pep1 Pep2 Pep3 = 3 additional peptides
        // Pro2 -> Pep3 Pep4 Pep5 = 1 additional peptides
        // Pro3 -> Pep5 Pep6 Pep7 = 3 additional peptides
        // Pro4 -> Pep8 Pep9 Pep10 = 3 additional peptides
        // Pro5 -> Pep9 Pep10 Pep11 = 0 additional peptides
        // Pro6 -> Pep12 Pep13 Pep14 = 3 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 1, 2, 2 },
                { 1, 3, 3 },
                { 2, 3, 3 },
                { 2, 4, 4 },
                { 2, 5, 5 },
                { 3, 5, 5 },
                { 3, 6, 6 },
                { 3, 7, 7 },
                { 10, 10, 10 },
                { 10, 20, 20 },
                { 10, 30, 30 },
                { 20, 20, 20 },
                { 20, 30, 30 },
                { 20, 40, 40 },
                { 30, 40, 40 },
                { 30, 50, 50 },
                { 30, 60, 60 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=10").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=20").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=30").begin()->get<int>(0));
        }


        // the rest of the test cases are testing gene level filtering
        config.geneLevelFiltering = true;

        // check for error when trying to gene level filter without gene metadata
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1 },
                { 2, 2, 2 },
                { 3, 3, 3 },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            unit_assert_throws_what(filter.filter(db->connected()), runtime_error, "error filtering connection: unable to perform gene level filtering without embedded gene metadata");
        }

        // Gene1 -> Pro1 -> Pep1 Pep2 Pep3 = 3 additional peptides
        // Gene2 -> Pro2 -> Pep3 Pep4 Pep5 = 1 additional peptides
        // Gene3 -> Pro3 -> Pep5 Pep6 Pep7 = 3 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 0, "", "Gene1" },
                { 1, 2, 2, 0, 0, "", "Gene1" },
                { 1, 3, 3, 0, 0, "", "Gene1" },
                { 2, 3, 3, 0, 0, "", "Gene2" },
                { 2, 4, 4, 0, 0, "", "Gene2" },
                { 2, 5, 5, 0, 0, "", "Gene2" },
                { 3, 5, 5, 0, 0, "", "Gene3" },
                { 3, 6, 6, 0, 0, "", "Gene3" },
                { 3, 7, 7, 0, 0, "", "Gene3" },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(1, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // Gene1 -> Pro1 -> Pep1 Pep2 Pep3 = 3 additional peptides
        // Gene2 -> Pro2 -> Pep2 Pep3 Pep4 = 0 additional peptides
        // Gene3 -> Pro3 -> Pep4 Pep5 Pep6 = 3 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 0, "", "Gene1" },
                { 1, 2, 2, 0, 0, "", "Gene1" },
                { 1, 3, 3, 0, 0, "", "Gene1" },
                { 2, 2, 2, 0, 0, "", "Gene2" },
                { 2, 3, 3, 0, 0, "", "Gene2" },
                { 2, 4, 4, 0, 0, "", "Gene2" },
                { 3, 4, 4, 0, 0, "", "Gene3" },
                { 3, 5, 5, 0, 0, "", "Gene3" },
                { 3, 6, 6, 0, 0, "", "Gene3" },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(3, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // CONSIDER: is this a good result?
        // Gene1 -> Pro1 -> Pep1 Pep2 Pep3 = 6 additional peptides
        // Gene2 -> Pro2 -> Pep2 Pep3 Pep4 = 0 additional peptides
        // Gene1 -> Pro3 -> Pep4 Pep5 Pep6 = 6 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 0, "", "Gene1" },
                { 1, 2, 2, 0, 0, "", "Gene1" },
                { 1, 3, 3, 0, 0, "", "Gene1" },
                { 2, 2, 2, 0, 0, "", "Gene2" },
                { 2, 3, 3, 0, 0, "", "Gene2" },
                { 2, 4, 4, 0, 0, "", "Gene2" },
                { 3, 4, 4, 0, 0, "", "Gene1" },
                { 3, 5, 5, 0, 0, "", "Gene1" },
                { 3, 6, 6, 0, 0, "", "Gene1" },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(6, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(0, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(6, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }

        // CONSIDER: is this a good result?
        // Gene1 -> Pro1 -> Pep1 Pep2 Pep3 = 6 additional peptides
        // Gene1 -> Pro2 -> Pep2 Pep3 Pep4 = 6 additional peptides
        // Gene1 -> Pro3 -> Pep4 Pep5 Pep6 = 6 additional peptides
        {
            const TestPSM testPSMs[] =
            {
                { 1, 1, 1, 0, 0, "", "Gene1" },
                { 1, 2, 2, 0, 0, "", "Gene1" },
                { 1, 3, 3, 0, 0, "", "Gene1" },
                { 2, 2, 2, 0, 0, "", "Gene1" },
                { 2, 3, 3, 0, 0, "", "Gene1" },
                { 2, 4, 4, 0, 0, "", "Gene1" },
                { 3, 4, 4, 0, 0, "", "Gene1" },
                { 3, 5, 5, 0, 0, "", "Gene1" },
                { 3, 6, 6, 0, 0, "", "Gene1" },
            };

            db = testCase(testPSMs);

            Filter filter;
            filter.config = config;
            filter.filter(db->connected());
            unit_assert_operator_equal(6, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=1").begin()->get<int>(0));
            unit_assert_operator_equal(6, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=2").begin()->get<int>(0));
            unit_assert_operator_equal(6, sqlite::query(*db, "SELECT AdditionalMatches FROM AdditionalMatches WHERE ProteinId=3").begin()->get<int>(0));
        }
    }
    catch (runtime_error&)
    {
        if (db)
        {
            cerr << "Saving failed test case to assertion_failed.idpDB" << endl;
            db->save_to_file("assert_failed.idpDB");
        }
        throw;
    }
}

void testSQLiteUserFunctions()
{
    TestDatabase db;

    const TestPSM testPSMs[] =
    {
        { 1, 1, 1 }
    };

    db = testCase(testPSMs);
    sqlite::query testGetSmallerMassError(*db, "SELECT GET_SMALLER_MASS_ERROR(?, ?)");

    testGetSmallerMassError.binder() << 0.01 << 0.0123;
    unit_assert_operator_equal(0.01, testGetSmallerMassError.begin()->get<double>(0));
    testGetSmallerMassError.reset();

    testGetSmallerMassError.binder() << 1.02 << 0.0234;
    unit_assert_operator_equal(1.02, testGetSmallerMassError.begin()->get<double>(0));
    testGetSmallerMassError.reset();

    testGetSmallerMassError.binder() << 0.03 << 0.00456;
    unit_assert_operator_equal(0.00456, testGetSmallerMassError.begin()->get<double>(0));
    testGetSmallerMassError.reset();

    testGetSmallerMassError.binder() << -1.04 << -0.4567;
    unit_assert_operator_equal(-1.04, testGetSmallerMassError.begin()->get<double>(0));
    testGetSmallerMassError.reset();


    sqlite::query testGetSmallerMassErrorAdjusted(*db, "SELECT GET_SMALLER_MASS_ERROR_ADJUSTED(?, ?)");

    testGetSmallerMassErrorAdjusted.binder() << 1.02 << 0.0234;
    unit_assert_operator_equal(1.02 - pwiz::chemistry::Neutron, testGetSmallerMassErrorAdjusted.begin()->get<double>(0));
    testGetSmallerMassErrorAdjusted.reset();

    testGetSmallerMassErrorAdjusted.binder() << 0.03 << 0.00456;
    unit_assert_operator_equal(0.00456, testGetSmallerMassErrorAdjusted.begin()->get<double>(0));
    testGetSmallerMassErrorAdjusted.reset();

    testGetSmallerMassErrorAdjusted.binder() << -2.04 << -0.4567;
    unit_assert_operator_equal(-(2.04 - 2*pwiz::chemistry::Neutron), testGetSmallerMassErrorAdjusted.begin()->get<double>(0));
    testGetSmallerMassErrorAdjusted.reset();


    sqlite::query testWithinMassToleranceMZ(*db, "SELECT WITHIN_MASS_TOLERANCE_MZ(?, ?, ?)");

    testWithinMassToleranceMZ.binder() << 123.4 << 123.5 << 0.05;
    unit_assert_operator_equal(0, testWithinMassToleranceMZ.begin()->get<int>(0));
    testWithinMassToleranceMZ.reset();

    testWithinMassToleranceMZ.binder() << 123.4 << 123.5 << 0.5;
    unit_assert_operator_equal(1, testWithinMassToleranceMZ.begin()->get<int>(0));
    testWithinMassToleranceMZ.reset();


    sqlite::query testWithinMassTolerancePPM(*db, "SELECT WITHIN_MASS_TOLERANCE_PPM(?, ?, ?)");

    testWithinMassTolerancePPM.binder() << 123.4 << 123.5 << 10;
    unit_assert_operator_equal(0, testWithinMassTolerancePPM.begin()->get<int>(0));
    testWithinMassTolerancePPM.reset();

    testWithinMassTolerancePPM.binder() << 12345.6 << 12345.7 << 10;
    unit_assert_operator_equal(1, testWithinMassTolerancePPM.begin()->get<int>(0));
    testWithinMassTolerancePPM.reset();

}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    if (find(testArgs.begin(), testArgs.end(), "-v") == testArgs.end())
        boost::log::core::get()->set_logging_enabled(false);

    try
    {
        testCoverage();
        testAdditionalPeptides();
        testSQLiteUserFunctions();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
