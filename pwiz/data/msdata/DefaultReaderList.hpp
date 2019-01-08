//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#ifndef _DEFAULTREADERLIST_HPP_
#define _DEFAULTREADERLIST_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "Reader.hpp"


namespace pwiz {
namespace msdata {


class PWIZ_API_DECL Reader_mzML : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "mzML";}
    virtual CVID getCvType() const {return MS_mzML_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".mzml", ".xml"};}

    private:
    enum Type { Type_mzML, Type_mzML_Indexed, Type_Unknown };
    Type type(std::istream& is) const;
};


class PWIZ_API_DECL Reader_mzXML : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "mzXML";}
    virtual CVID getCvType() const {return MS_ISB_mzXML_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".mzxml", ".xml"};}
};


class PWIZ_API_DECL Reader_MGF : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "Mascot Generic";}
    virtual CVID getCvType() const {return MS_Mascot_MGF_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".mgf"};}
};


class PWIZ_API_DECL Reader_MSn : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
};


class PWIZ_API_DECL Reader_MS1 : public Reader_MSn
{
    public:
    virtual const char* getType() const {return "MS1";}
    virtual CVID getCvType() const {return MS_MS1_format;}
    virtual std::vector<std::string> getFileExtensions() const { return { ".ms1", ".cms1", ".bms1" }; }
};


class PWIZ_API_DECL Reader_MS2 : public Reader_MSn
{
    public:
    // no-op function: Reader_MS1 is the only one that should do any work (and it just uses Reader_MSn::identify)
    virtual std::string identify(const std::string& filename, const std::string& head) const { return ""; }
    virtual const char* getType() const {return "MS2";}
    virtual CVID getCvType() const {return MS_MS2_format;}
    virtual std::vector<std::string> getFileExtensions() const { return { ".ms2", ".cms2", ".bms2" }; }
};


class PWIZ_API_DECL Reader_BTDX : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "Bruker Data Exchange";}
    virtual CVID getCvType() const {return MS_Bruker_XML_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".xml"};}
};


class PWIZ_API_DECL Reader_mz5 : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "MZ5";}
    virtual CVID getCvType() const {return MS_mz5_format;}
    virtual std::vector<std::string> getFileExtensions() const {return {".mz5"};}
};


/// default Reader list
class PWIZ_API_DECL DefaultReaderList : public ReaderList
{
    public:
    DefaultReaderList();
};


} // namespace msdata
} // namespace pwiz


#endif // _DEFAULTREADERLIST_HPP_
