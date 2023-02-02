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


namespace BiblioSpec {

PrideXmlReader::PrideXmlReader(BlibBuilder& maker,
                               const char* xmlfilename,
                               const ProgressIndicator* parentProgress)
: BuildParser(maker, xmlfilename, parentProgress),
    scoreType_(UNKNOWN_SCORE_TYPE),
    threshold_(-1),
    thresholdIsMax_(false),
    curState_(ROOT_STATE),
    isScoreLookup_(false)
{
   this->setFileName(xmlfilename); // this is for the saxhandler
   setSpecFileName(xmlfilename,    // this is for the BuildParser
                   false);         // don't look for the file

   lookUpBy_ = INDEX_ID;

   curBinaryConfig_.compression = BinaryDataEncoder::Compression_None;

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
        prepareCharRead(PEPTIDE_SEQUENCE_STATE);
    }
    else if (isElement("SpectrumReference", name))
    {
        prepareCharRead(SPECTRUM_REFERENCE_STATE);
    }
    else if (isElement("ModificationItem", name))
    {
        parseModificationItem(attr);
    }
    else if (isElement("ModLocation", name))
    {
        prepareCharRead(MOD_LOCATION_STATE);
    }
    else if (isElement("ModMonoDelta", name))
    {
        prepareCharRead(MOD_MONO_DELTA_STATE);
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
        lastState();
    }
    else if (isElement("mzArrayBinary", name))
    {
        lastState();
    }
    else if (isElement("intenArrayBinary", name))
    {
        lastState();
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
        endSpectrumReference();
    }
    else if (isElement("ModificationItem", name))
    {
        curPSM_->mods.push_back(curMod_);
    }
    else if (isElement("ModLocation", name))
    {
        endModLocation();
    }
    else if (isElement("ModMonoDelta", name))
    {
        endModMonoDelta();
    }
}

void PrideXmlReader::parseSpectrum(const XML_Char** attr)
{
    // reset counts
    numMzs_ = 0;
    numIntensities_ = 0;

    // setup a SpecData
    curSpec_ = new SpecData();
    curSpec_->id = getIntRequiredAttrValue("id", attr);
}

void PrideXmlReader::parseCvParam(const XML_Char** attr)
{
    string nameAttr(getRequiredAttrValue("name", attr));
    // make name lowercase for case insensitive comparison
    transform(nameAttr.begin(), nameAttr.end(), nameAttr.begin(), ::tolower);
    
    // get precursor info
    if (curState_ == ION_SELECTION_STATE)
    {
        if (nameAttr == "mass to charge ratio" || // PSI:1000040
            nameAttr == "selected ion m/z"        // MS:1000744
            )
        {
            double mz = getDoubleRequiredAttrValue("value", attr);
            curSpec_->mz = mz;
        }
        else if (nameAttr == "charge state") // PSI:1000041 ; MS:1000041
        {
            int chargeState = getIntRequiredAttrValue("value", attr);
            spectraChargeStates_[curSpec_->id] = chargeState;
        }
        else if (nameAttr == "parent ion retention time")   // PRIDE:0000203
        {
            double rt = getDoubleRequiredAttrValue("value", attr);
            curSpec_->retentionTime = rt;
        }
        else if (nameAttr == "retention time")  // PSI:RETENTION TIME ; MS:1000894
        {
            const char* rtStr = getRequiredAttrValue("value", attr);
            double rt;
            if (sscanf(rtStr, "PT%lfS", &rt) > 0) // in seconds
                curSpec_->retentionTime = rt / 60; // to minutes
            else
                curSpec_->retentionTime = atof(rtStr) / 60;
        }
    }
    // get score info
    else if (curState_ == PEPTIDE_ITEM_STATE)
    {
        if (nameAttr == "mass to charge ratio" || // PSI:1000040
            nameAttr == "selected ion m/z"        // MS:1000744
            )
        {
            // found a m/z in the PeptideItem, keep track of it
            // and save it later when we reach the end of the PeptideItem
            foundMz_ = getDoubleRequiredAttrValue("value", attr);            
        }
        // is it a charge state?
        if (nameAttr == "charge state") // PSI:1000041 ; MS:1000041
        {
            // found charge state, don't bother saving to spectraChargeStates_
            int chargeState = getIntRequiredAttrValue("value", attr);
            curPSM_->charge = chargeState;
        }
        else
        {
            // determine type of score, if any
            PSM_SCORE_TYPE curType = UNKNOWN_SCORE_TYPE;
            if      (nameAttr == "x correlation" || // PRIDE:0000013
                     nameAttr == "sequest:xcorr")   // MS:1001155
            {
                curType = SEQUEST_XCORR;
            }
            else if (nameAttr == "mascot score" || // PRIDE:0000069
                     nameAttr == "mascot:score")   // MS:1001171
            {
                curType = MASCOT_IONS_SCORE;
            }
            else if (nameAttr == "expect" ||        // PRIDE:0000183
                     nameAttr == "x!tandem:expect") // MS:1001330
            {
                curType = TANDEM_EXPECTATION_VALUE;
                if (scoreType_ == UNKNOWN_SCORE_TYPE) setThreshold(TANDEM, true);
            }
            else if (nameAttr == "spectrum mill peptide score" || // PRIDE:0000177
                     nameAttr == "spectrummill:score")            // MS:1001572
            {
                curType = SPECTRUM_MILL;
            }
            else if (nameAttr == "percolator:q value") // MS:1001491
            {
                curType = PERCOLATOR_QVALUE;
            }
            else if (nameAttr == "peptideprophet probability score") // PRIDE:0000099
            {
                curType = PEPTIDE_PROPHET_SOMETHING;
                if (scoreType_ == UNKNOWN_SCORE_TYPE) setThreshold(PEPXML, false);
            }
            else if (nameAttr == "scaffold:peptide probability") // MS:1001568
            {
                curType = SCAFFOLD_SOMETHING;
                if (scoreType_ == UNKNOWN_SCORE_TYPE) setThreshold(SCAFFOLD, false);
            }
            else if (nameAttr == "omssa e-value" || // PRIDE:0000185
                     nameAttr == "omssa:evalue")    // MS:1001328
            {
                curType = OMSSA_EXPECTATION_SCORE;
                if (scoreType_ == UNKNOWN_SCORE_TYPE) setThreshold(OMSSA, true);
            }
            else if (nameAttr == "proteinprospector:expectation value") // MS:1002045
            {
                curType = PROTEIN_PROSPECTOR_EXPECT;
                if (scoreType_ == UNKNOWN_SCORE_TYPE) setThreshold(PROT_PROSPECT, true);
            }

            // recognized score type
            if (curType != UNKNOWN_SCORE_TYPE)
            {
                // we've found score type for this file
                if (scoreType_ == UNKNOWN_SCORE_TYPE) {
                    scoreType_ = curType;
                    if (isScoreLookup_) {
                        throw EndEarlyException();
                    }
                }

                if (scoreType_ == curType)
                {
                    // save score
                    double score = getDoubleRequiredAttrValue("value", attr);
                    curPSM_->score = score;
                }
                else
                {
                    Verbosity::warn("Skipping unexpected score type, expected %s but was %s",
                                    scoreTypeToString(scoreType_), scoreTypeToString(curType));
                }
            }
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
        prepareCharRead(PEAKS_MZ_DATA_STATE);
    }
    else if (curState_ == PEAKS_INTENSITY_STATE)
    {
        numIntensities_ = length;
        prepareCharRead(PEAKS_INTENSITY_DATA_STATE);
    }
}

void PrideXmlReader::endData()
{
    if (curState_ == PEAKS_MZ_DATA_STATE)
    {
        // decode mzs
        BinaryDataEncoder encoder(curBinaryConfig_);
        pwiz::util::BinaryData<double> decoded;
        encoder.decode(charBuf_, decoded);

        if (decoded.size() != numMzs_)
        {
            // check if the length attribute was the number of bytes
            size_t decodedBytes = getDecodedNumBytes(charBuf_);
            if (decodedBytes != numMzs_)
            {
                Verbosity::warn("Length attribute (%d) did not match number of m/zs (%d) or bytes (%d)",
                                numMzs_, decoded.size(), decodedBytes);
            }
            numMzs_ = decoded.size();
        }

        curSpec_->mzs = new double[decoded.size()];
        copy(decoded.begin(), decoded.end(), curSpec_->mzs);

        lastState();
    }
    else if (curState_ == PEAKS_INTENSITY_DATA_STATE)
    {
        // decode intensities
        BinaryDataEncoder encoder(curBinaryConfig_);
        pwiz::util::BinaryData<double> decoded;
        encoder.decode(charBuf_, decoded);

        if (decoded.size() != numIntensities_)
        {
            // check if the length attribute was the number of bytes
            size_t decodedBytes = getDecodedNumBytes(charBuf_);
            if (decodedBytes != numIntensities_)
            {
                Verbosity::warn("Length attribute (%d) did not match number of intensities (%d) or bytes (%d)",
                                numIntensities_, decoded.size(), decodedBytes);
            }
            numIntensities_ = decoded.size();
        }

        vector<float> decodedToFloats(decoded.begin(), decoded.end());

        curSpec_->intensities = new float[decodedToFloats.size()];
        copy(decodedToFloats.begin(), decodedToFloats.end(), curSpec_->intensities);

        lastState();
    }
}

size_t PrideXmlReader::getDecodedNumBytes(string base64)
{
    // base 64 string length must be multiple of 4
    if (base64.length() % 4 != 0)
    {
        return -1;
    }
    // count number of padding bytes
    int padding = 0;
    for (size_t i = base64.length() - 1; base64[i] == '=' && i >= 0; --i)
    {
        ++padding;
    }

    // every 4 base 64 bytes decodes to 3 bytes
    return base64.length() / 4 * 3 - padding;
}

void PrideXmlReader::parsePeptideItem()
{
    curPSM_ = new PSM();
    foundMz_ = 0;
    newState(PEPTIDE_ITEM_STATE);
}

void PrideXmlReader::endPeptideItem()
{
    // we found an m/z in this PeptideItem, save it if needed
    if (foundMz_ > 0)
    {
        map<int, SpecData*>::iterator found = spectra_.find(curPSM_->specIndex);
        if (found == spectra_.end())
        {
            Verbosity::warn("Failed saving m/z to spectrum %d",
                            curPSM_->specIndex);
        }
        // set precursor m/z if it wasn't set in the Spectrum
        else if (found->second->mz <= 0)
        {
            found->second->mz = foundMz_;
        }
    }

    // Loop through mods and make sure positions are [1, peptide length]
    for(vector<SeqMod>::iterator iter = curPSM_->mods.begin();
        iter != curPSM_->mods.end();
        ++iter)
    {
        iter->position = min((int)curPSM_->unmodSeq.length(), max(iter->position, 1));
    }

    // save psm if above threshold
    if ((curPSM_->score >= threshold_ && !thresholdIsMax_) ||
        (curPSM_->score <= threshold_ && thresholdIsMax_))
    {
        psms_.push_back(curPSM_);
    }

    lastState();
}

void PrideXmlReader::endSequence()
{
    // done reading peptide sequence
    curPSM_->unmodSeq = charBuf_;
    lastState();
}

void PrideXmlReader::endSpectrumReference()
{
    // done reading spectrum reference
    int specRef = atoi(charBuf_.c_str());
    curPSM_->specIndex = specRef;
    curPSM_->charge = spectraChargeStates_[specRef];

    lastState();
}

void PrideXmlReader::parseModificationItem(const XML_Char** attr)
{
    curMod_.position = -1;
    curMod_.deltaMass = 0;
}

void PrideXmlReader::endModLocation()
{
    int modLocation = atoi(charBuf_.c_str());
    curMod_.position = modLocation;

    lastState();
}

void PrideXmlReader::endModMonoDelta()
{
    double modMonoDelta = atof(charBuf_.c_str());
    curMod_.deltaMass = modMonoDelta;

    lastState();
}

void PrideXmlReader::prepareCharRead(STATE dataState)
{
    charBuf_.clear();
    newState(dataState);
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

vector<PSM_SCORE_TYPE> PrideXmlReader::getScoreTypes() {
    isScoreLookup_ = true;
    parse();
    return vector<PSM_SCORE_TYPE>(1, scoreType_);
}

/**
 * Handler for all characters between tags.
 */
void PrideXmlReader::characters(const XML_Char *s, int len)
{
    if (curState_ == PEAKS_MZ_DATA_STATE ||
        curState_ == PEAKS_INTENSITY_DATA_STATE ||
        curState_ == PEPTIDE_SEQUENCE_STATE ||
        curState_ == SPECTRUM_REFERENCE_STATE ||
        curState_ == MOD_LOCATION_STATE ||
        curState_ == MOD_MONO_DELTA_STATE)
    {
        charBuf_.append(s, len);
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
void PrideXmlReader::lastState()
{
    STATE lastState = stateHistory_.at(stateHistory_.size() - 1);
    stateHistory_.pop_back();
    curState_ = lastState;
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

void PrideXmlReader::setThreshold(BUILD_INPUT type, bool isMax)
{
    threshold_ = getScoreThreshold(type);
    thresholdIsMax_ = isMax;
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
