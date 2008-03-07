//
// Base64.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _BASE64_HPP_
#define _BASE64_HPP_


#include <cstddef> // for size_t


namespace pwiz {
namespace util {

	
/// Base-64 binary->text encoding
/// (maps 3 bytes <-> 4 chars)
namespace Base64
{
    /// Returns buffer size required by binary->text conversion.
    size_t binaryToTextSize(size_t byteCount);

    /// binary -> text conversion
    /// - Caller must allocate buffer
    /// - Buffer will not be null-terminated
    /// - Returns the actual number of bytes written
    size_t binaryToText(const void* from, size_t byteCount, char* to);

    /// Returns sufficient buffer size for text->binary conversion.
    size_t textToBinarySize(size_t charCount);

    /// text -> binary conversion 
    /// - Caller must allocate buffer
    /// - Buffer will not be null-terminated
    /// - Returns the actual number of bytes written
    size_t textToBinary(const char* from, size_t charCount, void* to);

} // namespace Base64


} // namespace util
} // namespace pwiz


#endif // _BASE64_HPP_

