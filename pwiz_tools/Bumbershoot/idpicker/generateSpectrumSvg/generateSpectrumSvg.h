#ifndef _GENERATESPECTRUMSVG_H
#define _GENERATESPECTRUMSVG_H

#define USE_BOOST_REGEX

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"
#include "PeakSpectrum.h"

#define GENERATESPECTRUMSVG_MAJOR				2.
#define GENERATESPECTRUMSVG_MINOR				0.
#define GENERATESPECTRUMSVG_BUILD				2

#define GENERATESPECTRUMSVG_VERSION				BOOST_PP_CAT( GENERATESPECTRUMSVG_MAJOR, BOOST_PP_CAT( GENERATESPECTRUMSVG_MINOR, GENERATESPECTRUMSVG_BUILD ) )
#define GENERATESPECTRUMSVG_VERSION_STRING		BOOST_PP_STRINGIZE( GENERATESPECTRUMSVG_VERSION )
#define GENERATESPECTRUMSVG_BUILD_DATE			"12/18/2007"

#define GENERATESPECTRUMSVG_LICENSE				COMMON_LICENSE

#define READ_BUFFER 32768

#define RUNTIME_CONFIG	\
	COMMON_RTCONFIG SEQUENCE_RTCONFIG SPECTRUM_RTCONFIG \
	RTCONFIG_VARIABLE( string,			OutputSuffix,				""				) \
	RTCONFIG_VARIABLE( int,				DeisotopingMode,			0				) \
	RTCONFIG_VARIABLE( float,			TicCutoffPercentage,		1.0f			) \
	RTCONFIG_VARIABLE( size_t,			MaxPeakCount,				1000			) \
	RTCONFIG_VARIABLE( bool,			ShowErrorsFromCGI,			false			) \
	RTCONFIG_VARIABLE( bool,			CentroidPeaks,				true			) \
	RTCONFIG_VARIABLE( bool,			PreferVendorCentroid,		true			) \
	RTCONFIG_VARIABLE( string,			FormatSpecificOptions,		"pip=0 picp=0 fpc=1" )

using namespace freicore;

namespace freicore
{
namespace generateSpectrumSvg
{
	struct RunTimeConfig : public BaseRunTimeConfig
	{
	public:
		RTCONFIG_DEFINE_MEMBERS( RunTimeConfig, RUNTIME_CONFIG, "\r\n\t ", "generateSpectrumSvg.cfg", "\r\n" );

		ProcessingOptions processingOptions;

	private:
		void finalize()
		{
			string cwd;
			cwd.resize( MAX_PATH );
			getcwd( &cwd[0], MAX_PATH );
			WorkingDirectory = cwd.c_str();

			if( TicCutoffPercentage > 1.0f )
			{
				TicCutoffPercentage /= 100.0f;
				if( g_pid == 0 )
					cerr << g_hostString << ": TicCutoffPercentage > 1.0 (100%) corrected, now at: " << TicCutoffPercentage << endl;
			}

			if( !DynamicMods.empty() )
			{
				DynamicMods = TrimWhitespace( DynamicMods );
				g_residueMap->setDynamicMods( DynamicMods );
			}

			if( !StaticMods.empty() )
			{
				StaticMods = TrimWhitespace( StaticMods );
				g_residueMap->setStaticMods( StaticMods );
			}

			processingOptions.clear();
			string::const_iterator start = FormatSpecificOptions.begin();
			string::const_iterator end = FormatSpecificOptions.end();
			boost::match_flag_type flags = boost::match_default;
			regex nameValuePairRegex( "(\\S+)=(\\S+)" );

			smatch nameValuePair;
			while( regex_search( start, end, nameValuePair, nameValuePairRegex, flags ) )
			{
				processingOptions[nameValuePair[1]].value = nameValuePair[2];

				start = nameValuePair[0].second;
				flags |= boost::match_prev_avail;
				flags |= boost::match_not_bob;
			}
		}
	};

	struct PeakInfo
	{
		template< class Archive >
		void serialize( Archive& ar, const int unsigned version )
		{
			ar & intenClass;
		}

		int		intenClass;

	};

	struct Spectrum : public PeakSpectrum< PeakInfo >
	{
	};

	struct SpectraList : public PeakSpectraList< Spectrum, SpectraList >
	{
	};
}
}

#endif
