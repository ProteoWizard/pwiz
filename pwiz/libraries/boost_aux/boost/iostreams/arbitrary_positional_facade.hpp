// arbitrary_positional_facade.hpp: a base class of repositional stream facade

// Copyright Takeshi Mouri 2006, 2007.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

// See http://hamigaki.sourceforge.jp/libs/iostreams for library home page.

#ifndef HAMIGAKI_IOSTREAMS_ARBITRARY_POSITIONAL_FACADE_HPP
#define HAMIGAKI_IOSTREAMS_ARBITRARY_POSITIONAL_FACADE_HPP

#include <boost/config.hpp>
#include <boost/iostreams/detail/ios.hpp>
#include <boost/iostreams/positioning.hpp>
#include <boost/iostreams/traits.hpp>
#include <boost/assert.hpp>

namespace boost { namespace iostreams {

template<class Derived, class CharT, std::streamsize MaxBlockSize>
class arbitrary_positional_facade;

class core_access
{
#if defined(BOOST_NO_MEMBER_TEMPLATE_FRIENDS)
public:
#else
    template<class Derived, class CharT, std::streamsize MaxBlockSize>
    friend class arbitrary_positional_facade;

    friend struct device_operations;

    template<class Device>
    friend struct filter_operations;
#endif

    template<class RepositionalSource, class CharT>
    static std::streamsize read_blocks(
        RepositionalSource& src, CharT* s, std::streamsize n)
    {
        return src.read_blocks(s, n);
    }

    template<class RepositionalInputFilter, class Source>
    static std::streamsize read_blocks(
        RepositionalInputFilter& filter, Source& src,
        typename boost::iostreams::char_type_of<Source>::type* s,
        std::streamsize n)
    {
        return filter.read_blocks(src, s, n);
    }

    template<class RepositionalSink, class CharT>
    static std::streamsize write_blocks(
        RepositionalSink& sink, const CharT* s, std::streamsize n)
    {
        return sink.write_blocks(s, n);
    }

    template<class RepositionalOutputFilter, class Sink>
    static std::streamsize write_blocks(
        RepositionalOutputFilter& filter, Sink& sink,
        const typename boost::iostreams::char_type_of<Sink>::type* s,
        std::streamsize n)
    {
        return filter.write_blocks(sink, s, n);
    }

    template<class RepositionalSink, class CharT>
    static void close_with_flush(
        RepositionalSink& sink, const CharT* s, std::streamsize n)
    {
        return sink.close_with_flush(s, n);
    }

    template<class RepositionalOutputFilter, class Sink>
    static void close_with_flush(
        RepositionalOutputFilter& filter, Sink& sink,
        const typename boost::iostreams::char_type_of<Sink>::type* s,
        std::streamsize n)
    {
        return filter.close_with_flush(sink, s, n);
    }

    template<class RepositionalDevice>
    static std::streampos seek_blocks(
        RepositionalDevice& dev,
        boost::iostreams::stream_offset off, BOOST_IOS::seekdir way)
    {
        return dev.seek_blocks(off, way);
    }

    struct device_operations
    {
        template<class RepositionalDevice, class CharT>
        std::streamsize read_blocks(
            RepositionalDevice& t, CharT* s, std::streamsize n) const
        {
            return core_access::read_blocks(t, s, n);
        }

        template<class RepositionalDevice, class CharT>
        std::streamsize write_blocks(
            RepositionalDevice& t, const CharT* s, std::streamsize n) const
        {
            return core_access::write_blocks(t, s, n);
        }
    };

    template<class Device>
    struct filter_operations
    {
        typedef typename boost::iostreams::
            char_type_of<Device>::type char_type;

        Device* dev_ptr_;

        explicit filter_operations(Device& dev) : dev_ptr_(&dev) {}

        template<class RepositionalInputFilter>
        std::streamsize read_blocks(
            RepositionalInputFilter& t, char_type* s, std::streamsize n) const
        {
            return core_access::read_blocks(t, *dev_ptr_, s, n);
        }

        template<class RepositionalOutputFilter>
        std::streamsize write_blocks(
            RepositionalOutputFilter& t,
            const char_type* s, std::streamsize n) const
        {
            return core_access::write_blocks(t, *dev_ptr_, s, n);
        }
    };
};

template<class Derived, class CharT, std::streamsize MaxBlockSize>
class arbitrary_positional_facade
{
private:
    typedef CharT char_type;

    Derived& derived()
    {
      return *static_cast<Derived*>(this);
    }

protected:
    typedef arbitrary_positional_facade<
        Derived,CharT,MaxBlockSize> arbitrary_positional_facade_;

    void block_size(std::streamsize n)
    {
        block_size_ = n;
    }

public:
    arbitrary_positional_facade() : block_size_(MaxBlockSize), count_(0)
    {
    }

    explicit arbitrary_positional_facade(std::streamsize block_size)
        : block_size_(block_size), count_(0)
    {
        BOOST_ASSERT(block_size_ <= MaxBlockSize);
    }

    std::streamsize read(char_type* s, std::streamsize n)
    {
        return read_impl(core_access::device_operations(), s, n);
    }

    template<class Source>
    std::streamsize read(Source& src, char_type* s, std::streamsize n)
    {
        return read_impl(core_access::filter_operations<Source>(src), s, n);
    }

    std::streamsize write(const char_type* s, std::streamsize n)
    {
        return write_impl(core_access::device_operations(), s, n);
    }

    template<class Sink>
    std::streamsize write(Sink& sink, const char_type* s, std::streamsize n)
    {
        return write_impl(core_access::filter_operations<Sink>(sink), s, n);
    }

    void close()
    {
        BOOST_ASSERT(count_ < block_size_);
        core_access::close_with_flush(derived(), buffer_, count_);
    }

    template<class Sink>
    void close(Sink& sink)
    {
        BOOST_ASSERT(count_ < block_size_);
        core_access::close_with_flush(derived(), sink, buffer_, count_);
    }

    std::streampos seek(
        boost::iostreams::stream_offset off, BOOST_IOS::seekdir way)
    {
        if (way == BOOST_IOS::beg)
        {
            core_access::seek_blocks(derived(), off/block_size_, way);

            std::streamsize skip =
                static_cast<std::streamsize>(off%block_size_);
            if (skip == 0)
                count_ = 0;
            else
            {
                std::streamsize res =
                    core_access::read_blocks(derived(), buffer_, 1);
                if (res != 1)
                    throw BOOST_IOSTREAMS_FAILURE("bad seek");
                count_ = block_size_ - skip;
            }
            return boost::iostreams::offset_to_position(off);
        }
        else if (way == BOOST_IOS::cur)
        {
            std::streampos pos =
                core_access::seek_blocks(
                    derived(), (off-count_)/block_size_, way);

            std::streamsize skip =
                static_cast<std::streamsize>((off-count_)%block_size_);
            if (skip == 0)
            {
                count_ = 0;

                return boost::iostreams::offset_to_position(
                    boost::iostreams::position_to_offset(pos) * block_size_);
            }
            else
            {
                std::streamsize res =
                    core_access::read_blocks(derived(), buffer_, 1);
                if (res != 1)
                    throw BOOST_IOSTREAMS_FAILURE("bad seek");
                count_ = block_size_ - skip;

                return boost::iostreams::offset_to_position(
                    boost::iostreams::position_to_offset(pos) * block_size_
                    + block_size_-count_);
            }
        }
        else
        {
            std::streampos pos =
                core_access::seek_blocks(
                    derived(), (off-block_size_+1)/block_size_, way);

            count_ =
                static_cast<std::streamsize>((-off)%block_size_);
            if (count_ == 0)
            {
                return boost::iostreams::offset_to_position(
                    boost::iostreams::position_to_offset(pos) * block_size_);
            }
            else
            {
                std::streamsize res =
                    core_access::read_blocks(derived(), buffer_, 1);
                if (res != 1)
                    throw BOOST_IOSTREAMS_FAILURE("bad seek");

                return boost::iostreams::offset_to_position(
                    boost::iostreams::position_to_offset(pos) * block_size_
                    + block_size_-count_);
            }
        }
    }

private:
    char_type buffer_[MaxBlockSize];
    std::streamsize block_size_;
    std::streamsize count_;

    template<class Op>
    std::streamsize read_impl(const Op& op, char_type* s, std::streamsize n)
    {
        std::streamsize total = 0;

        if (count_ != 0)
        {
            std::streamsize amt = (std::min)(n, count_);
            char_type* start = buffer_ + (block_size_ - count_);
            s = std::copy(start, start+amt, s);
            n -= amt;
            count_ -= amt;
            total += amt;
        }

        if (n >= block_size_)
        {
            BOOST_ASSERT(count_ == 0);

            std::streamsize request = n/block_size_;
            std::streamsize res =
                op.read_blocks(derived(), s, request);

            if (res != -1)
            {
                s += res;
                n -= res;
                total += res;
            }

            if (res < request*block_size_)
                return total != 0 ? total : -1;
        }

        if (n != 0)
        {
            BOOST_ASSERT(n < block_size_);
            BOOST_ASSERT(count_ == 0);

            std::streamsize res =
                op.read_blocks(derived(), buffer_, 1);

            if (res > 0)
            {
                s = std::copy(buffer_, buffer_+n, s);
                count_ = block_size_ - n;
                total += n;
            }
        }

        return total != 0 ? total : -1;
    }

    template<class Op>
    std::streamsize write_impl(
        const Op& op, const char_type* s, std::streamsize n)
    {
        std::streamsize total = 0;

        if (count_ != 0)
        {
            std::streamsize amt = (std::min)(n, block_size_-count_);
            std::copy(s, s+amt, buffer_+count_);
            s += amt;
            n -= amt;
            count_ += amt;
            total += amt;

            if (count_ == block_size_)
            {
                op.write_blocks(derived(), buffer_, 1);
                count_ = 0;
            }
        }

        if (n >= block_size_)
        {
            BOOST_ASSERT(count_ == 0);

            std::streamsize request = n/block_size_;
            op.write_blocks(derived(), s, request);

            std::streamsize amt = request*block_size_;
            s += amt;
            n -= amt;
            total += amt;
        }

        if (n != 0)
        {
            BOOST_ASSERT(n < block_size_);
            BOOST_ASSERT(count_ == 0);

            std::copy(s, s+n, buffer_);
            count_ = n;
            total += n;
        }

        return total != 0 ? total : -1;
    }
};


} } // End namespaces iostreams, boost.

#endif // HAMIGAKI_IOSTREAMS_ARBITRARY_POSITIONAL_FACADE_HPP
