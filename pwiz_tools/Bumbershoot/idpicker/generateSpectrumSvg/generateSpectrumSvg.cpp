#include "generateSpectrumSvg.h"

using namespace freicore;
using namespace freicore::generateSpectrumSvg;

namespace freicore
{
namespace generateSpectrumSvg
{
	RunTimeConfig*		g_rtConfig;
	SpectraList			spectra;
	fileList_t			inputFileList;
	int					inputScan;
	int					chargeState;
	string				sequence;

	ostream*			pStatusOutput;
	ostream*			pXmlOutput;
	ofstream			xmlFileStream;
	stringstream		statusStringStream;
}
}

string generate()
{
	vector<float> peakMzs;
	vector<string> peakLabels;
	map< float, string > peakLabelMap;
	map< float, string > peakColorMap;
	map< float, int > peakWidthMap;
	Spectrum* s = *spectra.begin();
	s->Parse();
	s->id.setCharge( chargeState );
	for( PeakPreData::iterator itr = s->peakPreData.begin(); itr != s->peakPreData.end(); ++itr )
	{
		map<float, vector<int> > fragmentChargeStateTest;
		for( int z=1; z < chargeState; ++z )
		{
			vector<float> isotopeProbabilities;
			deisotoping::generateIsotopeProbabilities( itr->first * z, isotopeProbabilities );
			float sumOfProducts = 0;
			float nextIsotopeMz = itr->first + NEUTRON/z;
			for( size_t i=0; i < isotopeProbabilities.size(); ++i )
			{
				PeakPreData::iterator nextIsotopePeak = s->peakPreData.findNear( nextIsotopeMz, g_rtConfig->IsotopeMzTolerance );
				if( nextIsotopePeak != s->peakPreData.end() )
					sumOfProducts += nextIsotopePeak->second * isotopeProbabilities[i];
				nextIsotopeMz += NEUTRON/z;
			}
			fragmentChargeStateTest[sumOfProducts].push_back(z);
		}

		//for( map<float, vector<int> >::reverse_iterator itr2 = fragmentChargeStateTest.rbegin(); itr2 != fragmentChargeStateTest.rend(); ++itr2 )
		//	peakLabelMap[ itr->first ] += lexical_cast<string>( itr2->second ) + ":" + lexical_cast<string>( itr2->first ) + "\\n";
	}

	if( g_rtConfig->DeisotopingMode > 0 )
		s->Deisotope( g_rtConfig->IsotopeMzTolerance );
	s->FilterByTIC( g_rtConfig->TicCutoffPercentage );
	s->FilterByPeakCount( g_rtConfig->MaxPeakCount );

	if( !sequence.empty() )
	{
		CalculateSequenceIons( sequence, chargeState, &peakMzs, g_rtConfig->UseSmartPlusThreeModel, &peakLabels );

		for( size_t i=0; i < peakMzs.size(); ++i )
		{
			PeakPreData::iterator itr = s->peakPreData.findNear( peakMzs[i], g_rtConfig->FragmentMzTolerance );
			if( itr != s->peakPreData.end() )
			{
				peakLabelMap[ itr->first ] = peakLabels[i];
				peakColorMap[ itr->first ] = ( peakLabels[i].find( "b" ) == 0 ? "red" : "blue" );
				peakWidthMap[ itr->first ] = 2;
			}
		}

		return s->writeToSvg( &peakLabelMap, &peakColorMap, &peakWidthMap );
	} else
		return s->writeToSvg( &peakLabelMap, &peakColorMap, &peakWidthMap );
}

void usage( const string& binaryName, bool cgiMode = false )
{
	if( cgiMode )
	{
		*pXmlOutput << "Content-type: text/plain\r\n\r\n";
		*pXmlOutput << "Not enough arguments.\nUsage: " << binaryName << "?source=<raw source filemask>&scan=<input scan #>&charge=<charge state>[&sequence=<peptide sequence>]\n";
	} else
	{
		cerr << "Not enough arguments.\nUsage: " << binaryName << " <raw source filemask> <input scan #> <charge state> [peptide sequence]" << endl;
	}
}

int InitProcess( argList_t& args, bool cgiMode = false )
{
	g_hostString = GetHostname();
	g_residueMap = new ResidueMap();
	g_rtConfig = new RunTimeConfig;
	g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
	g_endianType = GetHostEndianType();

	inputFileList.clear();
	inputScan = 0;
	chargeState = 0;
	sequence.clear();

	if( cgiMode )
	{
		pStatusOutput = &statusStringStream;
		pXmlOutput = &cout;

		*pStatusOutput << string("<!-- GenerateSpectrumSvg ") + GENERATESPECTRUMSVG_VERSION_STRING + " (" + GENERATESPECTRUMSVG_BUILD_DATE + ") -->\n";

		char* queryEnvTest = getenv("QUERY_STRING");
		string queryString = ( queryEnvTest == NULL ? "" : queryEnvTest );
		vector<string> queryArgs;
		split( queryArgs, queryString, boost::is_any_of("&") );

		string inputFilemask;

		RunTimeVariableMap vars = g_rtConfig->getVariables();
		for( size_t i=0; i < queryArgs.size(); ++i )
		{
			vector<string> kvp;
			split( kvp, queryArgs[i], boost::is_any_of("=") );
			size_t escapeCharPos;
			while( ( escapeCharPos = kvp[1].find( "%" ) ) != string::npos )
			{
				stringstream escapeCharStream( kvp[1].substr( escapeCharPos+1, 2 ) );
				int escapeChar;
				escapeCharStream >> std::hex >> escapeChar;
				//cerr << kvp[1].substr( escapeCharPos+1, 2 ) << " is '" << (char)escapeChar << "'\n";
				kvp[1].replace( escapeCharPos, 3, lexical_cast<string>((char)escapeChar) );
			}
			if( kvp[0] == "source" )
			{
				inputFilemask = kvp[1];
				freicore::FindFilesByMask(inputFilemask, inputFileList);
			} else if( kvp[0] == "scan" )
				inputScan = lexical_cast<int>( kvp[1] );
			else if( kvp[0] == "charge" )
				chargeState = lexical_cast<int>( kvp[1] );
			else if( kvp[0] == "sequence" )
				sequence = kvp[1];
			else if( vars.count( kvp[0] ) > 0 )
				vars[kvp[0]] = UnquoteString( kvp[1] );
			else
				*pStatusOutput << "Unrecognized query parameter \"" << kvp[0] << "\" in CGI call." << endl;
		}
		g_rtConfig->setVariables( vars );

		if( !sequence.empty() )
			sequence = ConvertSqtPtmToFreiPtm( sequence );

		if( inputFileList.empty() || inputScan == 0 || chargeState == 0 )
		{
			usage( GetFilenameFromFilepath( args[0] ), true );
			if( inputFileList.empty() )
				*pXmlOutput << "\nError: no files found matching filemask \"" << inputFilemask << "\"." << endl;
			return 1;
		}

		*pXmlOutput << "Content-type: image/svg+xml\n\n";
	} else
	{
		pStatusOutput = &cout;
		pXmlOutput = &xmlFileStream;
		*pStatusOutput << "GenerateSpectrumSvg " << GENERATESPECTRUMSVG_VERSION_STRING << " (" << GENERATESPECTRUMSVG_BUILD_DATE << ")\n" << GENERATESPECTRUMSVG_LICENSE << endl;

		if( args.size() < 4 )
		{
			usage( GetFilenameFromFilepath( args[0] ), false );
			return 1;
		}

		for( size_t i=1; i < args.size(); ++i )
		{
			if( args[i] == "-cfg" && i+1 <= args.size() )
			{
				if( g_rtConfig->initializeFromFile( args[i+1] ) )
				{
					cerr << g_hostString << " could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
					return 1;
				}
				args.erase( args.begin() + i );

			} else if( args[i] == "-rescfg" && i+1 <= args.size() )
			{
				if( g_residueMap->initializeFromFile( args[i+1] ) )
				{
					cerr << g_hostString << " could not find residue masses at \"" << args[i+1] << "\"." << endl;
					return 1;
				}
				args.erase( args.begin() + i );
			} else
				continue;

			args.erase( args.begin() + i );
			--i;
		}

		// Command line overrides happen after config file has been distributed but before PTM parsing
		RunTimeVariableMap vars = g_rtConfig->getVariables();
		for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
		{
			string varName;
			varName += "-" + itr->first;

			for( size_t i=1; i < args.size(); ++i )
			{
				if( args[i].find( varName ) == 0 && i+1 <= args.size() )
				{
					//cout << varName << " " << itr->second << " " << args[i+1] << endl;
					itr->second = args[i+1];
					args.erase( args.begin() + i );
					args.erase( args.begin() + i );
					--i;
				}
			}
		}
		g_rtConfig->setVariables( vars );

		for( size_t i=1; i < args.size(); ++i )
		{
			if( args[i] == "-dump" )
			{
				g_rtConfig->dump();
				g_residueMap->dump();
				args.erase( args.begin() + i );
				--i;
			}
		}

		for( size_t i=1; i < args.size(); ++i )
		{
			if( args[i][0] == '-' )
			{
				cerr << "Warning: ignoring unrecognized parameter \"" << args[i] << "\"" << endl;
				args.erase( args.begin() + i );
				--i;
			}
		}

		freicore::FindFilesByMask(args[1], inputFileList);
		inputScan = lexical_cast<int>( args[2] );
		chargeState = lexical_cast<int>( args[3] );

		if( args.size() > 4 )
			sequence = args[4];
	}

	return 0;
}

int main( int argc, char* argv[] )
{

	vector< string > args;
	for( int i=0; i < argc; ++i )
		args.push_back( argv[i] );

	bool cgiMode = getenv("REQUEST_METHOD") != NULL;

	if( InitProcess( args, cgiMode ) )
	{
		if( cgiMode && g_rtConfig->ShowErrorsFromCGI )
			cerr << statusStringStream.str();
		return 1;
	}

	ostream& statusOutput = *pStatusOutput;
	ostream& xmlOutput = *pXmlOutput;

	if( !g_rtConfig->initialized() )
	{
		if( g_rtConfig->initializeFromFile() )
		{
			statusOutput << g_hostString << " could not find the default configuration file (hard-coded defaults in use)." << endl;
		}
	}

	if( !g_residueMap->initialized() )
	{
		if( g_residueMap->initializeFromFile() )
		{
			statusOutput << g_hostString << " could not find the default residue masses file (hard-coded defaults in use)." << endl;
		}
	}

	//PeakSpectrum<float>::GenerateIsotopeDistributionSVG("1,2,3,4");return 0;

	for( fileList_t::const_iterator fItr = inputFileList.begin(); fItr != inputFileList.end(); ++fItr )
	{
		spectra.clear();

		Timer readTime(true);
		statusOutput << "Reading peak data for scan " << inputScan << " from filepath: " << *fItr << endl;
		try
		{
			spectra.readPeaks( *fItr, inputScan, inputScan, true, 1, g_rtConfig->CentroidPeaks, g_rtConfig->PreferVendorCentroid, g_rtConfig->processingOptions );
			statusOutput << "Finished reading peak data; " << readTime.End() << " seconds elapsed." << endl;
		} catch( exception& e )
		{
			statusOutput << "Error: " << e.what() << endl;
			continue;
		}

		if( spectra.empty() )
		{
			cerr << "Error: no spectra read from input file." << endl;
			return 1;
		}

		if( !cgiMode )
		{
			stringstream svgFilename;
			svgFilename << SpectrumId( *fItr, inputScan, chargeState ) << g_rtConfig->OutputSuffix;
			if( !sequence.empty() )
				svgFilename << '-' << sequence;
			svgFilename << ".svg";
			xmlFileStream.open( svgFilename.str().c_str(), ios::binary );
		}
		xmlOutput << generate();

		if( cgiMode )
			break; // cgi mode only handles the first file
		else
			xmlFileStream.close();
	}

	if( cgiMode && g_rtConfig->ShowErrorsFromCGI )
		cerr << statusStringStream.str();
	return 0;
}
