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

int getStringVector(void* data, int columnCount, char** columnValues, char** columnNames)
{
    if (columnCount != 1)
        throw runtime_error("[Qonverter::getStringVector] result must have 1 column");

    static_cast<vector<string>*>(data)->push_back(columnValues[0]);

    return 0;
}

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
        bal::split(*static_cast<vector<string>*>(data), bal::to_lower_copy(string(columnValues[0])), bal::is_any_of(","));

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
    vector<PeptideSpectrumMatch> psmRows;

    PsmRowReader(const vector<string>& scoreIdSet) : scoresSize(scoreIdSet.size()) {}

    int operator() (int columnCount, char** columnValues, char** columnNames)
    {
        if (columnCount != 8 + scoresSize)
            throw runtime_error("[Qonverter::PsmRowReader()] result has incorrect column count");

        psmRows.push_back(PeptideSpectrumMatch());
        PeptideSpectrumMatch& psm = psmRows.back();

        psm.id = lexical_cast<sqlite_int64>(columnValues[0]);
        psm.spectrum = lexical_cast<sqlite_int64>(columnValues[1]);
        psm.originalRank = lexical_cast<sqlite_int64>(columnValues[2]);

        switch (columnValues[3][0])
        {
            case '0': psm.decoyState = DecoyState::Target; break;
            case '1': psm.decoyState = DecoyState::Decoy; break;
            case '2': psm.decoyState = DecoyState::Ambiguous; break;
        }

        psm.chargeState = lexical_cast<int>(columnValues[4]);
        psm.bestSpecificity = lexical_cast<int>(columnValues[5]);
        psm.missedCleavages = lexical_cast<int>(columnValues[6]);
        psm.massError = lexical_cast<double>(columnValues[7]);

        psm.scores.resize(scoresSize);
        for (size_t i=0; i < scoresSize; ++i)
            psm.scores[i] = lexical_cast<double>(columnValues[8+i]);

        psm.totalScore = 0;
        psm.qValue = 2;

        return 0;
    }

    private:
    size_t scoresSize;
};

int addPsmRows(void* data, int columnCount, char** columnValues, char** columnNames)
{
    return (*static_cast<PsmRowReader*>(data))(columnCount, columnValues, columnNames);
}

void validateSettings(const Qonverter::Settings& settings)
{
    // sanity checks
    if (settings.decoyPrefix.empty()) throw runtime_error("[Qonverter::validateSettings] no decoy prefix");
    if (settings.scoreInfoByName.empty()) throw runtime_error("[Qonverter::validateSettings] no score info");

    // check that qonverter method agrees with non-score handling
    if (settings.qonverterMethod != Qonverter::QonverterMethod::SVM)
    {
        if (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Feature)
            throw runtime_error("[Qonverter::validateSettings] charge state can only be used as a feature with the SVM qonverter");
        if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
            throw runtime_error("[Qonverter::validateSettings] mass error can only be used as a feature with the SVM qonverter");
        if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
            throw runtime_error("[Qonverter::validateSettings] charge state can only be used as a feature with the SVM qonverter");
        if (settings.terminalSpecificityHandling == Qonverter::TerminalSpecificityHandling::Feature)
            throw runtime_error("[Qonverter::validateSettings] terminal specificity can only be used as a feature with the SVM qonverter");
    }
}

void updatePsmRows(sqlite3* db, bool logQonversionDetails, const vector<PeptideSpectrumMatch>& psmRows)
{
    CHECK_SQLITE_RESULT(sqlite3_exec(db, "BEGIN TRANSACTION", NULL, NULL, &errorBuf));

    if (logQonversionDetails)
    {
        CHECK_SQLITE_RESULT(sqlite3_exec(db, "DROP TABLE IF EXISTS QonversionDetails; CREATE TABLE QonversionDetails (PsmId, Spectrum, TotalScore, QValue, DecoyState)", NULL, NULL, &errorBuf));
        BOOST_FOREACH(const PeptideSpectrumMatch& row, psmRows)
            CHECK_SQLITE_RESULT(sqlite3_exec(db, ("INSERT INTO QonversionDetails VALUES (" + \
                                                 lexical_cast<string>(row.id) + "," + \
                                                 lexical_cast<string>(row.spectrum) + "," + \
                                                 lexical_cast<string>(row.totalScore) + "," + \
                                                 lexical_cast<string>(row.qValue) + ",'" + \
                                                 DecoyState::Symbol[row.decoyState] + "')").c_str(), NULL, NULL, &errorBuf));
    }

    // update QValue column for each top-ranked PSM (non-top-ranked PSMs keep the default QValue)
    string sql = "UPDATE PeptideSpectrumMatch SET QValue = ? WHERE Id = ?";

    sqlite3_stmt* updateStmt;
    CHECK_SQLITE_RESULT(sqlite3_prepare_v2(db, sql.c_str(), -1, &updateStmt, NULL));
    BOOST_FOREACH(const PeptideSpectrumMatch& row, psmRows)
    {
        CHECK_SQLITE_RESULT(sqlite3_bind_double(updateStmt, 1, row.qValue));
        CHECK_SQLITE_RESULT(sqlite3_bind_int64(updateStmt, 2, row.id));
        CHECK_SQLITE_RESULT(sqlite3_step(updateStmt));
        CHECK_SQLITE_RESULT(sqlite3_reset(updateStmt));
    }
    CHECK_SQLITE_RESULT(sqlite3_finalize(updateStmt));

    CHECK_SQLITE_RESULT(sqlite3_exec(db, "COMMIT TRANSACTION", NULL, NULL, &errorBuf));
}

//} // namespace


BEGIN_IDPICKER_NAMESPACE


Qonverter::Qonverter()
{
    logQonversionDetails = false;
}

void Qonverter::qonvert(const string& idpDbFilepath, const ProgressMonitor& progressMonitor)
{
    sqlite::database db(idpDbFilepath);

    db.execute("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF");

    qonvert(db.connected(), progressMonitor);
}

void Qonverter::qonvert(sqlite3* db, const ProgressMonitor& progressMonitor)
{
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
    CHECK_SQLITE_RESULT(sqlite3_exec(db, sql.c_str(), getAnalysisSourcePairs, &analysisSourcePairs, &errorBuf));

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

        vector<string> strings;
        sql = "SELECT Name FROM SpectrumSource WHERE Id=" + spectrumSourceId;
        CHECK_SQLITE_RESULT(sqlite3_exec(db, sql.c_str(), getStringVector, &strings, &errorBuf));
        const string& sourceName = strings[0];

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
        CHECK_SQLITE_RESULT(sqlite3_exec(db, sql.c_str(), verifyDecoyPrefixOccurs, &decoyPrefixOccurs, &errorBuf));
        if (!decoyPrefixOccurs)
            throw runtime_error("[Qonverter::Qonvert] decoy prefix '" + decoyPrefix + "' does not occur in analysis " + analysisId);

        // get the set of expected score names
        set<string> expectedScoreNames;
        typedef pair<string, Settings::ScoreInfo> ScoreInfoPair;
        BOOST_FOREACH(const ScoreInfoPair& itr, scoreInfoByName)
            expectedScoreNames.insert(bal::to_lower_copy(itr.first));

        // get the set of actual score ids and names
        sql = "SELECT GROUP_CONCAT(scoreName.Name || ' ' || scoreName.Id) "
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

        CHECK_SQLITE_RESULT(sqlite3_exec(db, sql.c_str(), getScoreNames, &actualScoreIdNamePairs, &errorBuf));

        BOOST_FOREACH(const string& idName, actualScoreIdNamePairs)
        {
            vector<string> idNamePair;
            bal::split(idNamePair, idName, bal::is_space());
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
        BOOST_FOREACH(const string& name, scoreNameIntersection)
        {
            const Settings::ScoreInfo& scoreInfo = scoreInfoByName.find(name)->second;
            scoreWeightsVector.push_back(scoreInfo.order == Settings::Order::Ascending ? scoreInfo.weight : -scoreInfo.weight);
            scoreIdSet.push_back(actualScoreIdByName[name]);
        }

        // e.g. (1,2)
        //string scoreIdSetString = "(" + bal::join(scoreIdSet, ",") + ")";

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

        // retrieve triplets of psm id, decoy state, and score list (with the same order retrieved above)
        /*sql = "SELECT psm.Id, psm.Spectrum, "
              "      CASE WHEN SUM(DISTINCT CASE WHEN pro.Accession LIKE '" + decoyPrefix + "%' THEN 1 ELSE 0 END) + SUM(DISTINCT CASE WHEN pro.Accession NOT LIKE '" + decoyPrefix + "%' THEN 1 ELSE 0 END) = 2 THEN 2 " +
              "           ELSE SUM(DISTINCT CASE WHEN pro.Accession LIKE '" + decoyPrefix + "%' THEN 1 ELSE 0 END) " +
              "           END AS DecoyState, "
              "      GROUP_CONCAT(psmScore.Value) " // scores are ordered by ascending ScoreNameId
              "FROM Spectrum s "
              "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
              "JOIN PeptideSpectrumMatch psm ON s.Id=psm.Spectrum "
              "JOIN PeptideSpectrumMatchScore psmScore ON psm.Id=psmScore.PsmId "
              "JOIN Protein pro ON pi.Protein=pro.Id "
              "WHERE s.Source=" + spectrumSourceId +
              "  AND psm.Analysis=" + analysisId +
              "  AND psm.Charge=" + psmChargeState +
              "  AND NTerminusIsSpecific+CTerminusIsSpecific=" + specificity +
              (rerankMatches ? "" : " AND Rank = 1 ") +
              "  AND psmScore.ScoreNameId IN " + scoreIdSetString + " " +
              "GROUP BY psm.Id "
              "ORDER BY psm.Spectrum";*/

        sql = "SELECT psm.Id, psm.Spectrum, psm.Rank, "
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
        CHECK_SQLITE_RESULT(sqlite3_exec(db, sql.c_str(), addPsmRows, &psmRowReader, &errorBuf));

        switch (qonverterSettings.qonverterMethod.index())
        {
            default:
            case Qonverter::QonverterMethod::StaticWeighted:
                StaticWeightQonverter::Qonvert(psmRowReader.psmRows, qonverterSettings, scoreWeightsVector);
                break;
            case Qonverter::QonverterMethod::SVM:
                SVMQonverter::Qonvert(sourceName, psmRowReader.psmRows, qonverterSettings);
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
    sqlite::database db(idpDbFilepath);

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
    if (settings.chargeStateHandling != Qonverter::ChargeStateHandling::Partition &&
        settings.terminalSpecificityHandling != Qonverter::TerminalSpecificityHandling::Partition)
        return vector<PSMIteratorRange>(1, PSMIteratorRange(psmRows.begin(), psmRows.end()));

    vector<PSMIteratorRange> psmPartitionedRows;

    if (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Partition &&
        settings.terminalSpecificityHandling == Qonverter::TerminalSpecificityHandling::Partition)
    {
        sort(psmRows.begin(), psmRows.end(), ChargeAndSpecificityLessThan());

        // split the matches into a range for each charge state and terminal specificity
        PSMIterator begin = psmRows.begin(), cur = begin;
        while (cur != psmRows.end())
        {
            int lastCharge = cur->chargeState;
            int lastSpecificity = cur->bestSpecificity;
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
    else if (settings.chargeStateHandling == Qonverter::ChargeStateHandling::Partition)
    {
        sort(psmRows.begin(), psmRows.end(), ChargeStateLessThan());

        // split the matches into a range for each charge state
        PSMIterator begin = psmRows.begin(), cur = begin;
        while (cur != psmRows.end())
        {
            int lastCharge = cur->chargeState;
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
            int lastSpecificity = cur->bestSpecificity;
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


END_IDPICKER_NAMESPACE
