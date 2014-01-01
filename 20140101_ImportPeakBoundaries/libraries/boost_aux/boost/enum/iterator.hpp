///////////////////////////////////////////////////////////////////////////////
// iterator.hpp: defines the enum_iterator type
//
// Copyright 2005 Frank Laub
// Distributed under the Boost Software License, Version 1.0. (See
// accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)
//

#ifndef BOOST_ENUM_ITERATOR_HPP
#define BOOST_ENUM_ITERATOR_HPP

// MS compatible compilers support #pragma once
#if defined(_MSC_VER) && (_MSC_VER >= 1020)
# pragma once
#endif

#include <boost/iterator/iterator_facade.hpp>

namespace boost {
namespace detail {

	template <typename T>
	BOOST_DEDUCED_TYPENAME T::domain enum_cast(
		BOOST_DEDUCED_TYPENAME T::index_type index)
	{
		return static_cast<BOOST_DEDUCED_TYPENAME T::domain>(index);
	}

	template <typename T>
	class enum_iterator 
		: public boost::iterator_facade
			< enum_iterator<T>
			, const T
			, boost::random_access_traversal_tag>
	{
		typedef 
			boost::iterator_facade
				< enum_iterator<T>
				, const T
				, boost::random_access_traversal_tag>
			facade;

		typedef enum_iterator<T> this_type;

	public:
		enum_iterator(size_t index) 
			: m_value(enum_cast<T>(index)) 
			, m_index(index)
		{}

	private:
		friend class boost::iterator_core_access;

		const T& dereference() const 
		{
			return m_value;
		}

		void increment()
		{
			++m_index;
			m_value = enum_cast<T>(m_index);
		}

		void decrement()
		{
			--m_index;
			m_value = enum_cast<T>(m_index);
		}
		
		bool equal(const this_type& rhs) const
		{
			return m_index == rhs.m_index;
		}

		void advance(BOOST_DEDUCED_TYPENAME facade::difference_type n)
		{
			m_index += n;
			m_value = enum_cast<T>(m_index);
		}

		BOOST_DEDUCED_TYPENAME facade::difference_type distance_to(
			const this_type& rhs) const
		{
			return rhs.m_index - m_index;
		}

	private:
		T m_value;
		size_t m_index;
	};

} // detail
} // boost

#endif
