using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProteomeDb.API
{
    public static class Enzymes
    {
        public static Protease TRYPSIN = new Protease("KR", "P");
        public static Protease TRYPSIN_P = new Protease("KR", "");
        public static Protease TRYPSIN_K = new Protease("K", "P");
        public static Protease TRYPSIN_R = new Protease("R", "P");
        public static IDictionary<String,IProtease> AllEnzymes()
        {
            return new SortedDictionary<string, IProtease>
                       {
                           {"Trypsin", TRYPSIN},
                           {"Trypsin/P", TRYPSIN_P},
                           {"Trypsin/K", TRYPSIN_K},
                           {"Trypsin/R", TRYPSIN_R},
                       };
        }
    }
}
