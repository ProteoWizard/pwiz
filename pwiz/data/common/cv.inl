//
// $Id: cv.inl 3333 2012-04-09 20:41:05Z bpratt $
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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
// NOTE:
// This is a code fragment - it's meant to be #included in the 
// cvgen-created cv.cpp file. It won't compile by itself.
//
// The idea is to simplify the maintenance of non-generated cv access code
// by placing it in its own file.  This file's contents were formerly 
// written to cv.cpp by cvgen, which made maintenance somewhat confusing 
// and awkward.
//


PWIZ_API_DECL bool CV::operator==(const CV& that) const
{
    return id == that.id && fullName == that.fullName && URI == that.URI && version == that.version;
}


PWIZ_API_DECL bool CV::empty() const
{
    return id.empty() && fullName.empty() && URI.empty() && version.empty();
}


PWIZ_API_DECL const CV& cv(const string& prefix)
{
    const map<string,CV>& cvMap = CVTermData::instance->cvMap();
    if (cvMap.find(prefix) == cvMap.end())
        throw invalid_argument("[cv()] no CV associated with prefix \"" + prefix + "\"");
    return cvMap.find(prefix)->second;
}


PWIZ_API_DECL const string& CVTermInfo::shortName() const
{
    const string* result = &name;
    for (vector<string>::const_iterator it=exactSynonyms.begin(); it!=exactSynonyms.end(); ++it)
        if (result->size() > it->size())
            result = &*it;
    return *result;
}


PWIZ_API_DECL string CVTermInfo::prefix() const
{
    return id.substr(0, id.find_first_of(":"));
}


PWIZ_API_DECL const CVTermInfo& cvTermInfo(CVID cvid)
{
    const map<CVID,CVTermInfo>& infoMap = CVTermData::instance->infoMap();
    map<CVID,CVTermInfo>::const_iterator itr = infoMap.find(cvid);
    if (itr == infoMap.end())
        throw invalid_argument("[cvTermInfo()] no term associated with CVID \"" + lexical_cast<string>(cvid) + "\"");
    return itr->second;
}


inline unsigned int stringToCVID(const std::string& str)
{
    errno = 0;
    const char* stringToConvert = str.c_str();
    const char* endOfConversion = stringToConvert;
    unsigned int value = (unsigned int) strtoul (stringToConvert, const_cast<char**>(&endOfConversion), 10);
    if (( value == 0u && stringToConvert == endOfConversion) || // error: conversion could not be performed
        errno != 0 ) // error: overflow or underflow
        throw bad_lexical_cast();
    return value;
}


PWIZ_API_DECL const CVTermInfo& cvTermInfo(const std::string& id)
{
    return cvTermInfo(id.c_str());
}


PWIZ_API_DECL const CVTermInfo& cvTermInfo(const char* id)
{
    const map<CVID, CVTermInfo>& infoMap = CVTermData::instance->infoMap();
    if (id)
        for (int o=0;o<oboPrefixesSize_;++o)
        {
            // for oboPrefix "FOO", see if id has form "FOO:nnnnn"
            const char* ip = id;
            const char* op = oboPrefixes_[o];
            while ((*op==*ip) && *op) {++op;++ip;}
            if ((!*op) && (*ip++==':')) 
            {   // id has form "FOO:nnnnnn", and ip points at "nnnnnn"
                CVID cvid = (CVID)(o*enumBlockSize_ + strtoul(ip,NULL,10));
                map<CVID, CVTermInfo>::const_iterator find = infoMap.find(cvid);
                if (find == infoMap.end())
                {
                    if (bal::equals("UNIMOD", oboPrefixes_[o]))
                        return infoMap.find(CVID_Unknown)->second;
                    throw out_of_range("Invalid cvParam accession \"" + lexical_cast<string>(cvid) + "\"");
                }
                return find->second;
            }
        }
    return infoMap.find(CVID_Unknown)->second;
}


PWIZ_API_DECL bool cvIsA(CVID child, CVID parent)
{
    if (child == parent) return true;
    const CVTermInfo& info = cvTermInfo(child);
    for (CVTermInfo::id_list::const_iterator it=info.parentsIsA.begin(); it!=info.parentsIsA.end(); ++it)
        if (cvIsA(*it,parent)) return true;
    return false;
}


PWIZ_API_DECL const vector<CVID>& cvids()
{
   return CVTermData::instance->cvids();
}


