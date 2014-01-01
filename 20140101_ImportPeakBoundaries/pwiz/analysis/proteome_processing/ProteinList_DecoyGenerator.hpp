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


#ifndef _PROTEINLIST_DECOYGENERATOR_HPP_
#define _PROTEINLIST_DECOYGENERATOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/proteome/ProteinListWrapper.hpp"
#include <boost/cstdint.hpp>


namespace pwiz {
namespace analysis {


using namespace pwiz::proteome;


/// ProteinList decoy generator for creating decoy proteins on the fly
class PWIZ_API_DECL ProteinList_DecoyGenerator : public proteome::ProteinListWrapper
{
    public:

    /// client-implemented generator predicate -- called during construction of
    /// ProteinList_DecoyGenerator to create a decoy protein from a target protein
    struct PWIZ_API_DECL Predicate
    {
        /// return a decoy protein based on an input target protein
        virtual ProteinPtr generate(const Protein& protein) const = 0;

        /// return the string prefixed to a protein id to indicate it is a decoy
        virtual const std::string& decoyPrefix() const {return decoyPrefix_;}

        virtual ~Predicate() {}

        protected:
        std::string decoyPrefix_;
    };

    typedef boost::shared_ptr<Predicate> PredicatePtr;

    ProteinList_DecoyGenerator(const ProteinListPtr& original, const PredicatePtr& predicate);

    /// \name ProteinList interface
    //@{
    virtual size_t size() const;
    virtual size_t find(const std::string& id) const;
    virtual ProteinPtr protein(size_t index, bool getSequence = true) const;
    //@}

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    ProteinList_DecoyGenerator(const ProteinList_DecoyGenerator&);
    ProteinList_DecoyGenerator& operator=(const ProteinList_DecoyGenerator&);
};


/// creates a reversed copy of every target protein with the specified decoy string prefixed to the id
class PWIZ_API_DECL ProteinList_DecoyGeneratorPredicate_Reversed : public ProteinList_DecoyGenerator::Predicate
{
    public:
    ProteinList_DecoyGeneratorPredicate_Reversed(const std::string& decoyPrefix);

    virtual ProteinPtr generate(const Protein& protein) const;
};


/// creates a randomly shuffled copy of every target protein with the specified decoy string prefixed to the id
class PWIZ_API_DECL ProteinList_DecoyGeneratorPredicate_Shuffled : public ProteinList_DecoyGenerator::Predicate
{
    public:
    ProteinList_DecoyGeneratorPredicate_Shuffled(const std::string& decoyPrefix, boost::uint32_t randomSeed = 0u);

    virtual ProteinPtr generate(const Protein& protein) const;

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
};


} // namespace analysis
} // namespace pwiz


#endif // _PROTEINLIST_DECOYGENERATOR_HPP_
