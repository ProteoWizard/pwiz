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

using std::vector;
using std::map;
using std::pair;
using std::string;


namespace IDPicker {


#ifdef __cplusplus_cli
namespace native {
#endif


struct StaticWeightQonverter
{
    string decoyPrefix;
    map<string, double> scoreWeights;
    double nTerminusIsSpecificWeight;
    double cTerminusIsSpecificWeight;

    StaticWeightQonverter();

    void Qonvert(const string& idpDbFilepath);
};


#ifdef __cplusplus_cli
} // namespace native
#endif


} // namespace IDPicker


#ifdef __cplusplus_cli

#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "../freicore/pwiz_src/pwiz/utility/bindings/CLI/common/SharedCLI.hpp"
#pragma warning( pop )


using namespace System;
using namespace System::Collections::Generic;


namespace IDPicker {


public ref struct StaticWeightQonverter
{
    property String^ DecoyPrefix;
    property IDictionary<String^, double>^ ScoreWeights;
    property double NTerminusIsSpecificWeight;
    property double CTerminusIsSpecificWeight;

    StaticWeightQonverter();

    void Qonvert(String^ idpDbFilepath);
};


} // namespace IDPicker

#endif // __CLR__


#endif // _QONVERTER_HPP_
