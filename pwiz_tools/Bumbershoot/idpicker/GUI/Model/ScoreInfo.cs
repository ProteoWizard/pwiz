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

namespace IdPickerGui.MODEL
{
    public class ScoreInfo : ICloneable, IComparable<ScoreInfo>
    {
        private string scoreName;
        private Single scoreWeight;

        public string ScoreName
        {
            get { return scoreName; }
            set { scoreName = value; }
        }

        public Single ScoreWeight
        {
            get { return scoreWeight; }
            set { scoreWeight = value; }
        }

        public object Clone()
        {
            try
            {
                return new ScoreInfo(ScoreName, ScoreWeight);
            }
            catch (Exception exc)
            {
                throw exc;
            }
        }


        public ScoreInfo()
        {

        }

        public ScoreInfo(string name, Single weight)
        {
            try
            {
                ScoreName = name;

                ScoreWeight = weight;
            }
            catch (Exception)
            {
                throw new Exception("Error building score weight.");
            }
        }

        public ScoreInfo(string name, string weight)
        {
            try
            {
                ScoreName = name;

                ScoreWeight = Convert.ToSingle(weight);
            }
            catch (Exception)
            {
                throw new Exception("Score weight must be a numeric value.");
            }
        }

        public override string ToString()
        {
            return scoreName + ", " + ScoreWeight.ToString(); 
        }

        public int CompareTo(ScoreInfo other)
        {
            return ToString().CompareTo( other.ToString() );
        }
    }
}
