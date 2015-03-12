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
#include "sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "boost/crc.hpp"


using namespace pwiz::util;
namespace sqlite = sqlite3pp;


BEGIN_IDPICKER_NAMESPACE

const int CURRENT_SCHEMA_REVISION = 13;

namespace SchemaUpdater {


namespace {

struct DistinctDoubleArraySum
{
    typedef DistinctDoubleArraySum MyType;
    set<int> arrayIds;
    vector<double> result;
    boost::crc_32_type crc32;

    DistinctDoubleArraySum(int arrayLength) : result((size_t)arrayLength, 0.0) {}

    static void Step(sqlite3_context* context, int numValues, sqlite3_value** values)
    {
        void* aggContext = sqlite3_aggregate_context(context, sizeof(MyType*));
        if (aggContext == NULL)
            throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

        MyType** ppThis = static_cast<MyType**>(aggContext);
        MyType*& pThis = *ppThis;

        if (numValues > 1 || values[0] == NULL)
            return;

        int arrayByteCount = sqlite3_value_bytes(values[0]);
        int arrayLength = arrayByteCount / 8;
        const char* arrayBytes = static_cast<const char*>(sqlite3_value_blob(values[0]));
        if (arrayBytes == NULL)
            return;

        if (arrayByteCount % 8 > 0)
            throw runtime_error("distinct_double_array_sum only works with BLOBs of double precision floats");

        if (pThis == NULL)
            pThis = new DistinctDoubleArraySum(arrayLength);
        else
            pThis->crc32.reset();

        // if the arrayId was already in the set, ignore its values
        pThis->crc32.process_bytes(arrayBytes, arrayByteCount);
        int arrayId = pThis->crc32.checksum();
        if (!pThis->arrayIds.insert(arrayId).second)
            return;

        const double* arrayValues = reinterpret_cast<const double*>(arrayBytes);

        for (int i = 0; i < arrayLength; ++i)
            pThis->result[i] += arrayValues[i];
    }

    static void Final(sqlite3_context* context)
    {
        void* aggContext = sqlite3_aggregate_context(context, 0);
        if (aggContext == NULL)
            throw runtime_error(sqlite3_errmsg(sqlite3_context_db_handle(context)));

        MyType** ppThis = static_cast<MyType**>(aggContext);
        MyType*& pThis = *ppThis;

        if (pThis == NULL)
            pThis = new DistinctDoubleArraySum(0);

        sqlite3_result_blob(context, pThis->result.empty() ? NULL : &pThis->result[0], pThis->result.size() * sizeof(double), SQLITE_TRANSIENT);

        delete pThis;
    }
};

void update_12_to_13(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 12, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("CREATE TABLE IF NOT EXISTS PeptideModificationProbability(PeptideModification INTEGER PRIMARY KEY, Probability NUMERIC)");

    //update_13_to_14(db, ilr, vacuumNeeded);
}

void update_11_to_12(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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

void update_10_to_11(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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


void update_9_to_10(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 9, CURRENT_SCHEMA_REVISION, "updating schema version")

    // add XICMetrics
    db.execute("CREATE TABLE IF NOT EXISTS XICMetrics (PsmId INTEGER PRIMARY KEY, PeakIntensity NUMERIC, PeakArea NUMERIC, PeakSNR NUMERIC, PeakTimeInSeconds NUMERIC);");
    db.execute("CREATE TABLE IF NOT EXISTS XICMetricsSettings (SourceId INTEGER PRIMARY KEY, TotalSpectra INT, Settings STRING);");


    update_10_to_11(db, ilr, vacuumNeeded);
}


void update_8_to_9(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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

void update_7_to_8(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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

void update_6_to_7(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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
        BOOST_FOREACH(sqlite::query::rows row, q)
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
        BOOST_FOREACH(sqlite::query::rows row, q)
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

void update_5_to_6(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 5, CURRENT_SCHEMA_REVISION, "updating schema version")

    // force the basic filters to be reapplied
    db.execute("DROP TABLE IF EXISTS FilteringCriteria");

    update_6_to_7(db, ilr, vacuumNeeded);
}

void update_4_to_5(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
{
    ITERATION_UPDATE(ilr, 4, CURRENT_SCHEMA_REVISION, "updating schema version")

    db.execute("UPDATE SpectrumSource SET QuantitationMethod = IFNULL(QuantitationMethod, 0),"
               "                          TotalSpectraMS1 = IFNULL(TotalSpectraMS1, 0),"
               "                          TotalSpectraMS2 = IFNULL(TotalSpectraMS2, 0),"
               "                          TotalIonCurrentMS1 = IFNULL(TotalIonCurrentMS1, 0),"
               "                          TotalIonCurrentMS2 = IFNULL(TotalIonCurrentMS2, 0)");

    update_5_to_6(db, ilr, vacuumNeeded);
}

void update_3_to_4(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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

void update_2_to_3(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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

void update_1_to_2(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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

void update_0_to_1(sqlite::database& db, IterationListenerRegistry* ilr, bool& vacuumNeeded)
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


bool update(const string& idpDbFilepath, IterationListenerRegistry* ilr)
{
    sqlite::database db(idpDbFilepath);

    db.execute("PRAGMA journal_mode=OFF;"
        "PRAGMA synchronous=OFF;"
        "PRAGMA cache_size=50000;"
        IDPICKER_SQLITE_PRAGMA_MMAP);

    return update(db.connected());
}

bool update(sqlite3* idpDbConnection, IterationListenerRegistry* ilr)
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
    else if (schemaRevision > CURRENT_SCHEMA_REVISION)
        throw runtime_error("[SchemaUpdater::update] unable to update schema revision " +
                            lexical_cast<string>(schemaRevision) +
                            "; the latest compatible revision is " +
                            lexical_cast<string>(CURRENT_SCHEMA_REVISION));
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
    int result = sqlite3_create_function(idpDbConnection, "distinct_double_array_sum", -1, SQLITE_ANY,
                                         0, NULL, &DistinctDoubleArraySum::Step, &DistinctDoubleArraySum::Final);
    if (result != 0)
        throw runtime_error("unable to create user function: SQLite error " + lexical_cast<string>(result));
}


} // namespace SchemaUpdater
END_IDPICKER_NAMESPACE
