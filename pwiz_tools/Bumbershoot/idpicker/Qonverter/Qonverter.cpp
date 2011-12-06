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
// Contributor(s): Surendra Dasari
//


#include "../Lib/SQLite/sqlite3pp.h"
#include "pwiz/utility/misc/Std.hpp"
#include "Qonverter.hpp"
#include "StaticWeightQonverter.hpp"
#include "SVMQonverter.hpp"
#include "boost/foreach_field.hpp"


#define CHECK_SQLITE_RESULT(x) \
    { /* anonymous scope to prevent name conflicts */ \
        char* errorBuf = NULL; \
        int rc = (x); \
        if (rc != SQLITE_OK && rc != SQLITE_DONE && rc != SQLITE_ROW) \
        { \
            string error; \
            if (errorBuf) \
            { \
                error = errorBuf; \
                sqlite3_free(errorBuf); \
            } \
            throw runtime_error(string("[SQLite (") + __FILE__ + ":" + lexical_cast<string>(__LINE__) + ")] " + error); \
        } \
    }


//namespace {

using namespace IDPICKER_NAMESPACE;
namespace sqlite = sqlite3pp;

const static string defaultDecoyPrefix = "rev_";

typedef pair<string, string> AnalysisSourcePair;

int getAnalysisSourcePairs(void* data, int columnCount, char** columnValues, char** columnNames)
{
    if (columnCount != 3)
        throw runtime_error("[Qonverter::getAnalysisSourcePairs] result must have 3 columns");

    vector<AnalysisSourcePair>* pairs = static_cast<vector<AnalysisSourcePair>*>(data);
    pairs->push_back(make_pair(columnValues[0], columnValues[1]));

    return 0;
}

int getScoreNames(void* data, int columnCount, char** columnValues, char** columnNames)
{
    if (columnCount != 1)
        throw runtime_error("[Qonverter::getScoreNames] result must have 1 column");

    if (columnValues[0] != NULL)
    {
        string scoreNames = bal::to_lower_copy(string(columnValues[0]));
        bal::split(*static_cast<vector<string>*>(data), scoreNames, bal::is_any_of(","));
    }

    return 0;
}

int verifyDecoyPrefixOccurs(void* data, int columnCount, char** columnValues, char** columnNames)
{
    if (columnCount != 1 || columnValues[0] == NULL)
        throw runtime_error("[Qonverter::verifyDecoyPrefixOccurs] result must have 1 columns");

    if (lexical_cast<int>(columnValues[0]) == 0)
        *static_cast<bool*>(data) = false;

    return 0;
}

struct PsmRowReader
{
    PSMList psmRows;

    PsmRowReader(const vector<string>& scoreIdSet) : scoresSize(scoreIdSet.size()) {}

    void read(sqlite::database& db, const string& sql)
    {
        sqlite::query psmQuery(db, sql.c_str());

        BOOST_FOREACH(sqlite::query::rows row, psmQuery)
        {
            psmRows.push_back(new PeptideSpectrumMatch);
            PeptideSpectrumMatch& psm = psmRows.back();

            int decoyState;
            sqlite::query::rows::getstream psmGetter = row.getter();
            psmGetter >> psm.id
                      >> psm.spectrum
#ifdef QONVERTER_HAS_NATIVEID
                      >> psm.nativeID
#endif
                      >> psm.originalRank
                      >> decoyState
                      >> psm.chargeState
                      >> psm.bestSpecificity
                      >> psm.missedCleavages
                      >> psm.massError;

            switch (decoyState)
            {
                case 0: psm.decoyState = DecoyState::Target; break;
                case 1: psm.decoyState = DecoyState::Decoy; break;
                case 2: psm.decoyState = DecoyState::Ambiguous; break;
                default: throw runtime_error("[PsmRowReader::read] query returned invalid decoy state");
            }

            psm.scores.resize(scoresSize);
            for (size_t i=0; i < scoresSize; ++i)
                psmGetter >> psm.scores[i];

            psm.totalScore = 0;
            psm.qValue = 2;
        }
    }

    private:
    size_t scoresSize;
};

void validateSettings(const Qonverter::Settings& settings)
{
    // sanity checks
    if (settings.decoyPrefix.empty()) throw runtime_error("[Qonverter::validateSettings] no decoy prefix");
    if (settings.scoreInfoByName.empty()) throw runtime_error("[Qonverter::validateSettings] no score info");

    // check that qonverter method agrees with non-score handling
    if (settings.qonverterMethod == Qonverter::QonverterMethod::StaticWeighted)
    {
        if (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Feature])
            throw runtime_error("[Qonverter::validateSettings] charge state can only be used as a feature with the SVM qonverter");
        if (settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Feature])
            throw runtime_error("[Qonverter::validateSettings] terminal specificity can only be used as a feature with the SVM qonverter");
        if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
            throw runtime_error("[Qonverter::validateSettings] missed cleavages can only be used as a feature with the SVM qonverter");
        if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
            throw runtime_error("[Qonverter::validateSettings] mass error can only be used as a feature with the SVM qonverter");
    }
}

void updatePsmRows(sqlite::database& db, bool logQonversionDetails, const PSMList& psmRows)
{
    sqlite::transaction transaction(db);

#ifdef QONVERTER_HAS_NATIVEID
#define SPECTRUM_ID nativeID
#define SPECTRUM_ID_STR "NativeID"
#else
#define SPECTRUM_ID spectrum
#define SPECTRUM_ID_STR "Spectrum"
#endif

    if (logQonversionDetails)
    {
        sqlite::command(db, "DROP TABLE IF EXISTS QonversionDetails").execute();
        sqlite::command(db, "CREATE TABLE QonversionDetails (PsmId, "SPECTRUM_ID_STR", OriginalRank, NewRank, Charge, BestSpecificity, TotalScore, QValue, FDRScore, DecoyState)").execute();
        sqlite::command insertQonversionDetails(db, "INSERT INTO QonversionDetails VALUES (?,?,?,?,?,?,?,?,?,?)");
        BOOST_FOREACH(const PeptideSpectrumMatch& row, psmRows)
        {
            insertQonversionDetails.binder() << row.id
                                             << row.SPECTRUM_ID
                                             << row.originalRank
                                             << row.newRank
                                             << row.chargeState
                                             << row.bestSpecificity
                                             << row.totalScore
                                             << row.qValue
                                             << row.fdrScore
                                             << DecoyState::Symbol[row.decoyState];
            insertQonversionDetails.execute();
            insertQonversionDetails.reset();
        }
    }

    // update QValue column for each top-ranked PSM (non-top-ranked PSMs keep the default QValue)
    sqlite::command updatePSM(db, "UPDATE PeptideSpectrumMatch SET QValue = ? WHERE Id = ?");
    BOOST_FOREACH(const PeptideSpectrumMatch& row, psmRows)
    {
        updatePSM.binder() << row.fdrScore << row.id;
        updatePSM.execute();
        updatePSM.reset();
    }

    transaction.commit();
}

//} // namespace


BEGIN_IDPICKER_NAMESPACE


Qonverter::Qonverter()
{
    logQonversionDetails = false;
}

void Qonverter::qonvert(const string& idpDbFilepath, const ProgressMonitor& progressMonitor)
{
    sqlite::database db(idpDbFilepath, sqlite::no_mutex, sqlite::read_write);

    db.execute("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA cache_size=50000");

    qonvert(db.connected(), progressMonitor);
}

void Qonverter::qonvert(sqlite3* dbPtr, const ProgressMonitor& progressMonitor)
{
    // do not disconnect on close
    sqlite::database db(dbPtr, false);

    string sql;

    // get the set of distinct analysis/source pairs
    sql = //"CREATE TEMP TABLE MostSpecificInstance (Peptide INTEGER PRIMARY KEY, Specificity INT);"
          //"INSERT INTO MostSpecificInstance SELECT Peptide, MAX(NTerminusIsSpecific+CTerminusIsSpecific) FROM PeptideInstance GROUP BY Peptide;"
          //"SELECT psm.Analysis, s.Source, psm.Charge, mostSpecificInstance.Specificity, COUNT(DISTINCT psm.Id) "
          "SELECT psm.Analysis, s.Source, COUNT(DISTINCT psm.Id) "
          "FROM PeptideSpectrumMatch psm "
          //"JOIN MostSpecificInstance mostSpecificInstance ON psm.Peptide=mostSpecificInstance.Peptide "
          "JOIN Spectrum s ON psm.Spectrum=s.Id "
          "WHERE psm.QValue > 1 AND Rank = 1 "
          //(rerankMatches ? string() : string("AND Rank = 1 ")) +
          "GROUP BY psm.Analysis, s.Source";

    vector<AnalysisSourcePair> analysisSourcePairs;
    CHECK_SQLITE_RESULT(sqlite3_exec(dbPtr, sql.c_str(), getAnalysisSourcePairs, &analysisSourcePairs, &errorBuf));

    // send initial progress update to indicate how many qonversion steps there are
    ProgressMonitor::UpdateMessage updateMessage;
    updateMessage.qonvertedAnalyses = 0;
    updateMessage.totalAnalyses = analysisSourcePairs.size();
    updateMessage.cancel = false;
    progressMonitor(updateMessage);
    if (updateMessage.cancel)
        return;

    // validate global settings
    if (settingsByAnalysis.count(0) > 0)
        validateSettings(settingsByAnalysis[0]);

    // validate settings for each analysis/source pair
    BOOST_FOREACH(const AnalysisSourcePair& analysisSourcePair, analysisSourcePairs)
    {
        const string& analysisId = analysisSourcePair.first;
        const string& spectrumSourceId = analysisSourcePair.second;

        // if no settings are provided for this analysis, try to use the global settings (id == 0)
        int analysis = lexical_cast<int>(analysisId);
        if (settingsByAnalysis.count(analysis) == 0)
        {
            if (settingsByAnalysis.count(0) == 0)
                throw runtime_error("[Qonverter::Qonvert] no global or analysis-specific qonverter settings for analysis " + analysisId);
            settingsByAnalysis[analysis] = settingsByAnalysis[0];
        }
        else
            validateSettings(settingsByAnalysis[analysis]);
    }

    // qonvert each analysis/source pair independently
    BOOST_FOREACH(const AnalysisSourcePair& analysisSourcePair, analysisSourcePairs)
    {
        const string& analysisId = analysisSourcePair.first;
        const string& spectrumSourceId = analysisSourcePair.second;
        const Settings& qonverterSettings = settingsByAnalysis[lexical_cast<int>(analysisId)];
        const string& decoyPrefix = qonverterSettings.decoyPrefix;
        bool rerankMatches = qonverterSettings.rerankMatches;
        const map<string, Settings::ScoreInfo>& scoreInfoByName = qonverterSettings.scoreInfoByName;

        sql = "SELECT Name FROM SpectrumSource WHERE Id=" + spectrumSourceId;
        const string& sourceName = sqlite::query(db, sql.c_str()).begin()->get<string>(0);

        // verify that the decoyPrefix occurs in the PSM subset
        sql = "SELECT COUNT(*) "
              "FROM Spectrum s "
              "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
              "JOIN PeptideSpectrumMatch psm ON s.Id=psm.Spectrum "
              "JOIN Protein pro ON pi.Protein=pro.Id "
              "WHERE s.Source=" + spectrumSourceId +
              "  AND psm.Analysis=" + analysisId +
              "  AND pro.Accession LIKE '" + decoyPrefix + "%'";

        bool decoyPrefixOccurs = true;
        CHECK_SQLITE_RESULT(sqlite3_exec(dbPtr, sql.c_str(), verifyDecoyPrefixOccurs, &decoyPrefixOccurs, &errorBuf));
        if (!decoyPrefixOccurs)
            throw runtime_error("[Qonverter::Qonvert] decoy prefix '" + decoyPrefix + "' does not occur in analysis " + analysisId);

        // get the set of expected score names
        set<string> expectedScoreNames;
        typedef pair<string, Settings::ScoreInfo> ScoreInfoPair;
        BOOST_FOREACH(const ScoreInfoPair& itr, scoreInfoByName)
            expectedScoreNames.insert(bal::to_lower_copy(itr.first));

        // get the set of actual score ids and names
        sql = "SELECT GROUP_CONCAT(scoreName.Name || ';' || scoreName.Id) "
              "FROM PeptideSpectrumMatchScore psmScore "
              "JOIN PeptideSpectrumMatchScoreName scoreName ON ScoreNameId=scoreName.Id "
              "JOIN (SELECT psm.Id "
              "      FROM PeptideSpectrumMatch psm "
              "      JOIN Spectrum s ON Spectrum=s.Id "
              "      WHERE s.Source=" + spectrumSourceId +
              "        AND psm.Analysis=" + analysisId +
              (rerankMatches ? "" : " AND Rank = 1 ") +
              "      LIMIT 1) AS psm ON psmScore.PsmId=psm.Id";

        vector<string> actualScoreIdNamePairs; // the order of scores is important!
        vector<string> actualScoreNames;
        map<string, string> actualScoreIdByName;

        CHECK_SQLITE_RESULT(sqlite3_exec(dbPtr, sql.c_str(), getScoreNames, &actualScoreIdNamePairs, &errorBuf));

        BOOST_FOREACH(const string& idName, actualScoreIdNamePairs)
        {
            vector<string> idNamePair;
            bal::split(idNamePair, idName, bal::is_any_of(";"));
            actualScoreNames.push_back(idNamePair[0]);
            actualScoreIdByName[idNamePair[0]] = idNamePair[1];
        }

        if (actualScoreNames.empty())
        {
            ++updateMessage.qonvertedAnalyses;
            progressMonitor(updateMessage);
            if (updateMessage.cancel)
                return;
            
            throw runtime_error("[Qonverter::Qonvert] no scores in the PSMs of analysis " + analysisId);
        }

        sort(actualScoreNames.begin(), actualScoreNames.end()); // set_intersection input must be sorted

        // the intersection between the sets is used as a SQL condition
        vector<string> scoreNameIntersection;
        set_intersection(actualScoreNames.begin(), actualScoreNames.end(),
                         expectedScoreNames.begin(), expectedScoreNames.end(),
                         std::back_inserter(scoreNameIntersection));

        if (scoreNameIntersection.empty())
        {
            string expectedScores = "(" + bal::join(expectedScoreNames, ", ") + ")";
            string actualScores = "(" + bal::join(actualScoreNames, ", ") + ")";
            throw runtime_error("[Qonverter::Qonvert] expected scores " + expectedScores +
                                " do not match the actual scores " + actualScores +
                                " of analysis " + analysisId);
        }

        // get the set of weights to use for calculating each PSM's total score;
        // if an actual score is not mapped in scoreWeights, it gets the default value of 0
        vector<double> scoreWeightsVector;
        vector<string> scoreIdSet;
        vector<string> scoreNamesInIdOrder;
        BOOST_FOREACH(const string& name, scoreNameIntersection)
        {
            const Settings::ScoreInfo& scoreInfo = scoreInfoByName.find(name)->second;
            scoreWeightsVector.push_back(scoreInfo.order == Settings::Order::Ascending ? scoreInfo.weight : -scoreInfo.weight);
            scoreIdSet.push_back(actualScoreIdByName[name]);
            scoreNamesInIdOrder.push_back(name);
        }

        // e.g. "score1.Value, score2.Value, score3.Value"
        string scoreSelects = "score" + bal::join(scoreIdSet, ".Value, score") + ".Value ";

        string scoreJoins;
        BOOST_FOREACH(const string& id, scoreIdSet)
        {
            // e.g. "JOIN PeptideSpectrumMatchScore scoreX ON psm.Id=scoreX.PsmId AND X=scoreX.ScoreNameId "
            scoreJoins += "JOIN PeptideSpectrumMatchScore score" + id +
                          " ON psm.Id=score" + id + ".PsmId"
                          " AND " + id + "=score" + id + ".ScoreNameId ";
        }

        sql = "SELECT psm.Id, psm.Spectrum, "
#ifdef QONVERTER_HAS_NATIVEID
              "s.NativeID, "
#endif
              "psm.Rank, "
              "       CASE WHEN SUM(DISTINCT CASE WHEN pro.Accession LIKE '" + decoyPrefix + "%' THEN 1 ELSE 0 END) + SUM(DISTINCT CASE WHEN pro.Accession NOT LIKE '" + decoyPrefix + "%' THEN 1 ELSE 0 END) = 2 THEN 2 " +
              "            ELSE SUM(DISTINCT CASE WHEN pro.Accession LIKE '" + decoyPrefix + "%' THEN 1 ELSE 0 END) " +
              "            END AS DecoyState, "
              "       psm.Charge, "
              "       MAX(NTerminusIsSpecific+CTerminusIsSpecific), "
              "       MissedCleavages, "
              "       ABS(MonoisotopicMassError), " +
              scoreSelects +
              "FROM Spectrum s "
              "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
              "JOIN PeptideSpectrumMatch psm ON s.Id=psm.Spectrum " +
              scoreJoins +
              "JOIN Protein pro ON pi.Protein=pro.Id "
              "WHERE s.Source=" + spectrumSourceId +
              "  AND psm.Analysis=" + analysisId +
              //(rerankMatches ? "" : " AND Rank = 1 ") +
              " GROUP BY psm.Id";

        PsmRowReader psmRowReader(scoreIdSet);
        psmRowReader.read(db, sql.c_str());

        // normalize scores (according to qonverterSettings)
        normalize(qonverterSettings, psmRowReader.psmRows);

        switch (qonverterSettings.qonverterMethod.index())
        {
            default:
            case Qonverter::QonverterMethod::StaticWeighted:
                StaticWeightQonverter::Qonvert(psmRowReader.psmRows, qonverterSettings, scoreWeightsVector);
                break;
            case Qonverter::QonverterMethod::PartitionedSVM:
            case Qonverter::QonverterMethod::SingleSVM:
            //case Qonverter::QonverterMethod::SVM:
                SVMQonverter::Qonvert(sourceName, scoreNamesInIdOrder, psmRowReader.psmRows, qonverterSettings);
                break;
        }

        // update the database with the new Q values
        updatePsmRows(db, logQonversionDetails, psmRowReader.psmRows);

        ++updateMessage.qonvertedAnalyses;
        progressMonitor(updateMessage);
        if (updateMessage.cancel)
            return;
    }
}


void Qonverter::reset(const string& idpDbFilepath)
{
    sqlite::database db(idpDbFilepath, sqlite::no_mutex, sqlite::read_write);

    db.execute("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF");

    reset(db.connected());
}

void Qonverter::reset(sqlite3* idpDb)
{
    // drop FilteringCriteria
    CHECK_SQLITE_RESULT(sqlite3_exec(idpDb, "DROP TABLE IF EXISTS FilteringCriteria", NULL, NULL, &errorBuf));

    // drop old QonversionDetails
    CHECK_SQLITE_RESULT(sqlite3_exec(idpDb, "DROP TABLE IF EXISTS QonversionDetails", NULL, NULL, &errorBuf));

    // drop Filtered* tables
    string dropFilteredTables = "DROP TABLE IF EXISTS FilteredProtein;"
                                "DROP TABLE IF EXISTS FilteredPeptideInstance;"
                                "DROP TABLE IF EXISTS FilteredPeptide;"
                                "DROP TABLE IF EXISTS FilteredPeptideSpectrumMatch";
    CHECK_SQLITE_RESULT(sqlite3_exec(idpDb, dropFilteredTables.c_str(), NULL, NULL, &errorBuf));

    // restore Unfiltered* tables as the main tables
    try
    {
        // if unfiltered tables have not been created, this will throw and skip the rest of the block
        CHECK_SQLITE_RESULT(sqlite3_exec(idpDb, "SELECT Id FROM UnfilteredProtein LIMIT 1", NULL, NULL, &errorBuf));

        // drop filtered tables and rename unfiltered tables
        string renameUnfilteredTables = "DROP TABLE IF EXISTS Protein;"
                                        "DROP TABLE IF EXISTS PeptideInstance;"
                                        "DROP TABLE IF EXISTS Peptide;"
                                        "DROP TABLE IF EXISTS PeptideSpectrumMatch;"
                                        "ALTER TABLE UnfilteredProtein RENAME TO Protein;"
                                        "ALTER TABLE UnfilteredPeptideInstance RENAME TO PeptideInstance;"
                                        "ALTER TABLE UnfilteredPeptide RENAME TO Peptide;"
                                        "ALTER TABLE UnfilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch";
        CHECK_SQLITE_RESULT(sqlite3_exec(idpDb, renameUnfilteredTables.c_str(), NULL, NULL, &errorBuf));
    }
    catch (runtime_error&)
    {
    }

    // reset Q values
    CHECK_SQLITE_RESULT(sqlite3_exec(idpDb, "UPDATE PeptideSpectrumMatch SET QValue = 2", NULL, NULL, &errorBuf));
}


vector<PSMIteratorRange> partition(const Qonverter::Settings& settings, const PSMIteratorRange& psmRows)
{
    if (!settings.chargeStateHandling[Qonverter::ChargeStateHandling::Partition] &&
        !settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Partition])
        return vector<PSMIteratorRange>(1, PSMIteratorRange(psmRows.begin(), psmRows.end()));

    vector<PSMIteratorRange> psmPartitionedRows;

    if (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Partition] &&
        settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Partition])
    {
        sort(psmRows.begin(), psmRows.end(), ChargeAndSpecificityLessThan());

        // split the matches into a range for each charge state and terminal specificity
        PSMIterator begin = psmRows.begin(), cur = begin;
        while (cur != psmRows.end())
        {
            float lastCharge = cur->chargeState;
            float lastSpecificity = cur->bestSpecificity;
            ++cur;
            if (cur == psmRows.end() ||
                cur->chargeState != lastCharge ||
                cur->bestSpecificity != lastSpecificity)
            {
                psmPartitionedRows.push_back(PSMIteratorRange(begin, cur));
                begin = cur;
            }
        }
    }
    else if (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Partition])
    {
        sort(psmRows.begin(), psmRows.end(), ChargeStateLessThan());

        // split the matches into a range for each charge state
        PSMIterator begin = psmRows.begin(), cur = begin;
        while (cur != psmRows.end())
        {
            float lastCharge = cur->chargeState;
            ++cur;
            if (cur == psmRows.end() || cur->chargeState != lastCharge)
            {
                psmPartitionedRows.push_back(PSMIteratorRange(begin, cur));
                begin = cur;
            }
        }
    }
    else
    {
        sort(psmRows.begin(), psmRows.end(), SpecificityBetterThan());

        // split the matches into a range for each terminal specificity
        PSMIterator begin = psmRows.begin(), cur = begin;
        while (cur != psmRows.end())
        {
            float lastSpecificity = cur->bestSpecificity;
            ++cur;
            if (cur == psmRows.end() || cur->bestSpecificity != lastSpecificity)
            {
                psmPartitionedRows.push_back(PSMIteratorRange(begin, cur));
                begin = cur;
            }
        }
    }

    return psmPartitionedRows;
}

vector<PSMIteratorRange> partition(const Qonverter::Settings& settings, PSMList& psmRows)
{
    return partition(settings, PSMIteratorRange(psmRows.begin(), psmRows.end()));
}


void normalize(const Qonverter::Settings& settings, PSMList& psmRows)
{
    try
    {
        vector<PSMIteratorRange> psmPartitionedRows = partition(settings, psmRows);

        // PSM scores are in the vector in asciibetical order by score name (the same order as scoreInfoByName)
        int scoreIndex = -1;
        BOOST_FOREACH_FIELD((const string& scoreName)(const Qonverter::Settings::ScoreInfo& scoreInfo), settings.scoreInfoByName)
        {
            ++scoreIndex;
            if (scoreInfo.normalizationMethod == Qonverter::Settings::NormalizationMethod::Off)
                continue;

            BOOST_FOREACH(PSMIteratorRange& range, psmPartitionedRows)
            {
                if (scoreInfo.normalizationMethod == Qonverter::Settings::NormalizationMethod::Linear)
                {
                    MinMaxPair<double> minMaxPair;

                    // find extrema
                    BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
                        minMaxPair.compare(psm.scores[scoreIndex]);

                    // apply linear scaling between 0 and 1
                    BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
                        psm.scores[scoreIndex] = (minMaxPair.scale(psm.scores[scoreIndex]) + 1) / 2;
                }
                else
                {
                    /*vector<double> scores;

                    // get list of scores
                    BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
                        scores.push_back(psm.scores[scoreIndex]);*/
                }
            }
        }
    }
    catch(exception& e)
    {
        throw runtime_error(string("[normalize] ") + e.what());
    }
}


void discriminate(const PSMIteratorRange& psmRows)
{
    double targetToDecoyRatio = 1; // TODO: get the real ratio

    // calculate new ranks; adjacent PSMs from the same spectrum share the same rank
    map<sqlite3_int64, int> psmRankBySpectrum;
    sqlite3_int64 currentSpectrum = 0;
    double currentScore = 0;
    BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
    {
        if (currentSpectrum == psm.spectrum && currentScore == psm.totalScore)
            psm.newRank = psmRankBySpectrum[psm.spectrum];
        else
        {
            psm.newRank = ++ psmRankBySpectrum[psm.spectrum];
            currentSpectrum = psm.spectrum;
            currentScore = psm.totalScore;
        }
    }

    // eliminate non-top-ranked PSMs
    sort(psmRows.begin(), psmRows.end(), NewRankLessThanOrTotalScoreBetterThan());

    int numTargets = 0;
    int numDecoys = 0;

    sqlite3_int64 currentSpectrumId = psmRows.front().spectrum;
    double currentTotalScore = psmRows.front().totalScore;
    DecoyState::Type currentDecoyState = psmRows.front().decoyState;
    vector<PeptideSpectrumMatch*> currentPSMs(1, &psmRows.front());

    // calculate Q values with the current sort
    BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
    {
        if (psm.newRank > 1)
        {
            psm.fdrScore = psm.qValue = 2;
            continue;
        }

        // all the PSMs for currentSpectrumId have been handled
        if (currentSpectrumId != psm.spectrum)
        {
            switch (currentDecoyState)
            {
                case DecoyState::Target: ++numTargets; break;
                case DecoyState::Decoy: ++numDecoys; break;
                default: break;
            }

            if (currentTotalScore != psm.totalScore)
            {
                BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
                    psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;

                // reset the current total score
                currentTotalScore = psm.totalScore;
                currentPSMs.assign(1, &psm);
            }
            else
                // multiple spectra can share the same total score;
                // their decoy states are NOT combined but they still share the same Q-values
                currentPSMs.push_back(&psm);

            // reset the current spectrum
            currentSpectrumId = psm.spectrum;
            currentDecoyState = psm.decoyState;
        }
        else
        {
            // multiple top-rank PSMs can belong to the same spectrum;
            // their decoy states are combined and they share the same Q-values
            currentDecoyState = static_cast<DecoyState::Type>(currentDecoyState | psm.decoyState);
            currentPSMs.push_back(&psm);
        }
    }

    if (!currentPSMs.empty())
    {
        switch (currentDecoyState)
        {
            case DecoyState::Target: ++numTargets; break;
            case DecoyState::Decoy: ++numDecoys; break;
            default: break;
        }

        BOOST_FOREACH(PeptideSpectrumMatch* psm, currentPSMs)
            psm->qValue = (numTargets + numDecoys > 0) ? min(1.0, max(0.0, (numDecoys * 2 * targetToDecoyRatio) / (numTargets + numDecoys))) : 0;
    }

    // with high scoring decoys, Q values can spike and gradually go down again;
    // we squash these spikes such that Q value is monotonically increasing
    for (int i = int(psmRows.size())-2; i >= 0; --i)
        if (psmRows[i].qValue > psmRows[i+1].qValue)
        {
            int j = i - 1;
            while (j >= 0 && psmRows[j].qValue == psmRows[i].qValue)
            {
                psmRows[j].qValue = psmRows[i+1].qValue;
                --j;
            }
            psmRows[i].qValue = psmRows[i+1].qValue;
        }

    typedef PSMIterator::difference_type size_t;

    // Calculate "FDRScore" from AR Jones: "Improving sensitivity in proteome studies by analysis of false discovery rates for multiple search engines"
    size_t stepStart = 0; // the index where the current step starts
    for (size_t i=1; i < psmRows.size(); ++i)
    {
        PeptideSpectrumMatch& previousPSM = psmRows[i-1];
        PeptideSpectrumMatch& currentPSM = psmRows[i];
        PeptideSpectrumMatch& stepStartPSM = psmRows[stepStart];

        // at each step point, do a linear regression from the start of the last step
        if (previousPSM.qValue < currentPSM.qValue || i+1 == psmRows.size() || psmRows[i+1].newRank > 1)
        {
            size_t stepEnd = i;
            double scoreDelta = currentPSM.totalScore - stepStartPSM.totalScore;
            double qvalueDelta = currentPSM.qValue - stepStartPSM.qValue;
            if (scoreDelta != 0 && qvalueDelta != 0)
            {
                double slope = qvalueDelta / scoreDelta;
                /*if (slope >= 0)
                {
                    cout << "ERROR: slope is non-negative for step " << stepStart << " to " << stepEnd << endl;
                    break;
                }*/
                double intercept = currentPSM.qValue - slope * currentPSM.totalScore;
                for (size_t j=stepStart; j <= stepEnd; ++j)
                    psmRows[j].fdrScore = slope * psmRows[j].totalScore + intercept;
            }
            else if (qvalueDelta == 0)
            {
                for (size_t j=stepStart; j <= stepEnd; ++j)
                    psmRows[j].fdrScore = psmRows[j].qValue;
            }
            else
                for (size_t j=stepStart; j <= stepEnd; ++j)
                    psmRows[j].fdrScore = psmRows[j].qValue = 2;

            if (i+1 == psmRows.size() || psmRows[i+1].newRank > 1)
                break;

            stepStart = stepEnd;
        }
    }
}

void discriminate(PSMList& psmRows)
{
    discriminate(PSMIteratorRange(psmRows.begin(), psmRows.end()));
}


END_IDPICKER_NAMESPACE
