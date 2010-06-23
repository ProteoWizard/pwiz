//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.Text;

namespace IDPicker
{
	public class AlphaIndex
	{
		public AlphaIndex()
		{
			m_str = new StringBuilder("A");
			m_index = 0;
		}

		public AlphaIndex( string a )
		{
			m_str = new StringBuilder(a);
			m_index = 0;
		}

		public AlphaIndex( int n )
		{
			m_index = n;
			m_str = new StringBuilder( "A" );
			for( ; n > 0; --n )
				incrementChar( m_str.Length-1 );
		}

		public static AlphaIndex operator++( AlphaIndex i )
		{
			++i.m_index;
			i.incrementChar( i.m_str.Length - 1 );
			return i;
		}

		public static implicit operator AlphaIndex( string a )
		{
			AlphaIndex rv = new AlphaIndex(a);
			return rv;
		}

		public static implicit operator string( AlphaIndex i )
		{
			string rv = i.m_str.ToString();
			return rv;
		}

		public static implicit operator int( AlphaIndex i )
		{
			return i.m_index;
		}

		private void incrementChar( int i )
		{
			if( m_str[i] == 'Z' )
			{
				m_str[i] = 'A';
				if( i == 0 )
					m_str.Append( 'A' );
				else
					incrementChar( i - 1 );
			} else
				m_str[i] = Convert.ToChar( Convert.ToInt32( m_str[i] ) + 1 );
		}

		private int m_index;
		private StringBuilder m_str;
	}
}
