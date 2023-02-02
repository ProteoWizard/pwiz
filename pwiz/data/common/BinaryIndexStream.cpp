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


#include "BinaryIndexStream.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>


using boost::shared_ptr;


namespace pwiz {
namespace data {


namespace {

template<typename _Ty>
struct stream_vector_reader
{
    istream& operator() (istream& is, _Ty& value) const {return is >> value;}
};

template<typename _Ty, typename reader_type = stream_vector_reader<_Ty> >
class stream_vector_const_iterator
{
    public:

	typedef stream_vector_const_iterator<_Ty, reader_type> _Myt;
    typedef std::random_access_iterator_tag iterator_category;
	typedef _Ty value_type;
	typedef std::ptrdiff_t difference_type;
	typedef value_type* pointer;
	typedef value_type& reference;
    typedef const value_type* const_pointer;
	typedef const value_type& const_reference;

    typedef boost::iostreams::stream_offset stream_offset;

	stream_vector_const_iterator()
    : begin_(0), end_(0), next_(0)
	{
	}

    stream_vector_const_iterator(boost::shared_ptr<std::istream> streamPtr,
                                 size_t value_size,
                                 reader_type reader,
                                 stream_offset begin = 0,
                                 stream_offset end = std::numeric_limits<stream_offset>::max())
    : streamPtr_(streamPtr), value_size_(value_size),
      begin_(begin), end_(end), next_(begin_), cur_(end), reader_(reader)
	{
	}

	reference operator*() const
	{
        if (cur_ != next_)
        {
            cur_ = next_;
            streamPtr_->seekg(next_);
            reader_(*streamPtr_, curItem_);
        }
	    return curItem_;
	}

	pointer operator->() const
	{
		return &curItem_;
	}

	_Myt& operator++()
	{
        next_ += value_size_;
		return (*this);
	}

	_Myt operator++(int)
	{
		_Myt _Tmp = *this;
		++*this;
		return (_Tmp);
	}

	_Myt& operator--()
	{
		next_ -= value_size_;
		return (*this);
	}

	_Myt operator--(int)
	{
		_Myt _Tmp = *this;
		--*this;
		return (_Tmp);
	}

	_Myt& operator+=(difference_type _Off)
	{
		next_ += _Off * value_size_;
		return (*this);
	}

	_Myt operator+(difference_type _Off) const
	{
		_Myt _Tmp = *this;
		return (_Tmp += _Off);
	}

	_Myt& operator-=(difference_type _Off)
	{
		return (*this += -_Off);
	}

	_Myt operator-(difference_type _Off) const
	{
		_Myt _Tmp = *this;
		return (_Tmp -= _Off);
	}

	difference_type operator-(const _Myt& that) const
	{
        bool gotThis = this->streamPtr_.get() != NULL;
        bool gotThat = that.streamPtr_.get() != NULL;

        if (gotThis && gotThat)
            return (next_ - that.next_) / value_size_;
        else if (!gotThis && !gotThat) // end() - end()
            return 0;
        else if (gotThis)
            return -(end_ - begin_) / value_size_;
        else // gotThat
            return (that.end_ - that.begin_) / that.value_size_;
	}

	bool operator==(const _Myt& that) const
	{
        bool gotThis = this->streamPtr_.get() != NULL;
        bool gotThat = that.streamPtr_.get() != NULL;

        if (gotThis && gotThat)
            return next_ == that.next_;
        else if (!gotThis && !gotThat) // end() == end()
            return true;
        else if (gotThis)
            return next_ >= end_;
        else // gotThat
            return that.next_ >= that.end_;
	}

	bool operator!=(const _Myt& _Right) const
	{
		return (!(*this == _Right));
	}

	bool operator<(const _Myt& _Right) const
	{
		return streamPtr_.get() && (next_ < _Right.next_) ;
	}

	bool operator>(const _Myt& _Right) const
	{
		return (_Right < *this);
	}

	bool operator<=(const _Myt& _Right) const
	{
		return (!(_Right < *this));
	}

	bool operator>=(const _Myt& _Right) const
	{
		return (!(*this < _Right));
	}

    private:
    boost::shared_ptr<std::istream> streamPtr_;
    size_t value_size_;
    boost::iostreams::stream_offset begin_, end_, next_;
    mutable boost::iostreams::stream_offset cur_;
    mutable _Ty curItem_;
    reader_type reader_;
};

} // namespace


class BinaryIndexStream::Impl
{
    shared_ptr<iostream> isPtr_;

    stream_offset streamLength_;
    boost::uint64_t maxIdLength_;

    size_t size_;
    size_t entrySize_; // space-padded so entries are constant-length

    struct EntryIdLessThan { bool operator() (const Entry& lhs, const Entry& rhs) const {return lhs.id < rhs.id;} };
    struct EntryIndexLessThan { bool operator() (const Entry& lhs, const Entry& rhs) const {return lhs.index < rhs.index;} };

    const size_t indexedMetadataHeaderSize_ = 40 + sizeof(int64_t);

    // the index structure:
    //
    // int64_t: indexed file size (if this changes, the index is stale)
    //
    // 40 bytes of ASCII text: indexed file SHA1 (if this changes, the index is stale)
    //
    // std::stream_offset: stream length (if overwriting an existing index in the same stream, the existing index may be longer
    // than the new index, so the length marks the correct end point)
    //
    // uint64_t: maximum id length + 1 (all ids are padded with spaces to this length)
    //
    // a vector of index entries sorted by index (O(1) access)
    //
    // a vector of index entries sorted by id (O(logN) access)

    struct EntryReader
    {
        EntryReader(boost::uint64_t maxIdLength = 0) : maxIdLength_(maxIdLength) {}
        istream& operator() (istream& is, Entry& entry) const
        {
            is >> entry.id;
            is.seekg(maxIdLength_ - entry.id.length(), std::ios::cur);
            is.read(reinterpret_cast<char*>(&entry.index), sizeof(entry.index));
            is.read(reinterpret_cast<char*>(&entry.offset), sizeof(entry.offset));
            return is;
        }
        private: boost::uint64_t maxIdLength_;
    };

    typedef stream_vector_const_iterator<Entry, EntryReader> IndexStreamIterator;

    EntryReader entryReader_;
    mutable boost::mutex io_mutex;


    public:

    Impl(shared_ptr<iostream> isPtr) : isPtr_(isPtr)
    {
        if (!isPtr_.get())
            throw runtime_error("[BinaryIndexStream::ctor] Stream is null");

        isPtr_->clear();
        isPtr_->seekg(indexedMetadataHeaderSize_); // start reading after the hash and original file size
        isPtr_->read(reinterpret_cast<char*>(&streamLength_), sizeof(streamLength_));
        isPtr_->read(reinterpret_cast<char*>(&maxIdLength_), sizeof(maxIdLength_));
        if (*isPtr_)
        {
            // calculate entry size (depends on max. id length)
            entrySize_ = maxIdLength_ + sizeof(streamLength_) + sizeof(maxIdLength_);

            size_t headerSize = sizeof(streamLength_) + sizeof(maxIdLength_);

            // calculate number of entries (depends on entry size and stream length)
            size_ = (streamLength_ - headerSize) / (entrySize_ * 2);

            entryReader_ = EntryReader(maxIdLength_);
        }
        else
            size_ = maxIdLength_ = streamLength_ = 0;
    }

    ~Impl() {}

    virtual void create(std::vector<Entry>& entries)
    {
        isPtr_->clear();

        // if original file size and hash are missing, write dummy values
        if (isPtr_->tellp() == stream_offset(0))
            isPtr_->write(string(indexedMetadataHeaderSize_, 0).c_str(), indexedMetadataHeaderSize_);
        else
            isPtr_->seekp(indexedMetadataHeaderSize_); // start writing after the hash and original file size

        isPtr_->clear();

        size_ = entries.size();

        // determine max. id length
        auto longestIdEntryItr = std::max_element(entries.begin(), entries.end(),
            [](const Entry& lhs, const Entry& rhs) { return lhs.id.length() < rhs.id.length(); });
        maxIdLength_ = longestIdEntryItr->id.length() + 1; // space-terminated

        // sanity check
        if (maxIdLength_ > 2000)
            throw runtime_error("[BinaryIndexStream::create] creating index with huge ids (" + longestIdEntryItr->id + ") probably means ids are not being parsed correctly");

        // entry size depends on max. id length
        entrySize_ = maxIdLength_ + sizeof(streamLength_) + sizeof(maxIdLength_);

        size_t headerSize = sizeof(streamLength_) + sizeof(maxIdLength_);

        // write stream length (depends on entry size and number of entries)
        streamLength_ = headerSize + entrySize_ * size_ * 2;
        isPtr_->write(reinterpret_cast<const char*>(&streamLength_), sizeof(streamLength_));

        // write max. id length
        isPtr_->write(reinterpret_cast<const char*>(&maxIdLength_), sizeof(maxIdLength_));

        // padding buffer
        string padding(maxIdLength_, ' ');

        // sort entries by index
        sort(entries.begin(), entries.end(), EntryIndexLessThan());

        // write entries sorted by index
        for(const Entry& entry : entries)
        {
            isPtr_->write(entry.id.c_str(), entry.id.length());
            isPtr_->write(padding.c_str(), maxIdLength_ - entry.id.length());
            isPtr_->write(reinterpret_cast<const char*>(&entry.index), sizeof(entry.index));
            isPtr_->write(reinterpret_cast<const char*>(&entry.offset), sizeof(entry.offset));
        }

        // sort entries by id
        sort(entries.begin(), entries.end(), EntryIdLessThan());

        // write entries sorted by id
        for(const Entry& entry : entries)
        {
            isPtr_->write(entry.id.c_str(), entry.id.length());
            isPtr_->write(padding.c_str(), maxIdLength_ - entry.id.length());
            isPtr_->write(reinterpret_cast<const char*>(&entry.index), sizeof(entry.index));
            isPtr_->write(reinterpret_cast<const char*>(&entry.offset), sizeof(entry.offset));
        }

        // flush stream
        isPtr_->sync();

        entryReader_ = EntryReader(maxIdLength_);
    }

    virtual size_t size() const
    {
        return size_;
    }

    virtual EntryPtr find(const std::string& id) const
    {
        if (size_ == 0)
            return EntryPtr();

        EntryPtr entryPtr(new Entry); entryPtr->id = id;
        const stream_offset indexBegin = indexedMetadataHeaderSize_ + sizeof(streamLength_) + sizeof(maxIdLength_) + entrySize_ * size_;
        const stream_offset indexEnd = indexedMetadataHeaderSize_ + streamLength_;
      
        {
            boost::mutex::scoped_lock io_lock(io_mutex);
            isPtr_->clear();

            // binary search is done directly on the index stream
            IndexStreamIterator itr(isPtr_, entrySize_, entryReader_, indexBegin, indexEnd);
            itr = lower_bound(itr, IndexStreamIterator(), *entryPtr, EntryIdLessThan());

            // return null if search went to indexEnd
            if (itr == IndexStreamIterator())
                return EntryPtr();

            // update entry from iterator
            *entryPtr = *itr;
        }

        // return null if the returned iterator isn't exactly equal to the queried one
        if (entryPtr->id != id)
            return EntryPtr();

        return entryPtr;
    }

    virtual EntryPtr find(size_t index) const
    {
        if (index >= size_)
            return EntryPtr();

        EntryPtr entryPtr(new Entry);
        const stream_offset indexBegin = indexedMetadataHeaderSize_ + sizeof(streamLength_) + sizeof(maxIdLength_);
        const stream_offset entryOffset = indexBegin + index * entrySize_;

        {
            boost::mutex::scoped_lock io_lock(io_mutex);
            isPtr_->clear();
            isPtr_->seekg(entryOffset);
            entryReader_(*isPtr_, *entryPtr);
        }
        return entryPtr;
    }
};


PWIZ_API_DECL BinaryIndexStream::BinaryIndexStream(shared_ptr<iostream> isPtr) : impl_(new Impl(isPtr)) {}
PWIZ_API_DECL void BinaryIndexStream::create(vector<Entry>& entries) {impl_->create(entries);}
PWIZ_API_DECL size_t BinaryIndexStream::size() const {return impl_->size();}
PWIZ_API_DECL Index::EntryPtr BinaryIndexStream::find(const string& id) const {return impl_->find(id);}
PWIZ_API_DECL Index::EntryPtr BinaryIndexStream::find(size_t index) const {return impl_->find(index);}


} // namespace data
} // namespace pwiz
