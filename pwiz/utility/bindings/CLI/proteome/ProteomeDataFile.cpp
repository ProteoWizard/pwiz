//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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

#include "ProteomeDataFile.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/proteome/DefaultReaderList.hpp"

namespace b = pwiz::proteome;

namespace {

boost::shared_ptr<b::DefaultReaderList> readerList;
void initializeReaderList()
{
    if (!readerList.get())
        readerList.reset(new b::DefaultReaderList);
}

} // namespace


namespace pwiz {
namespace CLI {
namespace proteome {


ProteomeDataFile::ProteomeDataFile(System::String^ path)
: ProteomeData(0)
{
    try
    {
        initializeReaderList();
        base_ = new boost::shared_ptr<b::ProteomeDataFile>(new b::ProteomeDataFile(ToStdString(path), *readerList));
        ProteomeData::base_ = reinterpret_cast<boost::shared_ptr<b::ProteomeData>*>(base_);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception("[ProteomeDataFile::ProteomeDataFile()] Unhandled exception");
    }
}


ProteomeDataFile::ProteomeDataFile(System::String^ path, bool indexed)
: ProteomeData(0)
{
    try
    {
        b::DefaultReaderList readerList(indexed);
        base_ = new boost::shared_ptr<b::ProteomeDataFile>(new b::ProteomeDataFile(ToStdString(path), readerList));
        ProteomeData::base_ = reinterpret_cast<boost::shared_ptr<b::ProteomeData>*>(base_);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception("[ProteomeDataFile::ProteomeDataFile()] Unhandled exception");
    }
}


ProteomeDataFile::ProteomeDataFile(System::String^ path, Reader^ reader)
: ProteomeData(0)
{
    try
    {
        base_ = new boost::shared_ptr<b::ProteomeDataFile>(new b::ProteomeDataFile(ToStdString(path), reader->base()));
        ProteomeData::base_ = reinterpret_cast<boost::shared_ptr<b::ProteomeData>*>(base_);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception("[ProteomeDataFile::ProteomeDataFile()] Unhandled exception");
    }
}


void ProteomeDataFile::write(ProteomeData^ pd, System::String^ filename)
{
    write(pd, filename, gcnew WriteConfig());
}


void ProteomeDataFile::write(ProteomeData^ pd, System::String^ filename, WriteConfig^ config)
{
    try
    {
        b::ProteomeDataFile::WriteConfig nativeConfig((b::ProteomeDataFile::Format) config->format);
        b::ProteomeDataFile::write(pd->base(), ToStdString(filename), nativeConfig);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception(gcnew System::String("[ProteomeDataFile::write()] Unhandled exception"));
    }
}


void ProteomeDataFile::write(System::String^ filename)
{
    write(filename, gcnew WriteConfig());
}


void ProteomeDataFile::write(System::String^ filename, WriteConfig^ config)
{
    try
    {
        b::ProteomeDataFile::WriteConfig nativeConfig((b::ProteomeDataFile::Format) config->format);
        base().write(ToStdString(filename), nativeConfig);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception(gcnew System::String("[ProteomeDataFile::write()] Unhandled exception"));
    }
}


} // namespace proteome
} // namespace CLI
} // namespace pwiz
