#ifndef __GBITS_H__
#define __GBITS_H__

#include <stdlib.h>
#include "GError.h"

namespace GClasses {

#define BITS_PER_BYTE 8
#define BITS_PER_UINT (BITS_PER_BYTE * sizeof(unsigned int))

/// Contains various functions for bit analysis
class GBits
{
public:
	/// returns true iff the specified string is a valid floating point number.
	/// For example, it would return true for this string "-1.2e-14", but would
	/// return false for these: "e2", "2e", "-.", "2..3", "3-2", "2e3.5", "--1", etc.
	static bool isValidFloat(const char* pString, size_t len);

	/// Returns -1 if a < b, 0 if a = b, and 1 if a > b.
	static inline int compareInts(int a, int b)
	{
		if(a < b)
			return -1;
		if(b < a)
			return 1;
		return 0;
	}

	/// Returns -1 if a < b, 0 if a = b, and 1 if a > b.
	static inline int compareDoubles(double a, double b)
	{
		if(a < b)
			return -1;
		if(b < a)
			return 1;
		return 0;
	}

	/// Convert a number to its Gray code encoding
	static inline unsigned int binaryToGrayCode(unsigned int nBinary)
	{
		return nBinary ^ (nBinary >> 1);
	}

	/// Convert a number in Gray code encoding to a value
	static inline unsigned int grayCodeToBinary(unsigned int nGrayCode)
	{
		unsigned int nMask = nGrayCode >> 1;
		while(nMask > 0)
		{
			nGrayCode ^= nMask;
			nMask >>= 1;
		}
		return nGrayCode;
	}

	/// Returns the number of 1's in the binary representation of n
	static inline unsigned int countOnes(unsigned int n)
	{
		unsigned int count = 0;
		for(unsigned int i = 0; i < 32; i++)
		{
			if(n & 1)
				count++;
			n = n >> 1;
		}
		return count;
	}

	/// Returns the number of trailing zeros in the binary representation of n.
	/// For example, if n=712 (binary 1011001000), it will return 3.
	/// If n=0, it will return (size_t)-1 to represent inf.
	static inline size_t countTrailingZeros(size_t n)
	{
		if(n & 1)
			return 0;
		if(n & 2)
			return 1;
		if(n & 4)
			return 2;
		return boundingShift((n ^ (n - 1)) + 1) - 1;
	}

	/// Returns true if a number is a power of two
	static inline bool isPowerOfTwo(unsigned int n)
	{
		return ((n & (n - 1)) == 0);
	}

	/// Returns the fewest number of times that 1 must be shifted left to
	/// make a value greater than or equal to n.
	static inline size_t boundingShift(size_t n)
	{
		size_t min = 0;
		size_t max = sizeof(size_t) * 8 - 1;
		for(unsigned int i = 0; i < sizeof(size_t) / 4 + 3; i++)
		{
			size_t mid = (min + max) / 2;
			if(((size_t)1 << mid) < n)
				min = mid + 1;
			else
				max = mid;
		}
		while(((size_t)1 << min) < n)
			min++;
		return min;
	}

	/// Returns the smallest power of 2 that is greater than or equal to n.
	static inline unsigned int boundingPowerOfTwo(unsigned int n)
	{
		return 1u << boundingShift(n);
	}

	/// Returns the sign (-1, 0, +1) of an integer
	static inline int sign(int n)
	{
		if(n > 0)
			return 1;
		if(n < 0)
			return -1;
		return 0;
	}

	/// Returns the sign of d
	static inline int sign(double d)
	{
		if(d > 0)
			return 1;
		if(d < 0)
			return -1;
		return 0;
	}

	/// Converts two hexadecimal digits to a byte. lsn is least significant
	/// nybble. msn is most significant nybble.
	static inline unsigned char hexToByte(char lsn, char msn)
	{
		char v1, v2;
		if(lsn <= '9')
			v1 = lsn - '0';
		else if(lsn <= 'Z')
			v1 = lsn - 'A' + 10;
		else
			v1 = lsn - 'a' + 10;
		if(msn <= '9')
			v2 = msn - '0';
		else if(msn <= 'Z')
			v2 = msn - 'A' + 10;
		else
			v2 = msn - 'a' + 10;
		GAssert(v1 >= 0 && v1 <= 15 && v2 >= 0 && v2 <= 15);
		return(v1 | (v2 << 4));
	}

	/// Converts a byte to two hex digits. The least significate digit
	/// will come first. the most significant digit comes next.
	static inline void byteToHex(unsigned char byte, char* pHex)
	{
		pHex[0] = (byte & 15) + '0';
		if(pHex[0] > '9')
			pHex[0] += ('a' - '0' - 10);
		pHex[1] = (byte >> 4) + '0';
		if(pHex[1] > '9')
			pHex[1] += ('a' - '0' - 10);
	}

	/// Converts a byte to two hex digits. The least significate digit
	/// will come first. the most significant digit comes next.
	static inline void byteToHexBigEndian(unsigned char byte, char* pHex)
	{
		pHex[1] = (byte & 15) + '0';
		if(pHex[1] > '9')
			pHex[1] += ('a' - '0' - 10);
		pHex[0] = (byte >> 4) + '0';
		if(pHex[0] > '9')
			pHex[0] += ('a' - '0' - 10);
	}

	/// pHex should point to a buffer that is at 2 * nBufferSize + 1
	static void bufferToHex(const unsigned char* pBuffer, size_t nBufferSize, char* pHexTwiceAsLarge)
	{
		size_t n;
		for(n = 0; n < nBufferSize; n++)
			byteToHex(pBuffer[n], &pHexTwiceAsLarge[n << 1]);
		pHexTwiceAsLarge[2 * n] = '\0';
	}

	/// pHex should point to a buffer that is at 2 * nBufferSize + 1
	static void bufferToHexBigEndian(const unsigned char* pBuffer, size_t nBufferSize, char* pHexTwiceAsLarge)
	{
		size_t n;
		for(n = 0; n < nBufferSize; n++)
			byteToHexBigEndian(pBuffer[n], &pHexTwiceAsLarge[n << 1]);
		pHexTwiceAsLarge[2 * n] = '\0';
	}

	/// pBuffer should be half the size of nHexSize
	static void hexToBuffer(const char* pHex, size_t nHexSize, unsigned char* pBuffer)
	{
		GAssert(nHexSize % 2 == 0); // not a multiple of 2
		nHexSize /= 2;
		for(size_t n = 0; n < nHexSize; n++)
			pBuffer[n] = hexToByte(pHex[2 * n], pHex[2 * n + 1]);
	}

	/// pBuffer should be half the size of nHexSize
	static void hexToBufferBigEndian(const char* pHex, size_t nHexSize, unsigned char* pBuffer)
	{
		GAssert(nHexSize % 2 == 0); // not a multiple of 2
		nHexSize /= 2;
		for(size_t n = 0; n < nHexSize; n++)
			pBuffer[n] = hexToByte(pHex[2 * n + 1], pHex[2 * n]);
	}

	/// Convert a 64-bit native integer to little endian
	static inline unsigned long long n64ToLittleEndian(unsigned long long in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return ReverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert a 64-bit native integer to little endian
	static inline long long n64ToLittleEndian(long long in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return ReverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert a 32-bit native integer to little endian
	static inline unsigned int n32ToLittleEndian(unsigned int in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return ReverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert a 32-bit native integer to little endian
	static inline int n32ToLittleEndian(int in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return ReverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert a 16-bit native integer to little endian
	static inline unsigned short n16ToLittleEndian(unsigned short in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return ReverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert a 16-bit native integer to little endian
	static inline short n16ToLittleEndian(short in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return ReverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert a 32-bit native float to little endian
	static inline float r32ToLittleEndian(float in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return ReverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert a 64-bit native float to little endian
	static inline double r64ToLittleEndian(double in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return ReverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert little endian to a 64-bit native integer
	static inline unsigned long long littleEndianToN64(unsigned long long in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return reverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert little endian to a 64-bit native integer
	static inline long long littleEndianToN64(long long in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return reverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert little endian to a 32-bit native integer
	static inline unsigned int littleEndianToN32(unsigned int in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return reverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert little endian to a 32-bit native integer
	static inline int littleEndianToN32(int in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return reverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert little endian to a 16-bit native integer
	static inline unsigned short littleEndianToN16(unsigned short in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return reverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert little endian to a 16-bit native integer
	static inline short littleEndianToN16(short in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return reverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert little endian to a 32-bit native float
	static inline float littleEndianToR32(float in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return reverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Convert little endian to a 64-bit native float
	static inline double littleEndianToR64(double in)
	{
#ifdef BYTE_ORDER_BIG_ENDIAN
		return reverseEndian(in);
#else // BYTE_ORDER_BIG_ENDIAN
		return in;
#endif // !BYTE_ORDER_BIG_ENDIAN
	}

	/// Switch the endian of an unsigned integer
	static inline unsigned short reverseEndian(unsigned short in)
	{
		unsigned short out;
		((unsigned char*)&out)[0] = ((unsigned char*)&in)[1];
		((unsigned char*)&out)[1] = ((unsigned char*)&in)[0];
		return out;
	}

	/// Switch the endian of an unsigned integer
	static inline short reverseEndian(short in)
	{
		short out;
		((unsigned char*)&out)[0] = ((unsigned char*)&in)[1];
		((unsigned char*)&out)[1] = ((unsigned char*)&in)[0];
		return out;
	}

	/// Switch the endian of an unsigned integer
	static inline unsigned int reverseEndian(unsigned int in)
	{
		unsigned int out;
		((unsigned char*)&out)[0] = ((unsigned char*)&in)[3];
		((unsigned char*)&out)[1] = ((unsigned char*)&in)[2];
		((unsigned char*)&out)[2] = ((unsigned char*)&in)[1];
		((unsigned char*)&out)[3] = ((unsigned char*)&in)[0];
		return out;
	}

	/// Switch the endian of an integer
	static inline int reverseEndian(int in)
	{
		int out;
		((unsigned char*)&out)[0] = ((unsigned char*)&in)[3];
		((unsigned char*)&out)[1] = ((unsigned char*)&in)[2];
		((unsigned char*)&out)[2] = ((unsigned char*)&in)[1];
		((unsigned char*)&out)[3] = ((unsigned char*)&in)[0];
		return out;
	}

	/// Switch the endian of a double
	static inline unsigned long long reverseEndian(unsigned long long in)
	{
		unsigned long long out;
		((unsigned char*)&out)[0] = ((unsigned char*)&in)[7];
		((unsigned char*)&out)[1] = ((unsigned char*)&in)[6];
		((unsigned char*)&out)[2] = ((unsigned char*)&in)[5];
		((unsigned char*)&out)[3] = ((unsigned char*)&in)[4];
		((unsigned char*)&out)[4] = ((unsigned char*)&in)[3];
		((unsigned char*)&out)[5] = ((unsigned char*)&in)[2];
		((unsigned char*)&out)[6] = ((unsigned char*)&in)[1];
		((unsigned char*)&out)[7] = ((unsigned char*)&in)[0];
		return out;
	}

	/// Switch the endian of a double
	static inline long long reverseEndian(long long in)
	{
		long long out;
		((unsigned char*)&out)[0] = ((unsigned char*)&in)[7];
		((unsigned char*)&out)[1] = ((unsigned char*)&in)[6];
		((unsigned char*)&out)[2] = ((unsigned char*)&in)[5];
		((unsigned char*)&out)[3] = ((unsigned char*)&in)[4];
		((unsigned char*)&out)[4] = ((unsigned char*)&in)[3];
		((unsigned char*)&out)[5] = ((unsigned char*)&in)[2];
		((unsigned char*)&out)[6] = ((unsigned char*)&in)[1];
		((unsigned char*)&out)[7] = ((unsigned char*)&in)[0];
		return out;
	}

	/// Switch the endian of a float
	static inline float reverseEndian(float in)
	{
		float out;
		((unsigned char*)&out)[0] = ((unsigned char*)&in)[3];
		((unsigned char*)&out)[1] = ((unsigned char*)&in)[2];
		((unsigned char*)&out)[2] = ((unsigned char*)&in)[1];
		((unsigned char*)&out)[3] = ((unsigned char*)&in)[0];
		return out;
	}

	/// Switch the endian of a double
	static inline double reverseEndian(double in)
	{
		double out;
		((unsigned char*)&out)[0] = ((unsigned char*)&in)[7];
		((unsigned char*)&out)[1] = ((unsigned char*)&in)[6];
		((unsigned char*)&out)[2] = ((unsigned char*)&in)[5];
		((unsigned char*)&out)[3] = ((unsigned char*)&in)[4];
		((unsigned char*)&out)[4] = ((unsigned char*)&in)[3];
		((unsigned char*)&out)[5] = ((unsigned char*)&in)[2];
		((unsigned char*)&out)[6] = ((unsigned char*)&in)[1];
		((unsigned char*)&out)[7] = ((unsigned char*)&in)[0];
		return out;
	}

#ifndef NO_TEST_CODE
	static void test();
#endif
};

} // namespace GClasses

#endif // __GBITS_H__
