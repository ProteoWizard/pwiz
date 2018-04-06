/* $Id$
 *
 * Boost.MultiIndex example of serialization of a MRU list.
 *
 * Copyright 2003-2008 Joaquin M Lopez Munoz.
 * Distributed under the Boost Software License, Version 1.0.
 * (See accompanying file LICENSE_1_0.txt or copy at
 * http://www.boost.org/LICENSE_1_0.txt)
 *
 * See http://www.boost.org/libs/multi_index for library home page.
 */

#ifndef _MRU_LIST_HPP_
#define _MRU_LIST_HPP_


#if !defined(NDEBUG)
#define BOOST_MULTI_INDEX_ENABLE_INVARIANT_CHECKING
#define BOOST_MULTI_INDEX_ENABLE_SAFE_MODE
#endif


#include <boost/config.hpp> /* keep it first to prevent nasty warns in MSVC */
#include <algorithm>
#include <boost/multi_index_container.hpp>
#include <boost/multi_index/hashed_index.hpp>
#include <boost/multi_index/identity.hpp>
#include <boost/multi_index/member.hpp>
#include <boost/multi_index/mem_fun.hpp>
#include <boost/multi_index/sequenced_index.hpp>
#include <fstream>
#include <iostream>
#include <iterator>
#include <sstream>
#include <string>


namespace pwiz {
namespace util {


/* An MRU (most recently used) list keeps record of the last n
 * inserted items, listing first the newer ones. Care has to be
 * taken when a duplicate item is inserted: instead of letting it
 * appear twice, the MRU list relocates it to the first position.
 */

template <typename Item, typename KeyExtractor = boost::multi_index::identity<Item> >
class mru_list
{
    typedef boost::multi_index::multi_index_container
    <
        Item,
        boost::multi_index::indexed_by
        <
            boost::multi_index::sequenced<>,
            boost::multi_index::hashed_unique<KeyExtractor>
        >
    > item_list;

public:
  typedef Item item_type;
  typedef typename item_list::iterator iterator;
  typedef typename item_list::reverse_iterator reverse_iterator;
  typedef typename item_list::const_iterator const_iterator;
  typedef typename item_list::const_reverse_iterator const_reverse_iterator;
  typedef typename item_list::value_type value_type;

  mru_list(std::size_t max_num_items_) : max_num_items(max_num_items_){}

  bool insert(const item_type& item)
  {
    std::pair<iterator,bool> p=il.push_front(item);

    if(!p.second){                     /* duplicate item */
      il.relocate(il.begin(),p.first); /* put in front */
      return false;                    /* item not inserted */
    }
    else if(il.size()>max_num_items){  /* keep the length <= max_num_items */
      il.pop_back();
    }
    return true;                       /* new item inserted */
  }

  template<typename Modifier>
  bool modify(iterator position, Modifier modifier)
  {
      return il.modify(position, modifier);
  }

  bool empty() const {return il.empty();}
  std::size_t size() const {return il.size();}
  std::size_t max_size() const {return std::min(max_num_items, il.max_size());}
  void clear() {il.clear();}

  const item_type& mru() const {return *il.begin();}
  const item_type& lru() const {return *il.rbegin();}

  iterator begin() {return il.begin();}
  iterator end() {return il.end();}

  reverse_iterator rbegin() {return il.rbegin();}
  reverse_iterator rend() {return il.rend();}

  const_iterator begin() const {return il.begin();}
  const_iterator end() const {return il.end();}

  const_reverse_iterator rbegin() const {return il.rbegin();}
  const_reverse_iterator rend() const {return il.rend();}

private:
  item_list   il;
  std::size_t max_num_items;
};


} // namespace util
} // namespace pwiz


#endif // _MRU_LIST_HPP_
