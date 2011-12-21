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
#include "GBitTable.h"

namespace GClasses {

class GCharSet;
class GHeap;

/// This is a helper-class used by GTokenizer
class GTokenizerMapComparer
{
public:
	bool operator() (const char* a, const char* b) const;
};

/// This is a helper-class used by GTokenizer.  Use
/// GTokenizer::charSet to create
class GCharSet
{
protected:
	GBitTable m_bt;

	GCharSet(const char* szChars);
public:

	inline bool find(char c);

	friend class GTokenizer;
};


/// This is a simple tokenizer that reads a file, one token at-a-time.
class GTokenizer
{
protected:
	GHeap* m_pHeap;
	std::map<const char*,GCharSet*,GTokenizerMapComparer> m_charGroups;
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

	/// Returns a GCharSet. Many of the methods in this class require
	/// a GCharSet as a parameter. You get it by calling this method.
	/// szChars is an un-ordered set of characters (with no separator between
	/// them). The only special character is '-', which is used to indicate a
	/// range of characters if it is not the first character in the string.
	/// (So, if you want '-' in your set of characters, it should come first.)
	/// For example, the following string includes all letters: "a-zA-Z", and the
	/// following string includes all characters that might appear in a
	/// floating-point number: "-.,0-9e". (There is no way to include '\0' as
	/// a character in the set, since that character indicates the end of the
	/// string, but that is okay since '\0' should not occur in text files
	/// anyway, and this class is designed for parsing text files.)
	GCharSet& charSet(const char* szChars);

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
	char* nextUntil(GCharSet& delimeters, size_t minLen = 1);

	/// Reads until the next character would be one of the specified delimeters,
	/// and the current character is not escapeChar. 
	/// The token returned by this method will have been copied into an
	/// internal buffer, null-terminated, and a pointer to that buffer is returned.
	char* nextUntilNotEscaped(char escapeChar, GCharSet& delimeters);

	/// Reads while the character is one of the specified characters. Throws an
	/// exception if fewer than minLen characters are read.
	/// The token returned by this method will have been copied into an
	/// internal buffer, null-terminated, and a pointer to that buffer is returned.
	char* nextWhile(GCharSet& set, size_t minLen = 1);

	/// \brief Returns the next token defined by the given delimiters.
	/// \brief Allows quoting " or ' and escapes with an escape
	/// \brief character.
	///
	/// Returns the next token delimited by the given delimiters.
	/// (The default delimiters are white-space or {).
	///
	/// The token may include delimiters if it is enclosed in quotes or
	/// the delimiters are escaped.
	///
	/// If the next token begins with single or double quotes, then the
	/// token will be delimited by the quotes. If a newline character or
	/// the end-of-file is encountered before the matching quote, then
	/// an exception is thrown.  The quotation marks are not included in
	/// the token, but they are consumed by the operation.  The escape
	/// character is ignored inside quotes - unlike what would happen in
	/// C++.
	///
	/// If the first character of the token is not a quotation mark,
	/// then the escape character is used.  If an escape character
	/// preceeds any character, then it is included in the token.  The
	/// escape character is consumed but not included in the token.
	/// Thus, if the input is (The \\rain\\ in \"spain\") (not including
	/// the parentheses) and the esapeChar is '\', then the token read
	/// will be (The \rain\ in "spain").
	///
	/// No token may extend over multiple lines, thus the new-line
	/// character acts as an unescapable delimiter, no matter what set
	/// of delimiters is passed to the function..
	///
	///\param delimiters the set of delimiters used to separate tokens
	///
	///\param escapeChar the character that can be used to escape
	///                  delimiters when quoting is not active
	///
	///\return a pointer to an internal character buffer containing the
	///        null-terminated token
	char* nextArg(GCharSet& delimiters, char escapeChar = '\\');

	/// Reads past any characters specified in the list of delimeters.
	/// If szDelimeters is NULL, then any characters <= ' ' are considered
	/// to be delimeters. (This method is similar to nextWhile, except that
	/// it does not buffer the characters it reads.)
	void skip(GCharSet& delimeters);

	/// Skip until the next character is one of the delimeters.
	/// (This method is the same as nextUntil, except that it does not buffer what it reads.)
	void skipTo(GCharSet& delimeters);

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
	char* trim(GCharSet& set);

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
	void growBuf();
	char get();
	void bufferChar(char c);
};

} // namespace GClasses

#endif // __GTOKENIZER_H__
