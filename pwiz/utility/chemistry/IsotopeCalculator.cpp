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

#include "IsotopeCalculator.hpp"
#include "IsotopeTable.hpp"
#include "Ion.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/ptr_container/ptr_vector.hpp>


namespace pwiz {
namespace chemistry {


using boost::ptr_vector;


class IsotopeCalculator::Impl
{
    public:

    Impl(double abundanceCutoff, double massPrecision);

    MassDistribution distribution(const Formula& formula,
                                  int chargeState,
                                  int normalization) const;
    private:

    double abundanceCutoff_;
    double massPrecision_; 

    ptr_vector<IsotopeTable> tableStorage_;
    typedef map<Element::Type, const IsotopeTable*> TableMap;
    TableMap tableMap_;

    void initializeIsotopeTables();
    MassDistribution distributionManually(Element::Type e, int atomCount) const;
};


IsotopeCalculator::Impl::Impl(double abundanceCutoff, double massPrecision)
:   abundanceCutoff_(abundanceCutoff),
    massPrecision_(massPrecision)
{
    initializeIsotopeTables();
}


namespace {

bool hasLessMass(const MassAbundance& a, const MassAbundance& b)
{
    return a.mass < b.mass;
}

MassAbundance coalesce(const MassAbundance& a, const MassAbundance& b)
{
    double abundance = a.abundance + b.abundance;
    double mass = (a.abundance*a.mass + b.abundance*b.mass) / abundance;
    return MassAbundance(mass, abundance);
}

class Distinct
{
    public:

    Distinct(double mass, double precision) 
    :   mass_(mass), precision_(precision) 
    {}

    bool operator()(const MassAbundance& ma)
    {
        return fabs(ma.mass - mass_) > precision_;
    }
    
    private:
    double mass_;
    double precision_;
};

MassDistribution coalesceDistribution(const MassDistribution& md, double precision)
{
    // assumes MassDistribution md is sorted by mass!

    MassDistribution result;

    for (MassDistribution::const_iterator it=md.begin(); it!=md.end();)
    {
        Distinct distinct(it->mass, precision);
        MassDistribution::const_iterator next = find_if(it, md.end(), distinct);     
        result.push_back(accumulate(it, next, MassAbundance(), coalesce));
        it = next;
    }

    return result;
}

class Convolve
{
    public:

    Convolve(double cutoff = 0)
    :   cutoff_(cutoff)
    {}

    MassDistribution operator()(const MassDistribution& m, const MassDistribution& n)
    {
        if (m.empty()) return n;
        if (n.empty()) return m;

        MassDistribution result;

        // we could break out of these loops early if the mass distributions 
        // are sorted by abundance

        for (MassDistribution::const_iterator i=m.begin(); i!=m.end(); ++i)
        for (MassDistribution::const_iterator j=n.begin(); j!=n.end(); ++j)
        {
            double mass = i->mass + j->mass;
            double abundance = i->abundance * j->abundance;
            if (abundance > cutoff_)
                result.push_back(MassAbundance(mass, abundance));
        }

        return result;
    }

    private:
    double cutoff_;
};

class Ionize
{
    public:

    Ionize(int chargeState)
    :   chargeState_(chargeState)
    {}

    void operator()(MassAbundance& ma) const
    {
        ma.mass = Ion::mz(ma.mass, chargeState_);
    }

    private:
    int chargeState_;
};

void normalize(MassDistribution& md, int normalization)
{
    if (md.empty()) 
        return;

    double abundanceScale = 1;

    if (normalization & IsotopeCalculator::NormalizeAbundance)
    {
        double sumSquaredAbundances = 0;
        for (MassDistribution::iterator it=md.begin(); it!=md.end(); ++it)
        {
            double a = it->abundance;
            sumSquaredAbundances += a*a;
        }

        abundanceScale = sqrt(sumSquaredAbundances);
    }

    double massShift = (normalization & IsotopeCalculator::NormalizeMass) ? md[0].mass : 0;

    for (MassDistribution::iterator it=md.begin(); it!=md.end(); ++it)
    {
        it->mass -= massShift; 
        it->abundance /= abundanceScale; 
    }
}

} // namespace 


MassDistribution IsotopeCalculator::Impl::distribution(const Formula& formula,
                                                       int chargeState,
                                                       int normalization) const
{
    // collect the distributions for each element in the formula

    vector<MassDistribution> distributions; 

    Formula::Map formulaData = formula.data();
    for (Formula::Map::const_iterator it=formulaData.begin(); it!=formulaData.end(); ++it)
    {
        Element::Type e = it->first;
        int atomCount = it->second;

        TableMap::const_iterator table = tableMap_.find(e); 
        if (table != tableMap_.end())
            distributions.push_back(table->second->distribution(atomCount));
        else
            distributions.push_back(distributionManually(e, atomCount));
    }

    // coalesce each elemental distribution   

    vector<MassDistribution> coalescedDistributions;
    for (vector<MassDistribution>::iterator it=distributions.begin(); it!=distributions.end(); ++it)
        coalescedDistributions.push_back(coalesceDistribution(*it, massPrecision_)); 

    // combine the distributions and sort by mass

    MassDistribution combined = accumulate(coalescedDistributions.begin(), coalescedDistributions.end(), 
                                           MassDistribution(), Convolve(abundanceCutoff_));

    sort(combined.begin(), combined.end(), hasLessMass);

    MassDistribution result = coalesceDistribution(combined, massPrecision_);

    // adjust for charge state
    
    if (chargeState)
        for_each(result.begin(), result.end(), Ionize(chargeState));

    // normalize if requested

    if (normalization)
        normalize(result, normalization);
    
    return result;
}


namespace {

struct TableInfo
{
    Element::Type element;
    int maxAtomCount;
};

TableInfo tableInfo_[] = 
{
    {Element::C, 5000}, 
    {Element::H, 8000}, 
    {Element::N, 1500}, 
    {Element::O, 1500}, 
    {Element::S, 50}, 
};

const int tableInfoSize_ = sizeof(tableInfo_)/sizeof(TableInfo);

} // namespace


void IsotopeCalculator::Impl::initializeIsotopeTables()
{
    for (TableInfo* it=tableInfo_; it!=tableInfo_+tableInfoSize_; ++it)
    {
        IsotopeTable* temp(new IsotopeTable(Element::Info::record(it->element).isotopes, 
                                            it->maxAtomCount, 
                                            abundanceCutoff_));
        tableMap_[it->element] = temp; // store pointer in the map
        tableStorage_.push_back(temp); // maintain ownership in the ptr_vector
    }
}


MassDistribution IsotopeCalculator::Impl::distributionManually(Element::Type e, 
                                                                        int atomCount) const
{
    throw runtime_error("[IsotopeCalculator::distribution()] No table for element " 
        + Element::Info::record(e).symbol); 
}


PWIZ_API_DECL IsotopeCalculator::IsotopeCalculator(double abundanceCutoff, double massPrecision)
:   impl_(new Impl(abundanceCutoff, massPrecision))
{}


PWIZ_API_DECL IsotopeCalculator::~IsotopeCalculator(){} // auto destruction of impl_


PWIZ_API_DECL
MassDistribution IsotopeCalculator::distribution(const Formula& formula,
                                                 int chargeState,
                                                 int normalization) const
{
    return impl_->distribution(formula, chargeState, normalization);
}


} // namespace chemistry
} // namespace pwiz
