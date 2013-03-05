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
                                     set<string>* fixedMods) : SAXHandler(),
    modBank_(NULL), fixedMods_(fixedMods), state_(ROOT_STATE)
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
        if (isElement("fixedModifications", name))
        {
            state_ = FIXED_MODIFICATIONS_TAG;
        }
        else if (state_ == FIXED_MODIFICATIONS_TAG && isElement("string", name))
        {
            charBuf_.clear();
            state_ = READING_FIXED_MODIFICATION;
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
        if (isElement("modification", name))
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
        if (isElement("fixedModifications", name))
        {
            state_ = ROOT_STATE;
        }
        else if (state_ == READING_FIXED_MODIFICATION)
        {
            fixedMods_->insert(charBuf_);
            state_ = FIXED_MODIFICATIONS_TAG;
        }
    }
}

/**
 * Handler for all characters between tags.
 */
void MaxQuantModReader::characters(const XML_Char *s, int len)
{
    if (state_ == READING_POSITION ||
        state_ == READING_FIXED_MODIFICATION)
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
    if (iequals(positionString, "anywhere"))
    {
        return MaxQuantModification::ANYWHERE;
    }
    else if (iequals(positionString, "proteinNterm"))
    {
        return MaxQuantModification::PROTEIN_N_TERM;
    }
    else if (iequals(positionString, "proteinCterm"))
    {
        return MaxQuantModification::PROTEIN_C_TERM;
    }
    else if (iequals(positionString, "anyNterm"))
    {
        return MaxQuantModification::PROTEIN_C_TERM;
    }
    else if (iequals(positionString, "anyCterm"))
    {
        return MaxQuantModification::PROTEIN_C_TERM;
    }
    else if (iequals(positionString, "notNterm"))
    {
        return MaxQuantModification::NOT_N_TERM;
    }
    else if (iequals(positionString, "notCterm"))
    {
        return MaxQuantModification::NOT_C_TERM;
    }
    else
    {
        throw BlibException(false, "Invalid position value: %s", 
                                   positionString.c_str());
    }
}

} // namespace
