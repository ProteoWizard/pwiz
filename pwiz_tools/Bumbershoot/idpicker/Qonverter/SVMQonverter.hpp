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


#ifndef _SVMQONVERTER_HPP_
#define _SVMQONVERTER_HPP_


#include "Qonverter.hpp"


BEGIN_IDPICKER_NAMESPACE


struct SVMQonverter
{
    static void Qonvert(const std::string& sourceName,
                        const std::vector<std::string>& scoreNames,
                        PSMList& psmRows,
                        const Qonverter::Settings& settings);
};


END_IDPICKER_NAMESPACE


#endif // _SVMQONVERTER_HPP_
