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

#pragma once

#include <map>
#include "saxhandler.h"

namespace BiblioSpec {

class UnimodRecord {
public:
    UnimodRecord();
    UnimodRecord(const UnimodRecord& other);
    ~UnimodRecord();

    UnimodRecord& operator=(UnimodRecord other);

    friend void swap(UnimodRecord& x, UnimodRecord& y);

    double monoMass_;
};

class UnimodParser : public SAXHandler {
public:
    UnimodParser();
    UnimodParser(const char* xmlfilename);
    ~UnimodParser();

    void setFile(const char* xmlfilename);

    virtual void startElement(const XML_Char* name, const XML_Char** attr);
    virtual void endElement(const XML_Char* name);
    virtual void characters(const XML_Char* s, int len);

    size_t numMods() const;
    bool hasMod(int id) const;
    double getModMass(int id) const;

private:
    enum STATE {
        MODIFICATIONS_STATE, MOD_STATE
    };

    std::stack<STATE> state_;
    std::map<int, UnimodRecord> records_;
    std::map<int, UnimodRecord>::iterator currentRecord_;
};

} // namespace

