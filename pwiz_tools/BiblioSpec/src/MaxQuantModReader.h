//
// $Id$
//
//
// Original author: Kaipo Tamura <kaipot@uw.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

/*
 * This program sequentially parses MaxQuant modifications.xml files.
 */

#pragma once

#include "AminoAcidMasses.h"
#include "PSM.h"
#include "saxhandler.h"
#include <cctype>
#include <map>

using namespace std;
using namespace boost;

namespace BiblioSpec {

/**
 * \class A class for reading MaxQuant modifications.xml files.
 * Uses the sax handler to read the XML file.
 */
class MaxQuantModReader : public SAXHandler
{
public:
    MaxQuantModReader(const char* xmlfilename, map<string, double>* modBank);
    ~MaxQuantModReader();

    //parser methods
    virtual void startElement(const XML_Char* name, const XML_Char** attr);
 
private:
    double aaMasses_[128];
    map<string, double>* modBank_;

    double parseComposition(string composition);
};

} // namespace
