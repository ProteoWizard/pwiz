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
};


class PWIZ_API_DECL Reader_MGF : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "Mascot Generic";}
};


class PWIZ_API_DECL Reader_MSn : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "MSn";}
};


class PWIZ_API_DECL Reader_BTDX : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "Bruker Data Exchange";}
};


class PWIZ_API_DECL Reader_mz5 : public Reader
{
    public:
    virtual std::string identify(const std::string& filename, const std::string& head) const;
    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0, const Config& config = Config()) const;
    virtual void read(const std::string& filename, const std::string& head, std::vector<MSDataPtr>& results, const Config& config = Config()) const;
    virtual const char* getType() const {return "MZ5";}
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
