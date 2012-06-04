/*
	Copyright (C) 2011, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include <limits>
#include <cassert>
namespace GClasses{

  ///Template used for reversing numBits of type T.  You shouldn't
  ///need this, use the function reverseBits
  ///
  ///Taken from the post at 
  ///http://www.velocityreviews.com/forums/t457514-reverse-bit-order.html
  ///by
  ///Pete Becker, Author of "The Standard C++ Library Extensions: a Tutorial and
  ///Reference"
  template <class T, unsigned numBits> struct GBitReverser_imp {
    static inline T reverse(T val, T mask){
      mask >>= (numBits/2);
      return GBitReverser_imp<T, numBits/2>::reverse((val >> (numBits/2)) & mask, mask)
	| (GBitReverser_imp<T, numBits/2>::reverse(val & mask, mask) << (numBits/2));
    }
  };
  
  ///Base case of template used for reversing numBits of type T.  You
  ///shouldn't need this, use the function reverseBits
  ///
  ///Taken from the post at 
  ///http://www.velocityreviews.com/forums/t457514-reverse-bit-order.html
  ///by
  ///Pete Becker, Author of "The Standard C++ Library Extensions: a Tutorial and
  ///Reference"
  template <class T> struct GBitReverser_imp<T,1>{
    static inline T reverse(T val, T)
    {
      return val;
    }
  };

  
  ///Reverses the bits of value given that T is an unsigned integral
  ///type with binary representation and a number of bits that are a
  ///power of 2.
  ///
  ///Modified (added checks and some consts for readability) from the post at 
  ///http://www.velocityreviews.com/forums/t457514-reverse-bit-order.html
  ///by
  ///Pete Becker, Author of "The Standard C++ Library Extensions: a Tutorial and
  ///Reference"
  template<class T> 
  T reverseBits(T value){
    assert(!std::numeric_limits<T>::is_signed); //Is unsigned
    assert(std::numeric_limits<T>::is_integer); //Is integral type?
    assert(std::numeric_limits<T>::radix == 2); //Is binary
    const int bits = std::numeric_limits<T>::digits;
    const T max = std::numeric_limits<T>::max();
    return GBitReverser_imp<T,bits>::reverse(value, max);
  }

  void reverseBitsTest();

} //Namespace GClasses
