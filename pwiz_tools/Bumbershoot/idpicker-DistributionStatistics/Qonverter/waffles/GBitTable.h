/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GBITTABLE_H__
#define __GBITTABLE_H__

#include "GError.h"

namespace GClasses {

/// Represents a table of bits.
class GBitTable
{
protected:
	size_t m_size;
	size_t* m_pBits;

public:
	/// All bits are initialized to false
	GBitTable(size_t bitCount);
	
	///Copy Constructor
	GBitTable(const GBitTable& o);

	///Operator=
	GBitTable& operator=(const GBitTable& o);

	virtual ~GBitTable();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Sets all bits to false
	void clearAll();

	/// Sets all bits to true
	void setAll();

	/// Returns the bit at index
	bool bit(size_t index);

	/// Sets the bit at index
	void set(size_t index);

	/// Clears the bit at index
	void unset(size_t index);

	/// Toggles the bit at index
	void toggle(size_t index);

	/// Returns true iff the bit tables are exactly equal.
	/// (Behavior is undefined if the tables are not the same size.)
	bool equals(GBitTable* that);

	/// Returns true iff the first "count" bits are set. (Note that
	/// for most applications, it is more efficient to simply maintain
	/// a count of the number of bits that are set than to call this method.)
	bool areAllSet(size_t count);

	/// Returns true iff the first "count" bits are clear
	bool areAllClear(size_t count);
};

} // namespace GClasses

#endif // __GBITTABLE_H__
