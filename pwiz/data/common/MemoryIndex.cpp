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


#include "MemoryIndex.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace data {


class MemoryIndex::Impl
{
    map<string, EntryPtr> idToEntryMap;
    vector<EntryPtr> indexEntries;

    public:

    virtual void create(std::vector<Entry>& entries)
    {
        indexEntries.reserve(entries.size());
        BOOST_FOREACH(const Entry& entry, entries)
        {
            EntryPtr entryPtr(new Entry(entry));
            idToEntryMap[entry.id] = entryPtr;
            indexEntries.push_back(entryPtr);
        }
    }

    virtual size_t size() const
    {
        return idToEntryMap.size();
    }

    virtual EntryPtr find(const std::string& id) const
    {
        map<string, EntryPtr>::const_iterator result = idToEntryMap.find(id);
        if (result == idToEntryMap.end())
            return EntryPtr();
        return result->second;
    }

    virtual EntryPtr find(size_t index) const
    {
        if (index >= indexEntries.size())
            return EntryPtr();
        return indexEntries[index];
    }
};


PWIZ_API_DECL MemoryIndex::MemoryIndex() : impl_(new Impl) {}
PWIZ_API_DECL void MemoryIndex::create(vector<Entry>& entries) {impl_->create(entries);}
PWIZ_API_DECL size_t MemoryIndex::size() const {return impl_->size();}
PWIZ_API_DECL Index::EntryPtr MemoryIndex::find(const string& id) const {return impl_->find(id);}
PWIZ_API_DECL Index::EntryPtr MemoryIndex::find(size_t index) const {return impl_->find(index);}


} // namespace data
} // namespace pwiz
