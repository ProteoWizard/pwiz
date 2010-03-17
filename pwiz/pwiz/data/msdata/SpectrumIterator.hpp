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


#ifndef _SPECTRUMITERATOR_HPP_
#define _SPECTRUMITERATOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"


namespace pwiz {
namespace msdata {


using pwiz::util::IntegerSet;


///
/// SpectrumIterator provides convenient iteration through a set of scans in a SpectrumList.
///
/// Its behavior is similar to istream_iterator.  In particular:
/// - the default constructed SpectrumIterator() is a past-the-end marker
/// - references to the current Spectrum are invalidated by preincrement
///
/// Because SpectrumIterator holds a copy of the current Spectrum internally,
/// copy assignment and postincrement have been disabled.
/// 
/// Iteration may be customized in a number of ways:
/// - clients may specify an IntegerSet of scan numbers through which to iterate. 
/// - clients may specify a Sieve to filter based on Spectrum fields. 
/// - clients may specify whether binary data is retrieved in the Spectrum object (default==true)
///
/// For usage examples, see SpectrumIteratorTest.cpp
///
class PWIZ_API_DECL SpectrumIterator
{
    public:

    /// interface for filtering based on ScanInfo
    class PWIZ_API_DECL Sieve
    {
        public:
        virtual bool accept(const Spectrum& spectrum) const {return true;}
        virtual ~Sieve(){}
    };

    /// SpectrumIterator configuration -- note that constructors allow automatic 
    /// conversion from IntegerSet or Sieve to Config
    struct PWIZ_API_DECL Config
    {
        const IntegerSet* scanNumbers;
        const Sieve* sieve;
        bool getBinaryData;

        Config()
        :   scanNumbers(0), sieve(0), getBinaryData(true)
        {}

        Config(const IntegerSet& _scanNumbers, bool _getBinaryData = true)
        :   scanNumbers(&_scanNumbers), sieve(0), getBinaryData(_getBinaryData)
        {}
        
        Config(const Sieve& _sieve, bool _getBinaryData = true)
        :   scanNumbers(0), sieve(&_sieve), getBinaryData(_getBinaryData)
        {}
    };

    /// special default object for marking past-the-end 
    SpectrumIterator();

    /// constructor for normal initialization of the iterator
    SpectrumIterator(const SpectrumList& spectrumList,
                     const Config& config = Config());

    /// constructor using MSData object 
    SpectrumIterator(const MSData& msd,
                     const Config& config = Config());

    /// copy constructor
    SpectrumIterator(const SpectrumIterator&);

    /// \name input iterator interface
    //@{
    SpectrumIterator& operator++();
    const Spectrum& operator*() const;
    const Spectrum* operator->() const;
    bool operator==(const SpectrumIterator& that) const;
    bool operator!=(const SpectrumIterator& that) const;
    //@}

    /// \name standard iterator typedefs 
    //@{
    typedef std::input_iterator_tag iterator_category;
    typedef Spectrum value_type;
    typedef int difference_type;
    typedef value_type* pointer;
    typedef value_type& reference;
    //@}

    private:

    class Impl;
    boost::shared_ptr<Impl> impl_;

    /// no copying
    SpectrumIterator& operator=(const SpectrumIterator&);

    /// don't do this -- avoid temporary copy 
    SpectrumIterator operator++(int); 
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMITERATOR_HPP_

