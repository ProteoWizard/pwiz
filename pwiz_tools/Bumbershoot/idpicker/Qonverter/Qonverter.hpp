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


#ifndef _QONVERTER_HPP_
#define _QONVERTER_HPP_


#include <vector>
#include <map>
#include <utility>
#include <string>
#include <boost/enum.hpp>
#include <boost/range.hpp>
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

    int originalRank;
    DecoyState::Type decoyState;

    // all values are floating point so they can be scaled to arbitrary values
    double chargeState;
    double bestSpecificity;
    double missedCleavages;
    double massError;
    vector<double> scores;

    double totalScore;
    double qValue;
};


/// a factory-like class for calculating the totalScore and qValue fields in a list of PeptideSpectrumMatches
struct Qonverter
{
    BOOST_ENUM_VALUES(QonverterMethod, const char*,
        (StaticWeighted)("static-weighted")
        (SVM)("SVM-optimized")
    );

    BOOST_ENUM_VALUES(Kernel, const char*,
        (Linear)("linear")
        (Polynomial)("polynomial")
        (RBF)("radial basis function")
        (Sigmoid)("sigmoid")
    );

    BOOST_ENUM(ChargeStateHandling,
        (Ignore)
        (Partition)
        (Feature) // SVM only
    );

    BOOST_ENUM(TerminalSpecificityHandling,
        (Ignore)
        (Partition)
        (Feature) // SVM only
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

        /// for SVM qonversion, what kind of kernel should be used?
        Kernel kernel;

        /// what score names are expected and how should they be weighted and normalized?
        map<string, ScoreInfo> scoreInfoByName;
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


/// functor to sort PSMs by original rank
struct OriginalRankLessThan
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        return lhs.originalRank < rhs.originalRank;
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


typedef std::vector<PeptideSpectrumMatch>::iterator PSMIterator;
typedef boost::iterator_range<PSMIterator> PSMIteratorRange;

/// partitions PSMs by charge and/or terminal specificity
std::vector<PSMIteratorRange> partition(const Qonverter::Settings& settings, const PSMIteratorRange& psmRows);


END_IDPICKER_NAMESPACE


#endif // _QONVERTER_HPP_
