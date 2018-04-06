//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SERIALIZER_FASTA_HPP_
#define _SERIALIZER_FASTA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ProteomeData.hpp"
#include "pwiz/data/common/MemoryIndex.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace proteome {


/// ProteomeData <-> FASTA stream serialization
class PWIZ_API_DECL Serializer_FASTA
{
    public:
        
    /// Serializer_FASTA configuration
    struct PWIZ_API_DECL Config
    {
        data::IndexPtr indexPtr;

        Config() : indexPtr(new data::MemoryIndex) {}
    };

    Serializer_FASTA(const Config& config = Config());

    /// write ProteomeData object to ostream as FASTA
    void write(std::ostream& os, const ProteomeData& pd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const;

    /// read in ProteomeData object from a FASTA istream
    void read(boost::shared_ptr<std::istream> is, ProteomeData& pd) const;

    private:
    Config config_;
    Serializer_FASTA(Serializer_FASTA&);
    Serializer_FASTA& operator=(Serializer_FASTA&);
};


} // namespace proteome
} // namespace pwiz


#endif // _SERIALIZER_FASTA_HPP_

