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

 /*
  *   LOCATION:    see http://www.boost.org for most recent version.
  *   FILE         main.cpp
  *   VERSION      see <boost/version.hpp>
  *   DESCRIPTION: entry point for test program.
  */

#include "test.hpp"
#include "test_locale.hpp"
#include <stdarg.h>

#ifdef TEST_THREADS
#include <list>
#include <boost/thread.hpp>
#include <boost/thread/tss.hpp>
#include <boost/shared_ptr.hpp>
#include <boost/array.hpp>

int* get_array_data();

#endif

int error_count = 0;

void run_tests()
{
   basic_tests();
   test_simple_repeats();
   test_alt();
   test_sets();
   test_sets2();
   test_anchors();
   test_backrefs();
   test_character_escapes();
   test_assertion_escapes();
   test_tricky_cases();
   test_grep();
   test_replace();
   test_non_greedy_repeats();
   test_non_marking_paren();
   test_partial_match();
   test_forward_lookahead_asserts();
   test_fast_repeats();
   test_fast_repeats2();
   test_independent_subs();
   test_nosubs();
   test_conditionals();
   test_options();
   test_options2();
#ifndef TEST_THREADS
   test_en_locale();
#endif
   test_emacs();
   test_operators();
   test_overloads();
   test_unicode();
}

int cpp_main(int /*argc*/, char * /*argv*/[])
{
#ifdef TEST_THREADS
   get_array_data();  // initialises data.

   std::list<boost::shared_ptr<boost::thread> > threads;
   for(int i = 0; i < 5; ++i)
   {
      threads.push_back(boost::shared_ptr<boost::thread>(new boost::thread(&run_tests)));
   }
   std::list<boost::shared_ptr<boost::thread> >::const_iterator a(threads.begin()), b(threads.end());
   while(a != b)
   {
      (*a)->join();
      ++a;
   }
#else
   run_tests();
#endif
   return error_count;
}

#ifdef TEST_THREADS

int* get_array_data()
{
   static boost::thread_specific_ptr<boost::array<int, 200> > tp;

   if(tp.get() == 0)
      tp.reset(new boost::array<int, 200>);

   return tp.get()->data();
}

#endif

const int* make_array(int first, ...)
{
   //
   // this function takes a variable number of arguments
   // and packs them into an array that we can pass through
   // our testing macros (ideally we would use an array literal
   // but these can't apparently be used as macro arguments).
   //
#ifdef TEST_THREADS
   int* data = get_array_data();
#else
   static int data[200];
#endif
   va_list ap;
   va_start(ap, first);
   //
   // keep packing args, until we get two successive -2 values:
   //
   int terminator_count;
   int next_position = 1;
   data[0] = first;
   if(first == -2)
      terminator_count = 1;
   else
      terminator_count = 0;
   while(terminator_count < 2)
   {
      data[next_position] = va_arg(ap, int);
      if(data[next_position] == -2)
         ++terminator_count;
      else
         terminator_count = 0;
      ++next_position;
   }
   va_end(ap);
   return data;
}

#ifdef BOOST_NO_EXCEPTIONS

namespace boost{

void throw_exception(std::exception const & e)
{
   std::abort();
}

}

#endif

void test(const char& c, const test_regex_replace_tag& tag)
{
   do_test(c, tag);
}
void test(const char& c, const test_regex_search_tag& tag)
{
   do_test(c, tag);
}
void test(const char& c, const test_invalid_regex_tag& tag)
{
   do_test(c, tag);
}

#ifndef BOOST_NO_WREGEX
void test(const wchar_t& c, const test_regex_replace_tag& tag)
{
   do_test(c, tag);
}
void test(const wchar_t& c, const test_regex_search_tag& tag)
{
   do_test(c, tag);
}
void test(const wchar_t& c, const test_invalid_regex_tag& tag)
{
   do_test(c, tag);
}
#endif

#include <boost/test/included/prg_exec_monitor.hpp>
