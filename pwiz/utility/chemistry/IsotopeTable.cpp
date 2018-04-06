//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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

#include "IsotopeTable.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>


using namespace pwiz::chemistry;


namespace pwiz {
namespace chemistry {


//
//  Implementation notes:
//
//  The data is arranged as an array of records.  The nth record contains the  
//  mass distribution for a collection of n atoms, i.e. if i is an index into 
//  the table, then atomCount == i+1.
// 
//  Each record is a multidimensional array, where the dimension is one less 
//  than the size of the initial MassDistribution md.  Indexing into the array
//  is performed by a vector (MultiIndex) of the same dimension.
//
//  Suppose 
//      m = <m0, m1, ... , mr> are the isotope masses, and 
//      p = <p0, p1, ... , pr> are the corresponding abundances 
//      n = # of atoms
//
//  Then each record will have dimension r.
//
//  If <i1, ... , ir> is a MultiIndex, let i0 = n - sum(i1,...,ir), so
//  {i0, i1, ... , ir} is the corresponding partition of n.
//
//  The corresponding entry in the array is then the multinomial term:
//      A(i0, i1,...ir) = p0^i0 * p1^i1 * ... * pr^ir * C(n; i0,...ir)
//
//  The entries can be computed using the recursion formula:
//      A(i0,...,ij+1,...,ik-1,...,ir) = A(i0,...,ir) * pj/pk * ik/(ij+1) 
//
//  Note that only abundances are stored in the array, since the masses
//  can be computed by the multi-index.
//

class IsotopeTable::Impl
{
    public:

    Impl(const MassDistribution& md, int atomCount, double cutoff);
    MassDistribution distribution(int atomCount) const;

    private:

    // parameters
    MassDistribution md_;
    int maxAtomCount_;
    double cutoff_;

    // table data 
    typedef vector<int> MultiIndex; // multidimensional index into a record
    friend ostream& operator<<(ostream& os, const MultiIndex& multiIndex);
    
    MultiIndex extents_; // extents of each dimension of a record
    int recordSize_; // total volume of the multidimensional array (# of doubles) 

    typedef vector<double> Table;
    Table table_;

    // initialization
    void sortMassDistribution();
    void allocateTable();
    void computeTableValues();

    // record access
    Table::iterator recordBegin(int recordIndex) {return table_.begin()+recordIndex*recordSize_;}
    Table::const_iterator recordBegin(int recordIndex) const {return table_.begin()+recordIndex*recordSize_;}
    Table::iterator recordEnd(int recordIndex) {return table_.begin()+(recordIndex+1)*recordSize_;}
    Table::const_iterator recordEnd(int recordIndex) const {return table_.begin()+(recordIndex+1)*recordSize_;}

    // multidimensional indexing into a record  
    void increment(MultiIndex& multiIndex) const;
    double mass(int atomCount, const MultiIndex& multiIndex) const;
    Table::const_iterator predecessor(const Table::iterator& it, int dimension) const;
    int firstNonzeroDimension(const MultiIndex& multiIndex) const;
    double computeNextValue(double previousValue, const MultiIndex& multiIndex, int previousDimension, int atomCount) const;
    
    // debugging
    public:
    void printInfo(ostream& os) const;
    private:
    void printRecord(int recordIndex) const;
};


IsotopeTable::Impl::Impl(const MassDistribution& md, int atomCount, double cutoff) 
:   md_(md), maxAtomCount_(atomCount), cutoff_(cutoff), 
    recordSize_(0)
{
    sortMassDistribution();
    allocateTable();
    computeTableValues();
}


namespace {

bool hasGreaterAbundance(const MassAbundance& ma1, const MassAbundance& ma2)
{
    return ma1.abundance > ma2.abundance;
}

bool hasLessMass(const MassAbundance& a, const MassAbundance& b)
{
    return a.mass < b.mass;
}

bool hasZeroAbundance(const MassAbundance& ma)
{
    return ma.abundance == 0;
}

} // namespace


MassDistribution IsotopeTable::Impl::distribution(int atomCount) const
{
    if (atomCount<0 || atomCount>maxAtomCount_)
        throw runtime_error("[IsotopeTable::distribution()] Index out of range.");

    if (atomCount == 0) return MassDistribution();

    MassDistribution result;
    int recordIndex = atomCount - 1;

    MultiIndex multiIndex(extents_.size());

    for (Table::const_iterator it=recordBegin(recordIndex), end=recordEnd(recordIndex); 
         it!=end; 
         ++it, increment(multiIndex)) 
    {
         if (*it > cutoff_)
            result.push_back(MassAbundance(mass(atomCount, multiIndex), *it)); 
    }

    sort(result.begin(), result.end(), hasLessMass);

    return result;
}


void IsotopeTable::Impl::sortMassDistribution()
{
    // remove zeros and sort the MassDistribution

    md_.erase(remove_if(md_.begin(), md_.end(), hasZeroAbundance), md_.end());
    sort(md_.begin(), md_.end(), hasGreaterAbundance);

    if (md_.empty())
        throw runtime_error("[IsotopeTable::sortMassDistribution()] No isotopes!");
}


//
//  n = total number of atoms 
//  p = fraction of the less abundant isotope 
//  k = number of atoms of the less abundant isotope 
//
//  Pr(k) = probability of having exactly k atoms of the less abundant isotope 
//        = C(n,k) * p^k * (1-p)^(n-k)
//

//  Calculate point in binomial distribution at which its tail
//  falls below the cutoff.
namespace {
int tailCutoffPosition(int n, double p, double cutoff)
{
    double probability = pow(1-p, n); 
    double k_expected = n*p;

    for (int k=0; k<=n; k++)
    {
        if (k>k_expected && probability<cutoff)
            return k;

        probability *= p/(1-p)*(n-k)/(k+1);
    }

    return n+1;
}
} // namespace


void IsotopeTable::Impl::allocateTable()
{
    // calculate table size and allocate memory

    for (unsigned int i=1; i<md_.size(); i++)
    {
        double p = md_[i].abundance;
        int extent = tailCutoffPosition(maxAtomCount_, p, cutoff_); 
        extents_.push_back(extent);
    }

    recordSize_ = 1; 
    for (MultiIndex::const_iterator it=extents_.begin(); it!=extents_.end(); ++it)
        recordSize_ *= *it;

    table_.resize(maxAtomCount_ * recordSize_);
}


int IsotopeTable::Impl::firstNonzeroDimension(const MultiIndex& multiIndex) const
{
    for (int i=0; i<(int)multiIndex.size(); i++)
        if (multiIndex[i]) return i;

    throw runtime_error("[IsotopeTable::firstNonzeroDimension()] multiIndex is zero.");
}


IsotopeTable::Impl::Table::const_iterator 
IsotopeTable::Impl::predecessor(const IsotopeTable::Impl::Table::iterator& it, 
                                int dimension) const
{
    // dimension == 0-based index into a MultiIndex
    // If multiIndex is the MultiIndex corresponding to iterator it,
    // then return the iterator corresponding to multiIndex - (0,...,0,1,0,...,0),
    // where the 1 is in place indicated by dimension.

    int delta = 1;

    for (int i=1; i<=dimension; i++)
        delta *= extents_[i-1];

    return it - delta; 
}
            

double IsotopeTable::Impl::computeNextValue(double previousValue, 
                                            const IsotopeTable::Impl::MultiIndex& multiIndex, 
                                            int previousDimension,
                                            int atomCount) const
{
    // this is the recursion step in computing the multinomial probabilities

    double p0 = md_[0].abundance;
    double pj = md_[previousDimension+1].abundance;
    double k0_previous = atomCount - accumulate(multiIndex.begin(), multiIndex.end(), 0) + 1; 
    double kj = multiIndex[previousDimension];

    return previousValue * pj/p0 * k0_previous/kj;
}


void IsotopeTable::Impl::computeTableValues()
{
    const double p0 = md_[0].abundance;

    for (int recordIndex=0; recordIndex<maxAtomCount_; recordIndex++)
    {
        const int n = recordIndex + 1; // number of atoms

        Table::iterator begin = recordBegin(recordIndex), end=recordEnd(recordIndex);
        MultiIndex multiIndex(extents_.size()); 

        *begin = pow(p0, n);
        increment(multiIndex);

        for (Table::iterator it=begin+1; it!=end; ++it, increment(multiIndex))
        {
            if (accumulate(multiIndex.begin(), multiIndex.end(), 0) > n)
            {
                *it = 0;
                continue;
            }

            // for now, choose predecessor by first non-zero dimension
            // in order compute the next value 

            int previousDimension = firstNonzeroDimension(multiIndex); 
            Table::const_iterator previous = predecessor(it, previousDimension);
            double previousValue = *previous;
            double newValue = computeNextValue(previousValue, multiIndex, previousDimension, n); 
            *it = newValue;
        }
    }
}


void IsotopeTable::Impl::increment(MultiIndex& multiIndex) const
{
    // increments the MultiIndex, where dimension 0 is the least significant

    if (multiIndex.empty()) return; 

    for (unsigned int i=0; i<multiIndex.size(); i++) 
    {
        if (++multiIndex[i] >= extents_[i])
            multiIndex[i] = 0; // continue loop == carry
        else
            break;
    }
}


double IsotopeTable::Impl::mass(int atomCount, const MultiIndex& multiIndex) const
{
    if (multiIndex.size() != extents_.size() ||
        multiIndex.size() != md_.size()-1) 
        throw runtime_error("[IsotopeTable::mass()] oops");

    double result = 0;
    int atomsCounted = 0;
    for (unsigned int i=0; i<multiIndex.size(); i++)
    {
        double mass = md_[i+1].mass;
        int count = multiIndex[i];
        result += mass * count; 
        atomsCounted += count;
    }
    
    if (atomsCounted > atomCount) 
        throw runtime_error("[IsotopeTable::mass()] Counted too many atoms.");

    result += md_[0].mass * (atomCount - atomsCounted); 

    return result;
}


void IsotopeTable::Impl::printInfo(ostream& os) const
{
    os << "extents: ";
    copy(extents_.begin(), extents_.end(), ostream_iterator<int>(os, " "));
    os << endl;

    os << "record count: " << maxAtomCount_ << endl;
    os << "record size: " << recordSize_ << endl;
    os << "table size: " << maxAtomCount_ * recordSize_ << endl; 
    os << endl;

    for (int i=0; i<maxAtomCount_; i++)
    {
        os << "record " << i << endl;
        printRecord(i);
        os << endl;
    }
}


void IsotopeTable::Impl::printRecord(int recordIndex) const
{
    if (extents_.empty())
    {
        cout << *recordBegin(recordIndex) << endl;
        return;
    }

    MultiIndex multiIndex(extents_.size());

    cout << setprecision(12);

    for (Table::const_iterator it=recordBegin(recordIndex), end=recordEnd(recordIndex); 
         it!=end; 
         ++it, increment(multiIndex)) 
    {
         cout << *it << " ";
         if (multiIndex.size()>=1 && multiIndex[0]==extents_[0]-1) 
         {
            cout << endl;
            if (multiIndex.size()>=2 && multiIndex[1]==extents_[1]-1) 
                cout << endl;
         }
    }
}


// IsotopeTable implementation


PWIZ_API_DECL IsotopeTable::IsotopeTable(const MassDistribution& md, int maxAtomCount, double cutoff)
:   impl_(new Impl(md, maxAtomCount, cutoff))
{}


PWIZ_API_DECL IsotopeTable::~IsotopeTable() {} // auto destruction of impl_


PWIZ_API_DECL MassDistribution IsotopeTable::distribution(int atomCount) const 
{
    return impl_->distribution(atomCount);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const IsotopeTable& isotopeTable)
{
    isotopeTable.impl_->printInfo(os);
    return os;
}


} // namespace chemistry
} // namespace pwiz
