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
// Contributor(s): Surendra Dasari
//


#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Once.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "boost/bind.hpp"
#include "svm.h"
#include "SVMQonverter.hpp"
#include "Qonverter.hpp"
#include <climits>
#include <cfloat>
#include <iostream>


using namespace IDPICKER_NAMESPACE;
using namespace pwiz::svm;


// sorting functors
namespace {

struct FDRScoreLessThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        if (lhs.fdrScore != rhs.fdrScore)
            return lhs.fdrScore < rhs.fdrScore;

        // arbitrary tie-breaker when scores are equal
        return lhs.spectrum < rhs.spectrum;
    }
};

} // sorting functors


// discrimination functions
namespace {

void calculateBestSingleScoreDiscrimination(const Qonverter::Settings& settings,
                                            const PSMIteratorRange& psmRows,
                                            const std::vector<std::string>& scoreNames)
{
    size_t bestIndex = 0;
    Qonverter::Settings::Order bestOrder = Qonverter::Settings::Order::Ascending;
    size_t bestDiscrimination = 0;

    Qonverter::Settings::Order currentOrder = Qonverter::Settings::Order::Ascending;
    size_t currentDiscrimination = 0;

    //cout << "Charge: " << psmRows.front().chargeState << " NET: " << psmRows.front().bestSpecificity << endl;

    size_t scoresSize = psmRows.begin()->scores.size();
    for (size_t i=0; i < scoresSize; ++i)
    {
        if (currentOrder == Qonverter::Settings::Order::Ascending)
            BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
                psm.totalScore = psm.scores[i];
        else
            BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
                psm.totalScore = -psm.scores[i];

        if (settings.rerankMatches)
            boost::sort(psmRows, TotalScoreBetterThanIgnoringRank());
        else
            boost::sort(psmRows, TotalScoreBetterThanWithRank());

        // calculate Q values with the current sort
        discriminate(psmRows);

        /*BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
            cout << psm.spectrum << '\t'
                 << psm.originalRank << '\t'
                 << psm.newRank << '\t'
                 << psm.totalScore << '\t'
                 << psm.qValue << '\t'
                 << psm.fdrScore << '\t'
                 << DecoyState::Symbol[psm.decoyState] << endl;*/

        // iterate backward to find the last Q value within the threshold
        currentDiscrimination = 0;
        for (int j=psmRows.size()-1; j >= 0; --j)
            if ((psmRows.begin()+j)->fdrScore <= settings.truePositiveThreshold)
            {
                currentDiscrimination = j+1;
                break;
            }

        if (currentDiscrimination > bestDiscrimination)
        {
            bestIndex = i;
            bestOrder = currentOrder;
            bestDiscrimination = currentDiscrimination;
        }

        //cout << "Score: " << scoreNames[i] << " Order: " << currentOrder << " Passing: " << currentDiscrimination << " of " << psmRows.size() << endl;

        // try this score index again in descending order
        if (currentOrder == Qonverter::Settings::Order::Ascending)
        {
            currentOrder = Qonverter::Settings::Order::Descending;
            --i;
        }
        else
            currentOrder = Qonverter::Settings::Order::Ascending;
    }
    //cout << endl;

    // return early if the last score was the best
    if (currentDiscrimination == bestDiscrimination)
        return;

    // otherwise, finish by sorting by the most discriminating single score
    if (bestOrder == Qonverter::Settings::Order::Ascending)
        BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
            psm.totalScore = psm.scores[bestIndex];
    else
        BOOST_FOREACH(PeptideSpectrumMatch& psm, psmRows)
            psm.totalScore = -psm.scores[bestIndex];

    if (settings.rerankMatches)
        boost::sort(psmRows, TotalScoreBetterThanIgnoringRank());
    else
        boost::sort(psmRows, TotalScoreBetterThanWithRank());

    discriminate(psmRows);
}

void calculateProbabilityDiscrimination(const Qonverter::Settings& settings, const PSMIteratorRange& psmRows, int maxRank)
{
    // sort PSMs by the SVM probability
    if (settings.rerankMatches)
        boost::sort(psmRows, TotalScoreBetterThanIgnoringRank());
    else
        boost::sort(psmRows, TotalScoreBetterThanWithRank());

    discriminate(psmRows);
}

} // discrimination functions


// scaling functions
namespace {

struct NonScoreFeatureInfo
{
    MinMaxPair<float> bestSpecificity;
    MinMaxPair<float> chargeState;
    MinMaxPair<float> missedCleavages;
    MinMaxPair<double> massError;
};

NonScoreFeatureInfo scaleNonScoreFeatures(const Qonverter::Settings& settings, const PSMIteratorRange& range)
{
    NonScoreFeatureInfo result;

    // first pass: calculate extrema
    // second pass: scale features linearly between -1 and 1

    if (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Feature])
    {
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.chargeState.compare(psm.chargeState);
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.chargeState.scale(psm.chargeState);
    }

    if (settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Feature])
    {
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.bestSpecificity.compare(psm.bestSpecificity);
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.bestSpecificity.scale(psm.bestSpecificity);
    }

    if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
    {
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.missedCleavages.compare(psm.missedCleavages);
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.missedCleavages.scale(psm.missedCleavages);
    }

    if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
    {
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.massError.compare(psm.massError);
        BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
            result.massError.scale(psm.massError);
    }

    return result;
}

void scaleScoreFeatures(const PSMIteratorRange& range)
{
    vector<MinMaxPair<double> > scores(range.begin()->scores.size());

    // first pass: calculate extrema
    BOOST_FOREACH(const PeptideSpectrumMatch& psm, range)
        for (size_t i=0; i < psm.scores.size(); ++i)
            scores[i].compare(psm.scores[i]);

    // second pass: scale features linearly between -1 and 1
    BOOST_FOREACH(PeptideSpectrumMatch& psm, range)
        for (size_t i=0; i < psm.scores.size(); ++i)
            scores[i].scale(psm.scores[i]);
}

} // scaling functions


// debugging functions
namespace {

map<string, pwiz::util::once_flag_proxy> deletedSources;
void initFeatureDetails(const string& sourceName,
                        const Qonverter::Settings& settings,
                        const std::vector<std::string>& scoreNames)
{
    bfs::remove(sourceName + "-details.txt");
    bfs::remove(sourceName + "-scaled-features.tsv");
    
    ofstream psmDetails((sourceName + "-details.txt").c_str(), std::ios::app);
    ofstream psmFeatures((sourceName + "-scaled-features.tsv").c_str(), std::ios::app);

    using std::left;
    psmDetails  << setw(9) << left << "Ordinal"
#ifdef QONVERTER_HAS_NATIVEID
                << setw(50) << left << "NativeID"
#else
                << setw(9) << left << "Spectrum"
#endif
                << setw(8) << left << "Rank"
                << setw(8) << left << "Charge"
                << setw(8) << left << "NET"
                << setw(8) << left << "NMC"
                << setw(12) << left << "MassError"
                << setw(8) << left << "Decoy"
                << setw(17) << left << "BestSingleScore"
                << setw(8) << left << "QValue"
                << endl;

    int featureIndex = 0;
    psmFeatures << "IsTrue";
    if (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Feature])
        psmFeatures << '\t' << ++featureIndex << ":Charge";
    if (settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Feature])
        psmFeatures << '\t' << ++featureIndex << ":NET";
    if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
        psmFeatures << '\t' << ++featureIndex << ":NMC";
    if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
        psmFeatures << '\t' << ++featureIndex << ":MassError";
    for (size_t j=0; j < scoreNames.size(); ++j)
        psmFeatures << '\t' << ++featureIndex << ":" << scoreNames[j];
    psmFeatures << endl;
}

void writePsmDetails(ostream& psmDetails, ostream& psmFeatures,
                     int ordinal,
                     const PeptideSpectrumMatch& psm,
                     DecoyState::Type decoyState,
                     bool truePositive,
                     const Qonverter::Settings& settings,
                     const NonScoreFeatureInfo& nonScoreFeatureInfo)
{
    using std::left;

    float unscaledChargeState = psm.chargeState;
    float unscaledBestSpecificity = psm.bestSpecificity;
    float unscaledMissedCleavages = psm.missedCleavages;
    double unscaledMassError = psm.massError;
    if (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Feature])
        nonScoreFeatureInfo.chargeState.unscale(unscaledChargeState);
    if (settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Feature])
        nonScoreFeatureInfo.bestSpecificity.unscale(unscaledBestSpecificity);
    if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
        nonScoreFeatureInfo.missedCleavages.unscale(unscaledMissedCleavages);
    if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
        nonScoreFeatureInfo.massError.unscale(unscaledMassError);

    psmDetails  << setw(9) << left << ordinal
#ifdef QONVERTER_HAS_NATIVEID
                << setw(50) << left << psm.nativeID
#else
                << setw(9) << left << psm.spectrum
#endif
                << setw(8) << left << psm.originalRank
                << setw(8) << left << unscaledChargeState
                << setw(8) << left << unscaledBestSpecificity
                << setw(8) << left << unscaledMissedCleavages
                << setw(12) << left << unscaledMassError
                << setw(8) << left << DecoyState::Symbol[decoyState]
                << setw(17) << left << psm.totalScore
                << setw(8) << left << psm.qValue
                << "\n";

    int featureIndex = 0;
    psmFeatures << truePositive ? "+1" : "-1";
    if (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Feature])
        psmFeatures << '\t' << ++featureIndex << ':' << psm.chargeState;
    if (settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Feature])
        psmFeatures << '\t' << ++featureIndex << ':' << psm.bestSpecificity;
    if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
        psmFeatures << '\t' << ++featureIndex << ':' << psm.missedCleavages;
    if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
        psmFeatures << '\t' << ++featureIndex << ':' << psm.massError;
    for (size_t j=0; j < psm.scores.size(); ++j)
        psmFeatures << '\t' << ++featureIndex << ':' << psm.scores[j];
    psmFeatures << "\n";
}

void writeFeatureDetails(const string& sourceName,
                         const std::vector<std::string>& scoreNames,
                         const Qonverter::Settings& settings,
                         PSMIteratorRange fullRange, PSMIteratorRange truePositives,
                         const NonScoreFeatureInfo& nonScoreFeatureInfo)
{
    // HACK: delete each source file's feature details once (per process)
    if (deletedSources.count(sourceName) == 0)
        deletedSources[sourceName] = pwiz::util::init_once_flag_proxy;
    boost::call_once(deletedSources[sourceName].flag, boost::bind(&initFeatureDetails, sourceName, settings, scoreNames));

    ofstream psmDetails((sourceName + "-details.txt").c_str(), std::ios::app);
    ofstream psmFeatures((sourceName + "-scaled-features.tsv").c_str(), std::ios::app);

    map<const PeptideSpectrumMatch*, bool> truePositiveByPointer;
    BOOST_FOREACH(const PeptideSpectrumMatch& psm, truePositives)
        truePositiveByPointer[&psm] = true; 

    sqlite3_int64 currentSpectrumId = fullRange.front().spectrum;
    DecoyState::Type currentDecoyState = fullRange.front().decoyState;
    vector<const PeptideSpectrumMatch*> currentPSMs(1, &fullRange.front());

    int ordinal = 0;
    psmDetails << setprecision(6);
    psmFeatures << setprecision(6);
    BOOST_FOREACH(const PeptideSpectrumMatch& psm, fullRange)
    {
        if (currentSpectrumId != psm.spectrum)
        {
            BOOST_FOREACH(const PeptideSpectrumMatch* psm2, currentPSMs)
                writePsmDetails(psmDetails, psmFeatures,
                                ++ordinal, *psm2, currentDecoyState, truePositiveByPointer[psm2],
                                settings, nonScoreFeatureInfo);

            // reset the current spectrum
            currentSpectrumId = psm.spectrum;
            currentDecoyState = psm.decoyState;
            currentPSMs.assign(1, &psm);
        }
        else
        {
            currentDecoyState = static_cast<DecoyState::Type>(currentDecoyState | psm.decoyState);
            currentPSMs.push_back(&psm);
        }
    }
    
    BOOST_FOREACH(const PeptideSpectrumMatch* psm2, currentPSMs)
        writePsmDetails(psmDetails, psmFeatures,
                        ++ordinal, *psm2, currentDecoyState, truePositiveByPointer[psm2],
                        settings, nonScoreFeatureInfo);
}

} // debugging functions


// training and prediction functions
namespace {
void set_node(svm_node* nodes, int index, double value)
{
    nodes[index].index = index + 1;
    nodes[index].value = value;
}

int getNonScoreFeatureCount(const Qonverter::Settings& settings)
{
    return (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Feature] ? 1 : 0) +
           (settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Feature] ? 1 : 0) +
           (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature ? 1 : 0) +
           (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature ? 1 : 0);
}

int set_features(svm_node* nodes, const Qonverter::Settings& settings, const PeptideSpectrumMatch& psm)
{
    int numFeatures = getNonScoreFeatureCount(settings) + psm.scores.size() + 1; // include -1 terminating node
    size_t featureIndex = 0;

    if (settings.chargeStateHandling[Qonverter::ChargeStateHandling::Feature])
        set_node(nodes, featureIndex++, psm.chargeState);

    if (settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Feature])
        set_node(nodes, featureIndex++, psm.bestSpecificity);

    if (settings.missedCleavagesHandling == Qonverter::MissedCleavagesHandling::Feature)
        set_node(nodes, featureIndex++, psm.missedCleavages);

    if (settings.massErrorHandling == Qonverter::MassErrorHandling::Feature)
        set_node(nodes, featureIndex++, psm.massError);

    for (size_t j=0; j < psm.scores.size(); ++j)
        set_node(nodes, featureIndex+j, psm.scores[j]);

    nodes[numFeatures-1].index = -1;

    return numFeatures;
}

void print_null(const char *s) {}

svm_model* trainModel(const Qonverter::Settings& settings,
                      const PSMIteratorRange& truePositiveRange,
                      const PSMIteratorRange& falsePositiveRange)
{
    //cout << "Training with " << truePositiveRange.size() << " true positives and "
    //     << falsePositiveRange.size() << " false positives." << endl;

    svm_problem trainingData;
    trainingData.l = truePositiveRange.size() + falsePositiveRange.size();
    trainingData.y = (double*) malloc(trainingData.l * sizeof(double));
    trainingData.x = (svm_node**) malloc(trainingData.l * sizeof(svm_node*));

    int numFeatures = getNonScoreFeatureCount(settings) + truePositiveRange.front().scores.size() + 1; // include -1 terminating node

    for (int i=0; i < truePositiveRange.size(); ++i)
    {
        const PeptideSpectrumMatch& psm = truePositiveRange[i];
        trainingData.y[i] = 1.0;
        trainingData.x[i] = (svm_node*) malloc(numFeatures * sizeof(svm_node));
        set_features(trainingData.x[i], settings, psm);
    }

    for (int i=0; i < falsePositiveRange.size(); ++i)
    {
        const PeptideSpectrumMatch& psm = falsePositiveRange[i];
        size_t realIndex = i + truePositiveRange.size();
        trainingData.y[realIndex] = -1.0;
        trainingData.x[realIndex] = (svm_node*) malloc(numFeatures * sizeof(svm_node));
        set_features(trainingData.x[realIndex], settings, psm);
    }

    svm_parameter trainingParameters;
    trainingParameters.svm_type = settings.svmType.index();
    trainingParameters.kernel_type = settings.kernel.index();
    trainingParameters.cache_size = 100;
    trainingParameters.degree = settings.degree;//3;
    trainingParameters.gamma = settings.gamma; //1.0 / (numFeatures - 1);
    trainingParameters.coef0 = 0;
    trainingParameters.nu = settings.nu;//0.5;
    trainingParameters.C = 1;
    trainingParameters.eps = 1e-3;
    trainingParameters.p = 0.1;
    trainingParameters.shrinking = 1;
    trainingParameters.probability = settings.predictProbability ? 1 : 0;
    trainingParameters.nr_weight = 0;
    trainingParameters.weight_label = NULL;
    trainingParameters.weight = NULL;

    // weight the classes according to the relative size of the ranges (doesn't help)
    /*int weightLabels[] = {-1, 1};
    double weightValues[] = {1, (double) truePositiveRange.size() / falsePositiveRange.size()};
    trainingParameters.nr_weight = 2;
    trainingParameters.weight_label = &weightLabels[0];
    trainingParameters.weight = &weightValues[0];*/

    svm_model* trainedModel = svm_train(&trainingData, &trainingParameters);

    /*cout << "Performing 3 fold cross-validation." << endl;
    vector<double> predictedLabels(trainingData.l, 0);
    svm_cross_validation(&trainingData, &trainingParameters, 3, &predictedLabels[0]);

    int correctPredictions = 0;
    for (int i=0; i < trainingData.l; ++i)
        if(predictedLabels[i] == trainingData.y[i])
            ++correctPredictions;
    cout << "Cross-validation accuracy: " << 100.0 * correctPredictions / trainingData.l << "%" << endl;*/

    free(trainingData.y);

    // trainingData.x is freed by svm_free_and_destroy_model

    return trainedModel;
}

void testModel(const Qonverter::Settings& settings,
               const svm_model* trainedModel,
               const PSMIteratorRange& testRange)
{
    int numFeatures = getNonScoreFeatureCount(settings) + testRange.front().scores.size() + 1; // include -1 terminating node

    for (int i=0; i < testRange.size(); ++i)
    {
        PeptideSpectrumMatch& psm = testRange[i];

        vector<svm_node> testData(numFeatures);
        set_features(&testData[0], settings, psm);

        vector<double> probabilityEstimates(2, 0); // 0 is true positive, 1 is false positive
        if (settings.svmType == Qonverter::SVMType::EpsilonSVR ||
            settings.svmType == Qonverter::SVMType::NuSVR ||
            !settings.predictProbability)
            svm_predict_values(trainedModel, &testData[0], &probabilityEstimates[0]);
        else
            svm_predict_probability(trainedModel, &testData[0], &probabilityEstimates[0]);
        psm.totalScore = probabilityEstimates[0];
    }
}

void qonvertRange(const Qonverter::Settings& settings,
                  const string& sourceName,
                  const std::vector<std::string>& scoreNames,
                  const NonScoreFeatureInfo& nonScoreFeatureInfo,
                  const PSMIteratorRange& range)
{
    try
    {
        PSMIteratorRange fullRange(range);

        // HACK: we should be able to use default constructor here, but MSVC/boost bug triggers
        //       a debug assertion about incompatible iterators
        PSMIteratorRange truePositiveRange(fullRange.end(), fullRange.end());

        // search from the worst to best Q value to find the first one inside the threshold;
        // true positives have newRank=1 and qValue < truePositiveThreshold
        for (boost::reverse_iterator<PSMIterator>
             itr = boost::rbegin(fullRange),
             end = boost::rend(fullRange);
             itr != end;
             ++itr)
        {
            BOOST_ASSERT(itr->newRank == 1 || itr->fdrScore == 2);
            if (itr->fdrScore <= settings.truePositiveThreshold)
            {
                truePositiveRange = PSMIteratorRange(fullRange.begin(), itr.base());
                break;
            }
        }

        PSMIteratorRange falsePositiveRange = PSMIteratorRange(truePositiveRange.end(), fullRange.end());

        if (truePositiveRange.empty()) {/*cerr << "No true positives." << endl;*/ return;}
        if (falsePositiveRange.empty()) {/*cerr << "No false positives." << endl;*/ return;}

        //writeFeatureDetails(sourceName, scoreNames, settings, fullRange, truePositiveRange, nonScoreFeatureInfo);

        // select the false positive range from all matches over a given Q value and under a given rank
        for (PSMIterator itr = falsePositiveRange.begin();
             itr != falsePositiveRange.end();
             ++itr)
        {
            if (itr->newRank > settings.maxTrainingRank)
            {
                falsePositiveRange = PSMIteratorRange(falsePositiveRange.begin(), itr);
                break;
            }
        }

        // make the class sizes equal for better training
        if (truePositiveRange.size() > falsePositiveRange.size())
            truePositiveRange = PSMIteratorRange(truePositiveRange.begin(), truePositiveRange.begin()+falsePositiveRange.size());
        else if (truePositiveRange.size() < falsePositiveRange.size())
            falsePositiveRange = PSMIteratorRange(falsePositiveRange.begin(), falsePositiveRange.begin()+truePositiveRange.size());

        // train a model
        svm_model* trainedModel = trainModel(settings, truePositiveRange, falsePositiveRange);

        // use the model to predict probabilities for all matches
        testModel(settings, trainedModel, fullRange);

        //svm_save_model("d:/idpicker/branches/idpicker-3/svm.model", trainedModel);

        // the model isn't needed anymore
        svm_free_and_destroy_model(&trainedModel);

        // calculate new Q values for matches under a given rank
        calculateProbabilityDiscrimination(settings, fullRange, 1); // TODO: make threshold a variable
    }
    catch (exception& e)
    {
        throw runtime_error(string("[SVMQonverter::qonvertRange] ") + e.what());
    }
}

} // training and prediction functions


namespace IDPICKER_NAMESPACE
{

void SVMQonverter::Qonvert(const string& sourceName,
                           const std::vector<std::string>& scoreNames,
                           PSMList& psmRows,
                           const Qonverter::Settings& settings)
{
    try
    {
        // make libsvm quiet
        svm_set_print_string_function(&print_null);

        PSMIteratorRange fullRange(psmRows.begin(), psmRows.end());

        /*if (!settings.rerankMatches)
        {
            fullRange = PSMIteratorRange(psmRows.end(), psmRows.end());
            sort(psmRows.begin(), psmRows.end(), OriginalRankLessThan());

            for (PSMIterator itr = psmRows.begin(); itr != psmRows.end(); ++itr)
                if (itr->originalRank > 1)
                {
                    if (fullRange.empty())
                        fullRange = PSMIteratorRange(psmRows.begin(), itr);
                    itr->newRank = itr->originalRank;
                    itr->fdrScore = itr->qValue = 2;
                }
        }*/

        // partition the data by charge and/or terminal specificity (depending on qonverter settings)
        vector<PSMIteratorRange> psmPartitionedRows = partition(settings, fullRange);

        bool chargePartition = settings.chargeStateHandling[Qonverter::ChargeStateHandling::Partition];
        bool specificityPartition = settings.terminalSpecificityHandling[Qonverter::TerminalSpecificityHandling::Partition];

        // across all partitions, scale the non-score features linearly between -1 and 1
        NonScoreFeatureInfo nonScoreFeatureInfo = scaleNonScoreFeatures(settings, fullRange);

        // for each partition, scale the scores linearly between -1 and 1
        BOOST_FOREACH(const PSMIteratorRange& range, psmPartitionedRows)
        {
            /*if (chargePartition && specificityPartition)
                cout << "Partition charge=" << range.front().chargeState
                     << " specificity=" << range.front().bestSpecificity
                     << ": " << range.size() << " spectra" << endl;
            else if (chargePartition)
                cout << "Partition charge=" << range.front().chargeState
                     << ": " << range.size() << " spectra" << endl;
            else if (specificityPartition)
                cout << "Partition specificity=" << range.front().bestSpecificity
                     << ": " << range.size() << " spectra" << endl;
            else
                cout << "No partitions: " << range.size() << " spectra" << endl;*/

            scaleScoreFeatures(range);

            // find the single score which provides the most discrimination under a given Q value
            calculateBestSingleScoreDiscrimination(settings, range, scoreNames);

            // for partitioned SVM, we normalize and qonvert each partition independently
            if (settings.qonverterMethod == Qonverter::QonverterMethod::PartitionedSVM)
            {
                for (int i=0; i < 1; ++i)
                {
                    qonvertRange(settings, sourceName + "-iteration" + lexical_cast<string>(i), scoreNames, nonScoreFeatureInfo, range);
                    
                    /*size_t truePositives = 0;
                    for (boost::reverse_iterator<PSMIterator>
                         itr = boost::rbegin(range),
                         end = boost::rend(range);
                         itr != end;
                         ++itr)
                    {
                        BOOST_ASSERT(itr->originalRank == 1 || itr->qValue == 2);
                        if (itr->qValue <= 0.01)
                        {
                            truePositives = PSMIteratorRange(range.begin(), itr.base()).size();
                            break;
                        }
                    }
                    cout << "Iteration " << (i+1) << ": " << truePositives << endl;*/
                }
            }
        }

        // for single SVM, we qonvert all partitions together after normalizing scores independently
        if (settings.qonverterMethod == Qonverter::QonverterMethod::SingleSVM)
        {
            Qonverter::Settings settings2 = settings;
            settings2.chargeStateHandling = Qonverter::ChargeStateHandling::Partition;
            settings2.terminalSpecificityHandling = Qonverter::TerminalSpecificityHandling::Feature;
            psmPartitionedRows = partition(settings2, fullRange);

        BOOST_FOREACH(const PSMIteratorRange& range, psmPartitionedRows)
        {
            boost::sort(range, FDRScoreLessThan());

            for (int i=0; i < 1; ++i)
            {
                qonvertRange(settings, sourceName + "-iteration" + lexical_cast<string>(i), scoreNames, nonScoreFeatureInfo, range);

                /*size_t truePositives = 0;
                for (boost::reverse_iterator<PSMIterator>
                     itr = boost::rbegin(fullRange),
                     end = boost::rend(fullRange);
                     itr != end;
                     ++itr)
                {
                    BOOST_ASSERT(itr->originalRank == 1 || itr->qValue == 2);
                    if (itr->qValue <= 0.01)
                    {
                        truePositives = PSMIteratorRange(fullRange.begin(), itr.base()).size();
                        break;
                    }
                }
                cout << "Iteration " << (i+1) << ": " << truePositives << endl;*/
            }
        }
        }
    }
    catch (runtime_error&)
    {
        throw;
    }
    catch (exception& e)
    {
        throw runtime_error(string("[SVMQonverter::Qonvert] ") + e.what());
    }
}

} // namespace IDPicker
