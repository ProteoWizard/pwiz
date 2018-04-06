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

#ifndef _SIMPLETHREAD_H
#define _SIMPLETHREAD_H

#include "stdafx.h"

#ifdef WIN32
    #define simplethread_id_t            DWORD
    #define simplethread_handle_t        HANDLE
    #define simplethread_func_t            LPTHREAD_START_ROUTINE
    #define simplethread_arg_t            void*
    #define simplethread_return_t        DWORD WINAPI
    #define simplethread_mutex_t        CRITICAL_SECTION
    #define SIMPLETHREAD_CREATE            simplethread_handle_t simplethread_create_thread( simplethread_id_t* thread_id, simplethread_func_t thread_func, simplethread_arg_t thread_arg )

    typedef struct simplethread_handle_array
    {
        vector< simplethread_handle_t >    array;
    } simplethread_handle_array_t;
#else 
    #include <pthread.h>
    #include <signal.h>
    #define simplethread_id_t            pthread_t
    #define simplethread_handle_t        pthread_t
    #define simplethread_func_t            void*
    #define simplethread_arg_t            void*
    #define simplethread_return_t        void*
    #define simplethread_mutex_t        pthread_mutex_t
    #define SIMPLETHREAD_CREATE            simplethread_handle_t simplethread_create_thread( simplethread_id_t* thread_id, void* (*thread_func)(void *), simplethread_arg_t thread_arg )

    typedef struct simplethread_handle_array
    {
        simplethread_handle_array()
        {
            pthread_cond_init( &_cond, NULL );
            pthread_mutex_init( &_mutex, NULL );
        }

        ~simplethread_handle_array()
        {
            pthread_cond_destroy( &_cond );
            pthread_mutex_destroy( &_mutex );
        }

        vector< simplethread_handle_t >    array;
        pthread_cond_t                            _cond;
        pthread_mutex_t                            _mutex;
    } simplethread_handle_array_t;
#endif

                    SIMPLETHREAD_CREATE;
void                simplethread_exit_thread();
simplethread_id_t    simplethread_get_id();
void                simplethread_create_mutex( simplethread_mutex_t* mutex );
void                simplethread_destroy_mutex( simplethread_mutex_t* mutex );
void                simplethread_lock_mutex( simplethread_mutex_t* mutex );
void                simplethread_unlock_mutex( simplethread_mutex_t* mutex );
void                simplethread_join( simplethread_handle_t* thread_handle );
void                simplethread_join_all( simplethread_handle_array_t* thread_handles );
vector< int >        simplethread_join_any( simplethread_handle_array_t* thread_handles );
int                    simplethread_get_priority( simplethread_handle_t* thread_handle );
bool                simplethread_adjust_priority( simplethread_handle_t* thread_handle, int priorityOffset );


inline SIMPLETHREAD_CREATE
{
#ifdef WIN32
    //cout << "Creating Win32 thread" << endl;
    return CreateThread( 0, 0, thread_func, thread_arg, 0, thread_id );
#else
    //cout << "Creating *nix pthread" << endl;

    pthread_attr_t attr;
    pthread_attr_init( &attr );
    /*struct sched_param sParam;
    sParam.sched_priority = 0;
    pthread_attr_setinheritsched( &attr, PTHREAD_EXPLICIT_SCHED );
    pthread_attr_setschedpolicy( &attr, SCHED_RR );
    pthread_attr_setschedparam( &attr, &sParam );*/
    pthread_attr_setdetachstate( &attr, PTHREAD_CREATE_JOINABLE );

    pthread_create( thread_id, &attr, thread_func, thread_arg );
    pthread_attr_destroy( &attr );
    return *thread_id;
#endif
}

inline void simplethread_exit_thread()
{
#ifdef WIN32
    cout << "Exiting Win32 thread" << endl;
    ExitThread( 0 );
#else
    cout << "Exiting *nix pthread" << endl;
    pthread_exit( 0 );
#endif
}

inline void simplethread_delete_thread( simplethread_handle_t* thread_handle )
{
#ifdef WIN32
    CloseHandle( thread_handle );
#else
#endif
}

inline simplethread_id_t simplethread_get_id()
{
#ifdef WIN32
    return GetCurrentThreadId();
#else
    return pthread_self();
#endif
}


inline void simplethread_create_mutex( simplethread_mutex_t* mutex )
{
#ifdef WIN32
    InitializeCriticalSection( mutex );
#else
    pthread_mutex_init( mutex, NULL );
#endif
}

inline void simplethread_destroy_mutex( simplethread_mutex_t* mutex )
{
#ifdef WIN32
    DeleteCriticalSection( mutex );
#else
    pthread_mutex_destroy( mutex );
#endif
}

inline void simplethread_lock_mutex( simplethread_mutex_t* mutex )
{
#ifdef WIN32
    EnterCriticalSection( mutex );
#else
    pthread_mutex_lock( mutex );
#endif
}

inline void simplethread_unlock_mutex( simplethread_mutex_t* mutex )
{
#ifdef WIN32
    LeaveCriticalSection( mutex );
#else
    pthread_mutex_unlock( mutex );
#endif
}

inline void simplethread_join( simplethread_handle_t* thread_handle )
{
#ifdef WIN32
    WaitForSingleObject( *thread_handle, INFINITE );
#else
    pthread_join( *thread_handle, NULL );
#endif
}

inline void simplethread_join_all( simplethread_handle_array_t* thread_handles )
{
#ifdef WIN32
    WaitForMultipleObjects( (int) thread_handles->array.size(), &thread_handles->array.front(), TRUE, INFINITE );
    for( int i=0; i < (int) thread_handles->array.size(); ++i )
        CloseHandle( thread_handles->array[i] );
#else
    for( int i=0; i < (int) thread_handles->array.size(); ++i )
        pthread_join( thread_handles->array[i], NULL );
#endif
}

inline vector< int > simplethread_join_any( simplethread_handle_array_t* thread_handles )
{
    vector< int > joined_threads;

#ifdef WIN32
    // Wait for at least one thread to exit
    WaitForMultipleObjects( (int) thread_handles->array.size(), &thread_handles->array.front(), FALSE, INFINITE );

    // Then create a vector of the threads that have exited
    DWORD exitCode;
    for( int i=0; i < (int) thread_handles->array.size(); ++i )
    {
        GetExitCodeThread( thread_handles->array[i], &exitCode );
        if( exitCode != STILL_ACTIVE )
        {
            joined_threads.push_back(i);
            CloseHandle( thread_handles->array[i] );
        }
    }

#else
    pthread_mutex_lock( &thread_handles->_mutex );
    bool test;

    do {
        test = true;
        struct timespec timeout;
        struct timeval now;
        gettimeofday( &now, NULL );
        timeout.tv_sec = now.tv_sec;
        timeout.tv_nsec = (now.tv_usec + 1000) * 1000;
        pthread_cond_timedwait( &thread_handles->_cond, &thread_handles->_mutex, &timeout );

        // check if any threads have exited
        for( int i=0; i < (int) thread_handles->array.size(); ++i )
            if( pthread_kill( thread_handles->array[i], 0 ) == ESRCH )
            {
                test = false;
                joined_threads.push_back(i);
            }
    } while( test );

    pthread_mutex_unlock( &thread_handles->_mutex );
#endif

    return joined_threads;
}

inline int simplethread_get_priority( simplethread_handle_t* thread_handle )
{
#ifdef WIN32
    return GetThreadPriority( *thread_handle );
#else
    int sPolicy;
    struct sched_param sParam;
    pthread_getschedparam( *thread_handle, &sPolicy, &sParam );
    return sParam.sched_priority;
#endif
}

inline bool simplethread_adjust_priority( simplethread_handle_t* thread_handle, int priorityOffset )
{
#ifdef WIN32
    int priority = GetThreadPriority( *thread_handle );
    return ( SetThreadPriority( *thread_handle, priority + priorityOffset ) != 0 );
#else
    int sPolicy;
    struct sched_param sParam;
    pthread_getschedparam( *thread_handle, &sPolicy, &sParam );
    sParam.sched_priority += priorityOffset;
    return ( pthread_setschedparam( *thread_handle, sPolicy, &sParam ) == 0 );
#endif
}

#endif
