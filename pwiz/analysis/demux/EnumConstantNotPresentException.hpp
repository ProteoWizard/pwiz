//
// $Id$
//
//
// Original author: Austin Keller <atkeller .@. uw.edu>
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

#ifndef _ENUMCONSTANTNOTPRESENTEXCEPTION_HPP
#define _ENUMCONSTANTNOTPRESENTEXCEPTION_HPP

#include "pwiz/utility/misc/Std.hpp"

/// An exception class inspired by Java's EnumConstantNotPresentException.
class EnumConstantNotPresentException : public std::runtime_error
{
public:
    /// Constructor with string message
    explicit EnumConstantNotPresentException(const std::string& _Message)
        : runtime_error(_Message) {}

    /// Constructor with char* message
    explicit EnumConstantNotPresentException(const char* _Message)
        : runtime_error(_Message) {}

    /// Required override of destructor for std::exception
    ~EnumConstantNotPresentException() throw() override {}
        
    /// Provides descriptive message of error
    const char* what() const throw() override
    {
        return "Attempted to access enum by name that is not present";
    }
};
#endif