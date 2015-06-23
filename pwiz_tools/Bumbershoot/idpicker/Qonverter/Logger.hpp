//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2015 Vanderbilt University
//
// Contributor(s):
//


#ifndef _LOGGER_HPP_
#define _LOGGER_HPP_

#include <boost/log/core.hpp>
#include <boost/log/sources/global_logger_storage.hpp>
#include <boost/log/sources/record_ostream.hpp>
#include <boost/log/sources/severity_logger.hpp>
#include "boost/enum.hpp"


#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE


bool IsStdOutRedirected();


BOOST_ENUM(MessageSeverity,
    (VerboseInfo)
    (BriefInfo)
    (Warning)
    (Error)
)

// allow enum values to be the LHS of an equality expression
BOOST_ENUM_DOMAIN_OPERATORS(MessageSeverity);

BOOST_LOG_GLOBAL_LOGGER(logSource, boost::log::sources::severity_logger_mt<MessageSeverity::domain>)

#ifndef PWIZ_LOG_ITER
/// convenient BOOST_LOG_SEV wrapper that makes a BriefInfo log entry for the first and last iteration, and a VerboseInfo log entry for any other iteration
#define PWIZ_LOG_ITER(logger, index, count) BOOST_LOG_SEV((logger), (index) == 0 || (index)+1 == (count) ? MessageSeverity::BriefInfo : MessageSeverity::VerboseInfo)
#endif

END_IDPICKER_NAMESPACE


#endif // _LOGGER_HPP_
