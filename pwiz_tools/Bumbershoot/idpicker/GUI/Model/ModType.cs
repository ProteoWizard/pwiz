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
    public class ModType : ICloneable
    {
        private int modTypeValue;
        private string modTypeDesc;

      

        /// <summary>
        /// Mod type as int (Distinct = 0, Indistinct = 1)
        /// </summary>
        public int ModTypeValue
        {
            get { return modTypeValue; }
            set { modTypeValue = value; }
        }
        
        /// <summary>
        /// Mod type as string ('Distinct', 'Indistinct')
        /// </summary>
        public string ModTypeDesc
        {
            get { return modTypeDesc; }
            set { modTypeDesc = value; }
            
        }

        

        /// <summary> 
        /// Default
        /// </summary>
        public ModType()
        {
            ModTypeValue = -1;
            ModTypeDesc = string.Empty;
        }

        public ModType(int type, string desc)
        {
            ModTypeValue = type;
            ModTypeDesc = desc;
        }
        
        /// <summary>
        /// Create new ModType with ModTypeValue int.
        /// </summary>
        /// <param name="type"></param>
        public ModType(int type)
        {
            ModTypeValue = type;

            switch (type)
            {
                case 0:
                    ModTypeDesc = "Distinct";
                    break;
                case 1:
                    ModTypeDesc = "Indistinct";
                    break;
                default:
                    ModTypeDesc = "Unknown";
                    break;
            }

        }
        
        /// <summary>
        /// Create new ModType with ModTypeDesc string.
        /// </summary>
        /// <param name="typeDesc"></param>
        public ModType(string typeDesc)
        {
            ModTypeDesc = typeDesc;

            switch (typeDesc)
            {
                case "Distinct":
                    ModTypeValue = 0;
                    break;
                case "Indistinct":
                    ModTypeValue = 1;
                    break;
                default:
                    ModTypeValue = -1;
                    break;
            }


        }


        public object Clone()
        {
            try
            {
                return new ModType(ModTypeValue, ModTypeDesc);
            }
            catch (Exception exc)
            {
                throw exc;
            }
        }
    }
}
