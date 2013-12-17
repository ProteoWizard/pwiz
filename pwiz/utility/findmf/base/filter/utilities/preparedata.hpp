//
// $Id$
//
//
// Original author: Witold Wolski <wewolski@gmail.com>
//
// Copyright : ETH Zurich
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

#ifndef PREPAREDATA_H
#define PREPAREDATA_H

namespace ralab{
  namespace base{
    namespace filter{
      namespace utilities{
        /*!\brief

       Example Sequence : 1 2 3 4 5; 
        width 5 and mirror false:
       4 5 1 2 3 4 5 1 2,
       if mirror true than:
       2 1 1 2 3 4 5 5 4

       */
        template <typename TContainer ,typename TIterator>
        typename TContainer::iterator prepareData
        (
            TIterator dataBeg ,
            TIterator dataEnd ,
            size_t fsize,
            TContainer &res , //!< [out]
            bool mirror = false //!< should it be circular or mirrored.
            )
        {
          if(mirror)
            {
              typename TContainer::iterator it;
              size_t fsize2 = (fsize-1)/2;
              res.resize(std::distance(dataBeg,dataEnd)+fsize);
              boost::reverse_iterator<TIterator> reverse_begin(dataEnd);
              boost::reverse_iterator<TIterator> reverse_end(dataBeg);

              it = std::copy(reverse_end - fsize2,reverse_end, res.begin() );
              it = std::copy(dataBeg,dataEnd, it );
              it = std::copy( reverse_begin, reverse_begin + fsize2, it);
              return it;
            }
          else
            {
              typename TContainer::iterator it;
              size_t fsize2 = (fsize-1)/2;
              res.resize(std::distance(dataBeg,dataEnd)+fsize);
              it = std::copy(dataEnd - fsize2,dataEnd, res.begin() );
              it = std::copy(dataBeg,dataEnd, it );
              it = std::copy( dataBeg, dataBeg + fsize2, it);
              return it;
            }
        }
      }//end utilities

    }//filter
  }//base
}//ralab

#endif // PREPAREDATA_H
