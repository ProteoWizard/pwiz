/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GTOKENIZER_H__
#define __GTOKENIZER_H__

#include <istream>
#include <map>

namespace GClasses {

class GCharGroup;

/// This is a simple tokenizer that reads a file, one token at-a-time.
/// Several of the methods in this class require a string of
/// characters to be supplied as a parameter. These are simply an un-ordered
/// set of characters (with no separator between them). The only special
/// character is '-', which is used to indicate a range of characters if it
/// is not the first character in the string. (So, if you want '-' in your
/// set of characters, it should come first.) For example, this
/// string includes all letters: "a-zA-Z", and this string includes all
/// characters that might appear in a floating-point number: "-.,0-9e".
/// (There is no way to include '\0' as a delimeter, since that character
/// indicates the end of the string, but that is okay since '\0' should
/// not occur in text files anyway, and this class is designed for parsing
/// text files.)
class GTokenizer
{
protected:
	std::map<std::string,GCharGroup*> m_charGroups;
	char* m_pBufStart;
	char* m_pBufPos;
	char* m_pBufEnd;
	std::istream* m_pStream;
	size_t m_lineStart;
	size_t m_len;
	size_t m_line;

public:
	/// Opens the specified filename.
	GTokenizer(const char* szFilename);

	/// Uses the provided buffer of data. (If len is 0, then it
	/// will read until a null-terminator is found.)
	GTokenizer(const char* pFile, size_t len);
	~GTokenizer();

	/// Returns the next character in the stream. Returns '\0' if there are
	/// no more characters in the stream. (This could theoretically be ambiguous if the
	/// the next character in the stream is '\0', but presumably this class
	/// is mostly used for parsing text files, and that character should not
	/// occur in a text file.)
	char peek();

	/// Reads until the next character would be one of the specified delimeters.
	/// The delimeter character is not read. Throws an exception if fewer than
	/// minLen characters are read.
	/// The token returned by this method will have been copied into an
	/// internal buffer, null-terminated, and a pointer to that buffer is returned.
	char* nextUntil(const char* szDelimeters = "\t\n\r ", size_t minLen = 1);

	/// Reads until the next character would be one of the specified delimeters,
	/// and the current character is not escapeChar. 
	/// The token returned by this method will have been copied into an
	/// internal buffer, null-terminated, and a pointer to that buffer is returned.
	char* nextUntilNotEscaped(char escapeChar, const char* szDelimeters);

	/// Reads while the character is one of the specified characters. Throws an
	/// exception if fewer than minLen characters are read.
	/// The token returned by this method will have been copied into an
	/// internal buffer, null-terminated, and a pointer to that buffer is returned.
	char* nextWhile(const char* szSet = "-_a-zA-Z0-9", size_t minLen = 1);

	/// Returns the next token delimited by whitespace or a '{' character.
	/// If the next token begins
	/// with single or double quotes, then the token will be delimited by
	/// the quotes instead. If a newline character or the end-of-file is
	/// encountered before the matching quote, then an exception is thrown.
	/// The quotation marks are not included in the token, but they are
	/// consumed by the operation.
	char* nextArg();

	/// Reads past any characters specified in the list of delimeters.
	/// If szDelimeters is NULL, then any characters <= ' ' are considered
	/// to be delimeters. (This method is similar to nextWhile, except that
	/// it does not buffer the characters it reads.)
	void skip(const char* szDelimeters = "\t\n\r ");

	/// Skip until the next character is one of the delimeters.
	/// (This method is the same as nextUntil, except that it does not buffer what it reads.)
	void skipTo(const char* szDelimeters = "\t\n\r ");

	/// Advances past the next 'n' characters. (Stops if the end-of-file is reached.)
	void advance(size_t n);

	/// Reads past the specified string of characters. If the characters
	/// that are read from the file do not exactly match those in the string,
	/// an exception is thrown.
	void expect(const char* szString);

	/// Returns the previously-returned token, except with any of the specified characters
	/// trimmed off of both the beginning and end of the token. For example, if the last
	/// token that was returned was "  tok  ", then this will return "tok".
	/// (Calling this method will not change the value returned by tokenLength.)
	char* trim(const char* szSet = "\t\n\r ");

	/// Returns the current line number. (Begins at 1. Each time a '\n' is encountered,
	/// the line number is incremented. Mac line-endings do not increment the
	/// line number.)
	size_t line();

	/// Returns the current column index, which is the number of characters that have
	/// been read since the last newline character, plus 1.
	size_t col();

	/// Returns the number of remaining bytes to be read from the file.
	size_t remaining();

	/// Returns the length of the last token that was returned.
	size_t tokenLength();

protected:
	GCharGroup* getCharGroup(const char* szChars);
	void growBuf();
	char get();
	void bufferChar(char c);
};

} // namespace GClasses

#endif // __GTOKENIZER_H__
