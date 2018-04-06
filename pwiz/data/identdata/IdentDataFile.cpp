//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#define PWIZ_SOURCE

#include "IdentDataFile.hpp"
#include "TextWriter.hpp"
#include "Serializer_mzid.hpp"
#include "Serializer_pepXML.hpp"
#include "DefaultReaderList.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp" // for charcounter defn
#include "boost/iostreams/device/file.hpp"
#include "boost/iostreams/filtering_stream.hpp" 
#include "boost/iostreams/filter/gzip.hpp" 


namespace pwiz {
namespace identdata {


using namespace pwiz::util;


namespace {


void readFile(const string& filename,
              IdentData& mzid,
              const Reader& reader,
              const string& head,
              const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
              bool ignoreSequenceCollectionAndAnalysisData)
{
    if (!reader.accept(filename, head))
        throw runtime_error("[IdentDataFile::readFile()] Unsupported file format.");

    Reader::Config config;
    config.ignoreSequenceCollectionAndAnalysisData = ignoreSequenceCollectionAndAnalysisData;
    config.iterationListenerRegistry = iterationListenerRegistry;
    reader.read(filename, head, mzid, config);
}


shared_ptr<DefaultReaderList> defaultReaderList_;


} // namespace


PWIZ_API_DECL IdentDataFile::IdentDataFile(const string& filename,
                                           const Reader* reader,
                                           const pwiz::util::IterationListenerRegistry* iterationListenerRegistry,
                                           bool ignoreSequenceCollectionAndAnalysisData)
{
    // peek at head of file 
    string head = read_file_header(filename, 512);

    if (reader)
    {
        readFile(filename, *this, *reader, head, iterationListenerRegistry, ignoreSequenceCollectionAndAnalysisData);
    }
    else
    {
        if (!defaultReaderList_.get())
            defaultReaderList_ = shared_ptr<DefaultReaderList>(new DefaultReaderList);
        readFile(filename, *this, *defaultReaderList_, head, iterationListenerRegistry, ignoreSequenceCollectionAndAnalysisData);
    }
}


PWIZ_API_DECL
void IdentDataFile::write(const string& filename,
                          const WriteConfig& config,
                          const IterationListenerRegistry* iterationListenerRegistry)
{
    write(*this, filename, config, iterationListenerRegistry); 
}


PWIZ_API_DECL
void IdentDataFile::write(ostream& os,
                          const std::string& filename,
                          const WriteConfig& config,
                          const IterationListenerRegistry* iterationListenerRegistry)
{
    write(*this, filename, os, config, iterationListenerRegistry); 
}


namespace {


shared_ptr<ostream> openFile(const string& filename)
{
    shared_ptr<ostream> result(new ofstream(filename.c_str(), ios::binary));
    
    if (!result.get() || !*result)
        throw runtime_error(("[IdentDataFile::openFile()] Unable to open file " + filename).c_str());
    
    return result; 		
}


void writeStream(ostream& os, const IdentData& idd, const string& filename,
                 const IdentDataFile::WriteConfig& config,
                 const IterationListenerRegistry* iterationListenerRegistry)
{
    switch (config.format)
    {
        case IdentDataFile::Format_Text:
        {
            TextWriter(os,0)(idd);
            break;
        }

        case IdentDataFile::Format_MzIdentML:
        {
            Serializer_mzIdentML serializer;
            serializer.write(os, idd, iterationListenerRegistry);
            break;
        }

        case IdentDataFile::Format_pepXML:
        {
            Serializer_pepXML serializer;
            serializer.write(os, idd, filename, iterationListenerRegistry);
            break;
        }

        default:
            throw runtime_error("[IdentDataFile::write()] Format not implemented.");
    }
}


} // namespace


PWIZ_API_DECL
void IdentDataFile::write(const IdentData& idd,
                          const string& filename,
                          const WriteConfig& config,
                          const IterationListenerRegistry* iterationListenerRegistry)
{
    shared_ptr<ostream> os = openFile(filename);
    writeStream(*os, idd, filename, config, iterationListenerRegistry);
}


PWIZ_API_DECL
void IdentDataFile::write(const IdentData& idd,
                          const string& filename,
                          ostream& os,
                          const WriteConfig& config,
                          const IterationListenerRegistry* iterationListenerRegistry)
{
    WriteConfig config2(config);
    //config2.gzipped = false;
    writeStream(os, idd, filename, config2, iterationListenerRegistry);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, IdentDataFile::Format format)
{
    switch (format)
    {
        case IdentDataFile::Format_Text:
            os << "Text";
            return os;
        case IdentDataFile::Format_MzIdentML:
            os << "mzIdentML";
            return os;
        case IdentDataFile::Format_pepXML:
            os << "pepXML";
            return os;
        default:
            os << "Unknown";
            return os;
    }
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const IdentDataFile::WriteConfig& config)
{
    os << config.format;
    return os;
}


} // namespace identdata
} // namespace pwiz
