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

#ifndef _PROFILER_H
#define _PROFILER_H

#include "stdafx.h"
//#include "shared_funcs.h"

#ifdef WIN32
	#define MPI_CLOCKS_PER_SEC CLK_TCK
#else
	#define MPI_CLOCKS_PER_SEC CLOCKS_PER_SEC
#endif

namespace freicore
{
	class Profiler
	{
	public:

		Profiler( string name = "", bool bStartActive = false ) : m_name(name), m_lTickDifference(0)
		{
			if( bStartActive )
				Begin();
			m_lTicksPerSec = MPI_CLOCKS_PER_SEC;
		}

		~Profiler()
		{
		}

		// Start timer with 0 clocks difference
		// Start timer from last clocks difference
		// Get time elapsed since timer was last started
		// Get total time elapsed since timer was created or last reset
		// End timer and return time elapsed since timer was last started
		// End timer and return time elapsed since timer was created or last reset

		string	GetName()
		{
			return m_name;
		}

		void	Begin( bool bReset = true )
		{
			m_bActive = true;
			if( bReset )
				m_lTickDifference = 0;
			m_lStartTick = clock();
		}

		float	TimeElapsed( bool bFromLastStart = true )
		{
			if( m_bActive )
				m_lEndTick = clock();

			long lCurTickDifference;
			if( bFromLastStart )
				lCurTickDifference = m_lEndTick - m_lStartTick;
			else
				lCurTickDifference = m_lTickDifference;

			//return round( (float) lCurTickDifference / (float) m_lTicksPerSec, 4 );
			return (float) lCurTickDifference / (float) m_lTicksPerSec;
		}

		float	End( bool bFromLastStart = false )
		{
			if( m_bActive )
			{
				m_lEndTick = clock();
				m_lTickDifference += m_lEndTick - m_lStartTick;
				m_bActive = false;
			}
			return TimeElapsed( bFromLastStart );
		}

	private:
		string	m_name;
		bool	m_bActive;
		clock_t	m_lStartTick;
		clock_t	m_lEndTick;
		clock_t m_lTickDifference;
		long	m_lTicksPerSec;
	};

	class BaseTimer
	{
	public:
		virtual ~BaseTimer() {}
		virtual void Begin() = 0;
		virtual float TimeElapsed() = 0;
		virtual float End() = 0;
	};

	#ifdef WIN32
		class QueryPerformanceCounterTimer : BaseTimer
		{
		public:

			QueryPerformanceCounterTimer( bool bStartActive = false )
			{
				if( bStartActive )
					Begin();
				QueryPerformanceFrequency( &m_lTicksPerSec );
			}

			~QueryPerformanceCounterTimer()
			{
			}

			void	Begin()
			{
				m_bActive = true;
				QueryPerformanceCounter( &m_lStartTick );
			}

			float	TimeElapsed()
			{
				if( m_bActive )
					QueryPerformanceCounter( &m_lEndTick );
				return float( m_lEndTick.QuadPart - m_lStartTick.QuadPart ) / (float) m_lTicksPerSec.QuadPart;
			}

			float	End()
			{
				if( m_bActive )
					QueryPerformanceCounter( &m_lEndTick );
				m_bActive = false;
				return TimeElapsed();
			}

		private:
			bool	m_bActive;
			LARGE_INTEGER	m_lStartTick;
			LARGE_INTEGER	m_lEndTick;
			LARGE_INTEGER	m_lTicksPerSec;
		};

	#else

		class GetTimeOfDayTimer : BaseTimer
		{
		public:

			GetTimeOfDayTimer( bool bStartActive = false )
			{
				m_tzGMT.tz_minuteswest = 0;
				m_tzGMT.tz_dsttime = 0;

				if( bStartActive )
					Begin();
			}

			~GetTimeOfDayTimer()
			{
			}

			void	Begin()
			{
				m_bActive = true;
				gettimeofday( &m_tStartTime, &m_tzGMT );
			}

			float	TimeElapsed()
			{
				if( m_bActive )
					gettimeofday( &m_tEndTime, &m_tzGMT );
				double fStartTime = m_tStartTime.tv_sec + (double) m_tStartTime.tv_usec / 1000000.0f;
				double fEndTime = m_tEndTime.tv_sec + (double) m_tEndTime.tv_usec / 1000000.0f;
				return float( fEndTime - fStartTime );
			}

			float	End()
			{
				if( m_bActive )
					gettimeofday( &m_tEndTime, &m_tzGMT );
				m_bActive = false;
				return TimeElapsed();
			}

		private:
			bool			m_bActive;
			struct timezone	m_tzGMT;
			struct timeval	m_tStartTime;
			struct timeval	m_tEndTime;
		};

	#endif

	class Timer
	{
	public:

		Timer( bool bStartActive = false )
		{
			#ifdef WIN32
				// Check for hardware support of high resolution timer
				LARGE_INTEGER f;
				if( QueryPerformanceFrequency( &f ) )
				{
					m_pBaseTimer = (BaseTimer*) new QueryPerformanceCounterTimer( bStartActive );
				}
			#else
				m_pBaseTimer = (BaseTimer*) new GetTimeOfDayTimer( bStartActive );
			#endif

			if( bStartActive )
				Begin();
		}

		~Timer()
		{
			delete m_pBaseTimer;
		}

		void	Begin()			{ m_pBaseTimer->Begin(); }
		float	TimeElapsed()	{ return max( 0.0f, m_pBaseTimer->TimeElapsed() ); }
		float	End()			{ return max( 0.0f, m_pBaseTimer->End() ); }

	private:
		BaseTimer*	m_pBaseTimer;
	};
}

#endif
