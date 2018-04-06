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
namespace tradata {


using namespace pwiz::util;


// default implementation; most Readers don't need to worry about multi-run input files
/*PWIZ_API_DECL void Reader::readIds(const string& filename, const string& head, vector<string>& results) const
{
    TraData data;
    read(filename, head, data);
    results.push_back(data.id);
}*/


PWIZ_API_DECL std::string ReaderList::identify(const string& filename) const
{
    return identify(filename, read_file_header(filename, 512));
}


PWIZ_API_DECL std::string ReaderList::identify(const string& filename, const string& head) const
{
	std::string result;
    for (const_iterator it=begin(); it!=end(); ++it)
	{
		result = (*it)->identify(filename, head);
        if (result.length())
		{
			break;
		}
	}
    return result;
}


PWIZ_API_DECL void ReaderList::read(const string& filename, TraData& result, int sampleIndex /* = 0 */) const
{
    read(filename, read_file_header(filename, 512), result, sampleIndex);
}


PWIZ_API_DECL void ReaderList::read(const string& filename, const string& head, TraData& result, int sampleIndex /* = 0 */) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
        if ((*it)->accept(filename, head))
        {
            (*it)->read(filename, head, result, sampleIndex);
            return;
        }
    throw ReaderFail((" don't know how to read " +
                        filename).c_str());
}


PWIZ_API_DECL void ReaderList::read(const string& filename, vector<TraDataPtr>& results) const
{
    read(filename, read_file_header(filename, 512), results);
}


PWIZ_API_DECL void ReaderList::read(const string& filename, const string& head, vector<TraDataPtr>& results) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
        if ((*it)->accept(filename, head))
        {
            (*it)->read(filename, head, results);
            return;
        }
    throw ReaderFail((" don't know how to read " +
                        filename).c_str());
}


/*PWIZ_API_DECL void ReaderList::readIds(const string& filename, vector<string>& results) const
{
    readIds(filename, read_file_header(filename, 512), results);
}


PWIZ_API_DECL void ReaderList::readIds(const string& filename, const string& head, vector<string>& results) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
        if ((*it)->accept(filename, head))
        {
            (*it)->readIds(filename, head, results);
            return;
        }
    throw ReaderFail((" don't know how to read " +
                        filename).c_str());
}*/


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


} // namespace tradata
} // namespace pwiz

