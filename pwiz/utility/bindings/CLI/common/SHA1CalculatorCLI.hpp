#ifndef _SHA1CLI_HPP_CLI_
#define _SHA1CLI_HPP_CLI_
#include "pwiz\utility\misc\SHA1Calculator.hpp"
#include "SharedCLI.hpp"
#include <msclr\marshal_cppstd.h>

namespace pwiz {
namespace CLI {
namespace util {
    using namespace pwiz::util;

    public ref class SHA1CalculatorCLI
    {
        public:
        /// static function to calculate hash of a buffer
        static System::String^ Hash(System::String^ buffer);

        /// static function to calculate hash of a file 
        static System::String^ HashFile(System::String^ filename);
    };
} // util
} // CLI
} // pwiz

#endif // _SHA1CLI_HPP_CLI_