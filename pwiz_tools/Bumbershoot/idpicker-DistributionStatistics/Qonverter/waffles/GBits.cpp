#include "GBits.h"
#include "GRand.h"

using namespace GClasses;

//static
bool GBits::isValidFloat(const char* pString, size_t len)
{
	if(len == 0)
		return false;
	if(*pString == '-' || *pString == '+')
	{
		pString++;
		len--;
		if(len == 0)
			return false;
	}
	int digits = 0;
	int decimals = 0;
	while(len > 0)
	{
		if(*pString == '.')
			decimals++;
		else if(*pString >= '0' && *pString <= '9')
			digits++;
		else
			break;
		pString++;
		len--;
	}
	if(decimals > 1)
		return false;
	if(digits < 1)
		return false;
	if(len > 0 && (*pString == 'e' || *pString == 'E'))
	{
		pString++;
		len--;
		if(len == 0)
			return false;
		if(*pString == '-' || *pString == '+')
		{
			pString++;
			len--;
		}
		if(len == 0)
			return false;
		while(*pString >= '0' && *pString <= '9')
		{
			pString++;
			len--;
		}
	}
	if(len > 0)
		return false;
	return true;
}

#ifndef NO_TEST_CODE
size_t count_trailing_zeros(size_t n)
{
	size_t count = 0;
	for(size_t i = 0; i < 32; i++)
	{
		if(n & 1)
			return count;
		count++;
		n = n >> 1;
	}
	return (size_t)-1;
}

void test_boundingShift()
{
	GRand rand(0);
	for(size_t i = 0; i < 1000; i++)
	{
		size_t bits = (size_t)rand.next(31);
		int n = 1 << bits;
		if(GBits::boundingShift(n) != bits)
			ThrowError("failed");
		n++;
		if(GBits::boundingShift(n) != bits + 1)
			ThrowError("failed");
	}
}

void test_countTrailingZeros()
{
	for(size_t i = 0; i < 10000; i++)
	{
		if(count_trailing_zeros(i) != GBits::countTrailingZeros(i))
			ThrowError("failed");
	}
}

void GBits::test()
{
	test_boundingShift();
	test_countTrailingZeros();
}
#endif // NO_TEST_CODE
