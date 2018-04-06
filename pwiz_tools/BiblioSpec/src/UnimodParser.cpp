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

#include "UnimodParser.h"
#include <limits>

using namespace boost;

namespace BiblioSpec {

UnimodRecord::UnimodRecord()
    : monoMass_(numeric_limits<double>::quiet_NaN()) {
}

UnimodRecord::UnimodRecord(const UnimodRecord& other)
    : monoMass_(other.monoMass_) {
}

UnimodRecord::~UnimodRecord() {
}

UnimodRecord& UnimodRecord::operator=(UnimodRecord other) {
    swap(*this, other);
    return *this;
}

void swap(UnimodRecord& x, UnimodRecord& y) {
    using std::swap;
    swap(x.monoMass_, y.monoMass_);
}

UnimodParser::UnimodParser() {
}

UnimodParser::UnimodParser(const char* xmlfilename) {
    setFileName(xmlfilename);
}

UnimodParser::~UnimodParser() {
}

void UnimodParser::setFile(const char* xmlfilename) {
    setFileName(xmlfilename);
}

void UnimodParser::startElement(const XML_Char* name, const XML_Char** attr) {
    if (state_.empty()) {
        if (isElement("umod:modifications", name)) {
            state_.push(MODIFICATIONS_STATE);
        }
        return;
    }
    switch (state_.top()) {
    case MODIFICATIONS_STATE:
        if (isElement("umod:mod", name)) {
            state_.push(MOD_STATE);
            int id = getIntRequiredAttrValue("record_id", attr);
            currentRecord_ = records_.insert(make_pair(id, UnimodRecord())).first;
        }
        break;
    case MOD_STATE:
        if (isElement("umod:delta", name)) {
            currentRecord_->second.monoMass_ = getDoubleRequiredAttrValue("mono_mass", attr);
        }
    }
}

void UnimodParser::endElement(const XML_Char* name) {
    if (state_.empty()) {
        return;
    }
    switch (state_.top()) {
    case MODIFICATIONS_STATE:
        if (isElement("umod:modifications", name)) {
            state_.pop();
        }
        break;
    case MOD_STATE:
        if (isElement("umod:mod", name)) {
            state_.pop();
            if (currentRecord_->second.monoMass_ == numeric_limits<double>::quiet_NaN()) {
                records_.erase(currentRecord_);
            }
        }
        break;
    }
}

void UnimodParser::characters(const XML_Char *s, int len) {
}

size_t UnimodParser::numMods() const {
    return records_.size();
}

bool UnimodParser::hasMod(int id) const {
    return records_.find(id) != records_.end();
}

double UnimodParser::getModMass(int id) const {
    map<int, UnimodRecord>::const_iterator i = records_.find(id);
    return i != records_.end() ? i->second.monoMass_ : numeric_limits<double>::quiet_NaN();
}

} // namespace

