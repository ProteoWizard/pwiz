//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

#define PWIZ_SOURCE

#include "IntegerSet.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <limits>


namespace pwiz {
namespace util {


// IntegerSet::Interval implementation


PWIZ_API_DECL IntegerSet::Interval::Interval(int a)
:   begin(a), end(a) 
{}


PWIZ_API_DECL IntegerSet::Interval::Interval(int a, int b)
:   begin(a), end(b)
{
    if (a>b) throw runtime_error("[IntegerSet::Interval] Instantiation with a>b");
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const IntegerSet::Interval& interval)
{
    os << "[" << interval.begin << "," << interval.end << "]";
    return os;
}


PWIZ_API_DECL istream& operator>>(istream& is, IntegerSet::Interval& interval)
{
    string buffer;
    is >> buffer; // assumption: no whitespace within the encoded interval
    if (!is) return is;

    // first try to parse format [a,b]

    istringstream iss(buffer);

    char open = 0, comma = 0, close = 0;
    int a = 0, b = 0; 

    iss.imbue(locale("C")); // hack for msvc (Dinkumware): by default barfs on comma when reading an int
    iss >> open >> a >> comma >> b >> close;
    
    if (open=='[' && comma==',' && close==']')
    {
        interval.begin = a;
        interval.end = b;
        return is;
    }
    
    // now try format a[-][b]

    char dash = 0;
    a = 0; b = 0;

    istringstream iss2(buffer);
    iss2 >> a;
    if (iss2) interval.begin = interval.end = a;
    iss2 >> dash;
    if (dash=='-') interval.end = numeric_limits<int>::max();
    iss2 >> b;
    if (iss2) interval.end = b;
    
    return is;
}


// IntegerSet implementation


PWIZ_API_DECL IntegerSet::IntegerSet() {}
PWIZ_API_DECL IntegerSet::IntegerSet(int a) {insert(a);}
PWIZ_API_DECL IntegerSet::IntegerSet(int a, int b) {insert(a,b);}


namespace {

inline bool beginBefore(const IntegerSet::Interval& i, const IntegerSet::Interval& j)
{
    return (i.begin < j.begin);
}

inline bool endBefore(const IntegerSet::Interval& i, const IntegerSet::Interval& j)
{
    return (i.end < j.end);
}

} // namespace


PWIZ_API_DECL void IntegerSet::insert(Interval interval) 
{
    // eat any subintervals 

    Intervals::iterator eraseBegin = lower_bound(intervals_.begin(), intervals_.end(), 
                                                 interval.begin, beginBefore);

    Intervals::iterator eraseEnd = lower_bound(intervals_.begin(), intervals_.end(), 
                                               interval.end, endBefore);

    Intervals::iterator insertionPoint = intervals_.erase(eraseBegin, eraseEnd);

    // eat our left neighbor if it's next to us 

    if (insertionPoint != intervals_.begin())
    {
        --insertionPoint;
        const Interval& left = *insertionPoint;
        if (left.end >= interval.begin - 1)
        {
            interval.begin = left.begin;
            insertionPoint = intervals_.erase(insertionPoint);
        }
        else 
        {
            ++insertionPoint;
        }
    }

    // eat our right neighbor if it's next to us 

    if (insertionPoint != intervals_.end())
    {
        const Interval& right = *insertionPoint;
        if (right.begin <= interval.end + 1)
        {
            interval.end = right.end;
            insertionPoint = intervals_.erase(insertionPoint);
        }
    }

    // insert the interval

    intervals_.insert(insertionPoint, interval);
}


PWIZ_API_DECL void IntegerSet::insert(int a) 
{
    insert(Interval(a));
}


PWIZ_API_DECL void IntegerSet::insert(int a, int b) 
{
    insert(Interval(a,b));
}


PWIZ_API_DECL void IntegerSet::parse(const std::string& intervalList)
{
    istringstream iss(intervalList);
    vector<Interval> intervals;
    copy(istream_iterator<Interval>(iss), istream_iterator<Interval>(), back_inserter(intervals));
    for (vector<Interval>::const_iterator it=intervals.begin(); it!=intervals.end(); ++it)
        insert(*it);
}


PWIZ_API_DECL IntegerSet::const_iterator IntegerSet::begin() const 
{
    return IntegerSet::Iterator(*this);
}


PWIZ_API_DECL IntegerSet::const_iterator IntegerSet::end() const
{
    return IntegerSet::Iterator();
}


PWIZ_API_DECL bool IntegerSet::contains(int n) const
{
    for (Intervals::const_iterator it=intervals_.begin(); it!=intervals_.end(); ++it)
        if (it->contains(n)) return true;
    return false;
}


PWIZ_API_DECL bool IntegerSet::hasUpperBound(int n) const
{
    if (empty()) return true;
    int highest = intervals_.back().end;
    return highest <= n;
}


PWIZ_API_DECL size_t IntegerSet::intervalCount() const
{
    return intervals_.size();
}


PWIZ_API_DECL size_t IntegerSet::size() const
{
    size_t result = 0;
    for (Intervals::const_iterator it=intervals_.begin(); it!=intervals_.end(); ++it)
        result += (it->end - it->begin + 1);
    return result;            
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const IntegerSet& integerSet)
{
    copy(integerSet.intervals_.begin(), integerSet.intervals_.end(), 
         ostream_iterator<IntegerSet::Interval>(os," "));
    return os;
}


// IntegerSet::Iterator implementation


namespace {
// used for default-constructed Iterators, so that it_ and end_ 
// can be intialized and are comparable
IntegerSet::Intervals nothing_;  
} // namespace


PWIZ_API_DECL IntegerSet::Iterator::Iterator() 
:   it_(nothing_.end()),
    end_(nothing_.end()),
    value_(0)
{}


PWIZ_API_DECL IntegerSet::Iterator::Iterator(const IntegerSet& integerSet)
:   it_(integerSet.intervals_.begin()),
    end_(integerSet.intervals_.end()),
    value_(it_!=end_ ? it_->begin : 0) 
{}


PWIZ_API_DECL IntegerSet::Iterator& IntegerSet::Iterator::operator++()
{
    value_++;

    // when we finish an interval, jump to the next
    if (value_ > it_->end)
    {
        ++it_;
        value_ = it_!=end_ ? it_->begin : 0;  
    }

    return *this;
}


PWIZ_API_DECL const IntegerSet::Iterator IntegerSet::Iterator::operator++(int)
{
    Iterator temp(*this);
    this->operator++();
    return temp;
}


PWIZ_API_DECL int IntegerSet::Iterator::operator*() const
{
    if (it_ == end_)
        throw runtime_error("[IntegerSet::Iterator::operator*()] Invalid dereference.");

    return value_;
}


PWIZ_API_DECL bool IntegerSet::Iterator::operator!=(const Iterator& that) const
{
    return !this->operator==(that);
}


PWIZ_API_DECL bool IntegerSet::Iterator::operator==(const Iterator& that) const
{
    // true in two cases:
    // 1) "this" and "that" are both valid and equal
    // 2) "this" is at the end, and "that" is the special default constructed nothing_.end()

    return (it_!=end_ && that.it_!=that.end_ && it_==that.it_ && value_==that.value_ ||
            it_==end_ && that.it_==nothing_.end());
}


} // namespace util 
} // namespace pwiz


