//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2020 Matt Chambers
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

#include "SpectrumListBase.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include <boost/thread/lock_guard.hpp>
#include <boost/thread/mutex.hpp>
#include <boost/functional/hash.hpp>
#include <algorithm>
#include <numeric>


namespace {
    boost::mutex m;

// A spectrum needs more peaks than this before finding it in m/z order is taken as evidence
// about the writer rather than as coincidence.
const size_t MIN_PEAK_COUNT_FOR_MZ_SORT_CHECK = 10;


/// One array gathered into the given permutation of peak indexes, ready to be swapped in.
template <typename T>
std::vector<T> permuted(const std::vector<size_t>& order, const pwiz::util::BinaryData<T>& data)
{
    std::vector<T> result(order.size());
    for (size_t i = 0; i < order.size(); ++i)
        result[i] = data[order[i]];
    return result;
}

} // namespace

/// Whether the peaks run along some axis other than m/z, in which case they must be left exactly
/// as they are and say nothing about the writer.
///
/// Three ways that happens. The mobility axis of a combined ion mobility scan, or a scanning
/// quadrupole position: m/z then ascends only within each block, and a global sort would destroy
/// the structure rather than repair it. The x-axis is not m/z at all - Spectrum::getMZArray()
/// also returns a wavelength array, which the Agilent, Thermo and Bruker readers use for a
/// diode-array trace, and judging a UV trace as if it were m/z would let it settle the verdict for
/// every real spectrum in the file. Or each point is a transition rather than a peak - an SRM,
/// SIM or CRM spectrum lists one point per transition in the order the method defined them, which is the
/// order that matters, and the x-axis values are just the transitions' target m/z, not a scan
/// across a continuum; nothing about that order is wrong, so there is nothing to repair.
///
/// Asked by name, not by counting arrays: counting cannot tell an ordering axis from an ordinary
/// per-peak extra like signal-to-noise, and would refuse to repair any spectrum carrying one.
/// hasCVParamChild covers the whole ion mobility family and, like hasCVParam, looks into
/// referenceableParamGroups - mzML writers commonly factor the repeated binaryDataArray terms out
/// into one, where a plain scan of cvParams would miss them.
///
/// Exported (not file-local) because the same question - does this spectrum's peak order mean
/// anything - is asked again outside this file, by tests checking that a round trip preserved
/// ascending m/z order everywhere it is expected to hold.
PWIZ_API_DECL bool pwiz::msdata::hasNonMzOrderingAxis(const Spectrum& spectrum)
{
    using namespace pwiz::cv;

    // the ion mobility term is asked for with its children, which covers the whole family - mean,
    // raw, inverse reduced, deconvoluted; getArrayByCVID looks into referenceableParamGroups too,
    // where mzML writers commonly factor out the repeated binaryDataArray terms
    return spectrum.getArrayByCVID(MS_ion_mobility_array, true).get() != NULL ||
           spectrum.getArrayByCVID(MS_scanning_quadrupole_position_lower_bound_m_z_array).get() != NULL ||
           spectrum.getArrayByCVID(MS_scanning_quadrupole_position_upper_bound_m_z_array).get() != NULL ||
           spectrum.getArrayByCVID(MS_wavelength_array).get() != NULL ||
           spectrum.hasCVParam(MS_SRM_spectrum) ||
           spectrum.hasCVParam(MS_SIM_spectrum) ||
           spectrum.hasCVParam(MS_CRM_spectrum);
}

PWIZ_API_DECL void pwiz::msdata::ListBase::warn_once(const char * msg) const
{
    boost::lock_guard<boost::mutex> g(m);
    if (warn_msg_hashes_.insert(hash(msg)).second) // .second is true iff value is new
        cerr << msg << std::endl;
}


PWIZ_API_DECL void pwiz::msdata::SpectrumListBase::ensureMzAscending(const SpectrumPtr& spectrum) const
{
    if (!spectrum.get() || // Empty
        mzOrderVerdict_.load() == MzOrderVerdict::writerSortsByMz) // Already established this file is fine
        return;

    // Nothing to reorder, and nothing to learn, until the binary data is actually here. Tested
    // first because a metadata-only read still carries the array objects with their cvParams - IO
    // builds those and skips only the base64 decode - so without this every metadata pass would pay
    // for the scans below and, having no data, could never settle the verdict to stop paying.
    // Sizes come off the arrays rather than from defaultArrayLength, which is only guaranteed from
    // DetailLevel_FullMetadata up.
    bool hasPeakData = false;
    for (size_t i = 0; i < spectrum->binaryDataArrayPtrs.size() && !hasPeakData; ++i)
        hasPeakData = spectrum->binaryDataArrayPtrs[i].get() &&
                      spectrum->binaryDataArrayPtrs[i]->data.size() >= 2;
    if (!hasPeakData)
        return;

    if (hasNonMzOrderingAxis(*spectrum))
        return;

    BinaryDataArrayPtr mzArray = spectrum->getMZArray();
    BinaryDataArrayPtr intensityArray = spectrum->getIntensityArray();
    if (!mzArray.get() || !intensityArray.get())
        return;

    auto& mzs = mzArray->data;
    if (mzs.size() != intensityArray->data.size() || // Sanity check, flagged elsewhere if wrong
        mzs.size() < 2) // Indeterminate sortedness
        return;

    if (std::is_sorted(mzs.begin(), mzs.end()))
    {
        // Seems fine - but a short list can be in order by chance, so it does not settle anything
        MzOrderVerdict expected = MzOrderVerdict::unsettled;
        if (mzs.size() > MIN_PEAK_COUNT_FOR_MZ_SORT_CHECK)
            mzOrderVerdict_.compare_exchange_strong(expected, MzOrderVerdict::writerSortsByMz);
        return;
    }

    // One spectrum out of order means any others may also be out of order.
    // The exchange also tells us whether this is the first such spectrum, which the warning below
    // is keyed on.
    bool isFirstFoundSpectrumOutOfOrder =
        mzOrderVerdict_.exchange(MzOrderVerdict::writerDoesNotSortByMz) != MzOrderVerdict::writerDoesNotSortByMz;

    // A spectrum may carry other values like signal-to-noise, baseline, resolution or charge array
    // alongside m/z and intensity. Make sure those other arrays are permuted in the same way as m/z
    // and intensity.
    // Stable, so peaks sharing an m/z keep the order the writer gave them.
    std::vector<size_t> order(mzs.size());
    std::iota(order.begin(), order.end(), size_t(0));
    std::stable_sort(order.begin(), order.end(),
                     [&mzs](size_t a, size_t b) { return mzs[a] < mzs[b]; });

    // Every array is gathered first and only swapped in once they all succeeded. Doing it one array
    // at a time would let a throw part way through leave m/z sorted against unsorted intensities -
    // every value plausible and every pairing wrong - and nothing could detect it afterwards, since
    // the m/z axis would by then ascend and this function would take the "seems fine" branch
    // forever. The swap itself cannot throw.
    std::vector<std::pair<pwiz::util::BinaryData<double>*, std::vector<double> > > doubleArrays;
    for (size_t i = 0; i < spectrum->binaryDataArrayPtrs.size(); ++i)
        if (spectrum->binaryDataArrayPtrs[i].get() &&
            spectrum->binaryDataArrayPtrs[i]->data.size() == order.size())
            doubleArrays.push_back(std::make_pair(&spectrum->binaryDataArrayPtrs[i]->data,
                                                 permuted(order, spectrum->binaryDataArrayPtrs[i]->data)));

    typedef IntegerDataArray::value_type IntegerValue;
    std::vector<std::pair<pwiz::util::BinaryData<IntegerValue>*, std::vector<IntegerValue> > > integerArrays;
    for (size_t i = 0; i < spectrum->integerDataArrayPtrs.size(); ++i)
        if (spectrum->integerDataArrayPtrs[i].get() &&
            spectrum->integerDataArrayPtrs[i]->data.size() == order.size())
            integerArrays.push_back(std::make_pair(&spectrum->integerDataArrayPtrs[i]->data,
                                                  permuted(order, spectrum->integerDataArrayPtrs[i]->data)));

    for (size_t i = 0; i < doubleArrays.size(); ++i)
        doubleArrays[i].first->swap(doubleArrays[i].second);
    for (size_t i = 0; i < integerArrays.size(); ++i)
        integerArrays[i].first->swap(integerArrays[i].second);

    if (isFirstFoundSpectrumOutOfOrder)
        warn_once(("[SpectrumListBase] peaks were not written in ascending m/z order (first seen at \"" +
                   spectrum->id + "\"). Reordering them in memory before use.").c_str());
}


PWIZ_API_DECL size_t pwiz::msdata::SpectrumListBase::checkNativeIdFindResult(size_t result, const std::string& id) const
{
    if (result < size() || size() == 0)
        return result;

    if (id.empty())
        return size();

    try
    {
        const auto& firstId = spectrumIdentity(0).id;

        bool triedToFindScanByIndex = bal::starts_with(firstId, "scan=") && bal::starts_with(id, "index=");
        bool triedToFindIndexByScan = bal::starts_with(firstId, "index=") && bal::starts_with(id, "scan=");

        // HACK: special behavior if actual ids are scan/index and searched ids are index/scan (respectively)
        if (triedToFindScanByIndex)
            return find("scan=" + pwiz::util::toString(lexical_cast<int>(pwiz::msdata::id::value(id, "index")) + 1));
        else if (triedToFindIndexByScan)
            return find("index=" + pwiz::util::toString(lexical_cast<int>(pwiz::msdata::id::value(id, "scan")) - 1));
        else
        {
            boost::lock_guard<boost::mutex> g(m);

            // early exit if warning already issued, to avoid potentially doing these calculations for thousands of ids
            if (!impl_.warn_msg_hashes().insert(spectrum_id_mismatch_hash_).second)
                return size();
        }

        if (!checkNativeIdMatch(firstId, id))
            warn_once(("[SpectrumList::find] mismatch between spectrum id format of the file (" + firstId + ") and the looked-up id (" + id + ")").c_str());
        return size();
    }
    catch (std::exception& e)
    {
        warn_once((std::string("[SpectrumList::find] error checking for spectrum id conformance: ") + e.what()).c_str()); // TODO: log exception
        return size();
    }
}

size_t pwiz::msdata::ListBase::hash(const char* msg) const
{
    return boost::hash_range(msg, msg + strlen(msg));
}
