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
#include "pwiz/utility/misc/Std.hpp"

namespace BiblioSpec {

double ProxlXmlReader::aaMasses_[128];

ProxlXmlReader::ProxlXmlReader(BlibBuilder& maker, const char* filename, const ProgressIndicator* parentProgress)
    : BuildParser(maker, filename, parentProgress), curProxlPsm_(NULL) {
    setFileName(filename);
    AminoAcidMasses::initializeMass(aaMasses_, 1);

    dirs_.push_back("../");   // look in parent dir in addition to cwd
    dirs_.push_back("../../");  // look in grandparent dir in addition to cwd
    extensions_.push_back(".mz5"); // look for spec in mz5 files
    extensions_.push_back(".mzML"); // look for spec in mzML files
    extensions_.push_back(".mzXML"); // look for spec in mzXML files
    extensions_.push_back(".ms2");
    extensions_.push_back(".cms2");
    extensions_.push_back(".bms2");
    extensions_.push_back(".pms2");
}

ProxlXmlReader::~ProxlXmlReader() {
}

bool ProxlXmlReader::parseFile() {
    return parse();
}

vector<PSM_SCORE_TYPE> ProxlXmlReader::getScoreTypes() {
    return vector<PSM_SCORE_TYPE>(1, PERCOLATOR_QVALUE);
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
        if (isIElement("reported_peptide", name)) {
            state_.push_back(REPORTED_PEPTIDE_STATE);

            proxlMatches_.push_back(ProxlMatches());
            string type = getRequiredAttrValue("type", attr);
            if (type == "unlinked")
                proxlMatches_.back().linkType_ = LinkType::Unlinked;
            else if (type == "crosslink")
                proxlMatches_.back().linkType_ = LinkType::Crosslink;
            else if (type == "looplink")
                proxlMatches_.back().linkType_ = LinkType::Looplink;
            else
                proxlMatches_.back().linkType_ = LinkType::Other;
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
            curProxlPsm_->score = 1;
            curProxlPsm_->linkerMass_ = proxlMatches_.back().linkType_ != LinkType::Unlinked ? getDoubleRequiredAttrValue("linker_mass", attr) : 0;
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
                setSpecFileName(i->first, extensions_, dirs_);
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
    boost::format modSeqCrosslinkFormat("%s-%s-[%+.4f@%d,%d]");
    boost::format modSeqLooplinkFormat("%s-[%+.4f@%d-%d]");

    for (auto& match : proxlMatches_) {
        for (auto& peptide : match.peptides_)
            applyStaticMods(peptide.sequence_, peptide.mods_, peptide.links_.empty() ? -1 : peptide.links_[0]);

        for (auto& psmPair : match.psms_) {
            map< string, vector<PSM*> >::iterator lookup = fileToPsms_.find(psmPair.first);
            if (lookup == fileToPsms_.end()) {
                fileToPsms_[psmPair.first] = vector<PSM*>();
                lookup = fileToPsms_.find(psmPair.first);
            }
            for (auto& proxlPsm : psmPair.second) {
                if (proxlPsm->score <= getScoreThreshold(SQT)) {
                    switch (match.linkType_)
                    {
                        case LinkType::Unlinked:
                        {
                            if (match.peptides_.size() != 1)
                                throw runtime_error("[calcPsms] unexpected number of peptides in unlinked peptide: " + match.peptides_.size());

                            auto& pepA = match.peptides_[0];

                            PSM* psm = new PSM();
                            *psm = *proxlPsm;
                            psm->unmodSeq = pepA.sequence_;
                            psm->mods = pepA.mods_;
                            lookup->second.push_back(psm);
                        }
                        break;

                        case LinkType::Crosslink:
                        {
                            if (match.peptides_.size() != 2)
                                throw runtime_error("[calcPsms] unexpected number of peptides in crosslink: " + match.peptides_.size());

                            auto& pepA = match.peptides_[0];
                            auto& pepB = match.peptides_[1];
                            if (pepA.links_.size() != 1 || pepB.links_.size() != 1)
                                throw runtime_error("[calcPsms] unexpected number of links on crosslink: " + pepA.sequence_ + "/" + pepB.sequence_ + " " + lexical_cast<string>(pepA.links_.size()) + "/" + lexical_cast<string>(pepB.links_.size()));

                            PSM* psm = new PSM();
                            *psm = *proxlPsm;
                            psm->unmodSeq = pepA.sequence_ + "-" + pepB.sequence_;
                            string modifiedPepA = blibMaker_.generateModifiedSeq(pepA.sequence_.c_str(), pepA.mods_);
                            string modifiedPepB = blibMaker_.generateModifiedSeq(pepB.sequence_.c_str(), pepB.mods_);
                            psm->modifiedSeq = (modSeqCrosslinkFormat % modifiedPepA % modifiedPepB % proxlPsm->linkerMass_ % pepA.links_[0] % pepB.links_[0]).str();
                            psm->mods = pepA.mods_;
                            psm->mods.push_back(SeqMod(pepA.links_[0], pepB.mass() + proxlPsm->linkerMass_));
                            lookup->second.push_back(psm);
                        }
                        break;

                        case LinkType::Looplink:
                        {
                            if (match.peptides_.size() != 1)
                                throw runtime_error("[calcPsms] unexpected number of peptides in looplink: " + match.peptides_.size());

                            auto& pepA = match.peptides_[0];
                            if (pepA.links_.size() != 2)
                                throw runtime_error("[calcPsms] unexpected number of links on looplink: " + pepA.sequence_ + " " + lexical_cast<string>(pepA.links_.size()));

                            PSM* psm = new PSM();
                            *psm = *proxlPsm;
                            psm->unmodSeq = pepA.sequence_;
                            psm->modifiedSeq = blibMaker_.generateModifiedSeq(psm->unmodSeq.c_str(), pepA.mods_);
                            psm->modifiedSeq = (modSeqLooplinkFormat % pepA.sequence_ % proxlPsm->linkerMass_ % pepA.links_[0] % pepA.links_[1]).str();
                            psm->mods = pepA.mods_;
                            psm->mods.push_back(SeqMod(pepA.links_[0], proxlPsm->linkerMass_));
                            lookup->second.push_back(psm);
                        }
                        break;

                        case LinkType::Other:
                        default:
                            break;
                    }
                }
                delete proxlPsm;
            }
        }
    }
}

void ProxlXmlReader::applyStaticMods(const string& sequence, vector<SeqMod>& mods, int crosslinkPosition) {
    size_t varModCount = mods.size();
    for (int i = 0; i < sequence.length(); i++) {
        // CONSIDER: what is correct behavior for static mods on crosslink positions?
        //if (i + 1 == crosslinkPosition) // skip all static mods on crosslink position (e.g. C+57)
        //    continue;

        map< char, vector<double> >::const_iterator lookup = staticMods_.find(sequence[i]);
        if (lookup == staticMods_.end())
            continue;
        for (vector<double>::const_iterator j = lookup->second.begin(); j != lookup->second.end(); j++) {
            mods.push_back(SeqMod(i + 1, *j));
        }
    }

    // if static mods were added, sort all mods by position
    if (mods.size() > varModCount)
        sort(mods.begin(), mods.end(), [](const auto& lhs, const auto& rhs) { return lhs.position < rhs.position; });
}

}
