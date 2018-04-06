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


#define PWIZ_SOURCE

#include "Diff.hpp"
#include "TextWriter.hpp"
#include "pwiz/data/common/diff_std.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>


namespace pwiz {
namespace data {
namespace diff_impl {


using namespace pwiz::proteome;


PWIZ_API_DECL
void diff(const Protein& a,
          const Protein& b,
          Protein& a_b,
          Protein& b_a,
          const DiffConfig& config)
{
    a_b = Protein("", 0, "", "");
    b_a = Protein("", 0, "", "");

    // important scan metadata
    diff_integral(a.index, b.index, a_b.index, b_a.index, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);

    if (!config.ignoreMetadata)
    {
        diff(a.description, b.description, a_b.description, b_a.description, config);
    }

    string sequenceA_B, sequenceB_A;
    diff(a.sequence(), b.sequence(), sequenceA_B, sequenceB_A, config);

    // provide context
    if (!a_b.empty() || !b_a.empty() ||
        !sequenceA_B.empty() || !sequenceB_A.empty()) 
    {
        a_b = Protein(a.id, a.index, a_b.description, sequenceA_B);
        b_a = Protein(b.id, b.index, b_a.description, sequenceB_A);
    }
}


PWIZ_API_DECL
void diff(const ProteinList& a,
          const ProteinList& b,
          ProteinListSimple& a_b,
          ProteinListSimple& b_a,
          const DiffConfig& config)
{
    a_b.proteins.clear();
    b_a.proteins.clear();

    if (a.size() != b.size())
    {
        ProteinPtr dummy(new Protein("dummy", 0, "ProteinList sizes differ", ""));
        a_b.proteins.push_back(dummy);
        return;
    }

    for (size_t i=0; i < a.size(); ++i)
    { 
        ProteinPtr temp_a_b(new Protein("", 0, "", ""));        
        ProteinPtr temp_b_a(new Protein("", 0, "", ""));
        diff(*a.protein(i, true), *b.protein(i, true), *temp_a_b, *temp_b_a, config);

        if (!temp_a_b->empty() || !temp_b_a->empty())
        {
            a_b.proteins.push_back(temp_a_b);
            b_a.proteins.push_back(temp_b_a);
        }
    }
}


PWIZ_API_DECL
void diff(const ProteomeData& a,
          const ProteomeData& b,
          ProteomeData& a_b,
          ProteomeData& b_a,
          const DiffConfig& config)
{
    if (!config.ignoreMetadata)
    {
        diff(a.id, b.id, a_b.id, b_a.id, config);
    }

    // special handling for ProteinList diff
    shared_ptr<ProteinListSimple> temp_a_b(new ProteinListSimple); 
    shared_ptr<ProteinListSimple> temp_b_a(new ProteinListSimple);
    a_b.proteinListPtr = temp_a_b;
    b_a.proteinListPtr = temp_b_a; 
    ProteinListPtr temp_a = a.proteinListPtr.get() ? a.proteinListPtr : ProteinListPtr(new ProteinListSimple);
    ProteinListPtr temp_b = b.proteinListPtr.get() ? b.proteinListPtr : ProteinListPtr(new ProteinListSimple);
    diff(*temp_a, *temp_b, *temp_a_b, *temp_b_a, config);

    // provide context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


} // namespace diff_impl
} // namespace data


namespace proteome {

std::ostream& os_write_proteins(std::ostream& os, const ProteinListPtr a_b, const ProteinListPtr b_a)
{
    TextWriter write(os, 1);

    if(a_b->size()!=b_a->size())
    {
        os<<"in ProteinList diff: ProteinList sizes differ"<<std::endl;
        return os;
    }
  
    for (size_t index = 0; index < a_b->size(); ++index)
    {
      os<<"+\n";
      write(*(a_b->protein(index)));
   
      os<<"-\n";
      write(*(b_a->protein(index)));
    }

    return os;
}


PWIZ_API_DECL
std::ostream& operator<<(std::ostream& os, const data::Diff<ProteomeData, DiffConfig>& diff)
{
    TextWriter write(os, 1);

    if(!diff.a_b.empty()|| !diff.b_a.empty())
    {
        os<<"+\n";
        write(diff.a_b,true);
        os<<"-\n";
        write(diff.b_a,true);

        os_write_proteins(os, diff.a_b.proteinListPtr, diff.b_a.proteinListPtr);
    }

    return os;
}


} // namespace proteome
} // namespace pwiz
