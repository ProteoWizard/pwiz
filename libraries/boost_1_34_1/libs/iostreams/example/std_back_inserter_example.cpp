// (C) Copyright Jonathan Turkanis 2005.
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt.)

// See http://www.boost.org/libs/iostreams for documentation.

#include <cassert>
#include <iterator>  // back_inserter
#include <string>
#include <boost/iostreams/filtering_stream.hpp>

namespace io = boost::iostreams;

int main()
{
    using namespace std;

    string                 result;
    io::filtering_ostream  out(std::back_inserter(result));
    out << "Hello World!";
    out.flush();
    assert(result == "Hello World!");
}
