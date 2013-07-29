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

#include "MaxQuantModReader.h"

using namespace std;

namespace BiblioSpec {

/**
 * Construct MaxQuantModReader for reading modifications.xml file.
 */
MaxQuantModReader::MaxQuantModReader(const char* xmlfilename,
                                     set<MaxQuantModification>* modBank) : SAXHandler(),
    modBank_(modBank), fixedMods_(NULL), state_(ROOT_STATE)
{
    this->setFileName(xmlfilename); // this is for the saxhandler
   
    // initialize amino acid masses
    fill(aaMasses_, aaMasses_ + sizeof(aaMasses_)/sizeof(double), 0);
    AminoAcidMasses::initializeMass(aaMasses_, 1);

    // set heavy masses
    aaMasses_['H'] = 2.01355321270;
    aaMasses_['O'] = 16.9991322;
    aaMasses_['C'] = 13.0033548378;
    aaMasses_['N'] = 15.0001088984;
}

/**
 * Construct MaxQuantModReader for reading fixed mods from mqpar.xml file.
 */
MaxQuantModReader::MaxQuantModReader(const char* xmlfilename,
                                     set<string>* fixedMods,
                                     vector<MaxQuantLabels>* labelBank) : SAXHandler(),
    modBank_(NULL), fixedMods_(fixedMods), labelBank_(labelBank), groupParams_(0),
    rawIndex_(-1), state_(ROOT_STATE)
{
    this->setFileName(xmlfilename);
}

MaxQuantModReader::~MaxQuantModReader()
{
}

/**
 * Called by saxhandler when a new xml start tag is reached.  Collect
 * information from each tag according to element type and the current state.
 */
void MaxQuantModReader::startElement(const XML_Char* name, 
                                     const XML_Char** attr)
{
    // modifications.xml mode
    if (modBank_ && !fixedMods_)
    {
        if (isElement("modification", name))
        {
            curMod_.clear();
            curMod_.name = getRequiredAttrValue("title", attr);
            string modComposition = getRequiredAttrValue("composition", attr);
            curMod_.massDelta = parseComposition(modComposition);
            state_ = MODIFICATION_TAG;
        }
        else if (state_ == MODIFICATION_TAG)
        {
            if (isElement("position", name))
            {
                charBuf_.clear();
                state_ = READING_POSITION;
            }
            else if (isElement("modification_site", name))
            {
                char modSite = (getRequiredAttrValue("site", attr))[0];
                if (modSite >= 'A' && modSite <= 'Z')
                {
                    curMod_.sites.insert(modSite);
                }
            }
        }
    }
    // mqpar.xml mode
    else if (!modBank_ && fixedMods_)
    {
        if (isIElement("fixedModifications", name))
        {
            state_ = FIXED_MODIFICATIONS_TAG;
        }
        else if (state_ == FIXED_MODIFICATIONS_TAG && isIElement("string", name))
        {
            charBuf_.clear();
            state_ = READING_FIXED_MODIFICATION;
        }
        else if (isIElement("filePaths", name))
        {
            state_ = FILEPATHS_TAG;
        }
        else if (state_ == FILEPATHS_TAG && isIElement("string", name))
        {
            charBuf_.clear();
            state_ = READING_FILEPATH;
        }
        else if (isIElement("paramGroupIndices", name))
        {
            state_ = PARAMGROUPINDICES_TAG;
        }
        else if (state_ == PARAMGROUPINDICES_TAG && isIElement("int", name))
        {
            charBuf_.clear();
            state_ = READING_PARAMGROUPINDEX;
        }
        else if (isIElement("GroupParams", name) ||
                 (isIElement("parameterGroups", name) && !paramGroupIndices_.empty()))
        {
            ++groupParams_;
        }
        else if (groupParams_ > 0 && isIElement("labels", name))
        {
            ++rawIndex_;
            state_ = LABELS_TAG;
        }
        else if (state_ == LABELS_TAG && isIElement("string", name))
        {
            charBuf_.clear();
            state_ = READING_LABEL;
        }
    }
}

/**
 * Called by saxhandler when the closing tag of an xml element is
 * reached.  Change the state based on the tag and the current state.
 */
void MaxQuantModReader::endElement(const XML_Char* name)
{
    // modifications.xml mode
    if (modBank_ && !fixedMods_)
    {
        if (isIElement("modification", name))
        {
            modBank_->insert(curMod_);
            state_ = ROOT_STATE;
        }
        else if (state_ == READING_POSITION)
        {
            curMod_.position = stringToPosition(charBuf_);
            state_ = MODIFICATION_TAG;
        }
    }
    // mqpar.xml mode
    else if (!modBank_ && fixedMods_)
    {
        if (isIElement("fixedModifications", name))
        {
            state_ = ROOT_STATE;
        }
        else if (state_ == READING_FIXED_MODIFICATION)
        {
            fixedMods_->insert(charBuf_);
            state_ = FIXED_MODIFICATIONS_TAG;
        }
        else if (isIElement("filePaths", name))
        {
            state_ = ROOT_STATE;
        }
        else if (state_ == READING_FILEPATH)
        {
            string rawBaseName(charBuf_);
            size_t lastSlash = rawBaseName.find_last_of("/\\");
            if (lastSlash != string::npos)
            {
              rawBaseName = rawBaseName.substr(lastSlash + 1);
            }
            size_t extensionBegin = rawBaseName.find_last_of(".");
            if (extensionBegin != string::npos)
            {
              rawBaseName.erase(extensionBegin);
            }
            MaxQuantLabels newLabelingStates(rawBaseName);
            labelBank_->push_back(newLabelingStates);
            state_ = FILEPATHS_TAG;
        }
        else if (isIElement("paramGroupIndices", name))
        {
            // check that each raw file had a corresponding param group index
            if (paramGroupIndices_.size() != labelBank_->size())
            {
                throw BlibException(false, "Number of raw files (%d) did not match "
                                           "number of paramGroupIndices (%d).",
                                           labelBank_->size(), paramGroupIndices_.size());
            }
            state_ = ROOT_STATE;
        }
        else if (state_ == READING_PARAMGROUPINDEX)
        {
            paramGroupIndices_.push_back(lexical_cast<int>(charBuf_));
            state_ = PARAMGROUPINDICES_TAG;
        }
        else if (isIElement("parameterGroups", name) && !paramGroupIndices_.empty())
        {
            // check that each raw file had a corresponding parameterGroup
            for (vector<int>::iterator i = paramGroupIndices_.begin(); i != paramGroupIndices_.end(); ++i)
            {
                if (*i > rawIndex_)
                {
                    throw BlibException(false, "Parameter group index %d was outside the range of "
                                               "of parameter groups (%d).", *i, rawIndex_ + 1);
                }
            }
        }
        else if (isIElement("GroupParams", name))
        {
            // check that each raw file had a corresponding groupParam
            if (--groupParams_ == 0 &&
                (rawIndex_ + 1 != (int)labelBank_->size()))
            {
                throw BlibException(false, "Number of raw files (%d) did not match "
                                           "number of label sets (%d).", labelBank_->size(), rawIndex_ + 1);
            }
        }
        else if (isIElement("labels", name))
        {
            state_ = ROOT_STATE;
        }
        else if (state_ == READING_LABEL)
        {
            vector<string> newLabelSubset;
            // split multiple labels (e.g. "Arg6; Lys4")
            split(newLabelSubset, charBuf_, is_any_of(";"));
            // trim whitespace
            for_each(newLabelSubset.begin(), newLabelSubset.end(),
                boost::bind(&boost::trim<string>, _1, std::locale()));

            if (!paramGroupIndices_.empty())
            {
                // Assign label to all raw files with this parameter index
                for (size_t i = 0; i < paramGroupIndices_.size(); ++i)
                {
                    if (paramGroupIndices_[i] == rawIndex_)
                    {
                        (*labelBank_)[i].addModsStrings(newLabelSubset);
                        Verbosity::debug("Adding to labelBank_[%d] : %s", i, charBuf_.c_str());
                    }
                }
            }
            else
            {
                (*labelBank_)[rawIndex_].addModsStrings(newLabelSubset);
            }

            state_ = LABELS_TAG;
        }
    }
}

/**
 * Handler for all characters between tags.
 */
void MaxQuantModReader::characters(const XML_Char *s, int len)
{
    if (state_ == READING_POSITION ||
        state_ == READING_FIXED_MODIFICATION ||
        state_ == READING_FILEPATH ||
        state_ == READING_PARAMGROUPINDEX ||
        state_ == READING_LABEL)
    {
        charBuf_.append(s, len);
    }
}

/**
 * Return the mass of the given atomic composition.
 */
double MaxQuantModReader::parseComposition(string composition)
{
    double deltaMass = 0.0;

    char curElement = '\0';
    string curAmount;

    for (size_t i = 0; i < composition.length(); i++)
    {
        char curChar = composition[i];
        switch (curChar)
        {
        case 'H':
        case 'O':
        case 'C':
        case 'N':
        case 'P':
        case 'S':
            curElement = tolower(curChar);
            curAmount.clear();
            break;
        case 'x':
            // heavy
            curElement = toupper(curElement);
            break;
        case ' ':
            if (curElement != '\0')
            {
                deltaMass += aaMasses_[curElement];
                curElement = '\0';
            }
            break;
        case '-':
        case '0':
        case '1':
        case '2':
        case '3':
        case '4':
        case '5':
        case '6':
        case '7':
        case '8':
        case '9':
            curAmount += curChar;
            break;
        case '(':
            break;
        case ')':
            try
            {
                int convertedAmount = lexical_cast<int>(curAmount);
                deltaMass += aaMasses_[curElement] * convertedAmount;
            }
            catch (bad_lexical_cast e)
            {
                throw BlibException(false, "Could not convert \"%s\" to int", curAmount.c_str());
            }
            curElement = '\0';
            break;
        default:
            throw BlibException(false, "Invalid character '%c' in modification string %s", 
                                curChar, composition.c_str());
            break;
        }
    }

    // handle case where last character was element
    if (curElement != '\0')
    {
        deltaMass += aaMasses_[curElement];
    }

    return deltaMass;
}

/**
 * Converts a position string from a mqpar file into a MAXQUANT_MOD_POSITION.
 */
MaxQuantModification::MAXQUANT_MOD_POSITION MaxQuantModReader::stringToPosition(string positionString)
{
    const char* positionStringChars = positionString.c_str();

    if (isIElement(positionStringChars, "anywhere"))
    {
        return MaxQuantModification::ANYWHERE;
    }
    else if (isIElement(positionStringChars, "proteinNterm"))
    {
        return MaxQuantModification::PROTEIN_N_TERM;
    }
    else if (isIElement(positionStringChars, "proteinCterm"))
    {
        return MaxQuantModification::PROTEIN_C_TERM;
    }
    else if (isIElement(positionStringChars, "anyNterm"))
    {
        return MaxQuantModification::PROTEIN_C_TERM;
    }
    else if (isIElement(positionStringChars, "anyCterm"))
    {
        return MaxQuantModification::PROTEIN_C_TERM;
    }
    else if (isIElement(positionStringChars, "notNterm"))
    {
        return MaxQuantModification::NOT_N_TERM;
    }
    else if (isIElement(positionStringChars, "notCterm"))
    {
        return MaxQuantModification::NOT_C_TERM;
    }
    else
    {
        throw BlibException(false, "Invalid position value: %s", 
                                   positionStringChars);
    }
}

} // namespace
