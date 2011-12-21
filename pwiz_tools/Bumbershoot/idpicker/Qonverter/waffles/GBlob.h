/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GBLOB_H__
#define __GBLOB_H__

#include <string.h>
#include "GError.h"
#include <string>

namespace GClasses {

/// This class is for deserializing blobs. It takes care of
/// Endianness issues and protects against buffer overruns.
/// This class would be particularly useful for writing a network
/// protocol.
class GBlobIncoming
{
protected:
	size_t m_nBufferSize;
	size_t m_nBufferPos;
	unsigned char* m_pBuffer;
	bool m_bDeleteBuffer;

public:
	GBlobIncoming();
	GBlobIncoming(unsigned char* pBuffer, size_t nSize, bool bDeleteBuffer);
	~GBlobIncoming();

	unsigned char* getBlob() { return m_pBuffer; }
	size_t getPos() { return m_nBufferPos; }
	void setPos(size_t pos) { m_nBufferPos = pos; }
	size_t getBlobSize() { return m_nBufferSize; }
	void setBlob(unsigned char* pBuffer, size_t nSize, bool bDeleteBuffer);

	/// Pops a blob from the buffer (throws if buffer is too small)
	void get(unsigned char* pData, size_t nSize);

	/// Pops a single wide char from the buffer (throws if buffer is too small)
	void get(wchar_t* pwc);

	/// Pops a single char from the buffer (throws if buffer is too small)
	void get(char* pc);

	/// Pops an int from the buffer (throws if buffer is too small)
	void get(int* pn);

	/// Pops an unsigned int from the buffer (throws if buffer is too small)
	void get(unsigned int* pui);

	/// Pops a 64-bit unsigned int from the buffer (throws if buffer is too small)
	void get(unsigned long long* pull);

	/// Pops an unsigned char from the buffer (throws if buffer is too small)
	void get(unsigned char* puc);

	/// Pops a float from the buffer (throws if buffer is too small)
	void get(float* pf);

	/// Pops a double from the buffer (throws if buffer is too small)
	void get(double* pd);

	/// Pops a string from the buffer
	void get(std::string* pOutString);

	/// Retrieves bytes from within the buffer
	void peek(size_t nIndex, unsigned char* pData, size_t nSize);
};

/// This class is for serializing objects. It is the complement to GBlobIncoming.
class GBlobOutgoing
{
protected:
	size_t m_nBufferSize;
	size_t m_nBufferPos;
	unsigned char* m_pBuffer;
	bool m_bOkToResizeBuffer;

public:
	GBlobOutgoing(size_t nBufferSize, bool bOkToResizeBuffer);
	~GBlobOutgoing();

	void setPos(size_t pos) { m_nBufferPos = pos; }
	unsigned char* getBlob() { return m_pBuffer; }
	size_t getBlobSize() { return m_nBufferPos; }

	/// Pushes a blob into the blob
	void add(const unsigned char* pData, size_t nSize);

	/// Pushes a wide char into the blob
	void add(const wchar_t wc);

	/// Pushes a char into the blob
	void add(const char c);

	/// Pushes an int into the blob
	void add(const int n);

	/// Pushes an unsigned int into the blob
	void add(const unsigned int n);

	/// Pushes an unsigned 64-bit int into the blob
	void add(const unsigned long long n);

	/// Pushes an unsigned char into the blob
	void add(const unsigned char uc);

	/// Pushes a float into the blob
	void add(const float f);

	/// Pushes a double into the blob
	void add(const double d);

	/// Pushes a null-terminated string into the blob
	void add(const char* szString);


	/// Puts bytes into the buffer (overwriting existing data)
	void poke(size_t nIndex, const unsigned char* pData, size_t nSize);

	/// Puts an int into the buffer (overwriting existing data)
	void poke(size_t nIndex, const int n);

protected:
	void resizeBuffer(size_t nRequiredSize);
};


/// This is a special queue for handling blobs that come in and go out in varying sizes.
/// It is particulary designed for streaming things that must travel or be parsed in
/// packets that may differ in size from how they are sent or transmitted.
class GBlobQueue
{
protected:
	char* m_pBuffer;
	size_t m_bufferAllocation;
	size_t m_bufferBytes;
	const char* m_pPos;
	size_t m_bytesRemaining;

public:
	GBlobQueue();
	~GBlobQueue();

	/// Adds some bytes to the queue. pBlob is assumed to remain valid until
	/// a call to dequeue returns NULL.
	void enqueue(const char* pBlob, size_t len);

	/// Returns a pointer to the specified number of bytes, and advances the
	/// queue by that amount. If less than the required amount is available,
	/// then it will buffer any remaining bytes and return NULL, and not
	/// advance the queue. (The idea is that you will enqueue some more bytes
	/// and then try again.) In most cases, this method will just return a
	/// pointer within the blob that was most recently enqueued, but it may
	/// return a pointer to its own buffer when breaks occur at unfortunate
	/// positions. (The internal buffer is guaranteed to grow no bigger than
	/// the largest value ever passed for len.)
	const char* dequeue(size_t len);

	/// Returns the number of bytes that are ready to go. If you pass this same
	/// number to dequeue, it is guaranteed to return a non-NULL value, and
	/// subsequent calls to dequeue are guaranteed to return NULL until you
	/// call enqueue to put something back into the queue.
	size_t readyBytes() { return m_bytesRemaining; }

protected:
	void reallocBuffer(size_t newSize);
};

} // namespace GClasses

#endif // __GBLOB_H__
