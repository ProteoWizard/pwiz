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
 *class definition for PrideXmlReader.h
 */

#include "PrideXmlReader.h"
#include "BlibMaker.h"

using namespace std;

namespace BiblioSpec {

PrideXmlReader::PrideXmlReader(BlibBuilder& maker,
                               const char* xmlfilename,
                               const ProgressIndicator* parentProgress)
: BuildParser(maker, xmlfilename, parentProgress),
    scoreType_(UNKNOWN_SCORE_TYPE),
    foundScoreType_(false),
    curState_(ROOT_STATE)
{
   this->setFileName(xmlfilename); // this is for the saxhandler
   setSpecFileName(xmlfilename,    // this is for the BuildParser
                   false);         // don't look for the file
   lookUpBy_ = INDEX_ID;

   // point to self as spec reader
   delete specReader_;
   specReader_ = this;
}

PrideXmlReader::~PrideXmlReader()
{
    specReader_ = NULL; // so the parent class doesn't try to delete itself
    // free spectra
    map<int,SpecData*>::iterator it;
    for(it = spectra_.begin(); it != spectra_.end(); ++it){
        if (it->second != NULL)
            delete it->second;
        it->second = NULL;
    }
}

/**
 * Called by saxhandler when a new xml start tag is reached.  Collect
 * information from each tag according to element type and the current state.
 */
void PrideXmlReader::startElement(const XML_Char* name, 
                                  const XML_Char** attr)
{
    if (isElement("spectrum", name))
    {
        parseSpectrum(attr);
    }
    else if (isElement("ionSelection", name))
    {
        newState(ION_SELECTION_STATE);
    }
    else if (isElement("cvParam", name))
    {
        parseCvParam(attr);
    }
    else if (isElement("mzArrayBinary", name))
    {
        newState(PEAKS_MZ_STATE);
    }
    else if (isElement("intenArrayBinary", name))
    {
        newState(PEAKS_INTENSITY_STATE);
    }
    else if (isElement("data", name))
    {
        parseData(attr);
    }
    else if (isElement("PeptideItem", name))
    {
        parsePeptideItem();
    }
    else if (isElement("Sequence", name))
    {
        newState(PEPTIDE_SEQUENCE_STATE);
    }
    else if (isElement("SpectrumReference", name))
    {
        newState(SPECTRUM_REFERENCE_STATE);
    }
    else if (isElement("ModificationItem", name))
    {
        parseModificationItem(attr);
    }
    else if (isElement("ModLocation", name))
    {
        newState(MOD_LOCATION_STATE);
    }
    else if (isElement("ModMonoDelta", name))
    {
        newState(MOD_MONO_DELTA_STATE);
    }

}

/**
 * Called by saxhandler when the closing tag of an xml element is
 * reached.  Change the state based on the tag and the current state.
 */
void PrideXmlReader::endElement(const XML_Char* name)
{
    if (isElement("spectrum", name))
    {
        saveSpectrum();
    }
    else if (isElement("ionSelection", name))
    {
        curState_ = getLastState();
    }
    else if (isElement("mzArrayBinary", name))
    {
        curState_ = getLastState();
    }
    else if (isElement("intenArrayBinary", name))
    {
        curState_ = getLastState();
    }
    else if (isElement("data", name) &&
             (curState_ == PEAKS_MZ_DATA_STATE || curState_ == PEAKS_INTENSITY_DATA_STATE))
    {
        endData();
    }
    else if (isElement("PeptideItem", name))
    {
        endPeptideItem();
    }
    else if (isElement("Sequence", name))
    {
        endSequence();
    }
    else if (isElement("SpectrumReference", name))
    {
        curState_ = getLastState();
    }
    else if (isElement("ModificationItem", name))
    {
        curPSM_->mods.push_back(curMod_);
    }
    else if (isElement("ModLocation", name))
    {
        curState_ = getLastState();
    }
    else if (isElement("ModMonoDelta", name))
    {
        curState_ = getLastState();
    }
}

void PrideXmlReader::parseSpectrum(const XML_Char** attr)
{
    // reset counts
    numMzs_ = 0;
    numIntensities_ = 0;

    // setup a SpecData
    curSpec_ = new SpecData;
    curSpec_->id = getIntRequiredAttrValue("id", attr);
    curSpec_->retentionTime = 0.0;  // PRIDE files do not have retention time
}

void PrideXmlReader::parseCvParam(const XML_Char** attr)
{
    string nameAttr(getRequiredAttrValue("name", attr));
    
    // get precursor info
    if (curState_ == ION_SELECTION_STATE)
    {
        if (nameAttr == "Mass To Charge Ratio")
        {
            double mz = getDoubleRequiredAttrValue("value", attr);
            curSpec_->mz = mz;
        }
        else if (nameAttr == "Charge State")
        {
            int chargeState = getIntRequiredAttrValue("value", attr);
            spectraChargeStates_[curSpec_->id] = chargeState;
        }
    }
    // get score info
    else if (curState_ == PEPTIDE_ITEM_STATE)
    {
        if (nameAttr == "Sequest score" || nameAttr == "Mascot score")
        {
            PSM_SCORE_TYPE curType;
            if (nameAttr == "Sequest score") curType = SEQUEST_XCORR;
            else if (nameAttr == "Mascot score") curType = MASCOT_IONS_SCORE;

            if (!foundScoreType_)
            {
                foundScoreType_ = true;
                scoreType_ = curType;
                
                // set threshold
                if (scoreType_ == SEQUEST_XCORR) threshold_ = getScoreThreshold(SQT);
                else if (scoreType_ == MASCOT_IONS_SCORE) threshold_ = getScoreThreshold(MASCOT);
            }
            else if (scoreType_ != curType)
            {
                Verbosity::warn("Score type conflict, expected %d but was %d",
                                scoreType_, curType);
            }
            double score = getDoubleRequiredAttrValue("value", attr);
            curPSM_->score = score;
        }
        else if (nameAttr.find("score") != string::npos)
        {
            // Found some unknown cvParam in PeptideItem containing "score"
            foundScoreType_ = true;
            double score = getDoubleRequiredAttrValue("value", attr);
            curPSM_->score = score;

            Verbosity::warn("Found unknown score type in PeptideItem: %s",
                            nameAttr.c_str());
        }
    }
}

void PrideXmlReader::parseData(const XML_Char** attr)
{
    string endian(getRequiredAttrValue("endian", attr));
    int length = getIntRequiredAttrValue("length", attr);
    string precision(getRequiredAttrValue("precision", attr));

    curBinaryConfig_.byteOrder = (endian == "big") ? BinaryDataEncoder::ByteOrder_BigEndian : BinaryDataEncoder::ByteOrder_LittleEndian;
    curBinaryConfig_.precision = (precision == "32") ? BinaryDataEncoder::Precision_32 : BinaryDataEncoder::Precision_64;

    if (curState_ == PEAKS_MZ_STATE)
    {
        numMzs_ = length;
        newState(PEAKS_MZ_DATA_STATE);
    }
    else if (curState_ == PEAKS_INTENSITY_STATE)
    {
        numIntensities_ = length;
        newState(PEAKS_INTENSITY_DATA_STATE);
    }
}

void PrideXmlReader::endData()
{
    if (curState_ == PEAKS_MZ_DATA_STATE)
    {
        // decode mzs
        BinaryDataEncoder encoder(curBinaryConfig_);
        vector<double> decoded;
        encoder.decode(dataMzs_, decoded);

        curSpec_->mzs = new double[decoded.size()];
        copy(decoded.begin(), decoded.end(), curSpec_->mzs);

        dataMzs_.clear();
        curState_ = getLastState();
    }
    else if (curState_ == PEAKS_INTENSITY_DATA_STATE)
    {
        // decode intensities
        BinaryDataEncoder encoder(curBinaryConfig_);
        vector<double> decoded;
        encoder.decode(dataIntensities_, decoded);

        vector<float> decodedToFloats(decoded.begin(), decoded.end());

        curSpec_->intensities = new float[decodedToFloats.size()];
        copy(decodedToFloats.begin(), decodedToFloats.end(), curSpec_->intensities);

        dataIntensities_.clear();
        curState_ = getLastState();
    }
}

void PrideXmlReader::parsePeptideItem()
{
    curPSM_ = new PSM();
    newState(PEPTIDE_ITEM_STATE);
}

void PrideXmlReader::endPeptideItem()
{
    // Loop through mods and make sure positions are [1, peptide length]
    for(vector<SeqMod>::iterator iter = curPSM_->mods.begin();
        iter != curPSM_->mods.end();
        ++iter)
    {
        iter->position = min((int)curPSM_->unmodSeq.length(), max(iter->position, 1));
    }

    // save psm if above threshold or unknown scores type
    if ((scoreType_ != UNKNOWN_SCORE_TYPE && curPSM_->score > threshold_) ||
        (scoreType_ == UNKNOWN_SCORE_TYPE))
    {
        psms_.push_back(curPSM_);
    }

    curState_ = getLastState();
}

void PrideXmlReader::endSequence()
{
    // done reading peptide sequence
    curPSM_->unmodSeq = dataPeptide_;

    dataPeptide_.clear();
    curState_ = getLastState();
}

void PrideXmlReader::parseModificationItem(const XML_Char** attr)
{
    curMod_.position = -1;
    curMod_.deltaMass = 0;
}

bool PrideXmlReader::parseFile()
{
    bool success = parse();

    if (success)
    {
        // add psms of the scoring type
        buildTables(scoreType_, "");
    }

    return success;
}

/**
 * Handler for all characters between tags.
 */
void PrideXmlReader::characters(const XML_Char *s, int len)
{
    if (curState_ != PEAKS_MZ_DATA_STATE &&
        curState_ != PEAKS_INTENSITY_DATA_STATE &&
        curState_ != PEPTIDE_SEQUENCE_STATE &&
        curState_ != SPECTRUM_REFERENCE_STATE &&
        curState_ != MOD_LOCATION_STATE &&
        curState_ != MOD_MONO_DELTA_STATE)
    {
        return;
    }

    // get characters
    string buf(s, len);

    if (curState_ == PEAKS_MZ_DATA_STATE)
    {
        dataMzs_.append(buf);
    }
    else if (curState_ == PEAKS_INTENSITY_DATA_STATE)
    {
        dataIntensities_.append(buf);
    }
    else if (curState_ == PEPTIDE_SEQUENCE_STATE)
    {
        dataPeptide_.append(buf);
    }
    else if (curState_ == SPECTRUM_REFERENCE_STATE)
    {
        int specRef = atoi(buf.c_str());
        curPSM_->specIndex = specRef;
        curPSM_->charge = spectraChargeStates_[specRef];
    }
    else if (curState_ == MOD_LOCATION_STATE)
    {
        int modLocation = atoi(buf.c_str());
        curMod_.position = modLocation;
    }
    else if (curState_ == MOD_MONO_DELTA_STATE)
    {
        double modMonoDelta = atof(buf.c_str());
        curMod_.deltaMass = modMonoDelta;
    }
}

/**
 * Transition to a new state (usually at the start of a new element)
 * by saving the current in the history stack and setting the current
 * to the new.
 */
void PrideXmlReader::newState(STATE nextState)
{
    stateHistory_.push_back(curState_);
    curState_ = nextState;
}

/**
 * Return to the previous state (usually at the end of an element) by
 * popping the last state off the stack and setting the current to it.
 */
PrideXmlReader::STATE PrideXmlReader::getLastState()
{
    STATE lastState = stateHistory_.at(stateHistory_.size() - 1);
    stateHistory_.pop_back();
    return lastState;
}

/**
 * Saves the current data as a new spectrum in the spectra map.
 * Computes precursor mz from mass and charge.  Clears the current
 * data in preparation for the next PSM to parse.
 */
void PrideXmlReader::saveSpectrum()
{
    // confirm that we have the same number of m/z's and intensities
    if (numIntensities_ != numMzs_)
    {
        throw BlibException(false, "Different numbers of peaks. Spectrum %d "
                            "has %d fragment m/z values and %d intensities.",
                            curPSM_->specIndex, numMzs_, numIntensities_);
    }
    curSpec_->numPeaks = numMzs_;
    
    // add to map
    Verbosity::debug("Saving spectrum id %d.",
                      curSpec_->id);
    spectra_[curSpec_->id] = curSpec_;
}

// SpecFileReader methods
/**
 * Implemented to satisfy SpecFileReader interface.  Since spec and
 * results files are the same, no need to open a new one.
 */
void PrideXmlReader::openFile(const char* filename, bool mzSort)
{
    return;
}

void PrideXmlReader::setIdType(SPEC_ID_TYPE type){}

/**
 * Return a spectrum via the returnData argument.  If not found in the
 * spectra map, return false and leave returnData unchanged.
 */
bool PrideXmlReader::getSpectrum(int identifier, 
                                 SpecData& returnData,
                                 SPEC_ID_TYPE findBy,
                                 bool getPeaks)
{
    map<int, SpecData*>::iterator found = spectra_.find(identifier);
    if (found == spectra_.end())
    {
        return false;
    }

    SpecData* foundData = found->second;
    returnData = *foundData;
    return true;
}

bool PrideXmlReader::getSpectrum(string identifier, 
                                 SpecData& returnData, 
                                 bool getPeaks)
{
    Verbosity::warn("PrideXmlReader cannot fetch spectra by string "
                    "identifier, only by spectrum index.");
    return false;
}

/**
 *  For now, only specific spectra can be accessed from the
 *  PrideXmlReader.
 */
bool PrideXmlReader::getNextSpectrum(SpecData& returnData, bool getPeaks)
{
    Verbosity::warn("PrideXmlReader does not support sequential file "
                    "reading.");
    return false;
}

} // namespace
