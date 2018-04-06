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
#include "Qonverter.hpp"
#include "StaticWeightQonverter.hpp"
#include "MonteCarloQonverter.hpp"
#include "SVMQonverter.hpp"
#include <boost/foreach_field.hpp>


using namespace pwiz::util;
using namespace IDPicker;


vector<double> parseDoubleArray(const string& doubleArray)
{
    vector<double> doubleVector;
    vector<string> tokens;
    bal::split(tokens, doubleArray, bal::is_space());
    if (!tokens.empty() && !tokens[0].empty())
        for (size_t i=0; i < tokens.size(); ++i)
            doubleVector.push_back(lexical_cast<double>(tokens[i]));
    return doubleVector;
}


struct PSMLessThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        if (lhs.spectrum == rhs.spectrum)
            if (lhs.originalRank == rhs.originalRank)
                if (lhs.newRank == rhs.newRank)
                    if (lhs.chargeState == rhs.chargeState)
                        if (lhs.bestSpecificity == rhs.bestSpecificity)
                            throw runtime_error("duplicate PSM");
                        else
                            return lhs.bestSpecificity < rhs.bestSpecificity;
                    else
                        return lhs.chargeState < rhs.chargeState;
                else
                    return lhs.newRank < rhs.newRank;
            else
                return lhs.originalRank < rhs.originalRank;
        else
            return lhs.spectrum < rhs.spectrum;
    }
};


struct TestPSM
{
    sqlite_int64 id;
    sqlite_int64 spectrum;

    int originalRank;
    DecoyState::Type decoyState;

    float chargeState;
    float bestSpecificity;
    float missedCleavages;
    double massError;
    const char* scores;

    int newRank;
    double totalScore;
    double qValue;
    double fdrScore;

    PeptideSpectrumMatch* psm() const
    {
        PeptideSpectrumMatch* psm = new PeptideSpectrumMatch;
        psm->id = id;
        psm->spectrum = spectrum;
        psm->originalRank = originalRank;
        psm->decoyState = decoyState;
        psm->chargeState = chargeState;
        psm->bestSpecificity = bestSpecificity;
        psm->missedCleavages = missedCleavages;
        psm->massError = massError;
        psm->scores = parseDoubleArray(scores);
        psm->totalScore = totalScore;
        return psm;
    }
};


void testPartition()
{
    PSMList testPSMs;

    // generate a test list like:
    // id  spectrum  rank  specificity  charge
    // 1   1         1     0            1
    // 2   1         2     0            1
    // 3   1         3     0            1
    // 4   2         1     1            1
    // 5   2         2     1            1
    // 6   3         1     2            1
    // 7   4         1     0            2
    // 8   4         2     0            2
    // 9   4         3     0            2
    // ...
    sqlite_int64 id = 0, spectrum = 1;
    for (int charge=1; charge <= 3; ++charge)
    for (int specificity=0; specificity <= 2; ++specificity, ++spectrum)
    for (int rank=1; rank <= 3-specificity; ++rank)
    {
        testPSMs.push_back(new PeptideSpectrumMatch);
        PeptideSpectrumMatch& psm = testPSMs.back();
        psm.id = ++id;
        psm.spectrum = spectrum;
        psm.decoyState = DecoyState::Target;
        psm.originalRank = rank;
        psm.chargeState = charge;
        psm.bestSpecificity = specificity;
        psm.missedCleavages = 0;
        psm.massError = 0;
    }

    unit_assert_operator_equal(18, testPSMs.size());

    Qonverter::Settings settings;
    settings.minPartitionSize = 1;
    vector<PSMIteratorRange> psmPartitionedRows;

    // test no partitions
    settings.chargeStateHandling = Qonverter::ChargeStateHandling::Ignore;
    settings.terminalSpecificityHandling = Qonverter::TerminalSpecificityHandling::Ignore;
    psmPartitionedRows = partition(settings, testPSMs);
    unit_assert_operator_equal(1, psmPartitionedRows.size());
    for (int i=0; i < psmPartitionedRows[0].size(); ++i)
        unit_assert_operator_equal(testPSMs[i].id, psmPartitionedRows[0][i].id);

    // test partition by charge state only
    settings.chargeStateHandling = Qonverter::ChargeStateHandling::Partition;
    settings.terminalSpecificityHandling = Qonverter::TerminalSpecificityHandling::Ignore;
    psmPartitionedRows = partition(settings, testPSMs);
    unit_assert_operator_equal(3, psmPartitionedRows.size());
    for (size_t i=0; i < psmPartitionedRows.size(); ++i)
    {
        unit_assert_operator_equal(6, psmPartitionedRows[i].size());
        float expectedCharge = psmPartitionedRows[i].front().chargeState;
        unit_assert_operator_equal(expectedCharge, psmPartitionedRows[i].back().chargeState);
    }

    // test partition by terminal specificity only
    settings.chargeStateHandling = Qonverter::ChargeStateHandling::Ignore;
    settings.terminalSpecificityHandling = Qonverter::TerminalSpecificityHandling::Partition;
    psmPartitionedRows = partition(settings, testPSMs);
    unit_assert_operator_equal(3, psmPartitionedRows.size());
    for (size_t i=0; i < psmPartitionedRows.size(); ++i)
    {
        unit_assert(!psmPartitionedRows[i].empty());
        float expectedSpecificity = psmPartitionedRows[i].front().bestSpecificity;
        // specificity 2 has 3 PSMs, 1 has 6, 0 has 9
        unit_assert_operator_equal((3-expectedSpecificity)*3, psmPartitionedRows[i].size());
        unit_assert_operator_equal(expectedSpecificity, psmPartitionedRows[i].back().bestSpecificity);
    }

    // test partition by charge state and terminal specificity
    settings.chargeStateHandling = Qonverter::ChargeStateHandling::Partition;
    settings.terminalSpecificityHandling = Qonverter::TerminalSpecificityHandling::Partition;
    psmPartitionedRows = partition(settings, testPSMs);
    unit_assert_operator_equal(9, psmPartitionedRows.size());
    for (size_t i=0; i < psmPartitionedRows.size(); ++i)
    {
        float expectedSpecificity = floor(i/3.0f); // 1,2,3=1  4,5,6=2  7,8,9=3
        float expectedCharge = (i+1)%3 ? float((i+1)%3) : 3.0f; // 1,4,7=1  2,5,8=2  3,6,9=3
        unit_assert_operator_equal(3-expectedSpecificity, psmPartitionedRows[i].size());
        unit_assert_operator_equal(expectedSpecificity, psmPartitionedRows[i].front().bestSpecificity);
        unit_assert_operator_equal(expectedSpecificity, psmPartitionedRows[i].back().bestSpecificity);
        unit_assert_operator_equal(expectedCharge, psmPartitionedRows[i].front().chargeState);
        unit_assert_operator_equal(expectedCharge, psmPartitionedRows[i].back().chargeState);
    }
}


void testDiscriminate(const TestPSM* testPSMs, size_t testPSMsSize)
{
    PSMList psmRows;
    for (size_t i=0; i < testPSMsSize; ++i)
        psmRows.push_back(testPSMs[i].psm());

    discriminate(psmRows);

    for (size_t i=0; i < psmRows.size(); ++i)
    {
        unit_assert_operator_equal(testPSMs[i].newRank, psmRows[i].newRank);
        unit_assert_equal(testPSMs[i].qValue, psmRows[i].qValue, 1e-3);
        unit_assert_equal(testPSMs[i].fdrScore, psmRows[i].fdrScore, 1e-3);
    }
}


void testDiscriminate()
{
    // a simple example
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new        |--expected---|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   12,   0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   10,   0,     0.125},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   4,    0.5,   0.5},
            {0, 4, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   2,    0.5,   0.7},
            {0, 5, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   1,    0.8,   0.8},
        };

        testDiscriminate(testPSMs, sizeof(testPSMs)/sizeof(TestPSM));
    }

    // test ambiguous results without any ambiguous decoy states
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new        |--expected---|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   12,   0,     0},
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   12,   0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   10,   0,     0.125},
            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   10,   0,     0.125},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   4,    0.5,   0.5},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   4,    0.5,   0.5},
            {0, 4, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   2,    0.5,   0.7},
            {0, 4, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   2,    0.5,   0.7},
            {0, 5, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   1,    0.8,   0.8},
            {0, 5, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   1,    0.8,   0.8},
        };

        testDiscriminate(testPSMs, sizeof(testPSMs)/sizeof(TestPSM));
    }

    // test results with some ambiguous decoy states
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new        |--expected---|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   12,   0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   10,   0,     0.125},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   4,    0.5,   0.5},
            {0, 4, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   3,    0.5,   0.6},
            {0, 5, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   2,    0.5,   0.7},
            {0, 5, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   2,    0.5,   0.7},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   1,    0.8,   0.8},
        };

        testDiscriminate(testPSMs, sizeof(testPSMs)/sizeof(TestPSM));
    }

    // test reranking
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new        |--expected---|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   12,   0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   10,   0,     0.125},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   4,    0.5,   0.5},
            {0, 4, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   3,    0.5,   0.6},
            {0, 5, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   2,    0.5,   0.7},
            {0, 5, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   2,    0.5,   0.7},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   1,    0.8,   0.8},
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "",     2,   10,   2,     2},
            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "",     2,   9,    2,     2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     2,   3,    2,     2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     3,   2,    2,     2},
        };

        testDiscriminate(testPSMs, sizeof(testPSMs)/sizeof(TestPSM));
    }

    // test different spectra with equal totalScore
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new        |--expected---|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   12,   0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   12,   0,     0},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   4,    0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   4,    0.4,   0.4},
            {0, 5, 1,  DecoyState::Target, 1, 1,  0, 0, "",     1,   2,    0.4,   0.577},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "",     1,   1,    0.666, 0.666},
        };

        testDiscriminate(testPSMs, sizeof(testPSMs)/sizeof(TestPSM));
    }
}


int testQonvertedPSMs(PSMList& psmRows, const TestPSM* testPSMs, size_t testPSMsSize)
{
    sort(psmRows.begin(), psmRows.end(), PSMLessThan());

    int testsFailed = 0;
    size_t i;
    try
    {
        for (i = 0; i < psmRows.size(); ++i)
        {
            unit_assert_operator_equal(testPSMs[i].newRank, psmRows[i].newRank);
            unit_assert_equal(testPSMs[i].totalScore, psmRows[i].totalScore, 1e-6);
            unit_assert_equal(testPSMs[i].qValue, psmRows[i].qValue, 1e-3);
            unit_assert_equal(testPSMs[i].fdrScore, psmRows[i].fdrScore, 1e-3);
        }
    }
    catch (exception& e)
    {
        ++testsFailed;
        cout << "PSM test " << (i + 1) << " failed: " << e.what() << endl;
    }

    if (testsFailed > 0)
        for (size_t i = 0; i < psmRows.size(); ++i)
        {
            cout << "  Actual: "
                 << psmRows[i].spectrum << " "
                 << psmRows[i].originalRank << " "
                 << psmRows[i].newRank << " "
                 << psmRows[i].totalScore << " "
                 << psmRows[i].qValue << " "
                 << psmRows[i].fdrScore << endl;

            cout << "Expected: "
                 << testPSMs[i].spectrum << " "
                 << testPSMs[i].originalRank << " "
                 << testPSMs[i].newRank << " "
                 << testPSMs[i].totalScore << " "
                 << testPSMs[i].qValue << " "
                 << testPSMs[i].fdrScore << endl << endl;
        }

    return testsFailed;
}


void testStaticWeightQonverter(const TestPSM* testPSMs, size_t testPSMsSize,
                               const Qonverter::Settings& settings,
                               const vector<double>& scoreWeights,
                               int lineNumber)
{
    PSMList psmRows;
    for (size_t i=0; i < testPSMsSize; ++i)
        psmRows.push_back(testPSMs[i].psm());

    StaticWeightQonverter::Qonvert(psmRows, settings, scoreWeights);

    int failedTests = testQonvertedPSMs(psmRows, testPSMs, testPSMsSize);
    (failedTests > 0) ? throw std::runtime_error(unit_assert_equal_message(__FILE__, lineNumber, "0", lexical_cast<string>(failedTests), "failedTests")) : 0;
}


void testStaticWeightQonverter()
{
    Qonverter::Settings settings;
    settings.minPartitionSize = 1;
    vector<double> scoreWeights(2);

    // a simple example
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1",  1,   6,    0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 2",  1,   5,    0,     0.2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",  1,   4,    0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",  1,   3,    0.4,   0.488},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 5",  1,   2,    0.4,   0.577},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",  1,   1,    0.666, 0.666},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = 0.0;
        testStaticWeightQonverter(testPSMs, sizeof(testPSMs)/sizeof(TestPSM), settings, scoreWeights, __LINE__);
    }

    // test negative weights and a secondary score
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1",  1,   -1,   0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 2",  1,   -2,   0,     0.2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",  1,   -3,   0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",  1,   -4,   0.4,   0.488},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 5",  1,   -5,   0.4,   0.577},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",  1,   -6,   0.666, 0.666},
        };

        scoreWeights[0] = 0.0;
        scoreWeights[1] = -1.0;
        testStaticWeightQonverter(testPSMs, sizeof(testPSMs)/sizeof(TestPSM), settings, scoreWeights, __LINE__);
    }

    // test combined weights
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;
        testStaticWeightQonverter(testPSMs, sizeof(testPSMs)/sizeof(TestPSM), settings, scoreWeights, __LINE__);
    }

    // test multiple ranks without reranking
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 2,  DecoyState::Target, 1, 2,  0, 0, "5 1",  2,   4,    2,     2},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 2, 2,  DecoyState::Target, 1, 2,  0, 0, "4 1",  2,   3,    2,     2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 3, 2,  DecoyState::Target, 1, 1,  0, 0, "4 2",  2,   2,    2,     2},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 4, 2,  DecoyState::Target, 1, 2,  0, 0, "2 2",  2,   0,    2,     2},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 5, 2,  DecoyState::Target, 1, 2,  0, 0, "1 3",  2,   -2,   2,     2},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
            {0, 6, 2,  DecoyState::Decoy,  1, 1,  0, 0, "0 6",  2,   -6,   2,     2},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;
        testStaticWeightQonverter(testPSMs, sizeof(testPSMs)/sizeof(TestPSM), settings, scoreWeights, __LINE__);
    }

    // test multiple ranks with reranking
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 2,  DecoyState::Target, 1, 2,  0, 0, "5 1",  2,   4,    2,     2},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 2",  1,   3,    0,     0.066},
            {0, 2, 2,  DecoyState::Target, 1, 2,  0, 0, "4 1",  1,   3,    0,     0.066},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",  2,   1,    2,     2},
            {0, 3, 2,  DecoyState::Target, 1, 1,  0, 0, "4 2",  1,   2,    0,     0.1},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",  2,   -1,   2,     2},
            {0, 4, 2,  DecoyState::Target, 1, 2,  0, 0, "2 2",  1,   0,    0,     0.166},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 5",  2,   -3,   2,     2},
            {0, 5, 2,  DecoyState::Target, 1, 2,  0, 0, "1 3",  1,   -2,   0,     0.233},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",  1,   -5,   0.333, 0.333},
            {0, 6, 2,  DecoyState::Decoy,  1, 1,  0, 0, "0 6",  2,   -6,   2,     2},
        };

        settings.rerankMatches = true;

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;
        testStaticWeightQonverter(testPSMs, sizeof(testPSMs)/sizeof(TestPSM), settings, scoreWeights, __LINE__);

        settings.rerankMatches = false;
    }

    // test charge state partitioning
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 1,  DecoyState::Target, 2, 2,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 1,  DecoyState::Target, 3, 2,  0, 0, "6 1",  1,   5,    0,     0},

            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 2, 2,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 3, 2,  0, 0, "5 2",  1,   3,    0,     0.2},

            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  2, 1,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  3, 1,  0, 0, "4 3",  1,   1,    0.4,   0.4},

            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 2, 2,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 3, 2,  0, 0, "3 4",  1,   -1,   0.4,   0.488},

            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 2, 2,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 3, 2,  0, 0, "2 5",  1,   -3,   0.4,   0.577},

            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  2, 1,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  3, 1,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
        };

        settings.chargeStateHandling = Qonverter::ChargeStateHandling::Partition;

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;
        testStaticWeightQonverter(testPSMs, sizeof(testPSMs)/sizeof(TestPSM), settings, scoreWeights, __LINE__);
    }

    // test charge and specificity partitioning
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 1,  DecoyState::Target, 2, 1,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 1,  DecoyState::Target, 2, 2,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 1,  DecoyState::Target, 3, 1,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 1,  DecoyState::Target, 3, 2,  0, 0, "6 1",  1,   5,    0,     0},

            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 2, 1,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 2, 2,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 3, 1,  0, 0, "5 2",  1,   3,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 3, 2,  0, 0, "5 2",  1,   3,    0,     0.2},

            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  1, 2,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  2, 1,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  2, 2,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  3, 1,  0, 0, "4 3",  1,   1,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  3, 2,  0, 0, "4 3",  1,   1,    0.4,   0.4},

            {0, 4, 1,  DecoyState::Target, 1, 1,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 2, 1,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 2, 2,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 3, 1,  0, 0, "3 4",  1,   -1,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 3, 2,  0, 0, "3 4",  1,   -1,   0.4,   0.488},

            {0, 5, 1,  DecoyState::Target, 1, 1,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 2, 1,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 2, 2,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 3, 1,  0, 0, "2 5",  1,   -3,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 3, 2,  0, 0, "2 5",  1,   -3,   0.4,   0.577},

            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  1, 2,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  2, 1,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  2, 2,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  3, 1,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  3, 2,  0, 0, "1 6",  1,   -5,   0.666, 0.666},
        };

        settings.chargeStateHandling = Qonverter::ChargeStateHandling::Partition;
        settings.terminalSpecificityHandling = Qonverter::TerminalSpecificityHandling::Partition;

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;
        testStaticWeightQonverter(testPSMs, sizeof(testPSMs)/sizeof(TestPSM), settings, scoreWeights, __LINE__);
    }

}


void testMonteCarloQonverter(const TestPSM* testPSMs, size_t testPSMsSize,
                             const Qonverter::Settings& settings,
                             const vector<double>& scoreWeights,
                             const MonteCarloQonverter::WeightsByChargeAndBestSpecificity& expectedScoreWeights,
                             int lineNumber)
{
    PSMList psmRows;
    for (size_t i = 0; i < testPSMsSize; ++i)
        psmRows.push_back(testPSMs[i].psm());

    MonteCarloQonverter::WeightsByChargeAndBestSpecificity actualWeights;
    MonteCarloQonverter::Qonvert(psmRows, settings, scoreWeights, &actualWeights);

    int failedTests = testQonvertedPSMs(psmRows, testPSMs, testPSMsSize);
    (failedTests > 0) ? throw std::runtime_error(unit_assert_equal_message(__FILE__, lineNumber, "0", lexical_cast<string>(failedTests), "failedTests")) : 0;

    BOOST_FOREACH_FIELD((int chargeState)(const MonteCarloQonverter::WeightsByBestSpecificity& monteCarloWeightsByBestSpecificity), expectedScoreWeights)
    BOOST_FOREACH_FIELD((int bestSpecificity)(const vector<double>& expectedMonteCarloWeights), monteCarloWeightsByBestSpecificity)
    for (size_t i = 0; i < expectedMonteCarloWeights.size(); ++i)
    {
        unit_assert_operator_equal(expectedScoreWeights.size(), actualWeights.size());
        unit_assert_operator_equal(monteCarloWeightsByBestSpecificity.size(), actualWeights[chargeState].size());
        unit_assert_operator_equal(expectedMonteCarloWeights.size(), actualWeights[chargeState][bestSpecificity].size());
        unit_assert_equal(expectedMonteCarloWeights[i], actualWeights[chargeState][bestSpecificity][i], 1e-5);
    }
}


void testMonteCarloQonverter()
{
    Qonverter::Settings settings;
    settings.minPartitionSize = 1;
    vector<double> scoreWeights(2);
    
    // a simple example with just one score
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6",    1,   6,    0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5",    1,   5,    0,     0.2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4",    1,   4,    0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3",    1,   3,    0.4,   0.488},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2",    1,   2,    0.4,   0.577},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1",    1,   1,    0.666, 0.666},
        };

        vector<double> scoreWeights(1, 1.0);

        MonteCarloQonverter::WeightsByChargeAndBestSpecificity expectedScoreWeights;
        expectedScoreWeights[1][1] = vector<double>(1, 1.0);
        expectedScoreWeights[1][2] = vector<double>(1, 1.0);

        testMonteCarloQonverter(testPSMs, sizeof(testPSMs) / sizeof(TestPSM), settings, scoreWeights, expectedScoreWeights, __LINE__);
    }

    // test negative weights and a secondary score
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1.9", 1,  4.42, 0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 6",   1,  2.8,  0,     0.356},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",   1,  2.6,  0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",   1,  1.6,  0.4,   0.488},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 -14", 1,  4.4,  0,     0.004},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",   1,  -0.4, 0.666, 0.666},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;

        MonteCarloQonverter::WeightsByChargeAndBestSpecificity expectedScoreWeights;
        expectedScoreWeights[1][2] = {0.8, -0.2};
        expectedScoreWeights[1][1] = {0.8, -0.2};

        testMonteCarloQonverter(testPSMs, sizeof(testPSMs) / sizeof(TestPSM), settings, scoreWeights, expectedScoreWeights, __LINE__);
    }

    // test that 1,0 is considered
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 -10",  1,   6,    0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 -20",  1,   5,    0,     0.2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 -30",  1,   4,    0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 -40",  1,   3,    0.4,   0.488},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 -50",  1,   2,    0.4,   0.577},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 -60",  1,   1,    0.666, 0.666},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;

        MonteCarloQonverter::WeightsByChargeAndBestSpecificity expectedScoreWeights;
        expectedScoreWeights[1][2] = {1.0, -0.0};
        expectedScoreWeights[1][1] = {1.0, -0.0};

        testMonteCarloQonverter(testPSMs, sizeof(testPSMs) / sizeof(TestPSM), settings, scoreWeights, expectedScoreWeights, __LINE__);
    }

    // test that 0,1 is considered
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "10 1",  1,   -1,   0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "20 2",  1,   -2,   0,     0.2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "30 3",  1,   -3,   0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "40 4",  1,   -4,   0.4,   0.488},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "50 5",  1,   -5,   0.4,   0.577},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "60 6",  1,   -6,   0.666, 0.666},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;

        MonteCarloQonverter::WeightsByChargeAndBestSpecificity expectedScoreWeights;
        expectedScoreWeights[1][2] = {0.0, -1.0};
        expectedScoreWeights[1][1] = {0.0, -1.0};

        testMonteCarloQonverter(testPSMs, sizeof(testPSMs) / sizeof(TestPSM), settings, scoreWeights, expectedScoreWeights, __LINE__);
    }

    // test multiple ranks without reranking
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 90",  1,   6,    0,     0},
            {0, 1, 2,  DecoyState::Target, 1, 2,  0, 0, "5 90",  2,   5,    2,     2},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 80",  1,   5,    0,     0.2},
            {0, 2, 2,  DecoyState::Target, 1, 2,  0, 0, "4 80",  2,   4,    2,     2},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 70",  1,   4,    0.4,   0.4},
            {0, 3, 2,  DecoyState::Target, 1, 1,  0, 0, "4 70",  2,   4,    2,     2},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 60",  1,   3,    0.4,   0.488},
            {0, 4, 2,  DecoyState::Target, 1, 2,  0, 0, "2 60",  2,   2,    2,     2},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 50",  1,   2,    0.4,   0.577},
            {0, 5, 2,  DecoyState::Target, 1, 2,  0, 0, "1 50",  2,   1,    2,     2},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 40",  1,   1,    0.666, 0.666},
            {0, 6, 2,  DecoyState::Decoy,  1, 1,  0, 0, "0 40",  2,   0,    2,     2},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;

        MonteCarloQonverter::WeightsByChargeAndBestSpecificity expectedScoreWeights;
        expectedScoreWeights[1][2] = {1.0, -0.0};
        expectedScoreWeights[1][1] = {1.0, -0.0};

        testMonteCarloQonverter(testPSMs, sizeof(testPSMs) / sizeof(TestPSM), settings, scoreWeights, expectedScoreWeights, __LINE__);
    }

    // test multiple ranks with reranking
    /*{
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1",  1,   5,    0,     0},
            {0, 1, 2,  DecoyState::Target, 1, 2,  0, 0, "5 1",  2,   4,    2,     2},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 2",  1,   3,    0,     0.066},
            {0, 2, 2,  DecoyState::Target, 1, 2,  0, 0, "4 1",  1,   3,    0,     0.066},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",  2,   1,    2,     2},
            {0, 3, 2,  DecoyState::Target, 1, 1,  0, 0, "4 2",  1,   2,    0,     0.1},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",  2,   -1,   2,     2},
            {0, 4, 2,  DecoyState::Target, 1, 2,  0, 0, "2 2",  1,   0,    0,     0.166},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 5",  2,   -3,   2,     2},
            {0, 5, 2,  DecoyState::Target, 1, 2,  0, 0, "1 3",  1,   -2,   0,     0.233},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",  1,   -5,   0.333, 0.333},
            {0, 6, 2,  DecoyState::Decoy,  1, 1,  0, 0, "0 6",  2,   -6,   2,     2},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = 0.0;

        settings.rerankMatches = true;

        expectedScoreWeights[1][2][0] = 1.0;
        expectedScoreWeights[1][2][1] = 0.0;
        expectedScoreWeights[1][1][0] = 1.0;
        expectedScoreWeights[1][1][1] = 0.0;
        testMonteCarloQonverter(testPSMs, sizeof(testPSMs) / sizeof(TestPSM), settings, scoreWeights, expectedScoreWeights, __LINE__);

        settings.rerankMatches = false;
    }*/

    // test charge state partitioning
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 1.9", 1,  4.42, 0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 6",   1,  2.8,  0,     0.356},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 3",   1,  2.6,  0.4,   0.4},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 4",   1,  1.6,  0.4,   0.488},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 -14", 1,  4.4,  0,     0.004},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 6",   1,  -0.4, 0.666, 0.666},

            {0, 7, 1,  DecoyState::Target, 2, 2,  0, 0, "6 1.9", 1,  4.42, 0,     0},
            {0, 8, 1,  DecoyState::Target, 2, 2,  0, 0, "5 6",   1,  2.8,  0,     0.356},
            {0, 9, 1,  DecoyState::Decoy,  2, 1,  0, 0, "4 3",   1,  2.6,  0.4,   0.4},
            {0, 10, 1,  DecoyState::Target, 2, 2,  0, 0, "3 4",   1,  1.6,  0.4,   0.488},
            {0, 11, 1,  DecoyState::Target, 2, 2,  0, 0, "2 -14", 1,  4.4,  0,     0.004},
            {0, 12, 1,  DecoyState::Decoy,  2, 1,  0, 0, "1 6",   1,  -0.4, 0.666, 0.666},

            {0, 13, 1,  DecoyState::Target, 3, 2,  0, 0, "6 90",   1,  6,    0,     0},
            {0, 14, 1,  DecoyState::Target, 3, 2,  0, 0, "5 80",   1,  5,    0,     0.2},
            {0, 15, 1,  DecoyState::Decoy,  3, 1,  0, 0, "4 70",   1,  4,    0.4,   0.4},
            {0, 16, 1,  DecoyState::Target, 3, 2,  0, 0, "3 60",   1,  3,    0.4,   0.488},
            {0, 17, 1,  DecoyState::Target, 3, 2,  0, 0, "2 50",   1,  2,    0.4,   0.577},
            {0, 18, 1,  DecoyState::Decoy,  3, 1,  0, 0, "1 40",   1,  1,    0.666, 0.666},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;

        settings.chargeStateHandling = Qonverter::ChargeStateHandling::Partition;

        MonteCarloQonverter::WeightsByChargeAndBestSpecificity expectedScoreWeights;
        expectedScoreWeights[1][2] = {0.8, -0.2};
        expectedScoreWeights[1][1] = {0.8, -0.2};
        expectedScoreWeights[2][2] = {0.8, -0.2};
        expectedScoreWeights[2][1] = {0.8, -0.2};
        expectedScoreWeights[3][2] = {1.0, -0.0};
        expectedScoreWeights[3][1] = {1.0, -0.0};

        testMonteCarloQonverter(testPSMs, sizeof(testPSMs) / sizeof(TestPSM), settings, scoreWeights, expectedScoreWeights, __LINE__);
    }

    // test charge and specificity partitioning
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 1,  0, 0, "6 90",  1,   6,    0,     0},
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6 90",  1,   6,    0,     0},
            {0, 1, 1,  DecoyState::Target, 2, 1,  0, 0, "6 90",  1,   6,    0,     0},
            {0, 1, 1,  DecoyState::Target, 2, 2,  0, 0, "6 90",  1,   6,    0,     0},
            {0, 1, 1,  DecoyState::Target, 3, 1,  0, 0, "6 90",  1,   6,    0,     0},
            {0, 1, 1,  DecoyState::Target, 3, 2,  0, 0, "6 90",  1,   6,    0,     0},

            {0, 2, 1,  DecoyState::Target, 1, 1,  0, 0, "5 80",  1,   5,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5 80",  1,   5,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 2, 1,  0, 0, "5 80",  1,   5,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 2, 2,  0, 0, "5 80",  1,   5,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 3, 1,  0, 0, "5 80",  1,   5,    0,     0.2},
            {0, 2, 1,  DecoyState::Target, 3, 2,  0, 0, "5 80",  1,   5,    0,     0.2},

            {0, 3, 1,  DecoyState::Decoy,  1, 1,  0, 0, "4 70",  1,   4,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  1, 2,  0, 0, "4 70",  1,   4,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  2, 1,  0, 0, "4 70",  1,   4,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  2, 2,  0, 0, "4 70",  1,   4,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  3, 1,  0, 0, "4 70",  1,   4,    0.4,   0.4},
            {0, 3, 1,  DecoyState::Decoy,  3, 2,  0, 0, "4 70",  1,   4,    0.4,   0.4},

            {0, 4, 1,  DecoyState::Target, 1, 1,  0, 0, "3 60",  1,   3,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 1, 2,  0, 0, "3 60",  1,   3,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 2, 1,  0, 0, "3 60",  1,   3,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 2, 2,  0, 0, "3 60",  1,   3,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 3, 1,  0, 0, "3 60",  1,   3,   0.4,   0.488},
            {0, 4, 1,  DecoyState::Target, 3, 2,  0, 0, "3 60",  1,   3,   0.4,   0.488},

            {0, 5, 1,  DecoyState::Target, 1, 1,  0, 0, "2 50",  1,   2,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 1, 2,  0, 0, "2 50",  1,   2,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 2, 1,  0, 0, "2 50",  1,   2,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 2, 2,  0, 0, "2 50",  1,   2,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 3, 1,  0, 0, "2 50",  1,   2,   0.4,   0.577},
            {0, 5, 1,  DecoyState::Target, 3, 2,  0, 0, "2 50",  1,   2,   0.4,   0.577},

            {0, 6, 1,  DecoyState::Decoy,  1, 1,  0, 0, "1 40",  1,   1,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  1, 2,  0, 0, "1 40",  1,   1,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  2, 1,  0, 0, "1 40",  1,   1,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  2, 2,  0, 0, "1 40",  1,   1,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  3, 1,  0, 0, "1 40",  1,   1,   0.666, 0.666},
            {0, 6, 1,  DecoyState::Decoy,  3, 2,  0, 0, "1 40",  1,   1,   0.666, 0.666},
        };

        scoreWeights[0] = 1.0;
        scoreWeights[1] = -1.0;

        settings.chargeStateHandling = Qonverter::ChargeStateHandling::Partition;
        settings.terminalSpecificityHandling = Qonverter::TerminalSpecificityHandling::Partition;

        MonteCarloQonverter::WeightsByChargeAndBestSpecificity expectedScoreWeights;
        expectedScoreWeights[1][2] = {1.0, -0.0};
        expectedScoreWeights[1][1] = {1.0, -0.0};
        expectedScoreWeights[2][2] = {1.0, -0.0};
        expectedScoreWeights[2][1] = {1.0, -0.0};
        expectedScoreWeights[3][2] = {1.0, -0.0};
        expectedScoreWeights[3][1] = {1.0, -0.0};

        testMonteCarloQonverter(testPSMs, sizeof(testPSMs) / sizeof(TestPSM), settings, scoreWeights, expectedScoreWeights, __LINE__);
    }

}


/* TODO!
void testSVMQonverter()
{
    Qonverter::Settings settings;

    // a simple example
    {
        const TestPSM testPSMs[] =
        {
         //       orig                                          new  |-----expected------|
         // id sp rank decoyState          z NET NMC ME scores  rank total qValue fdrScore
            {0, 1, 1,  DecoyState::Target, 1, 2,  0, 0, "6",     1,   0.99,   0,     0},
            {0, 2, 1,  DecoyState::Target, 1, 2,  0, 0, "5",     1,   0.99,   0,     0},
            {0, 4, 1,  DecoyState::Target, 1, 2,  1, 0, "3",     1,   0.99,   0,     0.4},
            {0, 5, 1,  DecoyState::Target, 1, 2,  1, 0, "2",     1,   0.99,   0,     0.577},
            {0, 3, 1,  DecoyState::Decoy,  1, 1,  3, 0, "4",     1,   0.0,    0.4,   0.4},
            {0, 6, 1,  DecoyState::Decoy,  1, 1,  3, 0, "1",     1,   0.0,    0.666, 0.666},
        };

        PSMList psmRows;
        for (size_t i=0; i < sizeof(testPSMs)/sizeof(TestPSM); ++i)
            psmRows.push_back(testPSMs[i].psm());

        Qonverter::Settings settings;
        settings.scoreInfoByName["score"].weight = 1;
        settings.scoreInfoByName["score"].order = Qonverter::Settings::Order::Ascending;
        settings.scoreInfoByName["score"].normalizationMethod = Qonverter::Settings::NormalizationMethod::Off;
        settings.qonverterMethod = Qonverter::QonverterMethod::PartitionedSVM;
        settings.terminalSpecificityHandling = Qonverter::TerminalSpecificityHandling::Feature;
        settings.missedCleavagesHandling = Qonverter::MissedCleavagesHandling::Feature;
        SVMQonverter::Qonvert("", vector<string>(1, "score"), psmRows, settings);
        
        for (size_t i=0; i < psmRows.size(); ++i)
            cout << psmRows[i].spectrum << " "
                 << psmRows[i].totalScore << " "
                 << psmRows[i].qValue << " "
                 << psmRows[i].fdrScore << endl;

        for (size_t i=0; i < psmRows.size(); ++i)
        {
            unit_assert_operator_equal(testPSMs[i].newRank, psmRows[i].newRank);
            unit_assert_equal(testPSMs[i].totalScore, psmRows[i].totalScore, 1e-6);
            unit_assert_equal(testPSMs[i].qValue, psmRows[i].qValue, 1e-3);
            unit_assert_equal(testPSMs[i].fdrScore, psmRows[i].fdrScore, 1e-3);
        }
    }
}*/


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        testPartition();
        testDiscriminate();
        testStaticWeightQonverter();
        testMonteCarloQonverter();
        //testSVMQonverter();
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
