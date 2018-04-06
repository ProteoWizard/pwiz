//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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

#include "ProteinList_DecoyGenerator.hpp"
#include <boost/random.hpp>
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


using namespace pwiz::util;
using namespace pwiz::proteome;


//
// ProteinList_DecoyGenerator::Impl
//


struct ProteinList_DecoyGenerator::Impl
{
    const ProteinListPtr original;
    const PredicatePtr predicate;

    Impl(ProteinListPtr original, PredicatePtr predicate)
    :   original(original), predicate(predicate)
    {
        if (!original.get()) throw runtime_error("[ProteinList_DecoyGenerator] Null pointer");
    }
};


//
// ProteinList_DecoyGenerator
//


PWIZ_API_DECL ProteinList_DecoyGenerator::ProteinList_DecoyGenerator(const ProteinListPtr& original, const PredicatePtr& predicate)
:   ProteinListWrapper(original), impl_(new Impl(original, predicate))
{}


PWIZ_API_DECL size_t ProteinList_DecoyGenerator::size() const
{
    // a decoy is generated for every protein in the original list
    return impl_->original->size() * 2;
}

PWIZ_API_DECL size_t ProteinList_DecoyGenerator::find(const string& id) const
{
    if (bal::starts_with(id, impl_->predicate->decoyPrefix()))
    {
        // a decoy protein's index is the original protein's index plus the original size
        string originalId(id.begin() + impl_->predicate->decoyPrefix().length(), id.end());
        size_t originalIndex = impl_->original->find(originalId);
        return originalIndex + impl_->original->size();
    }
    return impl_->original->find(id);
}

PWIZ_API_DECL ProteinPtr ProteinList_DecoyGenerator::protein(size_t index, bool getSequence) const
{
    if (index > size())
        throw out_of_range("[ProteinList_DecoyGenerator::protein] Index out of range");

    size_t originalIndex = index % impl_->original->size();
    ProteinPtr protein = impl_->original->protein(originalIndex, getSequence);

    // the second half of the database is decoys
    if (index >= impl_->original->size())
    {
        protein = impl_->predicate->generate(*protein);
        protein->index = index;
    }

    return protein;
}


//
// ProteinList_DecoyGeneratorPredicate_Reversed
//


PWIZ_API_DECL ProteinList_DecoyGeneratorPredicate_Reversed::ProteinList_DecoyGeneratorPredicate_Reversed(const std::string& decoyPrefix)
{
    decoyPrefix_ = decoyPrefix;
}


PWIZ_API_DECL ProteinPtr ProteinList_DecoyGeneratorPredicate_Reversed::generate(const Protein& protein) const
{
    string reversedSequence(protein.sequence().rbegin(), protein.sequence().rend());
    return ProteinPtr(new Protein(decoyPrefix_ + protein.id, protein.index, "", reversedSequence));
}


//
// ProteinList_DecoyGeneratorPredicate_Shuffled::Impl
//


struct ProteinList_DecoyGeneratorPredicate_Shuffled::Impl
{
    Impl(boost::uint32_t randomSeed)
    :   engine(randomSeed), rng(engine, distribution)
    {
    }

    boost::mt19937 engine;
    boost::uniform_int<> distribution;
    boost::variate_generator<boost::mt19937, boost::uniform_int<> > rng;
};

//
// ProteinList_DecoyGeneratorPredicate_Shuffled
//


PWIZ_API_DECL ProteinList_DecoyGeneratorPredicate_Shuffled::ProteinList_DecoyGeneratorPredicate_Shuffled(const std::string& decoyPrefix, boost::uint32_t randomSeed)
:   impl_(new Impl(randomSeed))
{
    decoyPrefix_ = decoyPrefix;
}


PWIZ_API_DECL ProteinPtr ProteinList_DecoyGeneratorPredicate_Shuffled::generate(const Protein& protein) const
{
    string shuffledSequence = protein.sequence();
    std::random_shuffle(shuffledSequence.begin(), shuffledSequence.end(), impl_->rng);
    return ProteinPtr(new Protein(decoyPrefix_ + protein.id, protein.index, "", shuffledSequence));
}

} // namespace analysis
} // namespace pwiz
