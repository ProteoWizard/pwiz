//  tagged pointer, for aba prevention
//
//  Copyright (C) 2008 Tim Blechmann
//
//  Distributed under the Boost Software License, Version 1.0. (See
//  accompanying file LICENSE_1_0.txt or copy at
//  http://www.boost.org/LICENSE_1_0.txt)

//  Disclaimer: Not a Boost library.

#ifndef BOOST_LOCKFREE_TAGGED_PTR_HPP_INCLUDED
#define BOOST_LOCKFREE_TAGGED_PTR_HPP_INCLUDED

#include <boost/lockfree/detail/prefix.hpp>

#ifndef BOOST_LOCKFREE_PTR_COMPRESSION
#include <boost/lockfree/detail/tagged_ptr_dcas.hpp>
#else
#include <boost/lockfree/detail/tagged_ptr_ptrcompression.hpp>
#endif


namespace boost
{
namespace lockfree
{

template <class T>
tagged_ptr<T> make_tagged_ptr(T * p, typename tagged_ptr<T>::tag_t t = 0)
{
    tagged_ptr<T> result;
    result.set(p, t);
    return result;
}

} /* namespace lockfree */
} /* namespace boost */

#endif /* BOOST_LOCKFREE_TAGGED_PTR_HPP_INCLUDED */
