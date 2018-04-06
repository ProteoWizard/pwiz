//
// $Id$
//
//
// Original author: Andrei Alexandrescu
//
// Copyright 1999 Micro Modeling Associates, Inc.
//
// This code was downloaded from:
// http://erdani.org/publications/adapting_automation_arrays.html
//
// No license was specified in the article or in the download... permission pending?
//


#ifndef __AUTOMATION_VECTOR_H__
#define __AUTOMATION_VECTOR_H__

#ifndef _MSC_VER
// Because automation_vector provides std::vector semantics, non-MSVC compilers
// can parse it equivalently.
#include <vector>
#define automation_vector std::vector
#else // _MSC_VER

/******************************************************************************
Template class automation_vector<T>
Purpose: to wrap the VB safe one-dimensional arrays while having the following
properties:
1. The same memory layout as VB's safe arrays, in order to freely pass data 
back and forth without adapting/copying
2. Provide fully std::vector semantics
3. Hide all the details of locking/unlocking an such to the user.
******************************************************************************/

// identifier was truncated to 'number' characters in the debug information
#pragma warning(disable : 4786)	

#define NOMINMAX
#include <oaidl.h>
#include <algorithm>
#include <stdexcept>

#include "Export.hpp"

template <bool> struct static_checker;
template <> struct static_checker<true> {};

class PWIZ_API_DECL automation_vector_base;

template <VARENUM varenum>
struct static_variant_info
{
    enum { vt = varenum };
    enum { size = 
        vt == VT_I1 ? 1
        : vt == VT_I2 ? 2
        : vt == VT_I4 ? 4
        : vt == VT_R4 ? 4
        : vt == VT_R8 ? 8
        : vt == VT_CY ? 8
#ifdef _WIN64
        : vt == VT_BSTR ? 8
        : vt == VT_DISPATCH ? 8
        : vt == VT_UNKNOWN ? 8
#else
        : vt == VT_BSTR ? 4
        : vt == VT_DISPATCH ? 4
        : vt == VT_UNKNOWN ? 4
#endif
        : vt == VT_VARIANT ? 16
        : 0
    };
    static char size_checker[
        vt == VT_I1 ? 1
            : vt == VT_I2 ? 2
            : vt == VT_I4 ? 4
            : vt == VT_R4 ? 4
            : vt == VT_R8 ? 8
            : vt == VT_CY ? 8
#ifdef _WIN64
            : vt == VT_BSTR ? 8
            : vt == VT_DISPATCH ? 8
            : vt == VT_UNKNOWN ? 8
#else
            : vt == VT_BSTR ? 4
            : vt == VT_DISPATCH ? 4
            : vt == VT_UNKNOWN ? 4
#endif
            : vt == VT_VARIANT ? 16
            : 0];
};

namespace Configure
{
    static_variant_info<VT_I1> deduceVARENUM(char);
    static_variant_info<VT_I1> deduceVARENUM(signed char);
    static_variant_info<VT_I1> deduceVARENUM(unsigned char);
    static_variant_info<VT_I2> deduceVARENUM(short);
    static_variant_info<VT_I2> deduceVARENUM(unsigned short);
    static_variant_info<VT_I4> deduceVARENUM(int);
    static_variant_info<VT_I4> deduceVARENUM(unsigned int);
    static_variant_info<VT_I4> deduceVARENUM(long);
    static_variant_info<VT_I4> deduceVARENUM(unsigned long);
    static_variant_info<VT_R4> deduceVARENUM(float);
    static_variant_info<VT_R8> deduceVARENUM(double);
    static_variant_info<VT_CY> deduceVARENUM(CURRENCY);
    static_variant_info<VT_BSTR> deduceVARENUM(BSTR);
    static_variant_info<VT_DISPATCH> deduceVARENUM(IDispatch *);
    static_variant_info<VT_UNKNOWN> deduceVARENUM(IUnknown *);
    static_variant_info<VT_VARIANT> deduceVARENUM(VARIANT);
    static_variant_info<VT_VARIANT> deduceVARENUM(automation_vector_base);
}

template <class> class automation_vector;

class PWIZ_API_DECL automation_vector_base
{
    friend void trace(VARIANT &v);
    typedef automation_vector_base self;
protected:
    // Swap contents efficiently with another vector.
    void swap(self &that) /*throw()*/
    {
        _ASSERT(valid());
        _ASSERT(that.valid());
        std::swap(static_cast<VARIANT &>(m_Value),
                  static_cast<VARIANT &>(that.m_Value));
    }
    // Attach to a VARIANT.
    // No checking made!
    void attach(VARIANT &v) /*throw()*/;
    // Pours the contents in a VARIANT.
    // Assume the dest was cleared previously
    void detach(VARIANT &v) /*throw()*/;
public:
    // SAFEARRAY lock class wrapper
    // for exception safety
    class array_lock
    {
        SAFEARRAY *pArray;
    public:
        array_lock(SAFEARRAY &a) /*throw(std::runtime_error)*/ : pArray(&a)
        { self::com_enforce(::SafeArrayLock(pArray)); }
        void leave_ownership()
        { pArray = 0; }
        ~array_lock() /*throw()*/
        { if (pArray && FAILED(::SafeArrayUnlock(pArray))) _ASSERT(false); }
    };
    typedef size_t size_type;
    typedef ptrdiff_t difference_type;
#ifdef _DEBUG
    // Check if the vector is okay
    bool valid() const /*throw()*/;
#endif
    // The index of the first element
    long low_bound() const /*throw()*/
    {
        _ASSERT(valid());
        return empty() ? 0 : bounds().lLbound;
    }
    void low_bound(long NewValue) /*throw(std::runtime_error)*/
    {
        _ASSERT(valid());
        if (empty())
            throw std::runtime_error("Cannot set lower bound on an empty array");
        bounds().lLbound = NewValue;
    }
    // The index of the last element
    long up_bound() const /*throw()*/
    {
        _ASSERT(valid());
        if (empty()) 
            return -1;
        const SAFEARRAYBOUND &Bounds = bounds();
        return Bounds.lLbound + Bounds.cElements - 1;
    }
    VARTYPE plain_vartype() const
    {
        return m_Value.vt;
    }
    static void com_enforce(HRESULT hr) /*throw(std::runtime_error)*/;
    // The size of the vector
    size_t size() const /*throw()*/
    {
        _ASSERT(valid());
        return m_Value.vt == VT_EMPTY ? 0 : bounds().cElements;
    }
    size_t capacity() const /*throw()*/
    {
        return size();
    }
    // checks whether the vector is empty
    bool empty() const /*throw()*/
    {
        return size() == 0;
    }
    ~automation_vector_base() /*throw()*/;
    // Creation mode
    enum TCreateMode { MOVE, COPY };
protected:
    // Constructors/destructors are protected to prevent direct instantiation
    self(unsigned Elements, VARENUM VarType) 
        /*throw(std::invalid_argument, std::runtime_error)*/;
    SAFEARRAY &array() /*throw()*/
    {
        return array(m_Value);
    }
    const SAFEARRAY &array() const /*throw()*/
    {
        return array(m_Value);
    }
    SAFEARRAYBOUND &bounds() /*throw()*/
    {
        return array().rgsabound[0];
    }
    const SAFEARRAYBOUND &bounds() const /*throw()*/
    {
        return array().rgsabound[0];
    }
    void resize(size_type NewSize, VARENUM Type);
    void resize_no_initialize(size_type NewSize); // For use only with primitive types!  (Not sure about BSTR, CURRENCY etc)
    void clear()
    {
        _ASSERT(!V_ISBYREF(&m_Value));
        _ASSERT(empty() || array().cLocks == 0);
         com_enforce(::VariantClear(&m_Value));
    }
    static void get_element(const VARIANT &Array, long Index, VARIANT &v) 
        /*throw(std::runtime_error)*/;
    static void put_element(VARIANT &Array, long Index, const VARIANT &v) 
        /*throw(std::runtime_error)*/;
private:
    static SAFEARRAY &array(const VARIANT &v) /*throw()*/
    {
        _ASSERT(V_ISARRAY(&v));
        _ASSERT(!V_ISBYREF(&v));
        return *v.parray;
    }
    // The actual holder of the array
    VARIANT m_Value;
};

// Dummy implementations for deduceVARENUM functions
namespace Configure
{
    inline static_variant_info<VT_I1> deduceVARENUM(char)
    { return static_variant_info<VT_I1>(); }
    inline static_variant_info<VT_I1> deduceVARENUM(signed char)
    { return static_variant_info<VT_I1>(); }
    inline static_variant_info<VT_I1> deduceVARENUM(unsigned char)
    { return static_variant_info<VT_I1>(); }
    inline static_variant_info<VT_I2> deduceVARENUM(short)
    { return static_variant_info<VT_I2>(); }
    inline static_variant_info<VT_I2> deduceVARENUM(unsigned short)
    { return static_variant_info<VT_I2>(); }
    inline static_variant_info<VT_I4> deduceVARENUM(int)
    { return static_variant_info<VT_I4>(); }
    inline static_variant_info<VT_I4> deduceVARENUM(unsigned int)
    { return static_variant_info<VT_I4>(); }
    inline static_variant_info<VT_I4> deduceVARENUM(long)
    { return static_variant_info<VT_I4>(); }
    inline static_variant_info<VT_I4> deduceVARENUM(unsigned long)
    { return static_variant_info<VT_I4>(); }
    inline static_variant_info<VT_R4> deduceVARENUM(float)
    { return static_variant_info<VT_R4>(); }
    inline static_variant_info<VT_R8> deduceVARENUM(double)
    { return static_variant_info<VT_R8>(); }
    inline static_variant_info<VT_CY> deduceVARENUM(CURRENCY)
    { return static_variant_info<VT_CY>(); }
    inline static_variant_info<VT_BSTR> deduceVARENUM(BSTR)
    { return static_variant_info<VT_BSTR>(); }
    inline static_variant_info<VT_DISPATCH> deduceVARENUM(IDispatch *)
    { return static_variant_info<VT_DISPATCH>(); }
    inline static_variant_info<VT_UNKNOWN> deduceVARENUM(IUnknown *)
    { return static_variant_info<VT_UNKNOWN>(); }
    inline static_variant_info<VT_VARIANT> deduceVARENUM(VARIANT)
    { return static_variant_info<VT_VARIANT>(); }
    inline static_variant_info<VT_VARIANT> 
        deduceVARENUM(automation_vector_base)
    { return static_variant_info<VT_VARIANT>(); }
}

inline void from_automation(SAFEARRAY &, void *)
{
}

inline void to_automation(SAFEARRAY &, void *)
{
}

template <class T>
void from_automation(SAFEARRAY &Array, automation_vector<T> *pDummy);

template <class T>
void to_automation(SAFEARRAY &Array, automation_vector<T> *pDummy);

template <class T> class automation_vector : public automation_vector_base
{
    typedef automation_vector_base base;
    typedef automation_vector self;
    // Makes unnecessary const_cast in some cases
    const self &const_this()
    {
        return *this;
    }
public:
    // *** vector compatibilty typedefs
    typedef T value_type;
    typedef T &reference;
    typedef const T &const_reference;
    // iterators 
    typedef T *iterator;
    typedef const T *const_iterator;
    typedef std::reverse_iterator<iterator> reverse_iterator;
    typedef std::reverse_iterator<const_iterator> const_reverse_iterator;
    // *** Static VARIANT type mapped from the C++ type
    static VARENUM myVARENUM()
    {
        // If you have an error on the line below, you've instantiated
        // automation_vector with the wrong type
        static_checker<sizeof(T) == 
            sizeof(Configure::deduceVARENUM(T()).size_checker)>();
        return static_cast<VARENUM>(Configure::deduceVARENUM(T()).vt);
    }
    // *** Constructors
    // Construction options (mutual exclusive).
    // 1. MOVE: the data is moved from the source and the source is cleared.
    // Use it when you're sure you don't need the source anymore.
    // 2. COPY: make a full copy of the array. Use it cautiously.
    automation_vector(VARIANT &vSource, TCreateMode Mode) 
        /*throw(std::invalid_argument, std::runtime_error)*/
        : base(0, myVARENUM())
    {
        if (Mode == COPY)
        {
            // Make a copy and attach to it
            VARIANT v;
            ::VariantInit(&v);
            ::VariantCopy(&v, &vSource);
            attach(v);
        }
        else
            // Attach directly to the source
            attach(vSource);
    }

    // *** Constructors
    // Construction options (mutual exclusive).
    // 1. MOVE: the data is moved from the source and the source is cleared.
    // Use it when you're sure you don't need the source anymore.
    // 2. COPY: make a full copy of the array. Use it cautiously.
    automation_vector(SAFEARRAY &Array, TCreateMode Mode) 
        /*throw(std::invalid_argument, std::runtime_error)*/
        : base(0, myVARENUM())
    {
        if (Mode == COPY)
        {
            // Make a copy and attach to it
            VARIANT vSource, vCopy;
            ::VariantInit(&vSource);
            if (Array.rgsabound->cElements == 0)
                vSource.vt = VT_EMPTY;
            else
            {
                ::SafeArrayGetVartype(&Array, &vSource.vt);
                vSource.vt |= VT_ARRAY;
                vSource.parray = &Array;
                ::VariantInit(&vCopy);
                ::VariantCopy(&vCopy, &vSource);
                attach(vCopy);
            }
        }
        else
            // Attach directly to the source
            attach(Array);
    }

    // Takes the # of elements, the lower bound, and the VType of the elements.
    // The VType is deducted from the C++ type.
    explicit automation_vector(unsigned uElements = 0, T t = T())
        /*throw(std::invalid_argument, std::runtime_error)*/
        : base(uElements, myVARENUM())
    {
        if (empty())
            return;
        _ASSERT(sizeof(T) == array().cbElements);
        _ASSERT(array().cLocks == 0);
        com_enforce(::SafeArrayLock(&array()));
        std::uninitialized_fill(begin(), end(), t);
    }
    // Copy constructor. Warning! It copies data, so it may be inefficient. 
    // You may want to use attach() instead.
    automation_vector(const automation_vector &vSource) 
        /*throw(std::runtime_error)*/ : base(vSource.size(), myVARENUM())
    {
        if (empty())
            return;
        com_enforce(::SafeArrayLock(&array()));
        std::uninitialized_copy(vSource.begin(), vSource.end(), begin());
    }
    automation_vector(const_iterator first, const_iterator last)
        : base(last - first, myVARENUM())
    {
        std::uninitialized_copy(first, last, begin());
    }
    // *** ~
    ~automation_vector();
    // *** Assignment operator. Warning! It copies data, so it may be 
    // inefficient. You may want to use swap() instead.
    automation_vector &operator=(const automation_vector &that)
    {
        resize(that.size());
        std::copy(that.begin(), that.end(), begin());
        return *this;
    }
    // *** Assignment operator. Warning! It copies data, so it may be 
    // inefficient. You may want to use attach() instead
    automation_vector &operator=(const VARIANT &that)
    {
        VARIANT v;
        ::VariantInit(&v);
        ::VariantCopy(&v, &that);
        attach(v);
        return *this;
    }
    // *** Takes the contents of the source and empty it.
    // Requirement: vSource must be either a safe array or empty
    // All existing iterators to 'this' will be invalidated.
    void attach(VARIANT &vSource) 
        /*throw(std::invalid_argument, std::runtime_error)*/;

    // *** Takes the contents of the source and empty it.
    // Requirement: vSource must be either a safe array or empty
    // All existing iterators to 'this' will be invalidated.
    void attach(SAFEARRAY &vSource) 
        /*throw(std::invalid_argument, std::runtime_error)*/;

    // *** Moves the vector to Var.
    // Requirement: Var must be valid
    // After the move, size() will return zero.
    // All existing iterators to 'this' will be invalidated.
    void detach(VARIANT &Var)
    {
        _ASSERT(_CrtIsValidPointer(&Var, sizeof(VARIANT), true));
        com_enforce(::VariantClear(&Var));
        unlock();
        base::detach(Var);
    }
    VARIANT* detach()
    {
        VARIANT* v = new VARIANT;
        detach(*v);
        return v;
    }
    // *** vector compatibility methods
    void assign(const_iterator first, const_iterator last)
    {
        clear();
        insert(begin(), first, last);
    }
    void assign(size_type n, const T &x)
    {
        clear();
        insert(begin(), n, x);
    }
    // The start of the vector
    const_iterator begin() const /*throw(std::runtime_error)*/
    {
        _ASSERT(valid());
        _ASSERT(empty() || array().cLocks == 1);
        return empty() ? 0 : static_cast<T *>(array().pvData);
    }
    iterator begin() /*throw(std::runtime_error)*/
    {
        return const_cast<iterator>(const_this().begin());
    }
    // One past the last element of the vector
    const_iterator end() const /*throw(std::runtime_error)*/
    {
        _ASSERT(valid());
        if (empty())
            return 0;
        _ASSERT(array().cLocks == 1);
        const SAFEARRAY &a = array();
        return static_cast<T *>(a.pvData) + a.rgsabound[0].cElements;
    }
    iterator end() /*throw(std::runtime_error)*/
    {
        return const_cast<iterator>(const_this().end());
    }
    // The reversed begin of the vector
    reverse_iterator rbegin() /*throw(std::runtime_error)*/
    {
        return reverse_iterator(end());
    }
    const_reverse_iterator rbegin() const /*throw(std::runtime_error)*/
    {
        return const_reverse_iterator(end());
    }
    // The reversed end of the vector
    reverse_iterator rend() /*throw(std::runtime_error)*/
    {
        return reverse_iterator(begin());
    }
    const_reverse_iterator rend() const /*throw(std::runtime_error)*/
    {
        return const_reverse_iterator(begin());
    }
    // Reference to the first element of the vector
    // Requirement: the vector must not be empty
    const_reference front() const /*throw(std::runtime_error)*/
    {
        _ASSERT(!empty());
        return *begin();
    }
    reference front() /*throw(std::runtime_error)*/
    {
        return const_cast<reference>(const_this().front());
    }
    // Reference to the last element of the vector
    // Requirement: the vector must not be empty
    const_reference back() const /*throw(std::runtime_error)*/
    {
        _ASSERT(!empty());
        return end()[-1];
    }
    reference back() /*throw(std::runtime_error)*/
    {
        return const_cast<reference>(const_this().back());
    }
    size_t max_size() const /*throw()*/
    {
        return size_type(-1) / sizeof(T);
    }
    // C-like random access
    // Requirements: the index must fall within the bounds
    const_reference operator[] (long lIndex) const /*throw(std::runtime_error)*/
    {
        _ASSERT(valid());
        _ASSERT(!empty());
        const SAFEARRAYBOUND &Bounds = bounds();
        lIndex -= Bounds.lLbound;
        _ASSERT(lIndex >= 0 && (unsigned long)lIndex < Bounds.cElements);
        return begin()[lIndex];
    }
    reference operator[](long lIndex)
    {
        return const_cast<reference>(const_this()[lIndex]);
    }
    const_reference at(long lIndex) const /*throw(std::runtime_error)*/
    {
        _ASSERT(valid());
        if (empty())
            throw std::out_of_range("out of range");
        const SAFEARRAYBOUND &Bounds = bounds();
        lIndex -= Bounds.lLbound;
        if (lIndex < 0 || (unsigned long)lIndex >= Bounds.cElements)
            throw std::out_of_range("out of range");
        return begin()[lIndex];
    }
    reference at(long lIndex) /*throw(std::runtime_error)*/
    {
        return const_cast<reference>(const_this().at(lIndex));
    }
    void swap(self &that) /*throw()*/
    {
        base::swap(that);
    }
    // Insert an element BEFORE i within the vector.
    // Call insert(end(), x) or push_back(x) to append.
    iterator insert(iterator i, const T& x = T()) /*throw(std::runtime_error)*/
    {
        _ASSERT(i >= begin() && i <= end());
        size_t Offset = i - begin();
        insert(i, (T *)&x, (T *)&x + 1);
        return begin() + Offset;
    }
    // Insert a repetition of x BEFORE i within the vector.
    void insert(iterator i, size_t n, const T &x) /*throw(std::runtime_error)*/
    {
        size_t OldSize = size();
        resize(size() + n);
        std::copy_backward(begin(), begin() + OldSize, end());
        std::fill(begin(), begin() + n, x);
    }
    // Insert a sequence of elements BEFORE i within the vector.
    void insert(iterator i, const_iterator first, const_iterator last) 
        /*throw(std::runtime_error)*/
    {
        _ASSERT(valid());
        _ASSERT(last >= first);
        _ASSERT(i >= begin() && i <= end());
        size_t count = last - first;
        if (count == 0)
            return;
        size_t offset = i - begin(), old_size = size();
        resize(old_size + count);
        for (iterator j = begin() + old_size, k = end(); j != begin() + offset; )
            std::iter_swap(--j, --k);
        std::copy(first, last, begin() + offset);
        _ASSERT(valid());
    }
    iterator erase(iterator i)
    {
        unsigned Offset = i - begin();
        std::copy(i + 1, end(), i);
        pop_back();
        return begin() + Offset; 
    }
    iterator erase(iterator From, iterator To)
    {
        unsigned Offset = From - begin();
        iterator i = std::copy(To, end(), From);
        resize(i - begin());
        return begin() + Offset; 
    }
    // Change the size of the vector. NewDim may be 0 or whatever.
    void resize(size_t NewDim, const T &FillWith);
    void resize(size_t NewDim)
    {
        resize(NewDim, T());
    }
    void resize_no_initialize(size_t NewDim); // Don't use with anything but primitives!!!  (Not sure about BSTR, CURRENCY etc)
    // Append an element to the vector.
    void push_back(const T &ToAdd) /*throw(std::runtime_error)*/
    {
        _ASSERT(valid());
        insert(end(), ToAdd);
    }
    // Nuke the last element of the vector.
    void pop_back() /*throw(std::runtime_error)*/
    {
        _ASSERT(valid());
        _ASSERT(!empty());
        resize(size() - 1);
    }
    // Clear the entire vector
    void clear();
#ifdef _DEBUG
    // Check if the vector is okay
    bool valid() const /*throw()*/
    {
        if (!base::valid())
            return false;
        if (empty())
            return true;
        std::string ErrorMessage;
        if (array().cbElements != sizeof(T))
            ErrorMessage += "Element size (cbElements) is incorrect.\n";
        VARENUM ElementType = VARENUM(plain_vartype() & VT_TYPEMASK);
        if (ElementType != myVARENUM() &&
            !(ElementType == VT_DATE && myVARENUM() == VT_R4) &&
            !(ElementType == VT_ERROR && myVARENUM() == VT_I4))
            ErrorMessage += "Element type is incorrect.\n";
        if (ErrorMessage.empty())
            return true;
        throw std::runtime_error("The automation_vector is invalid due to the following problem(s):\n" + ErrorMessage);
    }
#endif
protected:
    void lock();
    void unlock();
};

template <class T>
automation_vector<T>::~automation_vector()
{
    _ASSERT(valid());
    try
    {
        unlock();
        _ASSERT(plain_vartype() == VT_EMPTY || array().cLocks == 0);
    }
    catch (...)
    {
        // Don't throw anything - just assert
        _ASSERT(false);
    }
}

template <class T>
void automation_vector<T>::clear()
{
    _ASSERT(valid());
    unlock();
    base::clear();
}

template <class T>
void automation_vector<T>::resize(size_t NewSize, const T &t) 
{
    _ASSERT(valid());
    size_type OldSize = size();
    unlock();
    base::resize(NewSize, myVARENUM());
    lock();
    _ASSERT(valid());
    if (OldSize < size())
        std::uninitialized_fill(begin() + OldSize, end(), t);
}

// Special version for primitives like double and float, where initialization isn't strictly needed and may not be useful.
// (Not sure about use with BSTR, CURRENCY etc - best not to use there)
template <class T>
void automation_vector<T>::resize_no_initialize(size_t NewSize)
{
    _ASSERT(valid());
    unlock();
    base::resize(NewSize, myVARENUM());
    lock();
    _ASSERT(valid());
}

template <class T>
void automation_vector<T>::lock()
{
    _ASSERT(valid());
    if (empty())
        // nothing to do -- all ok
        return;
    _ASSERT(array().cLocks == 0);
    // Lock self
    array_lock MyLock(array());
    _ASSERT(valid());
    // Convert contents
    from_automation(array(), static_cast<T *>(0));
    // Commit the lock
    MyLock.leave_ownership();
}

template <class T>
void automation_vector<T>::unlock()
{
    _ASSERT(valid());
    if (empty())
        // nothing to do -- all ok
        return;
    to_automation(array(), static_cast<T *>(0));
    // Unlock self
    com_enforce(::SafeArrayUnlock(&array()));
}

/*template <class T>
void from_automation(SAFEARRAY &Array, automation_vector<T> *)
{
    _ASSERT(Array.cbElements == sizeof(automation_vector<T>));
    automation_vector<T>::array_lock MyLock(Array);
    VARIANT *f = static_cast<VARIANT *>(Array.pvData), 
            *t = f + Array.rgsabound[0].cElements; 
    for (; f != t; ++f)
    {
        VARIANT ShallowCopy = *f;
        new (f) automation_vector<T>(ShallowCopy, automation_vector<T>::MOVE);
    }
}

template <class T>
void to_automation(SAFEARRAY &Array, automation_vector<T> *)
{
    _ASSERT(Array.cLocks == 1 && Array.cbElements == sizeof(automation_vector<T>));
    automation_vector<T> *f = static_cast<automation_vector<T> *>(Array.pvData), 
                         *t = f + Array.rgsabound[0].cElements; 
    VARIANT *iOut = static_cast<VARIANT *>(Array.pvData);
    for (; f != t; ++f, ++iOut)
    {
        VARIANT Copy;
        ::VariantInit(&Copy);
        f->detach(Copy);
        automation_vector<T>::com_enforce(Copy.Detach(iOut));
    }
}*/

template <class T> 
void automation_vector<T>::attach(VARIANT &vSource) 
/*throw(std::invalid_argument, std::runtime_error)*/
{
    if (V_VT(&vSource) == VT_EMPTY)
    {
        clear();
        return;
    }
    if (!V_ISARRAY(&vSource))
        throw std::invalid_argument("Invalid argument passed to attach()");
    SAFEARRAY &Array = V_ISBYREF(&vSource) ? **vSource.pparray : *vSource.parray;
    if (Array.cDims != 1)
        throw std::invalid_argument("Only one-dimensional arrays are supported");
    const VARENUM vt = VARENUM(vSource.vt & VT_TYPEMASK);
    automation_vector Temp;
    if (vt == myVARENUM())
    {
        // Great, types match 
        com_enforce(::SafeArrayLock(&Array));
        from_automation(Array, static_cast<T *>(0));
        Temp.base::attach(vSource);
    }
    else
    {
        // Types don't match, we'll have to do a conversion
        Temp.resize(Array.rgsabound[0].cElements);
        VARIANT* Converted = Temp.detach();
        long f = Array.rgsabound[0].lLbound, 
            t = f + Array.rgsabound[0].cElements;
        VARIANT Buffer;
        ::VariantInit(&Buffer);
        for (; f != t; ++f)
        {
            get_element(vSource, f, Buffer);
            if (myVARENUM() != VT_VARIANT)
               com_enforce(::VariantChangeType(&Buffer, &Buffer, 0, myVARENUM()));
            put_element(*Converted, f, Buffer);
        }
        _ASSERT(Converted->vt == (myVARENUM() | VT_ARRAY));
        Temp.attach(*Converted);
        delete Converted;
    }
    swap(Temp);
    _ASSERT(valid());
}

template <class T> 
void automation_vector<T>::attach(SAFEARRAY &Array) 
/*throw(std::invalid_argument, std::runtime_error)*/
{
    VARIANT v;
    ::VariantInit(&v);
    if (Array.rgsabound->cElements == 0)
        v.vt = VT_EMPTY;
    else
    {
        ::SafeArrayGetVartype(&Array, &v.vt);
        v.vt |= VT_ARRAY;
        v.parray = &Array;
    }
    attach(v);
}

#endif // _MSC_VER

#endif //__AUTOMATION_VECTOR_H__
