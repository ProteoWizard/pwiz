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


#ifndef _QONVERTER_HPP_
#define _QONVERTER_HPP_

#include <vector>
#include <map>
#include <utility>
#include <string>
#include <boost/enum.hpp>
#include <boost/range.hpp>
#include <boost/ref.hpp>
#include <boost/range/algorithm/sort.hpp>
#include <boost/ptr_container/ptr_vector.hpp>
#include "../Lib/SQLite/sqlite3.h"


#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE


using std::vector;
using std::map;
using std::pair;
using std::string;


namespace DecoyState
{
    // note that (Target | Decoy) == Ambiguous
    enum Type {Target = 1, Decoy = 2, Ambiguous = 3};
    static const char* Symbol = " TDB";
}


/// a generic, minimal representation of an IDPicker PSM with input and output fields
struct PeptideSpectrumMatch
{
    sqlite_int64 id;
    sqlite_int64 spectrum;
#ifdef QONVERTER_HAS_NATIVEID
    string nativeID;
#endif

    int originalRank;
    DecoyState::Type decoyState;

    // all values are floating point so they can be scaled to arbitrary values
    float chargeState;
    float bestSpecificity;
    float missedCleavages;
    double massError;
    vector<double> scores;

    int newRank;
    double totalScore;
    double qValue;
    double fdrScore;
};


/// a factory-like class for calculating the totalScore and qValue fields in a list of PeptideSpectrumMatches
struct Qonverter
{
    BOOST_ENUM_VALUES(QonverterMethod, const char*,
        (StaticWeighted)("static-weighted")
        //(SVM)("SVM-optimized")
        (PartitionedSVM)("SVM-optimized per partition")
        (SingleSVM)("SVM-optimized across partitions")
        (MonteCarlo)("Monte Carlo")
    );

    BOOST_ENUM(SVMType,
        (CSVC)
        (NuSVC)
        (OneClass)
        (EpsilonSVR)
        (NuSVR)
    );

    BOOST_ENUM_VALUES(Kernel, const char*,
        (Linear)("linear")
        (Polynomial)("polynomial")
        (RBF)("radial basis function")
        (Sigmoid)("sigmoid")
    );

    BOOST_BITFIELD(ChargeStateHandling,
        (Ignore)(1<<0)
        (Partition)(1<<1)
        (Feature)(1<<2) // SVM only
    );

    BOOST_BITFIELD(TerminalSpecificityHandling,
        (Ignore)(1<<0)
        (Partition)(1<<1)
        (Feature)(1<<2) // SVM only
    );

    BOOST_ENUM(MissedCleavagesHandling,
        (Ignore)
        (Feature) // SVM only
    );

    BOOST_ENUM(MassErrorHandling,
        (Ignore)
        (Feature) // SVM only
    );

    struct Settings
    {
        BOOST_ENUM(NormalizationMethod,
            (Off)
            (Quantile)
            (Linear)
        );

        BOOST_ENUM(Order,
            (Ascending)
            (Descending)
        );

        struct ScoreInfo
        {
            double weight;
            NormalizationMethod normalizationMethod;
            Order order;
        };

        QonverterMethod qonverterMethod;
        string decoyPrefix;

        /// should ranks within a spectrum be rearranged based on the total score?
        bool rerankMatches;

        /// how should charge state be used during qonversion?
        ChargeStateHandling chargeStateHandling;

        /// how should terminal specificity be used during qonversion?
        TerminalSpecificityHandling terminalSpecificityHandling;

        /// how should missed cleavages be used during qonversion?
        MissedCleavagesHandling missedCleavagesHandling;

        /// how should mass error be used during qonversion?
        MassErrorHandling massErrorHandling;

        SVMType svmType;

        /// for SVM qonversion, what kind of kernel should be used?
        Kernel kernel;

        /// for SVM qonversion, what Q-value threshold should be used for separating true and false positives?
        double truePositiveThreshold;
        int maxTrainingRank;
        bool predictProbability;
        double gamma;
        double nu;
        int degree;
        double maxFDR;

        /// what score names are expected and how should they be weighted and normalized?
        map<string, ScoreInfo> scoreInfoByName;

        Settings()
        {
            qonverterMethod = QonverterMethod::PartitionedSVM;
            rerankMatches = false;
            chargeStateHandling = ChargeStateHandling::Ignore;
            terminalSpecificityHandling = TerminalSpecificityHandling::Ignore;
            missedCleavagesHandling = MissedCleavagesHandling::Ignore;
            massErrorHandling = MassErrorHandling::Ignore;
            svmType = SVMType::CSVC;
            kernel = Kernel::Linear;
            truePositiveThreshold = 0.01;
            maxTrainingRank = 1;
            predictProbability = true;
            gamma = 5;
            nu = 0.5;
            degree = 3;
            maxFDR = 0.02;
        }
    };

    /// configuration is on a per-analysis basis;
    /// the '0' analysis is the fallback if an analysis does not have its own entry
    map<int, Settings> settingsByAnalysis;

    /// if true, the qonvert method will create a QonversionDetails table
    bool logQonversionDetails;

    Qonverter();

    struct ProgressMonitor
    {
        struct UpdateMessage
        {
            int qonvertedAnalyses;
            int totalAnalyses;
            bool cancel;
        };

        virtual void operator() (UpdateMessage& updateMessage) const {};
    };

    void qonvert(const string& idpDbFilepath, const ProgressMonitor& progressMonitor = ProgressMonitor());
    void qonvert(sqlite3* idpDb, const ProgressMonitor& progressMonitor = ProgressMonitor());

    void reset(const string& idpDbFilepath);
    void reset(sqlite3* idpDb);
};


// allow enum values to be the LHS of an equality expression
BOOST_ENUM_DOMAIN_OPERATORS(Qonverter::QonverterMethod);
BOOST_ENUM_DOMAIN_OPERATORS(Qonverter::Kernel);
BOOST_ENUM_DOMAIN_OPERATORS(Qonverter::ChargeStateHandling);
BOOST_ENUM_DOMAIN_OPERATORS(Qonverter::TerminalSpecificityHandling);
BOOST_ENUM_DOMAIN_OPERATORS(Qonverter::MissedCleavagesHandling);
BOOST_ENUM_DOMAIN_OPERATORS(Qonverter::MassErrorHandling);
BOOST_ENUM_DOMAIN_OPERATORS(Qonverter::Settings::NormalizationMethod);
BOOST_ENUM_DOMAIN_OPERATORS(Qonverter::Settings::Order);


/// functor to sort PSMs by original rank
struct OriginalRankLessThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        return lhs.originalRank < rhs.originalRank;
    }
};


/// functor to sort PSMs by new rank
struct NewRankLessThanOrTotalScoreBetterThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        if (lhs.newRank == rhs.newRank)
            return lhs.totalScore > rhs.totalScore;
        return lhs.newRank < rhs.newRank;
    }
};


/// functor to sort PSMs by terminal specificity
struct SpecificityBetterThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        return lhs.bestSpecificity > rhs.bestSpecificity;
    }
};


/// functor to sort PSMs by charge state
struct ChargeStateLessThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        return lhs.chargeState < rhs.chargeState;
    }
};


/// functor to sort PSMs first by terminal specificity then by charge state
struct ChargeAndSpecificityLessThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        if (lhs.bestSpecificity != rhs.bestSpecificity)
            return lhs.bestSpecificity < rhs.bestSpecificity;
        return lhs.chargeState < rhs.chargeState;
    }
};


/// functor to sort PSMs descending by total score
struct TotalScoreBetterThanIgnoringRank
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        if (lhs.totalScore != rhs.totalScore)
            return lhs.totalScore > rhs.totalScore;

        // arbitrary tie-breaker when scores are equal
        return lhs.spectrum < rhs.spectrum;
    }
};


/// functor to sort PSMs first by ascending rank then descending by total score
struct TotalScoreBetterThanWithRank
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        if (lhs.originalRank != rhs.originalRank)
            return lhs.originalRank < rhs.originalRank;
        else if (lhs.totalScore != rhs.totalScore)
            return lhs.totalScore > rhs.totalScore;

        // arbitrary tie-breaker when scores are equal
        return lhs.spectrum < rhs.spectrum;
    }
};


typedef boost::ptr_vector<PeptideSpectrumMatch> PSMList;
typedef PSMList::iterator PSMIterator;
typedef boost::iterator_range<PSMIterator> PSMIteratorRange;


/// partitions PSMs by charge and/or terminal specificity
std::vector<PSMIteratorRange> partition(const Qonverter::Settings& settings, const PSMIteratorRange& psmRows);

/// partitions PSMs by charge and/or terminal specificity
std::vector<PSMIteratorRange> partition(const Qonverter::Settings& settings, PSMList& psmRows);


// a convenience for storing the extrema of a value set; also provides linear scaling between -1 and 1
template <typename T>
struct MinMaxPair
{
    MinMaxPair() : min BOOST_PREVENT_MACRO_SUBSTITUTION ((std::numeric_limits<T>::max)()), max BOOST_PREVENT_MACRO_SUBSTITUTION (-(std::numeric_limits<T>::max)()) {}
    MinMaxPair(const T& initialMin, const T& initialMax) : min BOOST_PREVENT_MACRO_SUBSTITUTION (initialMin), max BOOST_PREVENT_MACRO_SUBSTITUTION (initialMax) {}

    void compare(const T& value) {min = (std::min)(min, value); max = (std::max)(max, value);}

    T& scale(T& value) const {return (min == max ? value : (value = 2 * (value - min) / (max - min) - 1));}
    T& unscale(T& value) const {return (min == max ? value : (value = (value + 1) / 2 * (max - min) + min));}

    T scale_copy(const T& value) const {T copy = value; return scale(copy);}
    T unscale_copy(const T& value) const {T copy = value; return unscale(copy);}

    T min, max;
};


/// normalizes PSM scores within each partition (according to qonverterSettings)
void normalize(const Qonverter::Settings& settings, PSMList& psmRows);


/// calculate new ranks, Q-values and FDRScores based on the presorted PSMIteratorRange (by descending discriminant score)
void discriminate(const PSMIteratorRange& psmRows);

/// calculate new ranks, Q-values and FDRScores based on the presorted PSMList (by descending discriminant score)
void discriminate(PSMList& psmRows);


/// calculate PSM totalScore as the sum of (scores[i] * scoreWeights[i])
void calculateWeightedTotalScore(const PSMIteratorRange& psmRows, const vector<double>& scoreWeights);

END_IDPICKER_NAMESPACE


#endif // _QONVERTER_HPP_
