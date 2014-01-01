///////////////////////////////////////////////////////////////////////////////
// enum_macros.hpp: macros to generate an enum model
//
// Copyright 2005 Frank Laub
// Distributed under the Boost Software License, Version 1.0. (See
// accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)
//

#ifndef BOOST_ENUM_MACROS_HPP
#define BOOST_ENUM_MACROS_HPP

// MS compatible compilers support #pragma once
#if defined(_MSC_VER) && (_MSC_VER >= 1020)
# pragma once
#endif

#include <boost/preprocessor.hpp>
#include <cstring>

#define BOOST_ENUM_IS_COLUMN_2(i, k) \
	BOOST_PP_EQUAL(BOOST_PP_MOD(i, 2), k)

#define BOOST_ENUM_GET_COLUMN_2(r, data, i, elem) \
	BOOST_PP_IF(BOOST_ENUM_IS_COLUMN_2(i, data), BOOST_PP_IDENTITY((elem)), BOOST_PP_EMPTY)()

#define BOOST_ENUM_VISITOR1(_seq, _macro, _col) \
	BOOST_PP_SEQ_FOR_EACH_I(_macro, _, _seq)

#define BOOST_ENUM_VISITOR2(_seq, _macro, _col) \
	BOOST_PP_SEQ_FOR_EACH_I( \
		_macro, \
		_, \
		BOOST_PP_SEQ_FOR_EACH_I(BOOST_ENUM_GET_COLUMN_2, _col, _seq) \
	)

#define BOOST_ENUM_DOMAIN_ITEM(r, data, i, elem) \
	BOOST_PP_COMMA_IF(i) elem

#define BOOST_ENUM_domain(_seq, _col, _colsize) \
	enum domain \
	{ \
		BOOST_PP_CAT(BOOST_ENUM_VISITOR, _colsize) \
			(_seq, BOOST_ENUM_DOMAIN_ITEM, _col) \
	}; \
	BOOST_STATIC_CONSTANT(index_type, size = BOOST_ENUM_size(_seq, _colsize)); 

#define BOOST_ENUM_size(_seq, _colsize) \
	BOOST_PP_DIV(BOOST_PP_SEQ_SIZE(_seq), _colsize)

#define BOOST_ENUM_PARSE_ITEM(r, data, i, elem) \
	if(strcmp(str, BOOST_PP_STRINGIZE(elem)) == 0) return optional(elem);

#define BOOST_ENUM_get_by_name(_name, _seq, _col, _colsize) \
	static optional get_by_name(const char* str) \
	{ \
		BOOST_PP_CAT(BOOST_ENUM_VISITOR, _colsize) \
			(_seq, BOOST_ENUM_PARSE_ITEM, _col) \
		return optional(); \
	}

#define BOOST_ENUM_CASE_STRING(r, data, i, elem) \
	case elem: return BOOST_PP_STRINGIZE(elem);

#define BOOST_ENUM_names(_seq, _col, _colsize) \
	static const char* names(domain index) \
	{ \
		BOOST_ASSERT(static_cast<index_type>(index) < size); \
		switch(index) \
		{ \
		BOOST_PP_CAT(BOOST_ENUM_VISITOR, _colsize) \
			(_seq, BOOST_ENUM_CASE_STRING, _col) \
		default: return NULL; \
		} \
	}

#define BOOST_ENUM_CASE_PART(elem) \
	case (elem):

#define BOOST_ENUM_VALUE_PART(elem) \
	return optional_value(elem);

#define BOOST_ENUM_VALUE_LINE(r, data, i, elem) \
	BOOST_PP_IF(BOOST_ENUM_IS_COLUMN_2(i, 0), \
		BOOST_ENUM_CASE_PART(elem), \
		BOOST_ENUM_VALUE_PART(elem) \
	)

#define BOOST_ENUM_values_identity() \
	typedef boost::optional<value_type> optional_value; \
	static optional_value values(domain index) \
	{ \
		if(static_cast<index_type>(index) < size) return optional_value(index); \
		return optional_value(); \
	}

#define BOOST_ENUM_values(_seq, _name_col, _value_col, _colsize) \
	typedef boost::optional<value_type> optional_value; \
	static optional_value values(domain index) \
	{ \
		switch(index) \
		{ \
		BOOST_PP_SEQ_FOR_EACH_I(BOOST_ENUM_VALUE_LINE, _, _seq) \
		default: return optional_value(); \
		} \
	}

#define BOOST_BITFIELD_names(_seq, _col, _colsize) \
	static const char* names(domain index) \
	{ \
		BOOST_ASSERT(static_cast<index_type>(index) < size); \
		switch(index) \
		{ \
		case all_mask: return "all_mask"; \
		BOOST_PP_CAT(BOOST_ENUM_VISITOR, _colsize) \
			(_seq, BOOST_ENUM_CASE_STRING, _col) \
		default: return NULL; \
		} \
	}

#define BOOST_BITFIELD_OR_ITEM(r, data, i, elem) \
	| (elem)

#define BOOST_BITFIELD_domain(_seq, _col, _colsize) \
	enum domain \
	{ \
		BOOST_PP_CAT(BOOST_ENUM_VISITOR, _colsize) \
			(_seq, BOOST_ENUM_DOMAIN_ITEM, _col), \
		all_mask, \
		not_mask \
	}; \
	BOOST_STATIC_CONSTANT(index_type, size = BOOST_ENUM_size(_seq, _colsize)); 

#define BOOST_BITFIELD_all_mask(_seq, _col, _colsize) \
	0 BOOST_PP_CAT(BOOST_ENUM_VISITOR, _colsize) \
		(_seq, BOOST_BITFIELD_OR_ITEM, _col) 

#define BOOST_BITFIELD_get_by_name(_name, _seq, _col, _colsize) \
	static optional get_by_name(const char* str) \
	{ \
		if(strcmp(str, "all_mask") == 0) return optional(all_mask); \
		if(strcmp(str, "not_mask") == 0) return optional(not_mask); \
		BOOST_PP_CAT(BOOST_ENUM_VISITOR, _colsize) \
			(_seq, BOOST_ENUM_PARSE_ITEM, _col) \
		return optional(); \
	}

#define BOOST_BITFIELD_values(_seq, _name_col, _value_col, _colsize) \
	typedef boost::optional<value_type> optional_value; \
	static optional_value values(domain index) \
	{ \
		switch(index) \
		{ \
		BOOST_PP_SEQ_FOR_EACH_I(BOOST_ENUM_VALUE_LINE, _, _seq) \
		case all_mask: return optional_value(BOOST_BITFIELD_all_mask(_seq, _value_col, _colsize)); \
		case not_mask: return optional_value(~(BOOST_BITFIELD_all_mask(_seq, _value_col, _colsize))); \
		default: return optional_value(); \
		} \
	}

#define BOOST_ENUM_EX(_name, _interface, _seq) \
	class _interface _name : public boost::detail::enum_base<_name> \
	{ \
	public: \
		BOOST_ENUM_domain(_seq, 0, 1) \
		_name() {} \
		_name(domain index) : boost::detail::enum_base<_name>(index) {} \
		BOOST_ENUM_get_by_name(_name, _seq, 0, 1) \
	private: \
		friend class boost::detail::enum_base<_name>; \
		BOOST_ENUM_names(_seq, 0, 1) \
		BOOST_ENUM_values_identity() \
	};

#define BOOST_ENUM(_name, _seq) BOOST_ENUM_EX(_name, , _seq)

#define BOOST_ENUM_VALUES_EX(_name, _interface, _type, _seq) \
	class _interface _name : public boost::detail::enum_base<_name, _type> \
	{ \
	public: \
		BOOST_ENUM_domain(_seq, 0, 2) \
		_name() {} \
		_name(domain index) : boost::detail::enum_base<_name, _type>(index) {} \
		BOOST_ENUM_get_by_name(_name, _seq, 0, 2) \
	private: \
		friend class boost::detail::enum_base<_name, _type>; \
		BOOST_ENUM_names(_seq, 0, 2) \
		BOOST_ENUM_values(_seq, 0, 1, 2) \
	};

#define BOOST_ENUM_VALUES(_name, _type, _seq) BOOST_ENUM_VALUES_EX(_name, , _type, _seq)

#define BOOST_BITFIELD_EX(_name, _interface, _seq) \
	class _interface _name : public boost::detail::bitfield_base<_name> \
	{ \
	public: \
		BOOST_BITFIELD_domain(_seq, 0, 2) \
		_name() {} \
		_name(domain index) : boost::detail::bitfield_base<_name>(index) {} \
		BOOST_ENUM_get_by_name(_name, _seq, 0, 2) \
	private: \
		friend class boost::detail::bitfield_access; \
		_name(value_type raw, int) : boost::detail::bitfield_base<_name>(raw, 0) {} \
		BOOST_BITFIELD_names(_seq, 0, 2) \
		BOOST_BITFIELD_values(_seq, 0, 1, 2) \
	};

#define BOOST_BITFIELD(_name, _seq) BOOST_BITFIELD_EX(_name, , _seq)

#define BOOST_ENUM_DOMAIN_OPERATORS(_name) \
    inline bool operator== (const _name::domain& lhs, const _name& rhs) {return rhs == lhs;} \
    inline bool operator!= (const _name::domain& lhs, const _name& rhs) {return rhs != lhs;}

#define BOOST_BITFIELD_DOMAIN_OPERATORS(_name) \
    BOOST_ENUM_DOMAIN_OPERATORS(_name) \
    inline _name operator| (const _name::domain& lhs, const _name::domain& rhs) {return _name(lhs) | rhs;} \
    inline _name operator& (const _name::domain& lhs, const _name::domain& rhs) {return _name(lhs) & rhs;}

#endif
