//
// $Id: Exception.hpp 2008 2010-05-29 02:46:49Z brendanx $
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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

#ifndef _STD_HPP_
#define _STD_HPP_

// a meta-header providing including common std headers and using common std classes;
// note that Filesystem.hpp is not included since it depends on Filesystem.cpp

#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/Environment.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"

#include <limits>
using std::numeric_limits;

#include <cmath>
#include <complex>
#include "pwiz/utility/math/round.hpp"
using std::abs;
using std::min;
using std::max;
using std::complex;

using std::swap;
using std::copy;

using std::locale;

#include <memory>
#include <boost/smart_ptr.hpp>
using std::auto_ptr;
using boost::shared_ptr;
using boost::weak_ptr;
using boost::scoped_ptr;

using std::exception;
using std::runtime_error;
using std::out_of_range;
using std::domain_error;
using std::invalid_argument;
using std::length_error;
using std::logic_error;
using std::overflow_error;
using std::range_error;
using std::underflow_error;

#endif // _STD_HPP_
