//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include <iostream>
#include <fstream>
#include <sstream>
#include <iomanip>
#include <boost/iostreams/operations.hpp>
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"

namespace bio = boost::iostreams;

using std::ios;
using std::iostream;
using std::istream;
using std::ostream;

using std::fstream;
using std::ifstream;
using std::ofstream;

using std::stringstream;
using std::istringstream;
using std::ostringstream;

using std::getline;

using std::streampos;
using std::streamoff;
using std::streamsize;

using std::cin;
using std::cout;
using std::cerr;
using std::endl;
using std::flush;

using std::setprecision;
using std::setw;
using std::setfill;
using std::setbase;

using std::showbase;
using std::showpoint;
using std::showpos;
using std::boolalpha;

using boost::lexical_cast;
using boost::bad_lexical_cast;
