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
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>
#include <boost/xpressive/xpressive_dynamic.hpp>


using namespace pwiz::data;
using namespace pwiz::util;
namespace bxp = boost::xpressive;


namespace pwiz {
namespace proteome {


namespace {

class ProteinList_FASTA : public ProteinList
{
    shared_ptr<istream> fsPtr_;
    string fsSHA1_;
    pwiz::util::SHA1Calculator sha1_;

    size_t size_;

    IndexPtr indexPtr_;

    vector<bxp::sregex> idAndDescriptionRegexes_;

    mutable boost::mutex io_mutex;

    void createIndex()
    {
        fsPtr_->clear();
        fsPtr_->seekg(0);

        string buf;

        // find offsets for all entries in the FASTA stream
        deque<Index::Entry> index;
        set<string> idSet;

        Index::stream_offset indexOffset = 0;
        while (getline(*fsPtr_, buf))
        {
            size_t bufLength = buf.length() + 1; // include newline
            indexOffset += bufLength;

            if (buf.empty())
                continue;

            if (buf[0] == '>') // signifies a new protein record in a FASTA file
            {
                index.push_back(Index::Entry());
                Index::Entry& ie = index.back();

                bal::trim_right_if(buf, bal::is_any_of(" \r"));

                string id;
                for(bxp::sregex& idAndDescriptionRegex : idAndDescriptionRegexes_)
                {
                    bxp::smatch match;
                    if (bxp::regex_match(buf, match, idAndDescriptionRegex))
                    {
                        id = match[1].str();
                        break;
                    }
                }

                if (id.empty())
                    throw runtime_error("[ProteinList_FASTA::createIndex] could not parse id from entry \"" + buf + "\"");

                ie.id = sha1_.hash(id);

                // note: We could silently skip the duplicates, but that would only be
                //       reasonable after checking that the sequences are equal.
                if (!idSet.insert(ie.id).second)
                    throw runtime_error("[ProteinList_FASTA::createIndex] duplicate protein id \"" + id + "\"");

                ie.index = index.size()-1;
                ie.offset = indexOffset - bufLength;
            }
        }

        idSet.clear();
        vector<Index::Entry> tmp(index.begin(), index.end());
        indexPtr_->create(tmp);
    }

    public:

    ProteinList_FASTA(shared_ptr<istream> fsPtr, IndexPtr indexPtr, const vector<string>& idAndDescriptionRegexes)
        : fsPtr_(fsPtr), indexPtr_(indexPtr)
    {
        for(const string& regexString : idAndDescriptionRegexes)
        {
            bxp::sregex idAndDescriptionRegex = bxp::sregex::compile(regexString);
            if (idAndDescriptionRegex.mark_count() == 1)
            {
                // TODO: log warning about only capturing id
            }
            else if (idAndDescriptionRegex.mark_count() != 2)
                throw runtime_error("[ProteinList_FASTA::ctor] regular expressions for capturing id and description must contain 1 or 2 capture groups; \"" + regexString + "\" has " + lexical_cast<string>(idAndDescriptionRegex.mark_count()));

            idAndDescriptionRegexes_.push_back(idAndDescriptionRegex);
        }

        if (indexPtr->size() == 0)
            createIndex();
    }

    virtual size_t find(const string& id) const
    {
        Index::EntryPtr entryPtr = indexPtr_->find(sha1_.hash(id));
        return entryPtr.get() ? entryPtr->index : indexPtr_->size();
    }

    virtual size_t size() const {return indexPtr_->size();}

    virtual ProteinPtr protein(size_t index, bool getSequence /*= true*/) const
    {
        if (index >= size())
            throw out_of_range("[ProteinList_FASTA::protein] Index out of range");

        Index::EntryPtr entryPtr = indexPtr_->find(index);

        if (!entryPtr.get())
            throw out_of_range("[ProteinList_FASTA::protein] Index out of range");

        boost::mutex::scoped_lock io_lock(io_mutex);

        fsPtr_->clear();
        fsPtr_->seekg(entryPtr->offset);

        string buf;
        getline(*fsPtr_, buf);

        // test that the index offset is valid
        if (buf.empty() || buf[0] != '>')
        {
            // TODO: log notice about stale index

            // recreate the index
            const_cast<ProteinList_FASTA&>(*this).createIndex();

            entryPtr = indexPtr_->find(index);

            if (!entryPtr.get())
                throw out_of_range("[ProteinList_FASTA::protein] Index out of range");

            fsPtr_->clear();
            fsPtr_->seekg(entryPtr->offset);

            string buf;
            getline(*fsPtr_, buf);

            // if the offset is still invalid, throw
            if (buf.empty() || buf[0] != '>')
                throw runtime_error("[ProteinList_FASTA::protein] Invalid index offset");
        }

        // trim whitespace and carriage returns from the end of the line
        bal::trim_right_if(buf, bal::is_any_of(" \r"));

        string id, description;
        for(const bxp::sregex& idAndDescriptionRegex : idAndDescriptionRegexes_)
        {
            if (idAndDescriptionRegex.mark_count() == 2)
            {
                bxp::smatch match;
                if (bxp::regex_match(buf, match, idAndDescriptionRegex))
                {
                    id = match[1].str();
                    description = match[2].str();
                    break;
                }
                //else
                    // TODO: exception is too harsh, log warning if none of the regexes match
            }
        }

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
       return ProteinPtr(new Protein(id, index, description, sequence));
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

    // TODO: make these regexes user-configurable
    vector<string> idAndDescriptionRegexes;
    idAndDescriptionRegexes.push_back(">\\s*(\\S*?IPI\\d+?\\.\\d+?)(?:\\s|\\|)(.*)");
    idAndDescriptionRegexes.push_back(">\\s*(\\S+)\\s?(.*)");
    pd.proteinListPtr.reset(new ProteinList_FASTA(is, config_.indexPtr, idAndDescriptionRegexes));
}


} // namespace proteome
} // namespace pwiz
