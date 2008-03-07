//
// IntegerSet.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _INTEGERSET_HPP_
#define _INTEGERSET_HPP_


#include <list>


namespace pwiz {
namespace util {


/// a virtual container of integers, accessible via an iterator interface,
/// stored as union of intervals
class IntegerSet
{
    public:

    /// a single closed interval of integers 
    struct Interval
    {
        int begin;
        int end;
        
        Interval(int a = 0); // allow int conversion
        Interval(int a, int b);

        friend std::ostream& operator<<(std::ostream& os, const Interval& interval);
    };

    /// collection of Interval objects
    typedef std::list<Interval> Intervals;

    /// forward iterator providing readonly access to the virtual container 
    class Iterator
    {
        public:

        /// \name instantiation 
        //@{
        /// default constructed Iterator marks end of any IntegerSet
        Iterator(); 

        /// initialized to beginning of the IntegerSet
        Iterator(const IntegerSet& integerSet);
        //@}

        /// \name forward iterator operators
        //@{
        Iterator& operator++();
        const Iterator operator++(int);
        int operator*() const; // note return by value
        bool operator!=(const Iterator& that) const; 
        bool operator==(const Iterator& that) const; 
        //@}

        /// \name standard iterator typedefs 
        //@{
        typedef std::forward_iterator_tag iterator_category;
        typedef int value_type;
        typedef int difference_type;
        typedef value_type* pointer;
        typedef value_type& reference;
        //@}

        private:
        Intervals::const_iterator it_;
        Intervals::const_iterator end_;
        int value_;
    };

    /// default construction
    IntegerSet();

    /// construction with a single integer
    explicit IntegerSet(int a);

    /// construction with a single interval
    IntegerSet(int a, int b);

    /// \name write access to the virtual container 
    //@{
    /// insert an interval of integers into the virtual container
    void insert(Interval interval);
    /// insert a single integer into the virtual container
    void insert(int a);
    /// insert an interval of integers into the virtual container
    void insert(int a, int b);
    //@}

    /// \name const iterator interface to the virtual container 
    //@{
    typedef Iterator const_iterator;
    const_iterator begin() const; 
    const_iterator end() const; 
    //@}

    /// true iff IntegerSet is empty
    bool empty() const {return intervals_.empty();}

    private:
    Intervals intervals_; 

    friend std::ostream& operator<<(std::ostream& os, const IntegerSet& integerSet);
};


} // namespace util 
} // namespace pwiz


#endif // _INTEGERSET_HPP_

