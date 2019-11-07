//
// $Id$
//
//
// Original author: Jarrett Egertson <jegertso      uw edu> 
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

#ifndef SQT_VERSION_H
#define SQT_VERSION_H

#include <string>
#include <boost/xpressive/xpressive_dynamic.hpp>


namespace BiblioSpec {

class SQTversion {
    public:
        SQTversion(){};
        virtual ~SQTversion(){}
        virtual bool trySetVersion(string versionString) = 0;
        virtual bool operator< (const SQTversion& rhs) const = 0;
        bool operator>(const SQTversion& rhs) const {return rhs < (*this);}
        bool operator<=(const SQTversion& rhs) const {return ! ((*this) > rhs);}
        bool operator>=(const SQTversion& rhs) const {return ! ((*this) < rhs);}
        virtual bool operator== (const SQTversion& rhs) const = 0;
        bool operator!=(const SQTversion& rhs) const {return ! ((*this) == rhs);}
        virtual string generatorName() = 0;
};

class SequestVersion : public SQTversion {
    public:
        SequestVersion();
        SequestVersion(string versionString);
        bool trySetVersion(string versionString);
        bool operator< (const SQTversion& rhs) const;
        bool operator== (const SQTversion& rhs) const;
        string generatorName(){return "Sequest";}
    private:
        boost::xpressive::sregex _versionParser;
        int _majorVersion;
        int _minorVersion;
};

class CometVersion : public SQTversion {
    public:
        CometVersion();
        CometVersion(string versionString);
        bool trySetVersion(string versionString);
        bool operator< (const SQTversion& rhs) const;
        bool operator== (const SQTversion& rhs) const;
        string generatorName(){return "Comet";}
    private:
        boost::xpressive::sregex _versionParser;
        int _majorVersion;
        int _minorVersion;
        int _revision;
};
}   //namespace

#endif
