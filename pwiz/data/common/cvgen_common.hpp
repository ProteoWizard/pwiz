//
// $Id$
//
//
// Original author: Brian Pratt <bspratt@proteinms dot net>
//
// Copyright 2026 University of Washington - Seattle, WA
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


#ifndef _CVGEN_COMMON_HPP_
#define _CVGEN_COMMON_HPP_


#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace cv {


// Simple character replacement for preprocessor defines and similar use cases
inline char toAllowableChar(char a)
{
    return isalnum(a) ? a : '_';
}


// Map of multi-byte UTF-8 sequences to their encoded representations
static const std::map<std::string, std::string> utf8EncodingMap = {
    {"²", "__sq__"},        // superscript 2 (U+00B2)
    {"³", "__cu__"},        // superscript 3 (U+00B3)
    {"¹", "__sup1__"},      // superscript 1 (U+00B9)
    {"°", "__deg__"},       // degree sign (U+00B0)
    {"Å", "__angstrom__"},  // angstrom (U+00C5)
    {"α", "__alpha__"},     // greek alpha (U+03B1)
    {"β", "__beta__"},      // greek beta (U+03B2)
    {"γ", "__gamma__"},     // greek gamma (U+03B3)
    {"δ", "__delta__"},     // greek delta (U+03B4)
    {"µ", "__micro__"},     // micro sign (U+00B5)
    {"Δ", "__Delta__"},     // greek Delta (U+0394)
    {"±", "__plusminus__"}  // plus-minus (U+00B1)
};

inline std::string toEscapedCharacters(const std::string& name)
{
    std::string encoded;
    encoded.reserve(name.length() * 2); // Reserve more space for encoded chars

    for (size_t i = 0; i < name.length(); )
    {
        unsigned char uc = static_cast<unsigned char>(name[i]);
        
        // Check for multi-byte UTF-8 sequences (first byte >= 0x80)
        bool foundUtf8 = false;
        if (uc >= 0x80) // Non-ASCII character
        {
            // First try our known UTF-8 sequences
            for (const auto& pair : utf8EncodingMap)
            {
                const std::string& utf8Seq = pair.first;
                if (i + utf8Seq.length() <= name.length() &&
                    name.substr(i, utf8Seq.length()) == utf8Seq)
                {
                    encoded += pair.second;
                    i += utf8Seq.length();
                    foundUtf8 = true;
                    break;
                }
            }
            
            // If not in our map, encode the UTF-8 sequence as hex
            if (!foundUtf8)
            {
                // Determine UTF-8 sequence length from first byte
                size_t seqLen = 1;
                if ((uc & 0xE0) == 0xC0) seqLen = 2;      // 110xxxxx - 2 bytes
                else if ((uc & 0xF0) == 0xE0) seqLen = 3; // 1110xxxx - 3 bytes
                else if ((uc & 0xF8) == 0xF0) seqLen = 4; // 11110xxx - 4 bytes
                
                // Encode each byte as hex
                for (size_t j = 0; j < seqLen && i + j < name.length(); j++)
                {
                    char buf[7];
                    std::snprintf(buf, sizeof(buf), "_x%02X_", static_cast<unsigned char>(name[i + j]));
                    encoded += std::string(buf);
                }
                i += seqLen;
                foundUtf8 = true;
            }
        }
        
        if (!foundUtf8)
        {
            char c = static_cast<char>(uc);
            
            // Check if it's alphanumeric or underscore - pass through
            // Safe to call isalnum since uc < 0x80 (ASCII range)
            if (isalnum(uc) || c == '_')
            {
                encoded.push_back(c);
            }
            else
            {
                // All other ASCII characters become underscore
                encoded.push_back('_');
            }
            i++;
        }
    }
    return encoded;
}

} // namespace cv
} // namespace pwiz


#endif // _CVGEN_COMMON_HPP_
