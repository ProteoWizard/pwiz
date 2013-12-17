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


#ifndef BASEUTILITIES_H
#define BASEUTILITIES_H


namespace ralab
{
  namespace base
  {
    namespace base
    {
      namespace utilities
      {
        // class generator:
        template<typename TReal>
        struct SeqPlus {
          TReal m_from;
          TReal m_by;

          SeqPlus(TReal from)
            : m_from(from), m_by(1)
          {}
          TReal operator()()
          {
            TReal current = m_from ;
            m_from += m_by;
            return current;
          }
        };

        template<typename TReal>
        struct SeqMinus {
          TReal m_from;
          TReal m_by;

          SeqMinus(TReal from)
            : m_from(from), m_by(1)
          {}
          TReal operator()()
          {
            TReal current = m_from ;
            m_from -= m_by;
            return current;
          }
        };
      }
    } //base
  } //base
}//ralab

#endif // BASE_H
