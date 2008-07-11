//
// Reader.cpp
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


#define PWIZ_SOURCE

#include "Reader.hpp"
#include <stdexcept>


namespace pwiz {
namespace msdata {


using namespace std;


PWIZ_API_DECL bool ReaderList::accept(const string& filename, const string& head) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
        if ((*it)->accept(filename, head)) return true;

    return false;
}


PWIZ_API_DECL void ReaderList::read(const string& filename, const string& head, MSData& result) const
{
    for (const_iterator it=begin(); it!=end(); ++it)
    if ((*it)->accept(filename, head))
    {
        (*it)->read(filename, head, result);
        return;
    }

    throw runtime_error(("[ReaderList::read()] No child accepted file " +
                        filename).c_str());
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


} // namespace msdata
} // namespace pwiz

