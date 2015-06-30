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

#ifndef _CHARINDEXEDVECTOR_H
#define _CHARINDEXEDVECTOR_H

namespace freicore
{
    template< class T >
    class CharIndexedVectorIterator
    {
        typedef array< T, 129 > type;
        typename type::iterator m_itr;

    public:
        typedef typename type::value_type            value_type;
        typedef typename type::iterator                iterator;
        typedef typename type::iterator                pointer;
        typedef typename type::const_iterator        const_iterator;
        typedef typename type::difference_type        difference_type;
        typedef typename type::reference            reference;
        typedef typename type::const_reference        const_reference;
        typedef typename type::size_type            size_type;
        typedef std::random_access_iterator_tag        iterator_category;

        CharIndexedVectorIterator( const iterator& itr ) : m_itr( itr ) {}

        reference operator*() const
        {
            return *m_itr;
        }

        bool operator!=( const CharIndexedVectorIterator& rhs ) const
        {
            return m_itr != *(iterator*)&rhs;
        }

        difference_type operator-( const CharIndexedVectorIterator& rhs ) const
        {
            return m_itr - rhs.m_itr;
        }

        CharIndexedVectorIterator& operator++()
        {    // preincrement
            do
            {
                ++m_itr;
            } while( *m_itr == T() );
            return (*this);
        }

        CharIndexedVectorIterator operator++(int)
        {    // postincrement
            CharIndexedVectorIterator _Tmp = *this;
            do
            {
                ++m_itr;
            } while( *m_itr == T() );
            return (_Tmp);
        }

        CharIndexedVectorIterator& operator--()
        {    // predecrement
            do
            {
                ++m_itr;
            } while( *m_itr == T() );
            return (*this);
        }

        CharIndexedVectorIterator operator--(int)
        {    // postdecrement
            CharIndexedVectorIterator _Tmp = *this;
            do
            {
                ++m_itr;
            } while( *m_itr == T() );
            return (_Tmp);
        }

        CharIndexedVectorIterator& operator+=(difference_type _Off)
        {    // increment by integer
            m_itr += _Off;
            return (*this);
        }

        CharIndexedVectorIterator& operator-=(difference_type _Off)
        {    // decrement by integer
            return (*this += -_Off);
        }
    };

    template< class T >
    class CharIndexedVectorConstIterator
    {
        typedef array< T, 129 > type;
        typename type::const_iterator m_itr;

        typedef CharIndexedVectorConstIterator<T>    ItrType;

    public:
        typedef typename type::value_type            value_type;
        typedef typename type::iterator                iterator;
        typedef typename type::iterator                pointer;
        typedef typename type::const_iterator        const_iterator;
        typedef typename type::difference_type        difference_type;
        typedef typename type::reference            reference;
        typedef typename type::const_reference        const_reference;
        typedef typename type::size_type            size_type;
        typedef std::random_access_iterator_tag        iterator_category;

        CharIndexedVectorConstIterator( const const_iterator& itr ) : m_itr( itr ) {}

        const_reference operator*() const
        {
            return *m_itr;
        }

        bool operator!=( const CharIndexedVectorConstIterator& rhs ) const
        {
            return m_itr != *(const_iterator*)&rhs;
        }

        difference_type operator-( const CharIndexedVectorConstIterator& rhs ) const
        {
            return m_itr - rhs.m_itr;
        }

        CharIndexedVectorConstIterator& operator++()
        {    // preincrement
            do
            {
                ++m_itr;
            } while( *m_itr == T() );
            return (*this);
        }

        CharIndexedVectorConstIterator operator++(int)
        {    // postincrement
            CharIndexedVectorConstIterator _Tmp = *this;
            do
            {
                ++m_itr;
            } while( *m_itr == T() );
            return (_Tmp);
        }

        CharIndexedVectorConstIterator& operator--()
        {    // predecrement
            do
            {
                ++m_itr;
            } while( *m_itr == T() );
            return (*this);
        }

        CharIndexedVectorConstIterator operator--(int)
        {    // postdecrement
            CharIndexedVectorConstIterator _Tmp = *this;
            do
            {
                ++m_itr;
            } while( *m_itr == T() );
            return (_Tmp);
        }

        CharIndexedVectorConstIterator& operator+=(difference_type _Off)
        {    // increment by integer
            m_itr += _Off;
            return (*this);
        }

        CharIndexedVectorConstIterator& operator-=(difference_type _Off)
        {    // decrement by integer
            return (*this += -_Off);
        }
    };

    template< class T >
    struct CharIndexedVector : public array< T, 129 >
    {
        typedef array< T, 129 > type;
        typedef CharIndexedVectorIterator<T> iterator;
        typedef CharIndexedVectorConstIterator<T> const_iterator;
        typedef std::reverse_iterator<iterator> reverse_iterator;
        typedef std::reverse_iterator<const_iterator> const_reverse_iterator;

        CharIndexedVector()// : vector< float >( 129, 0.0f ) { at(0) = -1; at(128) = -1; }
        {
            clear();
        }

        size_t size() const
        {
            size_t numResidues = 0;
            for( size_t i=0; i < 128; ++i )
                if( type::operator[](i) != T() )
                    ++ numResidues;
            return numResidues;
        }

        void erase( const char c )
        {
            this->operator[](c) = T();
        }

        void clear()
        {
            std::fill( type::begin(), type::end(), T() );
            //at(0) = at(128) = -1;
        }

        char getIndexAsChar( iterator itr ) const
        {
            return 'A' + ( itr - type::begin() );
        }

        char getIndexAsChar( size_t i ) const
        {
            return 'A' + ( &this->operator[](i) - type::begin() );
        }

        const T& operator[]( const char c ) const
        {
            return type::operator[]( (size_t) c );
        }

        T& operator[] ( const char c )
        {
            //return at( (size_t) c );
            return type::operator[]( (size_t) c ); //at(c);
        }

        const_iterator begin() const    { return ++ const_iterator( type::begin() ); }
        const_iterator end() const        { return -- const_iterator( type::end() ); }
        iterator begin()                { return ++ iterator( type::begin() ); }
        iterator end()                    { return -- iterator( type::end() ); }
    };
}
#endif
