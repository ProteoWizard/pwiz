// $Id$
//
// Copyright 2009 Chris Purcell.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)
//
// Taken from http://lists.boost.org/Archives/boost/2009/09/156509.php
// This file should be deleted if/when this makes it into boost.

#ifndef BOOST_FOREACH_FIELD

#include <boost/preprocessor/seq/for_each_i.hpp>
#include <boost/fusion/include/at_c.hpp>
#include <boost/fusion/include/std_pair.hpp> 
#include <boost/fusion/container/generation/ignore.hpp> // for boost::fusion::ignore

#define BOOST_FOREACH_ASSIGN_VAR(R, ROW, I, VAR) \
    for (VAR = boost::fusion::at_c<I>(ROW); \
         !BOOST_FOREACH_ID(_foreach_leave_outerloop); \
         BOOST_FOREACH_ID(_foreach_leave_outerloop) = true)

#define BOOST_FOREACH_FIELD(VARS, COL) \
    BOOST_FOREACH_PREAMBLE() \
    if (boost::foreach_detail_::auto_any_t BOOST_FOREACH_ID(_foreach_col) = BOOST_FOREACH_CONTAIN(COL)) {} \
    else if (boost::foreach_detail_::auto_any_t BOOST_FOREACH_ID(_foreach_cur) = BOOST_FOREACH_BEGIN(COL)) {} \
    else if (boost::foreach_detail_::auto_any_t BOOST_FOREACH_ID(_foreach_end) = BOOST_FOREACH_END(COL)) {} \
    else for (bool BOOST_FOREACH_ID(_foreach_continue) = true, BOOST_FOREACH_ID(_foreach_leave_outerloop) = true; \
              BOOST_FOREACH_ID(_foreach_continue) && !BOOST_FOREACH_DONE(COL); \
              BOOST_FOREACH_ID(_foreach_continue) ? BOOST_FOREACH_NEXT(COL) : (void)0) \
    if (boost::foreach_detail_::set_false(BOOST_FOREACH_ID(_foreach_continue))) {} \
    else if (boost::foreach_detail_::set_false(BOOST_FOREACH_ID(_foreach_leave_outerloop))) {} \
    else BOOST_PP_SEQ_FOR_EACH_I(BOOST_FOREACH_ASSIGN_VAR, BOOST_FOREACH_DEREF(COL), VARS) \
         for (;!BOOST_FOREACH_ID(_foreach_continue);BOOST_FOREACH_ID(_foreach_continue) = true) 

#endif // BOOST_FOREACH_FIELD
