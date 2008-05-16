/*
 *
 * Copyright (c) 2004
 * John Maddock
 *
 * Use, modification and distribution are subject to the 
 * Boost Software License, Version 1.0. (See accompanying file 
 * LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt)
 *
 */

#include "test.hpp"

#ifdef BOOST_MSVC
#pragma warning(disable:4127)
#endif

void test_sets()
{
   using namespace boost::regex_constants;
   // now test the set operator []
   TEST_REGEX_SEARCH("[abc]", extended, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[abc]", extended, "b", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[abc]", extended, "c", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[abc]", extended, "d", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[^bcd]", extended, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[^bcd]", extended, "b", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[^bcd]", extended, "d", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[^bcd]", extended, "e", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("a[b]c", extended, "abc", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[ab]c", extended, "abc", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[a^b]*c", extended, "aba^c", match_default, make_array(0, 5, -2, -2));
   TEST_REGEX_SEARCH("a[^ab]c", extended, "adc", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[]b]c", extended, "a]c", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[[b]c", extended, "a[c", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[-b]c", extended, "a-c", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[^]b]c", extended, "adc", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[^-b]c", extended, "adc", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[b-]c", extended, "a-c", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[a-z-]c", extended, "a-c", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[a-z-]+c", extended, "aaz-c", match_default, make_array(0, 5, -2, -2));
   TEST_INVALID_REGEX("a[b", extended);
   TEST_INVALID_REGEX("a[", extended);
   TEST_INVALID_REGEX("a[]", extended);

   // now some ranges:
   TEST_REGEX_SEARCH("[b-e]", extended, "a", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[b-e]", extended, "b", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[b-e]", extended, "e", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[b-e]", extended, "f", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[^b-e]", extended, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[^b-e]", extended, "b", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[^b-e]", extended, "e", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[^b-e]", extended, "f", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("a[1-3]c", extended, "a2c", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[-3]c", extended, "a-c", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[-3]c", extended, "a3c", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("a[^-3]c", extended, "a-c", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("a[^-3]c", extended, "a3c", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("a[^-3]c", extended, "axc", match_default, make_array(0, 3, -2, -2));
   TEST_INVALID_REGEX("a[3-1]c", extended & ~::boost::regex_constants::collate);
   TEST_INVALID_REGEX("a[1-3-5]c", extended);
   TEST_INVALID_REGEX("a[1-", extended);
   TEST_INVALID_REGEX("a[\\9]", perl);

   // and some classes
   TEST_REGEX_SEARCH("a[[:alpha:]]c", extended, "abc", match_default, make_array(0, 3, -2, -2));
   TEST_INVALID_REGEX("a[[:unknown:]]c", extended);
   TEST_INVALID_REGEX("a[[", extended);
   TEST_INVALID_REGEX("a[[:", extended);
   TEST_INVALID_REGEX("a[[:a", extended);
   TEST_INVALID_REGEX("a[[:alpha", extended);
   TEST_INVALID_REGEX("a[[:alpha:", extended);
   TEST_INVALID_REGEX("a[[:alpha:]", extended);
   TEST_INVALID_REGEX("a[[:alpha:!", extended);
   TEST_INVALID_REGEX("a[[:alpha,:]", extended);
   TEST_INVALID_REGEX("a[[:]:]]b", extended);
   TEST_INVALID_REGEX("a[[:-:]]b", extended);
   TEST_INVALID_REGEX("a[[:alph:]]", extended);
   TEST_INVALID_REGEX("a[[:alphabet:]]", extended);
   TEST_REGEX_SEARCH("[[:alnum:]]+", extended, "-%@a0X_-", match_default, make_array(3, 6, -2, -2));
   TEST_REGEX_SEARCH("[[:alpha:]]+", extended, " -%@aX_0-", match_default, make_array(4, 6, -2, -2));
   TEST_REGEX_SEARCH("[[:blank:]]+", extended, "a  \tb", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("[[:cntrl:]]+", extended, " a\n\tb", match_default, make_array(2, 4, -2, -2));
   TEST_REGEX_SEARCH("[[:digit:]]+", extended, "a019b", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("[[:graph:]]+", extended, " a%b ", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("[[:lower:]]+", extended, "AabC", match_default, make_array(1, 3, -2, -2));
   TEST_REGEX_SEARCH("[[:print:]]+", extended, "AabC", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("[[:punct:]]+", extended, " %-&\t", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("[[:space:]]+", extended, "a \n\t\rb", match_default, make_array(1, 5, -2, -2));
   TEST_REGEX_SEARCH("[[:upper:]]+", extended, "aBCd", match_default, make_array(1, 3, -2, -2));
   TEST_REGEX_SEARCH("[[:xdigit:]]+", extended, "p0f3Cx", match_default, make_array(1, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\d]+", perl, "a019b", match_default, make_array(1, 4, -2, -2));

   //
   // escapes are supported in character classes if we have either
   // perl or awk regular expressions:
   //
   TEST_REGEX_SEARCH("[\\n]", perl, "\n", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[\\b]", perl, "\b", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[\\n]", basic, "\n", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[\\n]", basic, "\\", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:class:]", basic|no_char_classes, ":", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:class:]", basic|no_char_classes, "[", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:class:]", basic|no_char_classes, "c", match_default, make_array(0, 1, -2, -2));
   //
   // test single character escapes:
   //
   TEST_REGEX_SEARCH("\\w", perl, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\w", perl, "Z", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\w", perl, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\w", perl, "z", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\w", perl, "_", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\w", perl, "}", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\w", perl, "`", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\w", perl, "[", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\w", perl, "@", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "a", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "z", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "A", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "Z", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "_", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "}", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "`", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "[", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\W", perl, "@", match_default, make_array(0, 1, -2, -2));

   TEST_REGEX_SEARCH("[[:lower:]]", perl|icase, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:upper:]]", perl|icase, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:alpha:]]", perl|icase, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:alnum:]]", perl|icase, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:lower:]]", perl|icase, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:upper:]]", perl|icase, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:alpha:]]", perl|icase, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:alnum:]]", perl|icase, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:lower:][:upper:]]", perl, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:lower:][:upper:]]", perl, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:lower:][:alpha:]]", perl, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[:lower:][:alpha:]]", perl, "a", match_default, make_array(0, 1, -2, -2));
}

void test_sets2b();
void test_sets2c();

void test_sets2()
{
   using namespace boost::regex_constants;
   // collating elements
   TEST_REGEX_SEARCH("[[.zero.]]", perl, "0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.one.]]", perl, "1", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.two.]]", perl, "2", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.three.]]", perl, "3", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.a.]]", perl, "bac", match_default, make_array(1, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.\xf0.]]", perl, "b\xf0x", match_default, make_array(1, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.right-curly-bracket.]]", perl, "}", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.NUL.]]", perl, "\0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.NUL.][.ae.]]", perl, "\0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.NUL.]-a]", extended, "\0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.NUL.]-a]", perl, "\0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.NUL.]-a]", extended, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.NUL.]-a]", perl, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.NUL.]-[.NUL.]a]", extended, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.NUL.]-[.NUL.]a]", perl, "a", match_default, make_array(0, 1, -2, -2));
   TEST_INVALID_REGEX("[[..]]", perl);
   TEST_INVALID_REGEX("[[.not-a-collating-element.]]", perl);
   TEST_INVALID_REGEX("[[.", perl);
   TEST_INVALID_REGEX("[[.N", perl);
   TEST_INVALID_REGEX("[[.NUL", perl);
   TEST_INVALID_REGEX("[[.NUL.", perl);
   TEST_INVALID_REGEX("[[.NUL.]", perl);
   TEST_INVALID_REGEX("[[:<:]z]", perl);
   TEST_INVALID_REGEX("[a[:>:]]", perl);
   TEST_REGEX_SEARCH("[[.A.]]", extended|icase, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.A.]]", extended|icase, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.A.]-b]+", extended|icase, "AaBb", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("[A-[.b.]]+", extended|icase, "AaBb", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("[[.a.]-B]+", extended|icase, "AaBb", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("[a-[.B.]]+", extended|icase, "AaBb", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("[\x61]", extended, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[\x61-c]+", extended, "abcd", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("[a-\x63]+", extended, "abcd", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("[[.a.]-c]+", extended, "abcd", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("[a-[.c.]]+", extended, "abcd", match_default, make_array(0, 3, -2, -2));
   TEST_INVALID_REGEX("[[:alpha:]-a]", extended);
   TEST_INVALID_REGEX("[a-[:alpha:]]", extended);
   TEST_REGEX_SEARCH("[[.ae.]]", basic, "ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]]", basic, "aE", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[[.AE.]]", basic, "AE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.Ae.]]", basic, "Ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]-b]", basic, "a", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[[.ae.]-b]", basic, "b", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]-b]", basic, "ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[a-[.ae.]]", basic, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[a-[.ae.]]", basic, "b", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[a-[.ae.]]", basic, "ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]]", basic|icase, "AE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]]", basic|icase, "Ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.AE.]]", basic|icase, "Ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.Ae.]]", basic|icase, "aE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.AE.]-B]", basic|icase, "a", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[[.Ae.]-b]", basic|icase, "b", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.Ae.]-b]", basic|icase, "B", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]-b]", basic|icase, "AE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]]", perl, "ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]]", perl, "aE", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[[.AE.]]", perl, "AE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.Ae.]]", perl, "Ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]-b]", perl, "a", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[[.ae.]-b]", perl, "b", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]-b]", perl, "ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[a-[.ae.]]", perl, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[a-[.ae.]]", perl, "b", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[a-[.ae.]]", perl, "ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]]", perl|icase, "AE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]]", perl|icase, "Ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.AE.]]", perl|icase, "Ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.Ae.]]", perl|icase, "aE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.AE.]-B]", perl|icase, "a", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("[[.Ae.]-b]", perl|icase, "b", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.Ae.]-b]", perl|icase, "B", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.]-b]", perl|icase, "AE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.][:lower:]]", perl|icase, "AE", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.][:lower:]]", perl|icase, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[.ae.][=a=]]+", perl, "zzaA", match_default, make_array(2, 4, -2, -2));
   TEST_INVALID_REGEX("[d-[.ae.]]", perl);
   //
   // try some equivalence classes:
   //
   TEST_REGEX_SEARCH("[[=a=]]", basic, "a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[=a=]]", basic, "A", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[=ae=]]", basic, "ae", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("[[=right-curly-bracket=]]", basic, "}", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[=NUL=]]", basic, "\x0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[[=NUL=]]", perl, "\x0", match_default, make_array(0, 1, -2, -2));
   TEST_INVALID_REGEX("[[=", perl);
   TEST_INVALID_REGEX("[[=a", perl);
   TEST_INVALID_REGEX("[[=ae", perl);
   TEST_INVALID_REGEX("[[=ae=", perl);
   TEST_INVALID_REGEX("[[=ae=]", perl);
   //
   // now some perl style single character classes:
   //
   TEST_REGEX_SEARCH("\\l+", perl, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\l]+", perl, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_INVALID_REGEX("[\\l-a]", perl);
   TEST_REGEX_SEARCH("[\\L]+", perl, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[[:^lower:]]+", perl, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\L+", perl, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\u+", perl, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\u]+", perl, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\U]+", perl, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[[:^upper:]]+", perl, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\U+", perl, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\d+", perl, "AB012AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\d]+", perl, "AB012AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\D]+", perl, "01abc01", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[[:^digit:]]+", perl, "01abc01", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\D+", perl, "01abc01", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\s+", perl, "AB   AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\s]+", perl, "AB   AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\S]+", perl, "  abc  ", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[[:^space:]]+", perl, "  abc  ", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\S+", perl, "  abc  ", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\s+", perl, "AB   AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("[\\w]+", perl, "AB_   AB", match_default, make_array(0, 3, -2, 6, 8, -2, -2));
   TEST_REGEX_SEARCH("[\\W]+", perl, "AB_   AB", match_default, make_array(3, 6, -2, -2));
   TEST_REGEX_SEARCH("[[:^word:]]+", perl, "AB_   AB", match_default, make_array(3, 6, -2, -2));
   TEST_REGEX_SEARCH("\\W+", perl, "AB_   AB", match_default, make_array(3, 6, -2, -2));
   test_sets2c();
}

void test_sets2c()
{
   using namespace boost::regex_constants;
   // and some Perl style properties:
   TEST_REGEX_SEARCH("\\pl+", perl, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\Pl+", perl, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\pu+", perl, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\Pu+", perl, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\pd+", perl, "AB012AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\PD+", perl, "01abc01", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\ps+", perl, "AB   AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\PS+", perl, "  abc  ", match_default, make_array(2, 5, -2, -2));

   TEST_REGEX_SEARCH("\\p{alnum}+", perl, "-%@a0X_-", match_default, make_array(3, 6, -2, -2));
   TEST_REGEX_SEARCH("\\p{alpha}+", perl, " -%@aX_0-", match_default, make_array(4, 6, -2, -2));
   TEST_REGEX_SEARCH("\\p{blank}+", perl, "a  \tb", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{cntrl}+", perl, " a\n\tb", match_default, make_array(2, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{digit}+", perl, "a019b", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{graph}+", perl, " a%b ", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{lower}+", perl, "AabC", match_default, make_array(1, 3, -2, -2));
   TEST_REGEX_SEARCH("\\p{print}+", perl, "AabC", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{punct}+", perl, " %-&\t", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{space}+", perl, "a \n\t\rb", match_default, make_array(1, 5, -2, -2));
   TEST_REGEX_SEARCH("\\p{upper}+", perl, "aBCd", match_default, make_array(1, 3, -2, -2));
   TEST_REGEX_SEARCH("\\p{xdigit}+", perl, "p0f3Cx", match_default, make_array(1, 5, -2, -2));
   TEST_REGEX_SEARCH("\\P{alnum}+", perl, "-%@a", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("\\P{alpha}+", perl, " -%@a", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("\\P{blank}+", perl, "a  ", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{cntrl}+", perl, " a\n", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("\\P{digit}+", perl, "a0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{graph}+", perl, " a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{lower}+", perl, "Aa", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{print}+", perl, "Absc", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\P{punct}+", perl, " %", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{space}+", perl, "a ", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{upper}+", perl, "aB", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{xdigit}+", perl, "pf", match_default, make_array(0, 1, -2, -2));

   TEST_INVALID_REGEX("\\p{invalid class}", perl);
   TEST_INVALID_REGEX("\\p{upper", perl);
   TEST_INVALID_REGEX("\\p{", perl);
   TEST_INVALID_REGEX("\\p", perl);
   TEST_INVALID_REGEX("\\P{invalid class}", perl);
   TEST_INVALID_REGEX("\\P{upper", perl);
   TEST_INVALID_REGEX("\\P{", perl);
   TEST_INVALID_REGEX("\\P", perl);

   // try named characters:
   TEST_REGEX_SEARCH("\\N{zero}", perl, "0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{one}", perl, "1", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{two}", perl, "2", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{three}", perl, "3", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{a}", perl, "bac", match_default, make_array(1, 2, -2, -2));
   TEST_REGEX_SEARCH("\\N{\xf0}", perl, "b\xf0x", match_default, make_array(1, 2, -2, -2));
   TEST_REGEX_SEARCH("\\N{right-curly-bracket}", perl, "}", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{NUL}", perl, "\0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("[\\N{zero}-\\N{nine}]+", perl, " 0123456789 ", match_default, make_array(1, 11, -2, -2));

   TEST_INVALID_REGEX("\\N", perl);
   TEST_INVALID_REGEX("\\N{", perl);
   TEST_INVALID_REGEX("\\N{}", perl);
   TEST_INVALID_REGEX("\\N{invalid-name}", perl);
   TEST_INVALID_REGEX("\\N{zero", perl);
   test_sets2b();
}

void test_sets2b()
{
   using namespace boost::regex_constants;

   // and repeat with POSIX-extended syntax:
   TEST_REGEX_SEARCH("\\pl+", extended, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\Pl+", extended, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\pu+", extended, "abABCab", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\Pu+", extended, "ABabcAB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\pd+", extended, "AB012AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\PD+", extended, "01abc01", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\ps+", extended, "AB   AB", match_default, make_array(2, 5, -2, -2));
   TEST_REGEX_SEARCH("\\PS+", extended, "  abc  ", match_default, make_array(2, 5, -2, -2));

   TEST_REGEX_SEARCH("\\p{alnum}+", extended, "-%@a0X_-", match_default, make_array(3, 6, -2, -2));
   TEST_REGEX_SEARCH("\\p{alpha}+", extended, " -%@aX_0-", match_default, make_array(4, 6, -2, -2));
   TEST_REGEX_SEARCH("\\p{blank}+", extended, "a  \tb", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{cntrl}+", extended, " a\n\tb", match_default, make_array(2, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{digit}+", extended, "a019b", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{graph}+", extended, " a%b ", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{lower}+", extended, "AabC", match_default, make_array(1, 3, -2, -2));
   TEST_REGEX_SEARCH("\\p{print}+", extended, "AabC", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{punct}+", extended, " %-&\t", match_default, make_array(1, 4, -2, -2));
   TEST_REGEX_SEARCH("\\p{space}+", extended, "a \n\t\rb", match_default, make_array(1, 5, -2, -2));
   TEST_REGEX_SEARCH("\\p{upper}+", extended, "aBCd", match_default, make_array(1, 3, -2, -2));
   TEST_REGEX_SEARCH("\\p{xdigit}+", extended, "p0f3Cx", match_default, make_array(1, 5, -2, -2));
   TEST_REGEX_SEARCH("\\P{alnum}+", extended, "-%@a", match_default, make_array(0, 3, -2, -2));
   TEST_REGEX_SEARCH("\\P{alpha}+", extended, " -%@a", match_default, make_array(0, 4, -2, -2));
   TEST_REGEX_SEARCH("\\P{blank}+", extended, "a  ", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{cntrl}+", extended, " a\n", match_default, make_array(0, 2, -2, -2));
   TEST_REGEX_SEARCH("\\P{digit}+", extended, "a0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{graph}+", extended, " a", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{lower}+", extended, "Aa", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{print}+", extended, "Absc", match_default, make_array(-2, -2));
   TEST_REGEX_SEARCH("\\P{punct}+", extended, " %", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{space}+", extended, "a ", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{upper}+", extended, "aB", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\P{xdigit}+", extended, "pf", match_default, make_array(0, 1, -2, -2));

   TEST_INVALID_REGEX("\\p{invalid class}", extended);
   TEST_INVALID_REGEX("\\p{upper", extended);
   TEST_INVALID_REGEX("\\p{", extended);
   TEST_INVALID_REGEX("\\p", extended);
   TEST_INVALID_REGEX("\\P{invalid class}", extended);
   TEST_INVALID_REGEX("\\P{upper", extended);
   TEST_INVALID_REGEX("\\P{", extended);
   TEST_INVALID_REGEX("\\P", extended);

   // try named characters:
   TEST_REGEX_SEARCH("\\N{zero}", extended, "0", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{one}", extended, "1", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{two}", extended, "2", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{three}", extended, "3", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{a}", extended, "bac", match_default, make_array(1, 2, -2, -2));
   TEST_REGEX_SEARCH("\\N{\xf0}", extended, "b\xf0x", match_default, make_array(1, 2, -2, -2));
   TEST_REGEX_SEARCH("\\N{right-curly-bracket}", extended, "}", match_default, make_array(0, 1, -2, -2));
   TEST_REGEX_SEARCH("\\N{NUL}", extended, "\0", match_default, make_array(0, 1, -2, -2));

   TEST_INVALID_REGEX("\\N", extended);
   TEST_INVALID_REGEX("\\N{", extended);
   TEST_INVALID_REGEX("\\N{}", extended);
   TEST_INVALID_REGEX("\\N{invalid-name}", extended);
   TEST_INVALID_REGEX("\\N{zero", extended);
}

