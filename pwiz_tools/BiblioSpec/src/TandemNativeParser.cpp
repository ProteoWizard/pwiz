//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
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
 *class definition for PepXMLreader.h
 */
/*

Information for getting spec filenames from .xtan files

<group>
<note label="Description">filename scan 333 (charge 2)</note>
</group>

*/

#include "TandemNativeParser.h"
#include "BlibMaker.h"
#include "AminoAcidMasses.h"
#include "pwiz/utility/misc/Std.hpp"


namespace BiblioSpec {

TandemNativeParser::TandemNativeParser(BlibBuilder& maker,
                                       const char* xmlfilename,
                                       const ProgressIndicator* parentProgress)
: BuildParser(maker, xmlfilename, parentProgress),
    probCutOff_(getScoreThreshold(TANDEM)),
    curState_(ROOT_STATE),
    mass_(0),
    numMzs_(0),
    numIntensities_(0)
{
   this->setFileName(xmlfilename); // this is for the saxhandler
   setSpecFileName(xmlfilename, // this is for the BuildParser
                   false); // don't look for the file
   mzs_ = NULL;
   intensities_ = NULL;

   // point to self as spec reader
   delete specReader_;
   specReader_ = this;

   AminoAcidMasses::initializeMass(aaMasses_, 1);
}

TandemNativeParser::~TandemNativeParser() {
    specReader_ = NULL; // so the parent class doesn't try to delete itself
    // free spectra
    map<int,SpecData*>::iterator it;
    for(it = spectra_.begin(); it != spectra_.end(); ++it){
        delete it->second;
        it->second = NULL;
    }
}

/**
 * Called by saxhandler when a new xml start tag is reached.  Collect
 * information from each tag according to element type and the current state.
 */
void TandemNativeParser::startElement(const XML_Char* name, 
                                       const XML_Char** attr)
{
    if(isElement("group", name)){
        parseGroup(attr);
    } else if(isElement("file", name)){
        parseSpectraFile(attr);
    } else if(isElement("note", name)){
        parseNote(attr);
    } else if(isElement("domain", name)){
        newState(DOMAIN_STATE);
        parseDomain(attr);
    } else if(isElement("aa", name)){
        parseMod(attr);
    } else if(isElement("GAML:Xdata", name) && curState_ == PEAKS_STATE){
        newState(PEAKS_MZ_STATE);
    } else if(isElement("GAML:Ydata", name) && curState_ == PEAKS_STATE){
        newState(PEAKS_INTENSITY_STATE);
    } else if(isElement("GAML:values", name)){
        parseValues(attr);
    }

}

/**
 * Called by saxhandler when the closing tag of an xml element is
 * reached.  Change the state based on the tag and the current state.
 */
void TandemNativeParser::endElement(const XML_Char* name)
{
    if(isElement("group", name)){
        endGroup();
    } else if (isElement("note", name)){
        endNote();
    } else if (isElement("domain", name)){
        endDomain();
    } else if (isElement("GAML:Xdata", name) && 
               curState_ == PEAKS_MZ_STATE){
        curState_ = getLastState();
    } else if (isElement("GAML:Ydata", name) && 
               curState_ == PEAKS_INTENSITY_STATE){
        curState_ = getLastState();
    }
}

bool TandemNativeParser::parseFile()
{
    bool success = parse();

    if( success ){
        // add psms by spectrum filename
        map<string, vector<PSM*> >::iterator fileIter = fileMap_.begin();
        for(; fileIter != fileMap_.end(); ++fileIter){
            psms_ = fileIter->second;
            buildTables(TANDEM_EXPECTATION_VALUE, fileIter->first);
        }
    } 

    return success;
}

vector<PSM_SCORE_TYPE> TandemNativeParser::getScoreTypes() {
    return vector<PSM_SCORE_TYPE>(1, TANDEM_EXPECTATION_VALUE);
}

/**
 * Extract data from a group element.  Groups of type = model at the
 * highest level are a single PSM.  Nested groups of type = support
 * contain the peaks.
 */
void TandemNativeParser::parseGroup(const XML_Char** attr){
    const char* type = getRequiredAttrValue("type", attr);
    const char* label = getAttrValue("label", attr);

    // three possible group types: model, support, parameter
    if( strcmp(type, "model") == 0 ){
        curState_ = PSM_GROUP_STATE;
        parsePSM(attr);

    } else if( strcmp(type, "support") == 0 ){
        // cur state now either generic nested group or peaks group
        if( strcmp(label, "fragment ion mass spectrum") == 0  ){
            newState(PEAKS_STATE);
        } else {
            newState(NESTED_GROUP_STATE);
        }
    } else if( strcmp(type, "parameters") == 0){
        if (strcmp(label, "residue mass parameters") == 0) {
            newState(RESIDUE_MASS_PARAMETERS_STATE);
        }
    }
    // type == something else, (e.g. parameter)
}

/**
 * Get the filename of the original spectrum source from the <file>
 * element.
 */
void TandemNativeParser::parseSpectraFile(const XML_Char** attr){
    const char* typeAttr = getAttrValue("type", attr);
    if( curState_ == PEAKS_STATE && (strcmp(typeAttr, "spectra") == 0)){
        curFilename_ = getAttrValue("URL", attr);
    }
}

/**
 * Get the filename of the original spectrum source from the <note>
 * element.
 */
void TandemNativeParser::parseNote(const XML_Char** attr){
    const char* label = getAttrValue("label", attr);
    bool description = strcmp(label, "Description") == 0;
    if (description) {
        retentionTimeStr_.clear();
        if (curFilename_.empty() && curState_ == PEAKS_STATE) {
            newState(DESCRIPTION_STATE);
        }
    }
}

/**
 * Read the sequence for this PSM.
 */
void TandemNativeParser::parseDomain(const XML_Char** attr){

    if( curPSM_ == NULL ){
        throw BlibException(false, "TandemNativeParser encountered a domain "
                            "without an accompanying model group.");
    } else if( curPSM_->unmodSeq.empty() ){
        curPSM_->unmodSeq = getRequiredAttrValue("seq", attr);
        applyResidueMassParameters(curPSM_);
        seqStart_ = getIntRequiredAttrValue("start", attr);
    } else {
        // can we assume that the sequences at other domains are the same?
        string seq = getRequiredAttrValue("seq", attr);
        if( seq != curPSM_->unmodSeq ){
            throw BlibException(false, "Two different sequences given for id "
                                "%d, %s and %s.", curPSM_->specKey, 
                                curPSM_->unmodSeq.c_str(), seq.c_str());
        }
    }
}

/**
 * Get modification for the current sequence.  Only collect them for
 * the first domain element encountered.
 */
void TandemNativeParser::parseMod(const XML_Char** attr){
    const char* aa = getRequiredAttrValue("type", attr);

    if (curState_ == RESIDUE_MASS_PARAMETERS_STATE) {
        double mass = getDoubleRequiredAttrValue("mass", attr);
        double diff = mass - aaMasses_[*aa];
        if (abs(diff) > 0.1) {
            aaMods_[*aa] = diff;
        }
        return;
    }

    if( curPSM_ == NULL ){
        throw BlibException(false, "TandemNativeParser encountered a mod"
                            "ification without an accompanying model group.");
    }

    int protPosition = getIntRequiredAttrValue("at", attr);
    double deltaMass = getDoubleRequiredAttrValue("modified", attr);

    Verbosity::debug("Found modified %s at position %d with delta mass %f.",
                      aa, protPosition, deltaMass);

    // change the position to be relative to the seq start, not the
    // protein start
    int seqPosition = protPosition - seqStart_; // + 1?

    // confirm that the modified aa is present in that position in the seq
    if( curPSM_->unmodSeq.at(seqPosition) != *aa ){
        throw BlibException(false,
                            "Specified modification does not match sequence. "
                            "Given a modified %c at position %d which is a "
                            "%c in %s.", *aa, seqPosition, 
                            curPSM_->unmodSeq.at(seqPosition), 
                            curPSM_->unmodSeq.c_str());
    }
    
    // create a new mod
    SeqMod mod;
    mod.deltaMass = deltaMass;
    mod.position = seqPosition + 1; // mods are 1-based
    curPSM_->mods.push_back(mod);
}

/**
 * Create a new PSM and store the info for the current element.
 */
void TandemNativeParser::parsePSM(const XML_Char** attr){
        // get id, mass, charge, score
        curPSM_ = new PSM();
        curPSM_->charge = getIntRequiredAttrValue("z", attr);      
        curPSM_->specKey = getIntRequiredAttrValue("id", attr);            
        curPSM_->score = getDoubleRequiredAttrValue("expect", attr);      
        mass_ = getDoubleRequiredAttrValue("mh", attr);      
        const char* timeStr = getAttrValue("rt", attr);
        if(strcmp(timeStr, "") != 0) {
            if (sscanf(timeStr, "PT%lfS", &retentionTime_) > 0) // in seconds
                retentionTime_ /= 60; // to minutes
            else
                sscanf(timeStr, "%lf", &retentionTime_); // bare time in minutes
        }
}

/**
 * Prepare to get the array of m/z values for the scan or the array of
 * intensity values.
 */
void TandemNativeParser::parseValues(const XML_Char** attr){

    int numValues = getIntRequiredAttrValue("numvalues", attr);
    if(curState_ == PEAKS_MZ_STATE){
        numMzs_ = numValues;
        mzs_ = new double[numMzs_]; 

    } else if(curState_ == PEAKS_INTENSITY_STATE ){
        numIntensities_ = numValues;
        intensities_ = new float[numIntensities_ ]; 

    } // else values for some other kind of data we don't care about
}

/**
 * Handler for all characters between tags.  We are only interested in
 * two tags: 
 * 1. The peaks data in 
 * <group type="support" label="fragment ion mass spectrum">
 *    <GAML:Xdata><GAML:values>
 * and
 *    <GAML:Ydata><GAML:values>
 * 2. The scan description dta in <note label="Description></note>
 * Use curState to determine if we are there.
 */
void TandemNativeParser::characters(const XML_Char *s, int len){
    // concatinate it on to the appropriate string
    if( curState_ == PEAKS_MZ_STATE){
        mzStr_.append(s, len);
    } else if( curState_ == PEAKS_INTENSITY_STATE){
        intensityStr_.append(s, len);
    } else if( curState_ == DESCRIPTION_STATE ){
        retentionTimeStr_.append(s, len);
        descriptionStr_.append(s, len);
    }
}

void TandemNativeParser::getPeaks(
  istringstream& tokenizer, ///< string version of values
  double* array,            ///< store values here
  int maxSize)              ///< array size
{
    // store values
    int curIdx = 0;
    while( !tokenizer.eof() ){
        assert(curIdx < maxSize);
        tokenizer >> array[curIdx];
        curIdx++;
    }
}

void TandemNativeParser::getPeaks(
  istringstream& tokenizer, ///< string version of values
  float* array,            ///< store values here
  int maxSize)              ///< array size
{
    // store values
    int curIdx = 0;
    while( !tokenizer.eof() ){
        assert(curIdx < maxSize);
        tokenizer >> array[curIdx];
        curIdx++;
    }
}

/**
 * At the end of a group element, update the state.  If it is a PSM
 * group element, save the spectrum if the PSM passes the score
 * threshold and delete the PSM. The PSM should have been saved when
 * its domain element ended.
 */
void TandemNativeParser::endGroup(){

    if( curState_ == NESTED_GROUP_STATE ){
        curState_ = getLastState();
        
    } else if( curState_ == PEAKS_STATE ){
        curState_ = getLastState();

    } else if( curState_ == PSM_GROUP_STATE ){
        curState_ = ROOT_STATE;
        Verbosity::debug("Cur psm has id %d, charge %d, score %f, "
                         "mass %f, seq %s",
                         curPSM_->specKey, curPSM_->charge, 
                         curPSM_->score, mass_, curPSM_->unmodSeq.c_str());
        // keep psm if score passes threshold
        if( curPSM_->score <= probCutOff_ ){
            saveSpectrum();
        }
        // move psm(s) being temporarily held into the file map
        // do we have a slot for this file already?
        map<string, vector<PSM*> >::iterator mapAccess 
            = fileMap_.find(curFilename_);
        if( mapAccess == fileMap_.end() ){ // no, add it
            vector<PSM*> tmpPsms;
            fileMap_[curFilename_] = tmpPsms;
            mapAccess = fileMap_.find(curFilename_);
        }
        for(size_t i = 0; i < psms_.size(); i++){
            mapAccess->second.push_back(psms_[i]);
        }
        psms_.clear();
        
        // we kept a copy of the current psm, don't need it any more
        delete curPSM_;
        curPSM_ = NULL;
        clearCurPeaks();
        
        curFilename_.clear();
    } else if( curState_ == RESIDUE_MASS_PARAMETERS_STATE ){
        curState_ = getLastState();

        for (map<string, vector<PSM*> >::iterator i = fileMap_.begin(); i != fileMap_.end(); i++) {
            for (vector<PSM*>::iterator j = i->second.begin(); j != i->second.end(); j++) {
                applyResidueMassParameters(*j);
            }
        }
    }
}

/**
 * Transition out of Description state.
 */
void TandemNativeParser::endNote(){
    if( curState_ == DESCRIPTION_STATE ){
        curState_ = getLastState();

        const string rtStr = "RTINSECONDS=";
        size_t rtStart = retentionTimeStr_.find(rtStr);
        if (rtStart != string::npos) {
            rtStart += rtStr.length();
            size_t rtEnd = retentionTimeStr_.find_first_not_of("0123456789.", rtStart);
            string rt = (rtEnd != string::npos) ? retentionTimeStr_.substr(rtStart, rtEnd - rtStart) : retentionTimeStr_.substr(rtStart);
            retentionTime_ = atof(rt.c_str()) / 60;
        }

        // File: "F:\QE\07-14-16\QE02179.raw"; SpectrumID: "287"; ...
        size_t fileStart = descriptionStr_.find("File:");
        if (fileStart != string::npos) {
            fileStart += 5;
            while (descriptionStr_[fileStart] != '"') {
                if (fileStart++ >= descriptionStr_.length()) {
                    curFilename_.clear();
                    return;
                }
            }
            fileStart++;
            size_t fileEnd = descriptionStr_.find('"', fileStart + 1);
            if (fileEnd == string::npos) {
                curFilename_.clear();
                return;
            }
            curFilename_ = descriptionStr_.substr(fileStart, fileEnd - fileStart);
            return;
        }

        // parse the filename out of the description
        size_t end = descriptionStr_.find(" ");
        curFilename_ = descriptionStr_.substr(0, end);
    }
}


/**
 * At the end of a domain element, update the state and save the PSM.
 * Create a copy of the PSM, clearing the sequence and modification
 * information in case there is another sequence (domain element)
 * associated with this spectrum.  Since we don't know the filename
 * until we get to the spectrum data, save the PSM temporarily and
 * later move it to the appropriate slot in the fileMap.
 */
void TandemNativeParser::endDomain(){

    curState_ = getLastState();
    if( curPSM_->score <= probCutOff_ ){
        psms_.push_back(curPSM_);

        // create a copy of the current
        PSM* tmpPSM = new PSM();
        tmpPSM->charge = curPSM_->charge;
        tmpPSM->specKey = curPSM_->specKey;
        tmpPSM->score = curPSM_->score;
        curPSM_ = tmpPSM;
    } else { // or if we are not going to accept it, just clear the seq
        curPSM_->unmodSeq.clear();
        curPSM_->mods.clear();
    }
}

/**
 * Convert the string where we collected all the lines containing
 * peaks data into two arrays of m/z and intensity values.
 */
void TandemNativeParser::stringsToPeaks(){

    // replace newlines with space
    replaceAllChar(mzStr_, '\n', ' ');
    replaceAllChar(intensityStr_, '\n', ' ');

    // remove whitespace at the end
    deleteTrailingWhitespace(mzStr_);
    deleteTrailingWhitespace(intensityStr_);
    
    istringstream tokenizer(mzStr_);
    getPeaks(tokenizer, mzs_, numMzs_);

    istringstream tokenizer2(intensityStr_);
    getPeaks(tokenizer2, intensities_, numIntensities_);

}

/**
 * Transition to a new state (usually at the start of a new element)
 * by saveing the current in the history stack and setting the current
 * to the new.
 */
void TandemNativeParser::newState(STATE nextState){
    stateHistory_.push_back(curState_);
    curState_ = nextState;
}

/**
 * Return to the previous state (usually at the end of an element) by
 * popping the last state off the stack and setting the current to it.
 */
TandemNativeParser::STATE TandemNativeParser::getLastState(){
    STATE lastState = stateHistory_.at(stateHistory_.size() - 1);
    stateHistory_.pop_back();
    return lastState;
}

/**
 * Delete the arrays of mz's and intensities, set the size of those
 * two arrays to zero, and clear the peak strings.  
 */
void TandemNativeParser::clearCurPeaks(){
    delete [] mzs_;
    delete [] intensities_;

    numMzs_ = 0;
    numIntensities_ = 0;

    mzStr_.clear();
    intensityStr_.clear();
}

/**
 * Saves the current data as a new spectrum in the spectra map.
 * Computes precursor mz from mass and charge.  Clears the current
 * data in preparation for the next PSM to parse.
 */
void TandemNativeParser::saveSpectrum(){
    // parse the string list of peaks into arrays
    stringsToPeaks();

    // confirm that we have the same number of m/z's and intensities
   if( numIntensities_ != numMzs_ ){
        // TODO get line number
        throw BlibException(false, "Different numbers of peaks. Spectrum %d "
                            "has %d fragment m/z values and %d intensities.",
                            curPSM_->specKey, numMzs_, numIntensities_);
    }

    // compute the mz
    double mz = mass_ / curPSM_->charge; // TODO extra H+??

    // create a new SpecData
    SpecData* curSpec = new SpecData();
    curSpec->mz = mz;

    if (boost::iequals(boost::filesystem::path(curFilename_).extension().string(), ".mgf")) {
        retentionTime_ /= 60;
    }
    curSpec->retentionTime = retentionTime_;
    curSpec->numPeaks = numMzs_;
    curSpec->mzs = mzs_;
    curSpec->intensities = intensities_;
    mzs_ = NULL;
    intensities_ = NULL;

    // add to map
    // check to see if it is already there?
    spectra_[curPSM_->specKey] = curSpec;    

}

void TandemNativeParser::applyResidueMassParameters(PSM* psm) {
    if (aaMods_.empty()) {
        return;
    }
    const string& seq = psm->unmodSeq;
    for (size_t i = 0; i < seq.length(); i++) {
        map<char, double>::const_iterator aaMod = aaMods_.find(seq[i]);
        if (aaMod != aaMods_.end()) {
            psm->mods.push_back(SeqMod(i + 1, aaMod->second));
        }
    }
}

// SpecFileReader methods
/**
 * Implemented to satisfy SpecFileReader interface.  Since spec and
 * results files are the same, no need to open a new one.
 */
void TandemNativeParser::openFile(const char* filename, bool mzSort){
    return;
}

void TandemNativeParser::setIdType(SPEC_ID_TYPE type){}

/**
 * Return a spectrum via the returnData argument.  If not found in the
 * spectra map, return false and leave returnData unchanged.
 */
bool TandemNativeParser::getSpectrum(int identifier, 
                                     SpecData& returnData,
                                     SPEC_ID_TYPE findBy,
                                     bool getPeaks){
    map<int,SpecData*>::iterator found = spectra_.find(identifier);
    if( found == spectra_.end() ){
        return false;
    }

    SpecData* foundData = found->second;
    returnData = *foundData;
    return true;
}

bool TandemNativeParser::getSpectrum(string identifier, 
                                     SpecData& returnData, 
                                     bool getPeaks){
    Verbosity::warn("TandemNativeParser cannot fetch spectra by string "
                    "identifier, only by scan number.");
    return false;
}

/**
 *  For now, only specific spectra can be accessed from the
 *  TandemNativeParser.
 */
bool TandemNativeParser::getNextSpectrum(SpecData& returnData, bool getPeaks){
    Verbosity::warn("TandemNativeParser does not support sequential file "
                    "reading.");
    return false;

}

} // namespace


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
