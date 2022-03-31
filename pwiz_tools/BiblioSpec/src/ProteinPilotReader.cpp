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

/**
 * The ProteinPilotReader class definition.  A class to parse
 * .group.xml files, the output of ABI's group2xml tool, part of
 * Protein Pilot.
 */

#include "ProteinPilotReader.h"
#include "AminoAcidMasses.h"


/* info for getting spec filenames

<SEARCH xml:id="SEARCH:1:111111">
<PEAKLIST originalfilename="full/path/.wiff"
</SEARCH>
<SPECTRUM>
<MATCH searches="SEARCH:">

*/
namespace BiblioSpec {

ProteinPilotReader::ProteinPilotReader(
                                       BlibBuilder& maker,
                                       const char* xmlFileName,
                                       const ProgressIndicator* parentProgress)
: BuildParser(maker, xmlFileName, parentProgress),
  state_(ROOT_STATE),
  expectedNumPeaks_(0),
  curSpecMz_(0),
  probCutOff_(getScoreThreshold(PROT_PILOT)),
  skipMods_(true),
  skipNTermMods_(false),
  skipCTermMods_(false),
  lastFilePosition_(0)
{
    this->setFileName(xmlFileName); // this is done for the saxhandler
    curPSM_ = NULL;
    curSpec_ = NULL;
    lookUpBy_ = NAME_ID;
    initializeMod();
    // point to self as spec reader
    delete specReader_;
    specReader_ = this;

    initReadAddProgress();
}

ProteinPilotReader::~ProteinPilotReader()
{
    specReader_ = NULL; // so parent class doesn't try to delete
                        // itself
    delete curSpec_;
    // free all spec in map
    map<string, SpecData*>::iterator it;
    for(it = spectrumMap_.begin(); it != spectrumMap_.end(); ++it){
        delete it->second;
        it->second = NULL;
    }
}
        
bool ProteinPilotReader::parseFile()
{
    string filename = getFileName();
    setSpecFileName(filename.c_str());
    int filesize = bfs::file_size(filename) / 1000ull;
    readSpecProgress_ = new ProgressIndicator(filesize);

    Verbosity::debug("ProteinPilotReader is parsing %s (%d kb).", filename.c_str(), filesize);
    bool success = parse();
    if (success)
    {
        Verbosity::debug("ProteinPilotReader finished parsing %s.", filename.c_str());
    }
    else
    {
        Verbosity::debug("ProteinPilotReader failed to parse %s.", filename.c_str());
    }

    if( ! success ){
        return success;
    }

    // add all the psms to the library, one search at a time
    map<string, vector<PSM*> >::iterator searchItr = searchIdPsmMap_.begin();
    // every call to buildTables clears the curSpecFileName_ parameter,
    // but for ProteinPilot all spectra are read from the same file.
    // So save the file name in order to restore it.
    string specFileName = getSpecFileName();
    for(; searchItr != searchIdPsmMap_.end(); ++searchItr){
        psms_ = searchItr->second;
        string filename = searchIdFileMap_[searchItr->first];
        setSpecFileName(specFileName.c_str(), false);
        buildTables(PROTEIN_PILOT_CONFIDENCE, filename);
    }
    return true;
}

vector<PSM_SCORE_TYPE> ProteinPilotReader::getScoreTypes() {
    return vector<PSM_SCORE_TYPE>(1, PROTEIN_PILOT_CONFIDENCE);
}

void ProteinPilotReader::startElement(const XML_Char* name, 
                                      const XML_Char** attr)
{
    // clear buffer for characters()
    nextWord_.clear();

    if (isElement("SEARCH", name)){
        state_ = SEARCH_STATE;
        parseSearchID(attr);
    } else if  (isElement("PEAKLIST", name)) {
        parseSpectrumFilename(attr);
    } else if  (isElement("El", name)) {
        state_ = ELEMENT_STATE; // as in chemical element, not xml element
    } else if (isElement("Mod", name)) {
        state_ = MOD_STATE;
    } else if (isElement("SPECTRUM", name)) {
        //cerr << "starting spectrum" << endl;
        state_ = SPECTRUM_STATE;
        parseSpectrumElement(attr);
    } else if (isElement("MATCH", name)) {
        //cerr << "starting match" << endl;
        parseMatchElement(attr);
    } else if (isElement("MOD_FEATURE", name)) {
        // two kinds of mod-feature elements, in params and out
        // for now only do out of params
        if(state_ == SPECTRUM_STATE){
            parseMatchModElement(attr, false);
        }
    } else if (isElement("TERM_MOD_FEATURE", name)) {
        // two kinds of term-mod-feature elements, in params and out
        // for now only do out of params.
        // (separate from mod-feature for debugging)
        if(state_ == SPECTRUM_STATE){
            parseMatchModElement(attr, true);
        }
    } else if (isElement("MSMSPEAKS", name)) {
        state_ = PEAKS_STATE;
		// HACK: Workaround for variable casing produced by Protein Pilot
		const char* attrName = "Size";
		if (strcmp(getAttrValue(attrName, attr), "") == 0)
		    attrName = "size";
        expectedNumPeaks_ = getIntRequiredAttrValue(attrName, attr);
        curPeaks_.clear();
        peaksStr_.clear();
    }

}

void ProteinPilotReader::endElement(const XML_Char* name)
{
    // chemical element and modification info stored as element values
    // not as attributes, so save the info after the value(s) have been read
    if( state_ == SEARCH_STATE ){
        state_ = ROOT_STATE;
    } else if( state_ == ELEMENT_STATE ){
        if (isElement("Sym", name) ) {
            getElementName();
        } else if (isElement("Mss", name) ){
            getElementMass();
        } else if (isElement("El", name) ){
            state_ = ROOT_STATE;
        }
    } else if( state_ == MOD_STATE){
		// Use Nme or DisplayName, whichever shows up last
        if (isElement("Nme", name) || isElement("DisplayName", name) ) {
            getModName();
        } else if (isElement("Fma", name) ) {
            getModFormula(); // add masses
        } else if (isElement("RpF", name) ) {
            getModFormula( false ); // subtract
        } else if (isElement("Mod", name) ) {
            addMod();
            state_ = ROOT_STATE;
        }
    }

    // go back to spectrum state at the end of matches and peaks
    // go back to root state at the end of spectrum
    if (isElement("SPECTRUM", name) ){
        //cerr << "ending spectrum" << endl;
        saveMatch();
        state_ = ROOT_STATE;

        int position = getCurrentByteIndex() / 1000ull;
        int progress = position - lastFilePosition_;
        lastFilePosition_ = position;
        readSpecProgress_->add(progress);

    } else if (isElement("MATCH", name)) {
        //cerr << "ending match" << endl;
        state_ = SPECTRUM_STATE;
    } else if (isElement("MSMSPEAKS", name)) {
        //cerr << "ending peaks" << endl;
        saveSpectrum();
        state_ = SPECTRUM_STATE;
    } 
}
    
void ProteinPilotReader::parseSearchID(const XML_Char** attr){
    curSearchID_ = getRequiredAttrValue("xml:id", attr);
}

void ProteinPilotReader::parseSpectrumFilename(const XML_Char** attr){
    string filename = getRequiredAttrValue("originalfilename", attr);

    searchIdFileMap_[curSearchID_] = filename;
}

void ProteinPilotReader::parseSpectrumElement(const XML_Char** attr)
{
    // start a new PSM, get id
    curPSM_ = new PSM();
    curPSM_->specName = getRequiredAttrValue("xml:id", attr); 
    retentionTime_ = getDoubleRequiredAttrValue("elution", attr);
}

void ProteinPilotReader::parseMatchElement(const XML_Char** attr)
{
    // make sure we have a spectrum
    if( curPSM_ == NULL || curPSM_->specName.empty() ){

        throw BlibException(false,
                            "Cannot find spectrum associated with match %s, "
                            "sequence %s.", getAttrValue("xml:id", attr),
                            getAttrValue("seq", attr));
    } 

    // get confidence and skip if doesn't pass cutoff 
    // or if it is ranked higher than first
    double score = getDoubleRequiredAttrValue("confidence", attr);
    if( score < probCutOff_ || score < curPSM_->score ) {
        skipMods_ = true;
        return;
    } 
    // find out what spectrum file this came from
    string searchID = getRequiredAttrValue("searches", attr);
    // create a vector of PSMs for this search/file if not present
    map<string, vector<PSM*> >::iterator mapAccess 
        = searchIdPsmMap_.find(searchID);
    if( mapAccess == searchIdPsmMap_.end() ){
        vector<PSM*> tmpPSMs;
        searchIdPsmMap_[searchID] = tmpPSMs;
        curSearchPSMs_ = &(searchIdPsmMap_[searchID]);
    } else {
        curSearchPSMs_ = &(mapAccess->second);
    }

    // if this not the first match, create a new psm
    if( curPSM_->score != 0 ){
        string specName = curPSM_->specName;
        curSearchPSMs_->push_back(curPSM_);
        curPSM_ = new PSM();
        curPSM_->specName = mapAccess == searchIdPsmMap_.end()
            ? specName
            : (mapAccess->second.back())->specName;
    }
    curPSM_->score = score;

    // get charge, m/z, seq
    curPSM_->charge = getIntRequiredAttrValue("charge", attr);
    curPSM_->unmodSeq = getRequiredAttrValue("seq", attr);
    curSpecMz_ = getDoubleRequiredAttrValue("mz", attr);
    //cerr << "keeping match score " << score << endl;
    skipMods_ = false;
    skipNTermMods_ = ( strcmp(getAttrValue("nt", attr), "") == 0) ;
    skipCTermMods_ = ( strcmp(getAttrValue("ct", attr), "") == 0) ;
    
    // Verbosity::debug("Parsed spectrum %s match %s", curPSM_->specName.c_str(), curPSM_->unmodSeq.c_str());
}

void ProteinPilotReader::saveMatch(){
    // if we have a PSM, keep it, if not free the PSM
    if( curPSM_ == NULL ){
        return;
    }

    if( curPSM_->unmodSeq.empty() ){
        delete curPSM_;
        curPSM_ = NULL;
    } else {
        curSearchPSMs_->push_back( curPSM_ );
        curPSM_ = NULL;
    }
}

void ProteinPilotReader::parseMatchModElement(const XML_Char** attr, bool termMod)
{
    
    if( skipMods_ ){
        return;
    }
    SeqMod mod;
    // get the position and the name
    mod.position = getIntRequiredAttrValue("pos", attr);
    if (termMod) {
        // Check for internal consistency, since group2xml can
        // sometimes write bogus TERM_MOD_FEATURE tags.
        if (skipNTermMods_ && mod.position == 1)
            return;
        if (skipCTermMods_ && mod.position == strlen(curPSM_->unmodSeq.c_str()))
            return;
    }
    string name = getRequiredAttrValue("mod", attr);

    // skip if it is the absence of a modification
    if( name.compare(0, strlen("No "), "No ") == 0 ||
        name.compare(0, strlen("no "), "no ") == 0 ){
        return;
    }

    // from the name look up the mass shift
    mod.deltaMass = getModMass(name);

    // add the new mod
    curPSM_->mods.push_back(mod);
} 

double ProteinPilotReader::getModMass(const string& name){
    map<string,double>::iterator found = modTable_.find(name);
    if( found == modTable_.end() ){
        throw BlibException(false, "PSM has an unrecognized mod, %s.", 
                            name.c_str());
    }
    return modTable_[name];//deltaMass;
}

/**
 * Handler for all characters between tags.  We are only interested in
 * the peaks data in the MSMSPEAKS elements and the values for
 * chemical elements and modifications.  Use the state to
 * determine if we are there.
 */
void ProteinPilotReader::characters(const XML_Char *s, int len){
    if( len == 0 || s == NULL ){
        return;
    }
    if( state_ == PEAKS_STATE && curPSM_ != NULL 
        && !curPSM_->unmodSeq.empty() && len > 0 ){

        // copy len characters
        char* buf = new char[len + 1];
        strncpy(buf, s, len);
        buf[len] = '\0';

        // add it to the collected peaks
        peaksStr_ += buf;
        delete [] buf;

    } else if( state_ == MOD_STATE || state_ == ELEMENT_STATE ){
        for(int i=0; i < len; i++ ){
            nextWord_ += s[i];
        }
    }
}

void ProteinPilotReader::saveSpectrum()
{
    if( peaksStr_.empty() ){
        return;
    }
    if( curPSM_ == NULL ){
        throw BlibException(false, 
                            "Found MS/MS peaks but no spectrum information.");
        // or more to the point, it somehow got deleted 
    }

    // translate peaksStr into a vector of peaks
    istringstream peakParser(peaksStr_);
    while( !peakParser.eof() ){
        PEAK_T peak;
        double charge;
        peakParser >> peak.mz >> charge >> peak.intensity;
        if( peak.mz == 0 && peak.intensity == 0 ){ break; }
        // peak location is actually M+H if charge > 0;  adjust
        if( charge > 0 ){
            peak.mz = (peak.mz + (charge - 1)*PROTON_MASS)/charge;
        }
        curPeaks_.push_back(peak);
    }

    // check that we got the correct number
    if( expectedNumPeaks_ != curPeaks_.size() ){
        Verbosity::comment(V_ALL, "peaksStr is %s", peaksStr_.c_str());
        throw BlibException(false, "Spectrum %s should have %d peaks but "
                            "%d were read.", curPSM_->specName.c_str(), 
                            expectedNumPeaks_, curPeaks_.size());
    }

    // sort them
    sort(curPeaks_.begin(), curPeaks_.end(), PeakProcessor::compPeakMz);

    // this is the end of the msmspeaks element
    // create a new spectrum and fill in the data
    SpecData* specD = new SpecData();
    specD->retentionTime = retentionTime_;
    specD->mz = curSpecMz_;
    specD->numPeaks = curPeaks_.size();
    specD->mzs = new double[specD->numPeaks];
    specD->intensities = new float [specD->numPeaks];
    for(int i=0; i < specD->numPeaks; i++){
        specD->mzs[i] = curPeaks_[i].mz;
        specD->intensities[i] = curPeaks_[i].intensity;
    }

    // save it in the hash, keyed by spec name
    spectrumMap_[curPSM_->specName] = specD;
}

void ProteinPilotReader::getElementName(){
    // only fill the element table once, even though 
    // there may be multiple copies in the file
    map<string, double>::iterator found = elementTable_.find(nextWord_);
    if( found == elementTable_.end() ){ //it's new, add it
        elementTable_[nextWord_] = -1; // init entry 
        curElement_ = nextWord_;
        //cerr << "Element name is " << nextWord_ << endl;
    } else { // skip it
        curElement_.clear();
    }
}

// ASSUMPTIONS: Always uses the first mass listed for the element
// the example file I have only contains monoisotopic masses and always
// lists the most common one first.  
void ProteinPilotReader::getElementMass(){
    if( elementTable_[curElement_] == -1 ){
        elementTable_[curElement_] = atof(nextWord_.c_str());
        //cerr << "Element mass is " << elementTable_[curElement_] << endl;
    }
}

void ProteinPilotReader::getModName(){
    curMod_.name = nextWord_;
    //cerr << "Mod name is " << curMod_.name << endl;
}

//TODO: some elements have multi letter codes (duh)
// don't store the list of elements, just add up the mass
void ProteinPilotReader::getModFormula( bool add ){
    //cerr << "formula to " << (add ? "add" : "subtract") << " is " << nextWord_ << endl;
    string element;
    int sign = (add ? 1 : -1);

    for(size_t i=0; i < nextWord_.length() ; i++){
        if( nextWord_[i] >= 'A' && nextWord_[i] <= 'Z' ){
            // add the last element we found
            addElement( curMod_.deltaMass, element, sign );
            // set the new element
            element = nextWord_[i];
        } else if( nextWord_[i] >= 'a' &&nextWord_[i] <= 'z' ){
            element += nextWord_[i];
        } else if( nextWord_[i] > '0' &&nextWord_[i] <= '9' ){
            int count = atoi( nextWord_.c_str() + i );
            addElement( curMod_.deltaMass, element, sign * count );
            element.clear();
        }
    }
    // now add the last element (if the formula didn't end with a number
    addElement( curMod_.deltaMass, element, sign );
}

void ProteinPilotReader::addElement(double& mass, string element, int count){
    if( element.empty() ){
        return;
    }

    // look up the element mass
    map<string, double>::iterator found = elementTable_.find(element);
    if( found == elementTable_.end() ){
        throw BlibException(false, "The formula for modification '%s' has an "
                            "unrecognzied element, %s.", curMod_.name.c_str(),
                            element.c_str());
    }
    // add it
    double newMass = found->second;
    mass += (count * newMass);
}

void ProteinPilotReader::initializeMod() {
    curMod_.name.clear();
    curMod_.deltaMass = 0;
}

void ProteinPilotReader::addMod(){
    // first check to see if we already have one for this mod
    map<string, double>::iterator found = modTable_.find(curMod_.name);
    if( found != modTable_.end() && //if it was found and has different mass
        found->second != curMod_.deltaMass ){
        throw BlibException(false, "Two entries for a modification named %s,"
                            "one with delta mass %f and one with %f.",
                            curMod_.name.c_str(), found->second, modTable_[curMod_.name]);
    }

    // else add it
    modTable_[curMod_.name] = curMod_.deltaMass;
    initializeMod();
}

// SpecFileReader functions
// This is the only one really needed
bool ProteinPilotReader::getSpectrum(string scanName, 
                                     SpecData& returnData, 
                                     bool getPeaks){
    Verbosity::comment(V_DETAIL, "Looking for spectrum %s", scanName.c_str());

    map<string,SpecData*>::iterator found = spectrumMap_.find(scanName);
    if( found == spectrumMap_.end() ){
        return false;
    }

    if( ! getPeaks ){
        returnData.numPeaks = 0; // so that they don't get copied
    }
    returnData = *(found->second);
    return true;

}

// Also inherited from SpecFileReader, not needed
void ProteinPilotReader::openFile(const char* filename, bool mzSort){
    Verbosity::debug("ProteinPilotReader is reading spectra from %s", 
                     (getFileName()).c_str());
}

void ProteinPilotReader::setIdType(SPEC_ID_TYPE type){}

bool ProteinPilotReader::getSpectrum(int scanNumber, SpecData& spectrum, 
                                     SPEC_ID_TYPE findBy,
                                     bool getPeaks){
    Verbosity::warn("ProteinPilotReader cannot fetch spectra by scan "
                       "number, only by string identifier.");
    return false;
}

bool ProteinPilotReader::getNextSpectrum(SpecData& spectrum, bool getPeaks){
    Verbosity::warn("Sequential retrivial of spectra not implemented "
                       "for ProteinPilotReader.");
    return false;
}




} // namespace


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
