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
 * The MzIdentMLReader class definition.  A class to parse the
 * mzIdentML files, particularly those produced by Scaffold.
 */

#include "MzIdentMLReader.h"
#include <iostream>
#include <boost/algorithm/string.hpp>

using namespace pwiz;
using namespace identdata;

namespace BiblioSpec {

MzIdentMLReader::MzIdentMLReader
    (BlibBuilder& maker,
     const char* mzidFileName,
     const ProgressIndicator* const parent_progress)
    : BuildParser(maker, mzidFileName, parent_progress)
{
    analysisType_ = UNKNOWN_ANALYSIS;
    pwizReader_ = new IdentDataFile(mzidFileName);
    list_iter_ = pwizReader_->dataCollection.analysisData.spectrumIdentificationList.begin();
    list_end_ = pwizReader_->dataCollection.analysisData.spectrumIdentificationList.end();

    lookUpBy_ = NAME_ID;
    scoreThreshold_ = 0;
    isScoreLookup_ = false;
}

MzIdentMLReader::~MzIdentMLReader()
{
    delete pwizReader_;
}

/**
 * Implementation of BuildParser virtual method.  Reads the .mzid file,
 * stores psms, organized by spectrum file, and imports all spectra.
 */
bool MzIdentMLReader::parseFile(){
    map<DBSequencePtr, Protein> proteins;
    Verbosity::debug("Reading psms from the file.");
    collectPsms(proteins);

    // for each file
    if( fileMap_.size() > 1 ){
        initSpecFileProgress((int)fileMap_.size());
    }

    map<string, string> mapSourceFiles;
    vector<pwiz::identdata::SourceFilePtr>& sourceFiles = pwizReader_->dataCollection.inputs.sourceFile;
    for(size_t i = 0; i < sourceFiles.size(); i++){
        string location = sourceFiles[i]->location;
        if (!location.empty())
        {
            size_t dot = location.find_last_of('.');
            size_t slash = location.find_last_of("\\/");
            string key = (dot != string::npos && slash != string::npos && slash < dot)
                ? location.substr(slash + 1, dot - slash - 1)
                : location;
            mapSourceFiles[key] = location;
        }
    }

    PSM_SCORE_TYPE scoreType = analysisToScoreType(analysisType_);

    map<string, vector<PSM*> >::iterator fileIterator = fileMap_.begin();
    vector<std::string> specExtensions;
    specExtensions.push_back(".MGF");
    specExtensions.push_back(".mzXML");
    specExtensions.push_back(".mzML");
    specExtensions.push_back(".mz5");
    #ifdef VENDOR_READERS
	    specExtensions.push_back(".raw"); // Waters/Thermo
	    specExtensions.push_back(".wiff"); // Sciex
	    specExtensions.push_back(".wiff2"); // Sciex
	    specExtensions.push_back(".d"); // Bruker/Agilent
	    specExtensions.push_back(".lcd"); // Shimadzu
	#endif
    for(; fileIterator != fileMap_.end(); ++fileIterator) {
        vector<string> pathParts;
        boost::split(pathParts, fileIterator->first, boost::is_any_of(";"));
        string specFileroot = getFileRoot(pathParts[0]);
        setSpecFileName(specFileroot.c_str(), specExtensions);
        string filename = getSpecFileName();
        if (filename.length() >= 6 && filename.substr(filename.length() - 6) == ".mzXML") {
            lookUpBy_ = SCAN_NUM_ID;
        }

        string sourceFile = pathParts[1];
        if (!mapSourceFiles[sourceFile].empty())
            sourceFile = mapSourceFiles[sourceFile];

        // move from map to psms_
        psms_ = fileIterator->second;
        if (!isScoreLookup_) {
            if (sourceFile.empty())
                buildTables(scoreType);
            else
                buildTables(scoreType, sourceFile);
        }
    }

    return true;
}

vector<PSM_SCORE_TYPE> MzIdentMLReader::getScoreTypes() {
    isScoreLookup_ = true;
    try {
        parseFile();
    } catch (SAXHandler::EndEarlyException) {
    }
    return vector<PSM_SCORE_TYPE>(1, analysisToScoreType(analysisType_));
}

/**
 *  Read through the whole file to find all PSMs.  Save those that
 *  pass the score threshold and are not decoys.  Nested data
 *  structure is
 *  SpectrumIdentificationList -- lists of spectra, one list per file
 *     SpectrumIdentificationResult -- the spectra in each list
 *         SpectrumIdenficiationItem -- specific peptide match to the spec
 *             PeptideEvidencePtr -- one for each prot in which pep is found
 */
void MzIdentMLReader::collectPsms(map<DBSequencePtr, Protein>& proteins) {
    // 1 SpectrumIdentificationList = 1 .MGF file
    for(; list_iter_ != list_end_; ++list_iter_){

        // 1 SpectrumIdentifiationResult = 1 spectrum
        for(result_iter_ = (**list_iter_).spectrumIdentificationResult.begin();
            result_iter_ != (**list_iter_).spectrumIdentificationResult.end();
            ++result_iter_)
        {
            SpectrumIdentificationResult& result = **result_iter_;

            // HACK: ProteinPilot mzid output has spectraData always pointing at SD_1 but has a file=xxx which is the 1-based index to the correct file
            if (bal::starts_with(result.id, "file="))
            {
                int fileIndex = msdata::id::valueAs<int>(result.id, "file") - 1;
                result.spectraDataPtr = pwizReader_->dataCollection.inputs.spectraData.at(fileIndex);
            }

            string filename = result.spectraDataPtr->location;
            string idStr = result.spectrumID;
            filename += ";";
            filename += getFilenameFromID(idStr);

            // 1 SpectrumIdentificationItem = 1 psm
            for(item_iter_ = result.spectrumIdentificationItem.begin();
                item_iter_ != result.spectrumIdentificationItem.end();
                ++item_iter_)
            {
                SpectrumIdentificationItem& item = **item_iter_;

                if (item.peptideEvidencePtr.empty())
                {
                    Verbosity::warn("%s does not have any PeptideEvidenceRefs", result.id.c_str());
                    continue;
                }

                // only include top-ranked PSMs, skip decoys
                if( item.rank > 1 || item.peptideEvidencePtr.front()->isDecoy ){
                    continue;
                }

                // skip if it doesn't pass score threshold
                double score = getScore(item);
                if (!passThreshold(score)) {
                    continue;
                }

                // now get the psm info
                curPSM_ = new PSM();
                switch (analysisType_) {
                    case BYONIC_ANALYSIS:
                        curPSM_->specName = result.cvParam(MS_spectrum_title).valueAs<string>();
                        break;
                    case MSGF_ANALYSIS:
                        if (result.hasCVParam(MS_scan_number_s__OBSOLETE)) {
                            curPSM_->specKey = result.cvParam(MS_scan_number_s__OBSOLETE).valueAs<int>();
                            lookUpBy_ = SCAN_NUM_ID;
                        } else {
                            // If still no scan number, use nativeID
                            curPSM_->specName = idStr;
                        }
                        break;
                    default:
                        curPSM_->specName = idStr;
                        break;
                }
                if (curPSM_->specKey < 0) {
                    stringToScan(curPSM_->specName, curPSM_);
                }
                curPSM_->score = score;
                curPSM_->charge = item.chargeState;
                extractIonMobility(result, item, curPSM_);

                PeptidePtr peptidePtr = item.peptidePtr;
                for (const auto& peptideEvidencePtr : item.peptideEvidencePtr) {
                    if (!peptideEvidencePtr->dbSequencePtr) {
                        Verbosity::error("peptideEvidenceRef %s has null dbSequenceRef", peptideEvidencePtr->id.c_str());
                        continue;
                    }
                    const DBSequencePtr& dbSeq = peptideEvidencePtr->dbSequencePtr;
                    if (!peptidePtr) peptidePtr = peptideEvidencePtr->peptidePtr;
                    map<DBSequencePtr, Protein>::const_iterator j = proteins.find(dbSeq);
                    if (j != proteins.end()) {
                        curPSM_->proteins.insert(&j->second);
                    } else {
                        proteins[dbSeq] = Protein(dbSeq->accession);
                        curPSM_->proteins.insert(&proteins[dbSeq]);
                    }
                }
                extractModifications(peptidePtr, curPSM_);

                // add the psm to the map
                Verbosity::comment(V_DETAIL, "For file %s adding PSM: "
                                   "scan '%s', charge %d, sequence '%s'.",
                                   filename.c_str(), curPSM_->specName.c_str(),
                                   curPSM_->charge, curPSM_->unmodSeq.c_str());
                map<string, vector<PSM*> >::iterator mapAccess =
                    fileMap_.find(filename);
                if( mapAccess == fileMap_.end() ){ // not found, add the file
                    vector<PSM*> tmpPsms(1, curPSM_);
                    fileMap_[filename] = tmpPsms;
                } else {  // add this psm to existing file entry
                    (mapAccess->second).push_back(curPSM_);
                }
                curPSM_ = NULL;
            } // next item (PSM)
        } // next result (spectrum)
    } // next list (file)
}


/**
 * Using the modified peptide sequence, with modifications of the form
 * +mass or -mass, set the unmodSeq and mods fields of the psm.
 */
void MzIdentMLReader::extractModifications(PeptidePtr peptide, PSM* psm){

    vector<ModificationPtr>::const_iterator itMod=peptide->modification.begin();
    vector<SubstitutionModificationPtr>::const_iterator itSubst=peptide->substitutionModification.begin();
    while (itMod!=peptide->modification.end() || itSubst!=peptide->substitutionModification.end()){

        int location;
        double massDelta;
        if (itMod!=peptide->modification.end() && (itSubst==peptide->substitutionModification.end() ||
                                                   (*itMod)->location < (*itSubst)->location)) {
            ModificationPtr mod = *itMod++;
            location = mod->location;
            massDelta = mod->monoisotopicMassDelta != 0 ? mod->monoisotopicMassDelta : mod->avgMassDelta;
        } else {
            SubstitutionModificationPtr mod = *itSubst++;
            location = mod->location;
            massDelta = mod->monoisotopicMassDelta != 0 ? mod->monoisotopicMassDelta : mod->avgMassDelta;
        }
        // N-terminal modifications can be set to location zero, which is not supported
        // in BiblioSpec.  Instead, N-terminal modifications are treated as modifictions
        // to the first amino acid residue, as in X! Tandem.
        location = max(location, 1);
        location = min(location, (int)peptide->peptideSequence.length());
        psm->mods.push_back(SeqMod(location, massDelta));
    }

    psm->unmodSeq = peptide->peptideSequence;
}

void MzIdentMLReader::extractIonMobility(const pwiz::identdata::SpectrumIdentificationResult& result, const pwiz::identdata::SpectrumIdentificationItem& item, PSM* psm) {

    auto ionMobilityParam = result.cvParamChild(MS_ion_mobility_attribute);
    if (ionMobilityParam.empty())
        ionMobilityParam = item.cvParamChild(MS_ion_mobility_attribute); // should not be in SII, but some PEAKS output has it there
    if (!ionMobilityParam.empty()) {
        psm->ionMobility = ionMobilityParam.valueAs<double>();
        switch (ionMobilityParam.cvid) {
            case MS_ion_mobility_drift_time:
                psm->ionMobilityType = IONMOBILITY_DRIFTTIME_MSEC;
                break;
            case MS_inverse_reduced_ion_mobility:
                psm->ionMobilityType = IONMOBILITY_INVERSEREDUCED_VSECPERCM2;
                break;
            case MS_FAIMS_CV:
                psm->ionMobilityType = IONMOBILITY_COMPENSATION_V;
                break;
            default:
                Verbosity::warn("unsupported ion mobility type: %s", ionMobilityParam.name().c_str());
                break;
        }
    }
}

/**
 * Look through the CVParams of the item and return the score for the
 * peptide probability.
 */
double MzIdentMLReader::getScore(const SpectrumIdentificationItem& item){

    // look through all params to find the probability
    for(const CVParam& cvParam : item.cvParams)
    {
        switch (cvParam.cvid)
        {
            case MS_PeptideShaker_PSM_confidence:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(PEPTIDESHAKER_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(PEPTIDE_SHAKER);
                }
                if (analysisType_ == PEPTIDESHAKER_ANALYSIS)
                    return cvParam.valueAs<double>() / 100.0;
                break;

            case MS_Scaffold_Peptide_Probability:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(SCAFFOLD_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(SCAFFOLD);
                }
                if (analysisType_ == SCAFFOLD_ANALYSIS)
                    return cvParam.valueAs<double>();
                break;

            case MS_Byonic__Peptide_AbsLogProb:
            case MS_Byonic__Peptide_AbsLogProb2D:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(BYONIC_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(BYONIC);
                }
                if (analysisType_ == BYONIC_ANALYSIS)
                    return pow(10, -1 * cvParam.valueAs<double>());
                break;

            case MS_MS_GF_QValue:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(MSGF_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(MSGF);
                }
                if (analysisType_ == MSGF_ANALYSIS)
                    return cvParam.valueAs<double>();
                break;

            case MS_Mascot_expectation_value:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(MASCOT_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(MASCOT);
                }
                if (analysisType_ == MASCOT_ANALYSIS)
                    return cvParam.valueAs<double>();
                break;

            case MS_PEAKS_peptideScore:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(PEAKS_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(PEAKS);
                }
                if (analysisType_ == PEAKS_ANALYSIS)
                    return pow(10, cvParam.valueAs<double>() / -10);
                break;

            case MS_Paragon_confidence:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(PROT_PILOT_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(PROT_PILOT);
                }
                if (analysisType_ == PROT_PILOT_ANALYSIS)
                    return cvParam.valueAs<double>();
                break;

            case MS_PSM_level_q_value:
            case MS_percolator_Q_value:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(GENERIC_QVALUE_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(GENERIC_QVALUE_INPUT);
                }
                if (analysisType_ == GENERIC_QVALUE_ANALYSIS)
                    return cvParam.valueAs<double>();
                break;

            default:
                continue;
        }
    }

    // another round of search for secondary scores if primary scores aren't found
    for (const CVParam& cvParam : item.cvParams)
    {
        switch (cvParam.cvid)
        {
            case MS_MS_GF_EValue:
                if (analysisType_ == UNKNOWN_ANALYSIS) {
                    setAnalysisType(MSGF_ANALYSIS);
                    scoreThreshold_ = getScoreThreshold(MSGF);
                }
                if (analysisType_ == MSGF_ANALYSIS)
                    return cvParam.valueAs<double>();
                break;
            default:
                break;
        }
    }

    Verbosity::error(".mzid file contains an unsupported score type");
    return 0;
}

void MzIdentMLReader::setAnalysisType(ANALYSIS analysisType) {
    analysisType_ = analysisType;
    if (isScoreLookup_) {
        throw SAXHandler::EndEarlyException();
    }
}

PSM_SCORE_TYPE MzIdentMLReader::analysisToScoreType(ANALYSIS analysisType) {
    switch (analysisType) {
        case SCAFFOLD_ANALYSIS:
            return SCAFFOLD_SOMETHING;
        case BYONIC_ANALYSIS:
            return BYONIC_PEP;
        case MSGF_ANALYSIS:
            return MSGF_SCORE;
        case PEPTIDESHAKER_ANALYSIS:
            return PEPTIDE_SHAKER_CONFIDENCE;
        case MASCOT_ANALYSIS:
            return MASCOT_IONS_SCORE;
        case PEAKS_ANALYSIS:
            return PEAKS_CONFIDENCE_SCORE;
        case PROT_PILOT_ANALYSIS:
            return PROTEIN_PILOT_CONFIDENCE;
        case GENERIC_QVALUE_ANALYSIS:
            return GENERIC_QVALUE;
        default:
            return UNKNOWN_SCORE_TYPE;
    }
}

bool MzIdentMLReader::passThreshold(double score)
{
    switch (analysisType_) {
        // Scores where lower is better
        case BYONIC_ANALYSIS:
        case MASCOT_ANALYSIS:
        case MSGF_ANALYSIS:
        case PEAKS_ANALYSIS:
        case GENERIC_QVALUE_ANALYSIS:
            return score <= scoreThreshold_;
        // Scores where higher is better
        case SCAFFOLD_ANALYSIS:
        case PEPTIDESHAKER_ANALYSIS:
        case PROT_PILOT_ANALYSIS:
            return score >= scoreThreshold_;
    }
    Verbosity::error("Can't determine cutoff score, unknown analysis type");
    return false;
}

bool MzIdentMLReader::stringToScan(const string& name, PSM* psm) {
    vector<string> parts;
    boost::split(parts, name, boost::is_any_of(" "));
    for (vector<string>::const_iterator i = parts.begin(); i != parts.end(); ++i) {
        if (i->compare(0, 5, "scan=") == 0) {
            psm->specKey = boost::lexical_cast<int>(i->substr(5));
            return true;
        }
    }

    // check for <scan>.<scan>
    boost::split(parts, name, boost::is_any_of("."));
    for (size_t i = 0; i < parts.size() - 1; i++) {
        if (parts[i] == parts[i+1] && parts[i].find_first_not_of("0123456789") == string::npos) {
            psm->specKey = atoi(parts[i].c_str());
            return true;
        }
    }

    return false;
}

} // namespace



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
