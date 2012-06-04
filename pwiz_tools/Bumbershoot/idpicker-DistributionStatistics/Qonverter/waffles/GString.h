/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GSTRING_H__
#define __GSTRING_H__

#include <stdlib.h>

namespace GClasses {

// This is similar to strncpy, but it always makes sure that
// there is a null-terminating '\0' at the end of the new string.
// Returns the length of the new string.
size_t safe_strcpy(char* szDest, const char* szSrc, size_t nDestBufferSize);


/// This class chops a big string at word breaks so you can display it intelligently
/// on multiple lines
class GStringChopper
{
protected:
	const char* m_szString;
	size_t m_nLen;
	size_t m_nMaxLen;
	size_t m_nMinLen;
	char* m_pBuf;
	bool m_bDropLeadingWhitespace;

public:
	GStringChopper(const char* szString, size_t nMinLength, size_t nMaxLength, bool bDropLeadingWhitespace);
	~GStringChopper();

	/// Starts over with szString
	void reset(const char* szString);

	/// Returns NULL when there are no more lines left
	const char* next();
};

} // namespace GClasses

#endif // __GSTRING_H__
