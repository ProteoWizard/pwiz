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
 * The MascotResultsReader collects a list of psms that should be
 * included in the library.  It passes the file object it was using to
 * the MascotSpecReader so the file only has to be opened and parsed once.
 */

#include "MascotResultsReader.h"
#include "BlibUtils.h"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"

namespace BiblioSpec {

MascotResultsReader::MascotResultsReader(BlibBuilder& maker, 
                    const char* datFileName, 
                    const ProgressIndicator* parent_progress)
: BuildParser(maker, datFileName, parent_progress)
{
    Verbosity::comment(V_DETAIL, "Creating a MascotResultsReader.");
    ms_params_ = NULL;
    readSpecProgress_ = NULL;
}

MascotResultsReader::~MascotResultsReader()
{
    delete ms_params_;
    // the spec reader will delete the results and file

    // Delete all the mod tables
    map<string, ModTable* >::iterator it = methodModsMaps_.begin();
    while (it != methodModsMaps_.end())
    {
        delete it->second;
        it++;
    }
    delete readSpecProgress_;
}

void MascotResultsReader::initParse() {
    // Create the ms_file
    unsigned int cacheFlag = getCacheFlag(getFileName(), blibMaker_.getCacheThreshold()); 
    const char* cachePath = getPsmFilePath();
    if( *cachePath == '\0' ){
        cachePath = ".";
    }

    string realDatFilePath(getFileName());
    if (pwiz::util::findUnicodeBytes(getFileName()) != getFileName().end())
    {
        Verbosity::warn("Mascot Parser does not support Unicode in filepaths ('%s'): copying file to a temporary non-Unicode path", getFileName().c_str());
        string sanitizedFilename = bfs::path(getFileName()).filename().string();
        string::const_iterator unicodeByteItr;
        while ((unicodeByteItr = pwiz::util::findUnicodeBytes(sanitizedFilename)) != sanitizedFilename.end())
        {
            sanitizedFilename[unicodeByteItr - sanitizedFilename.cbegin()] = '_';
        }

        realDatFilePath = (bfs::temp_directory_path() / sanitizedFilename).string();
        if (!bfs::exists(realDatFilePath))
        {
            bfs::copy_file(getFileName(), realDatFilePath);
            tmpDatFile_.reset(new TempFileDeleter(realDatFilePath));
        }
    }

    ms_file_ = new ms_mascotresfile(realDatFilePath.c_str(), 0, "",
                                    cacheFlag,
                                    cachePath);

    if (!ms_file_->isValid()) {
        int error = ms_file_->getLastError();
        if( error !=  ms_mascotresfile::ERR_NO_ERROR ) {
            string msg = getErrorMessage(error);
            throw BlibException(true, "Error with '%s'. %s", 
                                realDatFilePath.c_str(), msg.c_str());
        }
    }

    // set score threshold
    scoreThreshold_ = getScoreThreshold(MASCOT);

    // create the results objects
    unsigned int flags = ms_mascotresults::MSRES_DUPE_REMOVE_NONE;
    unsigned int flags2 = ms_peptidesummary::MSPEPSUM_USE_HOMOLOGY_THRESH |
                          ms_peptidesummary::MSPEPSUM_NO_PROTEIN_GROUPING;
    if (ms_file_->isErrorTolerant()) {
        flags |= ms_peptidesummary::MSRES_INTEGRATED_ERR_TOL;
        flags2 &= ~ms_peptidesummary::MSPEPSUM_NO_PROTEIN_GROUPING; // this flag does not work with error tolerant searches
    }
    ms_results_ = new ms_peptidesummary(*ms_file_, 
                                        flags,
                                        0, // get all results and filter later
                                        0, // get all ranks
                                        0, // no unigene index file
                                        0, // ignoreIonsScoreBelow
                                        0, // min peptide length
                                        0, // singleHit
                                        flags2);

    // register the name with BuildParser, but don't try to open it
    this->setSpecFileName(realDatFilePath.c_str(), false);

    ms_params_ = new ms_searchparams(*ms_file_);

    // get distiller rawfiles
    getDistillerRawFiles(ms_params_, rawFiles_);

    // create the spec reader, sharing the file and results objects
    delete specReader_;  
    specReader_ = new MascotSpecReader(realDatFilePath.c_str(), ms_file_, ms_results_, rawFiles_, tmpDatFile_);

    // get modifications information
    for(int i=1; 0 != ms_params_->getFixedModsDelta(i); i++){
        double deltaMass = ms_params_->getFixedModsDelta(i);
        string residues = ms_params_->getFixedModsResidues(i);

        // check for "N_term" or "C_term"
        string residuesLower(residues);
        transform(residuesLower.begin(), residuesLower.end(), residuesLower.begin(), ::tolower);
        size_t findIdx;
        findIdx = residuesLower.find("n_term");
        if (findIdx != string::npos)
        {
            residues.erase(findIdx, 6);
            addStaticModToTable(N_TERM_POS, deltaMass);
            Verbosity::comment(V_DEBUG, "Static mod of delta mass %f on N-term.", deltaMass);
        }
        findIdx = residuesLower.find("c_term");
        if (findIdx != string::npos)
        {
            residues.erase(findIdx, 6);
            addStaticModToTable(C_TERM_POS, deltaMass);
            Verbosity::comment(V_DEBUG, "Static mod of delta mass %f on C-term.", deltaMass);
        }

        // add each residue with a static mod to the collection
        for(size_t c=0; c < residues.length(); c++){
            if (residues[c] >= 'A' && residues[c] <= 'Z')
            {
                addStaticModToTable(residues[c], deltaMass);
                Verbosity::comment(V_DEBUG, "Static mod of delta mass %f for residue %c.",
                                   deltaMass, residues[c]);
            }
        }

    }

    // check for isotope labeling
    getIsotopeMasses();

    // initialize a list of possible file extensions for the spectrum inputs
    // they appear in spectrum title as [X:path\to\file.ext]
    specFileExtensions_.push_back(".raw]");
    specFileExtensions_.push_back(".RAW]");
    specFileExtensions_.push_back(".d]");
    specFileExtensions_.push_back(".wiff]");
	specFileExtensions_.push_back(".wiff2]");
	specFileExtensions_.push_back(".lcd]");
	specFileExtensions_.push_back(".mzXML]");
	specFileExtensions_.push_back(".mzxml]");
	specFileExtensions_.push_back(".mzML]");
	specFileExtensions_.push_back(".mzml]");

    // separately count reading .dat file and adding spec to the library
    initReadAddProgress();
}

bool MascotResultsReader::parseFile(){
    initParse();

    // track the progress of reading the file
    int nQueries = ms_file_->getNumQueries();
    readSpecProgress_ = readAddProgress_->newNestedIndicator(nQueries);

    // look at every spectrum
    for(int specId = 1; specId <= nQueries; specId++) {
        Verbosity::comment(V_DETAIL, "Parsing spec %d.", specId);

        // note: the peptides should not be deleted, as this will corrupt the result
        ms_peptide* pep;
        int rank = 1;

        bool hasPeptide = ms_results_->getPeptide(specId, rank, pep);
        if(!pep->getAnyMatch()) {// skip this spec if no matches
            continue;
        }

        // find out if it passes the score threshold
        double ionScore = pep->getIonsScore();
        double expectationValue = 
            ms_results_->getPeptideExpectationValue(ionScore, specId);

        if( expectationValue > scoreThreshold_ ) {
            continue;
        }

        // look for the filename in the title
        ms_inputquery spec(*ms_file_, specId);
        string file = getFilename(spec);
        // if it doesn't already exist, add it to the filemap
        map<string, vector<PSM*> >::iterator mapAccess
            = fileMap_.find(file);
        if( mapAccess == fileMap_.end() ){
            vector<PSM*> tmpPsms;
            fileMap_[file] = tmpPsms;
            mapAccess = fileMap_.find(file);
        }

        // add all PSMs that are rank 1, remove duplicates later
        while( hasPeptide && ionScore == pep->getIonsScore() ){ 

            int charge = pep->getCharge();
            if (0 >= charge || charge > 50) {
                Verbosity::warn("Unexpected charge state %d.  Skipping match with %s", charge, pep->getPeptideStr().c_str());
            } else {
                PSM* cur_psm = new PSM();
                cur_psm->charge = charge;
                cur_psm->unmodSeq = pep->getPeptideStr();
                cur_psm->specKey = specId;
                cur_psm->score = expectationValue;

                // get any labeling
                string quant = pep->getComponentStr();
                applyIsotopeDiffs(cur_psm, quant);

                // get any modifications
                string readVarMods =  ms_results_->getReadableVarMods(specId, rank);
                parseMods(cur_psm, pep->getVarModsStr(), readVarMods);
                mapAccess->second.push_back(cur_psm);

                Verbosity::comment(V_ALL, "Adding spec %d, charge %d, score %f, "
                                   "seq %s from file '%s'.", cur_psm->specKey, 
                                   cur_psm->charge, cur_psm->score, 
                                   cur_psm->unmodSeq.c_str(), file.c_str());
            }

            hasPeptide = ms_results_->getPeptide(specId, ++rank, pep);
        
        }// next psm with same score and I/L equivalent peptide

        readSpecProgress_->increment();
    }// next spectrum
 
    readAddProgress_->increment(); // finished reading

    // get max RefSpectra id before building tables
    sqlite3_stmt* checkMax;
    int maxRefSpectraId = -1;
    if (sqlite3_prepare(blibMaker_.getDb(), "SELECT max(id) FROM RefSpectra", -1, &checkMax, NULL) == SQLITE_OK) {
        if (sqlite3_step(checkMax) == SQLITE_ROW) {
            maxRefSpectraId = sqlite3_column_int(checkMax, 0);
        }
        sqlite3_finalize(checkMax);
    }

    // put all the collected spectra in the library
    map<string, vector<PSM*> >::iterator fileIterator = fileMap_.begin();
    for(; fileIterator != fileMap_.end(); ++fileIterator){
        psms_ = fileIterator->second;
        // it's possible that there were no filenames for the spectra
        // in which case we'll store the .dat file
        if( fileIterator->first.empty() ){
            buildTables(MASCOT_IONS_SCORE);
        } else {
            setSpecFileName(fileIterator->first.c_str(), false);
            buildTables(MASCOT_IONS_SCORE, fileIterator->first);
        }
    }

    MascotSpecReader* mascotSpecReader;
    if (((mascotSpecReader = dynamic_cast<MascotSpecReader*>(specReader_)) != NULL) && mascotSpecReader->needsRtConversion()) {
        Verbosity::status("Converting retention times to minutes for RefSpectra with id > %d.", maxRefSpectraId);
        char stmtBuf[128];
        sprintf(stmtBuf, "UPDATE RefSpectra SET retentionTime = retentionTime / 60.0 WHERE id > %d", maxRefSpectraId);
        blibMaker_.sql_stmt(stmtBuf);
    }

    return true;
}

vector<PSM_SCORE_TYPE> MascotResultsReader::getScoreTypes() {
    return vector<PSM_SCORE_TYPE>(1, MASCOT_IONS_SCORE);
}

/**
 * Translate the modstr into a set of SeqMod's and add them to the
 * psm-mods vector.  Also add any static mods which can be looked up
 * by residue from psm->unmodSeq.
 *
 * The modstr is a string of ints, one for each residue in the
 * peptide plus one n and c terminus.  A zero (0) indicates no
 * modification.  A non-zero can be used to look up the mod delta mass
 * from the ms_params_.  The readableModStr is a semi-colon-separated
 * list of text descriptions of the modifications to the sequence.
 * For mods generated from an error tolerant search, the text is the
 * only record of the mod mass shift.
 * Position is 1-based. 
 * 
 */
void MascotResultsReader::parseMods(PSM* psm, string modstr, 
                                    string readableModStr){
    int first_mod_pos = 1;
    int last_mod_pos = modstr.length() - 2;

    // first parse the terminal character
    if( modstr.at(0) == 'X' ){
        addErrorTolerantMod(psm, readableModStr, 0);
    } else {
        addVarMod(psm, modstr.at(0), 0);
    }

    // for characters first to last in modstr, 
    // use the int value of the char to look up delta mass
    for(int i = first_mod_pos; i <= last_mod_pos; i++){
        if( modstr.at(i) == 'X' ){
            addErrorTolerantMod(psm, readableModStr, i);
        } else {
            addVarMod(psm, modstr.at(i), i);
        }
    }

    // now get terminal character at the other end
    if( modstr.at(last_mod_pos+1) == 'X' ){
        addErrorTolerantMod(psm, readableModStr, psm->unmodSeq.length()+1);
    } else {
        addVarMod(psm, modstr.at(last_mod_pos+1), psm->unmodSeq.length()+1);
    }

    // for static mods look up each residue in the staticMods collection
    for (size_t i = 0; i < psm->unmodSeq.length(); i++) {
        addStaticMods(psm, psm->unmodSeq[i], i + 1);
    }
    addStaticMods(psm, N_TERM_POS, 0);
    addStaticMods(psm, C_TERM_POS, psm->unmodSeq.length()+1);

    // now consolidate terminal mods with residue-terminal mods (i.e. position 0 to 1, position N+1 to N)
    for (auto& mod : psm->mods)
        if (mod.position == 0)
            mod.position = 1;
        else if (mod.position == psm->unmodSeq.length() + 1)
            mod.position = psm->unmodSeq.length();
}

/**
 * Add a modification to the psm.  The varLookUpChar is a character
 * 0-9 or A-W that can be used to look up the mass of a particular
 * mod.  aaPosition is the location of this mod in the peptide.
 */
void MascotResultsReader::addVarMod(PSM* psm, 
                                    char varLookUpChar, 
                                    int aaPosition){
    
  int idx = getVarModIndex(varLookUpChar);
  if( idx != 0 ) {
    SeqMod mod;
    mod.deltaMass = ms_params_->getVarModsDelta(idx);
    mod.position = aaPosition;
    psm->mods.push_back(mod);
  }
}

/**
 * Adds all static modifications to the psm. The staticLookUpChar is a character
 * A-Z, N_TERM_POS, or C_TERM_POS that will be used to look up all masses of mods
 * for that particular residue. aaPosition is the location of this mod in the peptide.
 */
void MascotResultsReader::addStaticMods(PSM* psm, char staticLookUpChar, int aaPosition){
    for (vector<SeqMod>::const_iterator i = psm->mods.begin(); i != psm->mods.end(); i++) {
        if (i->position == aaPosition) {
            return; // don't add any static mods if there is already a mod at this position
        }
    }

    MultiModTable::iterator found = staticMods_.find(staticLookUpChar);
    if (found != staticMods_.end())
    {
        for (vector<double>::iterator massIter = found->second.begin();
             massIter != found->second.end();
             ++massIter)
        {
            SeqMod mod(aaPosition, *massIter);
            psm->mods.push_back(mod);
        }
    }
}

/**
 * Adds a new mod to the staticMods_ table.
 */
void MascotResultsReader::addStaticModToTable(char aa, double deltaMass){
    MultiModTable::iterator lookup = staticMods_.find(aa);
    if (lookup == staticMods_.end())
    {
        staticMods_[aa] = vector<double>(1, deltaMass);
    }
    else
    {
        lookup->second.push_back(deltaMass);
    }
}

/**
 * Add a modification to the psm.  Use the readableModStr
 * representation of the modifications to find the mass difference.
 */
void MascotResultsReader::addErrorTolerantMod(PSM* psm, 
                                              string readableModStr, 
                                              int aaPosition){

    // mods in readableModStr are separated by ;
    // if there is an error tolerant mod, it will be last and can be
    // identified by the presence of square braces enclosing a mass
    size_t lastSemiColon = readableModStr.find_last_of(';');
    if( lastSemiColon == string::npos ){
        lastSemiColon = 0;
    }

    // first check to see if this mod is a base change in which case
    // there is no post-translational modification
    if( readableModStr.find("NA_INSERTION", lastSemiColon) != string::npos ||
        readableModStr.find("NA_DELETION", lastSemiColon) != string::npos || 
        readableModStr.find("NA_SUBSTITUTION", lastSemiColon) != string::npos ){
        Verbosity::comment(V_DETAIL, "No change for an indel/substitution mod "
                           "in '%s'", readableModStr.c_str());
        return;
    }

    // find the open brace
    size_t brace = readableModStr.find('[', lastSemiColon);
    if( brace == string::npos ){
        throw BlibException(true, 
                            "Error tolerant modification is missing a mass "
                            "shift. Not found in '%s' from file '%s'.", 
                            readableModStr.c_str(), getFileName().c_str());
    }
    // parse the mass shift
    double mass = atof(readableModStr.c_str() + brace + 1);

    Verbosity::comment(V_DETAIL, "Adding ET mod with mass shift %f from "
                       "'%s'", mass, readableModStr.c_str());
    // create new mod and add to the psm
    SeqMod mod;
    mod.deltaMass = mass;
    mod.position = aaPosition;
    psm->mods.push_back(mod);
}


/**
 * The characters of a var mod string may include 0-9 and A-W where A
 * = 10 and W = 32.  Convert the character into its integer value
 */
int MascotResultsReader::getVarModIndex(const char c){
    char cBuf[2];
    cBuf[0] = c;
    cBuf[1] = '\0';
    int intVal = atoi(cBuf);

    if( intVal == 0 && cBuf[0] != '0' ) { 
        intVal = static_cast<int>(c) - static_cast<int>('A') + 10;
    }

    if( intVal > 32 || intVal < 0 ) {
        throw BlibException(true, 
                            "'%c' is not a legal modification character in %s.",
                            c, getFileName().c_str());
    }
    return intVal;
}

/**
 * \brief Find out if there was any isotope labeling for this run and
 * if so generate a table of mass differences for each residue.  These
 * differences will be reported as diff mods.  Create one table for
 * each method component name (e.g. heavy and light for 15N) and store
 * with the name as the key in methodModsMaps_.
 */
void MascotResultsReader::getIsotopeMasses(){

    // find out if there is labeling in this run
    string quantName = ms_params_->getQUANTITATION();
    Verbosity::debug("Quantitation method is %s.", 
                     (quantName.empty()) ? "not specified" : quantName.c_str());

    if( quantName.empty() || quantName == "None") {
        return;
    }

    string exeDir = getExeDirectory();

    // URL encode any spaces in the EXE path
    size_t pos = 0;
    while((pos = exeDir.find(" ", pos)) != string::npos) {
        exeDir.replace(pos, 1, "%20");
        pos += 3;
    }

    // get the quantification parameters
    ms_quant_configfile quantConfig;
    string quantSchemas("http://www.matrixscience.com/xmlns/schema/quantitation_1 ");
    quantSchemas += exeDir + "quantitation_1.xsd";
    quantSchemas += " http://www.matrixscience.com/xmlns/schema/quantitation_2 ";
    quantSchemas += exeDir + "quantitation_2.xsd";
    quantConfig.setSchemaFileName(quantSchemas.c_str());
    if( !ms_file_->getQuantitation(&quantConfig) ) {
        throw BlibException(true, "Cannot get quantitation information "
                            "from file '%s'.", getFileName().c_str());
    }

    // e.g. method is 15N, components are light/heavy
    const ms_quant_method* method = 
        quantConfig.getMethodByName(quantName.c_str());
    if( method == NULL ) {
        throw BlibException(true, "Cannot get quantitation method %s "
                            "in file '%s'.", quantName.c_str(), 
                            getFileName().c_str());
    }

    // get the parameters that include isotope masses
    string unimod(exeDir);
    unimod += "unimod_2.xsd"; 
    ms_umod_configfile massConfig;
    massConfig.setSchemaFileName(unimod.c_str()); 
    bool success = ms_file_->getUnimod(&massConfig);
    if( !success || !massConfig.isValid() ){
        throw BlibException(true, "Cannot get unimod masses in file '%s'.  "
                            "Possible error looking for '%s'.",
                            getFileName().c_str(), unimod.c_str());
    }
    
    // get a set of default masses to compare isotopes to
    ms_masses defaultMasses;

    // for each component in the method , create a table of mass diffs
    for(int comp_idx=0; comp_idx < method->getNumberOfComponents(); comp_idx++){
        string name = method->getComponentByNumber(comp_idx)->getName();
        ModTable* mods = new ModTable();
        methodModsMaps_[name] = mods;

        Verbosity::debug("Creating mods table for component %s.", name.c_str());

        ms_masses heavyMasses;
        heavyMasses.applyIsotopes(&massConfig, 
                                  method->getComponentByNumber(comp_idx));
        // for each residue
        for(char aa = 'A'; aa < 'Z'; aa++){
            double heavy = heavyMasses.getResidueMass(MASS_TYPE_MONO, aa);
            double light = defaultMasses.getResidueMass(MASS_TYPE_MONO, aa);
            if( (heavy - light) > 0.000005 ) { // don't add 0 mass diffs
                mods->insert(pair<char,double>(aa, (heavy - light)));
            }
        }

    }

}

/**
 * \brief Add a diff mod to each residue if there is isotope labeling
 * for this peptide.  Assumes that methodModMaps_ has already been
 * initialized.
 */
void MascotResultsReader::applyIsotopeDiffs(PSM* psm, string quantName){

    if( quantName.empty()) {
        return;
    }

    // find table of mods for this name
    ModTable* mods = methodModsMaps_.find(quantName)->second;
    if( mods == NULL ) {// do I need to test iterator != map.end() instead
        throw BlibException(true, "Labeling method %s was not found in file "
                            "'%s'.", quantName.c_str(), getFileName().c_str());
    }

    // find mod for each residue
    for(size_t i=0; i < psm->unmodSeq.length(); i++){
        ModTable::iterator found = 
            mods->find(psm->unmodSeq.at(i));

        if( found != mods->end() ) {
            double deltaMass = found->second;
            SeqMod mod;
            mod.position = i + 1;
            mod.deltaMass = deltaMass;
            psm->mods.push_back(mod);
        }
    }
}

/**
 * Parses the distiller raw files from the ms_searchparams and adds them to
 * the given vector.
 */
void MascotResultsReader::getDistillerRawFiles(const ms_searchparams* searchparams, vector<string>& v)
{
    const string rawFileHeader = "_DISTILLER_RAWFILE";
    string paramsString = searchparams->getAllUSERParams();
    vector<string> params;
    boost::split(params, paramsString, boost::is_any_of("\r\n"));

    // iterate over parameters
    for (vector<string>::iterator iter = params.begin();
         iter != params.end();
         ++iter)
    {
        // check if the parameter is a distiller rawfile
        if (iter->substr(0, rawFileHeader.length()) == rawFileHeader)
        {
            // find the start of the filename
            size_t idx = iter->find('=') + 1;
            string sampleStr(iter->substr(idx, 3));
            idx = iter->find('}', idx);
            if (idx != string::npos)
            {
                // add the filename to the vector
                string rawFile(iter->substr(idx + 1));
                Verbosity::debug("Found distiller raw file [%s]", rawFile.c_str());

                if (sampleStr != "{1}")
                {
                    throw BlibException(false, "Distiller raw file '%s' had a sample number other than 1.",
                                               rawFile.c_str());
                }

                v.push_back(rawFile);
            }
        }
    }
}

/**
 * examine a string to see if it looks like a reasonable raw file name
 */
bool MascotResultsReader::IsPlausibleRawFileName(const string &name) const
{
    string test = name + "]"; // so it looks like an entry in the specFileExtensions_ table
    for (int extIdx = 0; extIdx < specFileExtensions_.size();)
    {
        if (boost::algorithm::ends_with(test, specFileExtensions_[extIdx++]))
            return true;
    }
    return false;
}

/**
 * examine a string to see if it looks like a reasonable mgf file name
*/
bool MascotResultsReader::IsPlausibleMGFFileName(const string &name) const
{
    return boost::algorithm::ends_with(name, ".mgf") || boost::algorithm::ends_with(name, ".MGF");
}

/**
 * Look in the title string of the spectrum for the name of the file it
 * originally came from.  Return an empty string if no file found.
 */
string MascotResultsReader::getFilename(ms_inputquery& spec){

    // one distiller raw file, just return it
    if (rawFiles_.size() == 1)
    {
        return rawFiles_[0];
    }

    string idStr = spec.getStringTitle(true);
    string filename = getFilenameFromID(idStr);

    if (!filename.empty())
    {
        return filename;
    }

    // check for "from file" format
    const string fromFileStr = "from file";

    size_t idx = idStr.find(fromFileStr);
    size_t idxEnd;

    if (idx != string::npos)
    {
        idx += fromFileStr.length();
        // check if from multiple files (fail)
        // e.g. "from files [0,1,2]"
        if (idStr.length() > idx && idStr[idx] == 's')
        {
            throw BlibException(false, "Spectra from multiple files. Use scan group aggregation method "
                                       "'None' in Mascot Distiller MS/MS processing options. Title string "
                                       "was: '%s'", idStr.c_str());
        }
        // we now expect string like "from file [0]"
        // find file index
        idx = idStr.find('[', idx);
        if (idx != string::npos)
        {
            ++idx;
            idxEnd = idStr.find(']', idx);
            if (idxEnd != string::npos)
            {
                // get string between brackets
                string fileIdxStr(idStr, idx, idxEnd - idx);
                int fileIdx;
                try
                {
                    fileIdx = boost::lexical_cast<int>(fileIdxStr);
                }
                catch (boost::bad_lexical_cast e)
                {
                    // rethrow
                    throw BlibException(false, "Error reading file index from title '%s': %s",
                                               idStr.c_str(), e.what());
                }
                // get filename
                if (fileIdx < 0 || fileIdx >= (int)rawFiles_.size())
                {
                    throw BlibException(false, "File index %d out of range in title '%s'",
                                               fileIdx, idStr.c_str());
                }
                return rawFiles_[fileIdx];
            }
        }
    }
    else
    {
        // check for filename in brackets
        idx = idStr.find('[');
        if (idx != string::npos)
        {
            // look for a known file extension
            ++idx;
            idxEnd = string::npos;
            size_t extIdx = 0;
            while (idxEnd == string::npos &&
                   extIdx < specFileExtensions_.size())
            {
                idxEnd = idStr.find(specFileExtensions_[extIdx++], idx);
            }
            if (idxEnd != string::npos)
            {
                idxEnd += (specFileExtensions_[extIdx - 1].length() - 1);
                return idStr.substr(idx, idxEnd - idx);
            }
        }
    }

    // fail on case where we have multiple files and couldn't get file information from title
    if (rawFiles_.size() > 1)
    {
        throw BlibException(false, "When creating multi-file projects in Mascot Distiller, "
                                   "uncheck the 'Memory efficient (Not compatible with "
                                   "label-free)' checkbox. Title string was: '%s'", idStr.c_str());
    }

    if (filename.empty())
    {
        // often the filename is in the comment "COM=" or uploaded file "FILE=" or uploaded file url "URL="
        string globalFilename;
        if (IsPlausibleRawFileName(globalFilename = ms_params_->getFILENAME()) ||
            IsPlausibleRawFileName(globalFilename = ms_params_->getDATAURL()) ||
            IsPlausibleRawFileName(globalFilename = ms_params_->getCOM()) ||
            // MGF filename is a reasonable clue for Skyline to find raw file name
            IsPlausibleMGFFileName(globalFilename = ms_params_->getFILENAME()) ||
            IsPlausibleMGFFileName(globalFilename = ms_params_->getDATAURL()) ||
            IsPlausibleMGFFileName(globalFilename = ms_params_->getCOM()))
        {
            filename = globalFilename;
        }
    }

    return filename;
}

/**
 * Call getLastError and return a string with information about which
 * error was returned.
 */
string MascotResultsReader::getErrorMessage(){
    int errorCode = ms_file_->getLastError();
    return getErrorMessage(errorCode);
 }

/**
 * \return A string with a description of the given error code.
 */
string MascotResultsReader::getErrorMessage(int errorCode){
    string message;
    switch(errorCode){
    case ms_mascotresfile::ERR_NO_ERROR:
        message = "No error reported.";
        break;
    case ms_mascotresfile::ERR_NOMEM:
        message = "Not enough memory.";
        break;
    case ms_mascotresfile::ERR_NOSUCHFILE:
        message = "No such file.";
        break;
    case ms_mascotresfile::ERR_MISSINGSECTION:
        message = "Missing section.";
        break;
    case ms_mascotresfile::ERR_MISSINGSECTIONEND:
        message = "Missing section end.";
        break;
    case ms_mascotresfile::ERR_READINGFILE:
        message = "Error reading file.";
        break;
    case ms_mascotresfile::ERR_INVALID_CACHE_DIR:
        message = "Invalid cache directory '";
        message += getPsmFilePath();
        message += "'";
        break;
    case ms_mascotresfile::ERR_FAIL_OPEN_DAT_FILE:
        message = "Error opening .dat file.";
        break;
    case ms_mascotresfile::ERR_FAIL_MK_CACHE_DIR:
        message = "Failed to create cache directory.";
        break;
    case ms_mascotresfile::ERR_FAIL_CDB_INIT:
        message = "Error initiailzing cache CDB file.";
        break;
    case ms_mascotresfile::ERR_MISSING_CDB_FILE:
        message = "Missing cache CDB file.";
        break;
    case ms_mascotresfile::ERR_INVALID_NUMQUERIES:
        message = "Invalid number of queries.";
        break;
    case ms_mascotresfile::ERR_INVALID_RESFILE:
        message = "Invalid results (.dat) file.";
        break;
    case ms_mascotresfile::ERR_FAIL_MK_CDB_FILE:
        message = "Failed to create the cache CDB file.";
        break;
    case ms_mascotresfile::ERR_FAIL_CLOSE_FILE:
        message = "Failed to close file.";
        break;
    default:
        message = "Unhandled error code ";
        message += errorCode;
    }
    return message;
}

/**
 * Returns the correct flag to use for initializing the msresfile object.
 * If the input file is larger than a cutoff, use caching.  Otherwise do not.
 */
unsigned int MascotResultsReader::getCacheFlag(const string& filename, 
                                               int threshold){
  unsigned int flag = ms_mascotresfile::RESFILE_NOFLAG;

  // determine the size of the .dat file
  auto size = bfs::file_size(filename);
  Verbosity::debug("File size in bytes is %d and threshold is %d.",
      size, threshold);

  if (size > threshold  ){
    flag = ms_mascotresfile::RESFILE_USE_CACHE;
  }

  return flag;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
