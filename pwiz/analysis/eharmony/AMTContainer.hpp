///
/// AMTContainer.hpp
///

#ifndef _AMT_CONTAINER_HPP_
#define _AMT_CONTAINER_HPP_

#include "Feature2PeptideMatcher.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"

namespace pwiz{
namespace eharmony{

struct AMTContainer
{
    string _id;
    bool rtAdjusted;

    Feature2PeptideMatcher _f2pm;
    PeptideMatcher _pm;

    FdfPtr _fdf;
    PidfPtr _pidf;

    vector<SpectrumQuery> _sqs; // HACK TODO: FIX

    void merge(const AMTContainer& that);

    void write(XMLWriter& writer) const;
    void read(istream& is);
  
    bool operator==(const AMTContainer& that);
    bool operator!=(const AMTContainer& that);


    AMTContainer(PidfPtr pidf = PidfPtr(new PeptideID_dataFetcher()), FdfPtr fdf = FdfPtr(new Feature_dataFetcher())) : _id(""), rtAdjusted(false), _fdf(fdf), _pidf(pidf) {}

};


} // namespace eharmony
} // namespace pwiz



#endif // AMTContainer.hpp
