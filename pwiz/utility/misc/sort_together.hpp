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

    template<typename ContainerT, typename ContainerOfContainerTIterator>
    void sort_together(ContainerT& sortValues, ContainerOfContainerTIterator cosortValuesItrRangeBegin, ContainerOfContainerTIterator cosortValuesItrRangeEnd, bool stable = false)
    {
        size_t size = sortValues.size();
        vector<size_t> indices(size);
        for (size_t i = 0; i < size; ++i)
            indices[i] = i;

        if (stable)
            std::stable_sort(indices.begin(), indices.end(), SortByOther<typename ContainerT::iterator>(sortValues.begin()));
        else
            std::sort(indices.begin(), indices.end(), SortByOther<typename ContainerT::iterator>(sortValues.begin()));

        ContainerT tmpSortValues(size);
        size_t numRanges = cosortValuesItrRangeEnd - cosortValuesItrRangeBegin;
        vector<ContainerT> tmpValuesRanges(numRanges);
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

} // namespace util
} // namespace pwiz

#endif // _SORT_TOGETHER_
