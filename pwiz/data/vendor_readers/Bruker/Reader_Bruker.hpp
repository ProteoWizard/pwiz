//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _READER_BRUKER_HPP_
#define _READER_BRUKER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/Reader.hpp"


namespace pwiz {
namespace msdata {


class PWIZ_API_DECL Reader_Bruker : public Reader
{
    public:

	virtual std::string identify(const std::string& filename,
                                 const std::string& head) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      MSData& result,
                      int runIndex = 0,
                      const Config& config = Config()) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results,
                      const Config& config = Config()) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back(), 0, config);
    }

    virtual const char * getType() const {return "Bruker Analysis";}
};

class PWIZ_API_DECL Reader_Bruker_BAF : public Reader_Bruker
{
    virtual const char * getType() const {return "Bruker BAF";}
    virtual CVID getCvType() const {return MS_Bruker_BAF_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".d", ".baf"};}
};

class PWIZ_API_DECL Reader_Bruker_Dummy : public Reader_Bruker
{
    // no-op function: Reader_Bruker_BAF is the only one that should do any work (and it just uses Reader_Bruker::identify)
    virtual std::string identify(const std::string& filename, const std::string& head) const {return "";}
};

class PWIZ_API_DECL Reader_Bruker_YEP : public Reader_Bruker_Dummy
{
    virtual const char * getType() const {return "Bruker YEP";}
    virtual CVID getCvType() const {return MS_Bruker_Agilent_YEP_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".d", ".yep"};}
};

class PWIZ_API_DECL Reader_Bruker_FID : public Reader_Bruker_Dummy
{
    virtual const char * getType() const {return "Bruker FID";}
    virtual CVID getCvType() const {return MS_Bruker_FID_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {"fid"};} // not an extension, fid is the whole filename
};

class PWIZ_API_DECL Reader_Bruker_U2 : public Reader_Bruker_Dummy
{
    virtual const char * getType() const {return "Bruker U2";}
    virtual CVID getCvType() const {return MS_Bruker_U2_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".d", ".u2"};}
};

class PWIZ_API_DECL Reader_Bruker_TDF : public Reader_Bruker_Dummy
{
    virtual const char * getType() const {return "Bruker TDF";}
    virtual CVID getCvType() const {return MS_Bruker_TDF_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".d", ".tdf"};}
};

class PWIZ_API_DECL Reader_Bruker_TSF : public Reader_Bruker_Dummy
{
    virtual const char* getType() const { return "Bruker TSF"; }
    virtual CVID getCvType() const { return MS_Bruker_TSF_format; }
    virtual std::vector<std::string> getFileExtensions() const {return {".d", ".tsf"};}
};


} // namespace msdata
} // namespace pwiz


#endif // _READER_BRUKER_HPP_
