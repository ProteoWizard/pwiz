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
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//


#include "sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "SchemaUpdater.hpp"
#include "Merger.hpp"
#include "Qonverter.hpp"
#include "boost/foreach_field.hpp"
#include "boost/assert.hpp"
#include "boost/atomic.hpp"
#include "boost/thread.hpp"
#include "boost/make_shared.hpp"
#include <deque>
#include <algorithm>


using namespace pwiz::util;
typedef IterationListener::UpdateMessage UpdateMessage;


#ifdef WIN32
#define WIN32_LEAN_AND_MEAN
#include "windows.h"
#include <eh.h>
static bool isPathOnFixedDrive(const std::string& path)
{
    bfs::path completePath = bfs::system_complete(path);
    return GetDriveTypeA(completePath.root_path().string().c_str()) == DRIVE_FIXED;
}
#else
static bool isPathOnFixedDrive(const std::string& path)
{
    return true;
}
#endif // WIN32


namespace {

boost::format mergeProteinsSql(
    "DROP TABLE IF EXISTS ProteinMergeMap;\n"
    "CREATE TABLE ProteinMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);\n"
    "INSERT INTO ProteinMergeMap\n"
    "    SELECT newPro.Id, IFNULL(oldPro.Id, newPro.Id + %1%)\n"
    "    FROM %2%.Protein newPro\n"
    "    LEFT JOIN merged.Protein oldPro ON newPro.Accession = oldPro.Accession;\n"
    "CREATE UNIQUE INDEX ProteinMergeMap_Index2 ON ProteinMergeMap(AfterMergeId);\n"
    "\n"
    "DROP TABLE IF EXISTS NewProteins;\n"
    "CREATE TABLE NewProteins AS\n"
    "    SELECT BeforeMergeId, AfterMergeId\n"
    "    FROM ProteinMergeMap\n"
    "    WHERE AfterMergeId > %1%\n");

boost::format mergePeptideInstancesSql(
    "DROP TABLE IF EXISTS PeptideInstanceMergeMap;\n"
    "CREATE TABLE PeptideInstanceMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INTEGER, AfterMergeProtein INTEGER, BeforeMergePeptide INTEGER, AfterMergePeptide INTEGER);\n"
    "\n"
    "INSERT INTO PeptideInstanceMergeMap\n"
    "    SELECT newInstance.Id, IFNULL(oldInstance.Id, newInstance.Id + %1%), IFNULL(oldInstance.Protein, proMerge.AfterMergeId), newInstance.Peptide, IFNULL(oldInstance.Peptide, newInstance.Peptide + %2%)\n"
    "    FROM ProteinMergeMap proMerge\n"
    "    JOIN %3%.PeptideInstance newInstance ON proMerge.BeforeMergeId = newInstance.Protein\n"
    "    AND newInstance.Offset IS NOT NULL\n"
    "    LEFT JOIN merged.PeptideInstance oldInstance ON proMerge.AfterMergeId = oldInstance.Protein\n"
    "    AND newInstance.Length = oldInstance.Length\n"
    "    AND newInstance.Offset = oldInstance.Offset;\n"
    "INSERT INTO PeptideInstanceMergeMap\n"
    "    SELECT newInstance.Id, MIN(IFNULL(oldInstance.Id, newInstance.Id + %1%)), IFNULL(oldInstance.Protein, proMerge.AfterMergeId), newInstance.Peptide, MIN(IFNULL(oldPeptide.Id, newInstance.Peptide + %2%))\n"
    "    FROM ProteinMergeMap proMerge\n"
    "    JOIN %3%.PeptideInstance newInstance ON proMerge.BeforeMergeId = newInstance.Protein\n"
    "    AND newInstance.Offset IS NULL\n"
    "    JOIN %3%.Peptide newPeptide ON newInstance.Peptide = newPeptide.Id\n"
    "    LEFT JOIN merged.Peptide oldPeptide ON newPeptide.DecoySequence = oldPeptide.DecoySequence\n"
    "    LEFT JOIN merged.PeptideInstance oldInstance ON oldPeptide.Id = oldInstance.Peptide\n"
    "    AND proMerge.AfterMergeId = oldInstance.Protein\n"
    "    GROUP BY newInstance.Id;\n"
    "\n"
    "CREATE UNIQUE INDEX PeptideInstanceMergeMap_Index2 ON PeptideInstanceMergeMap(AfterMergeId);\n"
    "CREATE INDEX PeptideInstanceMergeMap_Index3 ON PeptideInstanceMergeMap(BeforeMergePeptide);\n"
    "CREATE INDEX PeptideInstanceMergeMap_Index4 ON PeptideInstanceMergeMap(AfterMergePeptide);\n");

boost::format mergeAnalysesSql(
    "DROP TABLE IF EXISTS AnalysisMergeMap;\n"
    "CREATE TABLE AnalysisMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);\n"
    "INSERT INTO AnalysisMergeMap\n"
    "    SELECT newAnalysis.Id, IFNULL(oldAnalysis.Id, newAnalysis.Id + %1%)\n"
    "    FROM(SELECT a.Id, SoftwareName || ' ' || SoftwareVersion || ' ' || GROUP_CONCAT(ap.Name || ' ' || ap.Value) AS DistinctKey\n"
    "    FROM %2%.Analysis a\n"
    "    LEFT JOIN %2%.AnalysisParameter ap ON a.Id = Analysis\n"
    "    GROUP BY a.Id) newAnalysis\n"
    "    LEFT JOIN(SELECT a.Id, SoftwareName || ' ' || SoftwareVersion || ' ' || GROUP_CONCAT(ap.Name || ' ' || ap.Value) AS DistinctKey\n"
    "    FROM merged.Analysis a\n"
    "    LEFT JOIN merged.AnalysisParameter ap ON a.Id = Analysis\n"
    "    GROUP BY a.Id) oldAnalysis ON newAnalysis.DistinctKey = oldAnalysis.DistinctKey\n");

boost::format mergeSpectrumSourceGroupsSql(
    "DROP TABLE IF EXISTS SpectrumSourceGroupMergeMap;\n"
    "CREATE TABLE SpectrumSourceGroupMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);\n"
    "INSERT INTO SpectrumSourceGroupMergeMap\n"
    "    SELECT newGroup.Id, IFNULL(oldGroup.Id, newGroup.Id + %1%)\n"
    "    FROM %2%.SpectrumSourceGroup newGroup\n"
    "    LEFT JOIN merged.SpectrumSourceGroup oldGroup ON newGroup.Name = oldGroup.Name\n");

boost::format mergeSpectrumSourcesSql(
    "DROP TABLE IF EXISTS SpectrumSourceMergeMap;\n"
    "CREATE TABLE SpectrumSourceMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);\n"
    "INSERT INTO SpectrumSourceMergeMap\n"
    "    SELECT newSource.Id, IFNULL(oldSource.Id, newSource.Id + %1%)\n"
    "    FROM %2%.SpectrumSource newSource\n"
    "    LEFT JOIN merged.SpectrumSource oldSource ON newSource.Name = oldSource.Name;\n"
    "CREATE UNIQUE INDEX SpectrumSourceMergeMap_Index2 ON SpectrumSourceMergeMap(AfterMergeId)\n");

boost::format mergeSpectrumSourceGroupLinksSql(
    "DROP TABLE IF EXISTS SpectrumSourceGroupLinkMergeMap;\n"
    "CREATE TABLE SpectrumSourceGroupLinkMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);\n"
    "INSERT INTO SpectrumSourceGroupLinkMergeMap\n"
    "    SELECT newLink.Id, IFNULL(oldLink.Id, newLink.Id + %1%)\n"
    "    FROM %2%.SpectrumSourceGroupLink newLink\n"
    "    JOIN SpectrumSourceMergeMap ssMerge ON newLink.Source = ssMerge.BeforeMergeId\n"
    "    JOIN SpectrumSourceGroupMergeMap ssgMerge ON newLink.Group_ = ssgMerge.BeforeMergeId\n"
    "    LEFT JOIN merged.SpectrumSourceGroupLink oldLink ON ssMerge.AfterMergeId = oldLink.Source\n"
    "    AND ssgMerge.AfterMergeId = oldLink.Group_\n");

boost::format mergeSpectraSql(
    "DROP TABLE IF EXISTS SpectrumMergeMap;\n"
    "CREATE TABLE SpectrumMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);\n"
    "INSERT INTO SpectrumMergeMap\n"
    "  SELECT newSpectrum.Id, IFNULL(oldSpectrum.Id, newSpectrum.Id + %1%)\n"
    "  FROM %2%.Spectrum newSpectrum\n"
    "  JOIN SpectrumSourceMergeMap ssMerge ON newSpectrum.Source = ssMerge.BeforeMergeId\n"
    "  LEFT JOIN merged.Spectrum oldSpectrum ON ssMerge.AfterMergeId = oldSpectrum.Source\n"
    "  AND newSpectrum.NativeID = oldSpectrum.NativeID\n");

boost::format mergeModificationsSql(
    "DROP TABLE IF EXISTS ModificationMergeMap;\n"
    "CREATE TABLE ModificationMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);\n"
    "INSERT INTO ModificationMergeMap\n"
    "    SELECT newMod.Id, IFNULL(oldMod.Id, newMod.Id + %1%)\n"
    "    FROM %2%.Modification newMod\n"
    "    LEFT JOIN merged.Modification oldMod ON IFNULL(newMod.Formula, 1) = IFNULL(oldMod.Formula, 1)\n"
    "    AND newMod.MonoMassDelta = oldMod.MonoMassDelta\n");

boost::format mergePeptideSpectrumMatchScoreNamesSql(
    "DROP TABLE IF EXISTS PeptideSpectrumMatchScoreNameMergeMap;\n"
    "CREATE TABLE PeptideSpectrumMatchScoreNameMergeMap(BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);\n"
    "INSERT INTO PeptideSpectrumMatchScoreNameMergeMap\n"
    "    SELECT newName.Id, IFNULL(oldName.Id, newName.Id + %1%)\n"
    "    FROM %2%.PeptideSpectrumMatchScoreName newName\n"
    "    LEFT JOIN merged.PeptideSpectrumMatchScoreName oldName ON newName.Name = oldName.Name\n");

boost::format addNewProteinsSql(
    "INSERT INTO merged.Protein (Id, Accession, IsDecoy, Length, GeneId, GeneGroup)\n"
    "    SELECT AfterMergeId, Accession, IsDecoy, Length, GeneId, GeneGroup\n"
    "    FROM NewProteins\n"
    "    JOIN %1%.Protein newPro ON BeforeMergeId = newPro.Id;\n"
    "\n"
    "INSERT INTO merged.ProteinMetadata\n"
    "    SELECT AfterMergeId, Description, TaxonomyId, GeneName, Chromosome, GeneFamily, GeneDescription\n"
    "    FROM NewProteins\n"
    "    JOIN %1%.ProteinMetadata newPro ON BeforeMergeId = newPro.Id;\n"
    "\n"
    "INSERT INTO merged.ProteinData\n"
    "    SELECT AfterMergeId, Sequence\n"
    "    FROM NewProteins\n"
    "    JOIN %1%.ProteinData newPro ON BeforeMergeId = newPro.Id\n");

boost::format addNewPeptideInstancesSql(
    "INSERT INTO merged.PeptideInstance\n"
    "    SELECT AfterMergeId, AfterMergeProtein, AfterMergePeptide,\n"
    "    Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages\n"
    "    FROM PeptideInstanceMergeMap piMerge\n"
    "    JOIN %1%.PeptideInstance newInstance ON BeforeMergeId = newInstance.Id\n"
    "    WHERE AfterMergeId > %2%\n");

boost::format addNewPeptidesSql(
    "INSERT INTO merged.Peptide (Id, MonoisotopicMass, MolecularWeight, DecoySequence)\n"
    "    SELECT AfterMergePeptide, MonoisotopicMass, MolecularWeight, DecoySequence\n"
    "    FROM %1%.Peptide newPep\n"
    "    JOIN PeptideInstanceMergeMap ON newPep.Id = BeforeMergePeptide\n"
    "    WHERE AfterMergePeptide > %2%\n"
    "    GROUP BY AfterMergePeptide\n");

boost::format addNewSpectrumSourceGroupsSql(
    "INSERT INTO merged.SpectrumSourceGroup\n"
    "    SELECT AfterMergeId, Name\n"
    "    FROM %1%.SpectrumSourceGroup newGroup\n"
    "    JOIN SpectrumSourceGroupMergeMap ssgMerge ON Id = BeforeMergeId\n"
    "    WHERE AfterMergeId > %2%\n");

boost::format addNewSpectrumSourcesSql(
    "INSERT INTO merged.SpectrumSource\n"
    "    SELECT ssMerge.AfterMergeId, Name, URL, ssgMerge.AfterMergeId, 0, 0, 0, 0, 0\n"
    "    FROM %1%.SpectrumSource newSource\n"
    "    JOIN SpectrumSourceMergeMap ssMerge ON newSource.Id = ssMerge.BeforeMergeId\n"
    "    JOIN SpectrumSourceGroupMergeMap ssgMerge ON newSource.Group_ = ssgMerge.BeforeMergeId\n"
    "    WHERE ssMerge.AfterMergeId > %2%;\n"
    "\n"
    "INSERT INTO merged.SpectrumSourceMetadata\n"
    "    SELECT ssMerge.AfterMergeId, NULL\n"
    "    FROM %1%.SpectrumSource newSource\n"
    "    JOIN SpectrumSourceMergeMap ssMerge ON newSource.Id = ssMerge.BeforeMergeId\n"
    "    JOIN SpectrumSourceGroupMergeMap ssgMerge ON newSource.Group_ = ssgMerge.BeforeMergeId\n"
    "    WHERE ssMerge.AfterMergeId > %2%\n");

boost::format addNewSpectrumSourceGroupLinksSql(
    "INSERT INTO merged.SpectrumSourceGroupLink\n"
    "    SELECT ssglMerge.AfterMergeId, ssMerge.AfterMergeId, ssgMerge.AfterMergeId\n"
    "    FROM %1%.SpectrumSourceGroupLink newLink\n"
    "    JOIN SpectrumSourceMergeMap ssMerge ON newLink.Source = ssMerge.BeforeMergeId\n"
    "    JOIN SpectrumSourceGroupMergeMap ssgMerge ON newLink.Group_ = ssgMerge.BeforeMergeId\n"
    "    JOIN SpectrumSourceGroupLinkMergeMap ssglMerge ON newLink.Id = ssglMerge.BeforeMergeId\n"
    "    WHERE ssglMerge.AfterMergeId > %2%\n");

boost::format addNewSpectraSql(
    "INSERT INTO merged.Spectrum\n"
    "    SELECT sMerge.AfterMergeId, ssMerge.AfterMergeId, Index_, NativeID, PrecursorMZ, ScanTimeInSeconds\n"
    "    FROM %1%.Spectrum newSpectrum\n"
    "    JOIN SpectrumSourceMergeMap ssMerge ON newSpectrum.Source = ssMerge.BeforeMergeId\n"
    "    JOIN SpectrumMergeMap sMerge ON newSpectrum.Id = sMerge.BeforeMergeId\n"
    "    WHERE sMerge.AfterMergeId > %2%\n");

boost::format addNewModificationsSql(
    "INSERT INTO merged.Modification\n"
    "    SELECT AfterMergeId, MonoMassDelta, AvgMassDelta, Formula, Name\n"
    "    FROM %1%.Modification newMod\n"
    "    JOIN ModificationMergeMap modMerge ON newMod.Id = BeforeMergeId\n"
    "    WHERE AfterMergeId > %2%\n");

boost::format addNewPeptideSpectrumMatchesSql(
    "INSERT INTO merged.PeptideSpectrumMatch\n"
    "    SELECT newPSM.Id + %1%, sMerge.AfterMergeId, aMerge.AfterMergeId, AfterMergePeptide,\n"
    "           QValue, ObservedNeutralMass, MonoisotopicMassError, MolecularWeightError,\n"
    "           Rank, Charge\n"
    "    FROM %2%.PeptideSpectrumMatch newPSM\n"
    "    JOIN PeptideInstanceMergeMap piMerge ON Peptide = BeforeMergePeptide\n"
    "    JOIN AnalysisMergeMap aMerge ON Analysis = aMerge.BeforeMergeId\n"
    "    JOIN SpectrumMergeMap sMerge ON Spectrum = sMerge.BeforeMergeId\n"
    "    GROUP BY newPSM.Id\n"
    "    ORDER BY newPSM.Id\n");

boost::format addNewPeptideSpectrumMatchScoreNamesSql(
    "INSERT INTO merged.PeptideSpectrumMatchScoreName\n"
    "    SELECT AfterMergeId, newName.Name\n"
    "    FROM %1%.PeptideSpectrumMatchScoreName newName\n"
    "    JOIN PeptideSpectrumMatchScoreNameMergeMap nameMerge ON newName.Id = BeforeMergeId\n"
    "    WHERE AfterMergeId > %2%\n");

boost::format addNewPeptideSpectrumMatchScoresSql(
    "INSERT INTO merged.PeptideSpectrumMatchScore\n"
    "    SELECT PsmId + %1%, Value, AfterMergeId\n"
    "    FROM %2%.PeptideSpectrumMatchScore newScore\n"
    "    JOIN PeptideSpectrumMatchScoreNameMergeMap nameMerge ON newScore.ScoreNameId = BeforeMergeId\n");

boost::format addNewPeptideModificationsSql(
    "INSERT INTO merged.PeptideModification\n"
    "    SELECT Id + %1%, PeptideSpectrumMatch + %2%, AfterMergeId, Offset, Site\n"
    "    FROM %3%.PeptideModification newPM\n"
    "    JOIN ModificationMergeMap modMerge ON Modification = BeforeMergeId\n");

boost::format addNewAnalysesSql(
    "INSERT INTO merged.Analysis\n"
    "    SELECT AfterMergeId, Name, SoftwareName, SoftwareVersion, Type, StartTime\n"
    "    FROM %1%.Analysis newAnalysis\n"
    "    JOIN AnalysisMergeMap aMerge ON Id = BeforeMergeId\n"
    "    WHERE AfterMergeId > %2%\n");

boost::format addNewAnalysisParametersSql(
    "INSERT INTO merged.AnalysisParameter (Analysis, Name, Value)\n"
    "    SELECT AfterMergeId, Name, Value\n"
    "    FROM %1%.AnalysisParameter newAP\n"
    "    JOIN AnalysisMergeMap aMerge ON Analysis = BeforeMergeId\n"
    "    WHERE AfterMergeId > %2%\n");

boost::format addNewQonverterSettingsSql(
    "INSERT INTO merged.QonverterSettings\n"
    "    SELECT AfterMergeId, QonverterMethod, DecoyPrefix, RerankMatches, Kernel, MassErrorHandling, MissedCleavagesHandling, TerminalSpecificityHandling, ChargeStateHandling, ScoreInfoByName\n"
    "    FROM %1%.QonverterSettings newQS\n"
    "    JOIN AnalysisMergeMap aMerge ON Id = BeforeMergeId\n"
    "    WHERE AfterMergeId > %2%\n");

boost::format getNewMaxIdsSql(
    "SELECT (SELECT IFNULL(MAX(Id), 0) FROM %1%.Protein),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.PeptideInstance),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.Peptide),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.PeptideSpectrumMatch),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.PeptideSpectrumMatchScoreName),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.PeptideModification),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.Modification),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.SpectrumSourceGroup),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.SpectrumSource),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.SpectrumSourceGroupLink),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.Spectrum),\n"
    "       (SELECT IFNULL(MAX(Id), 0) FROM %1%.Analysis)\n");


class TemporaryFile
{
    public:
    TemporaryFile(const string& extension = ".tmp")
    {
        filepath = bfs::unique_path("%%%%%%%%%%%%%%%%" + extension);
    }

    ~TemporaryFile()
    {
        if (bfs::exists(filepath))
            bfs::remove(filepath);
    }

    const bfs::path& path() const { return filepath; }

    private:
    bfs::path filepath;
};


} // namespace


BEGIN_IDPICKER_NAMESPACE


struct Merger::Impl
{
    // page_size = 32768
    static int tempCacheSize;
    static int mergedCacheSize;
    static int newCacheSize;

    /// Merge one or more idpDBs into a target idpDB.
    Impl(const string& mergeTargetFilepath, const vector<string>& mergeSourceFilepaths)
        : tempMergeTargetFile(".idpDB")
    {
        this->mergeTargetFilepath = mergeTargetFilepath;
        this->mergeSourceFilepaths = mergeSourceFilepaths;
        this->mergeSourceConnection = NULL;
        totalSourceFiles = mergeSourceFilepaths.size();

        initializeSqlFormats();
    }

    /// Merge an idpDB connection (either file or in-memory) to a target idpDB file.
    Impl(const string& mergeTargetFilepath, sqlite3* mergeSourceConnection)
        : tempMergeTargetFile(".idpDB")
    {
        this->mergeTargetFilepath = mergeTargetFilepath;
        this->mergeSourceConnection = mergeSourceConnection;

        initializeSqlFormats();
    }

    void merge(pwiz::util::IterationListenerRegistry* ilr = 0)
    {
        this->ilr = ilr;

        if (mergeSourceConnection != NULL)
            mergeConnection(mergeSourceConnection);
        else
            mergeFiles();
    }

    private:

    boost::format mergeProteinsSql;
    boost::format mergePeptideInstancesSql;
    boost::format mergeAnalysesSql;
    boost::format mergeSpectrumSourceGroupsSql;
    boost::format mergeSpectrumSourcesSql;
    boost::format mergeSpectrumSourceGroupLinksSql;
    boost::format mergeSpectraSql;
    boost::format mergeModificationsSql;
    boost::format mergePeptideSpectrumMatchScoreNamesSql;
    boost::format addNewProteinsSql;
    boost::format addNewPeptideInstancesSql;
    boost::format addNewPeptidesSql;
    boost::format addNewSpectrumSourceGroupsSql;
    boost::format addNewSpectrumSourcesSql;
    boost::format addNewSpectrumSourceGroupLinksSql;
    boost::format addNewSpectraSql;
    boost::format addNewModificationsSql;
    boost::format addNewPeptideSpectrumMatchesSql;
    boost::format addNewPeptideSpectrumMatchScoreNamesSql;
    boost::format addNewPeptideSpectrumMatchScoresSql;
    boost::format addNewPeptideModificationsSql;
    boost::format addNewAnalysesSql;
    boost::format addNewAnalysisParametersSql;
    boost::format addNewQonverterSettingsSql;
    boost::format getNewMaxIdsSql;

    void initializeSqlFormats()
    {
        mergeProteinsSql = ::mergeProteinsSql;
        mergePeptideInstancesSql = ::mergePeptideInstancesSql;
        mergeAnalysesSql = ::mergeAnalysesSql;
        mergeSpectrumSourceGroupsSql = ::mergeSpectrumSourceGroupsSql;
        mergeSpectrumSourcesSql = ::mergeSpectrumSourcesSql;
        mergeSpectrumSourceGroupLinksSql = ::mergeSpectrumSourceGroupLinksSql;
        mergeSpectraSql = ::mergeSpectraSql;
        mergeModificationsSql = ::mergeModificationsSql;
        mergePeptideSpectrumMatchScoreNamesSql = ::mergePeptideSpectrumMatchScoreNamesSql;
        addNewProteinsSql = ::addNewProteinsSql;
        addNewPeptideInstancesSql = ::addNewPeptideInstancesSql;
        addNewPeptidesSql = ::addNewPeptidesSql;
        addNewSpectrumSourceGroupsSql = ::addNewSpectrumSourceGroupsSql;
        addNewSpectrumSourcesSql = ::addNewSpectrumSourcesSql;
        addNewSpectrumSourceGroupLinksSql = ::addNewSpectrumSourceGroupLinksSql;
        addNewSpectraSql = ::addNewSpectraSql;
        addNewModificationsSql = ::addNewModificationsSql;
        addNewPeptideSpectrumMatchesSql = ::addNewPeptideSpectrumMatchesSql;
        addNewPeptideSpectrumMatchScoreNamesSql = ::addNewPeptideSpectrumMatchScoreNamesSql;
        addNewPeptideSpectrumMatchScoresSql = ::addNewPeptideSpectrumMatchScoresSql;
        addNewPeptideModificationsSql = ::addNewPeptideModificationsSql;
        addNewAnalysesSql = ::addNewAnalysesSql;
        addNewAnalysisParametersSql = ::addNewAnalysisParametersSql;
        addNewQonverterSettingsSql = ::addNewQonverterSettingsSql;
        getNewMaxIdsSql = ::getNewMaxIdsSql;
    }

    void clearSqlFormats()
    {
        mergeProteinsSql.clear_binds();
        mergePeptideInstancesSql.clear_binds();
        mergeAnalysesSql.clear_binds();
        mergeSpectrumSourceGroupsSql.clear_binds();
        mergeSpectrumSourcesSql.clear_binds();
        mergeSpectrumSourceGroupLinksSql.clear_binds();
        mergeSpectraSql.clear_binds();
        mergeModificationsSql.clear_binds();
        mergePeptideSpectrumMatchScoreNamesSql.clear_binds();
        addNewProteinsSql.clear_binds();
        addNewPeptideInstancesSql.clear_binds();
        addNewPeptidesSql.clear_binds();
        addNewSpectrumSourceGroupsSql.clear_binds();
        addNewSpectrumSourcesSql.clear_binds();
        addNewSpectrumSourceGroupLinksSql.clear_binds();
        addNewSpectraSql.clear_binds();
        addNewModificationsSql.clear_binds();
        addNewPeptideSpectrumMatchesSql.clear_binds();
        addNewPeptideSpectrumMatchScoreNamesSql.clear_binds();
        addNewPeptideSpectrumMatchScoresSql.clear_binds();
        addNewPeptideModificationsSql.clear_binds();
        addNewAnalysesSql.clear_binds();
        addNewAnalysisParametersSql.clear_binds();
        addNewQonverterSettingsSql.clear_binds();
        getNewMaxIdsSql.clear_binds();
    }

    static void precacheFile(const string& filepath)
    {
        if (!bfs::exists(filepath))
            return;

        char buf[32768];

        FILE* f = fopen(filepath.c_str(), "r");
        while (fread(&buf, 32768, 1, f) > 0) {}
        fclose(f);
    }

    void initializeTarget(sqlite3pp::database& db)
    {
        tempMergeTargetFilepath = mergeTargetFilepath;
        // for non-fixed drives, merge to a temporary file
        if (!isPathOnFixedDrive(mergeTargetFilepath))
            tempMergeTargetFilepath = tempMergeTargetFile.path().string();
        else
            tempMergeTargetFilepath = mergeTargetFilepath;

        if (!SchemaUpdater::isValidFile(mergeTargetFilepath))
        {
            // if the target doesn't exist, the biggest source is copied to the target
            if (bfs::exists(mergeTargetFilepath)) bfs::remove(mergeTargetFilepath);
            if (bfs::exists(tempMergeTargetFilepath)) bfs::remove(tempMergeTargetFilepath);

            if (!mergeSourceFilepaths.empty())
            {
                uintmax_t biggestFilesize = 0;
                string biggestSourceFilepath;
                BOOST_FOREACH(const string& filepath, mergeSourceFilepaths)
                {
                    uintmax_t currentFilesize = bfs::file_size(filepath);
                    if (currentFilesize > biggestFilesize)
                    {
                        biggestFilesize = currentFilesize;
                        biggestSourceFilepath = filepath;
                    }
                }
                bfs::copy_file(biggestSourceFilepath, tempMergeTargetFilepath);
                mergeSourceFilepaths.erase(std::find(mergeSourceFilepaths.begin(), mergeSourceFilepaths.end(), biggestSourceFilepath));
            }
        }
        else if (tempMergeTargetFilepath != mergeTargetFilepath)
        {
            // if the target does exist on a non-fixed drive, copy it to the temporary file
            if (bfs::exists(tempMergeTargetFilepath)) bfs::remove(tempMergeTargetFilepath);
            bfs::copy_file(mergeTargetFilepath, tempMergeTargetFilepath);
        }

        // update target database schema
        SchemaUpdater::update(tempMergeTargetFilepath, ilr);

        Qonverter::dropFilters(tempMergeTargetFilepath);

        string sqliteSafeMergeTargetFilepath = bal::replace_all_copy(tempMergeTargetFilepath, "'", "''");

        db.execute("ATTACH DATABASE '" + sqliteSafeMergeTargetFilepath + "' AS merged");
        db.executef("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA page_size=32768; PRAGMA cache_size=%d", tempCacheSize);
        db.executef("PRAGMA merged.journal_mode=OFF; PRAGMA merged.synchronous=OFF; PRAGMA merged.cache_size=%d", mergedCacheSize);

        db.execute("CREATE TABLE IF NOT EXISTS merged.MergedFiles (Filepath TEXT PRIMARY KEY)");
        db.execute("UPDATE About SET SoftwareVersion = '" + string("3.0.0") + "', StartTime = datetime('now')");

        string sql = (getNewMaxIdsSql % "merged").str();
        sqlite3pp::query maxIdRowQuery(db, sql.c_str());
        sqlite3pp::query::rows maxIdRow = *maxIdRowQuery.begin();
        MaxProteinId = maxIdRow.get<sqlite3_int64>(0);
        MaxPeptideInstanceId = maxIdRow.get<sqlite3_int64>(1);
        MaxPeptideId = maxIdRow.get<sqlite3_int64>(2);
        MaxPeptideSpectrumMatchId = maxIdRow.get<sqlite3_int64>(3);
        MaxPeptideSpectrumMatchScoreNameId = maxIdRow.get<sqlite3_int64>(4);
        MaxPeptideModificationId = maxIdRow.get<sqlite3_int64>(5);
        MaxModificationId = maxIdRow.get<sqlite3_int64>(6);
        MaxSpectrumSourceGroupId = maxIdRow.get<sqlite3_int64>(7);
        MaxSpectrumSourceId = maxIdRow.get<sqlite3_int64>(8);
        MaxSpectrumSourceGroupLinkId = maxIdRow.get<sqlite3_int64>(9);
        MaxSpectrumId = maxIdRow.get<sqlite3_int64>(10);
        MaxAnalysisId = maxIdRow.get<sqlite3_int64>(11);
    }

    void mergeFiles()
    {
        sqlite3pp::database inMemoryDb(":memory:");
        //if (OnMergingProgress(null, 0))
        //    return;

        mergeSourceDatabase = "new";

        initializeTarget(inMemoryDb);

        BOOST_FOREACH (const string& mergeSourceFilepath, mergeSourceFilepaths)
        {
            //if (OnMergingProgress(null, ++mergedFiles))
            //    return;

            if (!bfs::exists(mergeSourceFilepath) || mergeSourceFilepath == mergeTargetFilepath)
                continue;

            string sqliteSafeMergeSourceFilepath = bal::replace_all_copy(mergeSourceFilepath, "'", "''");

            // skip files that have already been merged
            if (sqlite3pp::query(inMemoryDb, ("SELECT COUNT(*) FROM merged.MergedFiles WHERE Filepath = '" + sqliteSafeMergeSourceFilepath + "'").c_str()).begin()->get<sqlite3_int64>(0) > 0)
                continue;

            // for non-fixed drives, copy source to a temporary file
            TemporaryFile tempMergeSourceFile(".idpDB");
            string tempMergeSourceFilepath;
            if (!isPathOnFixedDrive(mergeSourceFilepath))
            {
                tempMergeSourceFilepath = tempMergeSourceFile.path().string();
                bfs::copy_file(mergeSourceFilepath, tempMergeSourceFilepath);
            }
            else
                tempMergeSourceFilepath = mergeSourceFilepath;

            string sqliteSafeTempMergeSourceFilepath = bal::replace_all_copy(tempMergeSourceFilepath, "'", "''");

            precacheFile(tempMergeSourceFilepath);

            // update source database schema
            SchemaUpdater::update(tempMergeSourceFilepath, ilr);

            // drop source database's basic data filters
            Qonverter::dropFilters(tempMergeSourceFilepath);

            inMemoryDb.execute("ATTACH DATABASE '" + sqliteSafeTempMergeSourceFilepath + "' AS new");
            inMemoryDb.execute("PRAGMA new.cache_size=" + lexical_cast<string>(newCacheSize));

            sqlite3pp::transaction transaction(inMemoryDb);

            try
            {
                mergeProteins(inMemoryDb);
                mergePeptideInstances(inMemoryDb);
                mergeSpectrumSourceGroups(inMemoryDb);
                mergeSpectrumSources(inMemoryDb);
                mergeSpectrumSourceGroupLinks(inMemoryDb);
                mergeSpectra(inMemoryDb);
                mergeModifications(inMemoryDb);
                mergePeptideSpectrumMatchScoreNames(inMemoryDb);
                mergeAnalyses(inMemoryDb);
                addNewProteins(inMemoryDb);
                addNewPeptideInstances(inMemoryDb);
                addNewPeptides(inMemoryDb);
                addNewModifications(inMemoryDb);
                addNewSpectrumSourceGroups(inMemoryDb);
                addNewSpectrumSources(inMemoryDb);
                addNewSpectrumSourceGroupLinks(inMemoryDb);
                addNewSpectra(inMemoryDb);
                addNewPeptideSpectrumMatches(inMemoryDb);
                addNewPeptideSpectrumMatchScoreNames(inMemoryDb);
                addNewPeptideSpectrumMatchScores(inMemoryDb);
                addNewPeptideModifications(inMemoryDb);
                addNewAnalyses(inMemoryDb);
                addNewAnalysisParameters(inMemoryDb);
                addNewQonverterSettings(inMemoryDb);

                inMemoryDb.execute("INSERT INTO merged.MergedFiles VALUES ('" + sqliteSafeMergeSourceFilepath + "')");

                clearSqlFormats();
            }
            catch (runtime_error& e)
            {
                //if (OnMergingProgress(new Exception("Error merging " + mergeSourceFilepath, e), mergedFiles))
                //    break;
                throw runtime_error("Error merging " + mergeSourceFilepath + ": " + e.what());
            }

            getNewMaxIds(inMemoryDb);
            transaction.commit();
            inMemoryDb.execute("DETACH DATABASE new");
        }

        sqlite3pp::transaction transaction(inMemoryDb);

        addIntegerSet(inMemoryDb);
        inMemoryDb.execute("UPDATE SpectrumSourceMetadata SET MsDataBytes = NULL");
        deleteEmptySpectrumSourceGroups(inMemoryDb);

        transaction.commit();

        inMemoryDb.execute("DETACH DATABASE merged");

        // if merging to a temporary file, copy it back to the real target; TemporaryFile dtor will remove temporary file
        if (tempMergeTargetFilepath != mergeTargetFilepath)
            bfs::copy_file(tempMergeTargetFilepath, mergeTargetFilepath);
    }

    void mergeConnection(sqlite3* conn)
    {
        sqlite3pp::database db(conn, false);

        mergeSourceDatabase = "main";

        initializeTarget(db);

        sqlite3pp::transaction transaction(db);
        try
        {
            mergeProteins(db);
            mergePeptideInstances(db);
            mergeSpectrumSourceGroups(db);
            mergeSpectrumSources(db);
            mergeSpectrumSourceGroupLinks(db);
            mergeSpectra(db);
            mergeModifications(db);
            mergePeptideSpectrumMatchScoreNames(db);
            mergeAnalyses(db);
            addNewProteins(db);
            addNewPeptideInstances(db);
            addNewPeptides(db);
            addNewModifications(db);
            addNewSpectrumSourceGroups(db);
            addNewSpectrumSources(db);
            addNewSpectrumSourceGroupLinks(db);
            addNewSpectra(db);
            addNewPeptideSpectrumMatches(db);
            addNewPeptideSpectrumMatchScoreNames(db);
            addNewPeptideSpectrumMatchScores(db);
            addNewPeptideModifications(db);
            addNewAnalyses(db);
            addNewAnalysisParameters(db);
            addNewQonverterSettings(db);
            addIntegerSet(db);
            db.execute("UPDATE SpectrumSourceMetadata SET MsDataBytes = NULL");

            deleteEmptySpectrumSourceGroups(db);

            db.execute("DETACH DATABASE merged");
        }
        catch (runtime_error& e)
        {
            //OnMergingProgress(ex, 1);
            throw runtime_error(string("Error merging connection: ") + e.what());
        }

        // if merging to a temporary file, copy it back to the real target; TemporaryFile dtor will remove temporary file
        if (tempMergeTargetFilepath != mergeTargetFilepath)
            bfs::copy_file(tempMergeTargetFilepath, mergeTargetFilepath);
    }

    void mergeProteins(sqlite3pp::database& db) { db.execute((mergeProteinsSql % MaxProteinId % mergeSourceDatabase).str()); }
    void mergePeptideInstances(sqlite3pp::database& db) { db.execute((mergePeptideInstancesSql % MaxPeptideInstanceId % MaxPeptideId % mergeSourceDatabase).str()); }
    void mergeAnalyses(sqlite3pp::database& db) { db.execute((mergeAnalysesSql % MaxAnalysisId % mergeSourceDatabase).str()); }
    void mergeSpectrumSourceGroups(sqlite3pp::database& db) { db.execute((mergeSpectrumSourceGroupsSql % MaxSpectrumSourceGroupId % mergeSourceDatabase).str()); }
    void mergeSpectrumSources(sqlite3pp::database& db) { db.execute((mergeSpectrumSourcesSql % MaxSpectrumSourceId % mergeSourceDatabase).str()); }
    void mergeSpectrumSourceGroupLinks(sqlite3pp::database& db) { db.execute((mergeSpectrumSourceGroupLinksSql % MaxSpectrumSourceGroupLinkId % mergeSourceDatabase).str()); }
    void mergeSpectra(sqlite3pp::database& db) { db.execute((mergeSpectraSql % MaxSpectrumId % mergeSourceDatabase).str()); }
    void mergeModifications(sqlite3pp::database& db) { db.execute((mergeModificationsSql % MaxModificationId % mergeSourceDatabase).str()); }
    void mergePeptideSpectrumMatchScoreNames(sqlite3pp::database& db) { db.execute((mergePeptideSpectrumMatchScoreNamesSql% MaxPeptideSpectrumMatchScoreNameId % mergeSourceDatabase).str()); }
    void addNewProteins(sqlite3pp::database& db) { db.execute((addNewProteinsSql % mergeSourceDatabase).str()); }
    void addNewPeptideInstances(sqlite3pp::database& db) { db.execute((addNewPeptideInstancesSql % mergeSourceDatabase % MaxPeptideInstanceId).str()); }
    void addNewPeptides(sqlite3pp::database& db) { db.execute((addNewPeptidesSql % mergeSourceDatabase % MaxPeptideId).str()); }
    void addNewSpectrumSourceGroups(sqlite3pp::database& db) { db.execute((addNewSpectrumSourceGroupsSql % mergeSourceDatabase % MaxSpectrumSourceGroupId).str()); }
    void addNewSpectrumSources(sqlite3pp::database& db) { db.execute((addNewSpectrumSourcesSql % mergeSourceDatabase % MaxSpectrumSourceId).str()); }
    void addNewSpectrumSourceGroupLinks(sqlite3pp::database& db) { db.execute((addNewSpectrumSourceGroupLinksSql % mergeSourceDatabase % MaxSpectrumSourceGroupLinkId).str()); }
    void addNewSpectra(sqlite3pp::database& db) { db.execute((addNewSpectraSql % mergeSourceDatabase % MaxSpectrumId).str()); }
    void addNewModifications(sqlite3pp::database& db) { db.execute((addNewModificationsSql % mergeSourceDatabase % MaxModificationId).str()); }
    void addNewPeptideSpectrumMatches(sqlite3pp::database& db) { db.execute((addNewPeptideSpectrumMatchesSql % MaxPeptideSpectrumMatchId % mergeSourceDatabase).str()); }
    void addNewPeptideSpectrumMatchScoreNames(sqlite3pp::database& db) { db.execute((addNewPeptideSpectrumMatchScoreNamesSql % mergeSourceDatabase % MaxPeptideSpectrumMatchScoreNameId).str()); }
    void addNewPeptideSpectrumMatchScores(sqlite3pp::database& db) { db.execute((addNewPeptideSpectrumMatchScoresSql % MaxPeptideSpectrumMatchId % mergeSourceDatabase).str()); }
    void addNewPeptideModifications(sqlite3pp::database& db) { db.execute((addNewPeptideModificationsSql% MaxPeptideModificationId % MaxPeptideSpectrumMatchId % mergeSourceDatabase).str()); }
    void addNewAnalyses(sqlite3pp::database& db) { db.execute((addNewAnalysesSql % mergeSourceDatabase % MaxAnalysisId).str()); }
    void addNewAnalysisParameters(sqlite3pp::database& db) { db.execute((addNewAnalysisParametersSql % mergeSourceDatabase % MaxAnalysisId).str()); }
    void addNewQonverterSettings(sqlite3pp::database& db) { db.execute((addNewQonverterSettingsSql % mergeSourceDatabase % MaxAnalysisId).str()); }

    void getNewMaxIds(sqlite3pp::database& db)
    {
        string sql = (getNewMaxIdsSql % mergeSourceDatabase).str();
        sqlite3pp::query maxIdRowQuery(db, sql.c_str());
        sqlite3pp::query::rows maxIdRow = *maxIdRowQuery.begin();
        MaxProteinId += maxIdRow.get<sqlite3_int64>(0);
        MaxPeptideInstanceId += maxIdRow.get<sqlite3_int64>(1);
        MaxPeptideId += maxIdRow.get<sqlite3_int64>(2);
        MaxPeptideSpectrumMatchId += maxIdRow.get<sqlite3_int64>(3);
        MaxPeptideSpectrumMatchScoreNameId += maxIdRow.get<sqlite3_int64>(4);
        MaxPeptideModificationId += maxIdRow.get<sqlite3_int64>(5);
        MaxModificationId += maxIdRow.get<sqlite3_int64>(6);
        MaxSpectrumSourceGroupId += maxIdRow.get<sqlite3_int64>(7);
        MaxSpectrumSourceId += maxIdRow.get<sqlite3_int64>(8);
        MaxSpectrumSourceGroupLinkId += maxIdRow.get<sqlite3_int64>(9);
        MaxSpectrumId += maxIdRow.get<sqlite3_int64>(10);
        MaxAnalysisId += maxIdRow.get<sqlite3_int64>(11);
    }

    static void addIntegerSet(sqlite3pp::database& db)
    {
        sqlite3_int64 maxInteger = sqlite3pp::query(db, "SELECT IFNULL(MAX(Value),0) FROM IntegerSet").begin()->get<sqlite3_int64>(0);
        sqlite3_int64 maxProteinLength = sqlite3pp::query(db, "SELECT IFNULL(MAX(LENGTH(Sequence)),0) FROM ProteinData").begin()->get<sqlite3_int64>(0);

        sqlite3pp::command cmd(db, "INSERT INTO IntegerSet VALUES (?)");

        for (sqlite3_int64 i = maxInteger + 1; i <= maxProteinLength; ++i)
        {
            cmd.bind(1, i);
            cmd.execute();
            cmd.reset();
        }
    }

    static void deleteEmptySpectrumSourceGroups(sqlite3pp::database& db)
    {
        db.execute("DELETE FROM SpectrumSourceGroup\n"
                   "WHERE Id NOT IN (SELECT ssg.Id\n"
                   "                 FROM SpectrumSourceGroup ssg\n"
                   "                 JOIN(SELECT DISTINCT ssg.Name FROM SpectrumSourceGroup ssg JOIN SpectrumSource ss ON ssg.Id = ss.Group_) groupsWithNoDirectSources\n"
                   "                   ON instr(groupsWithNoDirectSources.Name || '/', ssg.Name || '/') != 0\n"
                   "                   OR ssg.Name = '/')");
        db.execute("DELETE FROM SpectrumSourceGroupLink WHERE Group_ NOT IN (SELECT Id FROM SpectrumSourceGroup)");
    }

    IterationListenerRegistry* ilr;

    int totalSourceFiles;
    string mergeTargetFilepath;
    string tempMergeTargetFilepath;
    TemporaryFile tempMergeTargetFile;
    vector<string> mergeSourceFilepaths;
    sqlite3* mergeSourceConnection;
    string mergeSourceDatabase;

    sqlite3_int64 MaxProteinId;
    sqlite3_int64 MaxPeptideInstanceId;
    sqlite3_int64 MaxPeptideId;
    sqlite3_int64 MaxPeptideSpectrumMatchId;
    sqlite3_int64 MaxPeptideSpectrumMatchScoreNameId;
    sqlite3_int64 MaxPeptideModificationId;
    sqlite3_int64 MaxModificationId;
    sqlite3_int64 MaxSpectrumSourceGroupId;
    sqlite3_int64 MaxSpectrumSourceId;
    sqlite3_int64 MaxSpectrumSourceGroupLinkId;
    sqlite3_int64 MaxSpectrumId;
    sqlite3_int64 MaxAnalysisId;
};

int Merger::Impl::tempCacheSize = 10000; // 328 MB
int Merger::Impl::mergedCacheSize = 30000; // 1 GB
int Merger::Impl::newCacheSize = 20000; // 655 MB


Merger::Merger()
{}

Merger::~Merger()
{}


struct MergeTask
{
    MergeTask() {};
    MergeTask(const string& sourceFilepath, bool isTemporary) : mergeSourceFilepath(sourceFilepath), isTemporary(isTemporary) {}
    ~MergeTask()
    {
        if (isTemporary && bfs::exists(mergeSourceFilepath))
            bfs::remove(mergeSourceFilepath);
    }

    string mergeSourceFilepath;
    bool isTemporary;
};

struct ThreadStatus
{
    bool userCanceled;
    boost::exception_ptr exception;

    ThreadStatus() : userCanceled(false) {}
    ThreadStatus(IterationListener::Status status) : userCanceled(status == IterationListener::Status_Cancel) {}
    ThreadStatus(const boost::exception_ptr& e) : userCanceled(false), exception(e) {}
};

void executePairwiseFileMergerTask(std::deque<shared_ptr<MergeTask> >& sourceQueue, ThreadStatus& status, boost::mutex& queueMutex, size_t& filesMerged, size_t& filesTotal)
{
    vector<shared_ptr<MergeTask> > mergeTasks(2);
    vector<string> sourceFilepaths(2);

    try
    {
        while (true)
        {
            string tempMergeTargetFilepath;
            bool newTemporaryCreated = false;

            // pop two sources from the queue; return if the queue only has one source
            {
                boost::lock_guard<boost::mutex> lock(queueMutex);
                if (sourceQueue.size() > 1)
                {
                    mergeTasks[0] = sourceQueue.front(); sourceQueue.pop_front();
                    mergeTasks[1] = sourceQueue.front(); sourceQueue.pop_front();
                }
                else
                    return;
            }

            if (!mergeTasks[0]->isTemporary && !mergeTasks[1]->isTemporary)
            {
                sourceFilepaths.resize(2);
                sourceFilepaths[0] = mergeTasks[0]->mergeSourceFilepath;
                sourceFilepaths[1] = mergeTasks[1]->mergeSourceFilepath;
                tempMergeTargetFilepath = (bfs::temp_directory_path() / bfs::unique_path("%%%%%%%%%%%%%%%%.idpDB")).string();
                newTemporaryCreated = true;
            }
            else if (mergeTasks[0]->isTemporary)
            {
                sourceFilepaths.resize(1);
                sourceFilepaths[0] = mergeTasks[1]->mergeSourceFilepath;
                tempMergeTargetFilepath = mergeTasks[0]->mergeSourceFilepath;
            }
            else // mergeTasks[1]->isTemporary
            {
                sourceFilepaths.resize(1);
                sourceFilepaths[0] = mergeTasks[0]->mergeSourceFilepath;
                tempMergeTargetFilepath = mergeTasks[1]->mergeSourceFilepath;
            }

            /*{
                boost::lock_guard<boost::mutex> lock(queueMutex);
                cout << "Thread " << boost::this_thread::get_id() << " starts merging " << bal::join(sourceFilepaths, " and ") << " to " << tempMergeTargetFilepath << endl;
            }*/

            Merger::Impl impl(tempMergeTargetFilepath, sourceFilepaths);
            impl.merge();

            /*{
                boost::lock_guard<boost::mutex> lock(queueMutex);
                cout << "Thread " << boost::this_thread::get_id() << " finished merging " << bal::join(sourceFilepaths, " and ") << " to " << tempMergeTargetFilepath << endl;
            }*/

            // add the merged target to the queue
            {
                boost::lock_guard<boost::mutex> lock(queueMutex);

                if (newTemporaryCreated)
                {
                    ++filesTotal; // the new temporary file is another file that has to be merged
                    filesMerged += 2;
                }
                else
                    ++filesMerged;

                if (newTemporaryCreated)
                    sourceQueue.push_front(boost::make_shared<MergeTask>(tempMergeTargetFilepath, true));
                else if (mergeTasks[0]->isTemporary)
                    sourceQueue.push_front(mergeTasks[0]);
                else // mergeTasks[1]->isTemporary
                    sourceQueue.push_front(mergeTasks[1]);
            }
        }
    }
    catch (exception& e)
    {
        cerr << "[executePairwiseFileMergerTask] " << boost::this_thread::get_id() << " error merging \"" + sourceFilepaths[0] + "\" and \"" + sourceFilepaths[1] + "\": " + e.what() << endl;
        status = boost::copy_exception(runtime_error("[executePairwiseFileMergerTask] error merging \"" + sourceFilepaths[0] + "\" and \"" + sourceFilepaths[1] + "\": " + e.what()));
    }
    catch (...)
    {
        status = boost::copy_exception(runtime_error("[executePairwiseFileMergerTask] unknown error merging \"" + sourceFilepaths[0] + "\" and \"" + sourceFilepaths[1] + "\""));
    }
}

void Merger::merge(const string& mergeTargetFilepath, const std::vector<string>& mergeSourceFilepaths, int maxThreads, pwiz::util::IterationListenerRegistry* ilr)
{
    // create a worker thread for each processor, up to maxThreads
    // each worker thread will consume 2 random source filepaths and merge them to a temporary filepath
    // the temporary filepath is added back to the source filepaths queue
    // when there is only one filepath left, the merge to the target is done

    vector<size_t> randomSources;
    for (size_t i = 0; i < mergeSourceFilepaths.size(); ++i)
        randomSources.push_back(i);
    std::random_shuffle(randomSources.begin(), randomSources.end());

    std::deque<shared_ptr<MergeTask> > sourceQueue;
    BOOST_FOREACH(int randomSource, randomSources)
        sourceQueue.push_back(boost::make_shared<MergeTask>(mergeSourceFilepaths[randomSource], false));

    using boost::thread;

    int processorCount = min(maxThreads, (int)boost::thread::hardware_concurrency());

    size_t filesTotal = mergeSourceFilepaths.size();
    size_t filesMerged = 0;

    // prevent boost::path::codecvt facet initialization race condition
    bfs::unique_path("%%%%%%%%%%%%%%%%.idpDB");

    // use list so iterators and references stay valid
    list<pair<boost::shared_ptr<thread>, ThreadStatus> > threads;
    boost::mutex queueMutex;
    for (int i = 0; i < processorCount; ++i)
    {
        threads.push_back(make_pair(boost::shared_ptr<thread>(), IterationListener::Status_Ok));
        threads.back().first.reset(new thread(executePairwiseFileMergerTask, boost::ref(sourceQueue), boost::ref(threads.back().second), boost::ref(queueMutex), boost::ref(filesMerged), boost::ref(filesTotal)));
    }

    try
    {
        set<boost::shared_ptr<thread> > finishedThreads;
        while (finishedThreads.size() < threads.size())
            BOOST_FOREACH_FIELD((boost::shared_ptr<thread>& t)(ThreadStatus& status), threads)
            {
                if (t->timed_join(boost::posix_time::seconds(1)))
                    finishedThreads.insert(t);

                ITERATION_UPDATE(ilr, filesMerged, filesTotal, "merging")

                if (status.exception)
                    boost::rethrow_exception(status.exception);
                else if (status.userCanceled)
                    return;
            }

        if (sourceQueue.size() > 1)
            throw runtime_error("[Merger::merge] there is more than one file left in the queue: something went wrong with the multi-threaded merge");

        sourceQueue.front()->isTemporary = false;
        bfs::rename(sourceQueue.front()->mergeSourceFilepath, mergeTargetFilepath);
    }
    catch (cancellation_exception&)
    {
        // clear the queue so the threads will exit after their current merge
        boost::mutex::scoped_lock lock(queueMutex);
        sourceQueue.clear();
    }

    //_impl.reset(new Impl(mergeTargetFilepath, mergeSourceFilepaths));
    //_impl->merge();
}

void Merger::merge(const string& mergeTargetFilepath, sqlite3* mergeSourceConnection, pwiz::util::IterationListenerRegistry* ilr)
{
    _impl.reset(new Impl(mergeTargetFilepath, mergeSourceConnection));
    _impl->merge(ilr);
}


END_IDPICKER_NAMESPACE
