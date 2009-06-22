//
// MZRTField.hpp
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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
                                                                                                     

#ifndef _MZRTFIELD_HPP_
#define _MZRTFIELD_HPP_


#include "MZTolerance.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/concept/assert.hpp"
#include "boost/concept/usage.hpp"
#include <set>
#include <vector>


namespace pwiz {
namespace analysis {


namespace {

///
/// lexicographic ordering, by m/z then retention time
///
template <typename T>
struct PWIZ_API_DECL LessThan_MZRT
{
    typedef boost::shared_ptr<T> TPtr;

    bool operator()(const T& a, const T& b) const
    {
        if (a.mz < b.mz) return true;
        if (b.mz < a.mz) return false;
        return (a.retentionTime < b.retentionTime); // rare
    }

    bool operator()(const TPtr& a, const TPtr& b) const
    {
        return (*this)(*a, *b);
    }
};


///
/// struct for Boost concept checking
///
template <typename T>
struct HasMZRT
{
    BOOST_CONCEPT_USAGE(HasMZRT)
    {
        T t; // default construction
        double a = t.mz; // must have member 'mz'
        a = t.retentionTime; // must have member 'retentionTime'
        a = t.retentionTimeMin(); // must have member 'double retentionTimeMin()'
        a = t.retentionTimeMax(); // must have member 'double retentionTimeMin()'
    }
};

} // namespace


///
/// MZRTField is a std::set of boost::shared_ptrs, stored as a binary tree 
/// ordered by LessThan_MZRT
///
template <typename T>
struct MZRTField : public std::set< boost::shared_ptr<T>, LessThan_MZRT<T> >
{
    BOOST_CONCEPT_ASSERT((HasMZRT<T>));

    typedef boost::shared_ptr<T> TPtr;

    /// find all objects in a given mz/rt range
    std::vector<TPtr> find(double mz, MZTolerance mzTolerance,
                           double retentionTime, double rtTolerance) const;

    /// remove an object via a shared reference, rather than an iterator into the set
    void remove(const TPtr& p); 
};


typedef MZRTField<pwiz::data::peakdata::Peakel> PeakelField;
typedef MZRTField<pwiz::data::peakdata::Feature> FeatureField;


namespace {
template <typename T>
struct RTMatches
{
    double rt;
    double rtTolerance;

    RTMatches(double _rt, double _rtTolerance) : rt(_rt), rtTolerance(_rtTolerance) {}

    typedef boost::shared_ptr<T> TPtr;

    bool operator()(const TPtr& t)
    {
        return (rt > t->retentionTimeMin() - rtTolerance &&
                rt < t->retentionTimeMax() + rtTolerance);
    }
};
} // namespace


template <typename T>
std::vector< boost::shared_ptr<T> >
MZRTField<T>::find(double mz, MZTolerance mzTolerance,
                   double retentionTime, double rtTolerance) const
{
    TPtr target(new T);

    target->mz = mz - mzTolerance;
    typename MZRTField<T>::const_iterator begin = this->lower_bound(target);

    target->mz = mz + mzTolerance;
    typename MZRTField<T>::const_iterator end = this->upper_bound(target);

    std::vector<TPtr> result;
    RTMatches<T> matches(retentionTime, rtTolerance);

    // copy_if(begin, end, back_inserter(result), matches); // some day this line will compile 
    for (typename MZRTField<T>::const_iterator it=begin; it!=end; ++it)
        if (matches(*it))
            result.push_back(*it);

    return result;
}


template <typename T>
void MZRTField<T>::remove(const boost::shared_ptr<T>& p)
{
    std::pair<typename MZRTField<T>::iterator, typename MZRTField<T>::iterator> 
        range = this->equal_range(p); // uses LessThan_MZRT
    
    typename MZRTField<T>::iterator 
        found = std::find(range.first, range.second, p); // uses shared_ptr::operator==

    if (found == range.second) throw std::runtime_error("[MZRTField::remove()] TPtr not found.");

    this->erase(found);
}


} // namespace analysis
} // namespace pwiz


#endif // _MZRTFIELD_HPP_

