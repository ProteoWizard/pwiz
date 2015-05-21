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

#ifndef _HISTOGRAM_H
#define _HISTOGRAM_H

#include "stdafx.h"
#include "PeakSpectrum.h"

namespace freicore
{
	template< class T >
	struct Histogram
	{
		typedef pair< T, int > bin_type;
		typedef map< T, int > ValueMapType;

		Histogram( size_t numBins = 100, size_t maxValues = 100 )
				:	m_numBins( numBins ), m_maxValues( maxValues )
		{}

		void add( const T& value )
		{
			++ m_values[ value ];
			if( m_values.size() > m_maxValues )
				smooth();
		}

		void clear()
		{
			m_bins.clear();
			m_values.clear();
		}

		void smooth()
		{
			if( m_values.size() < 2 )
				return;

			for( typename ValueMapType::iterator itr = m_bins.begin(); itr != m_bins.end(); ++itr )
			{
				pair< typename ValueMapType::iterator, bool > rv = m_values.insert( *itr );
				if( !rv.second )
					rv.first->second += itr->second;
			}
			m_bins.clear();

			T minValue = m_values.begin()->first;
			T maxValue = m_values.rbegin()->first;
			T valueRange = maxValue - minValue;
			//double rangeLog10 = log10( static_cast< double >( valueRange ) );
			//double rangeMagnitude;
			//double rangeMultiplier = ceil( pow( 10.0, modf( rangeLog10, &rangeMagnitude ) ) );
			//cout << "Range log10 magnitude: " << rangeMagnitude << " multiplier: " << rangeMultiplier << endl;
			//minValue = (T) pow( 10.0, floor( log10( static_cast< double >( minValue ) ) ) );
			//valueRange = (T) ( rangeMultiplier * pow( 10.0, rangeMagnitude ) );
			//maxValue = minValue + valueRange;
			T valueBinSize = valueRange / (T) m_numBins;
			//if( g_pid == 0 ) cout << "Smoothing histogram with " << m_values.size() << " values (" << minValue << "..." << maxValue << ")" << endl;
			for( T valueBin = minValue; round( valueBin ) <= maxValue; valueBin += valueBinSize )
			{
				typename ValueMapType::iterator binItr = m_bins.insert( typename ValueMapType::value_type( valueBin, 0 ) ).first;
				//if( g_pid == 0 ) cout << "Bin is " << valueBin << endl;
				T nextBinValue = binItr->first + valueBinSize;
				typename ValueMapType::iterator nextBinItr = m_bins.end();
				if( nextBinValue < maxValue )
					nextBinItr = m_bins.insert( typename ValueMapType::value_type( nextBinValue, 0 ) ).first;
				typename ValueMapType::iterator itr = m_values.lower_bound( valueBin );
				for( ; itr != m_values.end() && itr->first < nextBinValue; ++itr )
				{
					//if( g_pid == 0 ) cout << "Moving and erasing value " << itr->first << " to bin " << binItr->first << endl;
					binItr->second += itr->second;
				}
			}
			m_values.clear();
			//if( g_pid == 0 ) cout << "Smoothed histogram: " << m_bins << endl;
		}

		string writeToSvg( const string& hLabel, const string& vLabel, size_t width, size_t height )
		{
			smooth();

			if( m_bins.size() > 1 )
			{
				PeakSpectrum<> s;
				s.mzLowerBound = m_bins.begin()->first;
				s.mzUpperBound = m_bins.rbegin()->first;
				stringstream infoText;
				infoText << "Total: " << getTotal();// << "   Mean:";
				s.id.set( infoText.str(), 0, 0 );
				for( typename ValueMapType::const_iterator itr = m_bins.begin(); itr != m_bins.end(); ++itr )
				{
					s.peakPreData[ itr->first ] = (float) itr->second;
				}
				return s.writeToSvg( NULL, NULL, NULL );
			}

			stringstream svgXml;
			typename ValueMapType::const_iterator itr;
			size_t total = getTotal();
			size_t n;
			size_t hgWidth = width - 15;	// make room for the vertical axis line and label
			size_t hgx = 30;
			size_t hgHeight = height - 30;	// make room for the horizontal axis line and label, and bar labels
			//size_t hgy = 0;

			size_t widthOfBars = 8;
			size_t spaceBetweenBars = size_t( float( hgWidth - widthOfBars * m_bins.size() ) / float( m_bins.size()+1 ) ) + widthOfBars;

			svgXml <<	"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n"
						"<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.0//EN\" \"http://www.w3.org/TR/2001/REC-SVG-20010904/DTD/svg10.dtd\">\n"
						"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"" << width << "\" height=\"" << height << "\">\n" <<
						"\t<script type=\"text/ecmascript\"><![CDATA[\n\t\tvar valueNode = null;\n\t\tvar textNode = null;\n" <<
						"\t\tfunction showBarValue(evt)\n\t\t{\n\t\t\tvar total = " << total << ";\n\t\t\tvalueNode = document.createElementNS( \"http://www.w3.org/2000/svg\", \"text\" );\n" <<
						"\t\t\tvalueNode.setAttributeNS( null, \"x\", parseInt( evt.target.getAttributeNS( null, \"x\" ) ) + " << widthOfBars / 2 << " );\n" <<
						"\t\t\tif( evt.target.parentNode.getAttributeNS( null, \"fill\" ) == \"red\" )\n" <<
						"\t\t\t\tvalueNode.setAttributeNS( null, \"y\", parseInt( evt.target.getAttributeNS( null, \"y\" ) ) - 3 );\n";
			svgXml <<	"\t\t\telse\n" <<
						"\t\t\t\tvalueNode.setAttributeNS( null, \"y\", parseInt( evt.target.getAttributeNS( null, \"height\" ) ) - 3 );\n" <<
						"\t\t\tvar value = evt.target.getAttributeNS( null, \"value\" );\n\t\t\tvar percent = value / " << total << ";\n" <<
						"\t\t\ttextNode = document.createTextNode( value + \" (\" + ( percent * 100 ).toFixed() + \"%)\" );\n" <<
						"\t\t\tvalueNode.appendChild( textNode );\n\t\t\tdocument.getElementById(\"labels\").appendChild( valueNode );\n\t\t}\n" <<
						"\t\tfunction hideBarValue(evt)\n\t\t{\n\t\t\tdocument.getElementById(\"labels\").removeChild( valueNode );\n\t\t\tvalueNode = null;\n\t\t\ttextNode = null;\n\t\t}\n" <<
						"\t]]></script>\n";

			stringstream axisLabels;

			float maxValue = 0;
			for( itr = m_bins.begin(); itr != m_bins.end(); ++itr )
				maxValue = (float) max( itr->second, (int) maxValue );
			maxValue += 0.05f * maxValue;
			maxValue = min( (float) total, maxValue );

			svgXml <<	"\t<g fill=\"red\">\n";
			size_t x = hgx;
			for( itr = m_bins.begin(), n=0; itr != m_bins.end(); ++itr, ++n )
			{
				x += spaceBetweenBars - ( widthOfBars / 2 );
				size_t barHeight = size_t( ( (float) itr->second / maxValue ) * 0.95f * (float) hgHeight ) + 1;
				size_t y = hgHeight - barHeight;
				svgXml <<	"\t\t<rect width=\"" << widthOfBars << "\" height=\"" << barHeight <<
							"\" x=\"" << x << "\" y=\"" << y << "\" value=\"" << itr->second <<
							"\" onmouseover=\"showBarValue(evt)\" onmouseout=\"hideBarValue(evt)\" />\n";

				axisLabels << "\t\t<text x=\"" << x + widthOfBars / 2 << "\" y=\"" << hgHeight+15 << "\">";
				typename ValueMapType::const_iterator nextItr = itr; ++nextItr;
				if( nextItr == m_bins.end() )
					axisLabels << ">= " << round( itr->first, 1 );
				else if( nextItr->first - itr->first > 1 )
					axisLabels << round( itr->first, 1 ) << ".." << round( nextItr->first - 1, 1 );
				else
					axisLabels << round( itr->first, 1 );
				axisLabels << "</text>\n";
			}
			svgXml <<	"\t</g>\n";

			svgXml <<	"\t<g fill=\"white\">\n";
			x = hgx;
			for( itr = m_bins.begin(), n=0; itr != m_bins.end(); ++itr, ++n )
			{
				x += spaceBetweenBars - ( widthOfBars / 2 );
				size_t barHeight = size_t( ( (float) itr->second / maxValue ) * 0.95f * (float) hgHeight ) + 1;
				size_t y = hgHeight - barHeight;
				svgXml <<	"\t\t<rect width=\"" << widthOfBars << "\" height=\"" << y <<
							"\" x=\"" << x << "\" y=\"0\" value=\"" << itr->second <<
							"\" onmouseover=\"showBarValue(evt)\" onmouseout=\"hideBarValue(evt)\" />\n";
			}
			svgXml <<	"\t</g>\n";

			svgXml <<	"\t<g id=\"labels\" stroke=\"black\" stroke-width=\"0.3\" text-anchor=\"middle\" font-size=\"10\">\n" <<
						"\t\t<text font-size=\"12\" x=\"" << hgx + ( hgWidth / 2 ) << "\" y=\"" << height-3 << "\">" << hLabel << "</text>\n" <<
						"\t\t<text font-size=\"12\" x=\"0\" y=\"0\" transform=\"translate( " << hgx-10 <<", " << hgHeight / 2 << " ) rotate(-90)\">" << vLabel << "</text>\n" <<
						"\t\t<text x=\"" << hgx-10 << "\" y=\"" << hgHeight << "\">0%</text>\n" <<
						"\t\t<text x=\"" << hgx-15 << "\" y=\"" << 10 << "\">" << round( maxValue / total * 100, 0 ) << "%</text>\n" <<
						axisLabels.str() <<
						"\t</g>\n";

			svgXml <<	"\t<g stroke=\"black\" stroke-width=\"2\">\n" <<
						"\t\t<line x1=\"" << hgx << "\" y1=\"" << hgHeight << "\" x2=\"" << width << "\" y2=\"" << hgHeight << "\" />\n" <<
						"\t\t<line x1=\"" << hgx << "\" y1=\"" << hgHeight+1 << "\" x2=\"" << hgx << "\" y2=\"" << 0 << "\" />\n" <<
						"\t</g>\n";

			svgXml <<	"</svg>\n";
			return svgXml.str();
		}

		void writeToSvgFile( const string& svgFilename, const string& hLabel, const string& vLabel, size_t width, size_t height )
		{
			ofstream svgFile( svgFilename.c_str(), ios::binary );
			svgFile << writeToSvg( hLabel, vLabel, width, height );
		}
		
		int getTotal() const
		{
			int total = 0;
			for( typename ValueMapType::const_iterator itr = m_bins.begin(); itr != m_bins.end(); ++itr )
			{
				total += itr->second;
			}

			return total;
		}

		/*Histogram<T> getCumulativeCounts()
		{
			Histogram<T> cumulativeCounts;
			int currentCount = 0;
			for( typename map< T, int >::iterator itr = map< T, int >::begin(); itr != map< T, int >::end(); ++itr )
			{
				cumulativeCounts[ itr->first ] = currentCount;
				currentCount += itr->second;
			}

			return cumulativeCounts;
		}

		map< T, float > getPercentileMap()
		{
			Histogram<T> cumulativeCounts = getCumulativeCounts();
			int total = getTotal();
			map< T, float > percentileMap;
			for( typename map< T, int >::iterator itr = cumulativeCounts.begin(); itr != cumulativeCounts.end(); ++itr )
			{
				percentileMap[ itr->first ] = (float) itr->second / (float) total;
			}

			return percentileMap;
		}*/

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			//ar & boost::serialization::base_object< map< T, int > >( *this );
			ar & m_numBins & m_maxValues & m_values & m_bins;
		}

		Histogram<T> operator +=( Histogram<T>& rhs )
		{
			rhs.smooth();
			m_values.insert( rhs.m_bins.begin(), rhs.m_bins.end() );
			smooth();
			return *this;
		}

		operator string() const
		{
			stringstream o;
			o << "(";
			for( typename ValueMapType::const_iterator itr = m_bins.begin(); itr != m_bins.end(); ++itr )
				o << " " << itr->first << ":" << itr->second;
			o << " )";
			return o.str();
		}

	//protected:
		size_t m_numBins;
		size_t m_maxValues;
		ValueMapType m_values;
		ValueMapType m_bins;
	};
}

namespace std
{
	template< class T >
	ostream& operator<< ( ostream& o, const Histogram<T>& rhs )
	{
		return o << (string) rhs;
	}
}

#endif
