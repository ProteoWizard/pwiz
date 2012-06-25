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

#include "BuildParser.h"
#include <boost/algorithm/string.hpp>

namespace BiblioSpec {

BuildParser::BuildParser(BlibBuilder& maker,
                         const char* filename,
                         const ProgressIndicator* parentProgress_)
: fullFilename_(filename),
  blibMaker_(maker),
  fileProgressIncrement_(0),
  lookUpBy_(SCAN_NUM_ID)
{
    // parse full file name to get path and fileroot
    filepath_ = getPath(fullFilename_);
    fileroot_ = getFileRoot(fullFilename_);

    this->parentProgress_ = parentProgress_;
    this->readAddProgress_ = NULL;
    this->fileProgress_ = NULL;
    this->specProgress_ = NULL;
    this->curPSM_ = NULL;
    this->specReader_ = new PwizReader();
}

BuildParser::~BuildParser() {
    delete readAddProgress_;
    delete fileProgress_;
    delete specProgress_;
    delete specReader_;
}


/**
 * \brief Register the name of the next spectrum file to be read.
 *
 * Searches for a file of the form filepath fileroot extension (with
 * no characters inbetween).  The search starts in the directory of
 * the file being parsed (member variable filepath) and then proceeds
 * to the other directories in the given order. Expects extensions to
 * be given with the dot.  Searches extensions in order given.  First
 * file found that can be opened is set as the one to open next.  If
 * no such file is found, throws error.
 * Must be called before buildTables().
 */
void BuildParser::setSpecFileName(
  const char* specfileroot,///< basename of file
  const vector<const char*>& extensions, ///< extensions to be searched in order
  const vector<const char*>& directories)///< directories to be searched in order
{

    curSpecFileName_.clear();

    string fileroot = specfileroot;
    do {
        // try the location of the result file, then all dirs in the list
        for(int i=-1; i<(int)directories.size(); i++) {

            string path = filepath_.c_str();
            if( i >= 0 ) {
                path += directories.at(i);
            }

            for(int i=0; i<(int)extensions.size(); i++) {
                string trialName = path + fileroot + extensions.at(i);
                ifstream file(trialName.c_str());
                if(file.good()) {
                    curSpecFileName_ = trialName;
                    break;
                }

            }// next extension

            if( !curSpecFileName_.empty() )
                break;

        }// next dir

        // if nothing found, try removing possible file extensions in search of true
        // base file name.  Proteome Discoverer leaves .msf on its pepXML base names.
        if ( curSpecFileName_.empty() ) {
            size_t slash = fileroot.find_last_of("/\\");
            size_t dot = fileroot.find_last_of(".");
            if (dot == string::npos || (slash != string::npos && dot < slash))
                fileroot = "";  // no extenstion to remove
            else
                fileroot = fileroot.erase(dot);
        }

    } while (curSpecFileName_.empty() && !fileroot.empty());

    if( curSpecFileName_.empty() ) {
        string extString = fileNotFoundMessage(specfileroot,
                                               extensions, directories);
        throw BlibException(true, extString.c_str());
    }// else we found a file and set the name

}

/**
 * \brief Register the name of the next spectrum file to be read.
 *
 * Assumes filename is a fully-qualified path.  If file is not found
 * throws error.
 * Must be called before buildTables().
 */
void BuildParser::setSpecFileName
    (const char* specfile,  ///< name of spectrum file
     bool checkFile)         ///< see if the file exists
{
    curSpecFileName_.clear();
    if( checkFile ){
        ifstream file(specfile);
        if(!file.good()) {
            throw BlibException(true, "Could not open spectrum file '%s'.", 
                                specfile);
        }
    }
    curSpecFileName_ = specfile;
}

/**
 * \brief Generate a string indicating that no file with the given
 * base name and any of the extensions could be found in any of the
 * directories.
 */
string BuildParser::fileNotFoundMessage(
     const char* specfileroot,///< basename of file
     const vector<const char*>& extensions, ///< extensions searched
     const vector<const char*>& directories ///< directories searched
                                        )
{
    string extString;

    if( extensions.size() == 1 ) {
        extString = extensions.at(0);
    } else if( extensions.size() > 1 ){
        extString = "[";
        for(int i=0; i<(int)extensions.size(); i++) {
            extString += extensions.at(i);
            extString += "|";
        }
        extString.replace(extString.length()-1 , 1, "]");
    }

    string messageString = "Could not find spectrum file ";
    messageString += specfileroot + extString + " in " + filepath_;
    if( filepath_.empty() ) {
        messageString += "current directory";
    }

    messageString += ", ";

    for(unsigned int i=0; i < directories.size(); i++) {
        messageString += directories.at(i);
        messageString += ",";
    }
    messageString.replace(messageString.length()-1, 1, ".");

    return messageString;
}

/**
 * \returns A const pointer to the full path containing the file being
 * parsed.
 */
const char* BuildParser::getPsmFilePath(){ // path containing file being parsed
    return filepath_.c_str();
}


/**
 * \brief For sorting by position ascending.
 */
bool lessThanPosition(const SeqMod& mod1, const SeqMod& mod2)
{
    return mod1.position < mod2.position;
}

/**
 * Inserts the full path of the given filename into the
 * SpectrumSourceFiles table.
 * \returns The ID for this file in the table.
 */
sqlite3_int64 BuildParser::insertSpectrumFilename(string& filename, 
                                                  bool insertAsIs){
    // get full path of filename
    string fullPath;
    if(insertAsIs){
        fullPath = filename;
    } else {
        fullPath = getAbsoluteFilePath(filename);
    }

    // first see if the file already exists
    string statement = "SELECT id FROM SpectrumSourceFiles WHERE filename = '";
    statement += fullPath;
    statement += "'";

    int iRow, iCol;
    char** result;
    int returnCode = sqlite3_get_table(blibMaker_.getDb(), 
                                       statement.c_str(),
                                       &result, &iRow, &iCol, 0);
    blibMaker_.check_rc(returnCode, statement.c_str());
    if( iRow > 0 ){ // file already exists
        sqlite3_int64 fileID = atol(result[1]);
        sqlite3_free_table(result);
        return fileID;
    }
    sqlite3_free_table(result);

    string sql_statement = "INSERT INTO SpectrumSourceFiles(fileName) VALUES('";
    sql_statement += fullPath;
    sql_statement += "')";

    blibMaker_.sql_stmt(sql_statement.c_str());

    // get the file ID to save with each spectrum
    sqlite3_int64 fileId = sqlite3_last_insert_rowid(blibMaker_.getDb());
    return fileId;
}

/**
 * \brief Use the BlibBuilder to add to the library entries in the list
 * of psms, adding spectra from the curSpecFileName file. The same
 * score type for all spectra is used.  If the spectra originally came from
 * a different file, it may be given, in which case that name will be stored
 * in the database.
 *
 * Requires that the curSpecFilename be set.
 */
void BuildParser::buildTables(PSM_SCORE_TYPE scoreType, string specFilename) {
    // return if no psms for this file
    if( psms_.size() == 0 ) {
        Verbosity::status("No matches found in %s.", curSpecFileName_.c_str() );
        curSpecFileName_.clear();
        if( fileProgress_ ) { 
            if( fileProgressIncrement_ == 0 ){
                fileProgress_->increment(); 
            } else {
                fileProgress_->add(fileProgressIncrement_);
            }
        }
        return;
    }

    // first prune out any duplicates from the list of psms
    removeDuplicates();

    // for reading spectrum file
    if( specReader_ ) {
        specReader_->openFile(curSpecFileName_.c_str());
        Verbosity::status("Reading spectra from %s.", curSpecFileName_.c_str());
    } else {
        throw BlibException(true, "Cannot read spectrum file '%s' with NULL "
                            "reader.", curSpecFileName_.c_str());
    }

    // count the progress of each psm as a child of the file progress
    initSpecProgress(psms_.size());

    // begin a transaction and commit after adding all spec
    blibMaker_.beginTransaction();

    // add the file name to the library
    sqlite3_int64 fileId = -1;
    if( specFilename.empty() ){
        fileId = insertSpectrumFilename(curSpecFileName_);
    } else {
        fileId = insertSpectrumFilename(specFilename, true); // insert as is
    }

    // for each psm
    for(unsigned int i=0; i<psms_.size(); i++) {
        PSM* psm = psms_.at(i);
        SpecData curSpectrum;

        // get spectrum information
        bool success = specReader_->getSpectrum(psm, lookUpBy_,
                                                curSpectrum, true); //getpeaks
        if( ! success ){
            string idStr = psm->idAsString();
            Verbosity::warn("Did not find spectrum '%s' in '%s'.",
                               idStr.c_str(), curSpecFileName_.c_str());
            continue;
        }

        Verbosity::debug("Adding spectrum %d (%s), charge %d.", 
                         psm->specKey, psm->specName.c_str(), psm->charge);

        try{
            insertSpectrum(psm, curSpectrum, fileId, scoreType);

            specProgress_->increment();

        } catch(BlibException& e){
            e.addMessage("Could not add spectrum to library: "
                         "id %s, charge %d, sequence (unmodified) %s, "
                         "score %f, from file %s.", 
                         (psm->idAsString()).c_str(), 
                         psm->charge, psm->unmodSeq.c_str(), 
                         psm->score, fullFilename_.c_str());
            if( ! e.hasFilename() ){ e.setHasFilename(true); }
            throw e;
        }
    }// last psm

    // commit those additions
    blibMaker_.endTransaction();

    // empty the psm list and spec file name
    for(size_t i = 0; i < psms_.size(); i++){
        delete psms_.at(i);
        psms_.at(i) = NULL;
    }
    psms_.clear();

    curSpecFileName_.clear();
    if( fileProgress_ ) { 
        if( fileProgressIncrement_ == 0 ){
            fileProgress_->increment(); 
        } else {
            fileProgress_->add(fileProgressIncrement_);
        }
    }
}

/**
 * Given a PSM and its corresponding spectrum, insert it into the
 * library.
 */
void BuildParser::insertSpectrum(PSM* psm, 
                                 SpecData& curSpectrum, 
                                 sqlite3_int64 fileId,
                                 PSM_SCORE_TYPE scoreType){
    char sql_statement_buf[LARGE_BUFFER_SIZE];

    // get the spec id in the spec file
    string specIdStr = psm->idAsString();
    
    // construct insert statement for RefSpectra
    sprintf(sql_statement_buf,
            "INSERT INTO RefSpectra(peptideSeq, precursorMZ,"
            "precursorCharge, peptideModSeq, prevAA, nextAA, copies,"
            "numPeaks, retentionTime, fileID, specIDinFile, score, "
            "scoreType) "
            "VALUES('%s', %f, %d, '%s', '%s', '%s', 1, %d, %f, %lld, '%s', "
            "%f, %d)",
            psm->unmodSeq.c_str(),
            curSpectrum.mz,
            psm->charge,
            psm->modifiedSeq.c_str(),
            "-", "-", // don't add flanking AA
            curSpectrum.numPeaks,
            curSpectrum.retentionTime,
            fileId,
            specIdStr.c_str(),
            psm->score,
            scoreType);
    
    // submit
    blibMaker_.sql_stmt(sql_statement_buf);
    sql_statement_buf[0]='\0';
    
    // get library's ID for the spectrum
    int libSpecId = (int)sqlite3_last_insert_rowid(blibMaker_.getDb());
    
    // insert peaks into library
    blibMaker_.insertPeaks(libSpecId,
                           curSpectrum.numPeaks,
                           curSpectrum.mzs,
                           curSpectrum.intensities);
    sql_statement_buf[0]='\0';
    
    // for each modification, build insert statement and submit
    for(unsigned int i=0; i<psm->mods.size(); i++) {
        if( psm->mods.at(i).deltaMass == 0 ){
            continue;
        }
        sprintf(sql_statement_buf,
                "INSERT INTO Modifications(RefSpectraID, position, mass) "
                "VALUES(%d,%d,%f)",
                libSpecId,
                psm->mods.at(i).position,
                psm->mods.at(i).deltaMass);
        blibMaker_.sql_stmt(sql_statement_buf);
        sql_statement_buf[0]='\0';
        
    }// next mod

}

/**
 * \brief Create a sequence that includes modifications from an
 * unmodified seq and a list of mods.  Assumes that mods are sorted in
 * increasing order by position and that no two entries in the mods
 * vector are to the same position.
 */
string BuildParser::generateModifiedSeq(const char* unmodSeq,
                                       const vector<SeqMod>& mods) {
    string modifiedSeq(unmodSeq);
    char modBuffer[SMALL_BUFFER_SIZE];

    // insert mods from the rear so that the position remains the same
    for(int i = mods.size() - 1; i > -1; i--) {
        if( mods.at(i).deltaMass == 0 ) {
            continue;
        }
        if( mods.at(i).position > (int)modifiedSeq.size() ){
            throw BlibException(false, 
                                "Cannot modify sequence %s, length %d, at "
                                "position %d. ", modifiedSeq.c_str(), 
                                modifiedSeq.size(), mods.back().position);
        }

        sprintf(modBuffer, "[%+.1f]", mods.at(i).deltaMass);
        modifiedSeq.insert(mods.at(i).position, modBuffer);
    }

    return modifiedSeq;
}

bool seqsILEquivalent(string seq1, string seq2)
{
    if (seq1.length() != seq2.length())
        return false;
    for (int i = 0; i < (int)seq1.length(); i++){
        char c1 = seq1[i];
        char c2 = seq2[i];
        if (c1 != c2 && ((c1 != 'I' && c1 != 'L' && c1 != 'J') ||
                (c2 != 'I' && c2 != 'L' && c2 != 'J')))
            return false;
    }
    return true;
}

/**
 * \brief Find any spectra in the psms list twice and remove those
 * with conflicts.
 *
 * Only spec listed at the same charge state with sequences that
 * differ by a I/L swap can be kept as duplicates.  Spectra at
 * different charge states or with two different sequences will be
 * discarded.
 */
void BuildParser::removeDuplicates() {

    map<string,int> keyIndexPairs; // spectrum id, position in the vector

    int startingNumPsms = psms_.size(); // for debugging

    for(unsigned int i=0; i<psms_.size(); i++) {
        PSM* psm = psms_.at(i);
        if( psm == NULL ){
            continue;
        }
        // create the modified sequence, if we don't have it already
        if( psm->modifiedSeq.empty() ){
            sortPsmMods(psm);
            psm->modifiedSeq = generateModifiedSeq(psm->unmodSeq.c_str(),
                                                   psm->mods);
        }

        // choose the correct id type
        string id = boost::lexical_cast<string>(psm->specKey);
        if( lookUpBy_ == INDEX_ID ){
            id = boost::lexical_cast<string>(psm->specIndex);
        } else if( lookUpBy_ == NAME_ID ){
            id = psm->specName;
        }

        // have we seen this spec key yet?
        map<string,int>::iterator found = keyIndexPairs.find(id);
        if( found == keyIndexPairs.end() ) {
            // not seen yet
            keyIndexPairs[id] = i;
        } else {
            // it's a duplicate, four possibilties
            int dupIndex = found->second;
            PSM* dupPSM = psms_.at(dupIndex);
            // 1. was the duplicate already removed because of a third id
            if( dupPSM == NULL ){
                Verbosity::comment(V_DEBUG,
                       "Removing duplicate spectrum id '%s' with sequence %s.",
                                   id.c_str(), psm->modifiedSeq.c_str());
                // delete current
                delete psms_.at(i);
                psms_.at(i) = NULL;
            }
            // 2. check for identical sequences
            else if( psm->modifiedSeq == dupPSM->modifiedSeq ){
                // remove only one
                delete psms_.at(i);
                psms_.at(i) = NULL;
            }
            // 2. check for I/L differences
            else if ( seqsILEquivalent(psm->modifiedSeq, dupPSM->modifiedSeq)){
                keyIndexPairs[id] = i;
            }
            // 3. else delete
            else {
                Verbosity::comment(V_DEBUG, "Removing duplicate spectra id "
                                   "'%s', sequences %s and %s.",
                                   id.c_str(), psm->modifiedSeq.c_str(),
                                   dupPSM->modifiedSeq.c_str());
                // delete current
                delete psms_.at(i);
                psms_.at(i) = NULL;

                // delete position found
                delete psms_.at(dupIndex);
                psms_.at(dupIndex) = NULL;
            }
        }
    }// next psm

    // fill in any gaps
    unsigned int insert_index = 0;
    for(unsigned int move_index = 0; move_index < psms_.size(); move_index++ ) {

        if( psms_.at(move_index) != NULL) {
            if(move_index > insert_index ) {
                psms_.at(insert_index) = psms_.at(move_index);
                psms_.at(move_index) = NULL;
            }
            // if the two indexes are the same, just increment insert
            insert_index++;
        }
        else if( psms_.at(move_index) != NULL && move_index == insert_index ) {
            insert_index++;
        }

    }// next psm

    // resize psms deleting slots after insert_index
    psms_.resize(insert_index);

    Verbosity::debug( "%i psms before removing duplicates and %i after",
                       startingNumPsms, psms_.size());
}

/**
 * \brief Count the progress of reading the psm file separately from
 * the progress of adding spectra to the library.
 *
 * Optionally set by inherited classes if they want to track the rate
 * of results being read from the input file.  Nesting is
 * parent->readAdd->file->spec with readAdd and file being optional.
 */
void BuildParser::initReadAddProgress(){
    readAddProgress_ = parentProgress_->newNestedIndicator(2);
}

/**
 * \brief Register the number of spectrum files that will be processed.
 *
 * Generates a new progress indicator that will cout up to a fraction of
 * the parent progress.  (The caller (parent) tracks progress of build
 * input files, this tracks progress within that input file).  This
 * method only applies to input file types that contain references to
 * more than spectrum file (e.g. pepXML, idpXML).
 */
void BuildParser::initSpecFileProgress(int numSpecFiles) {
    if( readAddProgress_ ){
        fileProgress_ = readAddProgress_->newNestedIndicator(numSpecFiles);
    } else {
        fileProgress_ = parentProgress_->newNestedIndicator(numSpecFiles);
    }
}

/**
 * \brief Return the minimum score of matches that will go in the
 * library.
 *
 * The builder will have parsed the command line and stored the
 * thresholds for each input file type.  Subclasses do not have access
 * to the private builder.
 */
double BuildParser::getScoreThreshold(BUILD_INPUT fileType) {
    return blibMaker_.getScoreThreshold(fileType);
}

string BuildParser::getFileName() {
    return fullFilename_;
}

string BuildParser::getSpecFileName() {
    return curSpecFileName_;
}

/**
 * \brief Read through the mxXML file one spec at a time and match up
 * scan index numbers with the scan names used by Spectrum Mill.
 * Can't use MSToolkit because it doesn't extract the parentFileName
 * attribute from the spectrum.  Neither does RAMP.
 */
void BuildParser::findScanNumFromName() {

    // make a map of names and scan numbers (to be filled in)
    map<string, int> nameNumTable;
    for(size_t i=0; i<psms_.size(); i++) {
        PSM* cur_psm = psms_.at(i);
        nameNumTable.insert( pair<string,int>(cur_psm->specName, -1) );
    }

    // open and read file using the custom reader
    const char* specFileName = curSpecFileName_.c_str();
    mzxmlFinder reader(specFileName);
    reader.findScanNumFromName(&nameNumTable);

    for(size_t i=0; i<psms_.size(); i++) {
        PSM* cur_psm = psms_.at(i);
        // get the new scan number and update the psm
        cur_psm->specKey = nameNumTable.find(cur_psm->specName)->second;
    }

}

/**
 * \brief Read through the mxXML file one spec at a time and match up
 * scan index numbers with the scan names used by Spectrum Mill.
 * Can't use MSToolkit because it doesn't extract the parentFileName
 * attribute from the spectrum.  Neither does RAMP.
 */
void BuildParser::findScanIndexFromName() {

    // make a map for of names and scan numbers (to be filled in)
    map<string, int> nameNumTable;
    for(size_t i=0; i<psms_.size(); i++) {
        PSM* cur_psm = psms_.at(i);
        nameNumTable.insert( pair<string,int>(cur_psm->specName, -1) );
    }

    // open and read file using the custom reader
    const char* specFileName = curSpecFileName_.c_str();
    mzxmlFinder reader(specFileName);
    reader.findScanIndexFromName(&nameNumTable);

    for(size_t i=0; i<psms_.size(); i++) {
        PSM* cur_psm = psms_.at(i);
        // get the new scan number and update the psm
        cur_psm->specIndex = nameNumTable.find(cur_psm->specName)->second;
    }

}
void BuildParser::initSpecProgress(int numSpec){

    if( specProgress_ ){
        delete specProgress_;
    }

    if( fileProgress_ ) {
        specProgress_ = fileProgress_->newNestedIndicator(numSpec);
    } else {
        specProgress_ = parentProgress_->newNestedIndicator(numSpec);
    }
}

void BuildParser::sortPsmMods(PSM* psm){
    vector<SeqMod>& mods = psm->mods;
    if (mods.size() > 1) {
        sort(mods.begin(), mods.end(), lessThanPosition);
        // combine any mods to the same aa
        for(size_t i = 0; i < mods.size() - 1; i++) {
            while( i < mods.size() - 1 &&
                   mods.at(i).position == mods.at(i + 1).position ) {
                mods.at(i).deltaMass += mods.at(i+1).deltaMass;
                mods.erase(mods.begin()+i+1);
            }
        }
    }
}

/**
 * For pep.xml files that not have the number of spectrum files to
 * read, do the "file" progress as a measure of how far through the
 * .pep.xml file we have read.  The increment must jump by the number
 * of bytes read since last time.
 */
void BuildParser::setNextProgressSize(int size){
    fileProgressIncrement_ = size;
}

/**
 * Look in the ID string of the spectrum for the name of the file it
 * originally came from.  Return an empty string if no file found.
 */
string BuildParser::getFilenameFromID(const string& idStr){

    string filename = ""; // default if not found

    size_t start = idStr.find("File:");
    if( start != string::npos ){ // found it
        start = idStr.find_first_not_of(' ', start + strlen("File:"));
        if ( start != string::npos ){
            size_t end = idStr[start] != '"'
                ? idStr.find_first_of(',', start)
                : idStr.find_first_of('"', ++start);

            if ( end == string::npos )
                end = idStr.length();

            filename = idStr.substr(start, end - start);
        }
    }
    if (filename.empty()){
        // check for TPP/SEQUEST format <basename>.<start scan>.<end scan>.<charge>[.dta]
        vector<string> parts;
        boost::split(parts, idStr, boost::is_any_of("."));
        if ((parts.size() == 4 || (parts.size() == 5 && strcmp(parts[4].c_str(), "dta") == 0))
                              && atoi(parts[1].c_str()) != 0
                              && atoi(parts[2].c_str()) != 0
                              && atoi(parts[3].c_str()) != 0){

            filename = parts[0];

            // check for special ScaffoldIDNumber prefix
            const char* scaffoldPrefix = "ScaffoldIDNumber_";
            size_t lenPrefix = strlen(scaffoldPrefix);
            if (strncmp(filename.c_str(), scaffoldPrefix, lenPrefix) == 0) {
                size_t endPrefix = filename.find("_", lenPrefix);
                if (endPrefix != string::npos && endPrefix < filename.length() - 1
                        && atoi(filename.substr(lenPrefix, endPrefix - lenPrefix).c_str()) != 0)
                    filename = filename.substr(endPrefix + 1, filename.length() - endPrefix - 1);
            }
        }
    }
    if (filename.empty()){
        // Proteome Discoverer format <basename>-<spectrum id>-<start scan>_<end scan>
        size_t lastDash = idStr.rfind("-");
        if (lastDash != string::npos){
            size_t lastDash2 = idStr.rfind("-", lastDash - 1);
            if (lastDash2 != string::npos){
                size_t suffixStart = lastDash + 1;
                size_t spectrumStart = lastDash2 + 1;
                vector<string> parts;
                string startAndEnd = idStr.substr(suffixStart, idStr.length() - suffixStart);
                boost::split(parts, startAndEnd, boost::is_any_of("_"));
                if (parts.size() == 2
                        && atoi(parts[0].c_str()) != 0
                        && atoi(parts[1].c_str()) != 0
                        && atoi(idStr.substr(spectrumStart, lastDash - spectrumStart).c_str()) != 0){
                    filename = idStr.substr(0, lastDash2);
                }
            }
        }
    }

    return filename;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
