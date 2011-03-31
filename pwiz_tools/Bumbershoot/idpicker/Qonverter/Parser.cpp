//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//


#include "../Lib/SQLite/sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/data/common/diff_std.hpp"
#include "pwiz/data/mziddata/MzIdentMLFile.hpp"
#include "pwiz/data/mziddata/TextWriter.hpp"
#include "pwiz/data/proteome/ProteinListCache.hpp"
#include "pwiz/data/proteome/ProteomeDataFile.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "Parser.hpp"
#include "Qonverter.hpp"
#include "../freicore/AhoCorasickTrie.hpp"
#include "boost/foreach_field.hpp"


using namespace pwiz::mziddata;
using namespace pwiz::chemistry;
using namespace pwiz::util;
typedef IterationListener::UpdateMessage UpdateMessage;
namespace proteome = pwiz::proteome;
namespace sqlite = sqlite3pp;
using freicore::AhoCorasickTrie;


BEGIN_IDPICKER_NAMESPACE


typedef Parser::Analysis Analysis;
typedef Parser::AnalysisPtr AnalysisPtr;
typedef Parser::ConstAnalysisPtr ConstAnalysisPtr;



namespace {

struct SharedStringFastLessThan
{
    bool operator() (const boost::shared_ptr<string>& lhs, const boost::shared_ptr<string>& rhs) const
    {
        if (lhs->length() == rhs->length())
            return *lhs < *rhs;
        return lhs->length() < rhs->length();
    }
};

struct AminoAcidTranslator
{
    static size_t size() {return 26;}
    static size_t translate(char aa) {return aa - 'A';};
    static char translate(size_t index) {return static_cast<char>(index) + 'A';}
};

struct IsNotAnalysisParameter
{
    bool operator() (const pair<string, string>& parameter) const
    {
        return bal::starts_with(parameter.first, "PeakCounts:") ||
               bal::starts_with(parameter.first, "SearchStats:") ||
               bal::starts_with(parameter.first, "SearchTime:") ||
               parameter.first == "USEREMAIL" ||
               parameter.first == "USERNAME" ||
               parameter.first == "LICENSE" ||
               parameter.first == "COM";
    }
};

void parseAnalysis(const MzIdentMLFile& mzid, Analysis& analysis)
{
    SpectrumIdentification& si = *mzid.analysisCollection.spectrumIdentification[0];
    SpectrumIdentificationProtocol& sip = *si.spectrumIdentificationProtocolPtr;

    if (!sip.analysisSoftwarePtr.get() || sip.analysisSoftwarePtr->empty())
        throw runtime_error("no analysis software specified");

    // determine analysis software used
    CVParam searchEngine = sip.analysisSoftwarePtr->softwareName.cvParamChild(MS_analysis_software);
    if (!searchEngine.empty())
        analysis.softwareName = searchEngine.name();
    else if (!sip.analysisSoftwarePtr->softwareName.userParams.empty())
        analysis.softwareName = sip.analysisSoftwarePtr->softwareName.userParams[0].name;
    else
        throw runtime_error("[Parser::parseAnalysis()] analysis software could not be determined");

    if (si.searchDatabase.size() > 1)
        throw runtime_error("[Parser::parseAnalysis()] multi-database protocols are not supported");

    // determine search database used
    SearchDatabase& sd = *si.searchDatabase[0];
    analysis.importSettings.proteinDatabaseFilepath = sd.location;

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

    BOOST_FOREACH(const FilterPtr& filter, sip.databaseFilters)
    {
        cvParams.insert(cvParams.end(), filter->filterType.cvParams.begin(), filter->filterType.cvParams.end());
        cvParams.insert(cvParams.end(), filter->include.cvParams.begin(), filter->include.cvParams.end());
        cvParams.insert(cvParams.end(), filter->exclude.cvParams.begin(), filter->exclude.cvParams.end());
    }

    BOOST_FOREACH(const CVParam& cvParam, cvParams)
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

    // userParams are assumed to be uniquely keyed on name
    BOOST_FOREACH(const UserParam& userParam, userParams)
        analysis.parameters[userParam.name] = userParam.value;
    
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

// an analysis is distinct if its name is unique and it has at least one distinct parameter
typedef map<string, AnalysisPtr> DistinctAnalysisMap;
void findDistinctAnalyses(const vector<string>& inputFilepaths, DistinctAnalysisMap& distinctAnalyses)
{
    map<string, vector<AnalysisPtr> > sameNameAnalysesByName;
    BOOST_FOREACH(const string& filepath, inputFilepaths)
    {
        // ignore SequenceCollection and AnalysisData
        MzIdentMLFile mzid(filepath, 0, 0, true);

        AnalysisPtr analysis(new Analysis);
        parseAnalysis(mzid, *analysis);

        vector<AnalysisPtr>& sameNameAnalyses = sameNameAnalysesByName[analysis->name];
        AnalysisPtr sameAnalysis;

        // do a set union of the current analysis' parameters with every same name analysis;
        // if the union's size equals the current analysis' parameter size, the analysis is not distinct
        BOOST_FOREACH(AnalysisPtr& otherAnalysis, sameNameAnalyses)
        {
            vector<pair<string, string> > parameterUnion;
            std::set_union(otherAnalysis->parameters.begin(), otherAnalysis->parameters.end(),
                           analysis->parameters.begin(), analysis->parameters.end(),
                           std::back_inserter(parameterUnion));

            if (parameterUnion.size() == analysis->parameters.size())
            {
                sameAnalysis = otherAnalysis;
                break;
            }
        }

        if (!sameAnalysis.get())
        {
            sameNameAnalyses.push_back(analysis);
            sameAnalysis = sameNameAnalyses.back();
        }
        sameAnalysis->filepaths.push_back(filepath);
    }

    typedef pair<string, vector<AnalysisPtr> > SameNameAnalysesPair;
    BOOST_FOREACH(const SameNameAnalysesPair& itr, sameNameAnalysesByName)
    BOOST_FOREACH(const AnalysisPtr& analysis, itr.second)
    BOOST_FOREACH(const string& filepath, analysis->filepaths)
        distinctAnalyses[filepath] = analysis;
}


struct ParserImpl
{
    sqlite::database& idpDb;
    const IterationListenerRegistry& iterationListenerRegistry;

    map<boost::shared_ptr<string>, sqlite3_int64, SharedStringFastLessThan> distinctPeptideIdBySequence;
    map<double, sqlite3_int64> modIdByDeltaMass;

    ParserImpl(sqlite::database& idpDb, const IterationListenerRegistry& iterationListenerRegistry)
    : idpDb(idpDb), iterationListenerRegistry(iterationListenerRegistry)
    {
        initializeDatabase();
    }

    void initializeDatabase()
    {
        // optimize for bulk insertion
        idpDb.execute("PRAGMA journal_mode=OFF;"
                      "PRAGMA synchronous=OFF;"
                      "PRAGMA automatic_indexing=OFF;"
                      "PRAGMA default_cache_size=500000;"
                      "PRAGMA temp_store=MEMORY"
                     );

        sqlite::transaction transaction(idpDb);

        // initialize the tables
        idpDb.execute("CREATE TABLE SpectrumSource (Id INTEGER PRIMARY KEY, Name TEXT, URL TEXT, Group_ INT, MsDataBytes BLOB);"
                      "CREATE TABLE SpectrumSourceGroup (Id INTEGER PRIMARY KEY, Name TEXT);"
                      "CREATE TABLE SpectrumSourceGroupLink (Id INTEGER PRIMARY KEY, Source INT, Group_ INT);"
                      "CREATE TABLE Spectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC);"
                      "CREATE TABLE Analysis (Id INTEGER PRIMARY KEY, Name TEXT, SoftwareName TEXT, SoftwareVersion TEXT, Type INT, StartTime DATETIME);"
                      "CREATE TABLE AnalysisParameter (Id INTEGER PRIMARY KEY, Analysis INT, Name TEXT, Value TEXT);"
                      "CREATE TABLE Modification (Id INTEGER PRIMARY KEY, MonoMassDelta NUMERIC, AvgMassDelta NUMERIC, Formula TEXT, Name TEXT);"
                      "CREATE TABLE Protein (Id INTEGER PRIMARY KEY, Accession TEXT, Cluster INT, ProteinGroup TEXT, Length INT);"
                      "CREATE TABLE ProteinData (Id INTEGER PRIMARY KEY, Sequence TEXT);"
                      "CREATE TABLE ProteinMetadata (Id INTEGER PRIMARY KEY, Description TEXT);"
                      "CREATE TABLE Peptide (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC);"
                      "CREATE TABLE PeptideInstance (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);"
                      //"CREATE TABLE PeptideSequence (Id INTEGER PRIMARY KEY, Sequence TEXT);"
                      "CREATE TABLE PeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);"
                      "CREATE TABLE PeptideModification (Id INTEGER PRIMARY KEY, PeptideSpectrumMatch INT, Modification INT, Offset INT, Site TEXT);"
                      "CREATE TABLE PeptideSpectrumMatchScore (PsmId INTEGER NOT NULL, Value NUMERIC, ScoreNameId INTEGER NOT NULL, primary key (PsmId, ScoreNameId));"
                      "CREATE TABLE PeptideSpectrumMatchScoreName (Id INTEGER PRIMARY KEY, Name TEXT UNIQUE NOT NULL);"
                      "CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY);"
                      "CREATE TABLE LayoutProperty (Id INTEGER PRIMARY KEY, Name TEXT, PaneLocations TEXT, HasCustomColumnSettings INT);"
                      "CREATE TABLE ColumnProperty (Id INTEGER PRIMARY KEY, Scope TEXT, Name TEXT, Type TEXT, DecimalPlaces INT, ColorCode INT, Visible INT, Locked INT, Layout INT);"
                      "CREATE TABLE ProteinCoverage (Id INTEGER PRIMARY KEY, Coverage NUMERIC, CoverageMask BLOB);"
                     );
        transaction.commit();
    }

    void insertAnalysisMetadata(MzIdentMLFile& mzid)
    {
        if (mzid.analysisProtocolCollection.spectrumIdentificationProtocol.empty())
            throw runtime_error("no spectrum identification protocol");

        if (mzid.analysisProtocolCollection.spectrumIdentificationProtocol.size() > 1)
            throw runtime_error("more than one spectrum identification protocol not supported");

        SpectrumIdentificationProtocol& sip = *mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0];

        Analysis analysis;
        parseAnalysis(mzid, analysis);

        // insert the root group
        sqlite::command(idpDb, "INSERT INTO SpectrumSourceGroup (Id, Name) VALUES (1,'/')").execute();
        sqlite::command(idpDb, "INSERT INTO SpectrumSourceGroupLink (Id, Source, Group_) VALUES (1,1,1)").execute();

        // create commands for inserting file-level metadata (SpectrumSource, Analysis, AnalysisParameter)
        sqlite::command insertSpectrumSource(idpDb, "INSERT INTO SpectrumSource (Id, Name, URL, Group_, MsDataBytes) VALUES (?,?,?,1,null)");
        sqlite::command insertAnalysis(idpDb, "INSERT INTO Analysis (Id, Name, SoftwareName, SoftwareVersion, Type, StartTime) VALUES (?,?,?,?,?,?)");
        sqlite::command insertAnalysisParameter(idpDb, "INSERT INTO AnalysisParameter (Id, Analysis, Name, Value) VALUES (?,?,?,?)");

        string spectraDataName = mzid.dataCollection.inputs.spectraData[0]->name;
        if (spectraDataName.empty())
        {
            spectraDataName = bfs::path(mzid.dataCollection.inputs.spectraData[0]->location).replace_extension("").filename();
            if (spectraDataName.empty())
                throw runtime_error("no spectrum source name or location");
        }

        // insert file-level metadata into the database
        insertSpectrumSource.binder() << 1
                                      << spectraDataName
                                      << mzid.dataCollection.inputs.spectraData[0]->location;
        insertSpectrumSource.execute();

        insertAnalysis.binder() << 1
                                << analysis.name
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
    }

    void insertScoreNames(SpectrumIdentificationItemPtr& sii)
    {
        sqlite::command insertScoreName(idpDb, "INSERT INTO PeptideSpectrumMatchScoreName (Id, Name) VALUES (?,?)");

        sqlite3_int64 nextScoreId = 0;

        BOOST_FOREACH(CVParam& cvParam, sii->cvParams)
        {
            insertScoreName.binder() << ++nextScoreId << cvParam.name();
            insertScoreName.execute();
            insertScoreName.reset();
        }

        BOOST_FOREACH(UserParam& userParam, sii->userParams)
        {
            insertScoreName.binder() << ++nextScoreId << userParam.name;
            insertScoreName.execute();
            insertScoreName.reset();
        }
    }

    void insertSpectrumResults(MzIdentMLFile& mzid)
    {
        if (mzid.dataCollection.analysisData.spectrumIdentificationList.empty())
            throw runtime_error("no spectrum identification list");

        // create commands for inserting results
        sqlite::command insertSpectrum(idpDb, "INSERT INTO Spectrum (Id, Source, Index_, NativeID, PrecursorMZ) VALUES (?,1,?,?,?)");
        sqlite::command insertPeptide(idpDb, "INSERT INTO Peptide (Id, MonoisotopicMass, MolecularWeight) VALUES (?,?,?)");
        //sqlite::command insertPeptideSequence(idpDb, "INSERT INTO PeptideSequence (Id, Sequence) VALUES (?,?)");
        sqlite::command insertPSM(idpDb, "INSERT INTO PeptideSpectrumMatch (Id, Spectrum, Analysis, Peptide, QValue, MonoisotopicMass, MolecularWeight, MonoisotopicMassError, MolecularWeightError, Rank, Charge) VALUES (?,?,?,?,?,?,?,?,?,?,?)");
        sqlite::command insertPeptideModification(idpDb, "INSERT INTO PeptideModification (Id, PeptideSpectrumMatch, Modification, Offset, Site) VALUES (?,?,?,?,?)");
        sqlite::command insertModification(idpDb, "INSERT INTO Modification (Id, MonoMassDelta, AvgMassDelta, Formula, Name) VALUES (?,?,?,?,?)");
        sqlite::command insertScore(idpDb, "INSERT INTO PeptideSpectrumMatchScore (PsmId, Value, ScoreNameId) VALUES (?,?,?)");

        map<string, sqlite3_int64> distinctSpectra;
        //map<SearchModificationPtr, sqlite3_int64> modifications;

        SpectrumIdentificationList& sil = *mzid.dataCollection.analysisData.spectrumIdentificationList[0];

        sqlite3_int64 nextSpectrumId = 0, nextPeptideId = 0, nextPSMId = 0, nextPMId = 0, nextModId = 0;
        bool hasScoreNames = false;

        int iterationIndex = 0;
        BOOST_FOREACH(SpectrumIdentificationResultPtr& sir, sil.spectrumIdentificationResult)
        {
            if (iterationListenerRegistry.broadcastUpdateMessage(
                UpdateMessage(iterationIndex++,
                              sil.spectrumIdentificationResult.size(),
                              "writing spectrum results")) == IterationListener::Status_Cancel)
                return;

            // without an SII, precursor m/z is unknown, so empty results are skipped
            if (sir->spectrumIdentificationItem.empty())
                continue;

            // insert distinct spectrum
            nextSpectrumId = distinctSpectra.size() + 1;
            bool spectrumInserted = distinctSpectra.insert(make_pair(sir->spectrumID, nextSpectrumId)).second;
            if (!spectrumInserted)
                throw runtime_error("non-unique spectrumIDs not supported");

            double firstPrecursorMZ = sir->spectrumIdentificationItem[0]->experimentalMassToCharge;
            insertSpectrum.binder() << nextSpectrumId << nextSpectrumId << sir->spectrumID << firstPrecursorMZ;
            insertSpectrum.execute();
            insertSpectrum.reset();

            BOOST_FOREACH(SpectrumIdentificationItemPtr& sii, sir->spectrumIdentificationItem)
            {
                if (!sii->peptidePtr.get() || sii->peptidePtr->empty())
                    throw runtime_error("SII with a missing or empty peptide reference");

                // insert distinct peptide
                const string& sequence = sii->peptidePtr->peptideSequence;
                proteome::Peptide pwizPeptide(sequence);
                boost::shared_ptr<string> sharedSequence(new string(sequence));

                nextPeptideId = distinctPeptideIdBySequence.size() + 1;
                bool peptideInserted = distinctPeptideIdBySequence.insert(make_pair(sharedSequence, nextPeptideId)).second;
                if (peptideInserted)
                {
                    insertPeptide.binder() << nextPeptideId << pwizPeptide.monoisotopicMass() << pwizPeptide.molecularWeight();
                    insertPeptide.execute();
                    insertPeptide.reset();

                    /*insertPeptideSequence.binder() << nextPeptideId << sequence;
                    insertPeptideSequence.execute();
                    insertPeptideSequence.reset();*/
                }
                else
                    nextPeptideId = distinctPeptideIdBySequence[sharedSequence];

                ++nextPSMId;

                // insert modifications
                BOOST_FOREACH(ModificationPtr& mod, sii->peptidePtr->modification)
                {
                    ++nextPMId;

                    double modMass = mod->monoisotopicMassDelta > 0 ? mod->monoisotopicMassDelta
                                                                    : mod->avgMassDelta;

                    pair<map<double, sqlite3_int64>::iterator, bool> insertResult =
                        modIdByDeltaMass.insert(make_pair(modMass, 0));
                    if (insertResult.second)
                    {
                        insertResult.first->second = ++nextModId;
                        insertModification.binder() << nextModId
                                                    << mod->monoisotopicMassDelta
                                                    << mod->avgMassDelta
                                                    << "" // TODO: use Unimod
                                                    << "";
                        insertModification.execute();
                        insertModification.reset();
                    }

                    int offset = mod->location - 1;
                    if (offset < 0)
                        offset = INT_MIN;
                    else if (offset >= (int) sequence.length())
                        offset = INT_MAX;

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
                                                       << string(1, site);
                    insertPeptideModification.execute();
                    insertPeptideModification.reset();

                    pwizPeptide.modifications()[offset].push_back(proteome::Modification(mod->monoisotopicMassDelta, mod->avgMassDelta));
                }

                double precursorMass = Ion::neutralMass(sii->experimentalMassToCharge, sii->chargeState);

                // insert peptide spectrum match
                insertPSM.binder() << nextPSMId
                                   << nextSpectrumId
                                   << 1 // analysis
                                   << nextPeptideId
                                   << 2 // q value
                                   << precursorMass
                                   << precursorMass
                                   << precursorMass - pwizPeptide.monoisotopicMass()
                                   << precursorMass - pwizPeptide.molecularWeight()
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

                BOOST_FOREACH(CVParam& cvParam, sii->cvParams)
                {
                    insertScore.binder() << nextPSMId << cvParam.value << ++nextScoreId;
                    insertScore.execute();
                    insertScore.reset();
                }

                BOOST_FOREACH(UserParam& userParam, sii->userParams)
                {
                    insertScore.binder() << nextPSMId << userParam.value << ++nextScoreId;
                    insertScore.execute();
                    insertScore.reset();
                }
            }
        }
    }

    void insertPeptideInstances(MzIdentMLFile& mzid, const proteome::ProteomeData& pd)
    {
        typedef AhoCorasickTrie<AminoAcidTranslator> PeptideTrie;
        typedef map<boost::shared_ptr<string>, sqlite3_int64, SharedStringFastLessThan>::value_type PeptideIdPair;

        vector<boost::shared_ptr<string> > peptides;
        BOOST_FOREACH(const PeptideIdPair& pair, distinctPeptideIdBySequence)
            peptides.push_back(pair.first);

        sqlite::command insertProtein(idpDb, "INSERT INTO Protein (Id, Accession, Cluster, ProteinGroup, Length) VALUES (?,?,0,0,?)");
        sqlite::command insertProteinData(idpDb, "INSERT INTO ProteinData (Id, Sequence) VALUES (?,?)");
        sqlite::command insertProteinMetadata(idpDb, "INSERT INTO ProteinMetadata (Id, Description) VALUES (?,?)");
        sqlite::command insertPeptideInstance(idpDb, "INSERT INTO PeptideInstance (Id, Protein, Peptide, Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages) VALUES (?,?,?,?,?,?,?,?)");

        PeptideTrie peptideTrie(peptides.begin(), peptides.end());

        sqlite3_int64 nextProteinId = 0, nextPeptideInstanceId = 0;
        int maxProteinLength = 0;

        proteome::ProteinListPtr pl = pd.proteinListPtr;
        for (size_t i=0; i < pl->size(); ++i)
        {
            if (iterationListenerRegistry.broadcastUpdateMessage(UpdateMessage(i, pl->size(), "writing peptide instances")) == IterationListener::Status_Cancel)
                return;

            proteome::ProteinPtr protein = pl->protein(i);
            proteome::Digestion::Config digestionConfig(100000, 0, 100000, proteome::Digestion::NonSpecific);
            proteome::Digestion digestion(*protein, MS_Trypsin_P, digestionConfig); // TODO: use the right enzyme
            maxProteinLength = max((int) protein->sequence().length(), maxProteinLength);

            vector<PeptideTrie::SearchResult> peptideInstances = peptideTrie.find_all(protein->sequence());

            if (peptideInstances.empty())
                continue;

            insertProtein.binder() << ++nextProteinId << protein->id << (int) protein->sequence().length();
            insertProtein.execute();
            insertProtein.reset();

            insertProteinData.binder() << nextProteinId << protein->sequence();
            insertProteinData.execute();
            insertProteinData.reset();

            insertProteinMetadata.binder() << nextProteinId << protein->description;
            insertProteinMetadata.execute();
            insertProteinMetadata.reset();

            BOOST_FOREACH(PeptideTrie::SearchResult& instance, peptideInstances)
            {
                // calculate terminal specificity and missed cleavages
                proteome::DigestedPeptide peptide = digestion.find_first(*instance.keyword(), instance.offset());

                insertPeptideInstance.binder() << ++nextPeptideInstanceId
                                               << nextProteinId
                                               << distinctPeptideIdBySequence[instance.keyword()]
                                               << (int) instance.offset()
                                               << (int) instance.keyword()->length()
                                               << peptide.NTerminusIsSpecific()
                                               << peptide.CTerminusIsSpecific()
                                               << (int) peptide.missedCleavages();
                insertPeptideInstance.execute();
                insertPeptideInstance.reset();
            }
        }

        sqlite::command insertIntegerSet(idpDb, "INSERT INTO IntegerSet (Value) VALUES (?)");
        for (int i=1; i <= maxProteinLength; ++i)
        {
            insertIntegerSet.binder() << i;
            insertIntegerSet.execute();
            insertIntegerSet.reset();
        }
    }


    void createIndexes()
    {
        idpDb.execute("CREATE UNIQUE INDEX Protein_Accession ON Protein (Accession);"
                      "CREATE INDEX PeptideInstance_Peptide ON PeptideInstance (Peptide);"
                      "CREATE INDEX PeptideInstance_Protein ON PeptideInstance (Protein);"
                      "CREATE INDEX PeptideInstance_PeptideProtein ON PeptideInstance (Peptide, Protein);"
                      "CREATE UNIQUE INDEX PeptideInstance_ProteinOffsetLength ON PeptideInstance (Protein, Offset, Length);"
                      "CREATE UNIQUE INDEX SpectrumSourceGroupLink_SourceGroup ON SpectrumSourceGroupLink (Source, Group_);"
                      "CREATE INDEX Spectrum_SourceIndex ON Spectrum (Source, Index_);"
                      "CREATE UNIQUE INDEX Spectrum_SourceNativeID ON Spectrum (Source, NativeID);"
                      "CREATE INDEX PeptideSpectrumMatch_Analysis ON PeptideSpectrumMatch (Analysis);"
                      "CREATE INDEX PeptideSpectrumMatch_Peptide ON PeptideSpectrumMatch (Peptide);"
                      "CREATE INDEX PeptideSpectrumMatch_Spectrum ON PeptideSpectrumMatch (Spectrum);"
                      "CREATE INDEX PeptideSpectrumMatch_QValue ON PeptideSpectrumMatch (QValue);"
                      "CREATE INDEX PeptideSpectrumMatch_Rank ON PeptideSpectrumMatch (Rank);"
                      "CREATE INDEX PeptideModification_PeptideSpectrumMatch ON PeptideModification (PeptideSpectrumMatch);"
                      "CREATE INDEX PeptideModification_Modification ON PeptideModification (Modification);"
                     );
    }

    void applyQValueFilter(const Analysis& analysis, double qValueThreshold)
    {
        const Qonverter::Settings& settings = analysis.importSettings.qonverterSettings;

        // write QonverterSettings for preqonvert;
        // assemble scoreInfo string ("Weight Order NormalizationMethod ScoreName")
        vector<string> scoreInfoStrings;
        BOOST_FOREACH_FIELD((const string& name)(const Qonverter::Settings::ScoreInfo& scoreInfo), settings.scoreInfoByName)
        {
            ostringstream ss;
            ss << scoreInfo.weight << " "
               << scoreInfo.order << " "
               << scoreInfo.normalizationMethod << " "
               << name;
            scoreInfoStrings.push_back(ss.str());
        }
        string scoreInfo = bal::join(scoreInfoStrings, ";");

        idpDb.execute("CREATE TABLE QonverterSettings (Id INTEGER PRIMARY KEY,"
                      "                                QonverterMethod INT,"
                      "                                DecoyPrefix TEXT,"
                      "                                RerankMatches INT,"
                      "                                Kernel INT,"
                      "                                MassErrorHandling INT,"
                      "                                MissedCleavagesHandling INT,"
                      "                                TerminalSpecificityHandling INT,"
                      "                                ChargeStateHandling INT,"
                      "                                ScoreInfoByName TEXT);");

        sqlite::command insertQonverterSettings(idpDb, "INSERT INTO QonverterSettings VALUES (1,?,?,?,?,?,?,?,?,?)");
        insertQonverterSettings.binder() << (int) settings.qonverterMethod.index()
                                         << settings.decoyPrefix
                                         << (settings.rerankMatches ? 1 : 0)
                                         << (int) settings.kernel.index()
                                         << (int) settings.massErrorHandling.index()
                                         << (int) settings.missedCleavagesHandling.index()
                                         << (int) settings.terminalSpecificityHandling.index()
                                         << (int) settings.chargeStateHandling.index()
                                         << scoreInfo;
        insertQonverterSettings.execute();

        Qonverter qonverter;
        //qonverter.logQonversionDetails = true;
        qonverter.settingsByAnalysis[0] = settings;
        qonverter.qonvert(idpDb.connected());

        sqlite::transaction transaction(idpDb);

        const char* sql =
            // Apply a broad QValue filter on top-ranked PSMs
            "DELETE FROM PeptideSpectrumMatch WHERE QValue > %f AND Rank = 1;"

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
            //"DELETE FROM PeptideSequence WHERE Id NOT IN (SELECT Id FROM Peptide);"
            "DELETE FROM PeptideInstance WHERE Peptide NOT IN (SELECT Id FROM Peptide);"
            "DELETE FROM Protein WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);";

        idpDb.executef(sql, qValueThreshold);

        transaction.commit();

        idpDb.execute("VACUUM");
    }
};

} // namespace


Parser::Analysis::Analysis() : startTime(bdt::not_a_date_time) {}


void Parser::ImportSettingsCallback::operator() (const vector<ConstAnalysisPtr>& distinctAnalyses, bool& cancel) const
{
    throw runtime_error("[Parser::parse()] no import settings handler set");
}


void Parser::parse(const vector<string>& inputFilepaths) const
{
    if (inputFilepaths.empty())
        return;

    // get the set of distinct analyses in the input files
    DistinctAnalysisMap distinctAnalysisByFilepath;
    findDistinctAnalyses(inputFilepaths, distinctAnalysisByFilepath);

    vector<ConstAnalysisPtr> distinctAnalyses;
    BOOST_FOREACH(const DistinctAnalysisMap::value_type& nameAnalysisPair, distinctAnalysisByFilepath)
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
        throw runtime_error("Parser::parse()] no import settings handler set");

    typedef boost::shared_ptr<proteome::ProteomeData> ProteomeDataPtr;
    map<string, ProteomeDataPtr> proteinDatabaseByFilepath;
    BOOST_FOREACH(const ConstAnalysisPtr& analysis, distinctAnalyses)
    {
        const string& proteinDatabaseFilepath = analysis->importSettings.proteinDatabaseFilepath;
        ProteomeDataPtr& proteomeDataPtr = proteinDatabaseByFilepath[proteinDatabaseFilepath];

        try
        {
            if (!proteomeDataPtr.get())
            {
                using namespace pwiz::proteome;
                proteomeDataPtr.reset(new ProteomeDataFile(proteinDatabaseFilepath));
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

    BOOST_FOREACH(const string& inputFilepath, inputFilepaths)
    {
        if (distinctAnalysisByFilepath.count(inputFilepath) == 0)
            throw runtime_error("[Parser::parse()] unable to find analysis for file \"" + inputFilepath + "\"");

        string idpDbFilepath = bfs::path(inputFilepath).replace_extension(".idpDB").string();

        if (bfs::exists(idpDbFilepath))
        {
            // for now, abort; eventually we want to merge? here
            continue;
        }

        // create an in-memory database
        sqlite::database idpDb(":memory:");

        // read the mzid document into memory
        iterationListenerRegistry.broadcastUpdateMessage(UpdateMessage(0, 0, "opening " + bfs::path(inputFilepath).replace_extension("").filename()));
        MzIdentMLFile mzid(inputFilepath, 0, &iterationListenerRegistry);

        ParserImpl parser(idpDb, iterationListenerRegistry);
        
        sqlite::transaction transaction(idpDb);

        parser.insertAnalysisMetadata(mzid);

        //iterationListenerRegistry.broadcastUpdateMessage(UpdateMessage(0, 0, "writing spectrum results"));
        parser.insertSpectrumResults(mzid);

        //iterationListenerRegistry.broadcastUpdateMessage(UpdateMessage(0, 0, "writing peptide instances"));
        const AnalysisPtr& analysis = distinctAnalysisByFilepath[inputFilepath];
        const string& proteinDatabaseFilepath = analysis->importSettings.proteinDatabaseFilepath;
        const ProteomeDataPtr& proteomeDataPtr = proteinDatabaseByFilepath[proteinDatabaseFilepath];
        parser.insertPeptideInstances(mzid, *proteomeDataPtr);

        iterationListenerRegistry.broadcastUpdateMessage(UpdateMessage(0, 0, "creating indexes"));
        parser.createIndexes();

        transaction.commit();

        // run preqonvert if import settings specify it
        iterationListenerRegistry.broadcastUpdateMessage(UpdateMessage(0, 0, "qonverting"));
        parser.applyQValueFilter(*analysis, 0.25);

        iterationListenerRegistry.broadcastUpdateMessage(UpdateMessage(0, 0, "saving database"));
        idpDb.save_to_file(idpDbFilepath.c_str());
    }
}


void Parser::parse(const string& inputFilepath) const
{
    parse(vector<string>(1, inputFilepath));
}


END_IDPICKER_NAMESPACE
