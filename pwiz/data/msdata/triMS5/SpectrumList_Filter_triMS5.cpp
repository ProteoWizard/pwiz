//
// $Id$
//
//
// Original author: Jennifer Leclaire <leclaire@uni-mainz.de>
//
// Copyright 2019 Institute of Computer Science, Johannes Gutenberg-Universität Mainz
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

#include "pwiz/data/common/cv.hpp"
#include "SpectrumList_Filter_triMS5.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
	namespace msdata {
		namespace triMS5 {


		using namespace pwiz::cv;
		using namespace pwiz::util;
		using namespace pwiz::msdata;

		using boost::logic::tribool;


		//
		// SpectrumList_Filter::Impl
		//


		struct SpectrumList_Filter_triMS5::Impl
		{
			const SpectrumListPtr original;
			std::vector<SpectrumIdentity> spectrumIdentities; // local cache, with fixed up index fields
			std::vector<size_t> indexMap; // maps index -> original index
			DetailLevel detailLevel; // the detail level needed for a non-indeterminate result

			Impl(SpectrumListPtr original, const Predicate& predicate);
			void pushSpectrum(const SpectrumIdentity& spectrumIdentity);
		};


		SpectrumList_Filter_triMS5::Impl::Impl(SpectrumListPtr _original, const Predicate& predicate)
			: original(_original), detailLevel(predicate.suggestedDetailLevel())
		{
			if (!original.get()) throw runtime_error("[SpectrumList_Filter] Null pointer");

			// iterate through the spectra, using predicate to build the sub-list
			for (size_t i = 0, end = original->size(); i < end; i++)
			{
				if (predicate.done()) break;

				// first try to determine acceptance based on SpectrumIdentity alone
				const SpectrumIdentity& spectrumIdentity = original->spectrumIdentity(i);
				tribool accepted = predicate.accept(spectrumIdentity);

				if (accepted)
				{
					pushSpectrum(spectrumIdentity);
				}
				else if (!accepted)
				{
					// do nothing 
				}
				else // indeterminate
				{
					// not enough info -- we need to retrieve the Spectrum
					do
					{
						SpectrumPtr spectrum = original->spectrum(i, detailLevel);
						accepted = predicate.accept(*spectrum);

						if (boost::logic::indeterminate(accepted) && (int)detailLevel < (int)DetailLevel_FullMetadata)
							detailLevel = DetailLevel(int(detailLevel) + 1);
						else
						{
							if (accepted)
								pushSpectrum(spectrumIdentity);
							break;
						}
					} while ((int)detailLevel <= (int)DetailLevel_FullMetadata);
				}
			}
		}


		void SpectrumList_Filter_triMS5::Impl::pushSpectrum(const SpectrumIdentity& spectrumIdentity)
		{
			indexMap.push_back(spectrumIdentity.index);
			spectrumIdentities.push_back(spectrumIdentity);
			spectrumIdentities.back().index = spectrumIdentities.size() - 1;
		}


		//
		// SpectrumList_Filter
		//


		PWIZ_API_DECL SpectrumList_Filter_triMS5::SpectrumList_Filter_triMS5(const SpectrumListPtr original, const Predicate& predicate)
			: SpectrumListWrapper(original), impl_(new Impl(original, predicate))
		{}


		PWIZ_API_DECL size_t SpectrumList_Filter_triMS5::size() const
		{
			return impl_->indexMap.size();
		}


		PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Filter_triMS5::spectrumIdentity(size_t index) const
		{
			return impl_->spectrumIdentities.at(index);
		}


		PWIZ_API_DECL SpectrumPtr SpectrumList_Filter_triMS5::spectrum(size_t index, bool getBinaryData) const
		{
			return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
		}


		PWIZ_API_DECL SpectrumPtr SpectrumList_Filter_triMS5::spectrum(size_t index, DetailLevel detailLevel) const
		{
			size_t originalIndex = impl_->indexMap.at(index);
			SpectrumPtr originalSpectrum = impl_->original->spectrum(originalIndex, detailLevel);

			SpectrumPtr newSpectrum(new Spectrum(*originalSpectrum));
			newSpectrum->index = index;

			return newSpectrum;
		}


		//
		// SpectrumList_Filter_triMS5Predicate_IndexSet 
		//


		PWIZ_API_DECL SpectrumList_FilterPredicate_IndexSet_triMS5::SpectrumList_FilterPredicate_IndexSet_triMS5(const IntegerSet& indexSet)
			: indexSet_(indexSet), eos_(false)
		{}


		PWIZ_API_DECL tribool SpectrumList_FilterPredicate_IndexSet_triMS5::accept(const SpectrumIdentity& spectrumIdentity) const
		{
			if (indexSet_.hasUpperBound((int)spectrumIdentity.index)) eos_ = true;
			bool result = indexSet_.contains((int)spectrumIdentity.index);
			return result;
		}


		PWIZ_API_DECL bool SpectrumList_FilterPredicate_IndexSet_triMS5::done() const
		{
			return eos_; // end of set
		}




		//
		// SpectrumList_FilterPredicate_ScanEventSet_triMS5
		//


		PWIZ_API_DECL SpectrumList_FilterPredicate_ScanEventSet_triMS5::SpectrumList_FilterPredicate_ScanEventSet_triMS5(const IntegerSet& scanEventSet)
			: scanEventSet_(scanEventSet)
		{}


		PWIZ_API_DECL boost::logic::tribool SpectrumList_FilterPredicate_ScanEventSet_triMS5::accept(const msdata::Spectrum& spectrum) const
		{
			Scan dummy;
			const Scan& scan = spectrum.scanList.scans.empty() ? dummy : spectrum.scanList.scans[0]; //check scan


			CVParam param = scan.cvParam(MS_preset_scan_configuration); //look for preset scan configuration

			//if the MS_preset_scan_configuration wasn't found 
			if (param.cvid == CVID_Unknown) return boost::logic::indeterminate;


			int scanEvent = lexical_cast<int>(param.value);
			bool result = scanEventSet_.contains(scanEvent);
			return result;
		}


	} // namespace triMS5

} // namespace analysis
} // namespace pwiz

