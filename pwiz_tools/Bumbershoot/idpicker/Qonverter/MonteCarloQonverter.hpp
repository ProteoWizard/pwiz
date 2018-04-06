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
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s): Matt Chambers
//


#ifndef _MONTECARLOQONVERTER_HPP_
#define _MONTECARLOQONVERTER_HPP_


#include "Qonverter.hpp"

BEGIN_IDPICKER_NAMESPACE


struct MonteCarloQonverter
{
    typedef map<int, vector<double> > WeightsByBestSpecificity;
    typedef map<int, WeightsByBestSpecificity> WeightsByChargeAndBestSpecificity;

    static void Qonvert(PSMList& psmRows,
                        const Qonverter::Settings& settings,
                        const std::vector<double>& scoreWeights,
                        WeightsByChargeAndBestSpecificity* monteCarloWeightsByChargeAndBestSpecificity = NULL);
};


END_IDPICKER_NAMESPACE


#endif // _MONTECARLOQONVERTER_HPP_
