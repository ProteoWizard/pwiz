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
// Copyright 2014 Vanderbilt University
//
// Contributor(s):


#include "sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "Logger.hpp"
#include "SchemaUpdater.hpp"
#include "Filter.hpp"
#include "Qonverter.hpp"
#include "Embedder.hpp"
#include "TotalCounts.hpp"
#include "boost/foreach_field.hpp"
#include "boost/assert.hpp"
#include "boost/atomic.hpp"
#include "boost/thread.hpp"
#include "boost/make_shared.hpp"
#include "boost/functional/hash.hpp"
#include <stack>
#include <algorithm>


using namespace pwiz::util;


#ifdef WIN32
extern "C" __declspec(dllimport) int __stdcall GetDriveTypeA(const char *);
static bool isPathOnFixedDrive(const std::string& path)
{
    bfs::path completePath = bfs::system_complete(path);
    return GetDriveTypeA(completePath.root_path().string().c_str()) == 3; // DRIVE_FIXED
}
#else
static bool isPathOnFixedDrive(const std::string& path)
{
    return true;
}
#endif // WIN32


namespace {


/// MaxFDRScore, PrecursorMzTolerance
const string filteredSpectrumSelectSql =
    "SELECT s.*\n"
    "FROM PeptideSpectrumMatch psm\n"
    "JOIN Spectrum s ON psm.Spectrum = s.Id\n"
    "JOIN SpectrumSource ss ON s.Source = ss.Id\n"
    "-- filter out ungrouped spectrum sources\n"
    "WHERE ss.Group_ AND %1% >= psm.QValue AND psm.Rank = 1 %2%\n"
    "GROUP BY s.Id;\n";
boost::format createFilteredSpectrumTableSql(
    "CREATE TABLE FilteredSpectrum (Id INTEGER PRIMARY KEY, Source INTEGER, Index_ INTEGER, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);\n"
    "INSERT INTO FilteredSpectrum " + filteredSpectrumSelectSql +
    "CREATE UNIQUE INDEX FilteredSpectrum_SourceNativeID ON FilteredSpectrum (Source, NativeID);"
);
boost::format createDebugFilteredSpectrumTableSql(
    "DROP TABLE IF EXISTS DebugFilteredSpectrum; CREATE TABLE DebugFilteredSpectrum (Id INTEGER PRIMARY KEY);\n"
    "INSERT INTO DebugFilteredSpectrum SELECT s.Id\n"
    "FROM PeptideSpectrumMatch psm\n"
    "JOIN Spectrum s ON psm.Spectrum = s.Id\n"
    "JOIN SpectrumSource ss ON s.Source = ss.Id\n"
    "-- filter out ungrouped spectrum sources\n"
    "WHERE NOT ss.Group_ OR %1% < psm.QValue OR psm.Rank > 1 OR NOT (1 %2%)\n"
    "GROUP BY s.Id;\n"
);

/// MinDistinctPeptides, MinSpectra (used to compose a WHERE/GROUP BY/HAVING predicate which depends on whether grouping happens at the protein or gene level)
const string filteredProteinSelectSql =
    "SELECT pro.*\n"
    "FROM FilteredPeptideSpectrumMatch psm\n"
    "JOIN FilteredSpectrum s ON psm.Spectrum = s.Id\n"
    "JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide\n"
    "JOIN Protein pro ON pi.Protein = pro.Id\n"
    "GROUP BY pi.Protein\n"
    "HAVING %1% <= COUNT(DISTINCT psm.Peptide) AND\n"
    "       %2% <= COUNT(DISTINCT psm.Spectrum);\n";
boost::format createFilteredProteinTableSql(
    "CREATE TABLE FilteredProtein (Id INTEGER PRIMARY KEY, Accession TEXT, IsDecoy INT, Cluster INT, ProteinGroup INT, Length INT, GeneId TEXT, GeneGroup INT);\n"
    "INSERT INTO FilteredProtein " + filteredProteinSelectSql +
    "CREATE UNIQUE INDEX FiltProtein_Accession ON FilteredProtein (Accession);"
);
boost::format createDebugFilteredProteinTableSql(
    "DROP TABLE IF EXISTS DebugFilteredProtein; CREATE TABLE DebugFilteredProtein (Id INTEGER PRIMARY KEY);\n"
    "INSERT INTO DebugFilteredProtein SELECT pro.Id\n"
    "FROM FilteredPeptideSpectrumMatch psm\n"
    "JOIN FilteredSpectrum s ON psm.Spectrum = s.Id\n"
    "JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide\n"
    "JOIN Protein pro ON pi.Protein = pro.Id\n"
    "GROUP BY pi.Protein\n"
    "HAVING %1% > COUNT(DISTINCT psm.Peptide) AND\n"
    "       %2% > COUNT(DISTINCT psm.Spectrum);\n"
);

const string filteredProteinTableByGeneSelectSql =
    "SELECT pro.*\n"
    "FROM FilteredPeptideSpectrumMatch psm\n"
    "JOIN FilteredSpectrum s ON psm.Spectrum = s.Id\n"
    "JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide\n"
    "JOIN Protein pro ON pi.Protein = pro.Id\n"
    "WHERE GeneId IN (SELECT GeneId FROM FilteredPeptideSpectrumMatch psm, Protein pro, PeptideInstance pi\n"
    "                 WHERE psm.Peptide=pi.Peptide AND pro.Id=pi.Protein\n"
    "                 GROUP BY GeneId\n"
    "                 HAVING %1% <= COUNT(DISTINCT psm.Peptide) AND\n"
    "                        %2% <= COUNT(DISTINCT psm.Spectrum))\n"
    "GROUP BY pi.Protein;\n";
boost::format createFilteredProteinTableByGeneSql(
    "CREATE TABLE FilteredProtein (Id INTEGER PRIMARY KEY, Accession TEXT, IsDecoy INT, Cluster INT, ProteinGroup INT, Length INT, GeneId TEXT, GeneGroup INT);\n"
    "INSERT INTO FilteredProtein " + filteredProteinTableByGeneSelectSql +
    "CREATE UNIQUE INDEX FiltProtein_Accession ON FilteredProtein (Accession);"
);

/// MaxFDRScore, PrecursorMzTolerance
const string filteredPSMSelectSql =
    "SELECT psm.*\n"
    "FROM Protein pro\n"
    "JOIN PeptideInstance pi ON pro.Id = pi.Protein\n"
    "JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide\n"
    "JOIN FilteredSpectrum s ON psm.Spectrum = s.Id\n"
    "WHERE %1% >= psm.QValue AND psm.Rank = 1 %2%\n"
    "GROUP BY psm.Id;\n";
boost::format createFilteredPSMTableSql(
    "CREATE TABLE FilteredPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);\n"
    "INSERT INTO FilteredPeptideSpectrumMatch " + filteredPSMSelectSql +
    "CREATE INDEX FilteredPeptideSpectrumMatch_PeptideSpectrumAnalysis ON FilteredPeptideSpectrumMatch (Peptide, Spectrum, Analysis);\n"
    "CREATE INDEX FilteredPeptideSpectrumMatch_SpectrumPeptideAnalysis ON FilteredPeptideSpectrumMatch (Spectrum, Peptide, Analysis);"
);
boost::format createDebugFilteredPSMTableSql(
    "DROP TABLE IF EXISTS DebugFilteredPeptideSpectrumMatch; CREATE TABLE DebugFilteredPeptideSpectrumMatch (Id INTEGER PRIMARY KEY);\n"
    "INSERT INTO DebugFilteredPeptideSpectrumMatch SELECT psm.Id\n"
    "FROM Protein pro\n"
    "JOIN PeptideInstance pi ON pro.Id = pi.Protein\n"
    "JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide\n"
    "JOIN FilteredSpectrum s ON psm.Spectrum = s.Id\n"
    "WHERE %1% < psm.QValue OR psm.Rank > 1\n"
    "GROUP BY psm.Id;\n"
);

/// DistinctMatchFormatSqlExpression (with FilteredPSM instead of psm), DistinctMatchFormatSqlExpression, MinSpectraPerDistinctMatch
boost::format deleteFilteredPSMsUnderMatchCountSql(
    "DELETE FROM FilteredPeptideSpectrumMatch\n"
    "WHERE %1% IN\n"
    "      (SELECT %2%\n"
    "       FROM FilteredPeptideSpectrumMatch psm\n"
    "       GROUP BY %2%\n"
    "       HAVING %3% > COUNT(DISTINCT psm.Spectrum))"
);

/// (MinimumSpectraPerDistinctPeptide > 1 ? "HAVING " + MinimumSpectraPerDistinctPeptide + @" <= COUNT(DISTINCT psm.Spectrum)" : "");
const string filterdPeptideSelectSql =
    "SELECT pep.*\n"
    "FROM FilteredPeptideSpectrumMatch psm\n"
    "JOIN Peptide pep ON psm.Peptide = pep.Id\n"
    "GROUP BY pep.Id %1%";
boost::format createFilteredPeptideTableSql(
    "CREATE TABLE FilteredPeptide (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, PeptideGroup INT, DecoySequence TEXT);\n"
    "INSERT INTO FilteredPeptide " + filterdPeptideSelectSql
);

const string filteredPeptideInstanceSelectSql =
    "SELECT pi.*\n"
    "FROM FilteredPeptide pep\n"
    "JOIN PeptideInstance pi ON pep.Id = pi.Peptide\n"
    "JOIN FilteredProtein pro ON pi.Protein = pro.Id;\n";
const string createFilteredPeptideInstanceTableSql(
    "CREATE TABLE FilteredPeptideInstance (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);\n"
    "INSERT INTO FilteredPeptideInstance " + filteredPeptideInstanceSelectSql +
    "CREATE INDEX FilteredPeptideInstance_PeptideProtein ON FilteredPeptideInstance (Peptide, Protein);\n"
    "CREATE INDEX FilteredPeptideInstance_ProteinOffsetLength ON FilteredPeptideInstance (Protein, Offset, Length);"
);

const string renameFilteredTablesSql(
    "ALTER TABLE Protein RENAME TO UnfilteredProtein;\n"
    "ALTER TABLE PeptideInstance RENAME TO UnfilteredPeptideInstance;\n"
    "ALTER TABLE Peptide RENAME TO UnfilteredPeptide;\n"
    "ALTER TABLE PeptideSpectrumMatch RENAME TO UnfilteredPeptideSpectrumMatch;\n"
    "ALTER TABLE Spectrum RENAME TO UnfilteredSpectrum;\n"
    "\n"
    "ALTER TABLE FilteredProtein RENAME TO Protein;\n"
    "ALTER TABLE FilteredPeptideInstance RENAME TO PeptideInstance;\n"
    "ALTER TABLE FilteredPeptide RENAME TO Peptide;\n"
    "ALTER TABLE FilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch;\n"
    "ALTER TABLE FilteredSpectrum RENAME TO Spectrum"
);

const string assembleProteinGroupsSql(
    "CREATE TEMP TABLE ProteinGroups AS\n"
    "    SELECT pro.Id AS ProteinId, GROUP_CONCAT(DISTINCT pi.Peptide) AS ProteinGroup\n"
    "    FROM PeptideInstance pi\n"
    "    JOIN Protein pro ON pi.Protein = pro.Id\n"
    "    GROUP BY pi.Protein;\n"
    "\n"
    "--ProteinGroup will be a continuous sequence starting at 1\n"
    "CREATE TEMP TABLE TempProtein AS\n"
    "    SELECT ProteinId, Accession, IsDecoy, Cluster, pg2.rowid, Length, GeneId, GeneGroup\n"
    "    FROM ProteinGroups pg\n"
    "    JOIN(\n"
    "       SELECT pg.ProteinGroup\n"
    "       FROM ProteinGroups pg\n"
    "       GROUP BY pg.ProteinGroup\n"
    "    ) pg2 ON pg.ProteinGroup = pg2.ProteinGroup\n"
    "    JOIN Protein pro ON pg.ProteinId = pro.Id;\n"
    "\n"
    "DELETE FROM Protein;\n"
    "INSERT INTO Protein SELECT * FROM TempProtein;\n"
    "CREATE INDEX Protein_ProteinGroup ON Protein(ProteinGroup);\n"
    "DROP TABLE ProteinGroups;\n"
    "DROP TABLE TempProtein;"
);

const string assembleGeneGroupsSql(
    "CREATE TEMP TABLE GeneGroups AS\n"
    "    SELECT pro.GeneId AS GeneId, GROUP_CONCAT(DISTINCT pi.Peptide) AS GeneGroup\n"
    "    FROM PeptideInstance pi\n"
    "    JOIN Protein pro ON pi.Protein = pro.Id\n"
    "    GROUP BY pro.GeneId;\n"
    "\n"
    "--GeneGroup will be a continuous sequence starting at 1\n"
    "CREATE TEMP TABLE TempProtein AS\n"
    "    SELECT pro.Id, Accession, IsDecoy, Cluster, ProteinGroup, Length, pro.GeneId, gg2.rowid AS GeneGroup\n"
    "    FROM GeneGroups gg\n"
    "    JOIN(\n"
    "       SELECT gg.GeneGroup\n"
    "       FROM GeneGroups gg\n"
    "       GROUP BY gg.GeneGroup\n"
    "       ORDER BY gg.GeneId\n"
    "    ) gg2 ON gg.GeneGroup = gg2.GeneGroup\n"
    "    JOIN Protein pro ON gg.GeneId = pro.GeneId\n"
    "    GROUP BY pro.Id;\n"
    "\n"
    "DELETE FROM Protein;\n"
    "INSERT INTO Protein SELECT * FROM TempProtein;\n"
    "CREATE INDEX Protein_GeneGroup ON Protein(GeneGroup);\n"
    "DROP TABLE GeneGroups;\n"
    "DROP TABLE TempProtein;"
);

const string assemblePeptideGroupsSql(
    "CREATE TEMP TABLE PeptideGroups AS\n"
    "         SELECT pep.Id AS PeptideId, GROUP_CONCAT(DISTINCT pi.Protein) AS PeptideGroup\n"
    "         FROM PeptideInstance pi\n"
    "         JOIN Peptide pep ON pi.Peptide=pep.Id\n"
    "         GROUP BY pi.Peptide;\n"
    "\n"
    "-- PeptideGroup will be a continuous sequence starting at 1\n"
    "CREATE TEMP TABLE TempPeptide AS\n"
    "         SELECT PeptideId, MonoisotopicMass, MolecularWeight, pg2.rowid, DecoySequence\n"
    "         FROM PeptideGroups pg\n"
    "         JOIN (\n"
    "               SELECT pg.PeptideGroup\n"
    "               FROM PeptideGroups pg\n"
    "               GROUP BY pg.PeptideGroup\n"
    "              ) pg2 ON pg.PeptideGroup = pg2.PeptideGroup\n"
    "         JOIN Peptide pro ON pg.PeptideId = pro.Id;\n"
    "\n"
    "DELETE FROM Peptide;\n"
    "INSERT INTO Peptide SELECT * FROM TempPeptide;\n"
    "CREATE INDEX Peptide_PeptideGroup ON Peptide (PeptideGroup);\n"
    "DROP TABLE PeptideGroups;\n"
    "DROP TABLE TempPeptide;"
);

// DistinctMatchFormat
boost::format assembleDistinctMatchesSql(
    "DROP TABLE IF EXISTS DistinctMatch;\n"
    "CREATE TABLE DistinctMatch (PsmId INTEGER PRIMARY KEY, DistinctMatchId INT, DistinctMatchKey TEXT);\n"
    "INSERT INTO DistinctMatch (PsmId, DistinctMatchKey)\n"
    "    SELECT DISTINCT psm.Id, %1% FROM PeptideSpectrumMatch psm;\n"
    "CREATE TEMP TABLE GroupedDistinctMatch AS\n"
    "    SELECT MIN(PsmId) AS RepresentativePsmId, DistinctMatchKey AS UniqueDistinctMatchKey FROM DistinctMatch GROUP BY DistinctMatchKey;\n"
    "CREATE UNIQUE INDEX GroupedDistinctMatch_DistinctMatchKey ON GroupedDistinctMatch (UniqueDistinctMatchKey);\n"
    "UPDATE DistinctMatch SET DistinctMatchId = (SELECT RepresentativePsmId FROM GroupedDistinctMatch WHERE UniqueDistinctMatchKey=DistinctMatchKey);\n"
    "DROP TABLE GroupedDistinctMatch;\n"
    "CREATE INDEX DistinctMatch_DistinctMatchId ON DistinctMatch (DistinctMatchId);"
);

const string aggregateQuantitationStatisticsSql(
    "DELETE FROM PeptideQuantitation;\n"
    "INSERT INTO PeptideQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)\n"
    "    SELECT psm.Peptide, DISTINCT_DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DISTINCT_DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)\n"
    "    FROM PeptideSpectrumMatch psm\n"
    "    JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id\n"
    "    GROUP BY psm.Peptide;\n"
    "\n"
    "DELETE FROM DistinctMatchQuantitation;\n"
    "INSERT INTO DistinctMatchQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)\n"
    "    SELECT dm.DistinctMatchKey, DISTINCT_DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DISTINCT_DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)\n"
    "    FROM PeptideSpectrumMatch psm\n"
    "    JOIN DistinctMatch dm ON psm.Id=dm.PsmId\n"
    "    JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id\n"
    "    GROUP BY dm.DistinctMatchKey;\n"
    "\n"
    "DELETE FROM ProteinQuantitation;\n"
    "INSERT INTO ProteinQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)\n"
    "    SELECT pi.Protein, DISTINCT_DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DISTINCT_DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)\n"
    "    FROM PeptideSpectrumMatch psm\n"
    "    JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide\n"
    "    JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id\n"
    "    GROUP BY pi.Protein;"
);

// MaxProteinGroupsPerPeptide
boost::format applyMaxProteinGroupsSql(
    "DELETE FROM Peptide WHERE Id IN\n"
    "(\n"
    "    SELECT pi.Peptide\n"
    "    FROM Protein pro\n"
    "    JOIN PeptideInstance pi on pro.Id = pi.Protein\n"
    "    GROUP BY pi.Peptide\n"
    "    HAVING COUNT(DISTINCT ProteinGroup) > %1%\n"
    ");\n"
    "DELETE FROM PeptideInstance WHERE Peptide NOT IN (SELECT Id FROM Peptide);\n"
    "DELETE FROM Protein WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);\n"
    "DELETE FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);\n"
    "DELETE FROM Spectrum WHERE Id NOT IN (SELECT Spectrum FROM PeptideSpectrumMatch);"
);
boost::format applyDebugMaxProteinGroupsSql(
    "DROP TABLE IF EXISTS DebugMaxProteinGroupsFilterProteinGroups; CREATE TABLE DebugMaxProteinGroupsFilterProteinGroups AS SELECT pi.Peptide, COUNT(DISTINCT ProteinGroup) AS ProteinGroupCount, GROUP_CONCAT(DISTINCT ProteinGroup) AS ProteinGroups\n"
    "    FROM Protein pro\n"
    "    JOIN PeptideInstance pi on pro.Id = pi.Protein\n"
    "    GROUP BY pi.Peptide;\n"
    "DROP TABLE IF EXISTS DebugMaxProteinGroupsFilterPeptide; CREATE TABLE DebugMaxProteinGroupsFilterPeptide AS SELECT Id FROM Peptide WHERE Id IN\n"
    "(\n"
    "    SELECT pi.Peptide\n"
    "    FROM Protein pro\n"
    "    JOIN PeptideInstance pi on pro.Id = pi.Protein\n"
    "    GROUP BY pi.Peptide\n"
    "    HAVING COUNT(DISTINCT ProteinGroup) > %1%\n"
    ");\n"
    "DROP TABLE IF EXISTS DebugMaxProteinGroupsFilterPeptideInstance; CREATE TABLE DebugMaxProteinGroupsFilterPeptideInstance AS SELECT Id FROM PeptideInstance WHERE Peptide NOT IN (SELECT Id FROM Peptide);\n"
    "DROP TABLE IF EXISTS DebugMaxProteinGroupsFilterProtein; CREATE TABLE DebugMaxProteinGroupsFilterProtein AS SELECT Id FROM Protein WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);\n"
    "DROP TABLE IF EXISTS DebugMaxProteinGroupsFilterPeptideSpectrumMatch; CREATE TABLE DebugMaxProteinGroupsFilterPeptideSpectrumMatch AS SELECT Id FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);\n"
    "DROP TABLE IF EXISTS DebugMaxProteinGroupsFilterSpectrum; CREATE TABLE DebugMaxProteinGroupsFilterSpectrum AS SELECT Id FROM Spectrum WHERE Id NOT IN (SELECT Spectrum FROM PeptideSpectrumMatch);"
);

boost::format deleteProteinsUnderAdditionalPeptideCountSql(
    "DELETE FROM Protein\n"
    "    WHERE Id IN(SELECT pro.Id\n"
    "    FROM Protein pro\n"
    "    JOIN AdditionalMatches am ON pro.Id = am.ProteinId\n"
    "    WHERE am.AdditionalMatches < %1%);\n"
    "DELETE FROM PeptideInstance WHERE Protein NOT IN (SELECT Id FROM Protein);\n"
    "DELETE FROM Peptide WHERE Id NOT IN (SELECT Peptide FROM PeptideInstance);\n"
    "DELETE FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);\n"
    "DELETE FROM Spectrum WHERE Id NOT IN (SELECT Spectrum FROM PeptideSpectrumMatch);"
);
boost::format deleteDebugProteinsUnderAdditionalPeptideCountSql(
    "CREATE TABLE DebugAdditionalPeptidesFilterProtein AS SELECT Id FROM Protein\n"
    "    WHERE Id IN(SELECT pro.Id\n"
    "    FROM Protein pro\n"
    "    JOIN AdditionalMatches am ON pro.Id = am.ProteinId\n"
    "    WHERE am.AdditionalMatches < %1%);\n"
    "CREATE TABLE DebugAdditionalPeptidesFilterPeptideInstance AS SELECT Id FROM PeptideInstance WHERE Protein NOT IN (SELECT Id FROM Protein);\n"
    "CREATE TABLE DebugAdditionalPeptidesFilterPeptide AS SELECT Id FROM Peptide WHERE Id NOT IN (SELECT Peptide FROM PeptideInstance);\n"
    "CREATE TABLE DebugAdditionalPeptidesFilterPeptideSpectrumMatch AS SELECT Id FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);\n"
    "CREATE TABLE DebugAdditionalPeptidesFilterSpectrum AS SELECT Id FROM Spectrum WHERE Id NOT IN (SELECT Spectrum FROM PeptideSpectrumMatch);"
);

string trimFilteredTables(
    "DELETE FROM PeptideInstance WHERE Protein NOT IN (SELECT Id FROM Protein);\n"
    "DELETE FROM PeptideInstance WHERE Peptide NOT IN (SELECT Id FROM Peptide);\n"
    "DELETE FROM Protein WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);\n"
    "DELETE FROM Peptide WHERE Id NOT IN (SELECT Peptide FROM PeptideInstance);\n"
    "DELETE FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);\n"
    "DELETE FROM PeptideSpectrumMatch WHERE Spectrum NOT IN (SELECT Id FROM Spectrum);\n"
    "DELETE FROM Spectrum WHERE Id NOT IN (SELECT Spectrum FROM PeptideSpectrumMatch);"
);


const string prepareCoverageSelectSql1 =
    "    SELECT pro.Id AS Protein, pro.Length AS ProteinLength, pi.Offset AS PeptideOffset, pi.Length AS PeptideLength\n"
    "    FROM PeptideInstance pi\n"
    "    JOIN Protein pro ON pi.Protein = pro.Id\n"
    "    JOIN ProteinData pd ON pi.Protein = pd.Id\n"
    "    GROUP BY pi.Id;\n";
const string prepareCoverageSelectSql2 =
    "    SELECT Protein, CAST(COUNT(DISTINCT i.Value) AS REAL) * 100 / ProteinLength\n"
    "    FROM IntegerSet i, CoverageJoinTable\n"
    "    WHERE i.Value BETWEEN PeptideOffset AND PeptideOffset + PeptideLength - 1\n"
    "    GROUP BY Protein;";
const string prepareCoverageSql(
    "CREATE TEMP TABLE CoverageJoinTable AS\n" + prepareCoverageSelectSql1 +
    "CREATE INDEX CoverageJoinTable_ProteinOffsetLength ON CoverageJoinTable(Protein, PeptideOffset, PeptideLength);\n"
    "\n"
    "DELETE FROM ProteinCoverage;\n"
    "INSERT INTO ProteinCoverage(Id, Coverage)\n" + prepareCoverageSelectSql2
);

} // namespace


BEGIN_IDPICKER_NAMESPACE


struct Filter::Impl
{
    static int tempCacheSize;

    /// Apply data filters to an idpDB file
    Impl(const string& idpDbFilepath)
        : idpDbFilepath(idpDbFilepath)
    {
        this->idpDbConnection = NULL;

        initializeSqlFormats();
    }

    /// Apply data filters to an idpDB file
    Impl(sqlite3* idpDbConnection)
        : idpDbConnection(idpDbConnection)
    {
        initializeSqlFormats();
    }

    void filter(const Config& config, const IterationListenerRegistry* ilr = 0)
    {
        this->config = config;
        this->ilr = ilr;
        initializeConnection();
        filterConnection();
    }

    private:

    boost::format createFilteredSpectrumTableSql;
    boost::format createFilteredProteinTableSql;
    boost::format createFilteredProteinTableByGeneSql;
    boost::format createFilteredPSMTableSql;
    boost::format deleteFilteredPSMsUnderMatchCountSql;
    boost::format createFilteredPeptideTableSql;
    boost::format applyMaxProteinGroupsSql;

    boost::format createDebugFilteredSpectrumTableSql;
    boost::format createDebugFilteredProteinTableSql;
    boost::format createDebugFilteredProteinTableByGeneSql;
    boost::format createDebugFilteredPSMTableSql;
    boost::format applyDebugMaxProteinGroupsSql;

    void initializeSqlFormats()
    {
        createFilteredSpectrumTableSql = ::createFilteredSpectrumTableSql;
        createFilteredProteinTableSql = ::createFilteredProteinTableSql;
        createFilteredProteinTableByGeneSql = ::createFilteredProteinTableByGeneSql;
        createFilteredPSMTableSql = ::createFilteredPSMTableSql;
        deleteFilteredPSMsUnderMatchCountSql = ::deleteFilteredPSMsUnderMatchCountSql;
        createFilteredPeptideTableSql = ::createFilteredPeptideTableSql;
        applyMaxProteinGroupsSql = ::applyMaxProteinGroupsSql;

        createDebugFilteredSpectrumTableSql = ::createDebugFilteredSpectrumTableSql;
        createDebugFilteredProteinTableSql = ::createDebugFilteredProteinTableSql;
        createDebugFilteredPSMTableSql = ::createDebugFilteredPSMTableSql;
        applyDebugMaxProteinGroupsSql = ::applyDebugMaxProteinGroupsSql;
    }

    void clearSqlFormats()
    {
        createFilteredSpectrumTableSql.clear_binds();
        createFilteredProteinTableSql.clear_binds();
        createFilteredProteinTableByGeneSql.clear_binds();
        createFilteredPSMTableSql.clear_binds();
        deleteFilteredPSMsUnderMatchCountSql.clear_binds();
        createFilteredPeptideTableSql.clear_binds();
        applyMaxProteinGroupsSql.clear_binds();

        createDebugFilteredSpectrumTableSql.clear_binds();
        createDebugFilteredProteinTableSql.clear_binds();
        createDebugFilteredProteinTableByGeneSql.clear_binds();
        createDebugFilteredPSMTableSql.clear_binds();
        applyDebugMaxProteinGroupsSql.clear_binds();
    }

    static void precacheFile(const string& filepath)
    {
        if (!bfs::exists(filepath))
            return;

        char buf[32768];
        ifstream f(filepath.c_str());
        while (f.readsome(reinterpret_cast<char*>(&buf), 32768) > 0) {}
    }

    void initializeConnection()
    {
        if (idpDbConnection == NULL)
        {
            if (!bfs::exists(idpDbFilepath))
                throw runtime_error("[Filter::initializeConnection] filepath \"" + idpDbFilepath + "\" does not exist");
            idpDb.reset(new sqlite3pp::database(idpDbFilepath));

            SchemaUpdater::update(idpDb->connected(), ilr);
        }
        else
            idpDb.reset(new sqlite3pp::database(idpDbConnection, false));

        idpDb->load_extension("IdpSqlExtensions");

        hasGeneMetadata = Embedder::hasGeneMetadata(idpDb->connected());
    }

    enum FilterSteps
    {
        FilterStep_DropFilters = 0,
        FilterStep_AnalyzeUnfilteredDatabase,
        FilterStep_FilterSpectra,
        FilterStep_FilterPSMs,
        FilterStep_FilterProteins,
        FilterStep_FilterDistinctMatchPerPeptide,
        FilterStep_FilterPeptides,
        FilterStep_FilterPeptideInstances,
        FilterStep_AssembleProteinGroups1,
        FilterStep_AssembleGeneGroups1,
        FilterStep_FilterMaxProteinGroupsPerPeptide,
        FilterStep_AnalyzeFilteredDatabase,
        FilterStep_AssembleProteinGroups2,
        FilterStep_AssembleGeneGroups2,
        FilterStep_AssembleClusters,
        FilterStep_CalculateAdditionalPeptides,
        FilterStep_FilterAdditionalPeptidesPerProtein,
        FilterStep_AssembleProteinGroups3,
        FilterStep_AssembleGeneGroups3,
        FilterStep_CalculateProteinCoverage,
        FilterStep_AssembleProteinCoverage,
        FilterStep_AssembleDistinctMatches,
        FilterStep_AssemblePeptideGroups,
        FilterStep_CalculateSummaryStatistics,
        FilterStep_UpdateFilterHistory,
        FilterStep_AggregateQuantitationStatistics,
        FilterStep_Count
    };

    static string explainQueryPlan(sqlite3pp::database& db, const string& singleStatement)
    {
        ostringstream result;
        result << singleStatement << "\n";
        sqlite3pp::query planQuery(db, ("EXPLAIN QUERY PLAN " + singleStatement).c_str());
        BOOST_FOREACH(sqlite3pp::query::rows row, planQuery)
        {
            result << row.get<string>(3) << "\n";
        }
        return result.str();
    }

    void filterConnection()
    {
        sqlite3pp::database& idpDb = *this->idpDb;

        sqlite3pp::transaction transaction(idpDb);
        try
        {
            if (filterIsCurrent(idpDb))
            {
                ITERATION_UPDATE(ilr, 0, 1, "filter is current; no update necessary")
                return;
            }

            if (config.geneLevelFiltering && !hasGeneMetadata)
                throw runtime_error("unable to perform gene level filtering without embedded gene metadata");

            ITERATION_UPDATE(ilr, FilterStep_DropFilters, FilterStep_Count, "dropping old filters")
            Qonverter::dropFilters(idpDb.connected());

            ITERATION_UPDATE(ilr, FilterStep_AnalyzeUnfilteredDatabase, FilterStep_Count, "analyzing unfiltered database")
            idpDb.execute("ANALYZE");

            int filteredSpectra = createFilteredSpectrumTable(idpDb);
            int filteredPSMs = createFilteredPSMTable(idpDb);
            deleteFilteredPSMsUnderMatchCount(idpDb);
            int filteredProteins = createFilteredProteinTable(idpDb);
            createFilteredPeptideTable(idpDb);
            int filteredPeptideInstances = createFilteredPeptideInstanceTable(idpDb);

            BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << "First filter results: " << filteredSpectra << " spectra; " << filteredPSMs << " PSMs; " << filteredProteins << " proteins; " << filteredPeptideInstances << " peptide instances";

            renameFilteredTables(idpDb);

            assembleProteinGroups(idpDb, FilterStep_AssembleProteinGroups1);
            applyMaxProteinGroupsFilter(idpDb);

            idpDb.execute(trimFilteredTables.c_str());

            ITERATION_UPDATE(ilr, FilterStep_AnalyzeFilteredDatabase, FilterStep_Count, "analyzing filtered database")
            idpDb.execute("ANALYZE");

            assembleClusters(idpDb);
            applyAdditionalPeptidesFilter(idpDb);
            assembleProteinCoverage(idpDb);
            assembleDistinctMatches(idpDb);
            assemblePeptideGroups(idpDb);

            updateFilterHistory(idpDb);

            aggregateQuantitationStatistics(idpDb);

            transaction.commit();
        }
        catch (cancellation_exception&)
        {
            return;
        }
        catch (runtime_error& e)
        {
            throw runtime_error(string("error filtering connection: ") + e.what());
        }
    }

    string precursorMzTolerancePredicate(const boost::optional<pwiz::chemistry::MZTolerance>& precursorMzTolerance)
    {
        if (!precursorMzTolerance)
            return "";

        switch (precursorMzTolerance.get().units)
        {
            case pwiz::chemistry::MZTolerance::MZ: return " AND WITHIN_MASS_TOLERANCE_MZ(psm.ObservedNeutralMass, psm.ObservedNeutralMass + GET_SMALLER_MASS_ERROR_ADJUSTED(psm.MonoisotopicMassError, psm.MolecularWeightError), " + lexical_cast<string>(precursorMzTolerance.get().value) + ")";
            case pwiz::chemistry::MZTolerance::PPM: return " AND WITHIN_MASS_TOLERANCE_PPM(psm.ObservedNeutralMass, psm.ObservedNeutralMass + GET_SMALLER_MASS_ERROR_ADJUSTED(psm.MonoisotopicMassError, psm.MolecularWeightError), " + lexical_cast<string>(precursorMzTolerance.get().value) + ")";
            default: throw runtime_error("[precursorMzTolerancePredicate] invalid tolerance units");
        }
    }

    int createFilteredSpectrumTable(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_FilterSpectra, FilterStep_Count, "filtering spectra")
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << explainQueryPlan(db, (boost::format(filteredSpectrumSelectSql) % config.maxFDRScore % precursorMzTolerancePredicate(config.precursorMzTolerance)).str());
        //db.execute((createDebugFilteredSpectrumTableSql % config.maxFDRScore % precursorMzTolerancePredicate(config.precursorMzTolerance)).str());
        db.execute((createFilteredSpectrumTableSql % config.maxFDRScore % precursorMzTolerancePredicate(config.precursorMzTolerance)).str());
        return sqlite3pp::query(db, "SELECT COUNT(*) From FilteredSpectrum").begin()->get<int>(0);
    }

    int createFilteredProteinTable(sqlite3pp::database& db)
    {
        boost::format& format = config.geneLevelFiltering ? createFilteredProteinTableByGeneSql : createFilteredProteinTableSql;

        ITERATION_UPDATE(ilr, FilterStep_FilterProteins, FilterStep_Count, "filtering proteins")
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << explainQueryPlan(db, (boost::format(config.geneLevelFiltering ? filteredProteinTableByGeneSelectSql : filteredProteinSelectSql) % config.minDistinctPeptides % config.minSpectra).str());
        //db.execute((createDebugFilteredProteinTableSql % config.minDistinctPeptides % config.minSpectra).str());
        db.execute((format % config.minDistinctPeptides % config.minSpectra).str());
        return sqlite3pp::query(db, "SELECT COUNT(*) From FilteredProtein").begin()->get<int>(0);
    }

    int createFilteredPSMTable(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_FilterPSMs, FilterStep_Count, "filtering peptide spectrum matches")
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << explainQueryPlan(db, (boost::format(filteredPSMSelectSql) % config.maxFDRScore % precursorMzTolerancePredicate(config.precursorMzTolerance)).str());
        //db.execute((createDebugFilteredPSMTableSql % config.maxFDRScore % precursorMzTolerancePredicate(config.precursorMzTolerance)).str());
        db.execute((createFilteredPSMTableSql % config.maxFDRScore % precursorMzTolerancePredicate(config.precursorMzTolerance)).str());
        return sqlite3pp::query(db, "SELECT COUNT(*) From FilteredPeptideSpectrumMatch").begin()->get<int>(0);
    }

    int createFilteredPeptideInstanceTable(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_FilterPeptideInstances, FilterStep_Count, "filtering peptide instances")
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << explainQueryPlan(db, filteredPeptideInstanceSelectSql);
        db.execute(createFilteredPeptideInstanceTableSql);
        return sqlite3pp::query(db, "SELECT COUNT(*) From FilteredPeptideInstance").begin()->get<int>(0);
    }

    void renameFilteredTables(sqlite3pp::database& db)
    {
        db.execute(renameFilteredTablesSql);
    }

    void assembleDistinctMatches(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_AssembleDistinctMatches, FilterStep_Count, "assembling distinct matches")
        db.execute((assembleDistinctMatchesSql % config.distinctMatchFormat.sqlExpression()).str());
    }

    void assemblePeptideGroups(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_AssemblePeptideGroups, FilterStep_Count, "assembling peptide groups")
        db.execute(assemblePeptideGroupsSql);
    }

    void aggregateQuantitationStatistics(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_AggregateQuantitationStatistics, FilterStep_Count, "aggregating quantitation")
        db.execute(aggregateQuantitationStatisticsSql);
    }

    void deleteFilteredPSMsUnderMatchCount(sqlite3pp::database& db)
    {
        if (config.minSpectraPerDistinctMatch <= 1)
            return;

        ITERATION_UPDATE(ilr, FilterStep_FilterDistinctMatchPerPeptide, FilterStep_Count, "filtering out peptides without enough distinct matches")

        string sqlExpression = config.distinctMatchFormat.sqlExpression();
        string filteredSqlExpression = bal::replace_all_copy(sqlExpression, "psm.", "FilteredPeptideSpectrumMatch.");
        db.execute((deleteFilteredPSMsUnderMatchCountSql % filteredSqlExpression % sqlExpression % config.minSpectraPerDistinctMatch).str());
    }

    void createFilteredPeptideTable(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_FilterPeptides, FilterStep_Count, "filtering peptides")

        string filterExpression = config.minSpectraPerDistinctPeptide > 1 ?
                                  "HAVING " + lexical_cast<string>(config.minSpectraPerDistinctPeptide) + " <= COUNT(DISTINCT psm.Spectrum)" :
                                  "";
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << explainQueryPlan(db, (boost::format(filterdPeptideSelectSql) % filterExpression).str());
        db.execute((createFilteredPeptideTableSql % filterExpression).str());

        if (config.minSpectraPerDistinctMatch + config.minSpectraPerDistinctPeptide > 1)
            db.execute("DELETE FROM FilteredPeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM FilteredPeptide);");
    }

    void assembleProteinGroups(sqlite3pp::database& db, FilterSteps filterStep)
    {
        ITERATION_UPDATE(ilr, filterStep, FilterStep_Count, "assembling protein groups")
        db.execute(assembleProteinGroupsSql);

        if (!hasGeneMetadata)
            return;

        ITERATION_UPDATE(ilr, filterStep+1, FilterStep_Count, "assembling gene groups")
        db.execute(assembleGeneGroupsSql);
    }

    void applyMaxProteinGroupsFilter(sqlite3pp::database& db)
    {
        if (config.maxProteinGroupsPerPeptide == 0)
            return;

        ITERATION_UPDATE(ilr, FilterStep_FilterMaxProteinGroupsPerPeptide, FilterStep_Count, "filtering out peptides that map to too many protein groups")

        //db.execute((applyDebugMaxProteinGroupsSql % config.maxProteinGroupsPerPeptide).str());
        db.execute((applyMaxProteinGroupsSql % config.maxProteinGroupsPerPeptide).str());

        // reapply protein-level filters after filtering out ambiguous PSMs
        db.execute("DROP INDEX FiltProtein_Accession;\n"
                   "ALTER TABLE Protein RENAME TO TempProtein");
        string filterProteinsSql = (createFilteredProteinTableSql % config.minDistinctPeptides % config.minSpectra).str();
        bal::replace_all(filterProteinsSql, "Filtered", "");
        bal::replace_all(filterProteinsSql, "JOIN Protein", "JOIN TempProtein");
        db.execute(filterProteinsSql);
        db.execute("DROP TABLE TempProtein");

        assembleProteinGroups(db, FilterStep_AssembleProteinGroups2);
    }

    void assembleClusters(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_AssembleClusters, FilterStep_Count, "assembling clusters")

        typedef map<int, set<sqlite3_int64> > SpectrumSetByProteinGroup;
        SpectrumSetByProteinGroup spectrumSetByProteinGroup;
        map<sqlite3_int64, set<int> > proteinGroupSetBySpectrumId;

        sqlite3pp::query query(db, "SELECT pro.ProteinGroup, psm.Spectrum "
                                   "FROM PeptideSpectrumMatch psm "
                                   "JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide "
                                   "JOIN Protein pro on pi.Protein = pro.Id");

        BOOST_FOREACH(sqlite3pp::query::rows queryRow, query)
        {
            int proteinGroup = queryRow.get<int>(0);
            sqlite3_int64 spectrumId = queryRow.get<sqlite3_int64>(1);

            spectrumSetByProteinGroup[proteinGroup].insert(spectrumId);
            proteinGroupSetBySpectrumId[spectrumId].insert(proteinGroup);
        }

        map<int, int> clusterByProteinGroup;
        int clusterId = 0;
        stack<SpectrumSetByProteinGroup::const_iterator> clusterStack;

        for (SpectrumSetByProteinGroup::const_iterator itr = spectrumSetByProteinGroup.begin(); itr != spectrumSetByProteinGroup.end(); ++itr)
        {
            int proteinGroup = itr->first;

            if (clusterByProteinGroup.count(proteinGroup) > 0)
                continue;

            // for each protein without a cluster assignment, make a new cluster
            ++clusterId;
            clusterStack.push(itr);
            while (clusterStack.size() > 0)
            {
                SpectrumSetByProteinGroup::const_iterator spectrumSetByProteinGroupItr = clusterStack.top();
                clusterStack.pop();

                // add all "cousin" proteins to the current cluster
                BOOST_FOREACH(long spectrumId, spectrumSetByProteinGroupItr->second)
                BOOST_FOREACH(int cousinProteinGroup, proteinGroupSetBySpectrumId[spectrumId])
                {
                    if (!clusterByProteinGroup.insert(make_pair(cousinProteinGroup, clusterId)).second)
                        continue;

                    SpectrumSetByProteinGroup::const_iterator findItr = spectrumSetByProteinGroup.find(cousinProteinGroup);
                    clusterStack.push(findItr);
                }
            }
        }

        sqlite3pp::command assignCluster(db, "UPDATE Protein SET Cluster = ? WHERE ProteinGroup = ?");
        for (map<int, int>::const_iterator itr = clusterByProteinGroup.begin(); itr != clusterByProteinGroup.end(); ++itr)
        {
            assignCluster.binder() << itr->second << itr->first;
            assignCluster.step();
            assignCluster.reset();
        }
    }

    void assembleProteinCoverage(sqlite3pp::database& idpDb)
    {
        ITERATION_UPDATE(ilr, FilterStep_CalculateProteinCoverage, FilterStep_Count, "calculating protein coverage")
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << explainQueryPlan(idpDb, prepareCoverageSelectSql1);
        idpDb.execute(prepareCoverageSql);
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << explainQueryPlan(idpDb, prepareCoverageSelectSql2);

        // get non-zero coverage depths at each protein offset
        sqlite3pp::query coverageMaskRows(idpDb, "SELECT Protein, ProteinLength, i.Value, COUNT(i.Value)\n"
                                                 "FROM IntegerSet i, CoverageJoinTable\n"
                                                 "WHERE i.Value BETWEEN PeptideOffset AND PeptideOffset + PeptideLength - 1\n"
                                                 "GROUP BY Protein, i.Value\n"
                                                 "ORDER BY Protein, i.Value;\n"
                                                 "DROP TABLE CoverageJoinTable;");

        ITERATION_UPDATE(ilr, FilterStep_AssembleProteinCoverage, FilterStep_Count, "updating protein coverage masks")

        sqlite3pp::command updateCoverage(idpDb, "UPDATE ProteinCoverage SET CoverageMask = ? WHERE Id = ?");

        sqlite3_int64 currentProteinId = 0;
        vector<unsigned short> currentProteinMask;

        BOOST_FOREACH(sqlite3pp::query::rows queryRow, coverageMaskRows)
        {
            sqlite3_int64 proteinId;
            int proteinLength;
            int proteinOffset;
            unsigned short coverageDepth;

            queryRow.getter() >> proteinId >> proteinLength >> proteinOffset >> coverageDepth;
            proteinOffset += 2; // skip the 2 bytes at the beginning of the mask

            // before moving on to the next protein, update the current one
            if (proteinId > currentProteinId)
            {
                if (!currentProteinMask.empty())
                {
                    updateCoverage.bind(1, static_cast<const void*>(&currentProteinMask[0]), currentProteinMask.size() * sizeof(unsigned short));
                    updateCoverage.bind(2, currentProteinId);
                    updateCoverage.step();
                    updateCoverage.reset();
                }

                currentProteinId = proteinId;

                // initialize all offsets to 0 (no coverage)
                currentProteinMask.resize(proteinLength+2);
                fill(currentProteinMask.begin(), currentProteinMask.end(), 0);

                // store proteinLength as 2 shorts (little endian) at the beginning of the mask
                memcpy(&currentProteinMask[0], &proteinLength, sizeof(int));
            }

            // set a covered offset to its coverage depth
            currentProteinMask[proteinOffset] = coverageDepth;
        }

        // set the last protein's mask
        if (!currentProteinMask.empty())
        {
            updateCoverage.bind(1, static_cast<const void*>(&currentProteinMask[0]), currentProteinMask.size() * sizeof(unsigned short));
            updateCoverage.bind(2, currentProteinId);
            updateCoverage.step();
            updateCoverage.reset();
        }

        idpDb.execute("DROP TABLE CoverageJoinTable");
    }

    typedef set<sqlite3_int64> ResultSet; // a set of spectrum Ids
    typedef map<size_t, shared_ptr<ResultSet> > ResultMap; // a set of ResultSets keyed by the hash of the ResultSet
    typedef map<int, ResultMap> ResultMapByGroupId; // a ResultMap corresponding to each protein/gene group
    typedef map<int, ResultMapByGroupId> ResultMapByGroupIdByCluster; // a ResultMapByProteinId corresponding to each cluster

    struct AdditionalPeptidesRow
    {
        int groupId; // protein or gene
        int uniquePeptides;
        int sharedPeptides;
        int uniqueSpectra;
        int sharedSpectra;
    };

    struct AdditionalPeptidesContext
    {
        ResultMapByGroupIdByCluster resultSetByGroupIdByCluster;
        map<int, set<sqlite3_int64> > proteinSetByGroupId;
        map<sqlite3_int64, int> sharedResultsByGroupId;
        map<sqlite3_int64, int> additionalPeptidesByGroupId;
        set<AdditionalPeptidesRow> peptideCounts;

        set<int> maxGroupIds;
        size_t maxExplainedCount;
        int minSharedResults;
    };

    void applyAdditionalPeptidesFilter(sqlite3pp::database& db)
    {
        if (config.minAdditionalPeptides == 0)
            return;

        const char* groupProperty = config.geneLevelFiltering ? "pro.GeneGroup" : "pro.ProteinGroup";

        boost::format createSpectrumResultsTableSql("DROP TABLE IF EXISTS SpectrumResults;\n"
                                                    "CREATE TEMP TABLE SpectrumResults AS\n"
                                                    "   SELECT psm.Spectrum AS Spectrum, GROUP_CONCAT(DISTINCT psm.Peptide) AS Peptides, COUNT(DISTINCT %1%) AS SharedResultCount\n"
                                                    "   FROM PeptideSpectrumMatch psm\n"
                                                    "   JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide\n"
                                                    "   JOIN Protein pro ON pi.Protein=pro.Id\n"
                                                    "   GROUP BY psm.Spectrum");
        db.execute((createSpectrumResultsTableSql % groupProperty).str().c_str());

        boost::format queryByProteinSql("SELECT pro.Id, %1%, pro.Cluster, SUM(sr.SharedResultCount)\n"
                                        "FROM Protein pro\n"
                                        "JOIN PeptideInstance pi ON pro.Id = pi.Protein\n"
                                        "JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide\n"
                                        "JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum\n"
                                        "WHERE pi.Id = (SELECT Id FROM PeptideInstance WHERE Peptide = pi.Peptide AND Protein = pi.Protein LIMIT 1)\n"
                                        "AND psm.Id = (SELECT Id FROM PeptideSpectrumMatch WHERE Peptide = pi.Peptide LIMIT 1)\n"
                                        "GROUP BY pro.Id\n"
                                        "ORDER BY pro.Cluster");
        sqlite3pp::query queryByProtein(db, (queryByProteinSql % groupProperty).str().c_str());

        // For each protein/gene group, get the list of peptides evidencing it;
        // an ambiguous spectrum will show up as a nested list of peptides
        boost::format queryByResultSql("SELECT %1%, pro.Cluster, GROUP_CONCAT(DISTINCT sr.Peptides)\n"
                                       "FROM Protein pro\n"
                                       "JOIN PeptideInstance pi ON pro.Id = pi.Protein\n"
                                       "JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide\n"
                                       "JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum\n"
                                       "WHERE pi.Id = (SELECT Id FROM PeptideInstance WHERE Peptide = pi.Peptide AND Protein = pi.Protein LIMIT 1)\n"
                                       "AND psm.Id = (SELECT Id FROM PeptideSpectrumMatch WHERE Peptide = pi.Peptide LIMIT 1)\n"
                                       "GROUP BY %1%, sr.Peptides");
        sqlite3pp::query queryByResult(db, (queryByResultSql % groupProperty).str().c_str());

        AdditionalPeptidesContext context;

        ITERATION_UPDATE(ilr, FilterStep_CalculateAdditionalPeptides, FilterStep_Count, "calculating additional peptide counts")

        // construct the result set for each protein
        vector<string> resultIdTokens;
        BOOST_FOREACH(sqlite3pp::query::rows queryRow, queryByResult)
        {
            sqlite3_int64 groupId = queryRow.get<sqlite3_int64>(0);
            int cluster = queryRow.get<int>(1);
            string resultIds = queryRow.get<string>(2);
            bal::split(resultIdTokens, resultIds, bal::is_any_of(","));
            shared_ptr<ResultSet> resultSet(new ResultSet);
            BOOST_FOREACH(const string& token, resultIdTokens)
                resultSet->insert(lexical_cast<sqlite3_int64>(token));
            context.resultSetByGroupIdByCluster[cluster][groupId][boost::hash_range(resultSet->begin(), resultSet->end())] = resultSet;
        }

        int lastCluster = 0;
        BOOST_FOREACH(sqlite3pp::query::rows queryRow, queryByProtein)
        {
            sqlite3_int64 proteinId = queryRow.get<sqlite3_int64>(0);
            int groupId = queryRow.get<int>(1);
            int cluster = queryRow.get<int>(2);

            if (lastCluster > 0 && cluster != lastCluster)
            {
                calculateAdditionalPeptidesLoopBody(lastCluster, context);

                context.sharedResultsByGroupId.clear();
            }

            lastCluster = cluster;
            context.sharedResultsByGroupId[groupId] = queryRow.get<int>(3);
            context.proteinSetByGroupId[groupId].insert(proteinId);
        }

        if (lastCluster > 0)
            calculateAdditionalPeptidesLoopBody(lastCluster, context);

        db.execute("DROP TABLE IF EXISTS AdditionalMatches;\n"
                   "CREATE TABLE AdditionalMatches(ProteinId INTEGER PRIMARY KEY, AdditionalMatches INT)");

        sqlite3pp::command insertAdditionalMatches(db, "INSERT INTO AdditionalMatches VALUES (?, ?)");
        for (map<sqlite3_int64, int>::const_iterator itr = context.additionalPeptidesByGroupId.begin(); itr != context.additionalPeptidesByGroupId.end(); ++itr)
        BOOST_FOREACH(const sqlite3_int64& proteinId, context.proteinSetByGroupId[itr->first])
        {
            insertAdditionalMatches.binder() << proteinId << itr->second;
            insertAdditionalMatches.step();
            insertAdditionalMatches.reset();
        }

        ITERATION_UPDATE(ilr, FilterStep_FilterAdditionalPeptidesPerProtein, FilterStep_Count, "applying additional peptides filter")

        db.execute((deleteProteinsUnderAdditionalPeptideCountSql % config.minAdditionalPeptides).str());

        db.execute("DROP INDEX Protein_ProteinGroup; DROP INDEX IF EXISTS Protein_GeneGroup");
        assembleProteinGroups(db, FilterStep_AssembleProteinGroups3);
    }

    // find the proteins/genes that explain the most results for this cluster
    void findMaxProteins(ResultMapByGroupId& resultSetByGroupId, AdditionalPeptidesContext& context)
    {
        for (ResultMapByGroupId::const_iterator itr = resultSetByGroupId.begin(); itr != resultSetByGroupId.end(); ++itr)
        {
            sqlite3_int64 groupId = itr->first;
            const ResultMap& explainedResults = itr->second;
            int sharedResults = context.sharedResultsByGroupId[groupId];

            if (explainedResults.size() > context.maxExplainedCount)
            {
                context.maxGroupIds.clear();
                context.maxGroupIds.insert(groupId);
                context.maxExplainedCount = explainedResults.size();
                context.minSharedResults = sharedResults;
            }
            else if (explainedResults.size() == context.maxExplainedCount)
            {
                if (sharedResults < context.minSharedResults)
                {
                    context.maxGroupIds.clear();
                    context.maxGroupIds.insert(groupId);
                    context.minSharedResults = sharedResults;
                }
                else if (sharedResults == context.minSharedResults)
                    context.maxGroupIds.insert(groupId);
            }
        }
    }

    void calculateAdditionalPeptidesLoopBody(int cluster, AdditionalPeptidesContext& context)
    {
        context.maxGroupIds.clear();
        context.maxExplainedCount = 0;
        context.minSharedResults = 0;

        // keep track of the proteins that explain the most results
        ResultMapByGroupId& resultSetByGroupId = context.resultSetByGroupIdByCluster[cluster];

        // find the proteins that explain the most results for this cluster
        findMaxProteins(resultSetByGroupId, context);

        // the set of results explained by the max. proteins
        ResultMap maxExplainedResults;

        // loop until the resultSetByProteinId map is empty
        while (resultSetByGroupId.size() > 0)
        {
            maxExplainedResults.clear();

            // remove max. proteins from the resultSetByProteinId map
            BOOST_FOREACH(sqlite3_int64 maxGroupId, context.maxGroupIds)
            {
                ResultMapByGroupId::const_iterator findItr = resultSetByGroupId.find(maxGroupId);

                if (maxExplainedResults.empty())
                    maxExplainedResults = findItr->second;
                else
                {
                    maxExplainedResults.insert(findItr->second.begin(), findItr->second.end());
                }

                resultSetByGroupId.erase(maxGroupId);
                context.additionalPeptidesByGroupId[maxGroupId] = context.maxExplainedCount;
            }

            // subtract the max. proteins' results from the remaining proteins
            for (ResultMap::const_iterator itr = maxExplainedResults.begin(); itr != maxExplainedResults.end(); ++itr)
                for (ResultMapByGroupId::iterator itr2 = resultSetByGroupId.begin(); itr2 != resultSetByGroupId.end(); ++itr2)
                    itr2->second.erase(itr->first);

            context.maxGroupIds.clear();
            context.maxExplainedCount = 0;
            context.minSharedResults = 0;

            // find the proteins that explain the most results for this cluster
            findMaxProteins(resultSetByGroupId, context);

            // all remaining proteins present no additional evidence, so break the loop
            if (context.maxExplainedCount == 0)
            {
                for (ResultMapByGroupId::const_iterator itr = resultSetByGroupId.begin(); itr != resultSetByGroupId.end(); ++itr)
                    context.additionalPeptidesByGroupId[itr->first] = 0;
                break;
            }
        }
    }

    // returns false if filter is already up to date
    bool filterIsCurrent(sqlite3pp::database& db)
    {
        try
        {
            // if no unfiltered tables are present, the database is unfiltered (even if the FilterHistory is non-empty)
            sqlite3pp::query(db, "SELECT Id FROM UnfilteredProtein LIMIT 1").begin();

            sqlite3pp::query previousFilterQuery(db, "SELECT IFNULL(MAX(Id), 0)\n"
                                                     "FROM FilterHistory\n"
                                                     "WHERE MaximumQValue = ?\n"
                                                     "AND MinimumDistinctPeptides = ?\n"
                                                     "AND MinimumSpectra = ?\n"
                                                     "AND MinimumAdditionalPeptides = ?\n"
                                                     "AND GeneLevelFiltering = ?\n"
                                                     "AND PrecursorMzTolerance = ?\n"
                                                     "AND DistinctMatchFormat = ?\n"
                                                     "AND MinimumSpectraPerDistinctMatch = ?\n"
                                                     "AND MinimumSpectraPerDistinctPeptide = ?\n"
                                                     "AND MaximumProteinGroupsPerPeptide = ?");

            previousFilterQuery.binder() << config.maxFDRScore <<
                                            config.minDistinctPeptides <<
                                            config.minSpectra <<
                                            config.minAdditionalPeptides <<
                                            config.geneLevelFiltering <<
                                            (config.precursorMzTolerance ? lexical_cast<string>(config.precursorMzTolerance.get()) : "") <<
                                            config.distinctMatchFormat.filterHistoryExpression() <<
                                            config.minSpectraPerDistinctMatch <<
                                            config.minSpectraPerDistinctPeptide <<
                                            config.maxProteinGroupsPerPeptide;

            sqlite3_int64 currentFilterId = sqlite3pp::query(db, "SELECT IFNULL(MAX(Id), 0) FROM FilterHistory").begin()->get<sqlite3_int64>(0);

            sqlite3pp::query::iterator itr = previousFilterQuery.begin();
            if (currentFilterId > 0 && itr != previousFilterQuery.end())
            {
                sqlite3_int64 previousFilterId = itr->get<sqlite3_int64>(0);
                // if the new filter is already in the history and it's the current filter, return true to indicate no update is needed
                if (currentFilterId == previousFilterId)
                    return true;
            }
        }
        catch (sqlite3pp::database_error& e)
        {
            if (!bal::contains(e.what(), "no such table"))
                throw e;
        }

        return false;
    }

    void updateFilterHistory(sqlite3pp::database& db)
    {
        ITERATION_UPDATE(ilr, FilterStep_CalculateSummaryStatistics, FilterStep_Count, "calculating summary statistics")

        TotalCounts totalCounts(db.connected());

        ITERATION_UPDATE(ilr, FilterStep_UpdateFilterHistory, FilterStep_Count, "updating filter history")

        db.execute("CREATE TABLE IF NOT EXISTS FilterHistory (Id INTEGER PRIMARY KEY, MaximumQValue NUMERIC, MinimumDistinctPeptides INT, MinimumSpectra INT, MinimumAdditionalPeptides INT, GeneLevelFiltering INT, PrecursorMzTolerance TEXT\n"
                   "                                          DistinctMatchFormat TEXT, MinimumSpectraPerDistinctMatch INT, MinimumSpectraPerDistinctPeptide INT, MaximumProteinGroupsPerPeptide INT,\n"
                   "                                          Clusters INT, ProteinGroups INT, Proteins INT, GeneGroups INT, Genes INT, DistinctPeptides INT, DistinctMatches INT, FilteredSpectra INT, ProteinFDR NUMERIC, PeptideFDR NUMERIC, SpectrumFDR NUMERIC);");

        sqlite3_int64 nextFilterId = sqlite3pp::query(db, "SELECT IFNULL(MAX(Id), 0)+1 FROM FilterHistory").begin()->get<sqlite3_int64>(0);

        // must explicitly specify columns since the 8 to 9 schema upgrade added columns
        string insertFilterSql = "INSERT INTO FilterHistory (Id, MaximumQValue, MinimumDistinctPeptides, MinimumSpectra,  MinimumAdditionalPeptides, GeneLevelFiltering, PrecursorMzTolerance,\n"
                                 "                           DistinctMatchFormat, MinimumSpectraPerDistinctMatch, MinimumSpectraPerDistinctPeptide, MaximumProteinGroupsPerPeptide,\n"
                                 "                           Clusters, ProteinGroups, Proteins, GeneGroups, Genes, DistinctPeptides, DistinctMatches, FilteredSpectra, ProteinFDR, PeptideFDR, SpectrumFDR)\n"
                                 "VALUES\n"
                                 "(\n"
                                 " ?," // Id
                                 " ?," // MaximumQValue
                                 " ?," // MinimumDistinctPeptides
                                 " ?," // MinimumSpectra
                                 " ?," // MinimumAdditionalPeptides
                                 " ?," // GeneLevelFiltering
                                 " ?," // PrecursorMzTolerance
                                 " ?," // DistinctMatchFormat
                                 " ?," // MinimumSpectraPerDistinctMatch
                                 " ?," // MinimumSpectraPerDistinctPeptide
                                 " ?," // MaximumProteinGroupsPerPeptide
                                 " ?," // Clusters
                                 " ?," // ProteinGroups
                                 " ?," // Proteins
                                 " ?," // GeneGroups
                                 " ?," // Genes
                                 " ?," // DistinctPeptides
                                 " ?," // DistinctMatches
                                 " ?," // FilteredSpectra
                                 " ?," // ProteinFDR
                                 " ?," // PeptideFDR
                                 " ?" // SpectrumFDR
                                 ")";

        sqlite3pp::query previousFilterQuery(db, "SELECT IFNULL(MAX(Id), 0)\n"
                                                 "FROM FilterHistory\n"
                                                 "WHERE MaximumQValue = ?\n"
                                                 "AND MinimumDistinctPeptides = ?\n"
                                                 "AND MinimumSpectra = ?\n"
                                                 "AND MinimumAdditionalPeptides = ?\n"
                                                 "AND GeneLevelFiltering = ?\n"
                                                 "AND PrecursorMzTolerance = ?\n"
                                                 "AND DistinctMatchFormat = ?\n"
                                                 "AND MinimumSpectraPerDistinctMatch = ?\n"
                                                 "AND MinimumSpectraPerDistinctPeptide = ?\n"
                                                 "AND MaximumProteinGroupsPerPeptide = ?");

        previousFilterQuery.binder() << config.maxFDRScore <<
                                        config.minDistinctPeptides <<
                                        config.minSpectra <<
                                        config.minAdditionalPeptides <<
                                        config.geneLevelFiltering <<
                                        (config.precursorMzTolerance ? lexical_cast<string>(config.precursorMzTolerance.get()) : "") <<
                                        config.distinctMatchFormat.filterHistoryExpression() <<
                                        config.minSpectraPerDistinctMatch <<
                                        config.minSpectraPerDistinctPeptide <<
                                        config.maxProteinGroupsPerPeptide;

        sqlite3pp::query::iterator itr = previousFilterQuery.begin();
        if (itr != previousFilterQuery.end())
            db.execute(("DELETE FROM FilterHistory WHERE Id=" + lexical_cast<string>(itr->get<sqlite3_int64>(0))).c_str());

        sqlite3pp::command insertFilter(db, insertFilterSql.c_str());
        insertFilter.binder() << nextFilterId <<
                                 config.maxFDRScore <<
                                 config.minDistinctPeptides <<
                                 config.minSpectra <<
                                 config.minAdditionalPeptides <<
                                 config.geneLevelFiltering <<
                                 (config.precursorMzTolerance ? lexical_cast<string>(config.precursorMzTolerance.get()) : "") <<
                                 config.distinctMatchFormat.filterHistoryExpression() <<
                                 config.minSpectraPerDistinctMatch <<
                                 config.minSpectraPerDistinctPeptide <<
                                 config.maxProteinGroupsPerPeptide <<
                                 totalCounts.clusters() <<
                                 totalCounts.proteinGroups() <<
                                 totalCounts.proteins() <<
                                 totalCounts.geneGroups() <<
                                 totalCounts.genes() <<
                                 totalCounts.distinctPeptides() <<
                                 totalCounts.distinctMatches() <<
                                 totalCounts.filteredSpectra() <<
                                 totalCounts.proteinFDR() <<
                                 totalCounts.peptideFDR() <<
                                 totalCounts.spectrumFDR();
        insertFilter.step();
        insertFilter.reset();
    }


    const IterationListenerRegistry* ilr;

    Config config;
    sqlite3* idpDbConnection;
    string idpDbFilepath;
    scoped_ptr<sqlite3pp::database> idpDb;
    bool hasGeneMetadata;
};

int Filter::Impl::tempCacheSize = 30000; // 1 GB


string Filter::DistinctMatchFormat::sqlExpression() const
{
    // Peptide + Charge? + Analysis? + Modifications? (possibly rounded)
    ostringstream sql;
    sql << "(psm.Peptide";
    if (isChargeDistinct) sql << " || ' ' || psm.Charge";
    if (isAnalysisDistinct) sql << " || ' ' || psm.Analysis";

    if (areModificationsDistinct)
    {
        sql << " || ' ' ||\n";
        if (modificationMassRoundToNearest.is_initialized())
            sql << "IFNULL((SELECT GROUP_CONCAT((ROUND(mod.MonoMassDelta/" << modificationMassRoundToNearest.get() <<
                    ", 0)*" << modificationMassRoundToNearest.get() << ") || '@' || pm.Offset)\n"
                    "       FROM PeptideModification pm\n"
                    "       JOIN Modification mod ON pm.Modification = mod.Id\n"
                    "       WHERE psm.Id = pm.PeptideSpectrumMatch), '')\n";
        else
            sql << "IFNULL((SELECT GROUP_CONCAT(pm.Modification || '@' || pm.Offset)\n"
                   "        FROM PeptideModification pm\n"
                   "        WHERE psm.Id = pm.PeptideSpectrumMatch), '')\n";
    }
    sql << ")";

    return sql.str();
}

string Filter::DistinctMatchFormat::filterHistoryExpression() const
{
    ostringstream result;
    result << noboolalpha << isChargeDistinct << " " << isAnalysisDistinct << " " << areModificationsDistinct << " " << setprecision(7) << fixed << modificationMassRoundToNearest.get_value_or(1.0);
    return result.str();
}

void Filter::DistinctMatchFormat::parseFilterHistoryExpression(const string& expression)
{
    istringstream ss(expression);
    double tmp;
    ss >> isChargeDistinct >> isAnalysisDistinct >> areModificationsDistinct >> tmp;
    modificationMassRoundToNearest = tmp;
}


Filter::Filter()
{}

Filter::~Filter()
{}

void Filter::filter(const string& idpDbFilepath, const pwiz::util::IterationListenerRegistry* ilr)
{
    _impl.reset(new Impl(idpDbFilepath));
    _impl->filter(config, ilr);
}

void Filter::filter(sqlite3* idpDbConnection, const pwiz::util::IterationListenerRegistry* ilr)
{
    _impl.reset(new Impl(idpDbConnection));
    _impl->filter(config, ilr);
}

boost::optional<Filter::Config> Filter::currentConfig(const string& idpDbFilepath)
{
    // open the database
    sqlite3pp::database idpDb(idpDbFilepath, sqlite3pp::no_mutex);

    return currentConfig(idpDb.connected());
}

boost::optional<Filter::Config> Filter::currentConfig(sqlite3* idpDbConnection)
{
    // open the database
    sqlite3pp::database db(idpDbConnection, false);
    
    sqlite3pp::query currentFilterQuery(db, "SELECT MaximumQValue\n"
                                            "     , MinimumDistinctPeptides\n"
                                            "     , MinimumSpectra\n"
                                            "     , MinimumAdditionalPeptides\n"
                                            "     , GeneLevelFiltering\n"
                                            "     , PrecursorMzTolerance\n"
                                            "     , DistinctMatchFormat\n"
                                            "     , MinimumSpectraPerDistinctMatch\n"
                                            "     , MinimumSpectraPerDistinctPeptide\n"
                                            "     , MaximumProteinGroupsPerPeptide\n"
                                            "FROM FilterHistory\n"
                                            "ORDER BY Id DESC LIMIT 1");
    sqlite3pp::query::iterator currentFilterItr = currentFilterQuery.begin();

    try
    {
        // if no filter history is present, consider the database unfiltered
        if (currentFilterItr == currentFilterQuery.end())
            return boost::optional<Filter::Config>();

        // if no unfiltered tables are present, the database is unfiltered (even if the FilterHistory is non-empty)
        sqlite3pp::query(db, "SELECT Id FROM UnfilteredProtein LIMIT 1").begin();
    }
    catch (sqlite3pp::database_error& e)
    {
        if (!bal::icontains(e.what(), "no such table"))
            throw e;
        return boost::optional<Filter::Config>();
    }

    Config currentConfig;
    string precursorMzTolerance;
    string distinctMatchFormat;

    currentFilterItr->getter() >> currentConfig.maxFDRScore
        >> currentConfig.minDistinctPeptides
        >> currentConfig.minSpectra
        >> currentConfig.minAdditionalPeptides
        >> reinterpret_cast<int&>(currentConfig.geneLevelFiltering)
        >> precursorMzTolerance
        >> distinctMatchFormat
        >> currentConfig.minSpectraPerDistinctMatch
        >> currentConfig.minSpectraPerDistinctPeptide
        >> currentConfig.maxProteinGroupsPerPeptide;

    if (!precursorMzTolerance.empty())
        currentConfig.precursorMzTolerance = lexical_cast<pwiz::chemistry::MZTolerance>(precursorMzTolerance);
    currentConfig.distinctMatchFormat.parseFilterHistoryExpression(distinctMatchFormat);

    return currentConfig;
}


END_IDPICKER_NAMESPACE
