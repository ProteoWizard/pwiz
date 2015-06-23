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


#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "Logger.hpp"
#include <boost/log/expressions.hpp>
#include <boost/log/utility/setup/common_attributes.hpp>
#include <boost/log/sinks/sync_frontend.hpp>
#include <boost/log/sinks/text_ostream_backend.hpp>
#include "boost/core/null_deleter.hpp"


using namespace pwiz::util;
using namespace IDPicker;
using boost::format;
namespace expr = boost::log::expressions;
namespace sinks = boost::log::sinks;


struct severity_tag;
BOOST_LOG_ATTRIBUTE_KEYWORD(severity, "Severity", MessageSeverity::domain)


// The operator is used when putting the severity level to log
boost::log::formatting_ostream& operator<<
(
boost::log::formatting_ostream& strm,
const boost::log::to_log_manip< MessageSeverity::domain, severity_tag>& manip
)
{
    if (manip.get() >= MessageSeverity::Warning)
        strm << MessageSeverity::get_by_value(manip.get()).get().str() << ": ";
    return strm;
}


void test()
{
    bfs::remove("test.log");

#ifdef WIN32
# define NEWLINE "\r\n"
#else
# define NEWLINE "\n"
#endif

    typedef sinks::synchronous_sink<sinks::text_ostream_backend> text_sink;
    boost::shared_ptr<text_sink> sink = boost::make_shared<text_sink>();
    boost::shared_ptr<ofstream> logStream = boost::make_shared<ofstream>("test.log");

    sink->set_formatter(expr::stream << expr::attr<MessageSeverity::domain, severity_tag>("Severity") << expr::smessage);
    sink->locked_backend()->auto_flush(true);
    sink->locked_backend()->add_stream(logStream);
    sink->set_filter(severity >= MessageSeverity::VerboseInfo);
    boost::log::core::get()->add_sink(sink);

    // Add attributes
    boost::log::add_common_attributes();

    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << "VerboseInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "BriefInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "WarningTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "ErrorTest";

    string log = read_file_header("test.log", 1024).c_str();
    unit_assert_operator_equal("VerboseInfoTest"NEWLINE
                               "BriefInfoTest"NEWLINE
                               "Warning: WarningTest"NEWLINE
                               "Error: ErrorTest"NEWLINE,
                               log);

    logStream->close();
    bfs::remove("test.log");
    logStream->open("test.log");

    sink->set_filter(severity >= MessageSeverity::BriefInfo);
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << "VerboseInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "BriefInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "WarningTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "ErrorTest";

    log = read_file_header("test.log", 1024).c_str();
    unit_assert_operator_equal("BriefInfoTest"NEWLINE
                               "Warning: WarningTest"NEWLINE
                               "Error: ErrorTest"NEWLINE,
                               log);

    logStream->close();
    bfs::remove("test.log");
    logStream->open("test.log");

    sink->set_filter(severity >= MessageSeverity::BriefInfo);
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << "VerboseInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "BriefInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "WarningTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "ErrorTest";

    log = read_file_header("test.log", 1024).c_str();
    unit_assert_operator_equal("BriefInfoTest"NEWLINE
                               "Warning: WarningTest"NEWLINE
                               "Error: ErrorTest"NEWLINE,
                               log);

    logStream->close();
    bfs::remove("test.log");
    logStream->open("test.log");

    sink->set_filter(severity >= MessageSeverity::Warning);
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << "VerboseInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "BriefInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "WarningTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "ErrorTest";

    log = read_file_header("test.log", 1024).c_str();
    unit_assert_operator_equal("Warning: WarningTest"NEWLINE
                               "Error: ErrorTest"NEWLINE,
                               log);

    logStream->close();
    bfs::remove("test.log");
    logStream->open("test.log");

    sink->set_filter(severity >= MessageSeverity::Error);
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << "VerboseInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "BriefInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "WarningTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "ErrorTest";

    log = read_file_header("test.log", 1024).c_str();
    unit_assert_operator_equal("Error: ErrorTest"NEWLINE,
                               log);

    logStream->close();
    bfs::remove("test.log");
    logStream->open("test.log");

    sink->set_filter(severity < MessageSeverity::Error);
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::VerboseInfo) << "VerboseInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::BriefInfo) << "BriefInfoTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Warning) << "WarningTest";
    BOOST_LOG_SEV(logSource::get(), MessageSeverity::Error) << "ErrorTest";

    log = read_file_header("test.log", 1024).c_str();
    unit_assert_operator_equal("VerboseInfoTest"NEWLINE
                               "BriefInfoTest"NEWLINE
                               "Warning: WarningTest"NEWLINE,
                               log);

    logStream->close();
    bfs::remove("test.log");
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
