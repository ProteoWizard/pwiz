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

#ifndef _LNFACTORIALTABLE_H
#define _LNFACTORIALTABLE_H

#include "stdafx.h"
#include "simplethreads.h"

namespace freicore
{
    class lnFactorialTable
    {
        simplethread_mutex_t mutex;
    public:
        lnFactorialTable()
        {
            m_table.push_back(0);
            m_table.push_back(0);
            simplethread_create_mutex(&mutex);
        }

        ~lnFactorialTable() 
        {
            simplethread_destroy_mutex(&mutex);
        }

        double operator[]( size_t index )
        {
            // Is the table big enough?
            size_t maxIndex = m_table.size() - 1;
            if( index > maxIndex )
            {
                simplethread_lock_mutex(&mutex);
                while( index > maxIndex )
                {
                    m_table.push_back( m_table[ maxIndex ] + log( (float) m_table.size() ) );
                    ++maxIndex;
                }
                simplethread_unlock_mutex(&mutex);
            }

            return m_table[ index ];
        }

        void resize( size_t maxIndex )
        {
            this->operator []( maxIndex );
        }

    private:
        std::vector< double > m_table;
    };
}

#endif
