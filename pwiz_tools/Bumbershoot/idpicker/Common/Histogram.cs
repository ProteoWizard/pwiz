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
	public class Histogram<T> : Map< T, int > where T: IComparable<T>
	{
		public Histogram<T> getCumulativeCounts()
		{
			Histogram<T> cumulativeCounts = new Histogram<T>();
			int currentCount = 0;
			foreach( Map< T, int >.MapPair itr in this )
			{
				cumulativeCounts[ itr.Key ] = currentCount;
				currentCount += itr.Value;
			}

			return cumulativeCounts;
		}

		public int getTotal()
		{
			int total = 0;
			foreach( Map< T, int >.MapPair itr in this )
			{
				total += itr.Value;
			}

			return total;
		}
	}
}
