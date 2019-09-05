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


#ifndef _SPECTRUMLIST_FILTER_HPP_
#define _SPECTRUMLIST_FILTER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "boost/logic/tribool.hpp"

#include <set>

namespace pwiz {
	namespace msdata {
		namespace triMS5 {


			/// SpectrumList filter, for creating Spectrum sub-lists
			class PWIZ_API_DECL SpectrumList_Filter_triMS5 : public msdata::SpectrumListWrapper
			{
			public:

				/// client-implemented filter predicate -- called during construction of
				/// SpectrumList_Filter to create the filtered list of spectra
				struct PWIZ_API_DECL Predicate
				{
					/// controls whether spectra that pass the predicate are included or excluded from the result
					enum FilterMode
					{
						FilterMode_Include,
						FilterMode_Exclude
					};

					/// can be overridden in subclasses that know they will need a certain detail level;
					/// it must be overridden to return DetailLevel_FullData if binary data is needed
					virtual msdata::DetailLevel suggestedDetailLevel() const { return msdata::DetailLevel_InstantMetadata; }

					/// return values:
					///  true: accept the Spectrum
					///  false: reject the Spectrum
					///  indeterminate: need to see the full Spectrum object to decide
					virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const = 0;

					/// return true iff Spectrum is accepted
					virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const { return false; }

					/// return true iff done accepting spectra; 
					/// this allows early termination of the iteration through the original
					/// SpectrumList, possibly using assumptions about the order of the
					/// iteration (e.g. index is increasing, nativeID interpreted as scan number is
					/// increasing, ...)
					virtual bool done() const { return false; }

					virtual ~Predicate() {}
				};

				SpectrumList_Filter_triMS5(const msdata::SpectrumListPtr original, const Predicate& predicate);

				/// \name SpectrumList interface
				//@{
				virtual size_t size() const;
				virtual const msdata::SpectrumIdentity& spectrumIdentity(size_t index) const;
				virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
				virtual msdata::SpectrumPtr spectrum(size_t index, msdata::DetailLevel detailLevel) const;
				//@}

			private:
				struct Impl;
				boost::shared_ptr<Impl> impl_;
				SpectrumList_Filter_triMS5(SpectrumList_Filter_triMS5&);
				SpectrumList_Filter_triMS5& operator=(SpectrumList_Filter_triMS5&);
			};


			//PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const SpectrumList_Filter_triMS5::Predicate::FilterMode& mode);
			//PWIZ_API_DECL std::istream& operator >> (std::istream& is, SpectrumList_Filter_triMS5::Predicate::FilterMode& mode);

			class PWIZ_API_DECL SpectrumList_FilterPredicate_IndexSet_triMS5 : public SpectrumList_Filter_triMS5::Predicate
			{
			public:
				SpectrumList_FilterPredicate_IndexSet_triMS5(const util::IntegerSet& indexSet);
				virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const;
				virtual bool done() const;

			private:
				util::IntegerSet indexSet_;
				mutable bool eos_;
			};


			class PWIZ_API_DECL SpectrumList_FilterPredicate_ScanEventSet_triMS5 : public SpectrumList_Filter_triMS5::Predicate
			{
			public:
				SpectrumList_FilterPredicate_ScanEventSet_triMS5(const util::IntegerSet& scanEventSet);
				virtual boost::logic::tribool accept(const msdata::SpectrumIdentity& spectrumIdentity) const { return boost::logic::indeterminate; }
				virtual boost::logic::tribool accept(const msdata::Spectrum& spectrum) const;

			private:
				util::IntegerSet scanEventSet_;
			};

		} // namespace triMS5
	} // namespace msdata
} // namespace pwiz


#endif // _SpectrumList_Filter_triMS5_triMS5_HPP_

