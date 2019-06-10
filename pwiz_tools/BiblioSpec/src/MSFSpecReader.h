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
 * This program sequentially parses MSF spectrum xml files.
 */

#pragma once

#include <vector>
#include "saxhandler.h"


namespace BiblioSpec {

/**
 * \class A class for reading MSF spectrum xml files.
 * Uses the sax handler to read the XML file.
 */
class MSFSpecReader : public SAXHandler
{
public:
    MSFSpecReader(const char* xmlFilename, vector<double>* mzs, vector<float>* intensities);
    MSFSpecReader(string& xmlData, vector<double>* mzs, vector<float>* intensities);
    ~MSFSpecReader();

    //parser methods
    virtual void startElement(const XML_Char* name, const XML_Char** attr);
    virtual void endElement(const XML_Char* name);
 
private:
    vector<double>* mzs_;
    vector<float>* intensities_;
    bool peakReadingState_;
};

} // namespace
