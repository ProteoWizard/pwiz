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

using namespace std;
using namespace boost;

namespace BiblioSpec {

TSVReader::TSVReader(BlibBuilder& maker,
                     const char* tsvName,
                     const ProgressIndicator* parentProgress)
  : BuildParser(maker, tsvName, parentProgress), tsvName_(tsvName), masses_(new double[128]),
    scoreThreshold_(getScoreThreshold(GENERIC_QVALUE_INPUT)), lineNum_(1), separator_("", "\t", "")
{
    setSpecFileName(tsvName, false);

    // point to self as spec reader
    delete specReader_;
    specReader_ = this;

    AminoAcidMasses::initializeMass(masses_, 1);

    targetColumns_.push_back(TSVColumnTranslator("filename", TSVLine::insertFilename));
    targetColumns_.push_back(TSVColumnTranslator("RT", TSVLine::insertRt));
    targetColumns_.push_back(TSVColumnTranslator("FullPeptideName", TSVLine::insertSequence));
    targetColumns_.push_back(TSVColumnTranslator("Charge", TSVLine::insertCharge));
    targetColumns_.push_back(TSVColumnTranslator("m/z", TSVLine::insertMz));
    targetColumns_.push_back(TSVColumnTranslator("decoy", TSVLine::insertDecoy));
    targetColumns_.push_back(TSVColumnTranslator("aggr_Peak_Area", TSVLine::insertPeakArea));
    targetColumns_.push_back(TSVColumnTranslator("aggr_Fragment_Annotation", TSVLine::insertFragmentAnnotation));
    targetColumns_.push_back(TSVColumnTranslator("m_score", TSVLine::insertScore));

    optionalColumns_.push_back(TSVColumnTranslator("ProteinName", TSVLine::insertProteinName));
    optionalColumns_.push_back(TSVColumnTranslator("leftWidth", TSVLine::insertLeftWidth));
    optionalColumns_.push_back(TSVColumnTranslator("rightWidth", TSVLine::insertRightWidth));
}

TSVReader::~TSVReader() {
    specReader_ = NULL; // so parent class doesn't try to delete itself
    if (tsvFile_.is_open()) {
        tsvFile_.close();
    }
}

bool TSVReader::parseFile() {
    Verbosity::debug("Parsing Unimod");
    string unimodFile = getExeDirectory() + "unimod.xml";
    unimod_.setFile(unimodFile.c_str());
    unimod_.parse();
    Verbosity::debug("Successfully parsed %d Unimod records", unimod_.numMods());

    Verbosity::debug("Opening file");
    tsvFile_.open(tsvName_.c_str());
    if (!tsvFile_.is_open()) {
        return false;
    }

    Verbosity::debug("Parsing header");
    parseHeader();

    Verbosity::debug("Collecting PSMs");
    map<string, Protein> proteins;
    collectPsms(proteins);

    Verbosity::debug("Building tables");
    initSpecFileProgress(fileMap_.size());
    for (map< string, vector<TSVPSM*> >::iterator i = fileMap_.begin(); i != fileMap_.end(); i++) {
        psms_.assign(i->second.begin(), i->second.end());
        setSpecFileName(i->first.c_str(), false);
        buildTables(GENERIC_QVALUE, i->first, false);
    }

    return true;
}

void TSVReader::parseHeader() {
    string line;
    getline(tsvFile_, line);
    LineParser parser(line, separator_);

    // get the column index of each required column
    int colNumber = 0;
    for (LineParser::iterator i = parser.begin(); i != parser.end(); i++) {
        bool found = false;
        for (vector<TSVColumnTranslator>::iterator j = targetColumns_.begin(); j != targetColumns_.end(); j++) {
            if (iequals(*i, j->name_)) {
                found = true;
                j->position_ = colNumber;
                break;
            }
        }
        if (!found) {
            for (vector<TSVColumnTranslator>::iterator j = optionalColumns_.begin(); j != optionalColumns_.end(); j++) {
                if (iequals(*i, j->name_)) {
                    found = true;
                    j->position_ = colNumber;
                    targetColumns_.push_back(*j);
                    break;
                }
            }
        }
        colNumber++;
    }

    // check that all required columns were in the file
    for (vector<TSVColumnTranslator>::iterator i = targetColumns_.begin(); i != targetColumns_.end(); i++) {
        if (i->position_ < 0) {
            if (i->name_ != "m_score") {
                throw BlibException(false, "Did not find required column '%s'. Only OpenSWATH .tsv files are supported.", i->name_.c_str());
            } else {
                if (scoreThreshold_ < 1) {
                    throw BlibException(false,
                        "Did not find required column '%s'. You may set the cut-off score to 0 to "
                        "force building the library without scores.", i->name_.c_str());
                } else {
                    vector<TSVColumnTranslator>::iterator tmp = i - 1;
                    targetColumns_.erase(i);
                    i = tmp;
                }
            }
        }
    }

    // sort by column number so they can be fetched in order
    sort(targetColumns_.begin(), targetColumns_.end());
}

void TSVReader::collectPsms(map<string, Protein>& proteins) {
    streampos originalPos = tsvFile_.tellg();
    ProgressIndicator progress(count(istreambuf_iterator<char>(tsvFile_),
                                     istreambuf_iterator<char>(), '\n') + 1);
    tsvFile_.seekg(originalPos);

    while (!tsvFile_.eof()) {
        string lineStr;
        getline(tsvFile_, lineStr);
        lineNum_++;

        TSVLine line;
        int col = 0;
        size_t targetIdx = 0;
        try {
            LineParser parser(lineStr, separator_);
            for (LineParser::iterator i = parser.begin(); i != parser.end(); i++) {
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
        progress.increment();
    }
}

void TSVReader::storeLine(const TSVLine& line, map<string, Protein>& proteins) {
    if (line.decoy) {
        Verbosity::comment(V_DETAIL, "Not saving decoy PSM (line %d)", lineNum_);
        return;
    } else if (line.score > scoreThreshold_) {
        Verbosity::comment(V_DETAIL, "Not saving PSM with score %f (line %d)", line.score, lineNum_);
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
            for (vector<string>::const_iterator i = proteinNames.begin() + 1; i != proteinNames.end(); i++) {
                map<string, Protein>::const_iterator j = proteins.find(*i);
                if (j != proteins.end()) {
                    psm->proteins.insert(&j->second);
                } else {
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
    } else {
        fileMap_[line.filename].push_back(psm);
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

bool TSVReader::parsePeaks(
    const string& peakArea,
    const string& fragmentAnnotation,
    vector<double>* mz,
    vector<double>* intensity
) {
    if (mz != NULL) {
        mz->clear();
    }
    if (intensity != NULL) {
        intensity->clear();
    }

    vector<string> parts;

    split(parts, peakArea, is_any_of(";"));
    for (vector<string>::const_iterator i = parts.begin(); i != parts.end(); i++) {
        double area;
        try {
            area = lexical_cast<double>(*i);
        } catch (bad_lexical_cast&) {
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
    for (vector<string>::const_iterator i = parts.begin(); i != parts.end(); i++) {
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
        } catch (bad_lexical_cast&) {
            Verbosity::error("Unexpected format for fragment annotations on line %d: %s", lineNum_, i->c_str());
            return false;
        }
        double ionMz;
        if (!calcIonMz(seq, mods, parts2[1][0], ionNum, ionCharge, &ionMz)) {
            return false;
        } else if (mz != NULL) {
            mz->push_back(ionMz);
        }
    }

    return true;
}

bool TSVReader::calcIonMz(
    const string& seq,
    const vector<SeqMod>& mods,
    char ionType,
    int ionNum,
    int ionCharge,
    double* ionMz
) {
    if (ionMz != NULL) {
        *ionMz = ionCharge * PROTON_MASS;
    } else if (ionNum < 1 || ionNum > seq.length()) {
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
            *ionMz += 2*masses_['h'] + masses_['o'];
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
        } else {
            Verbosity::error("Invalid character '%c' in sequence '%s' on line %d", c, seq.c_str(), lineNum_);
            return false;
        }
    }
    for (vector<SeqMod>::const_iterator i = mods.begin(); i != mods.end(); i++) {
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

