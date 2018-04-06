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

#ifndef _INDEX_HPP_
#define _INDEX_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <vector>
#include <boost/iostreams/positioning.hpp>
#include <boost/shared_ptr.hpp>
#include <boost/cstdint.hpp>


namespace pwiz {
namespace data {


/// generic interface for creating and using an index on a stream of serialized objects
class PWIZ_API_DECL Index
{
    public:

    typedef boost::iostreams::stream_offset stream_offset;

    /// generic type identifying an indexed item by string id, ordinal index, and stream offset
    struct PWIZ_API_DECL Entry
    {
        std::string id;
        boost::uint64_t index;
        stream_offset offset;
    };

    typedef boost::shared_ptr<Entry> EntryPtr;

    /// create the index from specified list of entries;
    /// the list is non-const because the index implementation may resort the list
    virtual void create(std::vector<Entry>& entries) = 0;

    /// returns the number of entries in the index
    virtual size_t size() const = 0;

    /// returns the entry for the specified string id, or null if the id is not in the index
    virtual EntryPtr find(const std::string& id) const = 0;

    /// returns the entry for the specified ordinal index, or null if the ordinal is not in the index
    virtual EntryPtr find(size_t index) const = 0;

    virtual ~Index() {}
};


typedef boost::shared_ptr<Index> IndexPtr;


} // namespace data
} // namespace pwiz


#endif // _INDEX_HPP_
