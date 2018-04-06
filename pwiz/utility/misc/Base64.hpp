//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _BASE64_HPP_
#define _BASE64_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <cstddef> // for size_t


namespace pwiz {
namespace util {

	
/// Base-64 binary->text encoding
/// (maps 3 bytes <-> 4 chars)
namespace Base64
{
    /// Returns buffer size required by binary->text conversion.
    PWIZ_API_DECL size_t binaryToTextSize(size_t byteCount);

    /// binary -> text conversion
    /// - Caller must allocate buffer
    /// - Buffer will not be null-terminated
    /// - Returns the actual number of bytes written
    PWIZ_API_DECL size_t binaryToText(const void* from, size_t byteCount, char* to);

    /// Returns sufficient buffer size for text->binary conversion.
    PWIZ_API_DECL size_t textToBinarySize(size_t charCount);

    /// text -> binary conversion 
    /// - Caller must allocate buffer
    /// - Buffer will not be null-terminated
    /// - Returns the actual number of bytes written
    PWIZ_API_DECL size_t textToBinary(const char* from, size_t charCount, void* to);

} // namespace Base64


} // namespace util
} // namespace pwiz


#endif // _BASE64_HPP_

