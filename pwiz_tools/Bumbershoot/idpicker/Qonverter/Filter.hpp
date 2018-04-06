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
//


#ifndef _FILTER_HPP_
#define _FILTER_HPP_

#include <vector>
#include <map>
#include <utility>
#include <string>
#include <pwiz/utility/misc/IterationListener.hpp>
#include <pwiz/utility/chemistry/MZTolerance.hpp>
#include <boost/scoped_ptr.hpp>
#include <boost/exception_ptr.hpp>
#include <boost/optional.hpp>
#include "sqlite3.h"


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


/// an object responsible for filtering an idpDB according to various thresholds and parsimony
struct Filter
{
    Filter();
    ~Filter();

    struct DistinctMatchFormat
    {
        bool isChargeDistinct;
        bool isAnalysisDistinct;
        bool areModificationsDistinct;
        boost::optional<double> modificationMassRoundToNearest;

        string sqlExpression() const;
        string filterHistoryExpression() const;
        void parseFilterHistoryExpression(const string& expression);
    };

    struct Config
    {
        double maxFDRScore;
        int minDistinctPeptides;
        int minSpectra;
        int minAdditionalPeptides;
        bool geneLevelFiltering;
        boost::optional<pwiz::chemistry::MZTolerance> precursorMzTolerance;

        int minSpectraPerDistinctMatch;
        int minSpectraPerDistinctPeptide;
        int maxProteinGroupsPerPeptide;
        DistinctMatchFormat distinctMatchFormat;

        Config()
        {
            maxFDRScore = 0.02;
            minDistinctPeptides = 2;
            minSpectra = 2;
            minAdditionalPeptides = 1;
            geneLevelFiltering = false;

            minSpectraPerDistinctMatch = 1;
            minSpectraPerDistinctPeptide = 1;
            maxProteinGroupsPerPeptide = 10;

            distinctMatchFormat.isAnalysisDistinct = false;
            distinctMatchFormat.isChargeDistinct = true;
            distinctMatchFormat.areModificationsDistinct = true;
            distinctMatchFormat.modificationMassRoundToNearest = 1.0;
        }
    };

    Config config;

    void filter(const string& idpDbFilepath, const pwiz::util::IterationListenerRegistry* ilr = 0);
    void filter(sqlite3* idpDbConnection, const pwiz::util::IterationListenerRegistry* ilr = 0);

    static boost::optional<Config> currentConfig(const string& idpDbFilepath);
    static boost::optional<Config> currentConfig(sqlite3* idpDbConnection);

    struct Impl;

    private:
    boost::scoped_ptr<Impl> _impl;
};

END_IDPICKER_NAMESPACE


#endif // _FILTER_HPP_
