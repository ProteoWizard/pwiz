//
// $Id$
//
//
// Original author: Kaipo Tamura <kaipot@uw.edu>
//
// Copyright 2016 University of Washington - Seattle, WA 98195
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

#include "ProxlXmlReader.h"
#include "AminoAcidMasses.h"


namespace BiblioSpec {

double ProxlXmlReader::aaMasses_[128];

ProxlXmlReader::ProxlXmlReader(BlibBuilder& maker, const char* filename, const ProgressIndicator* parentProgress)
    : BuildParser(maker, filename, parentProgress), curProxlPsm_(NULL) {
    setFileName(filename);
    AminoAcidMasses::initializeMass(aaMasses_, 1);
}

ProxlXmlReader::~ProxlXmlReader() {
}

bool ProxlXmlReader::parseFile() {
    return parse();
}

void ProxlXmlReader::startElement(const XML_Char* name, const XML_Char** attr) {
    if (state_.empty()) {
        if (isIElement("proxl_input", name)) {
            state_.push_back(ROOT_STATE);
        }
        return;
    }
    
    switch (state_.back()) {
    case ROOT_STATE:
        if (isIElement("reported_peptides", name)) {
            state_.push_back(REPORTED_PEPTIDES_STATE);
        } else if (isIElement("static_modifications", name)) {
            state_.push_back(STATIC_MODIFICATIONS_STATE);
        }
        break;
    case REPORTED_PEPTIDES_STATE:
        if (isIElement("reported_peptide", name) && strcmp(getRequiredAttrValue("type", attr), "looplink") != 0) {
            state_.push_back(REPORTED_PEPTIDE_STATE);

            proxlMatches_.push_back(ProxlMatches());
        }
        break;
    case REPORTED_PEPTIDE_STATE:
        if (isIElement("peptides", name)) {
            state_.push_back(PEPTIDES_STATE);
        } else if (isIElement("psms", name)) {
            state_.push_back(PSMS_STATE);
        }
        break;
    case PEPTIDES_STATE:
        if (isIElement("peptide", name)) {
            state_.push_back(PEPTIDE_STATE);

            proxlMatches_.back().peptides_.push_back(ProxlPeptide(getRequiredAttrValue("sequence", attr)));
        }
        break;
    case PEPTIDE_STATE:
        if (isIElement("modifications", name)) {
            state_.push_back(MODIFICATIONS_STATE);
        } else if (isIElement("linked_positions", name)) {
            state_.push_back(LINKED_POSITIONS_STATE);
        }
        break;
    case MODIFICATIONS_STATE:
        if (isIElement("modification", name)) {
            // 1-based positions
            proxlMatches_.back().peptides_.back().mods_.push_back(
                SeqMod(getIntRequiredAttrValue("position", attr), getDoubleRequiredAttrValue("mass", attr)));
        }
        break;
    case LINKED_POSITIONS_STATE:
        if (isIElement("linked_position", name)) {
            proxlMatches_.back().peptides_.back().links_.push_back(getIntRequiredAttrValue("position", attr));
        }
        break;
    case PSMS_STATE:
        if (isIElement("psm", name)) {
            state_.push_back(PSM_STATE);

            curProxlPsm_ = new ProxlPsm();
            string filename(getRequiredAttrValue("scan_file_name", attr));
            map< string, vector<ProxlPsm*> >::iterator i = proxlMatches_.back().psms_.find(filename);
            if (i == proxlMatches_.back().psms_.end()) {
                proxlMatches_.back().psms_[filename] = vector<ProxlPsm*>(1, curProxlPsm_);
            } else {
                i->second.push_back(curProxlPsm_);
            }
            // Set sequence/mods at reported_peptide end tag
            curProxlPsm_->charge = getIntRequiredAttrValue("precursor_charge", attr);
            curProxlPsm_->specKey = getIntRequiredAttrValue("scan_number", attr);
            curProxlPsm_->score = numeric_limits<double>::max();
            curProxlPsm_->linkerMass_ = getDoubleRequiredAttrValue("linker_mass", attr);
        }
        break;
    case PSM_STATE:
        if (isIElement("filterable_psm_annotations", name)) {
            state_.push_back(FILTERABLE_PSM_ANNOTATIONS_STATE);
        }
        break;
    case FILTERABLE_PSM_ANNOTATIONS_STATE:
        if (isIElement("filterable_psm_annotation", name)) {
            string program(getRequiredAttrValue("search_program", attr));
            string score(getRequiredAttrValue("annotation_name", attr));
            if (program == "percolator" && score == "q-value") {
                curProxlPsm_->score = getDoubleRequiredAttrValue("value", attr);
            }
        }
        break;
    case STATIC_MODIFICATIONS_STATE:
        if (isIElement("static_modification", name)) {
            string aa(getRequiredAttrValue("amino_acid", attr));
            double mass = getDoubleRequiredAttrValue("mass_change", attr);
            for (string::const_iterator i = aa.begin(); i != aa.end(); i++) {
                map< char, vector<double> >::iterator lookup = staticMods_.find(*i);
                if (lookup == staticMods_.end()) {
                    staticMods_[*i] = vector<double>(1, mass);
                } else {
                    lookup->second.push_back(mass);
                }
            }
        }
        break;
    }
}

void ProxlXmlReader::endElement(const XML_Char* name) {
    if (state_.empty())
        return;

    switch (state_.back()) {
    case ROOT_STATE:
        if (isIElement("proxl_input", name)) {
            state_.pop_back();

            calcPsms();
            for (map< string, vector<PSM*> >::iterator i = fileToPsms_.begin(); i != fileToPsms_.end(); i++) {
                psms_ = vector<PSM*>(i->second);
                i->second.clear();
                setSpecFileName(i->first, true);
                buildTables(PERCOLATOR_QVALUE);
            }
        }
        break;
    case REPORTED_PEPTIDES_STATE:
        if (isIElement("reported_peptides", name)) {
            state_.pop_back();
        }
        break;
    case REPORTED_PEPTIDE_STATE:
        if (isIElement("reported_peptide", name)) {
            state_.pop_back();
        }
        break;
    case PEPTIDES_STATE:
        if (isIElement("peptides", name)) {
            state_.pop_back();
        }
        break;
    case PEPTIDE_STATE:
        if (isIElement("peptide", name)) {
            state_.pop_back();
        }
        break;
    case MODIFICATIONS_STATE:
        if (isIElement("modifications", name)) {
            state_.pop_back();
        }
        break;
    case LINKED_POSITIONS_STATE:
        if (isIElement("linked_positions", name)) {
            state_.pop_back();
        }
        break;
    case PSMS_STATE:
        if (isIElement("psms", name)) {
            state_.pop_back();
        }
        break;
    case PSM_STATE:
        if (isIElement("psm", name)) {
            state_.pop_back();
        }
        break;
    case FILTERABLE_PSM_ANNOTATIONS_STATE:
        if (isIElement("filterable_psm_annotations", name)) {
            state_.pop_back();
        }
        break;
    case STATIC_MODIFICATIONS_STATE:
        if (isIElement("static_modifications", name)) {
            state_.pop_back();
        }
        break;
    }
}

void ProxlXmlReader::characters(const XML_Char *s, int len) {
}

double ProxlXmlReader::calcMass(const string& sequence, const vector<SeqMod>& mods) {
    double sum = 2*aaMasses_['h'] + aaMasses_['o'];
    for (string::const_iterator i = sequence.begin(); i != sequence.end(); i++)
        sum += aaMasses_[*i];
    for (vector<SeqMod>::const_iterator i = mods.begin(); i != mods.end(); i++)
        sum += i->deltaMass;
    return sum;
}

void ProxlXmlReader::calcPsms() {

    for (vector<ProxlMatches>::iterator match = proxlMatches_.begin(); match != proxlMatches_.end(); match++) {
		for (vector<ProxlPeptide>::iterator k = match->peptides_.begin(); k != match->peptides_.end(); k++) {
			applyStaticMods(k->sequence_, k->mods_);
		}
		for (map< string, vector<ProxlPsm*> >::iterator i = match->psms_.begin(); i != match->psms_.end(); i++) {
            map< string, vector<PSM*> >::iterator lookup = fileToPsms_.find(i->first);
            if (lookup == fileToPsms_.end()) {
                fileToPsms_[i->first] = vector<PSM*>();
                lookup = fileToPsms_.find(i->first);
            }
            for (vector<ProxlPsm*>::iterator j = i->second.begin(); j != i->second.end(); j++) {
                if ((*j)->score <= getScoreThreshold(SQT)) {
                    for (vector<ProxlPeptide>::iterator k = match->peptides_.begin(); k != match->peptides_.end(); k++) {
                        PSM* psm = new PSM();
                        *psm = **j;
                        psm->unmodSeq = k->sequence_;
                        psm->mods = k->mods_;
                        if (k->links_.size() == 1 && match->peptides_.size() == 2) {
                            const ProxlPeptide& other = (k == match->peptides_.begin())
                                ? match->peptides_[1]
                                : match->peptides_[0];
                            psm->mods.push_back(SeqMod(k->links_[0], other.mass() + (*j)->linkerMass_));
                        }
                        lookup->second.push_back(psm);
                    }
                }
                delete *j;
            }
        }
    }
}

void ProxlXmlReader::applyStaticMods(const string& sequence, vector<SeqMod>& mods) {
    for (int i = 0; i < sequence.length(); i++) {
        map< char, vector<double> >::const_iterator lookup = staticMods_.find(sequence[i]);
        if (lookup == staticMods_.end())
            continue;
        for (vector<double>::const_iterator j = lookup->second.begin(); j != lookup->second.end(); j++) {
            mods.push_back(SeqMod(i + 1, *j));
        }
    }
}

}
