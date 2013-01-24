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

MaxQuantModReader::MaxQuantModReader(const char* xmlfilename,
                                     map<string, double>* modBank) : SAXHandler(),
    modBank_(modBank)
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
    if (isElement("modification", name))
    {
        string modName = getRequiredAttrValue("title", attr);
        string modComposition = getRequiredAttrValue("composition", attr);
        double modDelta = parseComposition(modComposition);
        modBank_->insert(pair<string, double>(modName, modDelta));
    }
}

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
            curElement = toupper(curElement);
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
                deltaMass += aaMasses_[tolower(curElement)] * convertedAmount;
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

} // namespace
