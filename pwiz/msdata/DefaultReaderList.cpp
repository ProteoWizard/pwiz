//
// DefaultReaderList.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "DefaultReaderList.hpp"
#include "SpectrumList_mzXML.hpp"
#include "Serializer_mzML.hpp"
#include "Serializer_mzXML.hpp"
#include "Reader_RAW.hpp"
#include <iostream>
#include <fstream>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;


namespace {


class Reader_mzML : public Reader
{
    public:

    virtual bool accept(const std::string& filename, const std::string& head) const
    {
         istringstream iss(head); 
         return type(iss) != Type_Unknown; 
    }

    virtual void read(const std::string& filename, const std::string& head, MSData& result) const
    {
        shared_ptr<istream> is(new ifstream(filename.c_str(), ios::binary));
        if (!is.get() || !*is)
            throw runtime_error(("[MSDataFile::Reader_mzML] Unable to open file " + filename).c_str());

        switch (type(*is))
        {
            case Type_mzML:
            {
                Serializer_mzML::Config config;
                config.indexed = false;
                Serializer_mzML serializer(config);
                serializer.read(is, result);
                break;
            }
            case Type_mzML_Indexed:
            {
                Serializer_mzML serializer;
                serializer.read(is, result);
                break;
            }
            case Type_Unknown:
            default:
            {
                throw runtime_error("[MSDataFile::Reader_mzML] This isn't happening."); 
            }
        }
    }

    private:

    enum Type { Type_mzML, Type_mzML_Indexed, Type_Unknown }; 

    Type type(istream& is) const
    {
        is.seekg(0);

        string buffer;
        is >> buffer;

        if (buffer != "<?xml")
            return Type_Unknown;
            
        getline(is, buffer);
        is >> buffer; 

        if (buffer == "<indexedmzML")
            return Type_mzML_Indexed;
        else if (buffer == "<mzML")
            return Type_mzML;
        else
            return Type_Unknown;
    }
};


class Reader_mzXML : public Reader
{
    virtual bool accept(const std::string& filename, const std::string& head) const
    {
        istringstream iss(head); 

        string buffer;
        iss >> buffer;

        if (buffer != "<?xml") return false;

        getline(iss, buffer);
        iss >> buffer; 

        return (buffer=="<mzXML" || buffer=="<msRun");
    }

    virtual void read(const std::string& filename, const std::string& head, MSData& result) const
    {
        shared_ptr<istream> is(new ifstream(filename.c_str(), ios::binary));
        if (!is.get() || !*is)
            throw runtime_error(("[MSDataFile::Reader_mzXML] Unable to open file " + filename).c_str());

        try
        {
            // assume there is a scan index
            Serializer_mzXML serializer;
            serializer.read(is, result);
            return;
        }
        catch (SpectrumList_mzXML::index_not_found&)
        {}

        // error looking for index -- try again, but generate index 
        is->seekg(0);
        Serializer_mzXML::Config config;
        config.indexed = false;
        Serializer_mzXML serializer(config);
        serializer.read(is, result);
        return;
    }
};


} // namespace


/// default Reader list
DefaultReaderList::DefaultReaderList()
{
    push_back(ReaderPtr(new Reader_mzML));
    push_back(ReaderPtr(new Reader_mzXML));

    #ifndef PWIZ_NO_READER_RAW
    push_back(ReaderPtr(new Reader_RAW));
    #endif
}


} // namespace msdata
} // namespace pwiz


