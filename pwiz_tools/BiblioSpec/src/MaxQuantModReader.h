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
 * This program sequentially parses MaxQuant modifications.xml and mqpar.xml files.
 */

#pragma once

#include "AminoAcidMasses.h"
#include "PSM.h"
#include "saxhandler.h"
#include <boost/algorithm/string.hpp>
#include <cctype>
#include <set>

using namespace std;
using namespace boost;

namespace BiblioSpec {

/**
 * Holds information about MaxQuant modifications.
 */
struct MaxQuantModification
{
public:
    enum MAXQUANT_MOD_POSITION {
        ANYWHERE,
        PROTEIN_C_TERM, PROTEIN_N_TERM,
        ANY_N_TERM, ANY_C_TERM,
        NOT_C_TERM, NOT_N_TERM };

    string name;
    double massDelta;
    MAXQUANT_MOD_POSITION position;
    set<char> sites;

    void clear()
    {
        this->name = "";
        this->massDelta = 0.0;
        this->position = ANYWHERE;
        this->sites.clear();
    }

    /**
     * Given the modification's name, return a pointer to it.
     * Return NULL if not found.
     */
    static const MaxQuantModification* find(set<MaxQuantModification>& modBank, const string& name)
    {
        for (set<MaxQuantModification>::iterator iter = modBank.begin();
             iter != modBank.end();
             ++iter)
        {
            if (iter->name == name)
            {
                return &*iter;
            }
        }
        return NULL;
    }

    /**
     * Implemented so these can be used in a set.
     */
    bool operator<(const MaxQuantModification& other) const
    {
        return name < other.name;
    }
};

/**
 * \class A class for reading MaxQuant modifications.xml files.
 * Uses the sax handler to read the XML file.
 */
class MaxQuantModReader : public SAXHandler
{
public:
    MaxQuantModReader(const char* xmlfilename, set<MaxQuantModification>* modBank);
    MaxQuantModReader(const char* xmlfilename, set<string>* fixedMods);
    ~MaxQuantModReader();

    //parser methods
    virtual void startElement(const XML_Char* name, const XML_Char** attr);
    virtual void endElement(const XML_Char* name);
    virtual void characters(const XML_Char *s, int len);
 
private:
    enum STATE {
        ROOT_STATE,
        MODIFICATION_TAG, READING_POSITION,                 // for modifications.xml reading
        FIXED_MODIFICATIONS_TAG, READING_FIXED_MODIFICATION // for mqpar.xml reading
    };

    double aaMasses_[128];
    MaxQuantModification curMod_;
    set<MaxQuantModification>* modBank_;
    set<string>* fixedMods_;
    STATE state_;
    
    string charBuf_;

    double parseComposition(string composition);
    MaxQuantModification::MAXQUANT_MOD_POSITION stringToPosition(string positionString);
};

} // namespace
