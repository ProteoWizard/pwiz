//
// MSDataFile.cpp
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

#include "MSDataFile.hpp"
#include "utility/misc/Exception.hpp"

#ifdef PWIZ_HAS_VENDOR_READERS
#include "data/vendor_readers/ExtendedReaderList.hpp"
#define ReaderListType pwiz::msdata::ExtendedReaderList
#else
#include "data/msdata/DefaultReaderList.hpp"
#define ReaderListType pwiz::msdata::DefaultReaderList
#endif

namespace b = pwiz::msdata;


namespace pwiz {
namespace CLI {
namespace msdata {


MSDataFile::MSDataFile(System::String^ filename)
: MSData(0)
{
    try
    {
        ReaderListType* readerList = new ReaderListType();
        base_ = new b::MSDataFile(ToStdString(filename), readerList);
        MSData::base_ = base_;
        delete readerList;
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
}


void MSDataFile::write(MSData^ msd, System::String^ filename)
{
    b::MSDataFile::write(*msd->base_, ToStdString(filename));
}


void MSDataFile::write(System::String^ filename)
{
    base_->write(ToStdString(filename));
}


} // namespace msdata
} // namespace CLI
} // namespace pwiz
