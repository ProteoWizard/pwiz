/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GDIFF_H__
#define __GDIFF_H__

namespace GClasses {

/// This is a helper struct used by GDiff.
struct GDiffLine
{
	const char* pLine;
	size_t nLength;
	size_t nLineNumber1;
	size_t nLineNumber2;
};


/// This class finds the differences between two text files
/// It is case and whitespace sensitive, but is tolerant of Unix/Windows/Mac
/// line endings. It uses lines as the atomic unit. It accepts
/// matching lines in a greedy manner.
class GDiff
{
protected:
	const char* m_pFile1;
	const char* m_pFile2;
	size_t m_nPos1, m_nPos2;
	size_t m_nNextMatch1, m_nNextMatch2, m_nNextMatchLen;
	size_t m_nLine1, m_nLine2;

public:
	GDiff(const char* szFile1, const char* szFile2);
	virtual ~GDiff();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE

	bool nextLine(struct GDiffLine* pLine);

protected:
	static size_t measureLineLength(const char* pLine);
	size_t findNextMatchingLine(size_t* pPos1, size_t* pPos2);
};


} // namespace GClasses

#endif // __GDIFF_H__
