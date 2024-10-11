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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "BuildParser.h"
#include "SpecData.h"
#include "SslReader.h"

namespace BiblioSpec {

BuildParser::BuildParser(BlibBuilder& maker,
                         const char* filename,
                         const ProgressIndicator* parentProgress_)
: fullFilename_(filename),
  blibMaker_(maker),
  fileProgressIncrement_(0),
  filteredOutPsmCount_(0),
  lookUpBy_(SCAN_NUM_ID)
{
    // initialize amino acid masses
    std::fill(aaMasses_, aaMasses_ + sizeof(aaMasses_)/sizeof(double), 0);
    AminoAcidMasses::initializeMass(aaMasses_, 1);

    // parse full file name to get path and fileroot
    filepath_ = getPath(fullFilename_);
    fileroot_ = getFileRoot(fullFilename_);

    preferEmbeddedSpectra_ = maker.preferEmbeddedSpectra().get_value_or(true);

    this->parentProgress_ = parentProgress_;
    this->readAddProgress_ = NULL;
    this->fileProgress_ = NULL;
    this->specProgress_ = NULL;
    this->curPSM_ = NULL;
    this->specReader_ = new PwizReader();

    string stmt = "INSERT INTO RefSpectra(peptideSeq, precursorMZ, precursorCharge, "
        "peptideModSeq, prevAA, nextAA, copies, numPeaks, ionMobility, collisionalCrossSectionSqA, "
        "ionMobilityHighEnergyOffset, ionMobilityType, retentionTime, startTime, endTime, totalIonCurrent, fileID, "
        "specIDinFile, score, scoreType" + 
        SmallMolMetadata::sql_col_names_csv() + 
        ") VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) ";
    sqlite3_prepare(maker.getDb(), stmt.c_str(),
     -1, &insertSpectrumStmt_, NULL);

}

BuildParser::~BuildParser() {
    delete readAddProgress_;
    delete fileProgress_;
    delete specProgress_;
    delete specReader_;
    sqlite3_finalize(insertSpectrumStmt_);
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
  std::string specfileroot,///< basename of file
  const vector<std::string>& extensions, ///< extensions to be searched in order
  const vector<std::string>& directories)///< directories to be searched in order
{

    curSpecFileName_.clear();

    auto localDirectories = directories;
    bal::replace_all(specfileroot, "\\", "/"); // attempt to make Windows paths parseable on POSIX

    // if specfileroot has a parent path, try that directory first
    bfs::path specfilepath(specfileroot);
    if (specfilepath.has_parent_path())
    {
        try
        {
            if (bfs::exists(bfs::complete(specfilepath.parent_path(), filepath_)))
            {
                localDirectories.insert(localDirectories.begin(), specfilepath.parent_path().string());
            }
        }
        catch (...)
        {
            // ignore any error that might happen checking if the file exists
        }
    }

    string fileroot = specfilepath.filename().string();
    Verbosity::debug("checking for basename: %s", fileroot.c_str());
    do {
        // try the location of the result file, then all dirs in the list
        for(int i=-1; i<(int)localDirectories.size(); i++) {

            string path = filepath_.c_str();
            if( i >= 0 ) {
                if (bfs::path(localDirectories[i]).is_absolute())
                    path = localDirectories[i];
                else
					path += localDirectories[i];
            }
            if (path.empty())
                path = ".";
            for (const string& ext : extensions) { // Search for extensions in priority order
                for (const auto& dir : bfs::directory_iterator(path)) {
                    bfs::path dirPath = dir.path();
                    string trialName = dirPath.filename().string();
                    // case insensitive filename comparison (i.e. so POSIX systems can match to basename.MGF or BaseName.mgf)
                    if (!bal::iequals(fileroot + ext, trialName))
                        continue;

                    if (bfs::is_directory(dirPath)) {
                        curSpecFileName_ = dirPath.string();
                        break;
                    }

                    ifstream file(dirPath.string().c_str());
                    if (file.good()) {
                        curSpecFileName_ = dirPath.string();
                        break;
                    }
                    else
                        Verbosity::comment(V_DETAIL, "cannot open spectrum file %s", dirPath.string().c_str());
                }

                if (!curSpecFileName_.empty())
                    break;

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
                                               extensions, localDirectories);
        throw BlibException(true, extString);
    }// else we found a file and set the name

    Verbosity::comment(V_DETAIL, "spectrum filename set to %s", curSpecFileName_.c_str());
}

/**
 * \brief Register the name of the next spectrum file to be read.
 *
 * Assumes filename is a fully-qualified path.  If file is not found
 * throws error.
 * Must be called before buildTables().
 */
void BuildParser::setSpecFileName
    (std::string specfile,  ///< name of spectrum file
     bool checkFile)         ///< see if the file exists
{
    curSpecFileName_.clear();
    if( checkFile ){
        ifstream file(specfile.c_str());
        if(!file.good()) {
            throw BlibException(true, "Could not open spectrum file '%s' for search results file '%s'.", 
                                specfile.c_str(), fullFilename_.c_str());
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
    std::string specfileroot,///< basename of file
    const vector<std::string>& extensions, ///< extensions searched
    const vector<std::string>& directories) ///< directories searched
{
    return filesNotFoundMessage(vector<std::string>(1, specfileroot), extensions, directories);
}

/**
 * \brief Generate a string indicating that no file with the given
 * base names and any of the extensions could be found in any of the
 * directories.
 */
string BuildParser::filesNotFoundMessage(
     const vector<std::string>& specfileroots,///< basename of files
     const vector<std::string>& extensions, ///< extensions searched
     const vector<std::string>& directories) ///< directories searched
{
    if (extensions.empty())
        throw BlibException(false, "empty extensions list for filesNotFoundMessage");

    string extString = boost::algorithm::join(extensions, ", ");

    string filesPlural = "file";
    string namesPlural = "name";
    if (specfileroots.size() > 1)
        filesPlural += "s", namesPlural += "s";

    string messageString = "While searching for spectrum " + filesPlural + " for the search results file '" + fullFilename_ +
                           "', could not find matches for the following base" + namesPlural +
                           " with any of the supported file extensions (" + extString + "):";
    for (const auto& specfileroot : specfileroots)
        messageString += "\n" + specfileroot;

    bfs::path deepestPath = filepath_.empty() ? bfs::current_path() : bfs::path(filepath_);
    messageString += "\n\nIn any of the following directories:\n" + bfs::canonical(deepestPath).make_preferred().string();
    set<string> parentPaths;
    for (const auto& dir : directories)
        parentPaths.insert((bfs::path(dir).is_absolute() ? dir : bfs::canonical(deepestPath / dir)).make_preferred().string());
    for (const auto& dir : boost::make_iterator_range(parentPaths.rbegin(), parentPaths.rend()))
        messageString += "\n" + dir;

    return messageString;
}

/**
* \brief Sets whether to prefer getting peaks from embedded sources (Mascot DAT, MaxQuant msms.txt, etc.) or external files (mzML, RAW, etc.)
*/
void BuildParser::setPreferEmbeddedSpectra(bool preferEmbeddedSpectra)
{
    preferEmbeddedSpectra_ = preferEmbeddedSpectra;
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
    int existingFileId = blibMaker_.getFileId(fullPath, blibMaker_.getCutoffScore());
    if (existingFileId >= 0) {
        return existingFileId;
    }

    // get the file ID to save with each spectrum
    sqlite3_int64 fileId = blibMaker_.addFile(fullPath, blibMaker_.getCutoffScore(), fullFilename_);

    const int MAX_SPECTRUM_FILES = 2000;
    int curFile = blibMaker_.getCurFile();
    map<int, int>::iterator inputLookup = inputToSpec_.find(curFile);
    if (inputLookup == inputToSpec_.end())
    {
        inputLookup = inputToSpec_.insert(pair<int, int>(curFile, 1)).first;
    }
    else if (++(inputLookup->second) > MAX_SPECTRUM_FILES)
    {
        throw BlibException(false, "Maximum limit of %d spectrum source files was exceeded. There "
                            "was most likely a problem reading the filenames.", MAX_SPECTRUM_FILES);
    }
    Verbosity::debug("Input file %d has had %d spectrum source files inserted", curFile, inputLookup->second);

    return fileId;
}

sqlite3_int64 BuildParser::insertProtein(const Protein* protein) {
    // first see if the protein already exists
    string statement = "SELECT id FROM Proteins WHERE accession = '" +
        SqliteRoutine::ESCAPE_APOSTROPHES(protein->accession) + "'";

    char** result;
    int iRow, iCol;
    int returnCode = sqlite3_get_table(blibMaker_.getDb(), statement.c_str(), &result, &iRow, &iCol, 0);
    blibMaker_.check_rc(returnCode, statement.c_str());
    if (iRow > 0) { // protein already exists
        sqlite3_int64 proteinId = atol(result[1]);
        sqlite3_free_table(result);
        return proteinId;
    }
    sqlite3_free_table(result);

    string sql_statement = "INSERT INTO Proteins (accession) VALUES('" +
        SqliteRoutine::ESCAPE_APOSTROPHES(protein->accession) + "')";

    blibMaker_.sql_stmt(sql_statement.c_str());
    return sqlite3_last_insert_rowid(blibMaker_.getDb());
}

// Optionally sort the psms before writing
void BuildParser::OptionalSort(PSM_SCORE_TYPE scoreType)
{
    if (scoreType == PSM_SCORE_TYPE::HARDKLOR_IDOTP)
    {
        // Sort PSMs by mass before writing to library
        std::stable_sort(psms_.begin(), psms_.end(), [](PSM* a, PSM* b)
        {
            if (a == NULL || b == NULL)
            {
                return b != NULL; // Nulls are ultimately ignored, but sort consistency matters
            }
            // Pick out the "123.45" from "mass123.45_RT6.78"
            const double massA = boost::lexical_cast<double>(a->smallMolMetadata.moleculeName.substr(4, a->smallMolMetadata.moleculeName.find("_")));
            const double massB = boost::lexical_cast<double>(b->smallMolMetadata.moleculeName.substr(4, b->smallMolMetadata.moleculeName.find("_")));
            if (massA == massB)
            {
                if (a->charge == b->charge)
                    return a->score > b->score; // High score first, so it gets retained in case we're discarding ambiguous
                return a->charge < b->charge;
            }
            return massA < massB; // Lower mass first
        }
        );
    }
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
void BuildParser::buildTables(PSM_SCORE_TYPE scoreType, string specFilename, bool showSpecProgress) {
    // return if no psms for this file
    if( psms_.size() == 0 ) {
        Verbosity::warn("No matches passed score filter in %s. %d matches did not pass filter.", curSpecFileName_.c_str(), filteredOutPsmCount_ );
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

    Verbosity::status("Read %d matches that passed the score filter (%d matches did not pass).", psms_.size(), filteredOutPsmCount_);

    // make sure sequences are uppercase; generate modified sequences if necessary
    verifySequences();

    // filter psms by sequence
    filterBySequence(blibMaker_.getTargetSequences(), blibMaker_.getTargetSequencesModified());

    bool hasMatches = psms_.size() > 0;
    if (!hasMatches)
        Verbosity::status("No matches left after filtering for target sequences in %s.", curSpecFileName_.c_str());

    // Optionally sort the psms before writing
    OptionalSort(scoreType);

    // prune out any duplicates from the list of psms
    if (!keepAmbiguous()) {
        removeDuplicates();
        removeNulls();
        hasMatches = psms_.size() > 0;
        if (!hasMatches)
            Verbosity::status("No matches left after removing ambiguous spectra in %s.", curSpecFileName_.c_str());
    }

    bool needsSpectra = false;
    for (unsigned int i = 0; i < psms_.size(); i++) {
        PSM* psm = psms_.at(i);
        if (psm != NULL && !psm->isPrecursorOnly())
        {
            needsSpectra = true;
            break;
        }
    }

    // for reading spectrum file
    if( specReader_ ) {
        if (needsSpectra)
        {
            Verbosity::status("Loading %s.", curSpecFileName_.c_str());
            specReader_->openFile(curSpecFileName_.c_str());
            Verbosity::status("Reading spectra from %s.", curSpecFileName_.c_str());
        }
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

    BiblioSpec::Verbosity::debug("BuildParser lookup method is %s", specIdTypeToString(lookUpBy_));

    // for each psm
    map<const Protein*, sqlite3_int64> proteinIds;
    for(unsigned int i=0; i<psms_.size(); i++) {
        PSM* psm = psms_.at(i);
        SpecData curSpectrum;

        // get spectrum information
        bool success = needsSpectra ? specReader_->getSpectrum(psm, lookUpBy_,
                                                curSpectrum, !psm->isPrecursorOnly()) : true;
        if( ! success ){
            string idStr = psm->idAsString();
            Verbosity::warn("Did not find spectrum '%s' in '%s'.",
                               idStr.c_str(), curSpecFileName_.c_str());
            continue;
        }

        curSpectrum.totalIonCurrent = 0;
        if (!psm->isPrecursorOnly())
        {
            for (int j = 0; j < curSpectrum.numPeaks; ++j)
                curSpectrum.totalIonCurrent += curSpectrum.intensities[j];
        }
        else
        {
            curSpectrum.numPeaks = 0;
        }

        Verbosity::comment(V_DETAIL, "Adding spectrum %d (%s), charge %d.", 
                           psm->specKey, psm->specName.c_str(), psm->charge);

        try{
            insertSpectrum(psm, curSpectrum, fileId, scoreType, proteinIds);

            if (showSpecProgress) {
                specProgress_->increment();
            }

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

bool BuildParser::keepAmbiguous()
{
    return blibMaker_.keepAmbiguous();
}

/**
 * Given a PSM and its corresponding spectrum, insert it into the
 * library.
 */
void BuildParser::insertSpectrum(PSM* psm, 
                                 const SpecData& curSpectrum, 
                                 sqlite3_int64 fileId,
                                 PSM_SCORE_TYPE scoreType,
                                 map<const Protein*, sqlite3_int64>& proteins) {
    char sql_statement_buf[LARGE_BUFFER_SIZE];

    // get the spec id in the spec file
    string specIdStr = psm->idAsString();

    // check if charge state exists
    if (psm->charge == 0) {
        // try to calculate charge
        Verbosity::debug("Attempting to calculate charge state for spectrum %s (%s)",
                         specIdStr.c_str(), psm->modifiedSeq.c_str());
        double pepMass = calculatePeptideMass(psm);
        int calcCharge = calculateCharge(pepMass, curSpectrum.mz);
        if (calcCharge > 0) {
            psm->charge = calcCharge;
        } else {
            Verbosity::warn("Could not calculate charge state for spectrum %s (%s, "
                            "mass %f and precursor m/z %f)",
                            specIdStr.c_str(), psm->modifiedSeq.c_str(), pepMass, curSpectrum.mz);
        }
    }

    if (!blibMaker_.keepCharge(psm->charge)) // Ignore items with unwanted charges
    {
        return;
    }

    // this order must agree with insertSpectrumStmt_ as set in the ctor
    int field = 1;
    sqlite3_bind_text(insertSpectrumStmt_, field++, psm->unmodSeq.c_str(), -1, SQLITE_STATIC);
    sqlite3_bind_double(insertSpectrumStmt_, field++, psm->smallMolMetadata.precursorMzDeclared == 0 ? curSpectrum.mz : psm->smallMolMetadata.precursorMzDeclared);
    sqlite3_bind_int(insertSpectrumStmt_, field++, psm->charge);
    sqlite3_bind_text(insertSpectrumStmt_, field++, psm->modifiedSeq.c_str(), -1, SQLITE_STATIC);
    sqlite3_bind_text(insertSpectrumStmt_, field++, "-", -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(insertSpectrumStmt_, field++, "-", -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(insertSpectrumStmt_, field++, 1);
    sqlite3_bind_int(insertSpectrumStmt_, field++, curSpectrum.numPeaks);
    sqlite3_bind_double(insertSpectrumStmt_, field++, (psm->ionMobilityType == IONMOBILITY_NONE ? curSpectrum.ionMobility : psm->ionMobility));
    sqlite3_bind_double(insertSpectrumStmt_, field++, curSpectrum.ccs);
    sqlite3_bind_double(insertSpectrumStmt_, field++, curSpectrum.getIonMobilityHighEnergyOffset());
    sqlite3_bind_int(insertSpectrumStmt_, field++, (int) (psm->ionMobilityType == IONMOBILITY_NONE ? curSpectrum.ionMobilityType : psm->ionMobilityType));
    sslPSM* sslpsm = dynamic_cast<sslPSM*>(psm);
    double rt = (curSpectrum.retentionTime == 0 && sslpsm != NULL) ? sslpsm->rtInfo.retentionTime : curSpectrum.retentionTime;
    double rtStart = (curSpectrum.startTime == 0 && sslpsm != NULL) ? sslpsm->rtInfo.startTime : curSpectrum.startTime;
    double rtEnd = (curSpectrum.endTime == 0 && sslpsm != NULL) ? sslpsm->rtInfo.endTime : curSpectrum.endTime;
    if (rt != 0) {
        sqlite3_bind_double(insertSpectrumStmt_, field++, rt);
    } else {
        sqlite3_bind_null(insertSpectrumStmt_, field++);
    }
    if (rtStart != 0 && rtEnd != 0) {
        sqlite3_bind_double(insertSpectrumStmt_, field++, rtStart);
        sqlite3_bind_double(insertSpectrumStmt_, field++, rtEnd);
    } else {
        sqlite3_bind_null(insertSpectrumStmt_, field++);
        sqlite3_bind_null(insertSpectrumStmt_, field++);
    }
    if (psm->isPrecursorOnly())
    {
        sqlite3_bind_null(insertSpectrumStmt_, field++); // No TIC if no spectrum
    }
    else
    {
        sqlite3_bind_double(insertSpectrumStmt_, field++, curSpectrum.totalIonCurrent);
    }
    sqlite3_bind_int(insertSpectrumStmt_, field++, fileId);
    sqlite3_bind_text(insertSpectrumStmt_, field++, 
        psm->isPrecursorOnly() ? "" : specIdStr.c_str(), // No spectrum ID for precursor-only records
        -1, SQLITE_STATIC);
    sqlite3_bind_double(insertSpectrumStmt_, field++, psm->score);
    sqlite3_bind_int(insertSpectrumStmt_, field++, scoreType);
    // Small molecule: moleculeName VARCHAR(128), chemicalFormula VARCHAR(128), precursorAdduct VARCHAR(128), inchiKey VARCHAR(128), otherKeys VARCHAR(128)
    sqlite3_bind_text(insertSpectrumStmt_, field++, psm->smallMolMetadata.moleculeName.c_str(), -1, SQLITE_STATIC);
    sqlite3_bind_text(insertSpectrumStmt_, field++, psm->smallMolMetadata.chemicalFormula.c_str(), -1, SQLITE_STATIC);
    sqlite3_bind_text(insertSpectrumStmt_, field++, psm->smallMolMetadata.precursorAdduct.c_str(), -1, SQLITE_STATIC);
    sqlite3_bind_text(insertSpectrumStmt_, field++, psm->smallMolMetadata.inchiKey.c_str(), -1, SQLITE_STATIC);
    sqlite3_bind_text(insertSpectrumStmt_, field++, psm->smallMolMetadata.otherKeys.c_str(), -1, SQLITE_STATIC);

    
    // submit
    sqlite3_step(insertSpectrumStmt_);
    sqlite3_reset(insertSpectrumStmt_);
    
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

    // insert protein info
    for (set<const Protein*>::const_iterator i = psm->proteins.begin(); i != psm->proteins.end(); i++) {
        map<const Protein*, sqlite3_int64>::const_iterator j = proteins.find(*i);
        sqlite3_int64 proteinId = (j == proteins.end()) ? insertProtein(*i) : j->second;
        sprintf(sql_statement_buf,
                "INSERT INTO RefSpectraProteins (RefSpectraId, ProteinId) VALUES (%d, %lld)",
                libSpecId, proteinId);
        blibMaker_.sql_stmt(sql_statement_buf);
        sql_statement_buf[0] = '\0';
    }
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

void BuildParser::verifySequences()
{
    for (vector<PSM*>::iterator iter = psms_.begin(); iter != psms_.end(); ++iter)
    {
        PSM* psm = *iter;
        // make sure sequence is all uppercase
        boost::to_upper(psm->unmodSeq);
        // create the modified sequence, if we don't have it already
        if( psm->modifiedSeq.empty() ){
            sortPsmMods(psm);
            psm->modifiedSeq = blibMaker_.generateModifiedSeq(psm->unmodSeq.c_str(),
                                                                psm->mods);
        } else {
            boost::to_upper(psm->modifiedSeq);
        }
    }
}

/**
 * Erase all PSMs except those with an unmodified sequence matching a sequence in targetSequences, or
 * a modified sequence matching a sequence in targetSequencesModified.
 * Does not erase any PSMs if there are no target sequences.
 */
void BuildParser::filterBySequence(const set<string>* targetSequences,
                                   const set<string>* targetSequencesModified)
{
    // don't filter if there are no targets
    if (targetSequences == NULL && targetSequencesModified == NULL)
    {
        return;
    }

    for (int i = (int)(psms_.size() - 1); i >= 0; --i)
    {
        // don't filter this psm if:
        //   targetSequences is not null and it contains the unmodified sequence
        //     OR
        //   targetSequencesModified is not null and it contains the modified sequence
        if ((targetSequences != NULL &&
             targetSequences->find(psms_[i]->unmodSeq) != targetSequences->end()))
        {
            continue;
        } 
        if (targetSequencesModified != NULL) 
        {
            string normalizedSequence = BlibBuilder::getLowPrecisionModSeq(psms_[i]->unmodSeq.c_str(), psms_[i]->mods);
            if (targetSequencesModified->find(normalizedSequence) != targetSequencesModified->end()) 
            {
                continue;
            }
        }
        delete psms_[i];
        psms_.erase(psms_.begin() + i);
    }
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
        if (psm->isPrecursorOnly())
        {
            continue; // There are no spectra to disambiguate
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
                if (blibMaker_.ambiguityMessages()) {
                    cout << "AMBIGUOUS:" << psm->modifiedSeq << endl;
                }
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
                if (blibMaker_.ambiguityMessages()) {
                    cout << "AMBIGUOUS:" << psm->modifiedSeq << endl
                         << "AMBIGUOUS:" << dupPSM->modifiedSeq << endl;
                }
                // delete current
                delete psms_.at(i);
                psms_.at(i) = NULL;

                // delete position found
                delete psms_.at(dupIndex);
                psms_.at(dupIndex) = NULL;
            }
        }
    }// next psm
}

void BuildParser::removeNulls() {
    int startingNumPsms = psms_.size(); // for debugging
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

const string& BuildParser::getFileName() {
    return fullFilename_;
}

const string& BuildParser::getSpecFileName() {
    return curSpecFileName_;
}

/**
 * \brief Read through the mxXML file one spec at a time and match up
 * scan index numbers with the scan names used by Spectrum Mill.
 * Can't use MSToolkit because it doesn't extract the parentFileName
 * attribute from the spectrum.  Neither does RAMP.
 */
void BuildParser::findScanIndexFromName(const map<PSM*, double>& precursorMap) {

    // make a map for of names and scan numbers (to be filled in)
    map<string, mzxmlFinder::SpecInfo*> nameNumTable;
    for(size_t i=0; i<psms_.size(); i++) {
        PSM* cur_psm = psms_.at(i);
        string specName = cur_psm->specName;
        map<PSM*, double>::const_iterator j = precursorMap.find(cur_psm);
        if (j == precursorMap.end()) {
            Verbosity::warn("Couldn't find precursor for spectrum '%s'", cur_psm->specName.c_str());
            continue;
        }
        double precursor = j->second;
        map<string, mzxmlFinder::SpecInfo*>::iterator k = nameNumTable.find(specName);
        mzxmlFinder::SpecInfo* old = k == nameNumTable.end() ? NULL : k->second;
        nameNumTable[specName] = new mzxmlFinder::SpecInfo(precursor, old);
    }

    // open and read file using the custom reader
    const char* specFileName = curSpecFileName_.c_str();
    mzxmlFinder reader(specFileName);
    reader.findScanIndexFromName(&nameNumTable);

    for(size_t i=0; i<psms_.size(); i++) {
        PSM* cur_psm = psms_.at(i);
        string specName = cur_psm->specName;
        // get the new scan number and update the psm
        map<PSM*, double>::const_iterator j = precursorMap.find(cur_psm);
        if (j == precursorMap.end()) {
            continue;
        }
        map<string, mzxmlFinder::SpecInfo*>::iterator k = nameNumTable.find(specName);
        if (k == nameNumTable.end()) {
            continue;
        }
        mzxmlFinder::SpecInfo* info = k->second->getMatch(j->second);
        if (info != NULL) {
            cur_psm->specIndex = info->getScan();
        }
    }

    // cleanup
    for (map<string, mzxmlFinder::SpecInfo*>::iterator i = nameNumTable.begin(); i != nameNumTable.end(); i++) {
        delete i->second;
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

double BuildParser::calculatePeptideMass(PSM* psm){
    double total = H2O_MASS;
    string seq = psm->unmodSeq;

    // sum amino acid masses
    for (int i = 0; i < (int)seq.length(); i++)
    {
        double curMass = aaMasses_[seq[i]];
        if (curMass > 0)
        {
            total += curMass;
        }
        else
        {
            Verbosity::warn("Ignoring unrecognized amino acid '%c' during calculation of "
                            "peptide mass: %s", seq[i], seq.c_str());
        }
    }

    // account for mods
    for(vector<SeqMod>::iterator iter = psm->mods.begin(); iter != psm->mods.end(); ++iter)
    {
        total += iter->deltaMass;
    }

    return total;
}

int BuildParser::calculateCharge(double neutralMass, double precursorMz){
    double estCharge = neutralMass / (precursorMz - PROTON_MASS);
    if (estCharge < 0.5)
    {
        return -1;
    }

    // round to nearest integer
    int calcCharge = floor(estCharge + 0.5);
    return calcCharge;
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
        // strip any following spaces
        start += strlen("File:");
        while (start < idStr.length() && idStr[start] == ' ') {
            ++start;
        }
        if (start < idStr.length()) {
            size_t end;
            // if the file attribute is quoted, end at tht next quote
            if (idStr[start] == '"'){
                end = idStr.find_first_of('"', ++start);
            } else if (idStr[start] == '~'){
                end = idStr.find_first_of('~', ++start);
            } else {
                // otherwise, end at the next comma
                end = idStr.find_first_of(',', start);
                if ( end == string::npos ) {
                    // or, if no comma is found, look for a following attribute colon
                    size_t nextAttr = idStr.find_first_of(':', start);
                    if ( nextAttr != string::npos ) {
                        // and back up to the space before the attribute name
                        end = idStr.find_last_of(' ', nextAttr);
                        // If end is now less than start, reset to end.
                        if ( end < start )
                            end = string::npos;
                        // otherwise, strip space characters to the first non-space
                        else if ( end != string::npos )
                            end = idStr.find_last_not_of(' ', end) + 1;
                    }
                }
            }
            // if no other end specifier was found, just the entire string
            if ( end == string::npos )
                end = idStr.length();

            filename = idStr.substr(start, end - start);
        }
    }

    // <basename>-MSILE-DATAID-...
    start = idStr.find("-MSILE-");
    if (start != string::npos) {
        return idStr.substr(0, start);
    }

    // check for TPP/SEQUEST format <basename>.<start scan>.<end scan>.<charge>[.dta]
    vector<string> parts;
    boost::split(parts, filename.empty() ? idStr : filename, boost::is_any_of("."));

    if ((parts.size() == 4 || (parts.size() == 5 && parts.back() == "dta")) && validInts(parts.begin() + 1, parts.begin() + 4)) {
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

                if (parts.size() == 2){

                    parts.push_back(idStr.substr(spectrumStart, lastDash - spectrumStart));
                    if (validInts(parts.begin(), parts.end())) {
                        filename = idStr.substr(0, lastDash2);
                    }
                }
            }
        }
    }

    return filename;
}

bool BuildParser::validInts(vector<string>::const_iterator begin, vector<string>::const_iterator end) {
    for (vector<string>::const_iterator i = begin; i != end; ++i) {
        int testInt = 0;
        stringstream intStream(*i);
        intStream >> testInt;
        if (intStream.get() != EOF || testInt == 0) {
            return false;
        }
    }
    return true;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
