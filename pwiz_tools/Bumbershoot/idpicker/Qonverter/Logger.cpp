#include "Logger.hpp"
#include <io.h>

BEGIN_IDPICKER_NAMESPACE

bool IsStdOutRedirected()
{
    return _isatty(_fileno(stdout)) == 0;
}

BOOST_LOG_GLOBAL_LOGGER_DEFAULT(logSource, boost::log::sources::severity_logger_mt<MessageSeverity::domain>)

END_IDPICKER_NAMESPACE