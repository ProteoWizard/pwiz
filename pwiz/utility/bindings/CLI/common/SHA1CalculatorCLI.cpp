#include "SHA1CalculatorCLI.hpp"

namespace pwiz {
namespace CLI {
namespace util {
    std::string ToStdStr(System::String^ str)
    {
        return msclr::interop::marshal_as<std::string>(str);
    }

    System::String^ SHA1CalculatorCLI::Hash(System::String^ buffer)
    {
        return ToSystemString(SHA1Calculator::hash(ToStdStr(buffer)));
    }

    System::String^ SHA1CalculatorCLI::HashFile(System::String^ fileName)
    {
        return ToSystemString(SHA1Calculator::hashFile(ToStdStr(fileName)));
    }
} // util
} // CLI
} // pwiz