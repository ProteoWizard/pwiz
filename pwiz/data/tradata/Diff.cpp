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


#define PWIZ_SOURCE

#include "Diff.hpp"
#include <string>
#include <cmath>
#include <stdexcept>

namespace pwiz {
namespace tradata {
namespace diff_impl {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;


PWIZ_API_DECL
void diff(const string& a, 
          const string& b, 
          string& a_b, 
          string& b_a,
          const DiffConfig& config)
{
    a_b.clear();
    b_a.clear();
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}


template <typename T>
void diff_numeric(const T& a, 
                  const T& b, 
                  T& a_b, 
                  T& b_a,
                  const DiffConfig& config)
{
    a_b = 0;
    b_a = 0;
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}


template <>
void diff_numeric(const double& a,
                  const double& b,
                  double& a_b,
                  double& b_a,
                  const DiffConfig& config)
{
    a_b = 0;
    b_a = 0;

    if (fabs(a - b) > config.precision + std::numeric_limits<double>::epsilon())
    {
        a_b = fabs(a - b);
        b_a = fabs(a - b);
    }
}


PWIZ_API_DECL
void diff(const CV& a, 
          const CV& b, 
          CV& a_b, 
          CV& b_a,
          const DiffConfig& config)
{
    diff(a.URI, b.URI, a_b.URI, b_a.URI, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.fullName, b.fullName, a_b.fullName, b_a.fullName, config);
    diff(a.version, b.version, a_b.version, b_a.version, config);
}


PWIZ_API_DECL
void diff(CVID a,
          CVID b,
          CVID& a_b,
          CVID& b_a,
          const DiffConfig& config)
{
    a_b = b_a = CVID_Unknown;
    if (a!=b)  
    {
        a_b = a;
        b_a = b;
    }
}


PWIZ_API_DECL
void diff(const CVParam& a, 
          const CVParam& b, 
          CVParam& a_b, 
          CVParam& b_a,
          const DiffConfig& config)
{
    diff(a.cvid, b.cvid, a_b.cvid, b_a.cvid, config);

    // use precision to compare floating point values
    try
    {
        lexical_cast<int>(a.value);
        lexical_cast<int>(b.value);
    }
    catch (boost::bad_lexical_cast&)
    {
        try
        {
            double aValue = lexical_cast<double>(a.value);
            double bValue = lexical_cast<double>(b.value);
            double a_bValue, b_aValue;
            diff_numeric<double>(aValue, bValue, a_bValue, b_aValue, config);
            a_b.value = lexical_cast<string>(a_bValue);
            b_a.value = lexical_cast<string>(b_aValue);
        }
        catch (boost::bad_lexical_cast&)
        {
            diff(a.value, b.value, a_b.value, b_a.value, config);
        }
    }

    diff(a.units, b.units, a_b.units, b_a.units, config);

    // provide names for context
    if (!a_b.empty() && a_b.cvid==CVID_Unknown) a_b.cvid = a.cvid; 
    if (!b_a.empty() && b_a.cvid==CVID_Unknown) b_a.cvid = b.cvid; 
}


PWIZ_API_DECL
void diff(const UserParam& a, 
          const UserParam& b, 
          UserParam& a_b, 
          UserParam& b_a,
          const DiffConfig& config)
{
    diff(a.name, b.name, a_b.name, b_a.name, config);
    diff(a.value, b.value, a_b.value, b_a.value, config);
    diff(a.type, b.type, a_b.type, b_a.type, config);
    diff(a.units, b.units, a_b.units, b_a.units, config);

    // provide names for context
    if (!a_b.empty() && a_b.name.empty()) a_b.name = a.name; 
    if (!b_a.empty() && b_a.name.empty()) b_a.name = b.name; 
}


template <typename object_type>
void vector_diff(const vector<object_type>& a,
                 const vector<object_type>& b,
                 vector<object_type>& a_b,
                 vector<object_type>& b_a)
{
    // calculate set differences of two vectors

    a_b.clear();
    b_a.clear();

    for (typename vector<object_type>::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (find(b.begin(), b.end(), *it) == b.end())
            a_b.push_back(*it);

    for (typename vector<object_type>::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (find(a.begin(), a.end(), *it) == a.end())
            b_a.push_back(*it);
}


template <typename object_type>
struct HasID
{
    const string& id_;
    HasID(const string& id) : id_(id) {}
    bool operator()(const shared_ptr<object_type>& objectPtr) {return objectPtr->id == id_;}
};


template <typename object_type>
class Same
{
    public:

    Same(const object_type& object,
         const DiffConfig& config)
    :   mine_(object), config_(config)
    {}

    bool operator()(const object_type& yours)
    {
        // true iff yours is the same as mine
        return !Diff<object_type>(mine_, yours, config_);
    }

    private:
    const object_type& mine_;
    const DiffConfig& config_;
};


template <typename object_type>
void vector_diff_diff(const vector<object_type>& a,
                      const vector<object_type>& b,
                      vector<object_type>& a_b,
                      vector<object_type>& b_a,
                      const DiffConfig& config)
{
    // calculate set differences of two vectors, using diff on each object

    a_b.clear();
    b_a.clear();

    for (typename vector<object_type>::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (find_if(b.begin(), b.end(), Same<object_type>(*it, config)) == b.end())
            a_b.push_back(*it);

    for (typename vector<object_type>::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (find_if(a.begin(), a.end(), Same<object_type>(*it, config)) == a.end())
            b_a.push_back(*it);
}


template <typename object_type>
class SameDeep
{
    public:

    SameDeep(const object_type& object,
             const DiffConfig& config)
    :   mine_(object), config_(config)
    {}

    bool operator()(const shared_ptr<object_type>& yours)
    {
        // true iff yours is the same as mine
        return !Diff<object_type>(mine_, *yours, config_);
    }

    private:
    const object_type& mine_;
    const DiffConfig& config_;
};


template <typename object_type>
void vector_diff_deep(const vector< shared_ptr<object_type> >& a,
                      const vector< shared_ptr<object_type> >& b,
                      vector< shared_ptr<object_type> >& a_b,
                      vector< shared_ptr<object_type> >& b_a,
                      const DiffConfig& config)
{
    // calculate set differences of two vectors of ObjectPtrs (deep compare using diff)

    a_b.clear();
    b_a.clear();

    for (typename vector< shared_ptr<object_type> >::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (find_if(b.begin(), b.end(), SameDeep<object_type>(**it, config)) == b.end())
            a_b.push_back(*it);

    for (typename vector< shared_ptr<object_type> >::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (find_if(a.begin(), a.end(), SameDeep<object_type>(**it, config)) == a.end())
            b_a.push_back(*it);
}


template <typename object_type>
void ptr_diff(const shared_ptr<object_type>& a,
              const shared_ptr<object_type>& b,
              shared_ptr<object_type>& a_b,
              shared_ptr<object_type>& b_a,
              const DiffConfig& config)
{
    if (!a.get() && !b.get()) return;

    shared_ptr<object_type> a_temp = a.get() ? a : shared_ptr<object_type>(new object_type);
    shared_ptr<object_type> b_temp = b.get() ? b : shared_ptr<object_type>(new object_type);

    if (!a_b.get()) a_b = shared_ptr<object_type>(new object_type);
    if (!b_a.get()) b_a = shared_ptr<object_type>(new object_type);
    diff(*a_temp, *b_temp, *a_b, *b_a, config);

    if (a_b->empty()) a_b = shared_ptr<object_type>();
    if (b_a->empty()) b_a = shared_ptr<object_type>();
}


PWIZ_API_DECL
void diff(const ParamContainer& a,
          const ParamContainer& b,
          ParamContainer& a_b,
          ParamContainer& b_a,
          const DiffConfig& config)
{
    vector_diff(a.cvParams, b.cvParams, a_b.cvParams, b_a.cvParams);
    vector_diff(a.userParams, b.userParams, a_b.userParams, b_a.userParams);
}


PWIZ_API_DECL
void diff(const Contact& a,
          const Contact& b,
          Contact& a_b,
          Contact& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Publication& a,
          const Publication& b,
          Publication& a_b,
          Publication& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Instrument& a,
          const Instrument& b,
          Instrument& a_b,
          Instrument& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Software& a,
          const Software& b,
          Software& a_b,
          Software& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.version, b.version, a_b.version, b_a.version, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Protein& a,
          const Protein& b,
          Protein& a_b,
          Protein& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.sequence, b.sequence, a_b.sequence, b_a.sequence, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const RetentionTime& a,
          const RetentionTime& b,
          RetentionTime& a_b,
          RetentionTime& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    ptr_diff(a.softwarePtr, b.softwarePtr, a_b.softwarePtr, b_a.softwarePtr, config);
}


PWIZ_API_DECL
void diff(const Evidence& a,
          const Evidence& b,
          Evidence& a_b,
          Evidence& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const Modification& a,
          const Modification& b,
          Modification& a_b,
          Modification& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff_numeric(a.location, b.location, a_b.location, b_a.location, config);
    diff_numeric(a.monoisotopicMassDelta, b.monoisotopicMassDelta, a_b.monoisotopicMassDelta, b_a.monoisotopicMassDelta, config);
    diff_numeric(a.averageMassDelta, b.averageMassDelta, a_b.averageMassDelta, b_a.averageMassDelta, config);
}


PWIZ_API_DECL
void diff(const Peptide& a,
          const Peptide& b,
          Peptide& a_b,
          Peptide& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.sequence, b.sequence, a_b.sequence, b_a.sequence, config);
    vector_diff_deep(a.proteinPtrs, b.proteinPtrs, a_b.proteinPtrs, b_a.proteinPtrs, config);
    vector_diff_diff(a.modifications, b.modifications, a_b.modifications, b_a.modifications, config);
    vector_diff_diff(a.retentionTimes, b.retentionTimes, a_b.retentionTimes, b_a.retentionTimes, config);
    diff(a.evidence, b.evidence, a_b.evidence, b_a.evidence, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Compound& a,
          const Compound& b,
          Compound& a_b,
          Compound& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    vector_diff_diff(a.retentionTimes, b.retentionTimes, a_b.retentionTimes, b_a.retentionTimes, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Prediction& a,
          const Prediction& b,
          Prediction& a_b,
          Prediction& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    ptr_diff(a.contactPtr, b.contactPtr, a_b.contactPtr, b_a.contactPtr, config);
    ptr_diff(a.softwarePtr, b.softwarePtr, a_b.softwarePtr, b_a.softwarePtr, config);
}


PWIZ_API_DECL
void diff(const Validation& a,
          const Validation& b,
          Validation& a_b,
          Validation& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const Interpretation& a,
          const Interpretation& b,
          Interpretation& a_b,
          Interpretation& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
}


PWIZ_API_DECL
void diff(const Configuration& a,
          const Configuration& b,
          Configuration& a_b,
          Configuration& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    ptr_diff(a.contactPtr, b.contactPtr, a_b.contactPtr, b_a.contactPtr, config);
    ptr_diff(a.instrumentPtr, b.instrumentPtr, a_b.instrumentPtr, b_a.instrumentPtr, config);
    vector_diff_diff(a.validations, b.validations, a_b.validations, b_a.validations, config);
}


PWIZ_API_DECL
void diff(const Transition& a,
          const Transition& b,
          Transition& a_b,
          Transition& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.precursor, b.precursor, a_b.precursor, b_a.precursor, config);
    diff(a.product, b.product, a_b.product, b_a.product, config);
    diff(a.prediction, b.prediction, a_b.prediction, b_a.prediction, config);
    ptr_diff(a.peptidePtr, b.peptidePtr, a_b.peptidePtr, b_a.peptidePtr, config);
    ptr_diff(a.compoundPtr, b.compoundPtr, a_b.compoundPtr, b_a.compoundPtr, config);
    vector_diff_diff(a.interpretationList, b.interpretationList, a_b.interpretationList, b_a.interpretationList, config);
    vector_diff_diff(a.configurationList, b.configurationList, a_b.configurationList, b_a.configurationList, config);

    // provide name for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const Target& a,
          const Target& b,
          Target& a_b,
          Target& b_a,
          const DiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.precursor, b.precursor, a_b.precursor, b_a.precursor, config);
    ptr_diff(a.peptidePtr, b.peptidePtr, a_b.peptidePtr, b_a.peptidePtr, config);
    ptr_diff(a.compoundPtr, b.compoundPtr, a_b.compoundPtr, b_a.compoundPtr, config);
    vector_diff_diff(a.configurationList, b.configurationList, a_b.configurationList, b_a.configurationList, config);

    // provide name for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


PWIZ_API_DECL
void diff(const TraData& a,
          const TraData& b,
          TraData& a_b,
          TraData& b_a,
          const DiffConfig& config)
{
    string a_b_version, b_a_version;

    diff(a.id, b.id, a_b.id, b_a.id, config);
    diff(a.version(), b.version(), a_b_version, b_a_version, config);
    vector_diff_diff(a.cvs, b.cvs, a_b.cvs, b_a.cvs, config);
    vector_diff_deep(a.contactPtrs, b.contactPtrs, a_b.contactPtrs, b_a.contactPtrs, config);
    vector_diff_diff(a.publications, b.publications, a_b.publications, b_a.publications, config);
    vector_diff_deep(a.instrumentPtrs, b.instrumentPtrs, a_b.instrumentPtrs, b_a.instrumentPtrs, config);
    vector_diff_deep(a.softwarePtrs, b.softwarePtrs, a_b.softwarePtrs, b_a.softwarePtrs, config);
    vector_diff_deep(a.proteinPtrs, b.proteinPtrs, a_b.proteinPtrs, b_a.proteinPtrs, config);
    vector_diff_deep(a.peptidePtrs, b.peptidePtrs, a_b.peptidePtrs, b_a.peptidePtrs, config);
    vector_diff_deep(a.compoundPtrs, b.compoundPtrs, a_b.compoundPtrs, b_a.compoundPtrs, config);
    vector_diff_diff(a.transitions, b.transitions, a_b.transitions, b_a.transitions, config);
    vector_diff_diff(a.targets.targetExcludeList, b.targets.targetExcludeList, a_b.targets.targetExcludeList, b_a.targets.targetExcludeList, config);
    vector_diff_diff(a.targets.targetIncludeList, b.targets.targetIncludeList, a_b.targets.targetIncludeList, b_a.targets.targetIncludeList, config);

    // provide context
    if (!a_b.empty() || !b_a.empty() ||
        !a_b_version.empty() || !b_a_version.empty()) 
    {
        a_b.id = a.id + (a_b_version.empty() ? "" : " (" + a_b_version + ")");
        b_a.id = b.id + (b_a_version.empty() ? "" : " (" + b_a_version + ")");
    }
}

} // namespace diff_impl


PWIZ_API_DECL
std::ostream& operator<<(std::ostream& os, const Diff<TraData>& diff)
{
  using namespace diff_impl;

  TextWriter write(os,1);

  if(!diff.a_b.empty() || !diff.b_a.empty())
  {
      os<<"+\n";
      write(diff.a_b);
      os<<"-\n";
      write(diff.b_a);
  }

    return os;

}

} // namespace tradata
} // namespace pwiz


