//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#include "TraDataFile.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"
//#include "boost/system/error_code.hpp"
#include <WinError.h>

namespace b = pwiz::tradata;

namespace {

boost::shared_ptr<pwiz::tradata::DefaultReaderList> readerList;
 void initializeReaderList()
 {
     if (!readerList.get())
         readerList.reset(new b::DefaultReaderList);
 }

} // namespace


namespace pwiz {
namespace CLI {
namespace tradata {

TraDataFile::TraDataFile(System::String^ path)
: TraData(0)
{
    try
    {
        initializeReaderList();
        base_ = new boost::shared_ptr<b::TraDataFile>(new b::TraDataFile(ToStdString(path), (b::Reader*) readerList.get()));
        TraData::base_ = reinterpret_cast<boost::shared_ptr<b::TraData>*>(base_);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception("[TraDataFile::TraDataFile()] Unhandled exception");
    }
}

void TraDataFile::write(TraData^ trad, System::String^ filename)
{
    WriteConfig^ config = gcnew WriteConfig();
    config->format = Format::Format_traML;
    write(trad, filename, config);
}

void TraDataFile::write(TraData^ trad, System::String^ filename, WriteConfig^ config)
{
    try
    {
        b::TraDataFile::WriteConfig config2((b::TraDataFile::Format) config->format);
        config2.gzipped = config->gzipped;
        b::TraDataFile::write(**trad->base_, ToStdString(filename), config2);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception(gcnew System::String("[TraDataFile::write()] Unhandled exception"));
    }
}

void TraDataFile::write(System::String^ filename)
{
    WriteConfig^ config = gcnew WriteConfig();
    config->format = Format::Format_traML;
    write(filename, config);
}

void TraDataFile::write(System::String^ filename, WriteConfig^ config)
{
    try
    {
        b::TraDataFile::WriteConfig config2((b::TraDataFile::Format) config->format);
        config2.gzipped = config->gzipped;
        (*base_)->write(ToStdString(filename), config2);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception(gcnew System::String("[TraDataFile::write()] Unhandled exception"));
    }
}

} // namespace tradata
} // namespace CLI
} // namespace pwiz
