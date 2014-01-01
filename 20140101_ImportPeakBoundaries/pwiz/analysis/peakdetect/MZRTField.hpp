//
// $Id$
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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/concept/assert.hpp"
#include "boost/concept/usage.hpp"
#include <set>
#include <vector>


namespace pwiz {
namespace analysis {


using chemistry::MZTolerance;


namespace {

///
/// lexicographic ordering, by m/z then retention time
///
template <typename T>
struct LessThan_MZRT
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
        const T& c = t;
        a = c.retentionTimeMin(); // must have member 'double retentionTimeMin() const'
        a = c.retentionTimeMax(); // must have member 'double retentionTimeMin() const'
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
    //BOOST_CONCEPT_ASSERT((HasMZRT<T>));

    typedef boost::shared_ptr<T> TPtr;

    /// find all objects with a given m/z, within a given m/z tolerance,
    /// satisfying the 'matches' predicate
    template <typename RTMatches>
    std::vector<TPtr> 
    find(double mz, MZTolerance mzTolerance, RTMatches matches) const;

    /// remove an object via a shared reference, rather than an iterator into the set
    void remove(const TPtr& p); 
};


typedef MZRTField<pwiz::data::peakdata::Peakel> PeakelField;
typedef MZRTField<pwiz::data::peakdata::Feature> FeatureField;


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const PeakelField& peakelField);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const FeatureField& featureField);


/// predicate always returns true
template <typename T>
struct RTMatches_Any
{
    bool operator()(const T& t) const {return true;}
};


/// predicate returns true iff the object's retention time range contains the specified
/// retention time
template <typename T>
struct RTMatches_Contains
{
    RTMatches_Contains(double rt, double rtTolerance = 0) : rt_(rt), rtTolerance_(rtTolerance) {}

    bool operator()(const T& t) const
    {
        return rt_>t.retentionTimeMin()-rtTolerance_ && rt_<t.retentionTimeMax()+rtTolerance_;
    }

    private:
    double rt_;
    double rtTolerance_;
};


/// predicate returns true iff the object's retention time range is completely contained within
/// the range of the specified reference object, up to the specified tolerance
template <typename T>
struct RTMatches_IsContainedIn
{
    RTMatches_IsContainedIn(const T& reference, double rtTolerance = 0)
    :   reference_(reference), rtTolerance_(rtTolerance) {}

    bool operator()(const T& t) const
    {
        return t.retentionTimeMin() > reference_.retentionTimeMin() - rtTolerance_ &&
               t.retentionTimeMax() < reference_.retentionTimeMax() + rtTolerance_;
    }

    private:
    const T& reference_;
    double rtTolerance_;
};


template <typename T>
template <typename RTMatches>
std::vector< boost::shared_ptr<T> > 
MZRTField<T>::find(double mz, MZTolerance mzTolerance, RTMatches matches) const
{
    TPtr target(new T);

    // use binary search to get a std::set iterator range

    target->mz = mz - mzTolerance;
    typename MZRTField<T>::const_iterator begin = this->lower_bound(target);

    target->mz = mz + mzTolerance;
    typename MZRTField<T>::const_iterator end = this->upper_bound(target);

    // linear copy_if within range 

    std::vector<TPtr> result;

    for (typename MZRTField<T>::const_iterator it=begin; it!=end; ++it)
        if (matches(**it))
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

