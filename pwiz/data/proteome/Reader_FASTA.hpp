//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _READER_FASTA_HPP_
#define _READER_FASTA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ProteomeData.hpp"
#include "Reader.hpp"


namespace pwiz {
namespace proteome {


/// FASTA -> ProteomeData stream serialization
class PWIZ_API_DECL Reader_FASTA : public Reader
{
    public:

    /// Reader_FASTA configuration
    struct PWIZ_API_DECL Config
    {
        /// read with a side-by-side index
        bool indexed;

        Config() {}
    };

    /// constructor
    Reader_FASTA(const Config& config = Config());

    const char* getType() const {return "FASTA";}

    virtual std::string identify(const std::string& uri,
                                 boost::shared_ptr<std::istream> uriStreamPtr) const;

    /// fill in the ProteomeData structure
    virtual void read(const std::string& uri,
                      boost::shared_ptr<std::istream> uriStreamPtr,
                      ProteomeData& result) const;

    private:
    Config config_;
    Reader_FASTA(Reader_FASTA&);
    Reader_FASTA& operator=(Reader_FASTA&);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Reader_FASTA::Config& config);


} // namespace proteome
} // namespace pwiz


#endif // _READER_FASTA_HPP_
