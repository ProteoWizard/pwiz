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


#ifndef _IPIFASTADATABASE_HPP_
#define _IPIFASTADATABASE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <memory>
#include <string>
#include <vector>
#include <iosfwd>


namespace pwiz {
namespace proteome {


/// class for accessing data in ipi.*.fasta files
class PWIZ_API_DECL IPIFASTADatabase
{
    public:

    /// constructor reads in entire file
    IPIFASTADatabase(const std::string& filename);
    ~IPIFASTADatabase();

    /// structure for holding peptide info
    struct PWIZ_API_DECL Record
    {
        int id;
        std::string faID;
        std::string fullSeqDescription;
        std::string sequence;
        
        Record(int _id=0) : id(_id) {}
    };

    
    /// access to the data in memory
    const std::vector<Record>& records() const; 
    
    /// typedef to simplify declaration of Record iterator
    typedef std::vector<Record>::const_iterator const_iterator;

    // begin() and end()
    const_iterator begin();
    const_iterator end();

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const IPIFASTADatabase::Record& record);


} // namespace proteome
} // namespace pwiz


#endif // _IPIFASTADATABASE_HPP_

