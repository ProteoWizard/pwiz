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


#include "Logger.hpp"

#pragma unmanaged
#include "pwiz/utility/misc/Std.hpp"
#include "../SchemaUpdater.hpp"
#include "../Logger.hpp"
#include <boost/log/expressions.hpp>
#include <boost/log/utility/setup/common_attributes.hpp>
#include <boost/log/sinks/sync_frontend.hpp>
#include <boost/log/sinks/text_ostream_backend.hpp>
#include <boost/core/null_deleter.hpp>
#include <boost/make_shared.hpp>
#include <iomanip>

using boost::format;
namespace expr = boost::log::expressions;
namespace sinks = boost::log::sinks;
BOOST_LOG_ATTRIBUTE_KEYWORD(severity, "Severity", NativeIDPicker::MessageSeverity::domain)

struct severity_tag;
// The operator is used when putting the severity level to log
boost::log::formatting_ostream& operator<<
(
    boost::log::formatting_ostream& strm,
    const boost::log::to_log_manip<NativeIDPicker::MessageSeverity::domain, severity_tag>& manip
)
{
    if (manip.get() >= NativeIDPicker::MessageSeverity::Warning)
        strm << '\n' << NativeIDPicker::MessageSeverity::get_by_value(manip.get()).get().str() << ": ";
    return strm;
}

#pragma managed


namespace IDPicker {


namespace {


    // streambuf that accepts stream output 
    // a TextReader subclass that contains a gcroot<ostream> ?
    ref class NativeTextReader : public System::IO::TextReader
    {
        public:
        NativeTextReader()
        {
            nativeStream_ = new std::stringstream();
        }

        ~NativeTextReader()
        {
            delete nativeStream_;
        }

        virtual int Peek() override
        {
            return nativeStream_->peek();
        }

        virtual int Read() override
        {
            return nativeStream_->get();
        }

        std::ostream* stream() { return nativeStream_; }

        private:
        std::stringstream* nativeStream_;
    };
}

void Logger::Initialize()
{
    System::GC::KeepAlive(Logger::Reader);
}

static Logger::Logger()
{
    NativeTextReader^ nativeReader = gcnew NativeTextReader();
    Logger::reader = nativeReader;

    typedef sinks::synchronous_sink<sinks::text_ostream_backend> text_sink;
    boost::shared_ptr<text_sink> sink = boost::make_shared<text_sink>();
    sink->locked_backend()->add_stream(boost::shared_ptr<std::ostream>(nativeReader->stream(), boost::null_deleter()));
    sink->locked_backend()->auto_flush(true);
    //sink->set_filter(severity < NativeIDPicker::MessageSeverity::Error);
    sink->set_formatter(expr::stream << expr::attr<NativeIDPicker::MessageSeverity::domain, severity_tag>("Severity") << expr::smessage);
    boost::log::core::get()->add_sink(sink);
}

System::IO::TextReader^ Logger::Reader::get() { return reader; }


} // namespace IDPicker
