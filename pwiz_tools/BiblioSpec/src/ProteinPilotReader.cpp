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

#define OLDSKOOLDEBUG Verbosity::debug("%s %d", __func__, __LINE__);

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
OLDSKOOLDEBUG
    this->setFileName(xmlFileName); // this is done for the saxhandler
OLDSKOOLDEBUG
    curPSM_ = NULL;
    curSpec_ = NULL;
    lookUpBy_ = NAME_ID;

    // point to self as spec reader
OLDSKOOLDEBUG
    delete specReader_;
OLDSKOOLDEBUG
    specReader_ = this;
OLDSKOOLDEBUG
    initReadAddProgress();
OLDSKOOLDEBUG
}
ProteinPilotReader::~ProteinPilotReader()
{
OLDSKOOLDEBUG
    specReader_ = NULL; // so parent class doesn't try to delete
                        // itself
OLDSKOOLDEBUG
    delete curSpec_;
    // free all spec in map
    map<string, SpecData*>::iterator it;
OLDSKOOLDEBUG
    for(it = spectrumMap_.begin(); it != spectrumMap_.end(); ++it){
OLDSKOOLDEBUG
        delete it->second;
OLDSKOOLDEBUG
        it->second = NULL;
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
}
bool ProteinPilotReader::parseFile()
{
OLDSKOOLDEBUG
    string filename = getFileName();
OLDSKOOLDEBUG
    setSpecFileName(filename.c_str());
OLDSKOOLDEBUG
    int filesize = bfs::file_size(filename) / 1000ull;
OLDSKOOLDEBUG
    readSpecProgress_ = new ProgressIndicator(filesize);

OLDSKOOLDEBUG
    Verbosity::debug("ProteinPilotReader is parsing %s (%d kb).", filename.c_str(), filesize);
OLDSKOOLDEBUG
    bool success = parse();
OLDSKOOLDEBUG
    if (success)
    {
OLDSKOOLDEBUG
        Verbosity::debug("ProteinPilotReader finished parsing %s.", filename.c_str());
    }
    else
    {
OLDSKOOLDEBUG
        Verbosity::debug("ProteinPilotReader failed to parse %s.", filename.c_str());
    }

OLDSKOOLDEBUG
    if( ! success ){
OLDSKOOLDEBUG
        return success;
    }

    // add all the psms to the library, one search at a time
OLDSKOOLDEBUG
    map<string, vector<PSM*> >::iterator searchItr = searchIdPsmMap_.begin();
    // every call to buildTables clears the curSpecFileName_ parameter,
    // but for ProteinPilot all spectra are read from the same file.
    // So save the file name in order to restore it.
    string specFileName = getSpecFileName();
OLDSKOOLDEBUG
    for(; searchItr != searchIdPsmMap_.end(); ++searchItr){
OLDSKOOLDEBUG
        psms_ = searchItr->second;
OLDSKOOLDEBUG
        string filename = searchIdFileMap_[searchItr->first];
OLDSKOOLDEBUG
        setSpecFileName(specFileName.c_str(), false);
OLDSKOOLDEBUG
        buildTables(PROTEIN_PILOT_CONFIDENCE, filename);
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
    return true;
}
void ProteinPilotReader::startElement(const XML_Char* name, 
                                      const XML_Char** attr)
{
    // clear buffer for characters()
OLDSKOOLDEBUG
    nextWord_.clear();

OLDSKOOLDEBUG
    if (isElement("SEARCH", name)){
OLDSKOOLDEBUG
        state_ = SEARCH_STATE;
OLDSKOOLDEBUG
        parseSearchID(attr);
OLDSKOOLDEBUG
    } else if  (isElement("PEAKLIST", name)) {
OLDSKOOLDEBUG
        parseSpectrumFilename(attr);
OLDSKOOLDEBUG
    } else if  (isElement("El", name)) {
OLDSKOOLDEBUG
        state_ = ELEMENT_STATE; // as in chemical element, not xml element
OLDSKOOLDEBUG
    } else if (isElement("Mod", name)) {
OLDSKOOLDEBUG
        state_ = MOD_STATE;
OLDSKOOLDEBUG
    } else if (isElement("SPECTRUM", name)) {
        //cerr << "starting spectrum" << endl;
OLDSKOOLDEBUG
        state_ = SPECTRUM_STATE;
OLDSKOOLDEBUG
        parseSpectrumElement(attr);
OLDSKOOLDEBUG
    } else if (isElement("MATCH", name)) {
        //cerr << "starting match" << endl;
OLDSKOOLDEBUG
        parseMatchElement(attr);
OLDSKOOLDEBUG
    } else if (isElement("MOD_FEATURE", name)) {
        // two kinds of mod-feature elements, in params and out
        // for now only do out of params
        if(state_ == SPECTRUM_STATE){
OLDSKOOLDEBUG
            parseMatchModElement(attr, false);
OLDSKOOLDEBUG
        }
    } else if (isElement("TERM_MOD_FEATURE", name)) {
        // two kinds of term-mod-feature elements, in params and out
        // for now only do out of params.
        // (separate from mod-feature for debugging)
        if(state_ == SPECTRUM_STATE){
OLDSKOOLDEBUG
            parseMatchModElement(attr, true);
OLDSKOOLDEBUG
        }
OLDSKOOLDEBUG
    } else if (isElement("MSMSPEAKS", name)) {
OLDSKOOLDEBUG
        state_ = PEAKS_STATE;
		// HACK: Workaround for variable casing produced by Protein Pilot
OLDSKOOLDEBUG
		const char* attrName = "Size";
OLDSKOOLDEBUG
		if (strcmp(getAttrValue(attrName, attr), "") == 0)
OLDSKOOLDEBUG
		    attrName = "size";
OLDSKOOLDEBUG
        expectedNumPeaks_ = getIntRequiredAttrValue(attrName, attr);
OLDSKOOLDEBUG
        curPeaks_.clear();
OLDSKOOLDEBUG
        peaksStr_.clear();
OLDSKOOLDEBUG
    }

OLDSKOOLDEBUG
}
void ProteinPilotReader::endElement(const XML_Char* name)
{
    // chemical element and modification info stored as element values
    // not as attributes, so save the info after the value(s) have been read
OLDSKOOLDEBUG
    if( state_ == SEARCH_STATE ){
OLDSKOOLDEBUG
        state_ = ROOT_STATE;
OLDSKOOLDEBUG
    } else if( state_ == ELEMENT_STATE ){
OLDSKOOLDEBUG
        if (isElement("Sym", name) ) {
OLDSKOOLDEBUG
            getElementName();
OLDSKOOLDEBUG
        } else if (isElement("Mss", name) ){
OLDSKOOLDEBUG
            getElementMass();
OLDSKOOLDEBUG
        } else if (isElement("El", name) ){
OLDSKOOLDEBUG
            state_ = ROOT_STATE;
OLDSKOOLDEBUG
        }
OLDSKOOLDEBUG
    } else if( state_ == MOD_STATE){
		// Use Nme or DisplayName, whichever shows up last
OLDSKOOLDEBUG
        if (isElement("Nme", name) || isElement("DisplayName", name) ) {
OLDSKOOLDEBUG
            getModName();
OLDSKOOLDEBUG
        } else if (isElement("Fma", name) ) {
OLDSKOOLDEBUG
            getModFormula(); // add masses
OLDSKOOLDEBUG
        } else if (isElement("RpF", name) ) {
OLDSKOOLDEBUG
            getModFormula( false ); // subtract
OLDSKOOLDEBUG
        } else if (isElement("Mod", name) ) {
OLDSKOOLDEBUG
            addMod();
OLDSKOOLDEBUG
            state_ = ROOT_STATE;
OLDSKOOLDEBUG
        }
    }

    // go back to spectrum state at the end of matches and peaks
    // go back to root state at the end of spectrum
    if (isElement("SPECTRUM", name) ){
        //cerr << "ending spectrum" << endl;
OLDSKOOLDEBUG
        saveMatch();
OLDSKOOLDEBUG
        state_ = ROOT_STATE;

OLDSKOOLDEBUG
        int position = getCurrentByteIndex() / 1000ull;
OLDSKOOLDEBUG
        int progress = position - lastFilePosition_;
OLDSKOOLDEBUG
        lastFilePosition_ = position;
OLDSKOOLDEBUG
        readSpecProgress_->add(progress);
OLDSKOOLDEBUG

OLDSKOOLDEBUG
    } else if (isElement("MATCH", name)) {
OLDSKOOLDEBUG
        //cerr << "ending match" << endl;
OLDSKOOLDEBUG
        state_ = SPECTRUM_STATE;
OLDSKOOLDEBUG
    } else if (isElement("MSMSPEAKS", name)) {
OLDSKOOLDEBUG
        //cerr << "ending peaks" << endl;
OLDSKOOLDEBUG
        saveSpectrum();
OLDSKOOLDEBUG
        state_ = SPECTRUM_STATE;
OLDSKOOLDEBUG
    } 
}
void ProteinPilotReader::parseSearchID(const XML_Char** attr){
OLDSKOOLDEBUG
    curSearchID_ = getRequiredAttrValue("xml:id", attr);
OLDSKOOLDEBUG
}
void ProteinPilotReader::parseSpectrumFilename(const XML_Char** attr){
OLDSKOOLDEBUG
    string filename = getRequiredAttrValue("originalfilename", attr);

OLDSKOOLDEBUG
    searchIdFileMap_[curSearchID_] = filename;
OLDSKOOLDEBUG
}
void ProteinPilotReader::parseSpectrumElement(const XML_Char** attr)
{
    // start a new PSM, get id
OLDSKOOLDEBUG
    curPSM_ = new PSM();
OLDSKOOLDEBUG
    curPSM_->specName = getRequiredAttrValue("xml:id", attr); 
OLDSKOOLDEBUG
    retentionTime_ = getDoubleRequiredAttrValue("elution", attr);
OLDSKOOLDEBUG
}
void ProteinPilotReader::parseMatchElement(const XML_Char** attr)
{
    // make sure we have a spectrum
OLDSKOOLDEBUG
    if( curPSM_ == NULL || curPSM_->specName.empty() ){
OLDSKOOLDEBUG
Verbosity::debug("Cannot find spectrum associated with match %s, sequence %s.", getAttrValue("xml:id", attr), getAttrValue("seq", attr));
    throw BlibException(false,
                            "Cannot find spectrum associated with match %s, "
                            "sequence %s.", getAttrValue("xml:id", attr),
                            getAttrValue("seq", attr));
    } 
    // get confidence and skip if doesn't pass cutoff 
    // or if it is ranked higher than first
OLDSKOOLDEBUG
    double score = getDoubleRequiredAttrValue("confidence", attr);
OLDSKOOLDEBUG
    if( score < probCutOff_ || score < curPSM_->score ) {
OLDSKOOLDEBUG
        skipMods_ = true;
OLDSKOOLDEBUG
        return;
    } 
    // find out what spectrum file this came from
OLDSKOOLDEBUG
    string searchID = getRequiredAttrValue("searches", attr);
    // create a vector of PSMs for this search/file if not present
OLDSKOOLDEBUG
    map<string, vector<PSM*> >::iterator mapAccess 
        = searchIdPsmMap_.find(searchID);
OLDSKOOLDEBUG
    if( mapAccess == searchIdPsmMap_.end() ){
OLDSKOOLDEBUG
        vector<PSM*> tmpPSMs;
OLDSKOOLDEBUG
        searchIdPsmMap_[searchID] = tmpPSMs;
OLDSKOOLDEBUG
        curSearchPSMs_ = &(searchIdPsmMap_[searchID]);
OLDSKOOLDEBUG
    } else {
OLDSKOOLDEBUG
        curSearchPSMs_ = &(mapAccess->second);
OLDSKOOLDEBUG
    }
    // if this not the first match, create a new psm
OLDSKOOLDEBUG
    if( curPSM_->score != 0 ){
OLDSKOOLDEBUG
        string specName = curPSM_->specName;
OLDSKOOLDEBUG
        curSearchPSMs_->push_back(curPSM_);
OLDSKOOLDEBUG
        curPSM_ = new PSM();
OLDSKOOLDEBUG
        curPSM_->specName = mapAccess == searchIdPsmMap_.end()
            ? specName
            : (mapAccess->second.back())->specName;
    }
OLDSKOOLDEBUG
    curPSM_->score = score;
OLDSKOOLDEBUG
    // get charge, m/z, seq
OLDSKOOLDEBUG
    curPSM_->charge = getIntRequiredAttrValue("charge", attr);
OLDSKOOLDEBUG
    curPSM_->unmodSeq = getRequiredAttrValue("seq", attr);
OLDSKOOLDEBUG
    curSpecMz_ = getDoubleRequiredAttrValue("mz", attr);
    //cerr << "keeping match score " << score << endl;
OLDSKOOLDEBUG
    skipMods_ = false;
OLDSKOOLDEBUG
    skipNTermMods_ = ( strcmp(getAttrValue("nt", attr), "") == 0) ;
OLDSKOOLDEBUG
    skipCTermMods_ = ( strcmp(getAttrValue("ct", attr), "") == 0) ;
    // Verbosity::debug("Parsed spectrum %s match %s", curPSM_->specName.c_str(), curPSM_->unmodSeq.c_str());
OLDSKOOLDEBUG
}
void ProteinPilotReader::saveMatch(){
    // if we have a PSM, keep it, if not free the PSM
OLDSKOOLDEBUG
    if( curPSM_ == NULL ){
OLDSKOOLDEBUG
        return;
    }

OLDSKOOLDEBUG
    if( curPSM_->unmodSeq.empty() ){
OLDSKOOLDEBUG
        delete curPSM_;
OLDSKOOLDEBUG
        curPSM_ = NULL;
OLDSKOOLDEBUG
    } else {
OLDSKOOLDEBUG
        curSearchPSMs_->push_back( curPSM_ );
OLDSKOOLDEBUG
        curPSM_ = NULL;
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
}
void ProteinPilotReader::parseMatchModElement(const XML_Char** attr, bool termMod)
{
OLDSKOOLDEBUG
    if( skipMods_ ){
OLDSKOOLDEBUG
        return;
    }
OLDSKOOLDEBUG
    SeqMod mod;
    // get the position and the name
OLDSKOOLDEBUG
    mod.position = getIntRequiredAttrValue("pos", attr);
OLDSKOOLDEBUG
    if (termMod) {
        // Check for internal consistency, since group2xml can
        // sometimes write bogus TERM_MOD_FEATURE tags.
OLDSKOOLDEBUG
        if (skipNTermMods_ && mod.position == 1) {
OLDSKOOLDEBUG
                return;
        }
OLDSKOOLDEBUG
        if (skipCTermMods_ && mod.position == strlen(curPSM_->unmodSeq.c_str())) {
            OLDSKOOLDEBUG
                return;
        }
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
    string name = getRequiredAttrValue("mod", attr);
    // skip if it is the absence of a modification
OLDSKOOLDEBUG
    if( name.compare(0, strlen("No "), "No ") == 0 ||
        name.compare(0, strlen("no "), "no ") == 0 ){
OLDSKOOLDEBUG
            return;
    }

    // from the name look up the mass shift
OLDSKOOLDEBUG
    mod.deltaMass = getModMass(name);
    // add the new mod
OLDSKOOLDEBUG
    curPSM_->mods.push_back(mod);
OLDSKOOLDEBUG
} 
double ProteinPilotReader::getModMass(const string& name){
OLDSKOOLDEBUG
    map<string,double>::iterator found = modTable_.find(name);
OLDSKOOLDEBUG
    if( found == modTable_.end() ){
OLDSKOOLDEBUG
Verbosity::debug("PSM has an unrecognized mod, %s.",    name.c_str());
        throw BlibException(false, "PSM has an unrecognized mod, %s.", 
                            name.c_str());
    }
OLDSKOOLDEBUG
    return modTable_[name];//deltaMass;
}
/**
 * Handler for all characters between tags.  We are only interested in
 * the peaks data in the MSMSPEAKS elements and the values for
 * chemical elements and modifications.  Use the state to
 * determine if we are there.
 */
void ProteinPilotReader::characters(const XML_Char *s, int len){
OLDSKOOLDEBUG
    if( len == 0 || s == NULL ){
OLDSKOOLDEBUG
        return;
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
    if( state_ == PEAKS_STATE && curPSM_ != NULL 
        && !curPSM_->unmodSeq.empty() && len > 0 ){
        // copy len characters
OLDSKOOLDEBUG
        char* buf = new char[len + 1];
OLDSKOOLDEBUG
        strncpy(buf, s, len);
OLDSKOOLDEBUG
        buf[len] = '\0';
        // add it to the collected peaks
OLDSKOOLDEBUG
        peaksStr_ += buf;
OLDSKOOLDEBUG
        delete [] buf;
OLDSKOOLDEBUG
    } else if( state_ == MOD_STATE || state_ == ELEMENT_STATE ){
OLDSKOOLDEBUG
        for(int i=0; i < len; i++ ){
            nextWord_ += s[i];
        }
    }
}
void ProteinPilotReader::saveSpectrum()
{
OLDSKOOLDEBUG
    if( peaksStr_.empty() ){
OLDSKOOLDEBUG
        return;
    }
OLDSKOOLDEBUG
    if( curPSM_ == NULL ){
OLDSKOOLDEBUG
Verbosity::debug("Found MS/MS peaks but no spectrum information.");
        throw BlibException(false, 
                            "Found MS/MS peaks but no spectrum information.");
        // or more to the point, it somehow got deleted 
    }

    // translate peaksStr into a vector of peaks
OLDSKOOLDEBUG
    istringstream peakParser(peaksStr_);
OLDSKOOLDEBUG
    while( !peakParser.eof() ){
OLDSKOOLDEBUG
        PEAK_T peak;
        double charge;
OLDSKOOLDEBUG
        peakParser >> peak.mz >> charge >> peak.intensity;
OLDSKOOLDEBUG
        if( peak.mz == 0 && peak.intensity == 0 ) {
OLDSKOOLDEBUG
          break;
        }
        // peak location is actually M+H if charge > 0;  adjust
OLDSKOOLDEBUG
        if( charge > 0 ){
OLDSKOOLDEBUG
            peak.mz = (peak.mz + (charge - 1)*PROTON_MASS)/charge;
OLDSKOOLDEBUG
        }
OLDSKOOLDEBUG
        curPeaks_.push_back(peak);
OLDSKOOLDEBUG
    }
    // check that we got the correct number
OLDSKOOLDEBUG
    if( expectedNumPeaks_ != curPeaks_.size() ){
OLDSKOOLDEBUG
        Verbosity::comment(V_ALL, "peaksStr is %s", peaksStr_.c_str());
OLDSKOOLDEBUG
      Verbosity::debug("Spectrum %s should have %d peaks but d were read.", curPSM_->specName.c_str(),         expectedNumPeaks_, curPeaks_.size());
        throw BlibException(false, "Spectrum %s should have %d peaks but "
                            "%d were read.", curPSM_->specName.c_str(), 
                            expectedNumPeaks_, curPeaks_.size());
    }
    // sort them
OLDSKOOLDEBUG
    sort(curPeaks_.begin(), curPeaks_.end(), PeakProcessor::compPeakMz);

    // this is the end of the msmspeaks element
    // create a new spectrum and fill in the data
OLDSKOOLDEBUG
    SpecData* specD = new SpecData();
OLDSKOOLDEBUG
    specD->retentionTime = retentionTime_;
OLDSKOOLDEBUG
    specD->mz = curSpecMz_;
OLDSKOOLDEBUG
    specD->numPeaks = curPeaks_.size();
OLDSKOOLDEBUG
    specD->mzs = new double[specD->numPeaks];
OLDSKOOLDEBUG
    specD->intensities = new float [specD->numPeaks];
OLDSKOOLDEBUG
    for(int i=0; i < specD->numPeaks; i++){
OLDSKOOLDEBUG
        specD->mzs[i] = curPeaks_[i].mz;
OLDSKOOLDEBUG
        specD->intensities[i] = curPeaks_[i].intensity;
    }
    // save it in the hash, keyed by spec name
OLDSKOOLDEBUG
    spectrumMap_[curPSM_->specName] = specD;
OLDSKOOLDEBUG
}
void ProteinPilotReader::getElementName(){
    // only fill the element table once, even though 
    // there may be multiple copies in the file
OLDSKOOLDEBUG
    map<string, double>::iterator found = elementTable_.find(nextWord_);
OLDSKOOLDEBUG
    if( found == elementTable_.end() ){ //it's new, add it
OLDSKOOLDEBUG
        elementTable_[nextWord_] = -1; // init entry 
OLDSKOOLDEBUG
        curElement_ = nextWord_;
OLDSKOOLDEBUG
        //cerr << "Element name is " << nextWord_ << endl;
OLDSKOOLDEBUG
    } else { // skip it
OLDSKOOLDEBUG
        curElement_.clear();
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
}
// ASSUMPTIONS: Always uses the first mass listed for the element
// the example file I have only contains monoisotopic masses and always
// lists the most common one first.  
void ProteinPilotReader::getElementMass(){
OLDSKOOLDEBUG
    if( elementTable_[curElement_] == -1 ){
OLDSKOOLDEBUG
        elementTable_[curElement_] = atof(nextWord_.c_str());
        //cerr << "Element mass is " << elementTable_[curElement_] << endl;
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
}
void ProteinPilotReader::getModName(){
OLDSKOOLDEBUG
    curMod_.name = nextWord_;
    //cerr << "Mod name is " << curMod_.name << endl;
OLDSKOOLDEBUG
}
//TODO: some elements have multi letter codes (duh)
// don't store the list of elements, just add up the mass
void ProteinPilotReader::getModFormula( bool add ){
    //cerr << "formula to " << (add ? "add" : "subtract") << " is " << nextWord_ << endl;
OLDSKOOLDEBUG
    string element;
OLDSKOOLDEBUG
    int sign = (add ? 1 : -1);

OLDSKOOLDEBUG
    for(size_t i=0; i < nextWord_.length() ; i++){
OLDSKOOLDEBUG
        if( nextWord_[i] >= 'A' && nextWord_[i] <= 'Z' ){
            // add the last element we found
OLDSKOOLDEBUG
            addElement( curMod_.deltaMass, element, sign );
            // set the new element
OLDSKOOLDEBUG
            element = nextWord_[i];
OLDSKOOLDEBUG
        } else if( nextWord_[i] >= 'a' &&nextWord_[i] <= 'z' ){
OLDSKOOLDEBUG
            element += nextWord_[i];
OLDSKOOLDEBUG
        } else if( nextWord_[i] > '0' &&nextWord_[i] <= '9' ){
OLDSKOOLDEBUG
            int count = atoi( nextWord_.c_str() + i );
OLDSKOOLDEBUG
            addElement( curMod_.deltaMass, element, sign * count );
OLDSKOOLDEBUG
            element.clear();
OLDSKOOLDEBUG
        }
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
    // now add the last element (if the formula didn't end with a number
    addElement( curMod_.deltaMass, element, sign );
OLDSKOOLDEBUG
}
void ProteinPilotReader::addElement(double& mass, string element, int count){
OLDSKOOLDEBUG
    if( element.empty() ){
OLDSKOOLDEBUG
        return;
    }
    // look up the element mass
OLDSKOOLDEBUG
    map<string, double>::iterator found = elementTable_.find(element);
OLDSKOOLDEBUG
    if( found == elementTable_.end() ){
OLDSKOOLDEBUG
      Verbosity::debug("The formula for modification '%s' has an unrecognzied element, %s.", curMod_.name.c_str(), element.c_str());)
        throw BlibException(false, "The formula for modification '%s' has an "
                            "unrecognzied element, %s.", curMod_.name.c_str(),
                            element.c_str());
    }
OLDSKOOLDEBUG
    // add it
    double newMass = found->second;
OLDSKOOLDEBUG
    mass += (count * newMass);
OLDSKOOLDEBUG
}
void ProteinPilotReader::addMod(){
    // first check to see if we already have one for this mod
OLDSKOOLDEBUG
    map<string, double>::iterator found = modTable_.find(curMod_.name);
OLDSKOOLDEBUG
    if( found != modTable_.end() && //if it was found and has different mass
        found->second != modTable_[curMod_.name] ){
OLDSKOOLDEBUG
Verbosity::debug("Two entries for a modification named %s, one with delta mass %d and one with %d.",    found->second, modTable_[curMod_.name]);
            throw BlibException(false, "Two entries for a modification named %s,"
                            "one with delta mass %d and one with %d.",
                            found->second, modTable_[curMod_.name]);
    }

OLDSKOOLDEBUG
    // else add it
    modTable_[curMod_.name] = curMod_.deltaMass;
OLDSKOOLDEBUG
    curMod_.name.clear();
OLDSKOOLDEBUG
    curMod_.deltaMass = 0;
OLDSKOOLDEBUG
}
// SpecFileReader functions
// This is the only one really needed
bool ProteinPilotReader::getSpectrum(string scanName, 
                                     SpecData& returnData, 
                                     bool getPeaks){
OLDSKOOLDEBUG
    Verbosity::comment(V_DETAIL, "Looking for spectrum %s", scanName.c_str());

OLDSKOOLDEBUG
    map<string,SpecData*>::iterator found = spectrumMap_.find(scanName);
OLDSKOOLDEBUG
    if( found == spectrumMap_.end() ){
OLDSKOOLDEBUG
        return false;
    }

OLDSKOOLDEBUG
    if( ! getPeaks ){
OLDSKOOLDEBUG
        returnData.numPeaks = 0; // so that they don't get copied
OLDSKOOLDEBUG
    }
OLDSKOOLDEBUG
    returnData = *(found->second);
OLDSKOOLDEBUG
    return true;
}
// Also inherited from SpecFileReader, not needed
void ProteinPilotReader::openFile(const char* filename, bool mzSort){
OLDSKOOLDEBUG
    Verbosity::debug("ProteinPilotReader is reading spectra from %s", 
                     (getFileName()).c_str());
OLDSKOOLDEBUG
}
void ProteinPilotReader::setIdType(SPEC_ID_TYPE type){}
bool ProteinPilotReader::getSpectrum(int scanNumber, SpecData& spectrum, 
                                     SPEC_ID_TYPE findBy,
                                     bool getPeaks){
OLDSKOOLDEBUG
    Verbosity::warn("ProteinPilotReader cannot fetch spectra by scan "
                       "number, only by string identifier.");
OLDSKOOLDEBUG
    return false;
}
bool ProteinPilotReader::getNextSpectrum(SpecData& spectrum, bool getPeaks){
OLDSKOOLDEBUG
    Verbosity::warn("Sequential retrivial of spectra not implemented "
                       "for ProteinPilotReader.");
OLDSKOOLDEBUG
    return false;
}




} // namespace


/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
