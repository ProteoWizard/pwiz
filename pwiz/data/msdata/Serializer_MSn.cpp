//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "Serializer_MSn.hpp"
#include "SpectrumList_MSn.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "boost/foreach.hpp"
#include "boost/algorithm/string/join.hpp"


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::iostreams::stream_offset;
using namespace pwiz::util;


class Serializer_MSn::Impl
{
    public:

    Impl()
    {}

    void write(ostream& os, const MSData& msd,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const;

    void read(shared_ptr<istream> is, MSData& msd) const;
};


void Serializer_MSn::Impl::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
{
    // write header lines
    bdt::local_time::local_time_facet* output_facet = new bdt::local_time::local_time_facet();
    stringstream creationDate;
    ss.imbue(locale(locale::classic(), output_facet));
    output_facet->format(bdt::default_date_format);
    os << "H\tCreationDate\t" << creationDate.str() << "\n";
    os << "H\tExtractor\tProteoWizard Serializer_MSn\n";
    os << "H\tExtractorVersion\t" << msdata::Version::str() << "\n";
    os << "H\tExtractorOptions\t" << endl;

    bool outputIsMS1 = msd.fileDescription.fileContent.hasCVParam(MS_MS1_spectrum);

    os << std::setprecision(10); // 1234.567890
    SpectrumList& sl = *msd.run.spectrumListPtr;

    if (outputIsMS1)
    {
    for (size_t i=0, end=sl.size(); i < end; ++i)
    {
        SpectrumPtr s = sl.spectrum(i, true);
        Scan& scan = s->scanList.scans[0];

        if (s->cvParam(MS_ms_level).value == "1")
        {
            const SelectedIon& si = s->precursors[0].selectedIons[0];
            CVParam scanTimeParam = scan.cvParam(MS_scan_start_time);
            CVParam chargeParam = si.cvParam(MS_charge_state);

            // MSn scan number takes a different form depending on the source's nativeID format
            string scanNumberStr = id::translateNativeIDToScanNumber(nativeIdFormat, spectrum.id);
            if (scanNumberStr.empty())
                os << "S\t" << spectrum.index+1 << '\t' << spectrum.index+1 << '\t' << si.cvParam(MS_selected_ion_m_z).value << '\n'; // scan number is a 1-based index for some nativeID formats
            else
                os << "S\t" << scanNumberStr << '\t' << scanNumberStr << '\t' << si.cvParam(MS_selected_ion_m_z).value << '\n';

            if (!scanTimeParam.empty())
                os << "I\tRTime" << scanTimeParam.timeInSeconds() << '\n';

            if (chargeParam.empty())
            {
                // at least one Z line is required, which if nothing else should be provided by SpectrumList_ChargeStateCalculator
                BOOST_FOREACH(const CVParam& param, si.cvParams)
                {
                    if (param.cvid == MS_possible_charge_state)
                        os << "Z\t" << param.value << '\t' <<
                              pwiz::proteome::Ion::ionMass(param.valueAs<double>(),
                                                           param.valueAs<int>()) << '\n';
                }
            } else
                os << "Z\t" << chargeParam.value << '\t' <<
                               pwiz::proteome::Ion::ionMass(chargeParam.valueAs<double>(),
                                                            chargeParam.valueAs<int>()) << '\n';

            const BinaryDataArray& mzArray = *s->getMZArray();
            const BinaryDataArray& intensityArray = *s->getIntensityArray();
            for (size_t p=0; p < s->defaultArrayLength; ++p)
                os << mzArray.data[p] << '\t' << intensityArray.data[p] << '\n';
        }

        // update any listeners and handle cancellation
        IterationListener::Status status = IterationListener::Status_Ok;

        if (iterationListenerRegistry)
            status = iterationListenerRegistry->broadcastUpdateMessage(
                IterationListener::UpdateMessage(i, end));

        if (status == IterationListener::Status_Cancel) 
            break;
    }
}


void Serializer_MSn::Impl::read(shared_ptr<istream> is, MSData& msd) const
{
    if (!is.get() || !*is)
        throw runtime_error("[Serializer_MSn::read()] Bad istream.");

    is->seekg(0);

    msd.fileDescription.fileContent.set(MS_MSn_spectrum);
    msd.fileDescription.fileContent.set(MS_centroid_spectrum);
    msd.run.spectrumListPtr = SpectrumList_MSn::create(is, msd);
    msd.run.chromatogramListPtr.reset(new ChromatogramListSimple);
}


//
// Serializer_MSn
//


PWIZ_API_DECL Serializer_MSn::Serializer_MSn()
:   impl_(new Impl())
{}


PWIZ_API_DECL void Serializer_MSn::write(ostream& os, const MSData& msd,
    const pwiz::util::IterationListenerRegistry* iterationListenerRegistry) const
  
{
    return impl_->write(os, msd, iterationListenerRegistry);
}


PWIZ_API_DECL void Serializer_MSn::read(shared_ptr<istream> is, MSData& msd) const
{
    return impl_->read(is, msd);
}


} // namespace msdata
} // namespace pwiz


