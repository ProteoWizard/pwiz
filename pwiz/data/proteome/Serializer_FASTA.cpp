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

#include "Serializer_FASTA.hpp"
#include "pwiz/data/common/Index.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>


using boost::shared_ptr;
using namespace pwiz::data;
using namespace pwiz::util;


namespace pwiz {
namespace proteome {


namespace {

class ProteinList_FASTA : public ProteinList
{
    shared_ptr<istream> fsPtr_;
    string fsSHA1_;

    size_t size_;

    IndexPtr indexPtr_;

    string delimiters_;

    mutable boost::mutex io_mutex;

    void createIndex()
    {
        fsPtr_->clear();
        fsPtr_->seekg(0);

        // find offsets for all entries in the FASTA stream
        vector<Index::Entry> index;
        string buf;
        Index::stream_offset indexOffset = 0;
        while (getline(*fsPtr_, buf))
        {
            if (buf.empty())
                continue;

			if (buf[0] == '>') // signifies a new protein record in a FASTA file
			{
                index.push_back(Index::Entry());
                Index::Entry& ie = index.back();

                size_t idEnd = std::min(buf.length(), buf.find_first_of(delimiters_));
                ie.id = buf.substr(1, idEnd-1);
                ie.index = index.size()-1;
                ie.offset = indexOffset;
			}

            indexOffset += buf.length() + 1; // include newline
		}
        indexPtr_->create(index);
    }

    size_t find(const string& id) const
    {
        Index::EntryPtr entryPtr = indexPtr_->find(id);
        return entryPtr.get() ? entryPtr->index : indexPtr_->size();
    }

    void setDelimiters()
    {
        if (delimiters_.find(' ') == string::npos) delimiters_ += ' ';
        if (delimiters_.find('\t') == string::npos) delimiters_ += '\t';
        if (delimiters_.find('\r') == string::npos) delimiters_ += '\r';
        if (delimiters_.find('\n') == string::npos) delimiters_ += '\n';
    }

    public:

    ProteinList_FASTA(shared_ptr<istream> fsPtr, IndexPtr indexPtr, string delimiters = " \t")
        : fsPtr_(fsPtr), indexPtr_(indexPtr), delimiters_(delimiters)
    {
        setDelimiters();

        if (indexPtr->size() == 0)
            createIndex();
    }

    size_t size() const {return indexPtr_->size();}

    ProteinPtr protein(size_t index, bool getSequence /*= true*/) const
    {
        if (index >= size())
            throw out_of_range("[ProteinList_FASTA::protein] Index out of range");

        Index::EntryPtr entryPtr = indexPtr_->find(index);

        if (!entryPtr.get())
            throw out_of_range("[ProteinList_FASTA::protein] Index out of range");

        const Index::Entry& entry = *entryPtr;

        boost::mutex::scoped_lock io_lock(io_mutex);

        fsPtr_->clear();
        fsPtr_->seekg(entry.offset);

        string buf;
        getline(*fsPtr_, buf);

        if (buf.empty() || buf[0] != '>')
            throw runtime_error("[ProteinList_FASTA::protein] Invalid index offset");

        string description;
        if (buf.length() > entry.id.length()+1) // the rest of buf after the id+1 is the description
            description = buf.substr(entry.id.length()+2);
        bal::trim_right_if(description, bal::is_any_of("\r"));

        string sequence;
        if (getSequence)
            while (getline(*fsPtr_, buf))
            {
                if (buf.empty() || buf[0] == '\r') // skip blank lines
                    continue;
		        if (buf[0] == '>') // signifies the next protein record in a FASTA file
                    break;
                size_t lineEnd = std::min(buf.length(), buf.find_first_of("\r\n"));
                sequence.append(buf.begin(), buf.begin()+lineEnd);
            }
       return ProteinPtr(new Protein(entry.id, index, description, sequence));
    }
};

} // namespace


PWIZ_API_DECL Serializer_FASTA::Serializer_FASTA(const Config& config) : config_(config) {}


PWIZ_API_DECL void Serializer_FASTA::write(ostream& os, const ProteomeData& pd,
   const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    if (!pd.proteinListPtr.get())
        throw runtime_error("[Serializer_FASTA::write()] Null protein list.");

    const ProteinList& pl = *pd.proteinListPtr;
    for (size_t i=0, end=pl.size(); i < end; ++i)
    {
        ProteinPtr proteinPtr = pl.protein(i, true);
        os << '>' << proteinPtr->id << ' ' << proteinPtr->description << '\n' << proteinPtr->sequence() << '\n';

        if (iterationListenerRegistry)
            iterationListenerRegistry->broadcastUpdateMessage(IterationListener::UpdateMessage(i+1, end));
    }
}


PWIZ_API_DECL void Serializer_FASTA::read(shared_ptr<istream> is, ProteomeData& pd) const
{
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_FASTA::read()] Bad istream.");

    pd.proteinListPtr.reset(new ProteinList_FASTA(is, config_.indexPtr));
}


} // namespace proteome
} // namespace pwiz
