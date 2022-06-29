/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class TextUtilTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestEncryptString()
        {
            const string testString1 = "TestString1";
            const string testString2 = "TestString2";
            string encrypted1 = TextUtil.EncryptString(testString1);

            byte[] bytes1 = Convert.FromBase64String(encrypted1);
            CollectionAssert.AreNotEqual(bytes1, Encoding.UTF8.GetBytes(testString1));

            string decrypted1 = TextUtil.DecryptString(encrypted1);
            Assert.AreEqual(testString1, decrypted1);

            string encrypted2 = TextUtil.EncryptString(testString2);
            Assert.AreNotEqual(encrypted1, encrypted2);
        }

        [TestMethod]
        public void TestDecryptGarbage()
        {
            const string garbageString = "garbage";
            AssertEx.ThrowsException<FormatException>(delegate 
            {
                TextUtil.DecryptString(garbageString);
            });
            AssertEx.ThrowsException<CryptographicException>(delegate 
            {
                TextUtil.DecryptString(Convert.ToBase64String(Encoding.UTF8.GetBytes(garbageString)));
            });
            Assert.AreEqual(garbageString, TextUtil.DecryptString(TextUtil.EncryptString(garbageString)));
        }

        [TestMethod]
        public void TestCommonPrefixAndSuffix()
        {
            string[] baseStrings = {"mediummer", "much, much longer", string.Empty, "short"};
            const string fixText = "1234567890";
            const int len1 = 3;
            Assert.AreEqual(fixText.Substring(0, len1), AddPrefix(baseStrings, fixText, len1).GetCommonPrefix());
            Assert.AreEqual(fixText.Substring(fixText.Length - len1), AddSuffix(baseStrings, fixText, len1).GetCommonSuffix());
            Assert.AreEqual(string.Empty, baseStrings.GetCommonPrefix());
            Assert.AreEqual(string.Empty, baseStrings.GetCommonSuffix());
            var base2 = baseStrings.Take(2).ToArray();
            Assert.AreEqual("m", base2.GetCommonPrefix());
            Assert.AreEqual("er", base2.GetCommonSuffix());
            Assert.AreEqual(string.Empty, base2.GetCommonPrefix(2));
            Assert.AreEqual(string.Empty, base2.GetCommonSuffix(3));
            const int len2 = 6;
            Assert.AreEqual(fixText.Substring(0, len2), AddPrefix(base2, fixText, len2).GetCommonPrefix(len2));
            Assert.AreEqual(fixText.Substring(fixText.Length - len2), AddSuffix(base2, fixText, len2).GetCommonSuffix(len2));
            Assert.AreEqual(string.Empty, AddPrefix(base2, fixText, len2).GetCommonPrefix(len2+1));
            Assert.AreEqual(string.Empty, AddSuffix(base2, fixText, len2).GetCommonSuffix(len2+1));
        }

        private IEnumerable<string> AddPrefix(string[] baseStrings, string fixText, int i)
        {
            foreach (string baseString in baseStrings)
                yield return fixText.Substring(0, i++) + baseString;
        }

        private IEnumerable<string> AddSuffix(string[] baseStrings, string fixText, int i)
        {
            foreach (string baseString in baseStrings)
                yield return baseString + fixText.Substring(fixText.Length - i, i++);
        }

        [TestMethod]
        public void TestNaturalSort()
        {
            var orderedSample = new List<string>() 
                {   "0NFwA","1NvWw","1U2t6","1uBYH","2E1fK","2V6La","3pxza","3sVDq","4tuzE","4VK2-","04ZaO","4ztTB", 
                    "5QRmn","5Zcd7","6hkS_","6qvCh","6sAzR","6-v2h","7Z25C","8fMsb","8HRzI","8wqJy","9jz1y","_thKv",
                    "a5E6p","a736Q","aL4lL","alpXu","B-2ag","BRrbj","bzUgj","c8qdT","CdaAF","CDk8I","Cdm0k","CFYgZ",
                    "D9Xdy","dP8a5","e9teS","eibUe","EMNkm","EsE4z","evAra","eZUyJ","f1A-B","fB6mW","F-oT2","FUjdN",
                    "GCtkn","gLHOp","gNbrM","Go6jE","gRm0t","Gw_5C","gxuE_","GYmXe","GyquW","GZw_D","h7k2O","h7L2H",
                    "H5a9A","H6KDQ","H6oOL","h_u-u","hcFa4","hhtYn","HNKWG","HOmz9","hp_4Y","hTa-9","igDjW","IqHG0",
                    "IXRRk","j0OXu","J6fuV","jnLi3","K6rl0","kCFaa","kIcKi","KS0Ua","KxR_m","Ky9Ef","LHiBw","lMdGG",
                    "ls8Vb","lSOc_","lWk5c","mE9Bc","mKppU","mpSNi","mWsW_","naLxD","ne-LG","NhsZW","NPk3-","nsoxE",
                    "Nxsqw","o6S3j","Ol8lY","oRQN_","otCNq","OXNJo","p5-zT","pc8rL","PEpSv","pj_FZ","PQWQ0","pseOl",
                    "pxiUY","qe7ib","QGkFc","QgYG9","QOi50","QwrRK","QWwUL","qZwkK","Rhsrt","rlw8H","-RWWt","sna_M",
                    "SwvOY","TachS","thxs7","U4sCZ","-uG-g","UIQF2","UNGYS","UPLhZ","USZgP","uz-Oq","ve7Ml","vlpJ-",
                    "vscw3","W2ffg","waINu","wAYVn","wcnac","wgnCt","Whe2M","WHs9b","wj-Sy","woie2","WOrKF","XfWhr",
                    "XfY9w","xlt5k","XPCHC","XxgDy","Zdrnb","zXdQ1"
                };

            var orderSampleStressTest = new List<string>()
            {
                "0aJdm", "0dAx8", "0gK4B", "0j_7m", "0LM7X", "0RBi2", "0S6Lx", "00Wv0", "0yIXs", "1c54a", "1eALG",
                "1F8Zo", "1fhQq", "1Guoe", "1H-un", "1idi4", "1iSYF", "1lyBg", "1MaiK", "1mVak", "1nS4b", "1NVXs",
                "1sEAM", "1vxz4", "1Wh3C", "1ZriP", "2_fCg", "2_IR6", "2_xrO", "2CJak", "2eW6q", "2f5Y7", "2FV_c",
                "2GqxW", "2h34K", "2H82P", "2Hi7I", "2jRHl", "2mqnQ", "2qS09", "2sdEJ", "2sTQn", "2t6K8", "2Tkkc",
                "2xOqG", "3CoiN", "3iQhM", "3kHin", "3Mqus", "3oX2-", "3-QS7", "3r5nY", "3thfz", "3W3qL", "3zaP5",
                "4C6Y5", "04g7Y", "4gAPt", "4GKQ1", "4l67F", "4P3K6", "4Q8mq", "4RMqU", "4rQZy", "4tBvu", "4TKGm",
                "4TZCD", "4woQn", "4yGpj", "4ZS7V", "5CAbI", "5cDzz", "5D5_M", "5EEh8", "5exq9", "5ghAD", "5LcQC",
                "5PBY5", "5pVjD", "5uYVN", "5YHzJ", "5zTLK", "5ZWdI", "6BFS8", "6LlRf", "6RBE7", "6VyzH", "6wbdn",
                "6XXz_", "6Z0cE", "6zHdl", "6Zieu", "7cmln", "7cTK2", "7cUuL", "7ehF5", "7FdxP", "7g_5F", "7G_Qm",
                "07GMA", "7Jj5b", "7LDbv", "7mhol", "7nKMA", "7Nn2B", "7PDlw", "7qQfz", "7rVYb", "7SeFm", "7TRZ1",
                "7um2b", "7W3lM", "7zD2s", "8bihR", "8bjC2", "8cOwj", "8E3mq", "8ePDP", "8eQi3", "8gyS3", "8HNR0",
                "08hye", "8Mdyh", "8mn1R", "8q4Ir", "8QK9-", "8Tft6", "8TTj3", "8v1q7", "8vD-4", "8wOxc", "8yHT9",
                "9-5WX", "9a17a", "9b8l_", "9b-J7", "9eYhr", "9FXr2", "9hBT5", "9HKBi", "9LB44", "9LP_G", "9PkVk",
                "9reUU", "9Wdl7", "9ytGi", "17Ojl", "20G0W", "20k5f", "23Qv0", "25Aet", "034UZ", "36aHq", "40ERX",
                "40fYQ", "43e_I", "49MAr", "58A7l", "58pHe", "62Qvv", "66EXt", "69dfg", "69EsO", "82IdY", "88knC",
                "90ZGR", "95yL9", "206yg", "267G_", "357Lx", "553Pa", "-3Dl4", "-5_bY", "-6f-6", "-8mca", "-8qkJ",
                "_1pdI", "_08d-", "_8HNr", "-_7My", "_bl5Z", "_f_NX", "_FqJm", "_hA-7", "_i6hD", "_I0Vw", "_JwZm",
                "_lRXu", "_M0nk", "_ozP-", "_PBKr", "_WjIM", "_YnGT", "_zSWO", "A0iJX", "A0QGC", "aCKh4", "aFVbf",
                "ahE9S", "aHPA_", "ajcr7", "AL2TU", "AmWN-", "aN2a-", "aO5-F", "aO8-j", "AR0T0", "aRrvd", "arsbN",
                "aSNgO", "AsqLX", "a-TvG", "avVpl", "aW2DY", "AWa3m", "axC-L", "aYp5r", "a-YvR", "AyWYM", "b9s4v",
                "b9vMc", "B4R3w", "B85C-", "-BAsh", "bBs3C", "BCMcu", "be6BM", "bEhRG", "bgGUv", "bhbJs", "bHv3e",
                "bi4T8", "BkDgK", "BLU9m", "bMce4", "Bnsal", "bNsP7", "BNV2G", "Bogdb", "Bs9x_", "bsXG1", "BUCTk",
                "BUhsx", "bvM8c", "Bvmf1", "bwFBH", "-By5e", "BYFPD", "byt8M", "bZcOx", "bZYSN", "c7yUJ", "C59ka",
                "cB4he", "CB3hc", "Cdl54", "cEFc7", "CHyO3", "ci6T1", "C-iKk", "cJjZF", "CK7Id", "cMfpJ", "cmHEZ",
                "CNAhr", "coGtb", "cOuXA", "CowhC", "CpBG3", "CpsrY", "Cq0v4", "c-QHA", "CqvGw", "cQvl0", "CsN7V",
                "CsnER", "csPxU", "cSQ5F", "cUHCY", "CUM4F", "cvSI9", "cydBu", "CZcpP", "CZNap", "d0WRB", "d5py0",
                "d6fCY", "d7AvW", "d8hYc", "D0BR9", "D1OQW", "D05bc", "D7wSo", "dB3Ji", "DB9J6", "DDfnH", "ddHnN",
                "dfw5X", "Dg0qQ", "dh8pi", "DhUL6", "dM9Kn", "dme_b", "DMRhB", "DOSvg", "DQVYB", "Dr8yQ", "DSVV4",
                "dSWQh", "DtoAy", "DtxxC", "duHIY", "DuwiZ", "DvPhJ", "dvSll", "dX88S", "dxgUf", "dxrWX", "e2Fd_",
                "e57_j", "E2D26", "E8CZW", "e_-d9", "EA9af", "Eb_hU", "EDsKR", "ee-I3", "eejVe", "EeZYx", "EfjoA",
                "EGYOe", "ejuHn", "ElhuC", "Envok", "eOkTW", "eoTFt", "eP33C", "eqNDJ", "ERJ7t", "ESM1A", "eTaSI",
                "EuEc5", "EvTr4", "ex9KB", "exM_y", "eymBb", "ezbby", "f3fV3", "f61RY", "F5Z9K", "F7RF1", "fbeV5",
                "FD925", "FE5fQ", "feOq5", "ff5A_", "FGaDO", "FgrxS", "fhd_P", "FHxHC", "FIdY9", "fjSuy", "fkX94",
                "FMra3", "FNiVT", "fo37F", "FOdQu", "FoRRD", "F-pDp", "fQQRY", "Fricg", "fSJ6i", "fSr0X", "FTO2K",
                "Fu9Rs", "FX_yN", "fxKJ1", "g2bTh", "g3i6X", "G0J0w", "gDRsl", "gELFg", "GFbjI", "GIzg-", "gJneB",
                "GJOKu", "GjVbR", "gMD5F", "GmUrI", "gO06G", "GPXaw", "gQU5B", "gSUyR", "GSVBp", "Gt1yx", "GTp0s",
                "guEij", "GweEF", "GwLYJ", "Gxbko", "GXxWN", "h3Py7", "h4G07", "H5tWL", "H8grD", "H8rfS", "H45kO",
                "H_K5F", "hABHg", "hBD6f", "hc78k", "hCqwE", "H-dXx", "hEPdX", "hIEGl", "Hk_Pu", "HkbbT", "hLpEb",
                "hNnXT", "hNyK1", "hOMkl", "HOqJs", "HpJS4", "HpNdm", "hPuYR", "hRSoX", "Htiei", "htUN1", "HuYEf",
                "HyfnH", "HyXsq", "hz7FP", "i3X2T", "i6JcT", "i9aBt", "I8zH1", "I1540", "IaraE", "iBbSh", "IBFus",
                "iCE-H", "IGBmF", "igtmw", "iGU9W", "iiGLY", "iih5S", "IJevQ", "IqJo4", "Iqu-7", "IscMW", "iSnhn",
                "IVIOR", "IWbnA", "IWLUi", "iwvUI", "iY1XY", "IyKwX", "IYyDB", "j0WiS", "j4rT1", "J3Spb", "j_C2i",
                "JAKDq", "jaRSR", "jAtmN", "jcbij", "JCcRV", "jCndN", "JDRln", "JEQDP", "Jf3hL", "JFg3s", "Jfo16",
                "JG9Qf", "jhq9t", "jiMjw", "JJJo1", "jKPV1", "JlIqH", "JmGDD", "JmnSV", "joLig", "JRbIh", "JTvux",
                "jU_7h", "-juCa", "juvFl", "jUWvE", "jVwfb", "jWUNN", "jwzT6", "jYFsB", "jywpm", "jzEuH", "k0jKL",
                "K0dRR", "K7KO3", "KbhWw", "kBYoh", "KdpSp", "Ke4Jn", "KEraO", "kEuhV", "kfliS", "KFmVx", "kgaYk",
                "Kgl12", "KhLxN", "KI0l0", "KkJgS", "kmZhG", "KN151", "knJhI", "KNksk", "kshjg", "KshQG", "kv5HC",
                "KWZxN", "kZYbA", "l7T_C", "l75ss", "L5kQw", "L7Dk8", "L9gQh", "laTNj", "lb2Z7", "LBr1p", "ldGJN",
                "LdrNC", "ldSJB", "leJOb", "LH8pD", "lhtR3", "LI_mw", "l-khw", "Llywm", "lm7ob", "lMNxc", "LNBAx",
                "lO0lg", "Lo6Vb", "lodif", "loSgW", "LPMJt", "LqeiO", "lTjxc", "Lu8il", "ly4ej", "lyR9u", "lYZlV",
                "LZBbw", "LZgLs", "LzlLp", "m3nxu", "m3RLi", "m4E4c", "m8itM", "M1NBx", "M2onH", "M7b-Y", "M7nGx",
                "M07xc", "mA2UV", "MAR-3", "MAT_h", "mbOhD", "MBQGU", "mcFLw", "Md0-C", "meHKr", "MF-jX", "MGqKp",
                "MHOA6", "MkBrt", "ML0rc", "mLhwt", "M-Mtq", "mNCyu", "mq4R5", "MRuMZ", "MrVnM", "MsiJR", "mSZcY",
                "mTxvB", "MWTa1", "M-WUo", "mXfSt", "mxNr1", "MX-Yq", "My13z", "mZLHx", "n2MRq", "N7U4n", "N12Sj",
                "n-_i9", "NaxoJ", "NbF-u", "nBiC1", "nBoHo", "ncV0O", "NdZmd", "-NhkU", "nhMEq", "Ni1Vq", "NID2U",
                "nIQg9", "nIWpU", "nk6E7", "nl9Lm", "nQGX3", "N-QHV", "nqJJw", "nR3bU", "Nrzka", "NSI7u", "n-sY8",
                "Nuw0U", "Nvoma", "Nw5NT", "nwfah", "nwJ2f", "nWrVL", "NWue5", "NyekB", "NzDw9", "o4s57", "o8K-R",
                "O1Z28", "O37cT", "OA18A", "oCehe", "OFGv9", "OGqxd", "OHyOC", "oJgsv", "OjJkO", "omtsl", "Oo8DD",
                "ooeQn", "opR4H", "OPwqm", "osCaa", "OU2Nm", "ovmO4", "OWGDd", "oXR9s", "OyAQL", "oYO-e", "Oz7Po",
                "oz_4R", "oZfes", "Ozn1Y", "P2CLn", "P5M3Y", "P7nio", "P83jL", "P_fQk", "paGAb", "pawmX", "p-Eyc",
                "pEYkb", "PGA5s", "Ph6vD", "pHFG7", "phogp", "piM2N", "PiOX8", "PIVqw", "pkgAy", "pKUgS", "PnkBO",
                "PnMNX", "PNqnm", "PNW4l", "pO9tn", "poiC5", "POn0u", "ppOlG", "Pq9bC", "pqANy", "PQApM", "PT-5A",
                "PVd5z", "PZcRC", "Q6X8E", "Q8yRO", "qC8MX", "qC80q", "qdJUE", "qDWgO", "QeDaU", "qELrG", "QffVI",
                "qGXYC", "qhxpr", "QHZNn", "QjTJ9", "QKSN1", "qMZl0", "QNkw6", "qNThs", "qOpAw", "QQlHo", "QqPYv",
                "-qsu8", "qSYbA", "QtlMD", "qVHOo", "qWMe_", "QWVV7", "qWWeF", "QyA53", "r2U32", "r9baj", "R3Npl",
                "R8NRw", "R-9tw", "rAv8K", "RcOVh", "RDrBT", "RDxfj", "Rgf7v", "RgX2V", "RhGak", "RHuC2", "rhvbR",
                "RiJUy", "rKBjB", "rkbvv", "rkfQI", "rL7oT", "rLL6t", "rn8i8", "RNLar", "ROcwU", "rPeOk", "-RrlA",
                "RRS7e", "RtIFu", "RTl2L", "Rur-i", "rveQ3", "rvQfl", "rVxej", "Ry3v7", "rYq7J", "s0hGb", "s3HBn",
                "S0Uut", "S1FBc", "S2mEF", "S2QGv", "S6vzd", "S9B8l", "S_Cag", "sAU6F", "SBrbJ", "sBrJT", "s-d7C",
                "-sfqs", "sI5SG", "SIW2y", "six2m", "SkGrK", "SM_9d", "SMP4O", "soBNX", "SPkR6", "sprQw", "sqVpD",
                "srzYs", "STpr1", "SUGz6", "Svw8U", "SWIW9", "syhEY", "SyjtP", "szFB5", "szMUT", "t7aSK", "t9YtG",
                "T1d-m", "T3KhJ", "T3oqE", "T5Hgq", "T093H", "T_gxI", "TAAbp", "taDt_", "tchMy", "Tgmug", "tgXKx",
                "TH_Wu", "ThIN1", "tiZNw", "TLZRY", "tNe7F", "TnG1f", "tNJV-", "tP0lD", "tPNqI", "tPVBw", "tSW4s",
                "ttNVz", "ttVVL", "TVB1P", "TVzX7", "tWw6J", "Tx214", "u0sZU", "u1LRx", "u5oh0", "u5yMi", "u17mQ",
                "U_aDj", "UAERJ", "UaXHu", "Ub2aG", "ubMlW", "u-d09", "ue7uO", "uEd03", "UEgaC", "uf09S", "uFkkO",
                "UJt7P", "UjY-6", "uk2zH", "UN_mf", "uNZ49", "UoTD9", "uP0_z", "UpBUn", "UPTFO", "uREtq", "urPvG",
                "uRRfr", "usQpl", "Usuof", "USZi3", "Ut8mt", "uuL71", "uus4v", "uw7Qx", "UwQiJ", "uXJ3Z", "uYGFC",
                "uygkU", "Uznaa", "v0Bii", "V3c_T", "vAA99", "vBJBq", "vDfH7", "vdsCT", "Ve6Ta", "VEoAO", "Vf6ec",
                "vfINc", "VipmQ", "VKQTr", "vKRM9", "Vm7DS", "vNfbo", "vnOtG", "vnyz6", "Vo1-6", "vp4ad", "vp_WA",
                "vPjSH", "vpr5l", "vqlAw", "vssyk", "VSxLB", "vt3g_", "vTjAk", "vy-8M", "Vz811", "vZkWO", "w0W0o",
                "w3bah", "w5m84", "w19Ht", "w430-", "W5TN-", "W7_Gg", "W8XKm", "W_17N", "W_zAH", "WadIY", "w-ayL",
                "wb9eC", "Wb1iY", "Wc-Se", "WCwth", "Wcyyt", "wD-Mr", "WEMDm", "WfG49", "wH7z5", "wHeOF", "wkzjo",
                "wlz9S", "WObZ2", "WohAe", "wp9GI", "wSEvL", "wSmeO", "WstBf", "wTpm1", "WvIMM", "WXnQH", "Wzi2E",
                "x8W-O", "X7bAs", "X78mS", "X_-fO", "x_wnm", "xcEvh", "xcmmi", "xcqX_", "xdiAw", "xdnqq", "XEgEz",
                "xESjA", "xfkcU", "xgThP", "-XI_N", "XiSQ3", "XJ0Ij", "xJl17", "XjWWj", "XlGMw", "XMgjD", "XPxAQ",
                "XQkWA", "xRiK2", "XSkvk", "XUO_k", "XWPlc", "Xx0hO", "XXA97", "XZ_VJ", "xZfiP", "y7C8P", "y7VP8",
                "Y6Egb", "Y6KYN", "Y8P0V", "Y8tqk", "YBhcq", "YbyUR", "yE59F", "yewLE", "YFavT", "yFJtl", "YFnAx",
                "yIaqR", "YJ_Bw", "ykUfT", "Ylp6y", "ymA2t", "yPNFs", "-yqQU", "YS_68", "YsfqU", "YtEmi", "ytYCY",
                "YtZZ8", "yUzgJ", "YZ09K", "yZeZY", "Yzlgb", "z1hKB", "z7DVy", "Z8FV9", "Z8qKD", "Z96lA", "Z-0NM",
                "zAu-r", "ZBE3X", "ZbPja", "zbUz-", "ZDjTb", "zFjSv", "zFunl", "zIW8u", "zjUA6", "zL3Mn", "ZLImV",
                "ZLpfb", "zMVI0", "zOpUQ", "Zuzaq", "ZvFIb", "zxWHd", "zz0Pq", "ZzIxa", "zzlPp", "ZZPVw"
            };


            SortAndTest(orderedSample);
            SortAndTest(orderSampleStressTest);

            void SortAndTest(List<string> ordered)
            {
                List<string> misOrdered = Shuffle(ordered); // Shuffle

                AssertEx.AreEqual(misOrdered.Distinct().Count(), ordered.Count, "Number of unique elements: " + misOrdered.Distinct().Count() + " Number of elements: " + misOrdered.Count);

                misOrdered.Sort((x, y) => NaturalComparer.Compare(x, y)); // Naturally sort

                misOrdered.ForEach(Console.WriteLine);

                for (int i = 0; i < ordered.Count; i++)
                {
                    AssertEx.AreEqual(ordered[i], misOrdered[i], string.Format("Test Natural is not sorting in the correct order: at position {0}: {1} vs {2}", i, ordered[i], misOrdered[i]));
                }
            }

            List<string> Shuffle(List<string> inOrder)
            {
                List<string> misOrdered = new List<string>(inOrder); // Copy list
                Random rand = new Random();
                for (int a = 0; a < misOrdered.Count; a++)
                {
                    int loc = rand.Next(misOrdered.Count);
                    (misOrdered[loc], misOrdered[a]) = (misOrdered[a], misOrdered[loc]);
                }
                return misOrdered;
            }
        }
    }
}
