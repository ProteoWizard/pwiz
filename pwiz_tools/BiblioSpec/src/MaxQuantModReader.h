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
#include <boost/bind.hpp>
#include <boost/filesystem.hpp>
#include <cctype>
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Container.hpp"


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
    static const MaxQuantModification* find(const map<string, MaxQuantModification>& modBank, const string& name)
    {
        auto findItr = modBank.find(name);
        if (findItr == modBank.end())
            return NULL;
        return &findItr->second;
    }
};

/**
 * Represents one labeling state for one raw file from a MaxQuant mqpar.xml file.
 */
struct MaxQuantLabelingState
{
public:
    vector<string> modsStrings;
    vector<const MaxQuantModification*> mods;
};

/**
 * Represents a set of labeling states for one raw file from a MaxQuant mqpar.xml file.
 */
struct MaxQuantLabels
{
public:
    string rawFile;
    vector<MaxQuantLabelingState> labelingStates;

    MaxQuantLabels(const string& filename)
    {
        rawFile = filename;
    }

    /**
     * Adds a new labeling state with the given mod strings.
     */
    void addModsStrings(const vector<string>& modsToAdd)
    {
        MaxQuantLabelingState newLabelingState;
        newLabelingState.modsStrings = modsToAdd;
        labelingStates.push_back(newLabelingState);
    }

    /**
     * Adds MaxQuantModifications to the given labeling state.
     */
    void addMods(vector<MaxQuantLabelingState>::iterator iter, vector<const MaxQuantModification*> modsToAdd)
    {
        iter->mods = modsToAdd;
    }

    /**
     * Given the raw file's name, return a pointer to its labels.
     * Return NULL if not found.
     */
    static const MaxQuantLabels* findLabels(const vector<MaxQuantLabels>& labelBank, const string& filename)
    {
        for (vector<MaxQuantLabels>::const_iterator iter = labelBank.begin();
             iter != labelBank.end();
             ++iter)
        {
            if (iter->rawFile == filename)
            {
                return &*iter;
            }
        }
        return NULL;
    }
private:
};

/**
 * \class A class for reading MaxQuant modifications.xml files.
 * Uses the sax handler to read the XML file.
 */
class MaxQuantModReader : public SAXHandler
{
public:
    MaxQuantModReader(const char* xmlfilename, map<string, MaxQuantModification>* modBank);
    MaxQuantModReader(const char* xmlfilename,
                      set<string>* fixedMods, vector<MaxQuantLabels>* labelBank);
    ~MaxQuantModReader();

    //parser methods
    virtual void startElement(const XML_Char* name, const XML_Char** attr);
    virtual void endElement(const XML_Char* name);
    virtual void characters(const XML_Char *s, int len);
 
private:
    enum STATE {
        ROOT_STATE,
        MODIFICATION_TAG, READING_POSITION,                  // for modifications.xml reading
        FIXED_MODIFICATIONS_TAG, READING_FIXED_MODIFICATION, // for mqpar.xml reading
        FILEPATHS_TAG, READING_FILEPATH,
        PARAMGROUPINDICES_TAG, READING_PARAMGROUPINDEX,
        LABELS_TAG, READING_LABEL
    };

    map<string, double> elementMasses_;
    MaxQuantModification curMod_;
    map<string, MaxQuantModification>* modBank_;
    set<string>* fixedMods_;
    vector<MaxQuantLabels>* labelBank_;
    vector<int> paramGroupIndices_; // raw -> paramGroupIndex
    STATE state_;
    int groupParams_;
    int rawIndex_;
    bool haveReadFilenames_;
    
    string charBuf_;

    double parseComposition(const string& composition);
    MaxQuantModification::MAXQUANT_MOD_POSITION stringToPosition(const string& positionString);
};

} // namespace
