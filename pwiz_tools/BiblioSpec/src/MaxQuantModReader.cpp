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


namespace BiblioSpec {

/**
 * Construct MaxQuantModReader for reading modifications.xml file.
 */
MaxQuantModReader::MaxQuantModReader(const char* xmlfilename,
                                     map<string, MaxQuantModification>* modBank) : SAXHandler(),
    modBank_(modBank), fixedMods_(NULL), state_(ROOT_STATE)
{
    this->setFileName(xmlfilename); // this is for the saxhandler
   
    elementMasses_ = map<string, double>();

    // Values taken from MaxQuant source at (https://github.com/JurgenCox/compbio-base/blob/master/BaseLibS/Mol/ChemElements.cs)
    elementMasses_["H"] = 1.0078250321;
    elementMasses_["O"] = 15.9949146221;
    elementMasses_["C"] = 12.0;
    elementMasses_["N"] = 14.0030740052;
    elementMasses_["P"] = 30.97376151;
    elementMasses_["S"] = 31.97207069;
    elementMasses_["Na"] = 22.98976967;
    // heavy masses
    elementMasses_["Hx"] = elementMasses_["2H"] = 2.014101778;
    elementMasses_["Ox"] = 16.9991315;
    elementMasses_["Cx"] = elementMasses_["13C"] = 13.0033548378;
    elementMasses_["Nx"] = 15.0001088984;
}

/**
 * Construct MaxQuantModReader for reading fixed mods from mqpar.xml file.
 */
MaxQuantModReader::MaxQuantModReader(const char* xmlfilename,
                                     set<string>* fixedMods,
                                     vector<MaxQuantLabels>* labelBank) : SAXHandler(),
    modBank_(NULL), fixedMods_(fixedMods), labelBank_(labelBank), groupParams_(0),
    rawIndex_(-1), haveReadFilenames_(false), state_(ROOT_STATE)
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
        else if (!haveReadFilenames_ && (isIElement("filePaths", name) || isIElement("Filenames", name)))
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
        else if (groupParams_ > 0 && 
                 (isIElement("labels", name) || isIElement("labelMods", name)))
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
            if (curMod_.massDelta != 0.0)
            {
                modBank_->insert(make_pair(curMod_.name, curMod_));
            }
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
        else if (isIElement("filePaths", name) || isIElement("Filenames", name))
        {
            haveReadFilenames_ = true;
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
        else if (state_ == LABELS_TAG &&
                 (isIElement("labels", name) || isIElement("labelMods", name)))
        {
            state_ = ROOT_STATE;
        }
        else if (state_ == READING_LABEL)
        {
            vector<string> newLabelSubset;
            // split multiple labels (e.g. "Arg6; Lys4")
            bal::split(newLabelSubset, charBuf_, bal::is_any_of(";"));
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
double MaxQuantModReader::parseComposition(const string& composition)
{
    double deltaMass = 0.0;

    vector<string> components;
    bal::split(components, composition, bal::is_any_of("\t "), boost::token_compress_on);
    
    for (vector<string>::iterator i = components.begin(); i != components.end(); i++)
    {
        bal::trim(*i);
        if (i->empty())
        {
            continue;
        }

        string element;
        int quantity;

        size_t openQuantity = i->find('(');
        if (openQuantity != string::npos)
        {
            element = i->substr(0, openQuantity);
            size_t closeQuantity = i->find(')', ++openQuantity);
            if (closeQuantity == string::npos)
            {
                // open parenthesis without closing
                Verbosity::warn("Invalid composition '%s': '(' without ')'", composition.c_str());
                return 0.0;
            }
            string quantityStr = i->substr(openQuantity, closeQuantity - openQuantity);
            try
            {
                quantity = boost::lexical_cast<int>(quantityStr);
            }
            catch (boost::bad_lexical_cast&)
            {
                // invalid quantity
                Verbosity::warn("Invalid quantity for '%s': '%s'", composition.c_str(), quantityStr.c_str());
                return 0.0;
            }
        }
        else
        {
            element = *i;
            quantity = 1;
        }

        map<string, double>::const_iterator j = elementMasses_.find(element);
        if (j == elementMasses_.end())
        {
            // element unknown
            Verbosity::warn("Unknown element for '%s': '%s'", composition.c_str(), element.c_str());
            return 0.0;
        }

        deltaMass += quantity * j->second;
    }

    return deltaMass;
}

/**
 * Converts a position string from a mqpar file into a MAXQUANT_MOD_POSITION.
 */
MaxQuantModification::MAXQUANT_MOD_POSITION MaxQuantModReader::stringToPosition(const string& positionString)
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
        return MaxQuantModification::ANY_N_TERM;
    }
    else if (isIElement(positionStringChars, "anyCterm"))
    {
        return MaxQuantModification::ANY_C_TERM;
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
