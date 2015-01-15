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
        msfName_(msfFile), schemaVersion_(-1), filtered_(has_extension(msfFile, ".pdResult"))
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

        // Get the schema version
        sqlite3_stmt* statement = getStmt(
            "SELECT SoftwareVersion FROM SchemaInfo");
        if (hasNext(statement))
        {
            string version = lexical_cast<string>(sqlite3_column_text(statement, 0));
            sqlite3_finalize(statement);
            vector<string> versionPieces;
            boost::split(versionPieces, version, boost::is_any_of("."));
            try {
                schemaVersion_ = lexical_cast<int>(versionPieces.front());
                Verbosity::debug("Schema version is %d (%s)", schemaVersion_, version.c_str());
            } catch (bad_lexical_cast &) {
                Verbosity::error("Unknown schema version format: '%s'", version.c_str());
            }
        }
        if (schemaVersion_ < 0) {
            Verbosity::error("Could not determine schema version.");
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
        int specCount = !filtered_ ?
            getRowCount("SpectrumHeaders WHERE SpectrumID IN (SELECT DISTINCT SpectrumID FROM Peptides)") :
            getRowCount("MSnSpectrumInfo WHERE SpectrumID IN (SELECT DISTINCT MSnSpectrumInfoSpectrumID FROM TargetPsmsMSnSpectrumInfo)");
        Verbosity::status("Parsing %d spectra.", specCount);
        ProgressIndicator progress(specCount);

        sqlite3_stmt* statement = !filtered_ ?
            getStmt(
                "SELECT SpectrumID, RetentionTime, Mass, Charge, Spectrum "
                "FROM SpectrumHeaders "
                "JOIN Spectra ON SpectrumHeaders.UniqueSpectrumID = Spectra.UniqueSpectrumID "
                "WHERE SpectrumID IN (SELECT DISTINCT SpectrumID FROM Peptides)") :
            getStmt(
                "SELECT SpectrumID, RetentionTime, Mass, Charge, Spectrum "
                "FROM MSnSpectrumInfo "
                "JOIN MassSpectrumItems ON MSnSpectrumInfo.SpectrumID = MassSpectrumItems.ID "
                "WHERE SpectrumID IN (SELECT DISTINCT MSnSpectrumInfoSpectrumID FROM TargetPsmsMSnSpectrumInfo)");

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
        sqlite3_stmt* statement;
        int resultCount;
        map<int, double> alts; // peptide id --> alt score, for breaking ties when q-values are identical
        vector<string> altScoreNames;
        altScoreNames.push_back("XCorr");
        altScoreNames.push_back("IonScore");

        if (!filtered_)
        {
            for (vector<string>::const_iterator i = altScoreNames.begin(); i != altScoreNames.end(); i++) {
                statement = getStmt(
                    "SELECT PeptideID, ScoreValue "
                    "FROM PeptideScores JOIN ProcessingNodeScores ON PeptideScores.ScoreID = ProcessingNodeScores.ScoreID "
                    "WHERE ScoreName = '" + *i + "'");
                while (hasNext(statement)) {
                    alts[sqlite3_column_int(statement, 0)] = sqlite3_column_double(statement, 1);
                }
                if (!alts.empty()) {
                    break;
                }
            }
        }

        bool qValues = hasQValues();
        if (!qValues)
        {
            // no q-value fields in this database, error unless user wants everything
            if (getScoreThreshold(SQT) < 1)
                throw BlibException(false, "This file does not contain q-values. You can set "
                                    "a cut-off score of 0 in order to build a library from it, "
                                    "but this may cause your library to include a lot "
                                    "of false-positives.");
            resultCount = getRowCount("Peptides");
            statement = getStmt("SELECT PeptideID, SpectrumID, Sequence FROM Peptides");
        }
        else
        {
            // get peptides with q-value <= threshold
            if (!filtered_)
            {
                statement = getStmt(
                    "SELECT Peptides.PeptideID, SpectrumID, Sequence, FieldValue "
                    "FROM Peptides JOIN CustomDataPeptides ON Peptides.PeptideID = CustomDataPeptides.PeptideID "
                    "WHERE FieldID IN (SELECT FieldID FROM CustomDataFields WHERE DisplayName IN ('q-Value', 'Percolator q-Value')) "
                    "AND FieldValue <= " + lexical_cast<string>(getScoreThreshold(SQT)));
                resultCount = getRowCount(
                    "Peptides JOIN CustomDataPeptides ON Peptides.PeptideID = CustomDataPeptides.PeptideID "
                    "WHERE FieldID IN (SELECT FieldID FROM CustomDataFields WHERE DisplayName IN ('q-Value', 'Percolator q-Value')) "
                    "AND FieldValue <= " + lexical_cast<string>(getScoreThreshold(SQT)));
            }
            else
            {
                try
                {
                    statement = getStmt(
                        "SELECT PeptideID, MSnSpectrumInfoSpectrumID, Sequence, PercolatorqValue "
                        "FROM TargetPsms JOIN TargetPsmsMSnSpectrumInfo ON PeptideID = TargetPsmsPeptideID "
                        "WHERE PercolatorqValue <= " + lexical_cast<string>(getScoreThreshold(SQT)));
                    resultCount = getRowCount(
                        "TargetPsms JOIN TargetPsmsMSnSpectrumInfo ON PeptideID = TargetPsmsPeptideID "
                        "WHERE PercolatorqValue <= " + lexical_cast<string>(getScoreThreshold(SQT)));
                }
                catch (BlibException& e)
                {
                    statement = getStmt(
                        "SELECT PeptideID, MSnSpectrumInfoSpectrumID, Sequence, qValue "
                        "FROM TargetPsms JOIN TargetPsmsMSnSpectrumInfo ON PeptideID = TargetPsmsPeptideID "
                        "WHERE qValue <= " + lexical_cast<string>(getScoreThreshold(SQT)));
                    resultCount = getRowCount(
                        "TargetPsms JOIN TargetPsmsMSnSpectrumInfo ON PeptideID = TargetPsmsPeptideID "
                        "WHERE qValue <= " + lexical_cast<string>(getScoreThreshold(SQT)));
                }
            }
        }
        Verbosity::status("Parsing %d PSMs.", resultCount);
        ProgressIndicator progress(resultCount);

        initFileNameMap();
        map<int, ProcessedMsfSpectrum> processedSpectra;
        map< int, vector<SeqMod> > modMap = getMods();
        map<int, int> fileIdMap = getFileIds();

        // turn each row of returned table into a psm
        while (hasNext(statement))
        {
            int peptideId = sqlite3_column_int(statement, 0);
            int specId = sqlite3_column_int(statement, 1);
            string sequence = lexical_cast<string>(sqlite3_column_text(statement, 2));
            double qvalue = qValues ? sqlite3_column_double(statement, 3) : 0.0;

            map<int, double>::const_iterator altIter = alts.find(peptideId);
            double altScore = (altIter != alts.end()) ? altIter->second : -numeric_limits<double>::max();

            // check if we already processed a peptide that references this spectrum
            map<int, ProcessedMsfSpectrum>::iterator processedSpectraSearch = processedSpectra.find(specId);
            if (processedSpectraSearch != processedSpectra.end())
            {
                ProcessedMsfSpectrum& processed = processedSpectraSearch->second;
                // not an ambigous spectrum (yet)
                if (!processed.ambiguous)
                {
                    // worse than other score, skip this
                    if (qvalue > processed.qvalue || (qvalue == processed.qvalue && altScore < processed.altScore))
                    {
                        Verbosity::debug("Peptide %d (%s) had a worse score than another peptide (%s) "
                                         "referencing spectrum %d (ignoring this peptide).",
                                         peptideId, sequence.c_str(), processed.psm->unmodSeq.c_str(), specId);
                        continue;
                    }
                    // equal, discard other and skip this
                    else if (qvalue == processed.qvalue && altScore == processed.altScore)
                    {
                        Verbosity::debug("Peptide %d (%s) had the same score as another peptide (%s) "
                                         "referencing spectrum %d (ignoring both peptides).",
                                         peptideId, sequence.c_str(), processed.psm->unmodSeq.c_str(), specId);

                        removeFromFileMap(processed.psm);
                        delete processed.psm;

                        processed.psm = NULL;
                        processed.ambiguous = true;
                        continue;
                    }
                    // better than other score, discard other
                    else
                    {
                        Verbosity::debug("Peptide %d (%s) had a better score than another peptide (%s) "
                                         "referencing spectrum %d (ignoring other peptide).",
                                         peptideId, sequence.c_str(), processed.psm->unmodSeq.c_str(), specId);
                        removeFromFileMap(processed.psm);
                        curPSM_ = processed.psm;
                        curPSM_->mods.clear();
                        processed.qvalue = qvalue;
                        processed.altScore = altScore;
                    }
                }
                // ambigous spectrum, check if score is better
                else
                {
                    Verbosity::debug("Peptide %d (%s) with score %f references same spectrum as other peptides "
                                     "that had score %f.", peptideId, sequence.c_str(), qvalue, processed.qvalue);
                    if (qvalue < processed.qvalue || (qvalue == processed.qvalue && altScore > processed.altScore))
                    {
                        curPSM_ = new PSM();
                        processedSpectraSearch->second = ProcessedMsfSpectrum(curPSM_, qvalue, altScore);
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            else
            {
                // unseen spectrum
                curPSM_ = new PSM();
                processedSpectra[specId] = ProcessedMsfSpectrum(curPSM_, qvalue, altScore);
            }

            curPSM_->charge = spectraChargeStates_[specId];
            curPSM_->unmodSeq = sequence;
            map< int, vector<SeqMod> >::iterator modAccess = modMap.find(peptideId);
            if (modAccess != modMap.end())
            {
                curPSM_->mods = modAccess->second;
                modMap.erase(modAccess);
            }
            curPSM_->specIndex = specId;
            curPSM_->score = qvalue;

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
            PSM_SCORE_TYPE scoreType = PERCOLATOR_QVALUE;
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
    }

    /**
     * Initialize fileNameMap_, which maps FileID to their filenames.
     */
    void MSFReader::initFileNameMap()
    {
        string fileTable = (schemaVersion_ < 2) ? "FileInfos" : "WorkflowInputFiles";
        sqlite3_stmt* statement = getStmt(
            "SELECT FileID, FileName FROM " + fileTable);
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
     * Return whether the MSF file has q-values or not.
     */
    bool MSFReader::hasQValues()
    {
        if (filtered_)
        {
            return true;
        }

        sqlite3_stmt* statement = getStmt(
            "SELECT FieldID "
            "FROM CustomDataFields "
            "WHERE DisplayName IN ('q-Value', 'Percolator q-Value') "
            "LIMIT 1");
        if (!hasNext(statement))
        {
            return false;
        }
        sqlite3_finalize(statement);
        return true;
    }

    /**
     * Return a map that maps PeptideID to all SeqMods for that peptide.
     */
    map< int, vector<SeqMod> > MSFReader::getMods()
    {
        sqlite3_stmt* statement = !filtered_ ?
            getStmt(
                "SELECT PeptideID, Position, DeltaMass "
                "FROM PeptidesAminoAcidModifications "
                "JOIN AminoAcidModifications "
                "   ON PeptidesAminoAcidModifications.AminoAcidModificationID = AminoAcidModifications.AminoAcidModificationID") :
            getStmt(
                "SELECT TargetPsmsPeptideID, Position, DeltaMonoisotopicMass "
                "FROM TargetPsmsFoundModifications "
                "JOIN FoundModifications "
                "   ON TargetPsmsFoundModifications.FoundModificationsModificationID = FoundModifications.ModificationID");

        // turn each row of returned table into a seqmod to be added to the map
        map< int, vector<SeqMod> > modMap;
        map< int, vector<SeqMod> >::iterator found;
        int modCount = 0;
        while (hasNext(statement))
        {
            int peptideId = sqlite3_column_int(statement, 0);
            // mod indices are 0 based in unfiltered, 1 based in filtered
            int pos = sqlite3_column_int(statement, 1);
            if (!filtered_)
                ++pos;
            SeqMod mod(pos, sqlite3_column_double(statement, 2));
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
        sqlite3_stmt* statement = !filtered_ ?
            getStmt(
                "SELECT PeptideID, FileID "
                "FROM Peptides "
                "JOIN SpectrumHeaders ON Peptides.SpectrumID = SpectrumHeaders.SpectrumID "
                "JOIN MassPeaks ON SpectrumHeaders.MassPeakID = MassPeaks.MassPeakID") :
            getStmt(
                "SELECT PeptideID, FileID "
                "FROM TargetPsms "
                "JOIN WorkflowInputFiles ON TargetPsms.WorkflowID = WorkflowInputFiles.WorkflowID");

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
        // "<hex base of archive> <hex size of archive>"
        stringstream zipInfo;
        zipInfo << hex << reinterpret_cast<const void*&>(src) << " " << srcLen;

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

        istringstream zipInfo(filename);
        zipInfo >> hex >> reinterpret_cast<void*&>(mem->base) >> mem->size;
        
        if (!zipInfo)
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
            throw BlibException("Cannot prepare SQL statement for %s: %s ",
                                msfName_, sqlite3_errmsg(msfFile_));
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