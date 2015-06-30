//
// $Id$
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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#ifndef _TOPSET_H
#define _TOPSET_H

#include "stdafx.h"
#include "Profiler.h"
namespace freicore
{
    template< class T, class ComparePredicate = std::less<T> >
    class topset : public set<T, ComparePredicate>
    {
        typedef set<T, ComparePredicate> MyBase;
    public:
        topset( size_t maxSize = 0 ) : MyBase(), m_maxSize( maxSize ) {}

        //Profiler insertTime;

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< MyBase >( *this );
            ar & m_maxSize;// & m_permSet;
        }

        size_t max_size()
        {
            return m_maxSize;
        }

        void max_size( size_t maxSize )
        {
            m_maxSize = maxSize;
            trim();
        }

        void trim()
        {
            if( !m_maxSize )
                return;

            while( MyBase::size() > m_maxSize )
                MyBase::erase( MyBase::begin() );
        }

        void clear()
        {
            MyBase::clear();
            //m_permSet.clear();
        }

        void erase(const T& value) {
            MyBase::erase(value);
        }

        TemplateSetEqualPair(MyBase) equal(const T& value)
        {
            TemplateSetEqualPair(MyBase) result = MyBase::equal_range( value );
            return result;
        }

        TemplateSetInsertPair(MyBase) insert( const T& value, bool noMatterWhat = false )
        {
            // If m_maxSize is not set (0), do a regular insert
            if( !m_maxSize || noMatterWhat )
                return MyBase::insert( value );
            else
            {
                typename MyBase::iterator itr = MyBase::find( value );

                // If the new value is already in the set, the insert fails
                if( itr != MyBase::end() )
                {
                    //insertTime.End();
                    return TemplateSetInsertPair(MyBase)( itr, false );
                }

                // If set is not full, add the new value
                if( MyBase::size() < m_maxSize )
                {
                    TemplateSetInsertPair(MyBase) result = MyBase::insert( value );
                    //insertTime.End();
                    return result;
                }

                // If set is full and the new value is better than the worst existing value,
                // add the new value and remove the worst value
                else if( *MyBase::begin() < value )
                {
                    MyBase::erase( MyBase::begin() );
                    TemplateSetInsertPair(MyBase) result = MyBase::insert( value );
                    //insertTime.End();
                    return result;
                }
            }

            //insertTime.End();

            // The set was full and the new value was worse than the worst existing value
            return TemplateSetInsertPair(MyBase)( MyBase::end(), false );
        }

    protected:
        size_t    m_maxSize;
    };
}

#endif
