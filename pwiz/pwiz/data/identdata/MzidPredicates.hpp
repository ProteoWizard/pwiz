//
// $Id$
//
//
// Origional author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#ifndef MZID_MZIDPREDICATE_HPP
#define MZID_MZIDPREDICATE_HPP


#include "IdentData.hpp"
#include <string>
#include <boost/shared_ptr.hpp>
#include <boost/algorithm/string/predicate.hpp>

namespace pwiz {
namespace identdata {

template<typename T>
struct id_p
{
    const std::string id;

    id_p(const std::string id) : id(id) {}

    bool operator()(const boost::shared_ptr<T>& t) const
    {
        return t->id == id;
    }
};

struct software_p
{
    CVID id;
    
    software_p(const CVID id) : id(id) {}

    bool operator()(const AnalysisSoftwarePtr p)
    {
        bool result = false;

        if (p.get())
            result = p->softwareName.hasCVParam(id);

        return result;
    }
};

struct sequence_p
{
    const std::string seq;
    
    sequence_p(const std::string& seq) : seq(seq) {}

    bool operator()(const PeptidePtr& p) const
    {
        return (p->peptideSequence == seq);
    }
};

struct seq_p
{
    const std::string seq;
    
    seq_p(const std::string& seq) : seq(seq) {}

    bool operator()(const DBSequencePtr& dbs) const
    {
        return (dbs->seq == seq);
    }
};

struct dbsequence_p
{
    const std::string seq;
    const std::string accession;
    
    dbsequence_p(const std::string& seq,
                 const std::string accession)
        : seq(seq), accession(accession) {}

    bool operator()(const DBSequencePtr& p) const
    {
        return boost::iequals(p->seq, seq) &&
            boost::iequals(p->accession, accession);
    }
};

struct organization_p
{
    bool operator()(ContactPtr contact)
    {
        return typeid(contact.get()).name() == typeid(Organization*).name();
    }
};

struct person_p
{
    bool operator()(ContactPtr contact)
    {
        return typeid(contact.get()).name() == typeid(Person*).name();
    }
};

} // namespace pwiz 
} // namespace identdata 

#endif // MZID_MZIDPREDICATE_HPP

