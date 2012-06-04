#include "GSDL.h"

namespace GClasses {

// todo: find a better home for this
char g_shiftTable[] =
{
	0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
	33, 34, 35, 36, 37, 38, '"', 40, 41, 42, 43,
	'<', '_', '>', '?',
	')', '!', '@', '#', '$', '%', '^', '&', '*', '(',
	58, ':', 60, 61, 62, 63, 64,
	'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
	'{', '|', '}', 94, 95, '~', 
	'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
	123, 124, 125, 126, 127
};

/*static*/ char GSDL::shiftKey(char c)
{
	return g_shiftTable[c & 127];
}

char g_numPadTable[] =
{
	'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
	'.', '/', '*', '-', '+', 13, '='
};

/*static*/ char GSDL::filterKey(int key)
{
	if(key < 256)
		return (char)key;
	else if(key < 273)
		return g_numPadTable[key - 256];
	return '\0';
}

} // namespace GClasses

