// (C) Copyright Jonathan Turkanis 2005.
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt.)

// See http://www.boost.org/libs/iostreams for documentation.

#include <cassert>
#include <string>
#include <boost/iostreams/stream_facade.hpp>
#include <libs/iostreams/example/container_device.hpp>

namespace io = boost::iostreams;
namespace ex = boost::iostreams::example;

int main()
{
    using namespace std;
    typedef ex::container_sink<string> string_sink;

    string                          result;
    io::stream_facade<string_sink>  out(result);
    out << "Hello World!";
    out.flush();
    assert(result == "Hello World!");
}
