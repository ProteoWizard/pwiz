//  lock-free freelist
//
//  Copyright (C) 2008, 2009 Tim Blechmann
//
//  Distributed under the Boost Software License, Version 1.0. (See
//  accompanying file LICENSE_1_0.txt or copy at
//  http://www.boost.org/LICENSE_1_0.txt)

//  Disclaimer: Not a Boost library.

#ifndef BOOST_LOCKFREE_FREELIST_HPP_INCLUDED
#define BOOST_LOCKFREE_FREELIST_HPP_INCLUDED

#include <boost/lockfree/detail/tagged_ptr.hpp>

#include <boost/atomic.hpp>
#include <boost/noncopyable.hpp>

#include <boost/mpl/map.hpp>
#include <boost/mpl/apply.hpp>
#include <boost/mpl/at.hpp>
#include <boost/type_traits/is_pod.hpp>

#include <algorithm>            /* for std::min */

namespace boost
{
namespace lockfree
{
namespace detail
{

struct freelist_node
{
    lockfree::tagged_ptr<freelist_node> next;
};

template <typename T,
          bool allocate_may_allocate,
          typename Alloc = std::allocator<T>
         >
class freelist_stack:
    Alloc
{
    typedef lockfree::tagged_ptr<freelist_node> tagged_ptr;

public:
    freelist_stack (std::size_t n = 0):
        pool_(make_tagged_ptr<freelist_node>(NULL))
    {
        reserve(n);
    }

    void reserve (std::size_t count)
    {
        for (std::size_t i = 0; i != count; ++i)
        {
            T * node = Alloc::allocate(1);
            deallocate(node);
        }
    }

    T * allocate (void)
    {
        tagged_ptr old_pool = pool_.load(memory_order_consume);
        for(;;)
        {
            if (!old_pool.get_ptr())
            {
                if (allocate_may_allocate)
                    return Alloc::allocate(1);
                else
                    return 0;
            }

            freelist_node * new_pool_ptr = old_pool->next.get_ptr();
            tagged_ptr new_pool = make_tagged_ptr<freelist_node>(new_pool_ptr, old_pool.get_tag() + 1);

            if (pool_.compare_exchange_strong(old_pool, new_pool)) {
                void * ptr = old_pool.get_ptr();
                return reinterpret_cast<T*>(ptr);
            }
        }
    }

    void deallocate (T * n)
    {
        void * node = n;
        tagged_ptr old_pool = pool_.load(memory_order_consume);
        freelist_node * new_pool_ptr = reinterpret_cast<freelist_node*>(node);

        for(;;)
        {
            tagged_ptr new_pool = make_tagged_ptr<freelist_node>(new_pool_ptr, old_pool.get_tag());
            new_pool->next.set_ptr(old_pool.get_ptr());

            if (pool_.compare_exchange_strong(old_pool, new_pool))
                return;
        }
    }

    ~freelist_stack(void)
    {
        tagged_ptr current (pool_);

        while (current)
        {
            freelist_node * current_ptr = current.get_ptr();
            if (current_ptr)
                current = current_ptr->next;
            Alloc::deallocate((T*)current_ptr, 1);
        }
    }

private:
    atomic<tagged_ptr> pool_;
};


} /* namespace detail */


struct caching_freelist_t {};
struct static_freelist_t {};



} /* namespace lockfree */
} /* namespace boost */

#endif /* BOOST_LOCKFREE_FREELIST_HPP_INCLUDED */
