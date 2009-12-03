//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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

#include "Reader_FASTA.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/array.hpp>
#include <stdexcept>


using boost::shared_ptr;


namespace pwiz {
namespace proteome {


namespace {

class ProteinList_FASTA : public ProteinList
{
    shared_ptr<istream> fsPtr_;

    shared_ptr<istream> isPtr_;
    size_t size_;

    struct IndexEntry { string id; size_t offset; };
    struct IndexEntryLessThan { bool operator() (const IndexEntry& lhs, const IndexEntry& rhs) const {return lhs.id < rhs.id;} };
    vector<IndexEntry> index_;

    string delimiters_;

    // index is stored in a separate stream with constant-length entries sorted asciibetically by protein id
    void createIndex(shared_ptr<ostream> isPtr = shared_ptr<ostream>())
    {
        fsPtr_->clear();
        fsPtr_->seekg(0);

        // find offsets for all entries in the FASTA stream
        string buf;
        while (getline(*fsPtr_, buf))
        {
            if (buf.empty())
                continue;

			if (buf[0] == '>') // signifies a new protein record in a FASTA file
			{
                index_.push_back(IndexEntry());
                IndexEntry& ie = index_.back();

                size_t idEnd = std::min(buf.length(), buf.find_first_of(delimiters_));
                ie.id = buf.substr(1, idEnd-1);
                ie.offset = size_t(fsPtr_->tellg())-buf.length();
			}
		}
        index_.resize(index_.size()); // trim allocated memory to just what is needed
        sort(index_.begin(), index_.end(), IndexEntryLessThan());

        size_ = index_.size();

        if (isPtr.get())
        {
            isPtr->clear();
            isPtr->seekp(0);

            BOOST_FOREACH(IndexEntry& ie, index_)
                *isPtr << ie.id << ' ' << ie.offset << '\n';
        }
    }

    void readIndex(shared_ptr<istream> isPtr)
    {
        isPtr->clear();
        isPtr->seekg(0);

        IndexEntry ie;
        while (*isPtr >> ie.id >> ie.offset)
            index_.push_back(ie);
        size_ = index_.size();
    }

    size_t find(const string& id) const
    {
        // binary search on index (it's already sorted)
        IndexEntry ie; ie.id = id;
        vector<IndexEntry>::const_iterator itr = lower_bound(index_.begin(), index_.end(), ie, IndexEntryLessThan());
        return size_t(itr - index_.begin());
    }

    void setDelimiters()
    {
        if( delimiters_.find( ' ' ) == string::npos ) delimiters_ += ' ';
        if( delimiters_.find( '\t' ) == string::npos ) delimiters_ += '\t';
        if( delimiters_.find( '\r' ) == string::npos ) delimiters_ += '\r';
        if( delimiters_.find( '\n' ) == string::npos ) delimiters_ += '\n';
    }

    public:

    ProteinList_FASTA(shared_ptr<istream> fsPtr, shared_ptr<istream> isPtr, string delimiters = " \t")
        : fsPtr_(fsPtr), isPtr_(isPtr), delimiters_(delimiters)
    {
        setDelimiters();
        readIndex(isPtr);
    }

    ProteinList_FASTA(shared_ptr<istream> fsPtr, shared_ptr<iostream> isPtr, string delimiters = " \t")
        : fsPtr_(fsPtr), isPtr_(isPtr), delimiters_(delimiters)
    {
        setDelimiters();
        createIndex(isPtr);
    }

    ProteinList_FASTA(shared_ptr<istream> fsPtr, string delimiters = " \t")
        : fsPtr_(fsPtr), isPtr_(), delimiters_(delimiters)
    {
        setDelimiters();
        createIndex();
    }

    size_t size() const {return size_;}

    ProteinPtr protein(size_t index, bool getSequence /*= true*/) const
    {
        if (index >= size_)
            throw runtime_error("TODO");

        const IndexEntry& entry = index_[index];

        fsPtr_->clear();
        fsPtr_->seekg(entry.offset + entry.id.length());

        string buf;

        string description;
        getline(*fsPtr_, buf);
        if (!buf.empty())
        {
            size_t descriptionEnd = std::min(buf.length(), buf.find_first_of("\r\n"));
            description = buf.substr(1, descriptionEnd-1);
        }

        string sequence;
        if (getSequence)
            while (getline(*fsPtr_, buf))
            {
                if (buf.empty() || buf == "\r")
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


//
// Reader_FASTA
//

PWIZ_API_DECL Reader_FASTA::Reader_FASTA(const Config& config)
{}


PWIZ_API_DECL string Reader_FASTA::identify(const string& filename, const std::string& head) const
{
    return bal::to_lower_copy(bfs::extension(filename)) == ".fasta" ? getType() : "";
}


PWIZ_API_DECL void Reader_FASTA::read(const string& filename, const string& head, ProteomeData& result) const
{
    result.id = filename;
    shared_ptr<istream> fsPtr(new ifstream(filename.c_str(), std::ios::binary));

    if (config.indexed)
    {
        if (bfs::exists(filename + ".index"))
        {
            shared_ptr<istream> existingIndexStreamPtr(new ifstream((filename + ".index").c_str(), std::ios::binary));
            result.proteinListPtr = ProteinListPtr(new ProteinList_FASTA(fsPtr, existingIndexStreamPtr));
        }
        else
        {
            // make a new index and then use it
            shared_ptr<iostream> newIndexStreamPtr(new fstream((filename + ".index").c_str(), std::ios::binary));
            result.proteinListPtr = ProteinListPtr(new ProteinList_FASTA(fsPtr, newIndexStreamPtr));
        }
    }
    else
        result.proteinListPtr = ProteinListPtr(new ProteinList_FASTA(fsPtr));
}


PWIZ_API_DECL void Reader_FASTA::read(const std::string& uri, boost::shared_ptr<std::istream> streamPtr, ProteomeData& result) const
{
    result.id = uri;
    result.proteinListPtr = ProteinListPtr(new ProteinList_FASTA(streamPtr));
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Reader_FASTA::Config& config)
{
    os << "indexed=\"" << boolalpha << config.indexed << "\"";
    return os;
}


} // namespace proteome
} // namespace pwiz
