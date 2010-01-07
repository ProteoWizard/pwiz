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


#include "Reader.hpp"
#include "pwiz/data/proteome/DefaultReaderList.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "boost/shared_ptr.hpp"

using System::Exception;
using System::String;

//#pragma unmanaged
//#include <boost/utility/mutexed_singleton.hpp>
//#pragma managed


namespace pwiz {
namespace CLI {
namespace proteome {

bool Reader::accept(String^ filename, String^ head)
{
    boost::shared_ptr<stringstream> tmp(new stringstream(ToStdString(head)));
    CATCH_AND_FORWARD(return base_->accept(ToStdString(filename), tmp);)
}

String^ Reader::identify(String^ filename, String^ head)
{
    boost::shared_ptr<stringstream> tmp(new stringstream(ToStdString(head)));
    CATCH_AND_FORWARD(return ToSystemString(base_->identify(ToStdString(filename), tmp));)
}

void Reader::read(String^ filename, String^ head, ProteomeData^ result)
{
    CATCH_AND_FORWARD(base_->read(ToStdString(filename), result->base());)
}

String^ ReaderList::identify(String^ filename)
{
    CATCH_AND_FORWARD(return ToSystemString(base_->identify(ToStdString(filename)));)
}

String^ ReaderList::identify(String^ filename, String^ head)
{
    boost::shared_ptr<stringstream> tmp(new stringstream(ToStdString(head)));
    CATCH_AND_FORWARD(return ToSystemString(base_->identify(ToStdString(filename), tmp));)
}

void ReaderList::read(String^ filename, String^ head, ProteomeData^ result)
{
    boost::shared_ptr<stringstream> tmp(new stringstream(ToStdString(head)));
    CATCH_AND_FORWARD(base_->read(ToStdString(filename), tmp, result->base());)
}

void ReaderList::read(String^ filename, ProteomeData^ result)
{
    CATCH_AND_FORWARD(base_->read(ToStdString(filename), result->base());)
}


namespace {

/*#pragma unmanaged
struct FullReaderListSingleton : public boost::mutexed_singleton<FullReaderListSingleton>
{
    FullReaderListSingleton(boost::restricted) {}

    pwiz::proteome::FullReaderList list;
};
#pragma managed*/

} // namespace


ReaderList^ ReaderList::DefaultReaderList::get()
{
    CATCH_AND_FORWARD
    (
        pwiz::proteome::DefaultReaderList* list = new pwiz::proteome::DefaultReaderList();
        return gcnew ReaderList(list, gcnew System::Object());
    )
}


} // namespace proteome
} // namespace CLI
} // namespace pwiz
