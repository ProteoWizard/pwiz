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

#include "OSWReader.h"
#include "TSVReader.h"
#include <cstring>

using namespace std;

namespace BiblioSpec {

OSWReader::OSWReader(BlibBuilder& maker,
                     const char* filename,
                     const ProgressIndicator* parentProgress)
  : BuildParser(maker, filename, parentProgress), filename_(filename), osw_(NULL),
    scoreThreshold_(getScoreThreshold(GENERIC_QVALUE_INPUT)) {
    setSpecFileName(filename, false);
    delete specReader_; // point to self as spec reader
    specReader_ = this;
    lookUpBy_ = NAME_ID;
}

OSWReader::~OSWReader() {
    for (vector<PSM*>::const_iterator i = psms_.begin(); i != psms_.end(); i++) {
        delete *i;
    }
    psms_.clear();
    specReader_ = NULL; // so parent class doesn't try to delete itself
    sqlite3_close(osw_);
}

bool OSWReader::parseFile() {
    Verbosity::debug("Parsing Unimod");
    string unimodFile = getExeDirectory() + "unimod.xml";
    unimod_.setFile(unimodFile.c_str());
    unimod_.parse();
    Verbosity::debug("Successfully parsed %d Unimod records", unimod_.numMods());

    Verbosity::debug("Opening %s", filename_.c_str());
    if (sqlite3_open(filename_.c_str(), &osw_) != SQLITE_OK) {
        Verbosity::error("Error opening %s", filename_.c_str());
        return false;
    }

    const string query =
        "SELECT FEATURE.ID, "
        "       RUN.FILENAME, "
        "       FEATURE.EXP_RT, "
        "       PEPTIDE.MODIFIED_SEQUENCE, "
        "       PRECURSOR.CHARGE, "
        "       PRECURSOR.PRECURSOR_MZ, "
        "       PROTEIN.PROTEIN_ACCESSION, "
        "       FEATURE.LEFT_WIDTH, "
        "       FEATURE.RIGHT_WIDTH, "
        "       TRANSITION.PRODUCT_MZ, "
        "       FEATURE_TRANSITION.AREA_INTENSITY, "
        "       SCORE_MS2.QVALUE "
        "FROM FEATURE "
        "JOIN PRECURSOR ON FEATURE.PRECURSOR_ID = PRECURSOR.ID "
        "JOIN PRECURSOR_PEPTIDE_MAPPING ON PRECURSOR.ID = PRECURSOR_PEPTIDE_MAPPING.PRECURSOR_ID "
        "JOIN PEPTIDE ON PRECURSOR_PEPTIDE_MAPPING.PEPTIDE_ID = PEPTIDE.ID "
        "JOIN RUN ON FEATURE.RUN_ID = RUN.ID "
        "JOIN SCORE_MS2 ON FEATURE.ID = SCORE_MS2.FEATURE_ID "
        "JOIN FEATURE_TRANSITION ON FEATURE.ID = FEATURE_TRANSITION.FEATURE_ID "
        "JOIN TRANSITION ON FEATURE_TRANSITION.TRANSITION_ID = TRANSITION.ID "
        "LEFT JOIN PEPTIDE_PROTEIN_MAPPING ON PRECURSOR_PEPTIDE_MAPPING.PEPTIDE_ID = PEPTIDE_PROTEIN_MAPPING.PEPTIDE_ID "
        "LEFT JOIN PROTEIN ON PEPTIDE_PROTEIN_MAPPING.PROTEIN_ID = PROTEIN.ID "
        "WHERE PRECURSOR.DECOY = 0 "
        "  AND SCORE_MS2.RANK = 1 "
        "  AND SCORE_MS2.QVALUE <= " + boost::lexical_cast<string>(scoreThreshold_) + " "
        "ORDER BY FEATURE.ID ASC";
    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(osw_, query.c_str(), -1, &stmt, NULL) != SQLITE_OK) {
        Verbosity::error("Error preparing PSM statement: %s", sqlite3_errmsg(osw_));
        return false;
    }

    map<string, Protein> proteins;
    string lastFeatureId = "";
    SpecData* curSpectrum = NULL;
    vector<double> curPeakMzs;
    vector<float> curPeakIntensities;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        string featureId = boost::lexical_cast<string>(sqlite3_column_text(stmt, 0));
        if (featureId != lastFeatureId) {
            lastFeatureId = featureId;

            curPSM_ = new PSM();
            curPSM_->charge = sqlite3_column_int(stmt, 4);
            string seq = boost::lexical_cast<string>(sqlite3_column_text(stmt, 3));
            if (!TSVReader::parseSequence(unimod_, seq, &(curPSM_->unmodSeq), &(curPSM_->mods))) {
                delete curPSM_;
                return false;
            }
            curPSM_->score = sqlite3_column_double(stmt, 11);
            curPSM_->specName = featureId;
            if (sqlite3_column_type(stmt, 6) == SQLITE_TEXT) {
                string proteinString = boost::lexical_cast<string>(sqlite3_column_text(stmt, 6));
                vector<string> proteinNames;
                boost::split(proteinNames, proteinString, boost::is_any_of(";"));
                for (vector<string>::const_iterator i = proteinNames.begin(); i != proteinNames.end(); i++) {
                    map<string, Protein>::const_iterator j = proteins.find(*i);
                    if (j != proteins.end()) {
                        curPSM_->proteins.insert(&j->second);
                    } else {
                        proteins[*i] = Protein(*i);
                        curPSM_->proteins.insert(&proteins[*i]);
                    }
                }
            }

            string filename = boost::lexical_cast<string>(sqlite3_column_text(stmt, 1));
            map< string, vector<PSM*> >::iterator i = fileMap_.find(filename);
            if (i == fileMap_.end()) {
                fileMap_[filename] = vector<PSM*>(1, curPSM_);
            } else {
                i->second.push_back(curPSM_);
            }

            transferPeaks(curSpectrum, curPeakMzs, curPeakIntensities); // set peaks for previous spectrum

            curSpectrum = &(spectra_.insert(make_pair(featureId, SpecData())).first->second);
            curSpectrum->retentionTime = sqlite3_column_double(stmt, 2)/60;
            curSpectrum->startTime = sqlite3_column_double(stmt, 7)/60;
            curSpectrum->endTime = sqlite3_column_double(stmt, 8)/60;
            curSpectrum->mz = sqlite3_column_double(stmt, 5);
        }
        curPeakMzs.push_back(sqlite3_column_double(stmt, 9));
        curPeakIntensities.push_back(sqlite3_column_double(stmt, 10));
    }
    transferPeaks(curSpectrum, curPeakMzs, curPeakIntensities); // set peaks for last spectrum

    if (sqlite3_finalize(stmt) != SQLITE_OK) {
        Verbosity::error("Error finalizing statement: %s", sqlite3_errmsg(osw_));
        return false;
    }

    Verbosity::debug("Building tables");
    initSpecFileProgress(fileMap_.size());
    for (map< string, vector<PSM*> >::iterator i = fileMap_.begin(); i != fileMap_.end(); i++) {
        psms_.assign(i->second.begin(), i->second.end());
        setSpecFileName(i->first.c_str(), false);
        buildTables(GENERIC_QVALUE, i->first, false);
    }

    return true;
}

vector<PSM_SCORE_TYPE> OSWReader::getScoreTypes() {
    return vector<PSM_SCORE_TYPE>(1, GENERIC_QVALUE);
}

void OSWReader::transferPeaks(SpecData* dst, vector<double>& mzs, vector<float>& intensities) {
    if (!dst) {
        return;
    } else if (mzs.size() != intensities.size()) {
        throw BlibException(false, "Number of m/zs %d did not match number of intensities %d",
                            mzs.size(), intensities.size());
    }

    dst->numPeaks = mzs.size();

    dst->mzs = new double[mzs.size()];
    std::copy(mzs.begin(), mzs.end(), dst->mzs);
    mzs.clear();

    dst->intensities = new float[intensities.size()];
    std::copy(intensities.begin(), intensities.end(), dst->intensities);
    intensities.clear();
}

bool OSWReader::getSpectrum(string identifier, SpecData& returnData, bool getPeaks) {
    map<string, SpecData>::const_iterator i = spectra_.find(identifier);
    if (i == spectra_.end()) {
        return false;
    }
    const SpecData& spectrum = i->second;

    returnData.retentionTime = spectrum.retentionTime;
    returnData.startTime = spectrum.startTime;
    returnData.endTime = spectrum.endTime;
    returnData.mz = spectrum.mz;
    returnData.numPeaks = spectrum.numPeaks;

    if (getPeaks) {
        returnData.mzs = new double[spectrum.numPeaks];
        memcpy(returnData.mzs, spectrum.mzs, spectrum.numPeaks*sizeof(double));
        returnData.intensities = new float[spectrum.numPeaks];
        memcpy(returnData.intensities, spectrum.intensities, spectrum.numPeaks*sizeof(float));
    }

    return true;
}

} // namespace
