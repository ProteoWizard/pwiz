//
// $Id$
//
//
// Original author: Kaipo Tamura <kaipot@u.washington.edu>
//
// Copyright 2013 University of Washington - Seattle, WA 98195
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

#include "MSFReader.h"

namespace BiblioSpec
{
    MSFReader::MSFReader(BlibBuilder& maker, 
                         const char* msfFile, 
                         const ProgressIndicator* parent_progress)
        : BuildParser(maker, msfFile, parent_progress),
        msfName_(msfFile)
    {
        setSpecFileName(msfFile, false);
        lookUpBy_ = INDEX_ID;
        // point to self as spec reader
        delete specReader_;
        specReader_ = this;
    }

    MSFReader::~MSFReader()
    {
        sqlite3_close(msfFile_);

        specReader_ = NULL; // so the parent class doesn't try to delete itself
        // free spectra
        for (map<int, SpecData*>::iterator it = spectra_.begin(); it != spectra_.end(); ++it)
        {
            if (it->second != NULL)
            {
                delete it->second;
                it->second = NULL;
            }
        }
    }

    bool MSFReader::parseFile()
    {
        sqlite3_open(msfName_, &msfFile_);
        if (!msfFile_)
        {
            throw BlibException(true, "Couldn't open '%s'.",
                                msfName_);
        }

        collectSpectra();
        collectPsms();

        // add psms by filename
        for (map< string, map< PSM_SCORE_TYPE, vector<PSM*> > >::iterator iter = fileMap_.begin();
             iter != fileMap_.end();
             ++iter)
        {
            for (map< PSM_SCORE_TYPE, vector<PSM*> >::iterator scoreIter = iter->second.begin();
                 scoreIter != iter->second.end();
                 ++scoreIter)
            {
                if (iter->second.size() > 0)
                {
                    psms_.assign(scoreIter->second.begin(), scoreIter->second.end());
                    setSpecFileName(iter->first.c_str(), false);
                    buildTables(scoreIter->first, iter->first);
                }
            }
        }

        return true;
    }

    /**
     * Parse all spectra.
     */
    void MSFReader::collectSpectra()
    {
        int specCount = getRowCount(
            "SpectrumHeaders "
            "WHERE SpectrumID IN (SELECT DISTINCT SpectrumID FROM Peptides)");
        Verbosity::status("Parsing %d spectra.", specCount);
        ProgressIndicator progress(specCount);

        sqlite3_stmt* statement = getStmt(
            "SELECT SpectrumID, RetentionTime, Mass, Charge, Spectrum "
            "FROM SpectrumHeaders "
            "JOIN Spectra ON SpectrumHeaders.UniqueSpectrumID = Spectra.UniqueSpectrumID "
            "WHERE SpectrumID IN (SELECT DISTINCT SpectrumID FROM Peptides)");

        // turn each row of returned table into a spectrum
        while (hasNext(statement))
        {
            int specId = sqlite3_column_int(statement, 0);
            double mass = sqlite3_column_double(statement, 2);
            double charge = sqlite3_column_int(statement, 3);

            SpecData* specData = new SpecData();
            specData->id = specId;
            specData->retentionTime = sqlite3_column_double(statement, 1);
            specData->mz = (mass + (PROTON_MASS * charge)) / charge;

            // unzip spectrum xml file
            const void* zippedSpectrumPtr = sqlite3_column_blob(statement, 4);
            size_t zippedSpectrumLen = sqlite3_column_bytes(statement, 4);
            string spectrumXml = unzipSpectrum(specId, zippedSpectrumPtr, zippedSpectrumLen);
            // read peak data from spectrum xml file
            readSpectrum(specId, spectrumXml, &(specData->numPeaks), &(specData->mzs), &(specData->intensities));

            // add spectrum to map
            spectra_[specId] = specData;
            spectraChargeStates_[specId] = (int)charge;

            progress.increment();
        }

        Verbosity::debug("Map has %d spectra", spectra_.size());
    }

    /**
     * Given a sequence of bytes and its length, assume that it is a zip archive containing one file.
     * Unzip the file and return its contents as a string.
     */
    string MSFReader::unzipSpectrum(int specId, const void* src, size_t srcLen)
    {
        // try to open zip archive 
        unzFile zipFile = openMemZip(src, srcLen);
        if (zipFile == NULL)
        {
            throw BlibException(false, "Could not open compressed spectrum %d.", specId);
        }

        // get zip file info
        unz_global_info globalInfo;
        if (unzGetGlobalInfo(zipFile, &globalInfo) != UNZ_OK ||
            globalInfo.number_entry != 1)
        {
            unzClose(zipFile);
            throw BlibException(false, "Compressed spectrum %d has invalid format.", specId);
        }

        // get spectrum file info
        unz_file_info fileInfo;
        char specFile[MAX_FILENAME];
        if (unzGetCurrentFileInfo(zipFile, &fileInfo, specFile, MAX_FILENAME, NULL, 0, NULL, 0) != UNZ_OK)
        {
            unzClose(zipFile);
            throw BlibException(false, "Could not read info for compressed spectrum file '%s' "
                                       "in compressed spectrum %d.", specFile, specId);
        }

        // open spectrum file
        if (unzOpenCurrentFile(zipFile) != UNZ_OK)
        {
            unzClose(zipFile);
            throw BlibException(false, "Could not open compressed spectrum file '%s' "
                                       "in compressed spectrum %d.", specFile, specId);
        }

        // extract contents of spectrum file to string
        char readBuffer[CHUNK_SIZE];
        string unzippedStr = "";
        int error;
        do
        {
            error = unzReadCurrentFile(zipFile, readBuffer, CHUNK_SIZE);
            if (error < 0)
            {
                unzCloseCurrentFile(zipFile);
                unzClose(zipFile);
                throw BlibException(false, "Error %d unzipping compressed spectrum file '%s' "
                                           "from compressed spectrum %d.", error, specFile, specId);
            }
            else if (error > 0)
            {
                unzippedStr.append(readBuffer, error);
            }
        } while (error != 0);

        // cleanup
        unzCloseCurrentFile(zipFile);
        unzClose(zipFile);

        return unzippedStr;
    }

    /**
     * Uses MSFSpecReader to read the spectrum XML. Stores the number of peaks, double* mzs, and
     * float* intensities at the addresses passed in.
     */
    void MSFReader::readSpectrum(int specId, string& spectrumXml,
                                 int* numPeaks, double** mzs, float** intensities)
    {
        vector<double> mzVector;
        vector<float> intensityVector;
        MSFSpecReader specReader(spectrumXml, &mzVector, &intensityVector);
        try
        {
            specReader.parse();
            *numPeaks = mzVector.size();
            Verbosity::comment(V_DETAIL, "Done parsing spectrum XML from spectrum %d, %d peaks found",
                               specId, numPeaks);
            *mzs = new double[*numPeaks];
            *intensities = new float[*numPeaks];
            copy(mzVector.begin(), mzVector.end(), *mzs);
            copy(intensityVector.begin(), intensityVector.end(), *intensities);
        }
        catch (BlibException& e)
        {
            throw BlibException(false, "Error parsing spectrum XML from spectrum %d: %s",
                                       specId, e.what());
        }
        catch (...)
        {
            throw BlibException(false, "Unknown error while parsing spectrum file %d.", specId);
        }
    }

    /**
     * Parse all PSMs.
     */
    void MSFReader::collectPsms()
    {
        int psmCount = getRowCount("Peptides");
        Verbosity::status("Parsing %d PSMs.", psmCount);
        ProgressIndicator progress(psmCount);

        map<int, PSM_SCORE_TYPE> scoreIds = getMainScores();

        if (scoreIds.empty())
        {
            throw BlibException(false, "No main score types in ProcessingNodeScores.");
        }

        string scoreConditions;
        for (map<int, PSM_SCORE_TYPE>::iterator scoreIter = scoreIds.begin();
             scoreIter != scoreIds.end();
             ++scoreIter)
        {
            if (scoreConditions.empty())
            {
                scoreConditions = "WHERE ScoreID = " + lexical_cast<string>(scoreIter->first);
            }
            else
            {
                scoreConditions += " OR ScoreID = " + lexical_cast<string>(scoreIter->first);
            }
        }

        initFileNameMap();
        set<int> passing = getPassingPSMs();
        map<int, PSM*> processedSpectra;
        map<int, double> ambiguousSpectra;
        map< int, vector<SeqMod> > modMap = getMods();
        map<int, int> fileIdMap = getFileIds();

        string query =
            "SELECT Peptides.PeptideID, SpectrumID, Sequence, ScoreValue, ScoreID "
            "FROM Peptides "
            "JOIN PeptideScores ON Peptides.PeptideID = PeptideScores.PeptideID "
            + scoreConditions;

        sqlite3_stmt* statement = getStmt(query);

        // turn each row of returned table into a psm
        int psmsAccepted = 0;
        int psmsSkipped = 0;
        while (hasNext(statement))
        {
            int peptideId = sqlite3_column_int(statement, 0);
            int specId = sqlite3_column_int(statement, 1);
            string sequence = lexical_cast<string>(sqlite3_column_text(statement, 2));
            double score = sqlite3_column_double(statement, 3);

            // skip psm if it didn't pass threshold
            set<int>::iterator passCheck = passing.find(peptideId);
            if (passCheck == passing.end())
            {
                ++psmsSkipped;
                continue;
            }

            // check if we already processed a peptide that references this spectrum
            map<int, PSM*>::iterator processedSpectraSearch = processedSpectra.find(specId);
            if (processedSpectraSearch != processedSpectra.end())
            {
                map<int, double>::iterator ambiguousCheck = ambiguousSpectra.find(specId);
                // not an ambigous spectrum (yet)
                if (ambiguousCheck == ambiguousSpectra.end())
                {
                    PSM* other = processedSpectraSearch->second;
                    // worse than other score, skip this
                    if (score < other->score)
                    {
                        Verbosity::debug("Peptide %d (%s) had a worse score than another peptide (%s) "
                                         "referencing spectrum %d (ignoring this peptide).",
                                         peptideId, sequence.c_str(), other->unmodSeq.c_str(), specId);
                        ++psmsSkipped;
                        continue;
                    }
                    // equal, discard other and skip this
                    else if (score == other->score)
                    {
                        if (sequence == other->unmodSeq)
                        {
                            // same score, but also same sequence, ignore the new one
                            ++psmsSkipped;
                            continue;
                        }
                        Verbosity::debug("Peptide %d (%s) had the same score as another peptide (%s) "
                                         "referencing spectrum %d (ignoring both peptides).",
                                         peptideId, sequence.c_str(), other->unmodSeq.c_str(), specId);

                        psmsSkipped += 2;
                        --psmsAccepted;

                        removeFromFileMap(other);
                        delete other;

                        ambiguousSpectra[specId] = score;
                        continue;
                    }
                    // better than other score, discard other
                    else
                    {
                        Verbosity::debug("Peptide %d (%s) had a better score than another peptide (%s) "
                                         "referencing spectrum %d (ignoring other peptide).",
                                         peptideId, sequence.c_str(), other->unmodSeq.c_str(), specId);
                        removeFromFileMap(other);
                        curPSM_ = other;
                        curPSM_->mods.clear();
                    }
                }
                // ambigous spectrum, check if score is better
                else
                {
                    Verbosity::debug("Peptide %d (%s) with score %f references same spectrum as other peptides "
                                     "that had score %f.", peptideId, sequence.c_str(), score, ambiguousCheck->second);
                    if (score > ambiguousCheck->second)
                    {
                        ambiguousSpectra.erase(ambiguousCheck);
                        curPSM_ = new PSM();
                        processedSpectraSearch->second = curPSM_;
                    }
                    else
                    {
                        ++psmsSkipped;
                        continue;
                    }
                }
            }
            else
            {
                // unseen spectrum
                curPSM_ = new PSM();
                processedSpectra[specId] = curPSM_;
            }

            ++psmsAccepted;

            curPSM_->charge = spectraChargeStates_[specId];
            curPSM_->unmodSeq = sequence;
            map< int, vector<SeqMod> >::iterator modAccess = modMap.find(peptideId);
            if (modAccess != modMap.end())
            {
                curPSM_->mods = modAccess->second;
                modMap.erase(modAccess);
            }
            curPSM_->specIndex = specId;
            curPSM_->score = score;

            map<int, int>::iterator fileIdMapAccess = fileIdMap.find(peptideId);
            if (fileIdMapAccess == fileIdMap.end())
            {
                throw BlibException(false, "No FileID for PSM %d.", peptideId);
            }
            string psmFileName = fileIdToName(fileIdMapAccess->second);
            fileIdMap.erase(fileIdMapAccess);

            // filename
            map< string, map< PSM_SCORE_TYPE, vector<PSM*> > >::iterator fileMapAccess = fileMap_.find(psmFileName);
            if (fileMapAccess == fileMap_.end())
            {
                map< PSM_SCORE_TYPE, vector<PSM*> > tmpMap;
                fileMap_[psmFileName] = tmpMap;
                fileMapAccess = fileMap_.find(psmFileName);
            }

            // score
            int scoreId = sqlite3_column_int(statement, 4);
            PSM_SCORE_TYPE scoreType = scoreIds[scoreId];
            map< PSM_SCORE_TYPE, vector<PSM*> >& scoreMap = fileMapAccess->second;
            map< PSM_SCORE_TYPE, vector<PSM*> >::iterator scoreMapAccess = scoreMap.find(scoreType);
            if (scoreMapAccess == scoreMap.end())
            {
                vector<PSM*> tmpVec;
                tmpVec.push_back(curPSM_);
                scoreMap[scoreType] = tmpVec;
            }
            else
            {
                scoreMapAccess->second.push_back(curPSM_);
            }

            progress.increment();
        }

        Verbosity::debug("%d psms found (%d accepted, %d not accepted).",
                         psmsAccepted + psmsSkipped, psmsAccepted, psmsSkipped);
    }

    /**
     * Initialize fileNameMap_, which maps FileID to their filenames.
     */
    void MSFReader::initFileNameMap()
    {
        sqlite3_stmt* statement = getStmt(
            "SELECT FileID, FileName "
            "FROM FileInfos");
        while (hasNext(statement))
        {
            int thisId = sqlite3_column_int(statement, 0);
            string fileName = lexical_cast<string>(sqlite3_column_text(statement, 1));
            fileNameMap_[thisId] = fileName;
        }
    }

    /**
     * Removes the PSM from the file map, if it exists.
     */
    void MSFReader::removeFromFileMap(PSM* psm)
    {
        for (map< string, map< PSM_SCORE_TYPE, vector<PSM*> > >::iterator iter = fileMap_.begin();
             iter != fileMap_.end();
             ++iter)
        {
            for (map< PSM_SCORE_TYPE, vector<PSM*> >::iterator scoreIter = iter->second.begin();
                 scoreIter != iter->second.end();
                 ++scoreIter)
            {
                for (vector<PSM*>::iterator psmIter = scoreIter->second.begin();
                     psmIter != scoreIter->second.end();
                     ++psmIter)
                {
                    if (psm == *psmIter)
                    {
                        scoreIter->second.erase(psmIter);
                        return;
                    }
                }
            }
        }
    }

    /**
     * Returns the name of the file, given the FileID.
     */
    string MSFReader::fileIdToName(int fileId)
    {
        map<int, string>::iterator mapAccess = fileNameMap_.find(fileId);
        if (mapAccess == fileNameMap_.end())
        {
            throw BlibException(false, "Invalid FileID: %d.", fileId);
        }

        return mapAccess->second;
    }

    /**
     * Gets the main score types in the MSF database.
     * Also sets the threshold_ variable, if known.
     */
    map<int, PSM_SCORE_TYPE> MSFReader::getMainScores()
    {
        sqlite3_stmt* statement;

        // get the main score types
        statement = getStmt(
            "SELECT ScoreID, ScoreName "
            "FROM ProcessingNodeScores "
            "WHERE IsMainScore = 1");

        map<int, PSM_SCORE_TYPE> scoreIds;
        while (hasNext(statement))
        {
            int scoreId = sqlite3_column_int(statement, 0);
            string scoreName = lexical_cast<string>(sqlite3_column_text(statement, 1));

            if (scoreName == "XCorr")
            {
                scoreIds[scoreId] = SEQUEST_XCORR;
            }
            else if (scoreName == "IonScore")
            {
                scoreIds[scoreId] = MASCOT_IONS_SCORE;
            }
            else
            {
                Verbosity::warn("Unrecognized score type '%s'.", scoreName.c_str());
                scoreIds[scoreId] = UNKNOWN_SCORE_TYPE;
            }
        }

        return scoreIds;
    }

    /**
     * Return a map containing all PeptideIDs of passing PSMs.
     * Returns NULL if no q-value.
     */
    set<int> MSFReader::getPassingPSMs()
    {
        sqlite3_stmt* statement;

        // check if peptides have q-values
        statement = getStmt(
            "SELECT FieldID "
            "FROM CustomDataFields "
            "WHERE DisplayName = 'q-Value' "
            "LIMIT 1");
        if (!hasNext(statement))
        {
            // no q-value fields in this database, error unless user wants everything
            if (getScoreThreshold(SQT) < 1)
                throw BlibException(false, "This file does not contain q-values. You cannot "
                                    "use a cut-off score in building a library for it.");
            statement = getStmt("SELECT PeptideID FROM Peptides");
        }
        else
        {
            sqlite3_finalize(statement);
            // get peptides with q-value <= threshold
            statement = getStmt(
                "SELECT DISTINCT PeptideID "
                "FROM CustomDataPeptides "
                "WHERE FieldID IN (SELECT FieldID FROM CustomDataFields WHERE DisplayName = 'q-Value')" 
                "   AND FieldValue <= " + lexical_cast<string>(getScoreThreshold(SQT)));
        }

        // add IDs to set
        set<int> passing;
        while (hasNext(statement))
        {
            int peptideId = sqlite3_column_int(statement, 0);
            passing.insert(peptideId);
        }

        return passing;
    }

    /**
     * Return a map that maps PeptideID to all SeqMods for that peptide.
     */
    map< int, vector<SeqMod> > MSFReader::getMods()
    {
        sqlite3_stmt* statement = getStmt(
            "SELECT PeptideID, Position, DeltaMass "
            "FROM PeptidesAminoAcidModifications "
            "JOIN AminoAcidModifications "
            "   ON PeptidesAminoAcidModifications.AminoAcidModificationID = AminoAcidModifications.AminoAcidModificationID");

        // turn each row of returned table into a seqmod to be added to the map
        map< int, vector<SeqMod> > modMap;
        map< int, vector<SeqMod> >::iterator found;
        int modCount = 0;
        while (hasNext(statement))
        {
            int peptideId = sqlite3_column_int(statement, 0);
            // mod indices are 0 based
            SeqMod mod(sqlite3_column_int(statement, 1) + 1, sqlite3_column_double(statement, 2));
            found = modMap.find(peptideId);
            if (found == modMap.end())
            {
                modMap[peptideId] = vector<SeqMod>(1, mod);
            }
            else
            {
                modMap[peptideId].push_back(mod);
            }
            ++modCount;
        }

        // get terminal mods if PeptidesTerminalModifications table exists
        statement = getStmt(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'PeptidesTerminalModifications'");
        if (hasNext(statement) && sqlite3_column_int(statement, 0) == 1) {
            sqlite3_finalize(statement);
            statement = getStmt(
                "SELECT PeptidesTerminalModifications.PeptideID, PositionType, DeltaMass, Sequence "
                "FROM PeptidesTerminalModifications "
                "JOIN Peptides ON PeptidesTerminalModifications.PeptideID = Peptides.PeptideID "
                "JOIN AminoAcidModifications ON TerminalModificationID = AminoAcidModificationID");

            // turn each row of returned table into a seqmod to be added to the map
            while (hasNext(statement))
            {
                int peptideId = sqlite3_column_int(statement, 0);
                int positionType = sqlite3_column_int(statement, 1);
                int position;
                switch (positionType) {
                case 1:
                case 3:
                    position = 1;
                    break;
                case 2:
                case 4:
                    position = strlen((const char*)sqlite3_column_text(statement, 3));
                    break;
                default:
                    throw BlibException(false, "Unknown position type in PeptideAminoAcidModifications "
                                        "for PeptideID %d", peptideId);
                }
                SeqMod mod(position, sqlite3_column_double(statement, 2));
                found = modMap.find(peptideId);
                if (found == modMap.end())
                {
                    modMap[peptideId] = vector<SeqMod>(1, mod);
                }
                else
                {
                    modMap[peptideId].push_back(mod);
                }
                ++modCount;
            }
        }

        Verbosity::debug("%d mods found for %d peptides", modCount, modMap.size());

        return modMap;
    }

    /**
     * Return a map that maps PeptideID to the FileID.
     */
    map<int, int> MSFReader::getFileIds()
    {
        sqlite3_stmt* statement = getStmt(
            "SELECT PeptideID, FileID "
            "FROM Peptides "
            "JOIN SpectrumHeaders ON Peptides.SpectrumID = SpectrumHeaders.SpectrumID "
            "JOIN MassPeaks ON SpectrumHeaders.MassPeakID = MassPeaks.MassPeakID");

        // process each row of the returned table
        map<int, int> fileIdMap;
        while (hasNext(statement))
        {
            int peptideId = sqlite3_column_int(statement, 0);
            int fileId = sqlite3_column_int(statement, 1);
            fileIdMap[peptideId] = fileId;
        }

        return fileIdMap;
    }

    /**
     * Open a zip archive from memory.
     */
    unzFile MSFReader::openMemZip(const void* src, size_t srcLen)
    {
        // set up minizip for reading from memory
        zlib_filefunc_def memFunctions;
        memFunctions.zopen_file = &MSFReader::fopenMem;
        memFunctions.zread_file = &MSFReader::freadMem;
        memFunctions.ztell_file = &MSFReader::ftellMem;
        memFunctions.zseek_file = &MSFReader::fseekMem;
        memFunctions.zclose_file = &MSFReader::fcloseMem;
        memFunctions.zerror_file = &MSFReader::ferrorMem;
        memFunctions.opaque = NULL;

        // fopenMem function expects filename parameter to be:
        // "<hex base of archive>+<hex size of archive>"
        stringstream zipInfo;
        zipInfo << hex << src << "+" << srcLen;

        // try to open zip archive 
        return unzOpen2(zipInfo.str().c_str(), &memFunctions);
    }

    /**
     * Open function to use for zip archives in memory.
     * Filename should be given as "<hex address of archive>+<hex size of archive>".
     */
    voidpf ZCALLBACK MSFReader::fopenMem(voidpf opaque, const char* filename, int mode)
    {
        zlib_mem* mem = new zlib_mem();

        if (sscanf(filename, "%x+%x", &mem->base, &mem->size) != 2)
        {
            // couldn't read base and size into struct from filename parameter
            return NULL;
        }

        return mem;
    }

    /**
     * Read function to use for zip archives in memory.
     */
    uLong ZCALLBACK MSFReader::freadMem(voidpf opaque, voidpf stream, void* buf, uLong size)
    {
        zlib_mem* mem = (zlib_mem*)stream;
        uLong toRead = (size <= mem->size - mem->cur_offset) ? size : mem->size - mem->cur_offset;
        memcpy(buf, mem->base + mem->cur_offset, toRead);
        mem->cur_offset += toRead;
        return toRead;
    }

    /**
     * Tell function to use for zip archives in memory.
     */
    long ZCALLBACK MSFReader::ftellMem(voidpf opaque, voidpf stream)
    {
        return ((zlib_mem*)stream)->cur_offset;
    }

    /**
     * Seek function to use for zip archives in memory.
     */
    long ZCALLBACK MSFReader::fseekMem(voidpf opaque, voidpf stream, uLong offset, int origin)
    {
        zlib_mem* mem = (zlib_mem*)stream;
        uLong newPos;
        switch (origin)
        {
        case ZLIB_FILEFUNC_SEEK_CUR:
            newPos = mem->cur_offset + offset;
            break;
        case ZLIB_FILEFUNC_SEEK_END:
            newPos = mem->size + offset;
            break;
        case ZLIB_FILEFUNC_SEEK_SET:
            newPos = offset;
            break;
        default:
            return -1;
        }

        if (newPos > mem->size)
        {
            return 1;
        }

        mem->cur_offset = newPos;
        return 0;
    }

    /**
     * Close function to use for zip archives in memory.
     */
    int ZCALLBACK MSFReader::fcloseMem(voidpf opaque, voidpf stream)
    {
        delete ((zlib_mem*)stream);
        return 0;
    }

    /**
     * Error function to use for zip archives in memory.
     */
    int ZCALLBACK MSFReader::ferrorMem(voidpf opaque, voidpf stream)
    {
        return 0;
    }

    /**
     * Prepares the given string as a SQLite query and verifies the return value.
     */
    sqlite3_stmt* MSFReader::getStmt(string query)
    {
        sqlite3_stmt* statement;
        if (sqlite3_prepare(msfFile_, query.c_str(), -1, &statement, NULL) != SQLITE_OK)
        {
            Verbosity::debug("SQLITE error message: %s", sqlite3_errmsg(msfFile_) );
            Verbosity::error("Cannot prepare SQL select statement for fetching "
                             "spectra from %s", msfName_);
        }
        return statement;
    }

    /**
     * Attempts to return the next row of the SQLite statement. Returns true on success.
     * Otherwise, finalize the statement and return false.
     */
    bool MSFReader::hasNext(sqlite3_stmt* statement)
    {
        if (sqlite3_step(statement) == SQLITE_ROW)
        {
            return true;
        }
        else
        {
            sqlite3_finalize(statement);
            return false;
        }
    }

    /**
     * Returns the number of records in the SQLite table.
     */
    int MSFReader::getRowCount(string table)
    {
        sqlite3_stmt* statement = getStmt("SELECT COUNT(*) FROM " + table);

        sqlite3_step(statement);
        int count = sqlite3_column_int(statement, 0);
        sqlite3_finalize(statement);

        return count;
    }

    // SpecFileReader methods
    /**
     * Implemented to satisfy SpecFileReader interface.  Since spec and
     * results files are the same, no need to open a new one.
     */
    void MSFReader::openFile(const char* filename, bool mzSort) {}

    void MSFReader::setIdType(SPEC_ID_TYPE type) {}

    /**
     * Return a spectrum via the returnData argument.  If not found in the
     * spectra map, return false and leave returnData unchanged.
     */
    bool MSFReader::getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks)
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

    /**
     * Only specific spectra can be accessed from the MSFReader.
     */
    bool MSFReader::getSpectrum(string identifier, SpecData& returnData, bool getPeaks)
    {
        Verbosity::warn("MSFReader cannot fetch spectra by string identifier, "
                        "only by spectrum index.");
        return false;
    }

    /**
     * Only specific spectra can be accessed from the MSFReader.
     */
    bool MSFReader::getNextSpectrum(SpecData& returnData, bool getPeaks)
    {
        Verbosity::warn("MSFReader does not support sequential file reading.");
        return false;
    }
}