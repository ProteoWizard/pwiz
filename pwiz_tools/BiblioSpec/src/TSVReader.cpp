//
// Original author: Kaipo Tamura <kaipot@uw.edu>
//
// Copyright 2018 University of Washington - Seattle, WA 98195
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
 * The TSVReader parses the PSMs from the tab delimited file
 * and stores each record. Records are grouped by file. Spectra are then
 * retrieved from the spectrum files. 
 */

#include "TSVReader.h"
#include "boost/smart_ptr/make_shared.hpp"

using namespace boost;

namespace BiblioSpec {

    bool operator<(const TSVColumnTranslator& left, const TSVColumnTranslator& right) {
        return left.position_ < right.position_;
    }

namespace {

    class OpenSwathResultReader : public TSVReader
    {
        public:
        static constexpr const TSVColumnTranslator requiredColumns[] =
        {
            {"filename", -1, TSVLine::insertFilename},
            {"RT", -1, TSVLine::insertRt},
            {"FullPeptideName", -1, TSVLine::insertSequence},
            {"Charge", -1, TSVLine::insertCharge},
            {"m/z", -1, TSVLine::insertMz},
            {"decoy", -1, TSVLine::insertDecoy},
            {"aggr_Peak_Area", -1, TSVLine::insertPeakArea},
            {"aggr_Fragment_Annotation", -1, TSVLine::insertFragmentAnnotation},
            {"m_score", -1, TSVLine::insertScore}
        };

        static constexpr const TSVColumnTranslator optionalColumns[] =
        {
            {"ProteinName", -1, TSVLine::insertProteinName},
            {"leftWidth", -1, TSVLine::insertLeftWidth},
            {"rightWidth", -1, TSVLine::insertRightWidth}
        };

        OpenSwathResultReader(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress)
            : TSVReader(maker, tsvName, parentProgress)
        {
            for (const auto& requiredColumn : requiredColumns)
                targetColumns_.push_back(requiredColumn);
            for (const auto& optionalColumn : optionalColumns)
                optionalColumns_.push_back(optionalColumn);

            string line;
            getlinePortable(tsvFile_, line);
            LineParser headerLine(line, separator_);
            parseHeader(headerLine, targetColumns_, optionalColumns_);

            // check that all required columns were in the file
            for (vector<TSVColumnTranslator>::iterator i = targetColumns_.begin(); i != targetColumns_.end(); ++i) {
                if (i->position_ < 0) {
                    if (!iequals(i->name_, "m_score")) {
                        throw BlibException(false, "Did not find required column '%s'. Only OpenSWATH .tsv files are supported.", i->name_);
                    }
                    else {
                        if (scoreThreshold_ < 1) {
                            throw BlibException(false,
                                "Did not find required column '%s'. You may set the cut-off score to 0 to "
                                "force building the library without scores.", i->name_);
                        }
                        else {
                            vector<TSVColumnTranslator>::iterator tmp = i - 1;
                            targetColumns_.erase(i);
                            i = tmp;
                        }
                    }
                }
            }
        }

        static bool hasExpectedColumns(LineParser& headerLine)
        {
            set<string> headerColumns(headerLine.begin(), headerLine.end());
            for (const auto& requiredColumn : requiredColumns)
                if (headerColumns.find(requiredColumn.name_) == headerColumns.end())
                    return false;
            return true;
        }

        bool parseFile()
        {
            if (!tsvFile_)
                return false;

            Verbosity::debug("Collecting PSMs");
            map<string, Protein> proteins;
            collectPsms(proteins);

            Verbosity::debug("Building tables");
            initSpecFileProgress(fileMap_.size());
            for (map< string, vector<TSVPSM*> >::iterator i = fileMap_.begin(); i != fileMap_.end(); ++i) {
                psms_.assign(i->second.begin(), i->second.end());
                setSpecFileName(i->first.c_str(), false);
                buildTables(GENERIC_QVALUE, i->first, false);
            }

            return true;
        }

        vector<PSM_SCORE_TYPE> getScoreTypes() {
            return vector<PSM_SCORE_TYPE>(1, GENERIC_QVALUE);
        }

        private:
        void storeLine(const TSVLine& line, std::map<std::string, Protein>& proteins)
        {
            if (line.decoy) {
                Verbosity::comment(V_DETAIL, "Not saving decoy PSM (line %d)", lineNum_);
                return;
            }
            else if (line.score > scoreThreshold_) {
                Verbosity::comment(V_DETAIL, "Not saving PSM with score %f (line %d)", line.score, lineNum_);
                ++filteredOutPsmCount_;
                fileMap_.insert(make_pair(line.filename, vector<TSVPSM*>()));
                return;
            }

            TSVPSM* psm = new TSVPSM();
            psm->specKey = lineNum_;
            psm->rt = line.rt;
            if (!parseSequence(unimod_, line.sequence, &(psm->unmodSeq), &(psm->mods), &lineNum_)) {
                delete psm;
                return;
            }
            psm->charge = line.charge;
            psm->mz = line.mz;
            if (!line.proteinName.empty()) {
                vector<string> proteinNames;
                boost::split(proteinNames, line.proteinName, boost::is_any_of("/"));
                if (proteinNames.size() > 1) {
                    for (vector<string>::const_iterator i = proteinNames.begin() + 1; i != proteinNames.end(); ++i) {
                        map<string, Protein>::const_iterator j = proteins.find(*i);
                        if (j != proteins.end()) {
                            psm->proteins.insert(&j->second);
                        }
                        else {
                            proteins[*i] = Protein(*i);
                            psm->proteins.insert(&proteins[*i]);
                        }
                    }
                }
            }
            psm->leftWidth = line.leftWidth;
            psm->rightWidth = line.rightWidth;
            psm->score = line.score;
            if (!parsePeaks(line.peakArea, line.fragmentAnnotation, &(psm->mzs), &(psm->intensities))) {
                delete psm;
                return;
            }

            map< string, vector<TSVPSM*> >::iterator mapAccess = fileMap_.find(line.filename);
            if (mapAccess == fileMap_.end()) {
                fileMap_[line.filename] = vector<TSVPSM*>(1, psm);
            }
            else {
                fileMap_[line.filename].push_back(psm);
            }
        }

        bool parsePeaks(
            const std::string& peakArea,
            const std::string& fragmentAnnotation,
            std::vector<double>* mz,
            std::vector<double>* intensity)
        {
            if (mz != NULL) {
                mz->clear();
            }
            if (intensity != NULL) {
                intensity->clear();
            }

            vector<string> parts;

            split(parts, peakArea, is_any_of(";"));
            for (vector<string>::const_iterator i = parts.begin(); i != parts.end(); ++i) {
                double area;
                try {
                    area = lexical_cast<double>(*i);
                }
                catch (bad_lexical_cast&) {
                    Verbosity::error("Invalid peak area '%s' on line %d", i->c_str(), lineNum_);
                    return false;
                }
                if (intensity != NULL) {
                    intensity->push_back(area);
                }
            }
            size_t numPeaks = parts.size();

            split(parts, fragmentAnnotation, is_any_of(";"));
            if (parts.size() != numPeaks) {
                Verbosity::error("Number of peak areas (%d) did not match number of fragment annotations (%d) "
                    "on line %d", numPeaks, parts.size(), lineNum_);
                return false;
            }
            for (vector<string>::const_iterator i = parts.begin(); i != parts.end(); ++i) {
                vector<string> parts2;
                split(parts2, *i, is_any_of("_"));
                string seq;
                vector<SeqMod> mods;
                if (parts2.size() != 5 || parts2[1].length() < 2 || !parseSequence(unimod_, parts2[3], &seq, &mods, &lineNum_)) {
                    Verbosity::error("Unexpected format for fragment annotations on line %d: %s", lineNum_, i->c_str());
                    return false;
                }
                int ionNum, ionCharge;
                try {
                    ionNum = lexical_cast<int>(parts2[1].substr(1));
                    ionCharge = lexical_cast<int>(parts2[2]);
                }
                catch (bad_lexical_cast&) {
                    Verbosity::error("Unexpected format for fragment annotations on line %d: %s", lineNum_, i->c_str());
                    return false;
                }
                double ionMz;
                if (!calcIonMz(seq, mods, parts2[1][0], ionNum, ionCharge, &ionMz)) {
                    return false;
                }
                else if (mz != NULL) {
                    mz->push_back(ionMz);
                }
            }

            return true;
        }

        bool calcIonMz(
            const string& seq,
            const vector<SeqMod>& mods,
            char ionType,
            int ionNum,
            int ionCharge,
            double* ionMz
        ) {
            if (ionMz != NULL) {
                *ionMz = ionCharge * PROTON_MASS;
            }
            else if (ionNum < 1 || ionNum > seq.length()) {
                Verbosity::error("Invalid ion number %d on line %d (must be between 1 and %d for peptide '%s')",
                    ionNum, lineNum_, seq.length(), seq.c_str());
                return false;
            }

            size_t seqStart, seqEnd;
            switch (ionType) {
                case 'b':
                    seqStart = 0;
                    seqEnd = ionNum;
                    break;
                case 'y':
                    seqStart = seq.length() - ionNum;
                    seqEnd = seq.length();
                    if (ionMz != NULL) {
                        *ionMz += 2 * masses_['h'] + masses_['o'];
                    }
                    break;
                default:
                    Verbosity::error("Invalid ion type '%c' on line %d", ionType, lineNum_);
                    return false;
            }
            for (size_t i = seqStart; i < seqEnd; i++) {
                char c = seq[i];
                if ('A' <= c && c <= 'Z') {
                    if (ionMz != NULL) {
                        *ionMz += masses_[c];
                    }
                }
                else {
                    Verbosity::error("Invalid character '%c' in sequence '%s' on line %d", c, seq.c_str(), lineNum_);
                    return false;
                }
            }
            for (vector<SeqMod>::const_iterator i = mods.begin(); i != mods.end(); ++i) {
                size_t modIndex = i->position - 1;
                if (ionMz != NULL && seqStart <= modIndex && modIndex < seqEnd) {
                    *ionMz += i->deltaMass;
                }
            }
            if (ionMz != NULL) {
                *ionMz /= ionCharge;
            }
            return true;
        }
    };


    class OpenSwathAssayReader : public TSVReader
    {
        public:
        static constexpr const TSVColumnTranslator requiredColumns[] =
        {
            {"NormalizedRetentionTime", -1, TSVLine::insertRt},
            {"ModifiedPeptideSequence", -1, TSVLine::insertSequence},
            {"PrecursorCharge", -1, TSVLine::insertCharge},
            {"PrecursorMz", -1, TSVLine::insertMz},
            {"Decoy", -1, TSVLine::insertDecoy},
            {"ProductMz", -1, TSVLine::insertProductMz}, // populates leftWidth field
            {"LibraryIntensity", -1, TSVLine::insertPeakArea},
            {"FragmentType", -1, TSVLine::insertFragmentAnnotation},
            {"FragmentSeriesNumber", -1, TSVLine::insertFragmentSeriesNumber}
        };

        static constexpr const TSVColumnTranslator optionalColumns[] =
        {
            {"ProteinId", -1, TSVLine::insertProteinName},
            {"CollisionEnergy", -1, TSVLine::insertCE},
            {"PrecursorIonMobility", -1, TSVLine::insertIonMobility}
        };

        OpenSwathAssayReader(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress)
            : TSVReader(maker, tsvName, parentProgress), currentPsm(new TSVPSM)
        {
            for (const auto& requiredColumn : requiredColumns)
                targetColumns_.push_back(requiredColumn);
            for (const auto& optionalColumn : optionalColumns)
                optionalColumns_.push_back(optionalColumn);

            string line;
            getlinePortable(tsvFile_, line);
            LineParser headerLine(line, separator_);
            parseHeader(headerLine, targetColumns_, optionalColumns_);
        }

        static bool hasExpectedColumns(LineParser& headerLine)
        {
            set<string> headerColumns(headerLine.begin(), headerLine.end());
            for (const auto& requiredColumn : requiredColumns)
                if (headerColumns.find(requiredColumn.name_) == headerColumns.end())
                    return false;
            return true;
        }

        bool parseFile()
        {
            if (!tsvFile_)
                return false;

            Verbosity::debug("Collecting PSMs");
            map<string, Protein> proteins;
            collectPsms(proteins);

            // insert last PSM
            if (!currentSequence.empty())
            {
                auto& filePsms = fileMap_[tsvName_];
                filePsms.push_back(currentPsm.release());
            }

            Verbosity::debug("Building tables");
            initSpecFileProgress(fileMap_.size());
            for (map< string, vector<TSVPSM*> >::iterator i = fileMap_.begin(); i != fileMap_.end(); ++i) {
                psms_.assign(i->second.begin(), i->second.end());
                setSpecFileName(i->first.c_str(), false);
                buildTables(UNKNOWN_SCORE_TYPE, i->first, false);
            }

            return true;
        }

        vector<PSM_SCORE_TYPE> getScoreTypes() {
            return vector<PSM_SCORE_TYPE>(1, UNKNOWN_SCORE_TYPE);
        }

        private:
        double currentPrecursorMz = 0;
        std::string currentSequence = "";
        unique_ptr<TSVPSM> currentPsm;
        void storeLine(const TSVLine& line, std::map<std::string, Protein>& proteins)
        {
            if (line.decoy) {
                Verbosity::comment(V_DETAIL, "Not saving decoy PSM (line %d)", lineNum_);
                return;
            }

            if (!currentPsm)
                throw std::runtime_error("NULL currentPsm");

            if (line.mz != currentPsm->mz || line.sequence != currentSequence)
            {
                if (!currentSequence.empty())
                {
                    // insert current PSM and start a new one
                    auto& filePsms = fileMap_[tsvName_];
                    filePsms.push_back(currentPsm.release());
                    currentPsm.reset(new TSVPSM);
                }
                currentSequence = line.sequence;

                // store peptide-level attributes

                //currentPsm->filename = tsvName_;
                currentPsm->specKey = lineNum_;
                currentPsm->rt = line.rt;
                if (!parseSequence(unimod_, line.sequence, &(currentPsm->unmodSeq), &(currentPsm->mods), &lineNum_))
                    return;

                currentPsm->charge = line.charge;
                currentPsm->mz = line.mz;
                if (!line.proteinName.empty()) {
                    vector<string> proteinNames;
                    boost::split(proteinNames, line.proteinName, boost::is_any_of(";"));
                    for (vector<string>::const_iterator i = proteinNames.begin() + 1; i != proteinNames.end(); ++i) {
                        map<string, Protein>::const_iterator j = proteins.find(*i);
                        if (j != proteins.end()) {
                            currentPsm->proteins.insert(&j->second);
                        }
                        else {
                            proteins[*i] = Protein(*i);
                            currentPsm->proteins.insert(&proteins[*i]);
                        }
                    }
                }

                if (line.ionMobility > 0)
                {
                    currentPsm->ionMobility = line.ionMobility;
                    currentPsm->ionMobilityType = IONMOBILITY_DRIFTTIME_MSEC;
                }
            }

            // add product m/z and intensity to current PSM
            currentPsm->mzs.push_back(line.leftWidth);
            currentPsm->intensities.push_back(lexical_cast<double>(line.peakArea));
        }

    };

        
    class PaserLibraryReader : public TSVReader
    {
        /* All column names:
          PrecursorMz
          ProductMz
          Annotation
          ProteinId
          GeneName
          PeptideSequence
          ModifiedPeptideSequence
          PrecursorCharge
          LibraryIntensity
          NormalizedRetentionTime
          PrecursorIonMobility
          FragmentType
          FragmentCharge
          FragmentSeriesNumber
          FragmentLossType
          DecoyMobility
          TransitionId
        */
        public:
        static constexpr TSVColumnTranslator requiredColumns[] =
        {
            {"NormalizedRetentionTime", -1, TSVLine::insertRtMinutes},
            {"ModifiedPeptideSequence", -1, TSVLine::insertSequence},
            {"PrecursorCharge", -1, TSVLine::insertCharge},
            {"PrecursorMz", -1, TSVLine::insertMz},
            {"DecoyMobility", -1, TSVLine::ignore}, // unique to Paser?
            {"ProductMz", -1, TSVLine::insertProductMz}, // populates leftWidth field
            {"LibraryIntensity", -1, TSVLine::insertPeakArea},
            {"FragmentType", -1, TSVLine::insertFragmentAnnotation},
            {"FragmentSeriesNumber", -1, TSVLine::insertFragmentSeriesNumber},
            {"GeneName", -1, TSVLine::ignore} // unique to Paser?
        };

        static constexpr TSVColumnTranslator optionalColumns[] =
        {
            {"ProteinId", -1, TSVLine::insertProteinName},
            {"PrecursorIonMobility", -1, TSVLine::insertIonMobility}
        };

        PaserLibraryReader(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress, map<string, vector<PSM*>>* resultPsmMap = nullptr)
            : TSVReader(maker, tsvName, parentProgress), currentPsm(new TSVPSM), resultPsmMap_(resultPsmMap)
        {
            for (const auto& requiredColumn : requiredColumns)
                targetColumns_.push_back(requiredColumn);
            for (const auto& optionalColumn : optionalColumns)
                optionalColumns_.push_back(optionalColumn);

            string line;
            getlinePortable(tsvFile_, line);
            LineParser headerLine(line, separator_);
            parseHeader(headerLine, targetColumns_, optionalColumns_);
        }

        static bool hasExpectedColumns(LineParser& headerLine)
        {
            set<string> headerColumns(headerLine.begin(), headerLine.end());
            for (const auto& requiredColumn : requiredColumns)
                if (headerColumns.find(requiredColumn.name_) == headerColumns.end())
                    return false;
            return true;
        }

        bool parseFile()
        {
            if (!tsvFile_)
                return false;

            if (!resultPsmMap_)
                Verbosity::debug("Collecting PSMs");

            map<string, Protein> proteins;
            collectPsms(proteins);

            // insert last PSM
            if (!resultPsmMap_ && !currentSequence.empty())
            {
                auto& filePsms = fileMap_[tsvName_];
                filePsms.push_back(currentPsm);
                currentPsm = nullptr;
            }
            else
                return true; // do not build tables when called by PaserResultReader

            Verbosity::debug("Building tables");
            initSpecFileProgress(fileMap_.size());
            for (map< string, vector<TSVPSM*> >::iterator i = fileMap_.begin(); i != fileMap_.end(); ++i) {
                psms_.assign(i->second.begin(), i->second.end());
                setSpecFileName(i->first.c_str(), false);
                buildTables(UNKNOWN_SCORE_TYPE, i->first, false);
            }

            return true;
        }

        vector<PSM_SCORE_TYPE> getScoreTypes() {
            return vector<PSM_SCORE_TYPE>(1, UNKNOWN_SCORE_TYPE);
        }

        private:
        std::string currentSequence;
        TSVPSM* currentPsm;
        vector<PSM*>* currentPsms = nullptr;
        map<string, vector<PSM*>>* resultPsmMap_ = nullptr;


        void storeLine(const TSVLine& line, std::map<std::string, Protein>& proteins)
        {
            if (line.decoy) {
                Verbosity::comment(V_DETAIL, "Not saving decoy PSM (line %d)", lineNum_);
                return;
            }

            if (resultPsmMap_)
            {
                if (!currentPsm || line.mz != currentPsm->mz || line.sequence != currentSequence)
                {
                    auto findItr = resultPsmMap_->find(line.sequence + lexical_cast<string>(line.charge));
                    if (findItr == resultPsmMap_->end())
                    {
                        Verbosity::comment(V_DETAIL, "No result found for library entry %s", (line.sequence + lexical_cast<string>(line.charge)).c_str());
                        return;
                    }
                    currentPsms = &findItr->second;
                }

                // update only the library peaks
                for (auto& currentPsm : *currentPsms)
                {
                    static_cast<TSVPSM*>(currentPsm)->mzs.push_back(line.leftWidth);
                    static_cast<TSVPSM*>(currentPsm)->intensities.push_back(lexical_cast<double>(line.peakArea));
                }
            }
            else
            {
                if (!currentPsm)
                    throw std::runtime_error("NULL currentPsm");

                if (line.mz != currentPsm->mz || line.sequence != currentSequence)
                {
                    if (!currentSequence.empty())
                    {
                        // insert current PSM and start a new one
                        auto& filePsms = fileMap_[tsvName_];
                        filePsms.push_back(currentPsm);
                        currentPsm = new TSVPSM;
                    }
                    currentSequence = line.sequence;

                    // store peptide-level attributes

                    //currentPsm->filename = tsvName_;
                    currentPsm->specKey = lineNum_;
                    currentPsm->rt = line.rt;
                    if (!parseSequence(unimod_, line.sequence, &(currentPsm->unmodSeq), &(currentPsm->mods), &lineNum_))
                        return;

                    currentPsm->charge = line.charge;
                    currentPsm->mz = line.mz;
                    if (!line.proteinName.empty()) {
                        vector<string> proteinNames;
                        boost::split(proteinNames, line.proteinName, boost::is_any_of(";"));
                        for (vector<string>::const_iterator i = proteinNames.begin() + 1; i != proteinNames.end(); ++i) {
                            map<string, Protein>::const_iterator j = proteins.find(*i);
                            if (j != proteins.end()) {
                                currentPsm->proteins.insert(&j->second);
                            }
                            else {
                                proteins[*i] = Protein(*i);
                                currentPsm->proteins.insert(&proteins[*i]);
                            }
                        }
                    }

                    if (line.ionMobility > 0)
                    {
                        currentPsm->ionMobility = line.ionMobility;
                        currentPsm->ionMobilityType = IONMOBILITY_INVERSEREDUCED_VSECPERCM2;
                    }
                }

                currentPsm->mzs.push_back(line.leftWidth);
                currentPsm->intensities.push_back(lexical_cast<double>(line.peakArea));
            }
        }
    };

    class PaserResultReader : public TSVReader
    {
        /* All column names:
            File.Name
            Run
            Protein.Group
            Protein.Ids
            Protein.Names
            Genes
            PG.Quantity
            PG.Normalised
            Genes.Quantity
            Genes.Normalised
            Genes.MaxLFQ
            Genes.MaxLFQ.Unique
            Modified.Sequence
            Stripped.Sequence
            Precursor.Id
            Precursor.Charge
            Q.Value
            Protein.Q.Value
            PG.Q.Value
            GG.Q.Value
            Proteotypic
            Precursor.Quantity
            Precursor.Normalised
            Label.Ratio
            Quantity.Quality
            RT
            RT.Start
            RT.Stop
            iRT
            Predicted.RT
            Predicted.iRT
            First.Protein.Description
            Lib.Q.Value
            Ms1.Profile.Corr
            Ms1.Corr.Sum
            Ms1.Area
            Evidence
            CScore
            Decoy.Evidence
            Decoy.CScore
            Fragment.Quant.Raw
            Fragment.Quant.Corrected
            Fragment.Correlations
            MS2.Scan
            Precursor.FWHM
            Ms1.Iso.Corr.Sum
            Precursor.Mz
            Fragment.Info
            Exp.1/K0
            Lib.1/K0
            Identified.By
        */
        public:
        static constexpr TSVColumnTranslator requiredColumns[] =
        {
            {"File.Name", -1, TSVLine::insertFilename},
            {"RT", -1, TSVLine::insertRtMinutes},
            {"RT.Start", -1, TSVLine::insertRtStartMinutes}, // leftWidth
            {"RT.Stop", -1, TSVLine::insertRtEndMinutes}, // rightWidth
            {"Modified.Sequence", -1, TSVLine::insertSequence},
            {"Q.Value", -1, TSVLine::insertScore},
            {"Precursor.Charge", -1, TSVLine::insertCharge},
            {"Precursor.Mz", -1, TSVLine::insertMz},
            {"Ms1.Area", -1, TSVLine::insertPeakArea},
        };

        static constexpr TSVColumnTranslator optionalColumns[] =
        {
            {"Protein.Ids", -1, TSVLine::insertProteinName},
            {"Exp.1/K0", -1, TSVLine::insertIonMobility}
        };

        PaserResultReader(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress)
            : TSVReader(maker, tsvName, parentProgress)
        {
            for (const auto& requiredColumn : requiredColumns)
                targetColumns_.push_back(requiredColumn);
            for (const auto& optionalColumn : optionalColumns)
                optionalColumns_.push_back(optionalColumn);

            string line;
            getlinePortable(tsvFile_, line);
            LineParser headerLine(line, separator_);
            parseHeader(headerLine, targetColumns_, optionalColumns_);

            // check for ip2_ip2 TSV files
            vector<bfs::path> ip2TsvFiles;
            pwiz::util::expand_pathmask(bfs::path(tsvName).parent_path() / "_ip2_ip2*.tsv", ip2TsvFiles);
            if (ip2TsvFiles.size() > 1)
                throw std::runtime_error("found more than one ip2_ip2 library TSV file in the same directory as the Paser results; move the TSV files to a separate directory with one ip2_ip2 TSV");
            if (ip2TsvFiles.empty())
                throw std::runtime_error("missing required ip2_ip2 library TSV file corresponding to Paser results (it should start with '_ip2_ip2_' and end with '.tsv')");

            libraryTsv_ = ip2TsvFiles[0];
        }

        static bool hasExpectedColumns(LineParser& headerLine)
        {
            set<string> headerColumns(headerLine.begin(), headerLine.end());
            for (const auto& requiredColumn : requiredColumns)
                if (headerColumns.find(requiredColumn.name_) == headerColumns.end())
                {
                    Verbosity::comment(V_DETAIL, "did not find column '%s'", requiredColumn.name_);
                    return false;
                }
            return true;
        }

        bool parseFile()
        {
            if (!tsvFile_)
                return false;

            Verbosity::debug("Collecting PSMs");
            map<string, Protein> proteins;
            collectPsms(proteins);

            if (!libraryTsv_.empty())
                addLibraryInfo();

            Verbosity::debug("Building tables");
            initSpecFileProgress(filePsmMap_.size());
            for (auto& resultPsmListPair : filePsmMap_) {
                psms_.swap(resultPsmListPair.second);
                setSpecFileName(resultPsmListPair.first, false);
                buildTables(GENERIC_QVALUE, resultPsmListPair.first, false);
                psms_.swap(resultPsmListPair.second);
            }

            resultPsmMap_.clear(); // no dangling pointers

            return true;
        }

        vector<PSM_SCORE_TYPE> getScoreTypes() {
            return vector<PSM_SCORE_TYPE>(1, GENERIC_QVALUE);
        }

        private:
        bfs::path libraryTsv_;
        map<string, vector<PSM*>> resultPsmMap_;
        map<string, vector<PSM*>> filePsmMap_; // store psms by filename

        void addLibraryInfo()
        {
            Verbosity::debug("Collecting peaks from library '%s'", libraryTsv_.string().c_str());
            PaserLibraryReader libraryReader(blibMaker_, libraryTsv_.string().c_str(), parentProgress_, &resultPsmMap_);
            libraryReader.parseFile();
        }

        void storeLine(const TSVLine& line, std::map<std::string, Protein>& proteins)
        {
            if (line.decoy) {
                Verbosity::comment(V_DETAIL, "Not saving decoy PSM (line %d)", lineNum_);
                return;
            }

            string currentFilename = line.filename;
            if (bal::contains(currentFilename, " - "))
                bal::erase_tail(currentFilename, line.filename.length() - line.filename.rfind(" - "));

            auto currentPsm = std::make_unique<TSVPSM>();

            // store peptide-level attributes
            currentPsm->score = line.score;
            currentPsm->specKey = lineNum_;
            currentPsm->rt = line.rt;
            currentPsm->leftWidth = line.leftWidth;
            currentPsm->rightWidth = line.rightWidth;
            if (!parseSequence(unimod_, line.sequence, &(currentPsm->unmodSeq), &(currentPsm->mods), &lineNum_))
                return;

            currentPsm->charge = line.charge;
            currentPsm->mz = line.mz;
            if (!line.proteinName.empty()) {
                vector<string> proteinNames;
                boost::split(proteinNames, line.proteinName, boost::is_any_of(";"));
                for (vector<string>::const_iterator i = proteinNames.begin() + 1; i != proteinNames.end(); ++i) {
                    map<string, Protein>::const_iterator j = proteins.find(*i);
                    if (j != proteins.end()) {
                        currentPsm->proteins.insert(&j->second);
                    }
                    else {
                        proteins[*i] = Protein(*i);
                        currentPsm->proteins.insert(&proteins[*i]);
                    }
                }
            }

            if (line.ionMobility > 0)
            {
                currentPsm->ionMobility = line.ionMobility;
                currentPsm->ionMobilityType = IONMOBILITY_INVERSEREDUCED_VSECPERCM2;
            }

            auto& filePsms = filePsmMap_[currentFilename];
            filePsms.push_back(currentPsm.release());
            resultPsmMap_[line.sequence + lexical_cast<string>(line.charge)].push_back(filePsms.back());
        }

    };

    // TODO: remove when switching to C++17
    constexpr const TSVColumnTranslator OpenSwathResultReader::requiredColumns[];
    constexpr const TSVColumnTranslator OpenSwathResultReader::optionalColumns[];
    constexpr const TSVColumnTranslator OpenSwathAssayReader::requiredColumns[];
    constexpr const TSVColumnTranslator OpenSwathAssayReader::optionalColumns[];
    constexpr const TSVColumnTranslator PaserLibraryReader::requiredColumns[];
    constexpr const TSVColumnTranslator PaserLibraryReader::optionalColumns[];
    constexpr const TSVColumnTranslator PaserResultReader::requiredColumns[];
    constexpr const TSVColumnTranslator PaserResultReader::optionalColumns[];

} // namespace

TSVReader::TSVReader(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress)
    : BuildParser(maker, tsvName, parentProgress), tsvName_(tsvName), masses_(new double[128]),
      scoreThreshold_(getScoreThreshold(GENERIC_QVALUE_INPUT)), lineNum_(1)
{
    setSpecFileName(tsvName, false);

    // point to self as spec reader
    delete specReader_;
    specReader_ = this;

    AminoAcidMasses::initializeMass(masses_, 1);

    Verbosity::debug("Parsing Unimod");
    string unimodFile = getExeDirectory() + "unimod.xml";
    unimod_.setFile(unimodFile.c_str());
    unimod_.parse();
    Verbosity::debug("Successfully parsed %d Unimod records", unimod_.numMods());

    Verbosity::debug("Opening file");
    tsvFile_.open(tsvName_.c_str());
}

TSVReader::~TSVReader() {
    specReader_ = NULL; // so parent class doesn't try to delete itself
}

const escaped_list_separator<char> TSVReader::separator_("", "\t", "");

std::shared_ptr<TSVReader> TSVReader::create(BlibBuilder& maker, const char* tsvName, const ProgressIndicator* parentProgress)
{
    string line;
    {
        ifstream tsvFile(tsvName);
        getlinePortable(tsvFile, line);
    }
    LineParser headerLine(line, separator_);

    if (PaserLibraryReader::hasExpectedColumns(headerLine))
        return std::make_shared<PaserLibraryReader>(maker, tsvName, parentProgress);
    if (PaserResultReader::hasExpectedColumns(headerLine))
        return std::make_shared<PaserResultReader>(maker, tsvName, parentProgress);
    if (OpenSwathResultReader::hasExpectedColumns(headerLine))
        return std::static_pointer_cast<TSVReader>(std::make_shared<OpenSwathResultReader>(maker, tsvName, parentProgress));
    if (OpenSwathAssayReader::hasExpectedColumns(headerLine))
        return std::make_shared<OpenSwathAssayReader>(maker, tsvName, parentProgress);
    throw BlibException(false, "Did not find required columns. Only OpenSWATH result, OpenSWATH assay, and Paser .tsv files are supported.");
}

void TSVReader::parseHeader(LineParser& headerLine, vector<TSVColumnTranslator>& targetColumns, vector<TSVColumnTranslator>& optionalColumns) {

    Verbosity::debug("Parsing header");

    // get the column index of each required column
    int colNumber = 0;
    for (LineParser::iterator i = headerLine.begin(); i != headerLine.end(); ++i) {
        bool found = false;
        for (vector<TSVColumnTranslator>::iterator j = targetColumns.begin(); j != targetColumns.end(); ++j) {
            if (iequals(*i, j->name_)) {
                found = true;
                j->position_ = colNumber;
                break;
            }
        }
        if (!found) {
            for (vector<TSVColumnTranslator>::iterator j = optionalColumns.begin(); j != optionalColumns.end(); ++j) {
                if (iequals(*i, j->name_)) {
                    found = true;
                    j->position_ = colNumber;
                    targetColumns.push_back(*j);
                    break;
                }
            }
        }
        colNumber++;
    }

    // sort by column number so they can be fetched in order
    sort(targetColumns.begin(), targetColumns.end());
}

void TSVReader::collectPsms(map<string, Protein>& proteins) {
    tsvFile_.seekg(0);
    string lineStr;
    getlinePortable(tsvFile_, lineStr);

    ProgressIndicator progress(bfs::file_size(tsvName_) - lineStr.length()+1);

    while (!tsvFile_.eof()) {
        getlinePortable(tsvFile_, lineStr);
        lineNum_++;

        TSVLine line;
        int col = 0;
        size_t targetIdx = 0;
        try {
            LineParser parser(lineStr, separator_);
            for (LineParser::iterator i = parser.begin(); i != parser.end(); ++i) {
                if (col++ == targetColumns_[targetIdx].position_) {
                    targetColumns_[targetIdx++].inserter_(line, *i);
                    if (targetIdx == targetColumns_.size()) {
                        break;
                    }
                }
            }
            if (targetIdx != targetColumns_.size()) {
                Verbosity::warn("Skipping invalid line %d", lineNum_);
                continue;
            }
        } catch (BlibException& e) {
            throw BlibException(false, "%s caught at line %d, column %d",
                                e.what(), lineNum_, col + 1);
        } catch (std::exception& e) {
            throw BlibException(false, "%s caught at line %d, column %d",
                                e.what(), lineNum_, col + 1);
        } catch (string& s) {
            throw BlibException(false, "%s caught at line %d, column %d",
                                s.c_str(), lineNum_, col + 1);
        } catch (...) {
            throw BlibException(false, "%s caught at line %d, column %d",
                                "Unknown exception", lineNum_, col + 1);
        }
        storeLine(line, proteins);
        progress.add(lineStr.length() + 1);
    }
}

bool TSVReader::parseSequence(
    const UnimodParser& unimod,
    const string& seq,
    string* outSeq,
    vector<SeqMod>* outMods,
    int* line
) {
    if (outSeq != NULL) {
        outSeq->clear();
    }
    if (outMods != NULL) {
        outMods->clear();
    }

    string seqUpper = to_upper_copy<string>(seq);
    if (!seqUpper.empty() && seqUpper[0] == '.') {
        seqUpper = seqUpper.substr(1);
    }
    const string searchString = "(UNIMOD:";
    size_t i;
    while ((i = seqUpper.find(searchString)) != string::npos) {
        size_t j = i + searchString.length();
        size_t k = seqUpper.find(')', j + 1);
        if (k == string::npos) {
            if (line) {
                Verbosity::error("Invalid sequence '%s' on line %d", seq.c_str(), *line);
            } else {
                Verbosity::error("Invalid sequence '%s'", seq.c_str());
            }
            return false;
        }
        int id;
        try {
            id = lexical_cast<int>(seqUpper.substr(j, k - j));
        } catch (bad_lexical_cast&) {
            if (line) {
                Verbosity::error("Non-numeric modification ID in sequence '%s' on line %d", seq.c_str(), *line);
            } else {
                Verbosity::error("Non-numeric modification ID in sequence '%s'", seq.c_str());
            }
            return false;
        }
        if (!unimod.hasMod(id)) {
            if (line) {
                Verbosity::error("Unknown modification ID %d in sequence '%s' on line '%d'", id, seq.c_str(), *line);
            } else {
                Verbosity::error("Unknown modification ID %d in sequence '%s'", id, seq.c_str());
            }
            return false;
        }
        seqUpper.erase(i, k - i + 1);
        if (outMods != NULL) {
            // the SeqMod's position is equal to the index after the modified AA, since it is 1-based
            outMods->push_back(SeqMod(std::max((size_t)1, i), unimod.getModMass(id)));
        }
    }

    if (outSeq != NULL) {
        *outSeq = seqUpper;
    }
    return true;
}


// Overrides the SpecFileReader implementation.
// Assumes the PSM is a TSVPSM and copies the additional data from it to the SpecData.
bool TSVReader::getSpectrum(PSM* psm, SPEC_ID_TYPE findBy, SpecData& returnData, bool getPeaks) {
    returnData.id = psm->specKey;
    returnData.retentionTime = ((TSVPSM*)psm)->rt;
    returnData.startTime = ((TSVPSM*)psm)->leftWidth;
    returnData.endTime = ((TSVPSM*)psm)->rightWidth;
    returnData.mz = ((TSVPSM*)psm)->mz;
    returnData.numPeaks = ((TSVPSM*)psm)->mzs.size();

    if (getPeaks) {
        returnData.mzs = new double[returnData.numPeaks];
        returnData.intensities = new float[returnData.numPeaks];
        for (int i = 0; i < returnData.numPeaks; i++) {
            returnData.mzs[i] = ((TSVPSM*)psm)->mzs[i];
            returnData.intensities[i] = (float)((TSVPSM*)psm)->intensities[i];
        }
    } else {
        returnData.mzs = NULL;
        returnData.intensities = NULL;
    }
    return true;
}

bool TSVReader::getSpectrum(int, SpecData&, SPEC_ID_TYPE, bool) { return false; }
bool TSVReader::getSpectrum(string, SpecData&, bool) { return false; }
bool TSVReader::getNextSpectrum(SpecData&, bool) { return false; }

} // namespace

