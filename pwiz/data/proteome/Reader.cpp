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


#define PWIZ_SOURCE

#include "Reader.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace proteome {


using namespace pwiz::util;
using boost::shared_ptr;


PWIZ_API_DECL void Reader::read(const string& uri, ProteomeData& result) const
{
    shared_ptr<istream> uriStreamPtr(new ifstream(uri.c_str()));
    read(uri, uriStreamPtr, result);
}


PWIZ_API_DECL std::string ReaderList::identify(const string& uri) const
{
    shared_ptr<istream> uriStreamPtr(new ifstream(uri.c_str()));
    return identify(uri, uriStreamPtr);
}


PWIZ_API_DECL std::string ReaderList::identify(const string& uri, shared_ptr<istream> uriStreamPtr) const
{
	std::string result;
    for (const_iterator it=begin(); it!=end(); ++it)
	{
		result = (*it)->identify(uri, uriStreamPtr);
        if (result.length())
		{
			break;
		}
	}
    return result;
}


PWIZ_API_DECL void ReaderList::read(const string& uri, ProteomeData& result) const
{
    shared_ptr<istream> uriStreamPtr(new ifstream(uri.c_str()));
    read(uri, uriStreamPtr, result);
}


PWIZ_API_DECL void ReaderList::read(const string& uri, shared_ptr<istream> uriStreamPtr, ProteomeData& result) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
        if ((*it)->accept(uri, uriStreamPtr))
        {
            (*it)->read(uri, uriStreamPtr, result);
            return;
        }
    throw ReaderFail(" don't know how to read " + uri);
}


PWIZ_API_DECL ReaderList& ReaderList::operator +=(const ReaderList& rhs)
{
    insert(end(), rhs.begin(), rhs.end());
    return *this;
}


PWIZ_API_DECL ReaderList& ReaderList::operator +=(const ReaderPtr& rhs)
{
    push_back(rhs);
    return *this;
}


PWIZ_API_DECL ReaderList ReaderList::operator +(const ReaderList& rhs) const
{
    ReaderList readerList(*this);
    readerList += rhs;
    return readerList;
}


PWIZ_API_DECL ReaderList ReaderList::operator +(const ReaderPtr& rhs) const
{
    ReaderList readerList(*this);
    readerList += rhs;
    return readerList;
}


PWIZ_API_DECL ReaderList operator +(const ReaderPtr& lhs, const ReaderPtr& rhs)
{
    ReaderList readerList;
    readerList.push_back(lhs);
    readerList.push_back(rhs);
    return readerList;
}


} // namespace proteome
} // namespace pwiz
