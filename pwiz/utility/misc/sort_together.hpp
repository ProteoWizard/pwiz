//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
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

#ifndef _SORT_TOGETHER_
#define _SORT_TOGETHER_

#include <vector>
#include <algorithm>

namespace pwiz {
namespace util {

    template<typename T>
    struct SortByOther
    {
        T begin_;

        SortByOther(T sortValuesBegin) :
            begin_(sortValuesBegin) {}

        bool operator()(int i1, int i2) const { return *(begin_ + i1) < *(begin_ + i2); }
    };

    /// The permutation that sorting sortValues would apply, without applying it.
    template<typename ContainerT>
    std::vector<size_t> sorted_indices(ContainerT& sortValues, bool stable = false)
    {
        size_t size = sortValues.size();
        std::vector<size_t> indices(size);
        for (size_t i = 0; i < size; ++i)
            indices[i] = i;

        if (stable)
            std::stable_sort(indices.begin(), indices.end(), SortByOther<typename ContainerT::iterator>(sortValues.begin()));
        else
            std::sort(indices.begin(), indices.end(), SortByOther<typename ContainerT::iterator>(sortValues.begin()));

        return indices;
    }

    template<typename ContainerT, typename ContainerOfContainerTIterator>
    void sort_together(ContainerT& sortValues, ContainerOfContainerTIterator cosortValuesItrRangeBegin, ContainerOfContainerTIterator cosortValuesItrRangeEnd, bool stable = false)
    {
        size_t size = sortValues.size();
        std::vector<size_t> indices = sorted_indices(sortValues, stable);

        ContainerT tmpSortValues(size);
        size_t numRanges = cosortValuesItrRangeEnd - cosortValuesItrRangeBegin;
        std::vector<ContainerT> tmpValuesRanges(numRanges);
        for (auto& tmpValues : tmpValuesRanges)
            tmpValues.resize(size);

        for (size_t i = 0; i < size; ++i)
        {
            tmpSortValues[i] = sortValues[indices[i]];
            for (size_t j = 0; j < numRanges; ++j)
            {
                auto& tmpValues = tmpValuesRanges[j];
                auto& cosortValuesItr = *(cosortValuesItrRangeBegin + j);
                tmpValues[i] = *(cosortValuesItr.begin() + indices[i]);
            }
        }
        std::swap(tmpSortValues, sortValues);
        for (size_t j = 0; j < numRanges; ++j)
        {
            auto& tmpCosortValues = *(cosortValuesItrRangeBegin + j);
            for (size_t i = 0; i < size; ++i)
                std::iter_swap(tmpValuesRanges[j].begin() + i, tmpCosortValues.begin() + i);
        }
    }

    template<typename ContainerT, typename ContainerOfContainerT>
    void sort_together(ContainerT& sortValues, ContainerOfContainerT cosortValuesItrRange, bool stable = false)
    {
        sort_together(sortValues, std::begin(cosortValuesItrRange), std::end(cosortValuesItrRange), stable);
    }

    template<typename ContainerT>
    void sort_together(ContainerT& sortValues, ContainerT& cosortValues, bool stable = false)
    {
        sort_together(sortValues, std::vector<boost::iterator_range<typename ContainerT::iterator>> { cosortValues }, stable);
    }

    /// Co-sorted ranges gathered under a permutation and held until swapped in, so that several sets
    /// of differing element type can all be gathered before any of them is modified.
    template<typename RangeIterator>
    class permuted_copies
    {
        public:
        typedef typename std::iterator_traits<decltype(std::begin(*std::declval<RangeIterator&>()))>::value_type value_type;

        permuted_copies(const std::vector<size_t>& indices, RangeIterator begin, RangeIterator end)
            : begin_(begin), tmp_(static_cast<size_t>(end - begin), std::vector<value_type>(indices.size()))
        {
            for (size_t j = 0; j < tmp_.size(); ++j)
            {
                auto& range = *(begin_ + j);
                for (size_t i = 0; i < indices.size(); ++i)
                    tmp_[j][i] = *(std::begin(range) + indices[i]);
            }
        }

        void swap_in()
        {
            for (size_t j = 0; j < tmp_.size(); ++j)
            {
                auto& range = *(begin_ + j);
                for (size_t i = 0; i < tmp_[j].size(); ++i)
                    std::iter_swap(tmp_[j].begin() + i, std::begin(range) + i);
            }
        }

        private:
        RangeIterator begin_;
        std::vector<std::vector<value_type>> tmp_;
    };

    /// Sorts by sortValues, carrying along two sets of co-sorted ranges whose element types differ -
    /// a spectrum's double arrays and its integer arrays, say, which are separate members and so
    /// cannot be passed as one range set.
    ///
    /// Every range is gathered before any is written back, so a throw part way through cannot leave
    /// one array permuted against another - values all plausible, every pairing wrong, and nothing
    /// downstream able to detect it.
    template<typename ContainerT, typename ContainerOfContainerT, typename ContainerOfContainerT2>
    void sort_together(ContainerT& sortValues,
                       ContainerOfContainerT cosortValuesItrRange,
                       ContainerOfContainerT2 cosortValuesItrRange2,
                       bool stable = false)
    {
        size_t size = sortValues.size();
        std::vector<size_t> indices = sorted_indices(sortValues, stable);

        auto begin1 = std::begin(cosortValuesItrRange), end1 = std::end(cosortValuesItrRange);
        auto begin2 = std::begin(cosortValuesItrRange2), end2 = std::end(cosortValuesItrRange2);

        permuted_copies<decltype(begin1)> gathered1(indices, begin1, end1);
        permuted_copies<decltype(begin2)> gathered2(indices, begin2, end2);

        ContainerT tmpSortValues(size);
        for (size_t i = 0; i < size; ++i)
            tmpSortValues[i] = sortValues[indices[i]];

        std::swap(tmpSortValues, sortValues);
        gathered1.swap_in();
        gathered2.swap_in();
    }

} // namespace util
} // namespace pwiz

#endif // _SORT_TOGETHER_
