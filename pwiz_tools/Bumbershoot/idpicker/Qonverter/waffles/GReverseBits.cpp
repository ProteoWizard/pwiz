/*
	Copyright (C) 2011, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GError.h"
#include "GReverseBits.h"
#include <bitset>
#ifdef WINDOWS
typedef unsigned char uint8_t;
typedef unsigned short uint16_t;
typedef unsigned int uint32_t;
#else
#	include <stdint.h> //Change to cstdint when C++0x comes out
#endif
namespace GClasses{

  void reverseBitsTest(){
    using std::bitset;
    using std::string;
    uint8_t a = (uint8_t)bitset<8>(string("11100010")).to_ulong();
    uint8_t a_rev = (uint8_t)bitset<8>(string("01000111")).to_ulong();

    if(a != reverseBits(a_rev) && a_rev != reverseBits(a)){
      ThrowError("reverseBits failed to correctly reverse an 8 bit number.");
    }

    uint16_t b
      = (uint16_t)bitset<16>(string("1011010010000001")).to_ulong();
    uint16_t b_rev
      = (uint16_t)bitset<16>(string("1000000100101101")).to_ulong();

    if(b != reverseBits(b_rev) && b_rev != reverseBits(b)){
      ThrowError("reverseBits failed to correctly reverse a 16 bit number.");
    }

    uint32_t c
      =bitset<32>(string("01011011110111000011111001101010")).to_ulong();
    uint32_t c_rev
      =bitset<32>(string("01010110011111000011101111011010")).to_ulong();

    if(c != reverseBits(c_rev) && c_rev != reverseBits(c)){
      ThrowError("reverseBits failed to correctly reverse a 32 bit number.");
    }

  }

} //Namespace GClasses
