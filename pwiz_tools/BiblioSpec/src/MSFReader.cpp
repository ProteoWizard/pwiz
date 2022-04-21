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
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"

namespace BiblioSpec
{
    MSFReader::MSFReader(BlibBuilder& maker, 
                         const char* msfFile, 
                         const ProgressIndicator* parent_progress)
        : BuildParser(maker, msfFile, parent_progress),
        msfName_(msfFile), schemaVersionMajor_(-1), schemaVersionMinor_(-1), filtered_(has_extension(msfFile, ".pdResult"))
    {
        setSpecFileName(msfFile, false);
        lookUpBy_ = NAME_ID;
        // point to self as spec reader
        delete specReader_;
        specReader_ = this;
    }

    MSFReader::~MSFReader()
    {
        sqlite3_close(msfFile_);

        specReader_ = NULL; // so the parent class doesn't try to delete itself
        // free spectra
        for (auto it = spectra_.begin(); it != spectra_.end(); ++it)
        {
            if (it->second != NULL)
            {
                delete it->second;
                it->second = NULL;
            }
        }
    }
    
    bool MSFReader::versionLess(int major, int minor) const {
        return schemaVersionMajor_ < major || (schemaVersionMajor_ == major && schemaVersionMinor_ < minor);
    }

    string MSFReader::uniqueSpecId(int specId, int workflowId) {
        if (workflowId != 0)
            return lexical_cast<string>(-workflowId) + "." + lexical_cast<string>(specId);
        else
            return lexical_cast<string>(specId);
    }

    void MSFReader::openFile() {
        sqlite3_open(msfName_, &msfFile_);
        if (!msfFile_) {
            throw BlibException(true, "Couldn't open '%s'.", msfName_);
        }
        // Get the schema version
        sqlite3_stmt* statement = getStmt("SELECT SoftwareVersion FROM SchemaInfo");
        if (hasNext(&statement)) {
            string version = lexical_cast<string>(sqlite3_column_text(statement, 0));
            sqlite3_finalize(statement);
            vector<string> versionPieces;
            boost::split(versionPieces, version, boost::is_any_of("."));
            try {
                schemaVersionMajor_ = lexical_cast<int>(versionPieces.front());
                if (versionPieces.size() > 1) {
                    schemaVersionMinor_ = lexical_cast<int>(versionPieces[1]);
                }
                Verbosity::debug("Schema version is %d (%s)", schemaVersionMajor_, version.c_str());
            } catch (bad_lexical_cast &) {
                Verbosity::error("Unknown schema version format: '%s'", version.c_str());
            }
        }
        if (schemaVersionMajor_ < 0) {
            Verbosity::error("Could not determine schema version.");
        }
    }

    bool MSFReader::parseFile() {
        openFile();

        if (!hasQValues() && getScoreThreshold(SQT) < 1) {
            // no q-value fields in this database, error unless user wants everything
            throw BlibException(false, "This file does not contain q-values. You can set "
                                       "a cut-off score of 0 in order to build a library from it, "
                                       "but this may cause your library to include a lot "
                                       "of false-positives.");
        }

        collectSpectra();
        collectPsms();

        // add psms by filename
        for (auto iter = fileMap_.begin();
             iter != fileMap_.end();
             ++iter)
        {
            for (auto scoreIter = iter->second.begin();
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

    vector<PSM_SCORE_TYPE> MSFReader::getScoreTypes() {
        openFile();
        int pepConfidence, protConfidence;
        PSM_SCORE_TYPE scoreType;
        getScoreInfo(NULL, NULL, &scoreType, &pepConfidence, &protConfidence);
        return vector<PSM_SCORE_TYPE>(1, scoreType);
    }

    /**
     * Parse all spectra.
     */
    void MSFReader::collectSpectra()
    {
        int specCount = 0;
        sqlite3_stmt* statement = NULL; // id, rt, mass, charge, peaks
        bool hasCompensationVoltage = false;

        if (filtered_ || !versionLess(2, 2)) { // < 2.2 and filtered, or 2.2+
            specCount = getRowCount("MSnSpectrumInfo WHERE SpectrumID IN (SELECT DISTINCT MSnSpectrumInfoSpectrumID FROM TargetPsmsMSnSpectrumInfo)");
            hasCompensationVoltage = columnExists(msfFile_, "MSnSpectrumInfo", "CompVoltageV");
            statement = getStmt(
                std::string("SELECT SpectrumID, MSnSpectrumInfo.RetentionTime, Mass, Charge, Spectrum, MSnSpectrumInfo.WorkflowID") +
                (hasCompensationVoltage ? ", CompVoltageV " : " ") +
                "FROM MSnSpectrumInfo "
                "JOIN MassSpectrumItems ON MSnSpectrumInfo.SpectrumID = MassSpectrumItems.ID AND MSnSpectrumInfo.WorkflowID = MassSpectrumItems.WorkflowID");
        } else {
            specCount = getRowCount("SpectrumHeaders WHERE SpectrumID IN (SELECT DISTINCT SpectrumID FROM Peptides)");
            statement = getStmt(
                "SELECT SpectrumID, RetentionTime, Mass, Charge, Spectrum, 0 "
                "FROM SpectrumHeaders "
                "JOIN Spectra ON SpectrumHeaders.UniqueSpectrumID = Spectra.UniqueSpectrumID "
                "WHERE SpectrumID IN (SELECT DISTINCT SpectrumID FROM Peptides)");
        }

        Verbosity::status("Parsing %d spectra.", specCount);
        ProgressIndicator progress(specCount);

        // turn each row of returned table into a spectrum
        while (hasNext(&statement))
        {
            string specId = uniqueSpecId(sqlite3_column_int(statement, 0), sqlite3_column_int(statement, 5));
            double mass = sqlite3_column_double(statement, 2);

            SpecData* specData = new SpecData();
            specData->id = sqlite3_column_int(statement, 0);
            specData->retentionTime = sqlite3_column_double(statement, 1);
            specData->charge = sqlite3_column_int(statement, 3);
            specData->mz = (mass + (PROTON_MASS * specData->charge)) / specData->charge;
            if (hasCompensationVoltage)
            {
                specData->ionMobilityType = IONMOBILITY_COMPENSATION_V;
                specData->ionMobility = sqlite3_column_double(statement, 6);
            }

            // unzip spectrum xml file
            const void* zippedSpectrumPtr = sqlite3_column_blob(statement, 4);
            size_t zippedSpectrumLen = sqlite3_column_bytes(statement, 4);
            string spectrumXml = unzipSpectrum(specId, zippedSpectrumPtr, zippedSpectrumLen);
            // read peak data from spectrum xml file
            readSpectrum(specId, spectrumXml, &(specData->numPeaks), &(specData->mzs), &(specData->intensities));

            // add spectrum to map
            spectra_[specId] = specData;

            progress.increment();
        }

        Verbosity::debug("Map has %d spectra", spectra_.size());
    }

    /**
     * Given a sequence of bytes and its length, assume that it is a zip archive containing one file.
     * Unzip the file and return its contents as a string.
     */
    string MSFReader::unzipSpectrum(const string& specId, const void* src, size_t srcLen)
    {
        // try to open zip archive 
        unzFile zipFile = openMemZip(src, srcLen);
        if (zipFile == NULL)
        {
            throw BlibException(false, "Could not open compressed spectrum %s.", specId.c_str());
        }

        // get zip file info
        unz_global_info globalInfo;
        if (unzGetGlobalInfo(zipFile, &globalInfo) != UNZ_OK ||
            globalInfo.number_entry != 1)
        {
            unzClose(zipFile);
            throw BlibException(false, "Compressed spectrum %s has invalid format.", specId.c_str());
        }

        // get spectrum file info
        unz_file_info fileInfo;
        char specFile[MAX_FILENAME];
        if (unzGetCurrentFileInfo(zipFile, &fileInfo, specFile, MAX_FILENAME, NULL, 0, NULL, 0) != UNZ_OK)
        {
            unzClose(zipFile);
            throw BlibException(false, "Could not read info for compressed spectrum file '%s' "
                                       "in compressed spectrum %s.", specFile, specId.c_str());
        }

        // open spectrum file
        if (unzOpenCurrentFile(zipFile) != UNZ_OK)
        {
            unzClose(zipFile);
            throw BlibException(false, "Could not open compressed spectrum file '%s' "
                                       "in compressed spectrum %s.", specFile, specId.c_str());
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
                                           "from compressed spectrum %s.", error, specFile, specId.c_str());
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
    void MSFReader::readSpectrum(const string& specId, string& spectrumXml,
                                 int* numPeaks, double** mzs, float** intensities)
    {
        vector<double> mzVector;
        vector<float> intensityVector;
        MSFSpecReader specReader(spectrumXml, &mzVector, &intensityVector);
        try
        {
            specReader.parse();
            *numPeaks = mzVector.size();
            Verbosity::comment(V_DETAIL, "Done parsing spectrum XML from spectrum %s, %d peaks found",
                               specId.c_str(), numPeaks);
            *mzs = new double[*numPeaks];
            *intensities = new float[*numPeaks];
            copy(mzVector.begin(), mzVector.end(), *mzs);
            copy(intensityVector.begin(), intensityVector.end(), *intensities);
        }
        catch (BlibException& e)
        {
            throw BlibException(false, "Error parsing spectrum XML from spectrum %s: %s",
                                       specId.c_str(), e.what());
        }
        catch (...)
        {
            throw BlibException(false, "Unknown error while parsing spectrum file %s.", specId.c_str());
        }
    }
    
    void MSFReader::collectPsms() {
        sqlite3_stmt* statement;
        map<int, double> alts; // peptide id --> alt score, for breaking ties when q-values are identical
        vector<string> altScoreNames;
        altScoreNames.push_back("XCorr");
        altScoreNames.push_back("IonScore");

        if (tableExists(msfFile_, "TargetPsms")) {
            for (vector<string>::const_iterator i = altScoreNames.begin(); i != altScoreNames.end(); ++i) {
                if (!columnExists(msfFile_, "TargetPsms", *i)) {
                    continue;
                }
                statement = getStmt("SELECT PeptideID, " + *i + " FROM TargetPsms");
                while (hasNext(&statement)) {
                    alts[sqlite3_column_int(statement, 0)] = sqlite3_column_double(statement, 1);
                }
                break;
            }
        } else if (tableExists(msfFile_, "PeptideScores") && tableExists(msfFile_, "ProcessingNodeScores")) {
            for (vector<string>::const_iterator i = altScoreNames.begin(); i != altScoreNames.end(); ++i) {
                statement = getStmt(
                    "SELECT PeptideID, ScoreValue "
                    "FROM PeptideScores JOIN ProcessingNodeScores ON PeptideScores.ScoreID = ProcessingNodeScores.ScoreID "
                    "WHERE ScoreName = '" + *i + "'");
                while (hasNext(&statement)) {
                    alts[sqlite3_column_int(statement, 0)] = sqlite3_column_double(statement, 1);
                }
                if (!alts.empty()) {
                    break;
                }
            }
        }

        int resultCount, pepConfidence, protConfidence;
        PSM_SCORE_TYPE scoreType;
        getScoreInfo(&statement, &resultCount, &scoreType, &pepConfidence, &protConfidence);

        Verbosity::status("Parsing %d PSMs.", resultCount);
        ProgressIndicator progress(resultCount);

        initFileNameMap();
        map<string, ProcessedMsfSpectrum> processedSpectra;
        ModSet modSet = ModSet(msfFile_, !versionLess(2, 2) || filtered_);
        map<int, int> fileIdMap = getFileIds();

        // turn each row of returned table into a psm
        while (hasNext(&statement)) {
            if (protConfidence > 0) {
                const unsigned char* tmpConf = static_cast<const unsigned char*>(sqlite3_column_blob(statement, 6));
                int tmpConfLen = sqlite3_column_bytes(statement, 6);
                if (tmpConfLen == 0) {
                    continue; // null
                } else if (tmpConfLen % 5 > 0) {
                    Verbosity::error("expected protein confidence to be multiple of 5 bytes but was %d", tmpConfLen);
                    continue;
                }
                int maxConf = -1;
                int tmpConfInt;
                for (int i = 0; i < tmpConfLen; i += 5) {
                    if (tmpConf[i + 4] > 0) { // last byte indicates used or not
                        memcpy(&tmpConfInt, tmpConf + i, 4);
                        if (tmpConfInt > maxConf) {
                            maxConf = tmpConfInt;
                        }
                    }
                }
                if (maxConf < protConfidence) {
                    continue;
                }
            }
            int peptideId = sqlite3_column_int(statement, 0);
            string specId = uniqueSpecId(sqlite3_column_int(statement, 1), sqlite3_column_int(statement, 4));
            string sequence = lexical_cast<string>(sqlite3_column_text(statement, 2));
            double qvalue = pepConfidence <= 0 ? sqlite3_column_double(statement, 3) : 0;

            auto findItr = spectra_.find(specId);
            if (findItr == spectra_.end()) {
                Verbosity::warn("Peptide %d (%s) with score %f has a spectrum id (%s) not present in the spectrum map.", peptideId, sequence.c_str(), qvalue, specId.c_str());
                continue;
            }

            auto altIter = alts.find(peptideId);    
            double altScore = (altIter != alts.end()) ? altIter->second : -std::numeric_limits<double>::max();

            // check if we already processed a peptide that references this spectrum
            auto processedSpectraSearch = processedSpectra.find(specId);
            if (processedSpectraSearch != processedSpectra.end()) {
                ProcessedMsfSpectrum& processed = processedSpectraSearch->second;
                // not an ambigous spectrum (yet)
                if (!processed.ambiguous) {
                    if (qvalue > processed.qvalue || (qvalue == processed.qvalue && altScore < processed.altScore)) { // worse than other score, skip this
                        Verbosity::debug("Peptide %d (%s) had a worse score than another peptide (%s) "
                                         "referencing spectrum %d (ignoring this peptide).",
                                         peptideId, sequence.c_str(), processed.psm->unmodSeq.c_str(), specId.c_str());
                        continue;
                    } else if (qvalue == processed.qvalue && altScore == processed.altScore) { // equal, discard other and skip this
                        Verbosity::debug("Peptide %d (%s) had the same score as another peptide (%s) "
                                         "referencing spectrum %d (ignoring both peptides).",
                                         peptideId, sequence.c_str(), processed.psm->unmodSeq.c_str(), specId.c_str());

                        removeFromFileMap(processed.psm);
                        delete processed.psm;

                        processed.psm = NULL;
                        processed.ambiguous = true;
                        continue;
                    } else { // better than other score, discard other
                        Verbosity::debug("Peptide %d (%s) had a better score than another peptide (%s) "
                                         "referencing spectrum %d (ignoring other peptide).",
                                         peptideId, sequence.c_str(), processed.psm->unmodSeq.c_str(), specId.c_str());
                        removeFromFileMap(processed.psm);
                        curPSM_ = processed.psm;
                        curPSM_->mods.clear();
                        processed.qvalue = qvalue;
                        processed.altScore = altScore;
                    }
                } else { // ambigous spectrum, check if score is better
                    Verbosity::debug("Peptide %d (%s) with score %f references same spectrum as other peptides "
                                     "that had score %f.", peptideId, sequence.c_str(), qvalue, processed.qvalue);
                    if (qvalue < processed.qvalue || (qvalue == processed.qvalue && altScore > processed.altScore)) {
                        curPSM_ = new PSM();
                        processedSpectraSearch->second = ProcessedMsfSpectrum(curPSM_, qvalue, altScore);
                    } else {
                        continue;
                    }
                }
            } else {
                // unseen spectrum
                curPSM_ = new PSM();
                processedSpectra[specId] = ProcessedMsfSpectrum(curPSM_, qvalue, altScore);
            }

            curPSM_->charge = findItr->second->charge;
            curPSM_->unmodSeq = sequence;
            curPSM_->mods = versionLess(2, 2) && !filtered_
                ? modSet.getMods(peptideId)
                : modSet.getMods(sqlite3_column_int(statement, 4), peptideId);
            curPSM_->specIndex = findItr->second->id;
            curPSM_->specName = specId;
            curPSM_->score = qvalue;

            string psmFileName;
            if (!filtered_ && versionLess(2, 2)) {
                auto fileIdMapAccess = fileIdMap.find(peptideId);
                if (fileIdMapAccess == fileIdMap.end()) {
                    throw BlibException(false, "No FileID for PSM %d.", peptideId);
                }
                psmFileName = fileIdToName(fileIdMapAccess->second);
                fileIdMap.erase(fileIdMapAccess);
            } else {
                psmFileName = lexical_cast<string>(sqlite3_column_text(statement, 5));
            }

            // filename
            auto fileMapAccess = fileMap_.find(psmFileName);
            if (fileMapAccess == fileMap_.end()) {
                map< PSM_SCORE_TYPE, vector<PSM*> > tmpMap;
                fileMap_[psmFileName] = tmpMap;
                fileMapAccess = fileMap_.find(psmFileName);
            }

            // score
            map< PSM_SCORE_TYPE, vector<PSM*> >& scoreMap = fileMapAccess->second;
            auto scoreMapAccess = scoreMap.find(scoreType);
            if (scoreMapAccess == scoreMap.end()) {
                vector<PSM*> tmpVec;
                tmpVec.push_back(curPSM_);
                scoreMap[scoreType] = tmpVec;
            } else {
                scoreMapAccess->second.push_back(curPSM_);
            }

            progress.increment();
        }
    }

    /**
     * set result count and statement (PeptideID, SpectrumID, unmodified sequence, q-value[, WorkflowID, SpectrumFileName])
     */
    void MSFReader::getScoreInfo(sqlite3_stmt** outStmt, int* outResultCount, PSM_SCORE_TYPE* outScoreType,
        int* outPepConfidence, int* outProtConfidence) {
        *outScoreType = PERCOLATOR_QVALUE;
        *outPepConfidence = -1;
        *outProtConfidence = -1;

        string stmtStr;
        string countStr;
        if (!filtered_ && versionLess(2, 2)) {
            if (!hasQValues()) {
                stmtStr = "SELECT PeptideID, SpectrumID, Sequence, '0' FROM Peptides";
                countStr = "Peptides";
            } else {
                stmtStr =
                    "SELECT Peptides.PeptideID, SpectrumID, Sequence, FieldValue, 0 "
                    "FROM Peptides JOIN CustomDataPeptides ON Peptides.PeptideID = CustomDataPeptides.PeptideID "
                    "WHERE FieldID IN (SELECT FieldID FROM CustomDataFields WHERE DisplayName IN ('q-Value', 'Percolator q-Value')) "
                    "AND FieldValue <= " + lexical_cast<string>(getScoreThreshold(SQT));
                countStr =
                    "Peptides JOIN CustomDataPeptides ON Peptides.PeptideID = CustomDataPeptides.PeptideID "
                    "WHERE FieldID IN (SELECT FieldID FROM CustomDataFields WHERE DisplayName IN ('q-Value', 'Percolator q-Value')) "
                    "AND FieldValue <= " + lexical_cast<string>(getScoreThreshold(SQT));
            }
            if (outStmt != NULL) {
                *outStmt = getStmt(stmtStr);
            }
            if (outResultCount != NULL) {
                *outResultCount = getRowCount(countStr);
            }
            return;
        }

        string pepPsmTable = "";
        if (tableExists(msfFile_, "TargetPeptideGroupsTargetPsms")) {
            pepPsmTable = "TargetPeptideGroupsTargetPsms";
        } else if (tableExists(msfFile_, "TargetPsmsTargetPeptideGroups")) {
            pepPsmTable = "TargetPsmsTargetPeptideGroups";
        }

        bool peptideGroups = false;
        bool proteins = false;
        const bool useProtConfidence = false;
        if (columnExists(msfFile_, "TargetPeptideGroups", "Confidence") && !pepPsmTable.empty()) {
            // Confidence levels correspond to whatever the user selected.
            // But by default, 3 = High (<= 0.01), 2 = Medium (<= 0.05), 1 = Low (> 0.05).
            double threshold = getScoreThreshold(SQT);
            if (std::abs(threshold - 0.01) < 0.001) {
                *outPepConfidence = 3;
            } else if (std::abs(threshold - 0.05) < 0.001) {
                *outPepConfidence = 2;
            }
            if (useProtConfidence && columnExists(msfFile_, "TargetProteins", "ProteinFDRConfidence") &&
                tableExists(msfFile_, "TargetPeptideGroupsTargetProteins")) {
                *outProtConfidence = *outPepConfidence;
            }
        }

        string qValueCol;
        string qValueWhere;
        if (!hasQValues()) {
            qValueCol = "'0'";
        } else {
            if (*outPepConfidence > 0) {
                peptideGroups = true;
                qValueCol = "peps.Confidence";
                if (*outProtConfidence > 0) {
                    proteins = true;
                }
            } else if (columnExists(msfFile_, "TargetPeptideGroups", "Qvalityqvalue")) {
                peptideGroups = true;
                qValueCol = "peps.Qvalityqvalue";
            } else if (columnExists(msfFile_, "TargetPsms", "PercolatorqValue")) {
                qValueCol = "psms.PercolatorqValue";
            } else if (columnExists(msfFile_, "TargetPsms", "qValue")) {
                qValueCol = "psms.qValue";
            } else if (columnExists(msfFile_, "TargetPsms", "ExpectationValue")) {
                qValueCol = "psms.ExpectationValue";
                *outScoreType = MASCOT_IONS_SCORE;
            }
            qValueWhere = (*outPepConfidence <= 0)
                ? " WHERE " + qValueCol + " <= " + lexical_cast<string>(getScoreThreshold(SQT))
                : " WHERE " + qValueCol + " >= " + lexical_cast<string>(*outPepConfidence);
        }
        stmtStr =
            "SELECT psms.PeptideID, psm_spec.MSnSpectrumInfoSpectrumID, psms.Sequence, " +
                qValueCol + ", psms.WorkflowID, psms.SpectrumFileName" + (*outProtConfidence > 0 ? ", prots.ProteinFDRConfidence" : "") +
            " FROM TargetPsms psms"
            " JOIN TargetPsmsMSnSpectrumInfo psm_spec ON psms.PeptideID = psm_spec.TargetPsmsPeptideID"
            "   AND psm_spec.TargetPsmsWorkflowID = psms.WorkflowID";
        countStr =
            "TargetPsms psms"
            " JOIN TargetPsmsMSnSpectrumInfo psm_spec ON psms.PeptideID = psm_spec.TargetPsmsPeptideID"
            "   AND psm_spec.TargetPsmsWorkflowID = psms.WorkflowID";
        if (peptideGroups) {
            string joins =
                " JOIN " + pepPsmTable + " psm_pep ON psms.PeptideID = psm_pep.TargetPsmsPeptideID"
                " JOIN TargetPeptideGroups peps ON psm_pep.TargetPeptideGroupsPeptideGroupID = peps.PeptideGroupID";
            if (proteins) {
                joins +=
                    " JOIN TargetPeptideGroupsTargetProteins pep_prot ON peps.PeptideGroupID = pep_prot.TargetPeptideGroupsPeptideGroupID"
                    " JOIN TargetProteins prots ON pep_prot.TargetProteinsUniqueSequenceID = prots.UniqueSequenceID";
            }
            stmtStr += joins;
            countStr += joins;
        }
        stmtStr += qValueWhere;
        countStr += qValueWhere;
        if (outStmt != NULL) {
            *outStmt = getStmt(stmtStr);
        }
        if (outResultCount != NULL) {
            *outResultCount = getRowCount(countStr);
        }
    }

    /**
     * Initialize fileNameMap_, which maps FileID to their filenames.
     */
    void MSFReader::initFileNameMap() {
        if (!versionLess(2, 2)) {
            return; // don't need to do this since info is already in TargetPsms table
        }
        string fileTable = (schemaVersionMajor_ < 2) ? "FileInfos" : "WorkflowInputFiles";
        sqlite3_stmt* statement = getStmt(
            "SELECT FileID, FileName FROM " + fileTable);
        while (hasNext(&statement))
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
        for (auto iter = fileMap_.begin();
             iter != fileMap_.end();
             ++iter)
        {
            for (auto scoreIter = iter->second.begin();
                 scoreIter != iter->second.end();
                 ++scoreIter)
            {
                for (auto psmIter = scoreIter->second.begin();
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
    string MSFReader::fileIdToName(int fileId) {
        auto mapAccess = fileNameMap_.find(fileId);
        if (mapAccess == fileNameMap_.end()) {
            throw BlibException(false, "Invalid FileID: %d.", fileId);
        }
        return mapAccess->second;
    }

    /**
     * Return whether the MSF file has q-values or not.
     */
    bool MSFReader::hasQValues() {
        sqlite3_stmt* statement;

        if (filtered_ || !versionLess(2, 2)) {
            return columnExists(msfFile_, "TargetPeptideGroups", "Qvalityqvalue") ||
                   columnExists(msfFile_, "TargetPsms", "PercolatorqValue") ||
                   columnExists(msfFile_, "TargetPsms", "qValue") ||
                   columnExists(msfFile_, "TargetPsms", "ExpectationValue");
        } else if (!tableExists(msfFile_, "CustomDataFields")) {
            return false;
        }

         statement = getStmt(
            "SELECT FieldID "
            "FROM CustomDataFields "
            "WHERE DisplayName IN ('q-Value', 'Percolator q-Value') "
            "LIMIT 1");
        if (!hasNext(&statement)) {
            return false;
        }
        sqlite3_finalize(statement);
        return true;
    }

    MSFReader::ModSet::ModSet(sqlite3* db, bool filtered) {
        sqlite3_stmt* stmt;
        if (!filtered) {
            stmt = MSFReader::getStmt(db,
                "SELECT '0', PeptideID, Position, DeltaMass "
                "FROM PeptidesAminoAcidModifications "
                "JOIN AminoAcidModifications "
                "   ON PeptidesAminoAcidModifications.AminoAcidModificationID = AminoAcidModifications.AminoAcidModificationID");
        } else {
            bool alternateTName = MSFReader::tableExists(db, "FoundModificationsTargetPsms");
            string tName = !alternateTName ? "TargetPsmsFoundModifications" : "FoundModificationsTargetPsms";
            stmt = MSFReader::getStmt(db,
                "SELECT TargetPsmsWorkflowID, TargetPsmsPeptideID, Position, DeltaMonoisotopicMass "
                "FROM " + tName + " "
                "JOIN FoundModifications "
                "   ON " + tName + ".FoundModificationsModificationID = FoundModifications.ModificationID");
        }

        // turn each row of returned table into a seqmod to be added to the map
        while (MSFReader::hasNext(&stmt)) {
            int workflowId = sqlite3_column_int(stmt, 0);
            int peptideId = sqlite3_column_int(stmt, 1);
            // mod indices are 0 based in unfiltered, 1 based in filtered
            int pos = sqlite3_column_int(stmt, 2);
            if (!filtered) {
                ++pos;
            }
            addMod(workflowId, peptideId, pos, sqlite3_column_double(stmt, 3));
        }

        // get terminal mods if PeptidesTerminalModifications table exists
        if (MSFReader::tableExists(db, "PeptidesTerminalModifications")) {
            stmt = MSFReader::getStmt(db,
                "SELECT PeptidesTerminalModifications.PeptideID, PositionType, DeltaMass, Sequence "
                "FROM PeptidesTerminalModifications "
                "JOIN Peptides ON PeptidesTerminalModifications.PeptideID = Peptides.PeptideID "
                "JOIN AminoAcidModifications ON TerminalModificationID = AminoAcidModificationID");

            // turn each row of returned table into a seqmod to be added to the map
            while (MSFReader::hasNext(&stmt)) {
                int peptideId = sqlite3_column_int(stmt, 0);
                int positionType = sqlite3_column_int(stmt, 1);
                int position;
                switch (positionType) {
                case 1:
                case 3:
                    position = 1;
                    break;
                case 2:
                case 4:
                    position = strlen((const char*)sqlite3_column_text(stmt, 3));
                    break;
                default:
                    throw BlibException(false, "Unknown position type in PeptideAminoAcidModifications "
                                        "for PeptideID %d", peptideId);
                }
                addMod(0, peptideId, position, sqlite3_column_double(stmt, 2));
            }
        }
    }

    const vector<SeqMod>& MSFReader::ModSet::getMods(int peptideId) {
        return getMods(0, peptideId);
    }

    const vector<SeqMod>& MSFReader::ModSet::getMods(int workflowId, int peptideId) {
        auto i = mods_.find(workflowId);
        if (i != mods_.end()) {
            const auto& workflowMap = i->second;
            auto j = workflowMap.find(peptideId);
            if (j != workflowMap.end()) {
                return j->second;
            }
        }
        return dummy_;
    }

    map< int, vector<SeqMod> >& MSFReader::ModSet::getWorkflowMap(int workflowId) {
        auto i = mods_.find(workflowId);
        if (i == mods_.end()) {
            pair< int, map< int, vector<SeqMod> > > toInsert(workflowId, map< int, vector<SeqMod> >());
            i = mods_.insert(toInsert).first;
        }
        return i->second;
    }

    void MSFReader::ModSet::addMod(int workflowId, int peptideId, int position, double mass) {
        SeqMod mod(position, mass);
        auto& workflowMap = getWorkflowMap(workflowId);
        auto peptideMap = workflowMap.find(peptideId);
        if (peptideMap == workflowMap.end()) {
            workflowMap[peptideId] = vector<SeqMod>(1, mod);
        } else {
            workflowMap[peptideId].push_back(mod);
        }
    }

    /**
     * Return a map that maps PeptideID to the FileID.
     */
    map<int, int> MSFReader::getFileIds() {
        map<int, int> fileIdMap;
        if (!filtered_ && versionLess(2, 2)) {
            sqlite3_stmt* statement = getStmt(
                "SELECT PeptideID, FileID "
                "FROM Peptides "
                "JOIN SpectrumHeaders ON Peptides.SpectrumID = SpectrumHeaders.SpectrumID "
                "JOIN MassPeaks ON SpectrumHeaders.MassPeakID = MassPeaks.MassPeakID");

            // process each row of the returned table
            while (hasNext(&statement))
            {
                int peptideId = sqlite3_column_int(statement, 0);
                int fileId = sqlite3_column_int(statement, 1);
                fileIdMap[peptideId] = fileId;
            }
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
    sqlite3_stmt* MSFReader::getStmt(const string& query) {
        return getStmt(msfFile_, query);
    }
    
    sqlite3_stmt* MSFReader::getStmt(sqlite3* handle, const string& query) {
        sqlite3_stmt* statement;
        if (sqlite3_prepare(handle, query.c_str(), -1, &statement, NULL) != SQLITE_OK) {
            throw BlibException("Cannot prepare SQL statement: %s ", sqlite3_errmsg(handle));
        }
        return statement;
    }

    /**
     * Attempts to return the next row of the SQLite statement. Returns true on success.
     * Otherwise, finalize the statement and return false.
     */
    bool MSFReader::hasNext(sqlite3_stmt** statement) {
        if (sqlite3_step(*statement) != SQLITE_ROW) {
            sqlite3_finalize(*statement);
            *statement = NULL;
            return false;
        }
        return true;
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

    bool MSFReader::tableExists(sqlite3* handle, string table) {
        sqlite3_stmt* statement = getStmt(handle, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '" + table + "'");
        bool found = hasNext(&statement) && sqlite3_column_int(statement, 0) == 1;
        sqlite3_finalize(statement);
        return found;
    }

    bool MSFReader::columnExists(sqlite3* handle, string table, string columnName) {
        sqlite3_stmt* statement = getStmt(handle, "PRAGMA table_info(" + table + ")");
        int numCols = sqlite3_column_count(statement);
        int nameIndex = -1;
        for (int i = 0; i < numCols; i++) {
            string cur(sqlite3_column_name(statement, i));
            if (boost::iequals(cur, "name")) {
                nameIndex = i;
                break;
            }
        }

        bool found = false;
        set<string> foundCols;
        while (hasNext(&statement)) {
            string cur = lexical_cast<string>(sqlite3_column_text(statement, nameIndex));
            if (boost::iequals(cur, columnName)) {
                found = true;
                break;
            }
            foundCols.insert(cur);
        }
        sqlite3_finalize(statement);

        if (found) {
            Verbosity::debug("Searched in table '%s' for column '%s', found",
                             table.c_str(), columnName.c_str());
        } else {
            string foundColsJoined = boost::algorithm::join(foundCols, ", ");
            Verbosity::debug("Searched in table '%s' for column '%s', not found",
                             table.c_str(), columnName.c_str(), foundColsJoined.c_str());
        }

        return found;
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
        Verbosity::warn("MSFReader does not support spectrum access by integer identifier.");
        return false;
    }

    /**
     * Only specific spectra can be accessed from the MSFReader.
     */
    bool MSFReader::getSpectrum(string identifier, SpecData& returnData, bool getPeaks)
    {
        auto found = spectra_.find(identifier);
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
    bool MSFReader::getNextSpectrum(SpecData& returnData, bool getPeaks)
    {
        Verbosity::warn("MSFReader does not support sequential file reading.");
        return false;
    }
}
