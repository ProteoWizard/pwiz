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
// Contributor(s):
//


#include "sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/data/identdata/TextWriter.hpp"
#include "pwiz/data/proteome/ProteinListCache.hpp"
#include "pwiz/data/proteome/ProteomeDataFile.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/utility/misc/SHA1.h"
#include "Parser.hpp"
#include "Qonverter.hpp"
#include "SchemaUpdater.hpp"
#include "CoreVersion.hpp"
#include "AhoCorasickTrie.hpp"
#include "boost/foreach_field.hpp"
#include "boost/thread/thread.hpp"
#include "boost/thread/mutex.hpp"
#include "boost/atomic.hpp"
#include "boost/exception/all.hpp"
#include "boost/range/algorithm/set_algorithm.hpp"
#include "Logger.hpp"


using namespace pwiz::identdata;
using namespace pwiz::chemistry;
using namespace pwiz::util;
typedef IterationListener::UpdateMessage UpdateMessage;
namespace proteome = pwiz::proteome;
namespace sqlite = sqlite3pp;
using freicore::AhoCorasickTrie;
typedef boost::shared_ptr<std::string> shared_string;


BEGIN_IDPICKER_NAMESPACE


typedef boost::shared_ptr<proteome::ProteomeData> ProteomeDataPtr;
typedef Parser::Analysis Analysis;
typedef Parser::AnalysisPtr AnalysisPtr;
typedef Parser::ConstAnalysisPtr ConstAnalysisPtr;


namespace {

struct SharedStringFastLessThan
{
    bool operator() (const shared_string& lhs, const shared_string& rhs) const
    {
        if (lhs->length() == rhs->length())
            return *lhs < *rhs;
        return lhs->length() < rhs->length();
    }
};

struct AminoAcidTranslator
{
    static int size() {return 26;}
    static int translate(char aa) {return aa - 'A';};
    static char translate(int index) {return static_cast<char>(index) + 'A';}
};

typedef AhoCorasickTrie<AminoAcidTranslator> PeptideTrie;

struct IsNotAnalysisParameter
{
    bool operator() (const pair<string, string>& parameter) const
    {
        return // Bumbershoot
               bal::starts_with(parameter.first, "PeakCounts:") ||
               bal::starts_with(parameter.first, "SearchStats:") ||
               bal::starts_with(parameter.first, "SearchTime:") ||
               parameter.first == "WorkingDirectory" ||
               parameter.first == "StatusUpdateFrequency" ||
               parameter.first == "UseMultipleProcessors" ||
               bal::starts_with(parameter.first, "MaxResult") ||
               parameter.first == "OutputSuffix" ||
               parameter.first == "OutputFormat" ||
               bal::contains(parameter.first, "Batch") ||

               // Mascot
               parameter.first == "USEREMAIL" ||
               parameter.first == "USERNAME" ||
               parameter.first == "LICENSE" ||
               parameter.first == "COM" ||
               parameter.first == "FILE" ||

               // X!Tandem
               bal::starts_with(parameter.first, "modelling, ") ||
               bal::starts_with(parameter.first, "timing, ") ||
               bal::starts_with(parameter.first, "output, ") ||
               bal::starts_with(parameter.first, "process, start") ||
               parameter.first == "quality values" ||
               parameter.first == "spectrum, path";
    }
};

void parseAnalysis(const IdentDataFile& mzid, Analysis& analysis)
{
    SpectrumIdentification& si = *mzid.analysisCollection.spectrumIdentification[0];
    SpectrumIdentificationProtocol& sip = *si.spectrumIdentificationProtocolPtr;

    if (!sip.analysisSoftwarePtr.get() || sip.analysisSoftwarePtr->empty())
        throw runtime_error("no analysis software specified");

    // determine analysis software used
    CVParam searchEngine = sip.analysisSoftwarePtr->softwareName.cvParamChild(MS_analysis_software);
    if (!searchEngine.empty())
        analysis.softwareName = searchEngine.value.empty() ? searchEngine.name() : searchEngine.value;
    else if (!sip.analysisSoftwarePtr->softwareName.userParams.empty())
        analysis.softwareName = sip.analysisSoftwarePtr->softwareName.userParams[0].name;
    else
    {
        searchEngine = sip.analysisSoftwarePtr->softwareName.cvParamChild(MS_custom_unreleased_software_tool);
        if (!searchEngine.empty())
            analysis.softwareName = searchEngine.value.empty() ? searchEngine.name() : searchEngine.value;
        else if (!sip.analysisSoftwarePtr->softwareName.userParams.empty())
            analysis.softwareName = sip.analysisSoftwarePtr->softwareName.userParams[0].name;
        else
            throw runtime_error("[Parser::parseAnalysis()] analysis software could not be determined");
    }

    if (si.searchDatabase.size() > 1)
        throw runtime_error("[Parser::parseAnalysis()] multi-database protocols are not supported");

    // determine search database used
    SearchDatabase& sd = *si.searchDatabase[0];
    analysis.importSettings.proteinDatabaseFilepath = sd.location;

    // by default, unmapped peptides are an error
    analysis.importSettings.ignoreUnmappedPeptides = false;

    // trim ".pro" extension often found in X!Tandem searches
    if (bal::iends_with(analysis.importSettings.proteinDatabaseFilepath, ".pro"))
        analysis.importSettings.proteinDatabaseFilepath.resize(analysis.importSettings.proteinDatabaseFilepath.size()-4);

    analysis.enzymes = sip.enzymes;

    // flatten params from the SpectrumIdentificationProtocol into single lists
    vector<CVParam> cvParams;
    vector<UserParam> userParams;

    cvParams.insert(cvParams.end(), sip.additionalSearchParams.cvParams.begin(), sip.additionalSearchParams.cvParams.end());
    userParams.insert(userParams.end(), sip.additionalSearchParams.userParams.begin(), sip.additionalSearchParams.userParams.end());

    // fragment/parent tolerance are treated separately since they use the same CV terms
    CVParam tolerance = sip.fragmentTolerance.cvParam(MS_search_tolerance_minus_value);
    userParams.push_back(UserParam("fragment tolerance minus value", tolerance.value + " " + cvTermInfo(tolerance.units).shortName()));
    tolerance = sip.fragmentTolerance.cvParam(MS_search_tolerance_plus_value);
    userParams.push_back(UserParam("fragment tolerance plus value", tolerance.value + " " + cvTermInfo(tolerance.units).shortName()));

    tolerance = sip.parentTolerance.cvParam(MS_search_tolerance_minus_value);
    userParams.push_back(UserParam("parent tolerance minus value", tolerance.value + " " + cvTermInfo(tolerance.units).shortName()));
    tolerance = sip.parentTolerance.cvParam(MS_search_tolerance_plus_value);
    userParams.push_back(UserParam("parent tolerance plus value", tolerance.value + " " + cvTermInfo(tolerance.units).shortName()));

    cvParams.insert(cvParams.end(), sip.threshold.cvParams.begin(), sip.threshold.cvParams.end());
    userParams.insert(userParams.end(), sip.threshold.userParams.begin(), sip.threshold.userParams.end());

    for(const FilterPtr& filter : sip.databaseFilters)
    {
        cvParams.insert(cvParams.end(), filter->filterType.cvParams.begin(), filter->filterType.cvParams.end());
        cvParams.insert(cvParams.end(), filter->include.cvParams.begin(), filter->include.cvParams.end());
        cvParams.insert(cvParams.end(), filter->exclude.cvParams.begin(), filter->exclude.cvParams.end());
    }

    for(const CVParam& cvParam : cvParams)
    {
        // value-less cvParams are keyed by their parent term;
        // e.g. "param: y ion" IS_A "ions series considered in search"
        string key, value;
        if (cvParam.value.empty())
        {
            const CVTermInfo& termInfo = cvTermInfo(cvParam.cvid);
            if (termInfo.parentsIsA.empty())
                key = cvParam.name();
            else
            {
                key = cvTermInfo(termInfo.parentsIsA[0]).shortName();
                value = cvParam.name();
            }
        }
        else
        {
            key = cvParam.name();
            value = cvParam.value;
            if (cvParam.units != CVID_Unknown)
                value += " " + cvTermInfo(cvParam.units).shortName();
        }

        // if key already exists, append the value
        if (!analysis.parameters.insert(make_pair(key, value)).second)
            analysis.parameters[key] += ", " + value;
    }
    
    // TODO: change from decoy prefix notation to a more generic regex notation
    CVParam decoyRegexp = sd.cvParam(MS_decoy_DB_accession_regexp);
    if (!decoyRegexp.empty())
        analysis.parameters["DecoyPrefix"] = bal::trim_left_copy_if(decoyRegexp.value, bal::is_any_of("^"));

    // userParams are assumed to be uniquely keyed on name
    for(const UserParam& userParam : userParams)
    {
        string name = userParam.name;
        if (bal::starts_with(name, "Config: "))
            name.erase(0, 8);
        analysis.parameters[name] = userParam.value;
    }
    
    // set analysis name
    analysis.name = analysis.softwareName;

    // TODO: move the translation of the "certain parameters" into pwiz

    // set analysis software version (either from analysisSoftwarePtr or from certain parameters)
    if (!sip.analysisSoftwarePtr->version.empty())
        analysis.softwareVersion = sip.analysisSoftwarePtr->version;
    else if (analysis.parameters.count("SearchEngine: Version") > 0)
        analysis.softwareVersion = analysis.parameters["SearchEngine: Version"];

    // if possible, add software version to analysis name
    if (!analysis.softwareVersion.empty())
        analysis.name += " " + analysis.softwareVersion;

    // set analysis start time (either from activityDate or from certain parameters)
    if (!mzid.analysisCollection.spectrumIdentification[0]->activityDate.empty())
        analysis.startTime = decode_xml_datetime(mzid.analysisCollection.spectrumIdentification[0]->activityDate);
    else if (analysis.parameters.count("SearchTime: Started") > 0)
    {
        blt::local_date_time localTime = parse_date_time("%H:%M:%S on %m-%d-%Y", analysis.parameters["SearchTime: Started"]);
        analysis.startTime = blt::local_date_time(localTime.utc_time(), blt::time_zone_ptr());
    }

    vector<pair<string, string> > parameters(analysis.parameters.begin(), analysis.parameters.end());
    parameters.erase(remove_if(parameters.begin(), parameters.end(), IsNotAnalysisParameter()),
                     parameters.end());
    analysis.parameters.clear();
    analysis.parameters.insert(parameters.begin(), parameters.end());
}

template <typename key_type, typename value_type>
void map_diff(const std::map<key_type, value_type>& a,
              const std::map<key_type, value_type>& b,
              std::vector<pair<key_type, value_type> >& a_b,
              std::vector<pair<key_type, value_type> >& b_a)
{
    // calculate set differences of two maps

    a_b.clear();
    b_a.clear();

    for (typename std::map<key_type, value_type>::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (b.find(it->first) == b.end() || b.find(it->first)->second != it->second)
            a_b.push_back(*it);

    for (typename std::map<key_type, value_type>::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (a.find(it->first) == a.end() || a.find(it->first)->second != it->second)
            b_a.push_back(*it);
}

// an analysis is distinct if its name is unique and it has at least one distinct parameter
typedef map<string, AnalysisPtr> DistinctAnalysisMap;
void findDistinctAnalyses(const vector<string>& inputFilepaths,
                          DistinctAnalysisMap& distinctAnalyses,
                          bool skipSourceOnError,
                          const IterationListenerRegistry* ilr)
{
    map<string, vector<AnalysisPtr> > sameNameAnalysesByName;
    vector<pair<string, string> > a_b, b_a;
    map<string, set<string> > differingParametersByAnalysisName;

    int iterationIndex = 0;
    for(const string& filepath : inputFilepaths)
    {
        ITERATION_UPDATE(ilr, iterationIndex++, inputFilepaths.size(), "finding distinct analyses");

        try
        {
            // ignore SequenceCollection and AnalysisData
            IdentDataFile mzid(filepath, 0, 0, true);

            AnalysisPtr analysis(new Analysis);
            parseAnalysis(mzid, *analysis);

            vector<AnalysisPtr>& sameNameAnalyses = sameNameAnalysesByName[analysis->name];
            AnalysisPtr sameAnalysis;

            // take the set difference of the current analysis' parameters with every same name analysis;
            // if the set difference is empty, the analysis is not distinct
            for(AnalysisPtr& otherAnalysis : sameNameAnalyses)
            {
                map_diff(analysis->parameters, otherAnalysis->parameters, a_b, b_a);

                if (a_b.empty() && b_a.empty())
                {
                    sameAnalysis = otherAnalysis;
                    break;
                }

                BOOST_FOREACH_FIELD((const string& key)(const string& value), a_b) differingParametersByAnalysisName[otherAnalysis->name].insert(key), value;
                BOOST_FOREACH_FIELD((const string& key)(const string& value), b_a) differingParametersByAnalysisName[otherAnalysis->name].insert(key), value;
            }

            if (!sameAnalysis.get())
            {
                sameNameAnalyses.push_back(analysis);
                sameAnalysis = sameNameAnalyses.back();
            }
            sameAnalysis->filepaths.push_back(filepath);
        }
        catch (exception &e)
        {
            if (skipSourceOnError)
            {
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "parsing \"" << filepath << "\": " << e.what() << endl;
            }
            else
                throw runtime_error("parsing \"" + filepath + "\": " + e.what());
        }
    }

    typedef pair<string, vector<AnalysisPtr> > SameNameAnalysesPair;
    for(const SameNameAnalysesPair& itr : sameNameAnalysesByName)
    for(const AnalysisPtr& analysis : itr.second)
    {
        const set<string>& differingParameters = differingParametersByAnalysisName[analysis->name];
        // change the analysis names based on their values for the differing parameters
        if (!differingParameters.empty())
        {
            vector<string> differingParametersWithValues;
            for(const string& key : differingParameters)
                if (!analysis->parameters[key].empty())
                    differingParametersWithValues.push_back(key + "=" + analysis->parameters[key]);
            analysis->name += " (" + bal::join(differingParametersWithValues, ", ") + ")";
        }

        for(const string& filepath : analysis->filepaths)
            distinctAnalyses[filepath] = analysis;
    }
}


struct ParserImpl
{
    const string& inputFilepath;
    const Analysis& analysis;
    sqlite::database& idpDb;
    const IdentDataFile& mzid;
    const IterationListenerRegistry* ilr;

    map<shared_string, sqlite3_int64, SharedStringFastLessThan> distinctPeptideIdBySequence;
    map<double, sqlite3_int64> modIdByDeltaMass;

    // the set of peptides that need to be mapped (it has at least one target protein instance)
    set<shared_string> targetPeptides;

    // the set of peptides that have been mapped to at least one protein
    set<shared_string> mappedPeptides;

    ParserImpl(const string& inputFilepath,
               const Analysis& analysis,
               sqlite::database& idpDb,
               const IdentDataFile& mzid,
               const IterationListenerRegistry* ilr)
    : inputFilepath(inputFilepath),
      analysis(analysis),
      idpDb(idpDb),
      mzid(mzid),
      ilr(ilr)
    {
        initializeDatabase();
    }

    void initializeDatabase()
    {
        // optimize for bulk insertion
        idpDb.execute("PRAGMA journal_mode=OFF;"
                      "PRAGMA synchronous=OFF;"
                      "PRAGMA automatic_indexing=OFF;"
                      "PRAGMA cache_size=30000;"
                      "PRAGMA temp_store=MEMORY;"
                      "PRAGMA page_size=32768;"
                      IDPICKER_SQLITE_PRAGMA_MMAP);

        sqlite::transaction transaction(idpDb);

        // initialize the tables
        idpDb.execute("DROP TABLE IF EXISTS About;"
                      "CREATE TABLE About (Id INTEGER PRIMARY KEY, SoftwareName TEXT, SoftwareVersion TEXT, StartTime DATETIME, SchemaRevision INT);"
                      "INSERT INTO About VALUES (1, 'IDPicker', '" + IDPicker::Version::str() + "', datetime('now'), " + lexical_cast<string>(CURRENT_SCHEMA_REVISION) + ");");

        idpDb.execute("CREATE TABLE IF NOT EXISTS SpectrumSource (Id INTEGER PRIMARY KEY, Name TEXT, URL TEXT, Group_ INT, TotalSpectraMS1 INT, TotalIonCurrentMS1 NUMERIC, TotalSpectraMS2 INT, TotalIonCurrentMS2 NUMERIC, QuantitationMethod INT, QuantitationSettings TEXT);"
                      "CREATE TABLE IF NOT EXISTS SpectrumSourceMetadata (Id INTEGER PRIMARY KEY, MsDataBytes BLOB);"
                      "CREATE TABLE IF NOT EXISTS SpectrumSourceGroup (Id INTEGER PRIMARY KEY, Name TEXT);"
                      "CREATE TABLE IF NOT EXISTS SpectrumSourceGroupLink (Id INTEGER PRIMARY KEY, Source INT, Group_ INT);"
                      "CREATE TABLE IF NOT EXISTS Spectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);"
                      "CREATE TABLE IF NOT EXISTS Analysis (Id INTEGER PRIMARY KEY, Name TEXT, SoftwareName TEXT, SoftwareVersion TEXT, Type INT, StartTime DATETIME);"
                      "CREATE TABLE IF NOT EXISTS AnalysisParameter (Id INTEGER PRIMARY KEY, Analysis INT, Name TEXT, Value TEXT);"
                      "CREATE TABLE IF NOT EXISTS Modification (Id INTEGER PRIMARY KEY, MonoMassDelta NUMERIC, AvgMassDelta NUMERIC, Formula TEXT, Name TEXT);"
                      "CREATE TABLE IF NOT EXISTS Protein (Id INTEGER PRIMARY KEY, Accession TEXT, IsDecoy INT, Cluster INT, ProteinGroup INT, Length INT, GeneId TEXT, GeneGroup INT);"
                      "CREATE TABLE IF NOT EXISTS ProteinData (Id INTEGER PRIMARY KEY, Sequence TEXT);"
                      "CREATE TABLE IF NOT EXISTS ProteinMetadata (Id INTEGER PRIMARY KEY, Description TEXT, Hash BLOB, TaxonomyId INT, GeneName TEXT, Chromosome TEXT, GeneFamily TEXT, GeneDescription TEXT);"
                      "CREATE TABLE IF NOT EXISTS Peptide (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, PeptideGroup INT, DecoySequence TEXT);"
                      "CREATE TABLE IF NOT EXISTS PeptideInstance (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);"
                      "CREATE TABLE IF NOT EXISTS PeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
                      "CREATE TABLE IF NOT EXISTS PeptideModification (Id INTEGER PRIMARY KEY, PeptideSpectrumMatch INT, Modification INT, Offset INT, Site TEXT);"
                      "CREATE TABLE IF NOT EXISTS PeptideModificationProbability(PeptideModification INTEGER PRIMARY KEY, Probability NUMERIC);"
                      "CREATE TABLE IF NOT EXISTS PeptideSpectrumMatchScore (PsmId INTEGER NOT NULL, Value NUMERIC, ScoreNameId INTEGER NOT NULL, primary key (PsmId, ScoreNameId));"
                      "CREATE TABLE IF NOT EXISTS PeptideSpectrumMatchScoreName (Id INTEGER PRIMARY KEY, Name TEXT UNIQUE NOT NULL);"
                      "CREATE TABLE IF NOT EXISTS IntegerSet (Value INTEGER PRIMARY KEY);"
                      "CREATE TABLE IF NOT EXISTS IsobaricSampleMapping (GroupId INTEGER PRIMARY KEY, Samples TEXT);"
                      "CREATE TABLE IF NOT EXISTS LayoutProperty (Id INTEGER PRIMARY KEY, Name TEXT, PaneLocations TEXT, HasCustomColumnSettings INT, FormProperties TEXT);"
                      "CREATE TABLE IF NOT EXISTS ProteinCoverage (Id INTEGER PRIMARY KEY, Coverage NUMERIC, CoverageMask BLOB);"
                      "CREATE TABLE IF NOT EXISTS SpectrumQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
                      "CREATE TABLE IF NOT EXISTS DistinctMatchQuantitation (Id TEXT PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
                      "CREATE TABLE IF NOT EXISTS PeptideQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
                      "CREATE TABLE IF NOT EXISTS ProteinQuantitation (Id INTEGER PRIMARY KEY, iTRAQ_ReporterIonIntensities BLOB, TMT_ReporterIonIntensities BLOB, PrecursorIonIntensity NUMERIC);"
                      "CREATE TABLE IF NOT EXISTS XICMetrics (Id INTEGER PRIMARY KEY, DistinctMatch INTEGER, SpectrumSource INTEGER, Peptide INTEGER, PeakIntensity NUMERIC, PeakArea NUMERIC, PeakSNR NUMERIC, PeakTimeInSeconds NUMERIC);"
                      "CREATE TABLE IF NOT EXISTS QonverterSettings (Id INTEGER PRIMARY KEY, QonverterMethod INT, DecoyPrefix TEXT, RerankMatches INT, Kernel INT, MassErrorHandling INT, MissedCleavagesHandling INT, TerminalSpecificityHandling INT, ChargeStateHandling INT, ScoreInfoByName TEXT);"
                      "CREATE TABLE IF NOT EXISTS FilterHistory (Id INTEGER PRIMARY KEY, MaximumQValue NUMERIC, MinimumDistinctPeptides INT, MinimumSpectra INT,  MinimumAdditionalPeptides INT, GeneLevelFiltering INT, PrecursorMzTolerance TEXT,\n"
                      "                                          DistinctMatchFormat TEXT, MinimumSpectraPerDistinctMatch INT, MinimumSpectraPerDistinctPeptide INT, MaximumProteinGroupsPerPeptide INT,\n"
                      "                                          Clusters INT, ProteinGroups INT, Proteins INT, GeneGroups INT, Genes INT, DistinctPeptides INT, DistinctMatches INT, FilteredSpectra INT, ProteinFDR NUMERIC, PeptideFDR NUMERIC, SpectrumFDR NUMERIC);"

                      "DELETE FROM SpectrumSource;"
                      "DELETE FROM SpectrumSourceMetadata;"
                      "DELETE FROM SpectrumSourceGroup;"
                      "DELETE FROM SpectrumSourceGroupLink;"
                      "DELETE FROM Spectrum;"
                      "DELETE FROM Analysis;"
                      "DELETE FROM AnalysisParameter;"
                      "DELETE FROM Modification;"
                      "DELETE FROM Protein;"
                      "DELETE FROM ProteinData;"
                      "DELETE FROM ProteinMetadata;"
                      "DELETE FROM Peptide;"
                      "DELETE FROM PeptideInstance;"
                      "DELETE FROM PeptideSpectrumMatch;"
                      "DELETE FROM PeptideModification;"
                      "DELETE FROM PeptideSpectrumMatchScore;"
                      "DELETE FROM PeptideSpectrumMatchScoreName;"
                      "DELETE FROM IntegerSet;"
                      "DELETE FROM QonverterSettings;"
                     );
        transaction.commit();
    }

    void insertAnalysisMetadata()
    {
        sqlite::transaction transaction(idpDb);

        if (mzid.analysisProtocolCollection.spectrumIdentificationProtocol.empty())
            throw runtime_error("no spectrum identification protocol");

        if (mzid.analysisProtocolCollection.spectrumIdentificationProtocol.size() > 1)
            throw runtime_error("more than one spectrum identification protocol not supported");

        //SpectrumIdentificationProtocol& sip = *mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];

        // insert the root group
        sqlite::command(idpDb, "INSERT INTO SpectrumSourceGroup (Id, Name) VALUES (1,'/')").execute();
        sqlite::command(idpDb, "INSERT INTO SpectrumSourceGroupLink (Id, Source, Group_) VALUES (1,1,1)").execute();

        // create commands for inserting file-level metadata (SpectrumSource, Analysis, AnalysisParameter)
        sqlite::command insertSpectrumSource(idpDb, "INSERT INTO SpectrumSource (Id, Name, URL, Group_, TotalSpectraMS1, TotalIonCurrentMS1, TotalSpectraMS2, TotalIonCurrentMS2, QuantitationMethod) VALUES (?,?,?,1,0,0,0,0,0)");
        sqlite::command insertSpectrumSourceMetadata(idpDb, "INSERT INTO SpectrumSourceMetadata (Id) VALUES (1)");
        sqlite::command insertAnalysis(idpDb, "INSERT INTO Analysis (Id, Name, SoftwareName, SoftwareVersion, Type, StartTime) VALUES (?,?,?,?,?,?)");
        sqlite::command insertAnalysisParameter(idpDb, "INSERT INTO AnalysisParameter (Id, Analysis, Name, Value) VALUES (?,?,?,?)");

        string spectraDataName = mzid.dataCollection.inputs.spectraData[0]->name;
        bal::replace_all(spectraDataName, "\\", "/");
        if (spectraDataName.empty())
        {
            string location = mzid.dataCollection.inputs.spectraData[0]->location;
            bal::replace_all(location, "\\", "/");
            spectraDataName = Parser::sourceNameFromFilename(bfs::path(location).filename().string());

            if (spectraDataName.empty())
                throw runtime_error("no spectrum source name or location");
        }
        else
            spectraDataName = Parser::sourceNameFromFilename(bfs::path(spectraDataName).filename().string());

        // insert file-level metadata into the database
        insertSpectrumSource.binder() << 1
                                      << spectraDataName
                                      << mzid.dataCollection.inputs.spectraData[0]->location;
        insertSpectrumSource.execute();
        insertSpectrumSourceMetadata.execute();

        insertAnalysis.binder() << 1
                                << analysis.importSettings.analysisName
                                << analysis.softwareName
                                << analysis.softwareVersion
                                << 0;
        if (!analysis.startTime.is_not_a_date_time())
            insertAnalysis.bind(6, format_date_time("%Y-%m-%d %H:%M:%S", analysis.startTime));
        insertAnalysis.execute();

        int analysisParameterId = 0;
        BOOST_FOREACH_FIELD((const string& name)(const string& value), analysis.parameters)
        {
            insertAnalysisParameter.binder() << ++analysisParameterId << 1 << name << value;
            insertAnalysisParameter.execute();
            insertAnalysisParameter.reset();
        }

        transaction.commit();
    }

    void insertScoreNames(SpectrumIdentificationItemPtr& sii)
    {
        sqlite::command insertScoreName(idpDb, "INSERT INTO PeptideSpectrumMatchScoreName (Id, Name) VALUES (?,?)");

        sqlite3_int64 nextScoreId = 0;

        for(CVParam& cvParam : sii->cvParams)
        {
            insertScoreName.binder() << ++nextScoreId << cvParam.name();
            insertScoreName.execute();
            insertScoreName.reset();
        }

        for(UserParam& userParam : sii->userParams)
        {
            insertScoreName.binder() << ++nextScoreId << userParam.name;
            insertScoreName.execute();
            insertScoreName.reset();
        }
    }

    void insertSpectrumResults(IterationListener::Status& status)
    {
        sqlite::transaction transaction(idpDb);

        if (mzid.dataCollection.analysisData.spectrumIdentificationList.empty())
            throw runtime_error("no spectrum identification list");

        // create commands for inserting results
        sqlite::command insertSpectrum(idpDb, "INSERT INTO Spectrum (Id, Source, Index_, NativeID, PrecursorMZ, ScanTimeInSeconds) VALUES (?,1,?,?,?,?)");
        sqlite::command insertPeptide(idpDb, "INSERT INTO Peptide (Id, MonoisotopicMass, MolecularWeight, PeptideGroup, DecoySequence) VALUES (?,?,?,0,?)");
        sqlite::command insertPSM(idpDb, "INSERT INTO PeptideSpectrumMatch (Id, Spectrum, Analysis, Peptide, QValue, ObservedNeutralMass, MonoisotopicMassError, MolecularWeightError, Rank, Charge) VALUES (?,?,?,?,?,?,?,?,?,?)");
        sqlite::command insertPeptideModification(idpDb, "INSERT INTO PeptideModification (Id, PeptideSpectrumMatch, Modification, Offset, Site) VALUES (?,?,?,?,?)");
        sqlite::command insertModification(idpDb, "INSERT INTO Modification (Id, MonoMassDelta, AvgMassDelta, Formula, Name) VALUES (?,?,?,?,?)");
        sqlite::command insertScore(idpDb, "INSERT INTO PeptideSpectrumMatchScore (PsmId, Value, ScoreNameId) VALUES (?,?,?)");

        // only decoy proteins and peptide instances are inserted with these commands
        sqlite::command insertProtein(idpDb, "INSERT INTO Protein (Id, Accession, IsDecoy, Cluster, ProteinGroup, Length) VALUES (?,?,1,0,0,NULL)");
        sqlite::command insertPeptideInstance(idpDb, "INSERT INTO PeptideInstance (Id, Protein, Peptide, Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages) VALUES (?,?,?,NULL,?,?,?,?)");

        map<string, sqlite3_int64> distinctSpectra;
        map<string, sqlite3_int64> proteinIdByAccession;
        //map<SearchModificationPtr, sqlite3_int64> modifications;

        SpectrumIdentificationProtocol& sip = *mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];
        SpectrumIdentificationList& sil = *mzid.dataCollection.analysisData.spectrumIdentificationList[0];

        sqlite3_int64 nextSpectrumId = 0, nextPeptideId = 0, nextPSMId = 0, nextPMId = 0, nextModId = 0,
                      nextProteinId = 0, nextPeptideInstanceId = 0;

        const string& decoyPrefix = analysis.importSettings.qonverterSettings.decoyPrefix;

        bool hasScoreNames = false;
        int iterationIndex = 0;
        try
        {
            for(SpectrumIdentificationResultPtr& sir : sil.spectrumIdentificationResult)
            {
                ITERATION_UPDATE(ilr, iterationIndex++, sil.spectrumIdentificationResult.size(), "writing spectrum results");

                if (!sir) throw runtime_error("[Parser::insertSpectrumResults] null spectrumIdentificationResult");

                // without an SII, precursor m/z is unknown, so empty results are skipped
                if (sir->spectrumIdentificationItem.empty())
                    continue;

                if (!sir->spectrumIdentificationItem[0]) throw runtime_error("[Parser::insertSpectrumResults] null spectrumIdentificationItem");

                // insert distinct spectrum
                nextSpectrumId = distinctSpectra.size() + 1;
                bool spectrumInserted = distinctSpectra.insert(make_pair(sir->spectrumID, nextSpectrumId)).second;
                if (!spectrumInserted)
                    throw runtime_error("non-unique spectrumIDs not supported (" + sir->spectrumID + ")");

                double firstPrecursorMZ = sir->spectrumIdentificationItem[0]->experimentalMassToCharge;
                double scanTimeInSeconds = sir->cvParam(MS_scan_start_time).timeInSeconds();
                insertSpectrum.binder() << nextSpectrumId << nextSpectrumId << sir->spectrumID << firstPrecursorMZ << scanTimeInSeconds;
                insertSpectrum.execute();
                insertSpectrum.reset();

                for(SpectrumIdentificationItemPtr& sii : sir->spectrumIdentificationItem)
                {
                    if (!sii) throw runtime_error("[Parser::insertSpectrumResults] null spectrumIdentificationItem");

                    PeptidePtr peptidePtr = sii->peptidePtr;
                    if (!peptidePtr && !sii->peptideEvidencePtr.empty()) peptidePtr = sii->peptideEvidencePtr.front()->peptidePtr;
                    if (!peptidePtr || peptidePtr->empty())
                        throw runtime_error("SII with a missing or empty peptide reference (" + sii->id + ")");

                    // skip low ranking results according to import settings
                    if (analysis.importSettings.maxResultRank > 0 &&
                        analysis.importSettings.maxResultRank < sii->rank)
                        continue;

                    // insert distinct peptide
                    const string& sequence = peptidePtr->peptideSequence;

                    // skip short peptides
                    if (analysis.importSettings.minPeptideLength > sequence.length())
                        continue;

                    proteome::Peptide pwizPeptide(sequence);
                    shared_string sharedSequence(new string(sequence));

                    nextPeptideId = distinctPeptideIdBySequence.size() + 1;
                    bool peptideInserted = distinctPeptideIdBySequence.insert(make_pair(sharedSequence, nextPeptideId)).second;
                    if (peptideInserted)
                    {
                        bool hasDecoy = false;
                        bool hasTarget = false;
                        vector<PeptideEvidencePtr> decoyPeptideEvidence;
                        for(const PeptideEvidencePtr& pe : sii->peptideEvidencePtr)
                        {
                            if (!pe) throw runtime_error("[Parser::insertSpectrumResults] null peptideEvidencePtr");
                            if (!pe->dbSequencePtr) throw runtime_error("[Parser::insertSpectrumResults] null dbSequencePtr");

                            bool isDecoy = bal::starts_with(pe->dbSequencePtr->accession, decoyPrefix);
                            hasDecoy |= isDecoy;
                            hasTarget |= !isDecoy;
                            if (isDecoy)
                                decoyPeptideEvidence.push_back(pe);
                        }

                        insertPeptide.binder() << nextPeptideId << pwizPeptide.monoisotopicMass() << pwizPeptide.molecularWeight();

                        if (hasTarget)
                            targetPeptides.insert(sharedSequence);

                        // if the peptide comes from only target proteins, leave the DecoySequence null
                        if (!hasDecoy)
                            insertPeptide.binder(4) << sqlite::ignore;
                        else
                            insertPeptide.binder(4) << *sharedSequence;

                        insertPeptide.execute();
                        insertPeptide.reset();

                        // some bogus files may repeat the same decoy peptide
                        set<shared_string> decoyPeptides;

                        // decoy proteins and peptide instances are inserted immediately
                        for(const PeptideEvidencePtr& pe : decoyPeptideEvidence)
                        {
                            const DBSequence& dbs = *pe->dbSequencePtr;
                            map<string, sqlite3_int64>::iterator itr; bool wasInserted;
                            boost::tie(itr, wasInserted) = proteinIdByAccession.insert(make_pair(dbs.accession, 0));

                            if (wasInserted)
                            {
                                itr->second = ++nextProteinId;

                                insertProtein.binder() << nextProteinId << dbs.accession;
                                insertProtein.execute();
                                insertProtein.reset();
                            }

                            set<shared_string>::iterator itr2; bool wasInserted2;
                            boost::tie(itr2, wasInserted2) = decoyPeptides.insert(sharedSequence);

                            if (wasInserted2)
                            {
                                sqlite3_int64 curProteinId = itr->second;

                                // if PeptideEvidence has invalid pre/post, we cannot know the terminal specifity, so just assume fully specific
                                bool nTerminusIsSpecific = true;
                                bool cTerminusIsSpecific = true;
                                int missedCleavages = 0;

                                if (bal::is_any_of("-ABCDEFGHIKLMNPQRSTUVWYZ")(pe->pre) &&
                                    bal::is_any_of("-ABCDEFGHIKLMNPQRSTUVWYZ")(pe->post))
                                {
                                    proteome::DigestedPeptide peptide = digestedPeptide(sip, *pe);

                                    nTerminusIsSpecific = peptide.NTerminusIsSpecific();
                                    cTerminusIsSpecific = peptide.CTerminusIsSpecific();
                                    missedCleavages = peptide.missedCleavages();
                                }

                                insertPeptideInstance.binder() << ++nextPeptideInstanceId
                                                               << curProteinId
                                                               << nextPeptideId
                                                               << *sharedSequence
                                                               << nTerminusIsSpecific
                                                               << cTerminusIsSpecific
                                                               << missedCleavages;
                                insertPeptideInstance.execute();
                                insertPeptideInstance.reset();
                            }
                        }
                    }
                    else
                        nextPeptideId = distinctPeptideIdBySequence[sharedSequence];

                    ++nextPSMId;

                    // build map of mod offset to total mod mass (in order to merge mods at the same offset)
                    typedef pair<double, double> MassPair;
                    map<int, MassPair> modMassByOffset;
                    for(ModificationPtr& mod : peptidePtr->modification)
                    {
                        if (!mod) throw runtime_error("[Parser::insertSpectrumResults] null modification");

                        int offset = mod->location - 1;
                        if (offset < 0)
                            offset = INT_MIN;
                        else if (offset >= (int) sequence.length())
                            offset = INT_MAX;

                        MassPair& massPair = modMassByOffset[offset];
                        massPair.first += mod->monoisotopicMassDelta;
                        massPair.second += mod->avgMassDelta;
                    }

                    // insert modifications
                    BOOST_FOREACH_FIELD((const int& offset)(const MassPair& massPair), modMassByOffset)
                    {
                        ++nextPMId;

                        double modMass = massPair.first > 0 ? massPair.first : massPair.second;

                        pair<map<double, sqlite3_int64>::iterator, bool> insertResult =
                            modIdByDeltaMass.insert(make_pair(modMass, 0));
                        if (insertResult.second)
                        {
                            insertResult.first->second = ++nextModId;
                            insertModification.binder() << nextModId
                                                        << massPair.first
                                                        << massPair.second;
                            insertModification.execute();
                            insertModification.reset();
                        }

                        char site;
                        if (offset == INT_MIN)
                            site = '(';
                        else if (offset == INT_MAX)
                            site = ')';
                        else
                            site = sequence[offset];

                        insertPeptideModification.binder() << nextPMId
                                                           << nextPSMId
                                                           << insertResult.first->second // mod id
                                                           << offset
                                                           << site;
                        insertPeptideModification.execute();
                        insertPeptideModification.reset();

                        pwizPeptide.modifications()[offset].push_back(proteome::Modification(massPair.first, massPair.second));
                    }

                    double precursorMass = Ion::neutralMass(sii->experimentalMassToCharge, sii->chargeState);

                    // insert peptide spectrum match
                    insertPSM.binder() << nextPSMId
                                       << nextSpectrumId
                                       << 1 // analysis
                                       << nextPeptideId
                                       << 2 // q value
                                       << precursorMass
                                       << (precursorMass - pwizPeptide.monoisotopicMass())
                                       << (precursorMass - pwizPeptide.molecularWeight())
                                       << sii->rank
                                       << sii->chargeState;
                    insertPSM.execute();
                    insertPSM.reset();

                    if (!hasScoreNames)
                    {
                        hasScoreNames = true;
                        insertScoreNames(sii);
                    }

                    if (sii->cvParams.empty() && sii->userParams.empty())
                        throw runtime_error("no scores found for SII");

                    sqlite3_int64 nextScoreId = 0;

                    for(const CVParam& cvParam : sii->cvParams)
                    {
                        insertScore.binder() << nextPSMId << cvParam.value << ++nextScoreId;
                        insertScore.execute();
                        insertScore.reset();
                    }

                    for(const UserParam& userParam : sii->userParams)
                    {
                        insertScore.binder() << nextPSMId << userParam.value << ++nextScoreId;
                        insertScore.execute();
                        insertScore.reset();
                    }
                }
            }
        }
        catch (exception& e)
        {
            throw runtime_error("error parsing spectrum result " + lexical_cast<string>(iterationIndex) + " (" + sil.spectrumIdentificationResult[iterationIndex]->id + "): " + e.what());
        }
        catch (...)
        {
            throw runtime_error("unknown error parsing spectrum result " + lexical_cast<string>(iterationIndex) + " (" + sil.spectrumIdentificationResult[iterationIndex]->id + ")");
        }

        if (targetPeptides.size() == distinctPeptideIdBySequence.size())
            throw runtime_error("no peptides found mapping to a decoy protein; is the decoy prefix set correctly?");

        transaction.commit();
    }

    void createIndexes()
    {
        sqlite::transaction transaction(idpDb);
        idpDb.execute("CREATE UNIQUE INDEX IF NOT EXISTS Protein_Accession ON Protein (Accession);"
                      "CREATE INDEX IF NOT EXISTS PeptideInstance_PeptideProtein ON PeptideInstance (Peptide, Protein);"
                      "CREATE UNIQUE INDEX IF NOT EXISTS PeptideInstance_ProteinOffsetLength ON PeptideInstance (Protein, Offset, Length);"
                      "CREATE UNIQUE INDEX IF NOT EXISTS SpectrumSourceGroupLink_SourceGroup ON SpectrumSourceGroupLink (Source, Group_);"
                      "CREATE INDEX IF NOT EXISTS Spectrum_SourceIndex ON Spectrum (Source, Index_);"
                      "CREATE UNIQUE INDEX IF NOT EXISTS Spectrum_SourceNativeID ON Spectrum (Source, NativeID);"
                      "CREATE INDEX IF NOT EXISTS PeptideSpectrumMatch_PeptideSpectrumAnalysis ON PeptideSpectrumMatch (Peptide, Spectrum, Analysis);"
                      "CREATE INDEX IF NOT EXISTS PeptideSpectrumMatch_SpectrumAnalysisPeptide ON PeptideSpectrumMatch (Spectrum, Analysis, Peptide);"
                      "CREATE INDEX IF NOT EXISTS PeptideSpectrumMatch_QValue ON PeptideSpectrumMatch (QValue);"
                      "CREATE INDEX IF NOT EXISTS PeptideSpectrumMatch_Rank ON PeptideSpectrumMatch (Rank);"
                      "CREATE INDEX IF NOT EXISTS PeptideModification_PeptideSpectrumMatch ON PeptideModification (PeptideSpectrumMatch);"
                      "CREATE INDEX IF NOT EXISTS PeptideModification_Modification ON PeptideModification (Modification);"
                      "CREATE INDEX IF NOT EXISTS XICMetrics_MatchSourcePeptide ON XICMetrics (DistinctMatch,SpectrumSource,Peptide);"
                     );
        transaction.commit();
    }

    void applyQValueFilter(const Analysis& analysis)
    {
        const Qonverter::Settings& settings = analysis.importSettings.qonverterSettings;

        Qonverter qonverter;
        qonverter.logQonversionDetails = analysis.importSettings.logQonversionDetails;
        qonverter.settingsByAnalysis[0] = settings;
        qonverter.qonvert(idpDb.connected(), ilr);

        if (analysis.importSettings.maxQValue == 1 && analysis.importSettings.maxResultRank == 0)
            return;

        int maxResultRank = analysis.importSettings.maxResultRank > 0 ? analysis.importSettings.maxResultRank : 100;

        sqlite::transaction transaction(idpDb);

        string sql =
            // Apply a broad QValue filter on top-ranked PSMs
            "DELETE FROM PeptideSpectrumMatch WHERE (Rank = 1 AND QValue > %1%) OR (Rank > %2%);"

            // Delete all PSMs for a spectrum if the spectrum's top-ranked PSM was deleted above
            "DELETE FROM PeptideSpectrumMatch"
            "      WHERE Rank > 1"
            "        AND Spectrum NOT IN ("
            "                             SELECT DISTINCT Spectrum"
            "                             FROM PeptideSpectrumMatch"
            "                             WHERE Rank = 1"
            "                            );"

            // Delete links to the deleted PSMs
            "DELETE FROM PeptideSpectrumMatchScore WHERE PsmId NOT IN (SELECT Id FROM PeptideSpectrumMatch);"
            "DELETE FROM PeptideModification WHERE PeptideSpectrumMatch NOT IN (SELECT Id FROM PeptideSpectrumMatch);"
            "DELETE FROM Spectrum WHERE Id NOT IN (SELECT DISTINCT Spectrum FROM PeptideSpectrumMatch);"
            "DELETE FROM Peptide WHERE Id NOT IN (SELECT DISTINCT Peptide FROM PeptideSpectrumMatch);"
            "DELETE FROM PeptideInstance WHERE Peptide NOT IN (SELECT Id FROM Peptide);"
            "DELETE FROM Protein WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);"
            "DELETE FROM ProteinData WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);"
            "DELETE FROM ProteinMetadata WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);";

        sql = (boost::format(sql) % analysis.importSettings.maxQValue % maxResultRank).str();
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::DebugInfo) << sql;
        idpDb.execute(sql.c_str());

        transaction.commit();
        
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "Spectra after import FDR score filter: " << sqlite::query(idpDb, "SELECT COUNT(*) FROM Spectrum").begin()->get<int>(0);

        idpDb.execute("VACUUM");
    }
};


struct ProteinDatabaseTaskGroup
{
    ProteomeDataPtr proteomeDataPtr;
    vector<string> inputFilepaths;
};

vector<ProteinDatabaseTaskGroup> createTasksPerProteinDatabase(const vector<string>& inputFilepaths,
                                                               const DistinctAnalysisMap& distinctAnalysisByFilepath,
                                                               map<string, ProteomeDataPtr> proteinDatabaseByFilepath,
                                                               bool skipSourceOnError,
                                                               int maxThreads)
{
    // group input files by their protein database
    map<string, vector<string> > inputFilepathsByProteinDatabase;
    for(const string& inputFilepath : inputFilepaths)
    {
        if (distinctAnalysisByFilepath.count(inputFilepath) == 0)
        {
            if (skipSourceOnError)
                continue;

            throw runtime_error("[Parser::parse()] unable to find analysis for file \"" + inputFilepath + "\"");
        }

        const AnalysisPtr& analysis = distinctAnalysisByFilepath.find(inputFilepath)->second;
        const string& proteinDatabaseFilepath = analysis->importSettings.proteinDatabaseFilepath;

        inputFilepathsByProteinDatabase[proteinDatabaseFilepath].push_back(inputFilepath);
    }

    int processorCount = min(maxThreads, (int) boost::thread::hardware_concurrency());
    vector<ProteinDatabaseTaskGroup> taskGroups;

    if (inputFilepathsByProteinDatabase.empty())
    {
        BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "[createTasksPerProteinDatabase] no tasks created";
        return taskGroups;
    }

    BOOST_FOREACH_FIELD((const string& proteinDatabaseFilepath)(vector<string>& inputFilepaths),
                        inputFilepathsByProteinDatabase)
    {
        taskGroups.push_back(ProteinDatabaseTaskGroup());
        taskGroups.back().proteomeDataPtr = proteinDatabaseByFilepath[proteinDatabaseFilepath];

        // shuffled so that large and small input files get mixed
        random_shuffle(inputFilepaths.begin(), inputFilepaths.end());

        int processorsUsed = 0;
        for(const string& inputFilepath : inputFilepaths)
        {
            bfs::path outputFilepath = Parser::outputFilepath(inputFilepath);
            if (bfs::exists(outputFilepath))
            {
                // for now, abort; eventually we want to merge? here
                throw runtime_error("[createTasksPerProteinDatabase] file already exists: \"" + outputFilepath.string() + "\"");
            }

            taskGroups.back().inputFilepaths.push_back(inputFilepath);
            ++processorsUsed;

            // if out of processors and there are more input files for this database, add another top-level task
            if (processorsUsed == processorCount && &inputFilepath != &inputFilepaths.back())
            {
                taskGroups.push_back(ProteinDatabaseTaskGroup());
                taskGroups.back().proteomeDataPtr = proteinDatabaseByFilepath[proteinDatabaseFilepath];
                processorsUsed = 0;
            }
        }
    }

    return taskGroups;
}

// an iteration listener that prepends the inputFilepath before forwarding the update message
struct ParserForwardingIterationListener : public IterationListener
{
    const IterationListenerRegistry& inner;
    const string& inputFilepath;

    ParserForwardingIterationListener(const IterationListenerRegistry& inner, const string& inputFilepath)
        : inner(inner), inputFilepath(inputFilepath)
    {}

    virtual Status update(const UpdateMessage& updateMessage)
    {
        string specificMessage = inputFilepath + "*" + updateMessage.message;
        return inner.broadcastUpdateMessage(UpdateMessage(updateMessage.iterationIndex,
                                                          updateMessage.iterationCount,
                                                          specificMessage));
    }
};


struct ThreadStatus
{
    bool userCanceled;
    boost::exception_ptr exception;

    ThreadStatus() : userCanceled(false) {}
    ThreadStatus(IterationListener::Status status) : userCanceled(status == IterationListener::Status_Cancel) {}
    ThreadStatus(const boost::exception_ptr& e) : userCanceled(false), exception(e) {}
};


struct ParserTask
{
    ParserTask(const string& inputFilepath = "") : inputFilepath(inputFilepath) {}

    string inputFilepath;
    boost::shared_ptr<sqlite::database> idpDb;
    boost::shared_ptr<IdentDataFile> mzid;
    boost::shared_ptr<ParserImpl> parser;
    AnalysisPtr analysis;
    boost::shared_ptr<IterationListenerRegistry> ilr;
    boost::mutex* ioMutex;
};

typedef boost::shared_ptr<ParserTask> ParserTaskPtr;


void executeParserTask(ParserTaskPtr parserTask, ThreadStatus& status)
{
    const string& inputFilepath = parserTask->inputFilepath;
    const IterationListenerRegistry* ilr = parserTask->ilr.get();
    //boost::mutex& ioMutex = *peptideFinderTask->ioMutex;

    try
    {
        // read the mzid document into memory
        ITERATION_UPDATE(ilr, 0, 0, "opening file");
        {
            //boost::mutex::scoped_lock ioLock(ioMutex);
            parserTask->mzid.reset(new IdentDataFile(inputFilepath, 0, ilr));
        }

        try
        {
            // create parser instance
            parserTask->parser.reset(new ParserImpl(inputFilepath,
                                                    *parserTask->analysis,
                                                    *parserTask->idpDb,
                                                    *parserTask->mzid,
                                                    ilr));

            parserTask->parser->insertAnalysisMetadata();

            try
            {
                IterationListener::Status tmpStatus = IterationListener::Status_Ok;
                parserTask->parser->insertSpectrumResults(tmpStatus);
                if (tmpStatus == IterationListener::Status_Cancel)
                {
                    status = tmpStatus;
                    parserTask->mzid.reset();
                    return;
                }

                status = IterationListener::Status_Ok;
            }
            catch (exception& e)
            {
                status = boost::copy_exception(runtime_error("[executeParserTask] error parsing spectrum results \"" + inputFilepath + "\": " + e.what()));
            }
            catch (...)
            {
                status = boost::copy_exception(runtime_error("[executeParserTask] unknown error parsing spectrum results \"" + inputFilepath + "\""));
            }
        }
        catch (exception& e)
        {
            status = boost::copy_exception(runtime_error("[executeParserTask] error creating parser for \"" + inputFilepath + "\": " + e.what()));
        }
        catch (...)
        {
            status = boost::copy_exception(runtime_error("[executeParserTask] unknown error creating parser for \"" + inputFilepath + "\""));
        }
    }
    catch (exception& e)
    {
        status = boost::copy_exception(runtime_error("[executeParserTask] error reading \"" + inputFilepath + "\": " + e.what()));
    }
    catch (...)
    {
        status = boost::copy_exception(runtime_error("[executeParserTask] unknown error reading \"" + inputFilepath + "\""));
    }

    parserTask->mzid.reset();
}


struct PeptideFinderTask;
typedef boost::weak_ptr<PeptideFinderTask> PeptideFinderTaskWeakPtr;


struct ProteinReaderTask
{
    ProteomeDataPtr proteomeDataPtr;
    int proteinCount;
    vector<PeptideFinderTaskWeakPtr> peptideFinderTasks;
    string decoyPrefix;
    boost::mutex queueMutex;
    boost::atomic_uint32_t done;
};

typedef boost::shared_ptr<ProteinReaderTask> ProteinReaderTaskPtr;


struct PeptideFinderTask
{
    ProteinReaderTaskPtr proteinReaderTask;
    deque<proteome::ProteinPtr> proteinQueue;
    ParserTaskPtr parserTask;
    boost::atomic<bool> done;
    const IterationListenerRegistry* ilr;
    boost::mutex* ioMutex;
};

typedef boost::shared_ptr<PeptideFinderTask> PeptideFinderTaskPtr;


void executeProteinReaderTask(ProteinReaderTaskPtr proteinReaderTask, ThreadStatus& status)
{
    try
    {
        const proteome::ProteomeData& pd = *proteinReaderTask->proteomeDataPtr;
        const proteome::ProteinList& pl = *pd.proteinListPtr;

        const size_t batchSize = 50;
        vector<proteome::ProteinPtr> proteinBatch(batchSize);

        boost::mutex::scoped_lock lock(proteinReaderTask->queueMutex, boost::defer_lock);

        // protein database is read over for each peptide batch
        while (true)
        {
            if (proteinReaderTask->done == proteinReaderTask->peptideFinderTasks.size())
            {
                status = IterationListener::Status_Ok;
                return; // ~scoped_lock calls unlock()
            }

            for (size_t i=0; i < pl.size(); ++i)
            {
                proteinBatch.clear();

                for (int j=0; j < batchSize && i+j < pl.size(); ++j)
                {
                    proteome::ProteinPtr p = pl.protein(i+j);
                    string sequence = p->sequence();
                    for (size_t k=0; k < sequence.length(); ++k)
                        if (sequence[k] < 'A' || sequence[k] > 'Z')
                            sequence[k] = 'X';
                    p.reset(new proteome::Protein(p->id, p->index, p->description, sequence));
                    proteinBatch.push_back(p);
                }
                i += batchSize - 1;

                while (true)
                {
                    // check for early cancellation
                    if (proteinReaderTask->done == proteinReaderTask->peptideFinderTasks.size())
                    {
                        status = IterationListener::Status_Cancel;
                        return; // ~scoped_lock calls unlock()
                    }

                    lock.lock();

                    size_t maxQueueSize = 0;
                    for(const PeptideFinderTaskWeakPtr& taskPtr : proteinReaderTask->peptideFinderTasks)
                    {
                        PeptideFinderTaskPtr task = taskPtr.lock();
                        maxQueueSize = max(maxQueueSize, task.get() ? task->proteinQueue.size() : 0);
                    }

                    // keep at most 100 batches in the queue
                    if (maxQueueSize > batchSize * 100)
                    {
                        lock.unlock();
                        boost::this_thread::sleep(bpt::milliseconds(100));
                        continue;
                    }
                    else
                        break;
                }

                // lock is still locked

                for(const PeptideFinderTaskWeakPtr& taskPtr : proteinReaderTask->peptideFinderTasks)
                {
                    PeptideFinderTaskPtr task = taskPtr.lock();
                    if (task.get() && !task->done)
                        task->proteinQueue.insert(task->proteinQueue.end(), proteinBatch.begin(), proteinBatch.end());
                }
                lock.unlock();
            }
        }
    }
    catch (exception& e)
    {
        proteinReaderTask->done.store(proteinReaderTask->peptideFinderTasks.size());
        status = boost::copy_exception(runtime_error("[executeProteinReaderTask] error reading proteins: " + string(e.what())));
    }
    catch (...)
    {
        proteinReaderTask->done.store(proteinReaderTask->peptideFinderTasks.size());
        status = boost::copy_exception(runtime_error("[executeProteinReaderTask] unknown error reading proteins"));
    }
}


void executePeptideFinderTask(PeptideFinderTaskPtr peptideFinderTask, ThreadStatus& status)
{
    ProteinReaderTask& proteinReaderTask = *peptideFinderTask->proteinReaderTask;
    deque<proteome::ProteinPtr>& proteinQueue = peptideFinderTask->proteinQueue;
    ParserTask& parserTask = *peptideFinderTask->parserTask;
    ParserImpl& parser = *parserTask.parser;
    sqlite::database& idpDb = *parserTask.idpDb;
    const IterationListenerRegistry* ilr = peptideFinderTask->ilr;
    boost::mutex& ioMutex = *peptideFinderTask->ioMutex;
    map<shared_string, sqlite3_int64, SharedStringFastLessThan>& distinctPeptideIdBySequence =
        parser.distinctPeptideIdBySequence;
    set<shared_string>& targetPeptides = parser.targetPeptides;
    set<shared_string>& mappedPeptides = parser.mappedPeptides;

    try
    {
        sqlite::command insertProtein(idpDb, "INSERT INTO Protein (Id, Accession, IsDecoy, Cluster, ProteinGroup, Length) VALUES (?,?,0,0,0,?)");
        sqlite::command insertProteinData(idpDb, "INSERT INTO ProteinData (Id, Sequence) VALUES (?,?)");
        sqlite::command insertProteinMetadata(idpDb, "INSERT INTO ProteinMetadata (Id, Description, Hash) VALUES (?,?,?)");
        sqlite::command insertPeptideInstance(idpDb, "INSERT INTO PeptideInstance (Id, Protein, Peptide, Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages) VALUES (?,?,?,?,?,?,?,?)");

        sqlite3_int64 nextProteinId = sqlite::query(idpDb, "SELECT MAX(Id) FROM Protein").begin()->get<int>(0);
        sqlite3_int64 nextPeptideInstanceId = sqlite::query(idpDb, "SELECT MAX(Id) FROM PeptideInstance").begin()->get<int>(0);
        int maxProteinLength = 0;
        CSHA1 hasher;
        char hash[20];

        const string& decoyPrefix = parserTask.analysis->importSettings.qonverterSettings.decoyPrefix;
        vector<string> cleavageAgentRegexes = pwiz::identdata::cleavageAgentRegexes(parser.analysis.enzymes);

        if (cleavageAgentRegexes.empty())
        {
            if (!parser.analysis.enzymes.enzymes.empty() && parser.analysis.enzymes.enzymes[0]->terminalSpecificity == pwiz::proteome::Digestion::NonSpecific)
                cleavageAgentRegexes.push_back("(?=.)");
            else
                throw runtime_error("unknown cleavage agent");
        }

        boost::mutex::scoped_lock lock(proteinReaderTask.queueMutex, boost::defer_lock);

        /*if (ilr && ilr->broadcastUpdateMessage(UpdateMessage(0, 0, parserTask.inputFilepath + "*opening protein database")) == IterationListener::Status_Cancel)
        {
            lock.lock();
            proteinReaderTask.done = true;
            return IterationListener::Status_Cancel;
        }*/

        // peptide tries are created in batches to ensure scalability
        const size_t peptideBatchSize = 200000;
        int peptideQueries = 0;

        vector<shared_string> peptides;
        BOOST_FOREACH_FIELD((const shared_string& peptide), distinctPeptideIdBySequence)
            peptides.push_back(peptide);

        // distinctPeptideIdBySequence is sorted on peptide length, which is bad for the trie
        random_shuffle(peptides.begin(), peptides.end());

        // maps proteins indexes to protein ids (in the database)
        map<size_t, sqlite3_int64> proteinIdByIndex;

        vector<shared_string> peptideBatch;
        for(const shared_string& peptide : peptides)
        {
            peptideBatch.push_back(peptide);

            // only proceed if we're at the batch size or the end of the peptides
            if (peptideBatch.size() < peptideBatchSize && peptide != *peptides.rbegin())
                continue;

            peptideQueries += (int) peptideBatch.size();
            if (ilr && ilr->broadcastUpdateMessage(UpdateMessage(peptideQueries-1, peptides.size(), parserTask.inputFilepath + "*building peptide trie")) == IterationListener::Status_Cancel)
            {
                proteinReaderTask.done.store(proteinReaderTask.peptideFinderTasks.size());
                status = IterationListener::Status_Cancel;
                return;
            }

            PeptideTrie peptideTrie(peptideBatch.begin(), peptideBatch.end());
            peptideBatch.clear();

            int proteinsDigested = 0;

            while (true)
            {
                // dequeue a batch of proteins, or sleep if none are available
                vector<proteome::ProteinPtr> proteinBatch;

                lock.lock();
                size_t queueSize = proteinQueue.size();
                if (queueSize == 0)
                {
                    if (proteinReaderTask.done == proteinReaderTask.peptideFinderTasks.size())
                    {
                        lock.unlock();
                        break;
                    }
                    else
                    {
                        lock.unlock();
                        boost::this_thread::sleep(bpt::milliseconds(100));
                        continue;
                    }
                }

                const size_t maxBatchSize = 50;
                size_t proteinsRemaining = proteinReaderTask.proteinCount - proteinsDigested;
                size_t batchSize = min(queueSize, min(proteinsRemaining, maxBatchSize));

                proteinBatch.assign(proteinQueue.begin(), proteinQueue.begin() + batchSize);
                proteinQueue.erase(proteinQueue.begin(), proteinQueue.begin() + batchSize);
                lock.unlock();

                // move to the next peptide batch
                if (proteinBatch.empty())
                    break;

                proteinsDigested += proteinBatch.size();

                if (ilr && ilr->broadcastUpdateMessage(UpdateMessage(proteinsDigested-1, proteinReaderTask.proteinCount, parserTask.inputFilepath + "*finding peptides in proteins")) == IterationListener::Status_Cancel)
                {
                    proteinReaderTask.done.store(proteinReaderTask.peptideFinderTasks.size());
                    status = IterationListener::Status_Cancel;
                    return;
                }

                for(proteome::ProteinPtr& protein : proteinBatch)
                {
                    // skip decoy proteins
                    if (bal::istarts_with(protein->id, decoyPrefix))
                        continue;

                    typedef boost::shared_ptr<proteome::Digestion> DigestionPtr;
                    proteome::Digestion::Config digestionConfig(100000, 0, 100000, proteome::Digestion::NonSpecific);
                    vector<DigestionPtr> digestions;

                    // if digestions were done independently, create a Digestion for each enzyme;
                    // else create a single Digestion using all enzymes together
                    if (parser.analysis.enzymes.independent)
                        for(const string& cleavageAgentRegex : cleavageAgentRegexes)
                            digestions.push_back(DigestionPtr(new proteome::Digestion(*protein, cleavageAgentRegex, digestionConfig)));
                    else
                        digestions.push_back(DigestionPtr(new proteome::Digestion(*protein, cleavageAgentRegexes, digestionConfig)));

                    vector<PeptideTrie::SearchResult> peptideInstances = peptideTrie.find_all(protein->sequence());

                    if (peptideInstances.empty())
                        continue;

                    maxProteinLength = max((int) protein->sequence().length(), maxProteinLength);

                    map<size_t, sqlite3_int64>::iterator itr; bool wasInserted;
                    boost::tie(itr, wasInserted) = proteinIdByIndex.insert(make_pair(protein->index, 0));

                    if (wasInserted)
                    {
                        itr->second = ++nextProteinId;

                        insertProtein.binder() << nextProteinId << protein->id << (int) protein->sequence().length();
                        insertProtein.execute();
                        insertProtein.reset();

                        insertProteinData.binder() << nextProteinId << protein->sequence();
                        insertProteinData.execute();
                        insertProteinData.reset();

                        hasher.Reset();
                        hasher.Update(reinterpret_cast<const unsigned char*>(&protein->sequence()[0]), protein->sequence().length());
                        hasher.Final();
                        hasher.GetHash(reinterpret_cast<unsigned char*>(hash));

                        insertProteinMetadata.bind(1, nextProteinId);
                        insertProteinMetadata.bind(2, protein->description);
                        insertProteinMetadata.bind(3, reinterpret_cast<void*>(hash), 20);
                        insertProteinMetadata.execute();
                        insertProteinMetadata.reset();
                    }

                    sqlite3_int64 curProteinId = itr->second;

                    for(const PeptideTrie::SearchResult& instance : peptideInstances)
                    {
                        // find the highest terminal specificity for the peptide instance from all digestions
                        proteome::DigestedPeptide bestPeptide("A", 0, 0, false, false);
                        for(const DigestionPtr& digestion : digestions)
                        {
                            proteome::DigestedPeptide peptide = digestion->find_first(*instance.keyword(), instance.offset());
                            if (peptide.specificTermini() > bestPeptide.specificTermini())
                                bestPeptide = peptide;

                            // it can't get better
                            if (bestPeptide.specificTermini() == 2)
                                break;
                        }

                        mappedPeptides.insert(instance.keyword());

                        insertPeptideInstance.binder() << ++nextPeptideInstanceId
                                                       << curProteinId
                                                       << distinctPeptideIdBySequence[instance.keyword()]
                                                       << (int) instance.offset()
                                                       << (int) instance.keyword()->length()
                                                       << bestPeptide.NTerminusIsSpecific()
                                                       << bestPeptide.CTerminusIsSpecific()
                                                       << (int) bestPeptide.missedCleavages();
                        insertPeptideInstance.execute();
                        insertPeptideInstance.reset();
                    }
                }
            }
        }

        // all peptides have been searched against all proteins;
        // if there are still unmapped peptides, the database is probably incorrect
        if (mappedPeptides.size() < targetPeptides.size())
        {
            size_t unmappedPeptideCount = targetPeptides.size() - mappedPeptides.size();
            string unmappedPeptideMessage = lexical_cast<string>(unmappedPeptideCount) +
                                            " of " +
                                            lexical_cast<string>(targetPeptides.size()) +
                                            " peptides did not map to the database and cannot be imported, e.g.";

            vector<shared_string> unmappedPeptides;
            boost::set_difference(targetPeptides,
                                  mappedPeptides,
                                  back_inserter(unmappedPeptides));

            for (size_t i=0; i < unmappedPeptides.size() && i < 3; ++i)
                unmappedPeptideMessage += " " + *unmappedPeptides[i];

            unmappedPeptideMessage += "; did you select the right database? "
                                      "If you want to ignore and skip these peptides, set \"Ignore Unmapped Peptides\" to true.";

            if (!parser.analysis.importSettings.ignoreUnmappedPeptides)
                throw runtime_error(unmappedPeptideMessage);
            else
                BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << unmappedPeptideMessage << endl;
        }

        if (ilr && ilr->broadcastUpdateMessage(UpdateMessage(proteinReaderTask.proteinCount-1, proteinReaderTask.proteinCount, parserTask.inputFilepath + "*finding peptides in proteins")) == IterationListener::Status_Cancel)
        {
            proteinReaderTask.done.store(proteinReaderTask.peptideFinderTasks.size());
            status = IterationListener::Status_Cancel;
            return;
        }

        // the protein reader task stops when done == proteinReaderTask.peptideFinderTasks.size()
        ++proteinReaderTask.done;
        peptideFinderTask->done.store(true);
        proteinQueue.clear();

        sqlite::command insertIntegerSet(idpDb, "INSERT INTO IntegerSet (Value) VALUES (?)");
        for (int i=1; i <= maxProteinLength; ++i)
        {
            insertIntegerSet.binder() << i;
            insertIntegerSet.execute();
            insertIntegerSet.reset();
        }

        try
        {
            ITERATION_UPDATE(ilr, 0, 0, parserTask.inputFilepath + "*creating indexes");
            parser.createIndexes();
        }
        catch (exception& e)
        {
            // failure to create indexes is not fatal (need to check the database for the error)
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "[executePeptideFinderTask] thread " << boost::this_thread::get_id() << " failed to create indexes: " << e.what();
        }

        try
        {
            // run preqonvert if import settings specify it
            ITERATION_UPDATE(ilr, 0, 0, parserTask.inputFilepath + "*qonverting");
            parser.applyQValueFilter(*parserTask.analysis);
        }
        catch (exception& e)
        {
            // failure during qonversion is not fatal
            //ITERATION_UPDATE(ilr, 0, 0, parserTask.inputFilepath + "*failed to apply Q value filter: " + e.what());
            BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "[executePeptideFinderTask] failed to apply Q value filter: " << e.what();
        }

        ITERATION_UPDATE(ilr, 0, 0, parserTask.inputFilepath + "*waiting");
        boost::mutex::scoped_lock ioLock(ioMutex);

        ITERATION_UPDATE(ilr, 0, 0, parserTask.inputFilepath + "*saving database");
        string idpDbFilepath = Parser::outputFilepath(parserTask.inputFilepath).string();
        idpDb.save_to_file(idpDbFilepath.c_str());

        ITERATION_UPDATE(ilr, 0, 1, parserTask.inputFilepath + "*done");
        status = IterationListener::Status_Ok;
    }
    catch (exception& e)
    {
        boost::mutex::scoped_lock lock(proteinReaderTask.queueMutex);
        proteinReaderTask.done.store(proteinReaderTask.peptideFinderTasks.size());
        status = boost::copy_exception(runtime_error("[executePeptideFinderTask] error finding peptides for \"" + parserTask.inputFilepath + "\": " + e.what()));
    }
    catch (...)
    {
        boost::mutex::scoped_lock lock(proteinReaderTask.queueMutex);
        proteinReaderTask.done.store(proteinReaderTask.peptideFinderTasks.size());
        status = boost::copy_exception(runtime_error("[executePeptideFinderTask] unknown error finding peptides for \"" + parserTask.inputFilepath + "\""));
    }
}


void executeTaskGroup(const ProteinDatabaseTaskGroup& taskGroup,
                      const DistinctAnalysisMap& distinctAnalysisByFilepath,
                      vector<shared_ptr<sqlite::database> > memoryDatabases,
                      bool skipSourceOnError,
                      IterationListenerRegistry* ilr)
{
    using boost::thread;

    boost::mutex ioMutex;

    vector<ParserTaskPtr> parserTasks;

    // parsing stage
    {
        // use list so iterators and references stay valid
        list<pair<boost::shared_ptr<thread>, ThreadStatus> > threads;

        for (size_t i=0; i < taskGroup.inputFilepaths.size(); ++i)
        {
            const string& inputFilepath = taskGroup.inputFilepaths[i];

            if (distinctAnalysisByFilepath.count(inputFilepath) == 0)
                throw runtime_error("[Parser::parse()] unable to find analysis for file \"" + inputFilepath + "\"");

            ParserTaskPtr parserTask(new ParserTask(inputFilepath));
            parserTask->analysis = distinctAnalysisByFilepath.find(inputFilepath)->second;
            parserTask->idpDb = memoryDatabases[i];
            if (ilr)
            {
                parserTask->ilr.reset(new IterationListenerRegistry);
                parserTask->ilr->addListener(IterationListenerPtr(new ParserForwardingIterationListener(*ilr, inputFilepath)), 10);
            }
            parserTask->ioMutex = &ioMutex;
            parserTasks.push_back(parserTask);

            threads.push_back(make_pair(boost::shared_ptr<thread>(), IterationListener::Status_Ok));
            threads.back().first.reset(new thread(executeParserTask, parserTasks.back(), boost::ref(threads.back().second)));
        }

        set<boost::shared_ptr<thread> > finishedThreads;
        while (finishedThreads.size() < threads.size())
            BOOST_FOREACH_FIELD((boost::shared_ptr<thread>& t)(ThreadStatus& status), threads)
            {
                if (t->timed_join(bpt::seconds(1)))
                    finishedThreads.insert(t);

                if (status.exception)
                {
                    if (skipSourceOnError)
                        try { boost::rethrow_exception(status.exception); }
                        catch (exception& e) { BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << e.what() << endl; }
                        catch (...) { boost::rethrow_exception(status.exception); }
                    else
                        boost::rethrow_exception(status.exception);
                }
                else if (status.userCanceled)
                    return;
            }
    }

    // peptide finding stage
    {
        list<pair<boost::shared_ptr<thread>, ThreadStatus> > threads;

        ProteinReaderTaskPtr proteinReaderTask(new ProteinReaderTask);
        proteinReaderTask->proteomeDataPtr = taskGroup.proteomeDataPtr;
        proteinReaderTask->proteinCount = taskGroup.proteomeDataPtr->proteinListPtr->size();
        proteinReaderTask->decoyPrefix = parserTasks[0]->analysis->importSettings.qonverterSettings.decoyPrefix;
        proteinReaderTask->done.store(0);

        for (size_t i=0; i < taskGroup.inputFilepaths.size(); ++i)
        {
            PeptideFinderTaskPtr peptideFinderTask(new PeptideFinderTask);
            peptideFinderTask->proteinReaderTask = proteinReaderTask;
            peptideFinderTask->parserTask = parserTasks[i];
            peptideFinderTask->done.store(false);
            peptideFinderTask->ilr = ilr;
            peptideFinderTask->ioMutex = &ioMutex;
            proteinReaderTask->peptideFinderTasks.push_back(peptideFinderTask);

            threads.push_back(make_pair(boost::shared_ptr<thread>(), IterationListener::Status_Ok));
            threads.back().first.reset(new thread(executePeptideFinderTask, peptideFinderTask, boost::ref(threads.back().second)));
        }

        // threads will free their parserTask
        //parserTasks.clear();

        ThreadStatus status;
        boost::thread proteinReaderThread(executeProteinReaderTask, proteinReaderTask, boost::ref(status));

        proteinReaderThread.join();

        set<boost::shared_ptr<thread> > finishedThreads;
        while (finishedThreads.size() < threads.size())
            BOOST_FOREACH_FIELD((boost::shared_ptr<thread>& t)(ThreadStatus& status), threads)
            {
                if (t->timed_join(bpt::seconds(1)))
                    finishedThreads.insert(t);

                if (status.exception)
                {
                    if (skipSourceOnError)
                        try { boost::rethrow_exception(status.exception); }
                        catch (exception& e) { BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << e.what() << endl; }
                        catch (...) { boost::rethrow_exception(status.exception); }
                    else
                        boost::rethrow_exception(status.exception);
                }
                else if (status.userCanceled)
                    return;
            }
    }
    
    // fatal error if an idpDB didn't get saved
    for(const string& inputFilepath : taskGroup.inputFilepaths)
        if (!bfs::exists(Parser::outputFilepath(inputFilepath)))
            throw runtime_error("[executeTaskGroup] no database created for file \"" + inputFilepath + "\"");
}

} // namespace


Parser::Analysis::Analysis() : startTime(bdt::not_a_date_time)
{
    importSettings.maxQValue = 0.25;
    importSettings.maxResultRank = 0;
    importSettings.minPeptideLength = 5;
}


void Parser::ImportSettingsCallback::operator() (const vector<ConstAnalysisPtr>& distinctAnalyses, bool& cancel) const
{
    throw runtime_error("[Parser::parse()] no import settings handler set");
}


Parser::Parser() : skipSourceOnError(false) {}


void Parser::parse(const vector<string>& inputFilepaths, int maxThreads, IterationListenerRegistry* ilr) const
{
    if (inputFilepaths.empty())
        return;

    sqlite::enable_shared_cache(false);

    // get the set of distinct analyses in the input files
    DistinctAnalysisMap distinctAnalysisByFilepath;
    findDistinctAnalyses(inputFilepaths, distinctAnalysisByFilepath, skipSourceOnError, ilr);

    vector<ConstAnalysisPtr> distinctAnalyses;
    for(const DistinctAnalysisMap::value_type& nameAnalysisPair : distinctAnalysisByFilepath)
        if (find(distinctAnalyses.begin(), distinctAnalyses.end(), nameAnalysisPair.second) == distinctAnalyses.end())
            distinctAnalyses.push_back(nameAnalysisPair.second);

    // inform the caller about the distinct analyses and ask for databases and qonversion settings
    if (importSettingsCallback.get())
    {
        bool cancel = false;
        (*importSettingsCallback)(distinctAnalyses, cancel);
        if (cancel)
            return;
    }
    else
        throw runtime_error("[Parser::parse()] no import settings handler set");

    map<string, ProteomeDataPtr> proteinDatabaseByFilepath;
    for(const ConstAnalysisPtr& analysis : distinctAnalyses)
    {
        const string& proteinDatabaseFilepath = analysis->importSettings.proteinDatabaseFilepath;
        ProteomeDataPtr& proteomeDataPtr = proteinDatabaseByFilepath[proteinDatabaseFilepath];

        if (proteinDatabaseFilepath.empty())
            throw runtime_error("no protein database set");

        try
        {
            if (!bfs::exists(proteinDatabaseFilepath))
                throw runtime_error("file does not exist");
        }
        catch (runtime_error& e)
        {
            throw runtime_error("[Parser::parse()] error checking for protein database \"" + proteinDatabaseFilepath + "\": " + e.what());
        }

        try
        {
            if (!proteomeDataPtr.get())
            {
                using namespace pwiz::proteome;
                proteomeDataPtr.reset(new ProteomeDataFile(proteinDatabaseFilepath, true));
                if (proteomeDataPtr->proteinListPtr->size() <= 50000)
                    proteomeDataPtr->proteinListPtr.reset(new ProteinListCache(proteomeDataPtr->proteinListPtr,
                                                                               ProteinListCacheMode_MetaDataAndSequence,
                                                                               50000));
            }
        }
        catch (runtime_error& e)
        {
            throw runtime_error("[Parser::parse()] unable to open protein database \"" + proteinDatabaseFilepath + "\": " + e.what());
        }
    }

    vector<ProteinDatabaseTaskGroup> taskGroups = createTasksPerProteinDatabase(inputFilepaths,
                                                                                distinctAnalysisByFilepath,
                                                                                proteinDatabaseByFilepath,
                                                                                skipSourceOnError,
                                                                                maxThreads);

    // re-use the same in-memory databases because SQLite doesn't seem to let go of the memory after closing
    vector<shared_ptr<sqlite::database> > memoryDatabases;
    for (int i=0; i < maxThreads; ++i)
        memoryDatabases.push_back(shared_ptr<sqlite::database>(new sqlite::database(":memory:", sqlite::no_mutex)));

    for(const ProteinDatabaseTaskGroup& taskGroup : taskGroups)
        executeTaskGroup(taskGroup, distinctAnalysisByFilepath, memoryDatabases, skipSourceOnError, ilr);
}


void Parser::parse(const string& inputFilepath, int maxThreads, IterationListenerRegistry* ilr) const
{
    parse(vector<string>(1, inputFilepath), maxThreads, ilr);
}

string Parser::parseSource(const string& inputFilepath)
{
    IdentDataFile mzid(inputFilepath, 0, 0, true);

    string spectraDataName = mzid.dataCollection.inputs.spectraData[0]->name;
    if (spectraDataName.empty())
    {
        const string& location = mzid.dataCollection.inputs.spectraData[0]->location;
        spectraDataName = sourceNameFromFilename(bfs::path(location).filename().string());

        if (spectraDataName.empty())
            throw runtime_error("no spectrum source name or location");
    }
    else
        spectraDataName = Parser::sourceNameFromFilename(bfs::path(spectraDataName).filename().string());

    return spectraDataName;
}

bfs::path Parser::outputFilepath(const string& inputFilepath)
{
    if (bal::iends_with(inputFilepath, ".pep.xml"))
        return bal::ireplace_last_copy(inputFilepath, ".pep.xml", ".idpDB");
    return bfs::path(inputFilepath).replace_extension(".idpDB");
}

string Parser::sourceNameFromFilename(const string& filename)
{
    if (bal::iends_with(filename, ".pep.xml"))
        return bal::ireplace_last_copy(filename, ".pep.xml", "");
    else
        return bfs::path(filename).replace_extension("").string();
}


END_IDPICKER_NAMESPACE
