// base64.hpp: Base64 filter

// Copyright Takeshi Mouri 2006, 2007.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

// See http://hamigaki.sourceforge.jp/libs/iostreams for library home page.

#ifndef HAMIGAKI_IOSTREAMS_FILTER_BASE64_HPP
#define HAMIGAKI_IOSTREAMS_FILTER_BASE64_HPP

#include <boost/iostreams/arbitrary_positional_facade.hpp>
#include <boost/iostreams/detail/ios.hpp>
#include <boost/iostreams/read.hpp>
#include <boost/iostreams/write.hpp>
#include <boost/integer.hpp>
#include <algorithm>

namespace boost { namespace iostreams {

struct base64_traits
{
    static const char padding = '=';

    static char encode(unsigned char n)
    {
        const char table[] =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            "abcdefghijklmnopqrstuvwxyz"
            "0123456789+/";

        return table[n];
    }

    static unsigned char decode(char c)
    {
        // TODO: should support non-ASCII
        const unsigned char table[] =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,

            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,

            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0x3E, 0xFF, 0xFF, 0xFF, 0x3F,

            0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B,
            0x3C, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,

            0xFF, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06,
            0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E,

            0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16,
            0x17, 0x18, 0x19, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,

            0xFF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20,
            0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28,

            0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30,
            0x31, 0x32, 0x33, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        };
        unsigned char uc = static_cast<unsigned char>(c);
        if (static_cast<std::size_t>(uc) >= sizeof(table))
            throw BOOST_IOSTREAMS_FAILURE("bad Base64 sequence");
        unsigned char val = table[uc];
        if (val == 0xFF)
            throw BOOST_IOSTREAMS_FAILURE("bad Base64 sequence");
        return val;
    }
};

struct urlsafe_base64_traits
{
    static const char padding = '=';

    static char encode(unsigned char n)
    {
        const char table[] =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            "abcdefghijklmnopqrstuvwxyz"
            "0123456789-_";

        return table[n];
    }

    static unsigned char decode(char c)
    {
        // TODO: should support non-ASCII
        const unsigned char table[] =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,

            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,

            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3E, 0xFF, 0xFF,

            0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B,
            0x3C, 0x3D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,

            0xFF, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06,
            0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E,

            0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16,
            0x17, 0x18, 0x19, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F,

            0xFF, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20,
            0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28,

            0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30,
            0x31, 0x32, 0x33, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        };
        unsigned char uc = static_cast<unsigned char>(c);
        if (static_cast<std::size_t>(uc) >= sizeof(table))
            throw BOOST_IOSTREAMS_FAILURE("bad URL-safe Base64 sequence");
        unsigned char val = table[uc];
        if (val == 0xFF)
            throw BOOST_IOSTREAMS_FAILURE("bad URL-safe Base64 sequence");
        return val;
    }
};

template<class Traits>
class basic_base64_encoder
    : public arbitrary_positional_facade<basic_base64_encoder<Traits>, char, 3>
{
    friend class core_access;

    typedef boost::uint_t<24>::fast uint24_t;

public:
    typedef char char_type;

    struct category
        : public boost::iostreams::output
        , public boost::iostreams::filter_tag
        , public boost::iostreams::multichar_tag
        , public boost::iostreams::closable_tag
    {};

private:
    static uint24_t char_to_uint24(char c)
    {
        return static_cast<uint24_t>(static_cast<unsigned char>(c) & 0xFF);
    }

    static void encode(char* dst, const char* src)
    {
        uint24_t tmp =
            (char_to_uint24(src[0]) << 16) |
            (char_to_uint24(src[1]) <<  8) |
            (char_to_uint24(src[2])      ) ;

        dst[0] = Traits::encode((tmp >> 18) & 0x3F);
        dst[1] = Traits::encode((tmp >> 12) & 0x3F);
        dst[2] = Traits::encode((tmp >>  6) & 0x3F);
        dst[3] = Traits::encode((tmp      ) & 0x3F);
    }

    template<class Sink>
    std::streamsize write_blocks(Sink& sink, const char* s, std::streamsize n)
    {
        for (int i = 0; i < n; ++i)
        {
            char buf[4];
            encode(buf, s);
            boost::iostreams::write(sink, buf, sizeof(buf));
            s += 3;
        }
        return n*3;
    }

    template<class Sink>
    void close_with_flush(Sink& sink, const char* s, std::streamsize n)
    {
        if (n != 0)
        {
            char src[3] = { 0, 0, 0 };
            std::copy(s, s+n, &src[0]);

            char buf[4];
            encode(buf, src);

            if (n == 1)
                buf[2] = Traits::padding;
            buf[3] = Traits::padding;

            boost::iostreams::write(sink, buf, sizeof(buf));
        }
    }
};

template<class Traits>
class basic_base64_decoder
    : public arbitrary_positional_facade<basic_base64_decoder<Traits>, char, 3>
{
    friend class core_access;

    typedef boost::uint_t<24>::fast uint24_t;
    typedef boost::uint_t<6>::least uint6_t;

public:
    typedef char char_type;

    struct category
        : public boost::iostreams::input
        , public boost::iostreams::filter_tag
        , public boost::iostreams::multichar_tag
        , public boost::iostreams::closable_tag
    {};

private:
    static char uint24_to_char(uint24_t n)
    {
        return static_cast<char>(static_cast<unsigned char>(n & 0xFF));
    }

    static uint24_t decode(char c)
    {
        return Traits::decode(c);
    }

    static std::streamsize decode(char* dst, const char* src)
    {
        char buf[4];
        std::copy(src, src+4, buf);

        if (src[3] == Traits::padding)
        {
            buf[3] = Traits::encode(0);
            if (src[2] == Traits::padding)
                buf[2] = Traits::encode(0);
        }

        uint24_t tmp =
            (decode(buf[0]) << 18) |
            (decode(buf[1]) << 12) |
            (decode(buf[2]) <<  6) |
            (decode(buf[3])      ) ;

        std::streamsize n = 0;
        dst[n++] = uint24_to_char(tmp >> 16);
        if (src[2] != Traits::padding)
            dst[n++] = uint24_to_char(tmp >>  8);
        if (src[3] != Traits::padding)
            dst[n++] = uint24_to_char(tmp      );
        return n;
    }

    template<class Source>
    std::streamsize read_blocks(Source& src, char* s, std::streamsize n)
    {
        std::streamsize total = 0;
        for (int i = 0; i < n; ++i)
        {
            char buf[4];
            std::streamsize res =
                boost::iostreams::read(src, buf, sizeof(buf));
            if (res == -1)
                break;
            std::streamsize amt = decode(s, buf);
            s += amt;
            total += amt;
        }
        return (total != 0) ? total : -1;
    }

    template<class Source>
    void close_with_flush(Source&, const char*, std::streamsize)
    {
    }
};

typedef basic_base64_encoder<base64_traits> base64_encoder;
typedef basic_base64_decoder<base64_traits> base64_decoder;

typedef basic_base64_encoder<urlsafe_base64_traits> urlsafe_base64_encoder;
typedef basic_base64_decoder<urlsafe_base64_traits> urlsafe_base64_decoder;

} } // End namespaces iostreams, boost.

#endif // HAMIGAKI_IOSTREAMS_FILTER_BASE64_HPP
