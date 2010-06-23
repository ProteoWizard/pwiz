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
    public class ModOverrideInfo : ICloneable, IComparable<ModOverrideInfo>
    {
        
        private string name;
        private Single weight;
        private ModType type;
        

        public string Name
        {
            get { return name; }
            set { name = value; }
        }
        public Single Mass
        {
            get { return weight; }
            set { weight = value; }

        }
        public ModType Type
        {
            get { return type; }
            set { type = value; }

        }


        public ModOverrideInfo()
        {

        }

        public ModOverrideInfo(string name, string weight, int type)
        {
            try
            {
                Name = name;
                Type = new ModType(type);
                Mass = Convert.ToSingle(weight);
            }
            catch (Exception)
            {
                throw new Exception("Mod error.");
            }

        }

        public ModOverrideInfo(string name, Single weight, ModType modType)
        {
            try
            {
                Name = name;
                Type = modType;
                Mass = weight;
            }
            catch (Exception)
            {
                throw new Exception("Mod error.");
            }

        }

        public ModOverrideInfo(string name, string weight, string typeDesc)
        {
            try
            {
                Name = name;
                Type = new ModType(typeDesc);
                Mass = Convert.ToSingle(weight);
            }
            catch (Exception)
            {
                throw new Exception("Mod error.");
            }

        }

        public override string ToString()
        {
            return Name + ", " + Mass.ToString() + ", " + Type.ModTypeDesc;
            
        }

        public int CompareTo(ModOverrideInfo other)
        {
            return ToString().CompareTo( other.ToString() );
        }

        public object Clone()
        {
            try
            {
                return new ModOverrideInfo(Name, weight, Type);
            }
            catch (Exception exc)
            {
                throw exc;
            }
        }
        
    }
}
