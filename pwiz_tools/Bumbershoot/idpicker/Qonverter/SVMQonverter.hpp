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
