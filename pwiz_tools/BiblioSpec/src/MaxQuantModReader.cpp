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

    // Values taken from MaxQuant MonoIsotopicMass property at (https://github.com/JurgenCox/compbio-base/blob/master/BaseLibS/Mol/ChemElements.cs)
    // (the mass of the most abundant isotope)
    elementMasses_["H"] = 1.0078250321;
    elementMasses_["[1H]"] = 1.0078250321;
    elementMasses_["Hx"] = elementMasses_["2H"] = 2.014101778;
    elementMasses_["T"] = 3.0160492777;
    elementMasses_["He"] = 4.00260325415;
    elementMasses_["Li"] = 7.016004;
    elementMasses_["B"] = 11.0093055;
    elementMasses_["Be"] = 9.0121822;
    elementMasses_["C"] = 12;
    elementMasses_["Cx"] = elementMasses_["13C"] = 13.0033548378;
    elementMasses_["N"] = 14.0030740052;
    elementMasses_["Nx"] = elementMasses_["15N"] = 15.0001088984;
    elementMasses_["O"] = 15.9949146221;
    elementMasses_["Ox"] = 17.9991604;
    elementMasses_["Oy"] = 16.9991315;
    elementMasses_["F"] = 18.9984032;
    elementMasses_["Ne"] = 19.9924401754;
    elementMasses_["Na"] = 22.98976967;
    elementMasses_["Mg"] = 23.9850417;
    elementMasses_["Al"] = 26.98153863;
    elementMasses_["P"] = 30.97376151;
    elementMasses_["S"] = 31.97207069;
    elementMasses_["Sx"] = 33.96786683;
    elementMasses_["Sy"] = 32.9714585;
    elementMasses_["Si"] = 27.9769265325;
    elementMasses_["Cl"] = 34.96885271;
    elementMasses_["Clx"] = 36.9659026;
    elementMasses_["Ar"] = 39.9623831225;
    elementMasses_["K"] = 38.9637069;
    elementMasses_["Kx"] = 40.96182597;
    elementMasses_["Sc"] = 44.9559119;
    elementMasses_["Ti"] = 47.9479463;
    elementMasses_["Ca"] = 39.9625912;
    elementMasses_["V"] = 50.9439595;
    elementMasses_["Cr"] = 51.9405075;
    elementMasses_["Mn"] = 54.9380451;
    elementMasses_["Fe"] = 55.9349421;
    elementMasses_["Fex"] = 55.9349421;
    elementMasses_["Fey"] = 56.9353987;
    elementMasses_["Ni"] = 57.9353479;
    elementMasses_["Co"] = 58.933195;
    elementMasses_["Cu"] = 62.9296011;
    elementMasses_["Zn"] = 63.9291466;
    elementMasses_["Ga"] = 68.9255736;
    elementMasses_["Ge"] = 73.9211778;
    elementMasses_["As"] = 74.9215964;
    elementMasses_["Se"] = 79.9165218;
    elementMasses_["Br"] = 78.9183376;
    elementMasses_["Kr"] = 83.911507;
    elementMasses_["Rb"] = 84.911789738;
    elementMasses_["Sr"] = 87.9056121;
    elementMasses_["Y"] = 88.9058483;
    elementMasses_["Zr"] = 89.9047044;
    elementMasses_["Nb"] = 92.9063781;
    elementMasses_["Rh"] = 102.905504;
    elementMasses_["Ag"] = 106.905093;
    elementMasses_["Mo"] = 97.9054078;
    elementMasses_["Tc"] = 98.9062547;
    elementMasses_["Ru"] = 101.9043493;
    elementMasses_["Pd"] = 105.903486;
    elementMasses_["Cd"] = 113.9033585;
    elementMasses_["Sb"] = 120.9038157;
    elementMasses_["Sn"] = 119.9021947;
    elementMasses_["I"] = 126.904468;
    elementMasses_["In"] = 114.903878;
    elementMasses_["Te"] = 129.9062244;
    elementMasses_["La"] = 138.9063533;
    elementMasses_["Ce"] = 139.9054387;
    elementMasses_["Xe"] = 131.9041535;
    elementMasses_["Ba"] = 137.9052472;
    elementMasses_["Cs"] = 132.905451933;
    elementMasses_["Pr"] = 140.9076528;
    elementMasses_["Nd"] = 141.9077233;
    elementMasses_["Pm"] = 144.912749;
    elementMasses_["Eu"] = 152.9212303;
    elementMasses_["Sm"] = 151.9197324;
    elementMasses_["Gd"] = 157.9241039;
    elementMasses_["Tb"] = 158.9253468;
    elementMasses_["Dy"] = 163.9291748;
    elementMasses_["Ho"] = 164.9303221;
    elementMasses_["Er"] = 165.9302931;
    elementMasses_["Tm"] = 168.9342133;
    elementMasses_["Yb"] = 173.9388621;
    elementMasses_["Lu"] = 174.9407718;
    elementMasses_["Hf"] = 179.94655;
    elementMasses_["Ta"] = 180.9479958;
    elementMasses_["Re"] = 186.9557531;
    elementMasses_["Ir"] = 192.9629264;
    elementMasses_["W"] = 183.9509312;
    elementMasses_["Os"] = 191.9614807;
    elementMasses_["Pt"] = 194.9647911;
    elementMasses_["Au"] = 196.966552;
    elementMasses_["Hg"] = 201.970626;
    elementMasses_["Pb"] = 207.9766521;
    elementMasses_["Tl"] = 204.9744275;
    elementMasses_["Bi"] = 208.9803987;
    elementMasses_["Po"] = 208.9824304;
    elementMasses_["At"] = 209.987148;
    elementMasses_["Rn"] = 222.0175777;
    elementMasses_["Fr"] = 223.0197359;
    elementMasses_["Ra"] = 226.0254098;
    elementMasses_["Ac"] = 227.0277521;
    elementMasses_["Th"] = 232.0380553;
    elementMasses_["Pa"] = 231.035884;
    elementMasses_["U"] = 238.0507882;
    elementMasses_["Np"] = 237.0481734;
    elementMasses_["Pu"] = 244.064204;
    elementMasses_["Am"] = 243.0613811;
    elementMasses_["Cm"] = 247.070354;
    elementMasses_["Bk"] = 247.070307;
    elementMasses_["Cf"] = 251.079587;
    elementMasses_["Es"] = 252.08298;
    elementMasses_["Fm"] = 257.095105;
    elementMasses_["Md"] = 258.098431;
    elementMasses_["No"] = 259.10103;
    elementMasses_["Lr"] = 262.10963;
    elementMasses_["Rf"] = 265.1167;
    elementMasses_["Db"] = 268.12545;
    elementMasses_["Sg"] = 271.13347;
    elementMasses_["Bh"] = 272.13803;
    elementMasses_["Hs"] = 270.13465;
    elementMasses_["Mt"] = 276.15116;
    elementMasses_["Ds"] = 281.16206;
    elementMasses_["Rg"] = 280.16447;
    elementMasses_["Cn"] = 285.17411;
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
