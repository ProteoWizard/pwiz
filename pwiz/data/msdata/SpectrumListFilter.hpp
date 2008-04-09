//
// SpectrumListFilter.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#ifndef _SPECTRUMLISTFILTER_HPP_
#define _SPECTRUMLISTFILTER_HPP_


#include "MSData.hpp"


namespace pwiz {
namespace msdata {


class SpectrumListFilter : public SpectrumList
{
    public:

    struct Predicate
    {
        virtual bool accept(const Spectrum& spectrum) const = 0; 
    };

    SpectrumListFilter(const SpectrumListPtr original, const Predicate& predicate);

    /// \name SpectrumList interface
    //@{
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    //@}

    private:
    const SpectrumListPtr original_;
    std::vector<size_t> indexMap_;
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMLISTFILTER_HPP_

