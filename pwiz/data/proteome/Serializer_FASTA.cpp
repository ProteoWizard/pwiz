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

// HACK: boost::regex::mark_count() is bugged, always returns the real count + 1
#include <boost/regex.hpp>


using boost::shared_ptr;
using namespace pwiz::data;
using namespace pwiz::util;
using boost::regex;
using boost::smatch;
using boost::regex_search;


namespace pwiz {
namespace proteome {


namespace {

class ProteinList_FASTA : public ProteinList
{
    shared_ptr<istream> fsPtr_;
    string fsSHA1_;

    size_t size_;

    IndexPtr indexPtr_;

    vector<regex> idAndDescriptionRegexes_;

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
            size_t bufLength = buf.length() + 1; // include newline
            indexOffset += bufLength;

            if (buf.empty())
                continue;

			if (buf[0] == '>') // signifies a new protein record in a FASTA file
			{
                index.push_back(Index::Entry());
                Index::Entry& ie = index.back();

                bal::trim_right_if(buf, bal::is_any_of(" \r"));

                BOOST_FOREACH(regex& idAndDescriptionRegex, idAndDescriptionRegexes_)
                {
                    smatch match;
                    if (regex_match(buf, match, idAndDescriptionRegex))
                    {
                        ie.id = match[1].str();
                        break;
                    }
                }

                if (ie.id.empty())
                    throw runtime_error("[ProteinList_FASTA::createIndex] could not parse id from entry \"" + buf + "\"");

                ie.index = index.size()-1;
                ie.offset = indexOffset - bufLength;
			}
		}
        indexPtr_->create(index);
    }

    size_t find(const string& id) const
    {
        Index::EntryPtr entryPtr = indexPtr_->find(id);
        return entryPtr.get() ? entryPtr->index : indexPtr_->size();
    }


    public:

    ProteinList_FASTA(shared_ptr<istream> fsPtr, IndexPtr indexPtr, const vector<string>& idAndDescriptionRegexes)
        : fsPtr_(fsPtr), indexPtr_(indexPtr)
    {
        // HACK: boost::regex::mark_count() is bugged, always returns the real count + 1
        BOOST_FOREACH(const string& regexString, idAndDescriptionRegexes)
        {
            regex idAndDescriptionRegex(regexString);
            if (idAndDescriptionRegex.mark_count() == 2)
            {
                // TODO: log warning about only capturing id
            }
            else if (idAndDescriptionRegex.mark_count() != 3)
                throw runtime_error("[ProteinList_FASTA::ctor] regular expressions for capturing id and description must contain 1 or 2 capture groups; \"" + regexString + "\" has " + lexical_cast<string>(idAndDescriptionRegex.mark_count()-1));

            idAndDescriptionRegexes_.push_back(idAndDescriptionRegex);
        }

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

        string description;
        BOOST_FOREACH(const regex& idAndDescriptionRegex, idAndDescriptionRegexes_)
        {
            // HACK: boost::regex::mark_count() is bugged, always returns the real count + 1
            if (idAndDescriptionRegex.mark_count() == 3)
            {
                smatch match;
                if (regex_match(buf, match, idAndDescriptionRegex))
                {
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
       return ProteinPtr(new Protein(entryPtr->id, index, description, sequence));
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
    idAndDescriptionRegexes.push_back(">\\s*(\\S*?IPI\\S+?)(?:\\s|\\|)(.*)");
    idAndDescriptionRegexes.push_back(">\\s*(\\S+)\\s?(.*)");
    pd.proteinListPtr.reset(new ProteinList_FASTA(is, config_.indexPtr, idAndDescriptionRegexes));
}


} // namespace proteome
} // namespace pwiz
