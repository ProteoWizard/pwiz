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
 * The mzTabReader parses the PSMs from the mzTab file
 * and stores each record. Records are grouped by file. Spectra are then
 * retrieved from the spectrum files.
 */

#include "mzTabReader.h"

using namespace std;
using namespace boost;

namespace BiblioSpec {

const string mzTabReader::NULL_FIELD = "null";
const string mzTabReader::PSM_CHARGE_FIELD = "charge";
const string mzTabReader::PSM_SEQ_FIELD = "sequence";
const string mzTabReader::PSM_MODS_FIELD = "modifications";
const string mzTabReader::PSM_SPEC_FIELD = "spectra_ref";
const string mzTabReader::PSM_SCORE_FIELD = "search_engine_score"; // search_engine_score[1-n]

mzTabReader::mzTabReader(BlibBuilder& maker,
                         const char* filename,
                         const ProgressIndicator* parentProgress)
 : BuildParser(maker, filename, parentProgress), filename_(filename), lineNum_(0), scoreIdxFile_(0) {
    // initialize acceptable score types: <score string, higher score better, psm score type, build input>
    // priority is high to low (i.e. if multiple acceptable score types are found, the one with the lowest index in scoreTypes_ is used)
    scoreTypes_.push_back(boost::make_tuple(
        "MS:1001491, percolator:Q value", false, PERCOLATOR_QVALUE, SQT));
    scoreTypes_.push_back(boost::make_tuple(
        "MS:1001330, X!Tandem:expect", false, TANDEM_EXPECTATION_VALUE, TANDEM));
    scoreTypes_.push_back(boost::make_tuple(
        "MS:1001172, Mascot:expectation value", false, MASCOT_IONS_SCORE, MASCOT));
    scoreTypes_.push_back(boost::make_tuple(
        "MS:1001901, MaxQuant:PEP", false, MAXQUANT_SCORE, MAXQUANT));
    scoreTypes_.push_back(boost::make_tuple(
        "MS:1002054, MS-GF:QValue", false, MSGF_SCORE, MSGF));
    scoreTypes_.push_back(boost::make_tuple(
        "MS:1002354, PSM-level q-value", false, GENERIC_QVALUE, GENERIC_QVALUE_INPUT));
    scoreIdxVector_ = scoreTypes_.size();
}

mzTabReader::~mzTabReader() {
    if (file_.is_open()) {
        file_.close();
    }
}

bool mzTabReader::parse() {
    Verbosity::debug("Parsing Unimod");
    string unimodFile = getExeDirectory() + "unimod.xml";
    unimod_.setFile(unimodFile.c_str());
    unimod_.parse();
    Verbosity::debug("Successfully parsed %d Unimod records", unimod_.numMods());

    Verbosity::debug("Opening file");
    file_.open(filename_.c_str());
    if (!file_.is_open()) {
        return false;
    }
    Verbosity::debug("Collecting PSMs");
    collectPsms();
}

bool mzTabReader::parseFile() {
    parse();
    Verbosity::debug("Building tables");
    initSpecFileProgress(fileMap_.size());
    for (map< string, vector<PSM*> >::iterator i = fileMap_.begin(); i != fileMap_.end(); i++) {
        psms_.assign(i->second.begin(), i->second.end());
        if (filesystem::exists(i->first)) {
            setSpecFileName(i->first.c_str(), false);
        } else {
            filesystem::path p(i->first);
            setSpecFileName(p.filename().string().c_str(), true);
        }
        buildTables(scoreTypes_[scoreIdxVector_].get<2>(), i->first, false);
    }
    return true;
}

vector<PSM_SCORE_TYPE> mzTabReader::getScoreTypes() {
    if (!parse()) {
        return vector<PSM_SCORE_TYPE>(1, UNKNOWN_SCORE_TYPE);
    }
    set<PSM_SCORE_TYPE> allScoreTypes;
    for (map< string, vector<PSM*> >::iterator i = fileMap_.begin(); i != fileMap_.end(); i++) {
        allScoreTypes.insert(scoreTypes_[scoreIdxVector_].get<2>());
    }
    return vector<PSM_SCORE_TYPE>(allScoreTypes.begin(), allScoreTypes.end());
}

void mzTabReader::collectPsms() {
    streampos originalPos = file_.tellg();
    ProgressIndicator progress(count(istreambuf_iterator<char>(file_),
                                     istreambuf_iterator<char>(), '\n') + 1);
    file_.seekg(originalPos);

    while (!file_.eof()) {
        string line;
        getline(file_, line);
        lineNum_++;
        parseLine(line);
        progress.increment();
    }
}

void mzTabReader::parseLine(const string& line) {
    vector<string> fields;
    split(fields, line, is_any_of("\t"));
    if (fields.empty()) {
        return;
    }
    const string fieldType = fields[0];
    if (fieldType == "MTD") { // metadata
        if (fields.size() == 3) {
            const string filePrefix = "ms_run[";
            const string fileSuffix = "]-location";
            const string scoreNumPrefix = "psm_search_engine_score[";
            const string scoreNumSuffix = "]";
            if (starts_with(fields[1], filePrefix) && ends_with(fields[1], fileSuffix)) {
                string runStr = fields[1].substr(filePrefix.size(), fields[1].size() - filePrefix.size() - fileSuffix.size());
                string runFile = fields[2];
                const string fileLocationPrefix = "file://";
                if (starts_with(runFile, fileLocationPrefix)) {
                    runFile.erase(0, fileLocationPrefix.size());
                }
                try {
                    runs_[lexical_cast<int>(runStr)] = runFile;
                } catch (bad_lexical_cast&) {
                    throw BlibException(false, "Invalid file number '%s' at line %d", runStr.c_str(), lineNum_);
                }
            } else if (starts_with(fields[1], scoreNumPrefix) && ends_with(fields[1], scoreNumSuffix)) {
                string searchEngine = fields[2];
                for (size_t i = 0; i < scoreIdxVector_; i++) {
                    const boost::tuple<string, bool, PSM_SCORE_TYPE, BUILD_INPUT>& scoreType = scoreTypes_[i];
                    if (searchEngine.find(scoreType.get<0>()) != string::npos) {
                        scoreIdxVector_ = i;
                        string scoreNum = fields[1].substr(scoreNumPrefix.size(), fields[1].size() - scoreNumPrefix.size() - scoreNumSuffix.size());
                        try {
                            scoreIdxFile_ = lexical_cast<int>(scoreNum);
                        } catch (bad_lexical_cast&) {
                            throw BlibException(false, "Invalid search engine score number '%s' at line %d",
                                                scoreNum.c_str(), lineNum_);
                        }
                        break;
                    }
                }
            }
        }
    } else if (fieldType == "PSH") { // psm table header
        if (!psh_.empty()) {
            throw BlibException(false, "Multiple PSH lines found at line %d", lineNum_);
        } else if (scoreIdxVector_ >= scoreTypes_.size()) {
            throw BlibException(false, "No acceptable score type found", lineNum_);
        }
        for (size_t i = 0; i < fields.size(); i++) {
            psh_[fields[i]] = i;
        }
        vector<string> required;
        required.push_back(PSM_CHARGE_FIELD);
        required.push_back(PSM_SEQ_FIELD);
        required.push_back(PSM_MODS_FIELD);
        required.push_back(PSM_SPEC_FIELD);
        for (vector<string>::const_iterator i = required.begin(); i != required.end(); i++) {
            if (psh_.find(*i) == psh_.end()) {
                throw BlibException(false, "PSH line missing required field '%s' at line %d", i->c_str(), lineNum_);
            }
        }
        Verbosity::status("Using score '%s'", scoreTypes_[scoreIdxVector_].get<0>().c_str());
    } else if (fieldType == "PSM") { // psm
        if (psh_.empty()) {
            throw BlibException(false, "No PSH line found before PSM at line %d", lineNum_);
        } else if (psh_.size() != fields.size()) {
            throw BlibException(false, "PSH line had %d fields, but PSM at line %d had %d",
                                psh_.size(), lineNum_, fields.size());
        }
        int charge;
        parseCharge(fields, charge);
        string seq;
        vector<SeqMod> mods;
        if (!parseSequence(fields, seq, mods)) {
            return;
        }
        vector< pair<string, string> > spectra;
        parseSpectrum(fields, spectra);        
        double score;
        if (!parseScore(fields, score)) {
            return;
        }
        const string scanPrefix = "scan=";
        const string indexPrefix = "index=";
        for (vector< pair<string, string> >::const_iterator i = spectra.begin(); i != spectra.end(); i++) {
            curPSM_ = new PSM();
            curPSM_->charge = charge;
            curPSM_->unmodSeq = seq;
            curPSM_->mods = mods;
            curPSM_->score = score;
            if (starts_with(i->second, scanPrefix)) {
                string scanStr = i->second.substr(scanPrefix.size());
                try {
                    curPSM_->specKey = lexical_cast<int>(scanStr);
                } catch (bad_lexical_cast&) {
                    throw BlibException(false, "Invalid scan '%s' on line %d", scanStr.c_str(), lineNum_);
                }
            } else if (starts_with(i->second, indexPrefix)) {
                string indexStr = i->second.substr(indexPrefix.size());
                try {
                    curPSM_->specIndex = lexical_cast<int>(indexStr);
                } catch (bad_lexical_cast&) {
                    throw BlibException(false, "Invalid index '%s' on line %d", indexStr.c_str(), lineNum_);
                }
            } else {
                curPSM_->specName = i->second;
            }
            map< string, vector<PSM*> >::iterator j = fileMap_.find(i->first);
            if (j == fileMap_.end()) {
                fileMap_[i->first] = vector<PSM*>(1, curPSM_);
            } else {
                j->second.push_back(curPSM_);
            }
        }
    }
}

void mzTabReader::parseCharge(const vector<string>& fields, int& outCharge) {
    try {
        outCharge = lexical_cast<int>(fields[psh_[PSM_CHARGE_FIELD]]);
    } catch (bad_lexical_cast&) {
        throw BlibException(false, "Invalid charge '%s' on line %d",
                            fields[psh_[PSM_CHARGE_FIELD]].c_str(), lineNum_);
    }
}

bool mzTabReader::parseSequence(const vector<string>& fields, string& outSequence, vector<SeqMod>& outMods) {
    outSequence = fields[psh_[PSM_SEQ_FIELD]];
    
    outMods.clear();
    const string& modStr = fields[psh_[PSM_MODS_FIELD]];
    if (modStr == NULL_FIELD) { // no modifications
        return true;
    }

    // modifications are "{position}{parameter}-[{identifier}|{neutral loss}]", separated by commas
    // position is 1-based (0 = n-terminal); if position is ambiguous, multiple positions are separated by '|'
    // parameter (optional) may be used to represent a numerical value (e.g. probability score)
    // identifier may be "UNIMOD:18" (unimod) or "MOD:00815" (psi-mod)
    // neutral losses (optional) are reported as cvParams, e.g. "[MS, MS:1001524, fragment neutral loss, 63.998285]; position is optional if modification is neutral loss
    const string unimodPrefix = "UNIMOD:";
    const string psimodPrefix = "MOD:";
    for (size_t i = 0; i < modStr.size(); i++) {
        if (modStr[i] == '[') {
            Verbosity::warn("Ignoring PSM containing positionless modification");
            return false;
        } else if (modStr[i] < '0' || modStr[i] > '9') {
            throw BlibException(false, "Invalid modification format on line %d, expected position (%s)", lineNum_, modStr.c_str());
        }
        size_t start = i;
        for ( ; i < modStr.size() && '0' <= modStr[i] && modStr[i] <= '9'; i++);
        if (i >= modStr.size()) {
            throw BlibException(false, "Invalid modification format on line %d (%s)", lineNum_, modStr.c_str());
        }
        string posStr = modStr.substr(start, i - start);
        int pos;
        try {
            pos = lexical_cast<int>(posStr);
        } catch (bad_lexical_cast&) {
            throw BlibException(false, "Invalid mod position '%s' on line %d (%s)", posStr.c_str(), lineNum_, modStr.c_str());
        }
        if (pos < 1) {
            pos = 1;
        } else if (pos > outSequence.size()) {
            pos = outSequence.size();
        }
        if (modStr[i] == '[') { // parameter, skip past it
            for ( ; i < modStr.size() && modStr[i] != ']'; i++);
            if (i++ >= modStr.size()) {
                throw BlibException(false, "Invalid modification format on line %d (%s)", lineNum_, modStr.c_str());
            }
        }
        double mass;
        if (i >= modStr.size() || modStr[i] != '-') {
            throw BlibException(false, "Invalid modification format on line %d, expected '-' (%s)", lineNum_, modStr.c_str());
        } else if (++i >= modStr.size()) {
            throw BlibException(false, "Invalid modification format on line %d, expected identifier (%s)", lineNum_, modStr.c_str());
        } else if (modStr[i] == '[') {
            for ( ; i < modStr.size() && modStr[i] != ']'; i++);
            if (i++ >= modStr.size()) {
                throw BlibException(false, "Invalid modification format on line %d (%s)", lineNum_, modStr.c_str());
            } else if (i >= modStr.size() || modStr[i] != ',') {
                throw BlibException(false, "Invalid modification format on line %d, expected ',' or end (%s)", lineNum_, modStr.c_str());
            }
            // TODO Implement support
            throw BlibException(false, "Neutral losses not yet supported. Line %d (%s)", lineNum_, modStr.c_str());
        } else if (modStr.substr(i, unimodPrefix.size()) == unimodPrefix) {
            i += unimodPrefix.size();
            start = i;
            for ( ; i < modStr.size() && modStr[i] != ','; i++);
            string idStr = modStr.substr(start, i - start);
            int id;
            try {
                id = lexical_cast<int>(idStr);
            } catch (bad_lexical_cast&) {
                throw BlibException(false, "Invalid Unimod ID '%s' on line %d (%s)", idStr.c_str(), lineNum_, modStr.c_str());
            }
            if (!unimod_.hasMod(id)) {
                Verbosity::warn("Unrecognized Unimod ID %d on line %d", id, lineNum_);
                return false;
            }
            mass = unimod_.getModMass(id);
        } else if (modStr.substr(i, psimodPrefix.size()) == psimodPrefix) {
            i += psimodPrefix.size();
            start = i;
            for ( ; i < modStr.size() && modStr[i] != ','; i++);
            string idStr = modStr.substr(start, i - start);
            int id;
            try {
                id = lexical_cast<int>(idStr);
            } catch (bad_lexical_cast&) {
                throw BlibException(false, "Invalid PSI-MOD ID '%s' on line %d (%s)", idStr.c_str(), lineNum_, modStr.c_str());
            }
            // TODO Implement support
            throw BlibException(false, "PSI-MOD modifications not yet supported. Line %d (%s)", lineNum_, modStr.c_str());
        }
        outMods.push_back(SeqMod(pos, mass));
    }
    return true;
}

void mzTabReader::parseSpectrum(const vector<string>& fields, vector< pair<string, string> >& outSpectra) {
    const string runPrefix = "ms_run[";
    const string runSuffix = "]:";
    const string& specStr = fields[psh_[PSM_SPEC_FIELD]];
    // "ms_run[#]:{spectra_ref}", multiple references are separated by '|'
    // spectra_ref can be one of multiple formats:
    //   thermo nativeID: "controllerType=# controllerNumber=# scan=#"
    //   waters nativeID: "function=# process=# scan=#"
    //   wiff nativeID: "sample=# period=# cycle=# experiment=#"
    //   scan number: "scan=#"
    //   file: "file=id_ref"
    //   index: "index=#"
    //   spectrum: "spectrum=#"
    //   mzML: "spectrum_id"

    vector<string> specs;
    split(specs, specStr, is_any_of("|"));
    for (vector<string>::const_iterator i = specs.begin(); i != specs.end(); i++) {
        if (!starts_with(*i, runPrefix)) {
            throw BlibException(false, "Invalid spectrum reference '%s' on line %d, must begin with '%s'",
                                i->c_str(), lineNum_, runPrefix.c_str());
        }
        string spec = i->substr(runPrefix.size());
        size_t j = spec.find(runSuffix);
        if (j == string::npos) {
            throw BlibException(false, "Invalid spectrum reference '%s' on line %d", i->c_str(), lineNum_);
        }
        string runStr = spec.substr(0, j);
        int run;
        try {
            run = lexical_cast<int>(runStr);
        } catch (bad_lexical_cast&) {
            throw BlibException(false, "Invalid file number '%s' in spectrum reference '%s' on line %d",
                                runStr.c_str(), i->c_str(), lineNum_);
        }
        map<int, string>::const_iterator runIter = runs_.find(run);
        if (runIter == runs_.end()) {
            throw BlibException(false, "Unknown ms_run %d in spectrum reference '%s' on line %d",
                                run, i->c_str(), lineNum_);
        }
        outSpectra.push_back(make_pair(runIter->second, spec.substr(j + runSuffix.size())));
    }
}

bool mzTabReader::parseScore(const vector<string>& fields, double& outScore) {
    map<string, size_t>::const_iterator i = psh_.find(PSM_SCORE_FIELD + "[" + lexical_cast<string>(scoreIdxFile_) + "]");
    if (i == psh_.end()) {
        throw BlibException(false, "Missing score %d on line %d", scoreIdxFile_, lineNum_);
    }
    try {
        outScore = lexical_cast<double>(fields[i->second]);
    } catch (bad_lexical_cast&) {
        throw BlibException(false, "Invalid score '%s' on line %d", fields[i->second].c_str(), lineNum_);
    }
    if (scoreTypes_[scoreIdxVector_].get<1>()) { // higher score is better
        if (outScore < getScoreThreshold(scoreTypes_[scoreIdxVector_].get<3>())) {
            return false;
        }
    } else { // lower score is better
        if (outScore > getScoreThreshold(scoreTypes_[scoreIdxVector_].get<3>())) {
            return false;
        }
    }
    return true;
}

} // namespace
