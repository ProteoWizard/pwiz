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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
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

        /// <summary>
        /// Test for natural sort. Randomly shuffles sorted lists of stings and then sorts them again in order to ensure
        /// consistency and accuracy in establishing the original natural sort order. This test is run both on a small list
        /// and a larger stress test list to demonstrate consistency across a diverse range of strings for this sort.
        /// The longer stress test list additionally ensures that the random shuffle of the list will not simply shuffle
        /// into the correct order.

        /// </summary>
        [TestMethod]
        public void TestNaturalSort()
        {   
            //Short sort test 
            var orderedSample = new List<string>() 
            {   
                "1NvWw","1U2t6","1uBYH","2E1fK","2V6La","3pxza","3sVDq","4tuzE",
                "4VK2-","4ztTB","5QRmn","5Zcd7","6hkS_","6qvCh","6sAzR","7Z25C","8fMsb","8HRzI",
                "8wqJy","9jz1y","a5E6p","a736Q","aL4lL","alpXu","B-2ag","BRrbj","bzUgj","c8qdT","CdaAF",
                "CDk8I","Cdm0k","CFYgZ","D9Xdy","dP8a5","e9teS","eibUe","EMNkm","EsE4z","evAra","eZUyJ",
                "f1A-B","fB6mW","F-oT2","FUjdN","GCtkn","gLHOp","gNbrM","Go6jE","gRm0t","Gw_5C","gxuE_",
                "GYmXe","GyquW","GZw_D","h_u-u","H5a9A","H6KDQ","H6oOL","h7k2O","h7L2H","hcFa4","hhtYn",
                "HNKWG","HOmz9","hp_4Y","hTa-9","igDjW","IqHG0","IXRRk","j0OXu","J6fuV","jnLi3","K6rl0",
                "kCFaa","kIcKi","KS0Ua","KxR_m","Ky9Ef","LHiBw","lMdGG","ls8Vb","lSOc_","lWk5c","mE9Bc",
                "mKppU","mpSNi","mWsW_","naLxD","ne-LG","NhsZW","NPk3-","nsoxE","Nxsqw","o6S3j","Ol8lY",
                "oRQN_","otCNq","OXNJo","p5-zT","pc8rL","PEpSv","pj_FZ","PQWQ0","pseOl","pxiUY","qe7ib",
                "QGkFc","QgYG9","QOi50","QwrRK","QWwUL","qZwkK","Rhsrt","rlw8H",
                "Skyline-64_1_4_0_4421.zip","Skyline-64_1_4_0_4422.zip","Skyline-64_2_5_0_5675.zip","Skyline-64_20_1_0_155.zip",
                "sna_M","SwvOY",
                "TachS","thxs7","U4sCZ","UIQF2","UNGYS","UPLhZ","USZgP","uz-Oq","ve7Ml","vlpJ-",
                "vscw3","W2ffg","waINu","wAYVn","wcnac","wgnCt","Whe2M","WHs9b","wj-Sy","woie2","WOrKF",
                "XfWhr","XfY9w","xlt5k","XPCHC","XxgDy","Zdrnb","zXdQ1"
            };

            //Long sort stress test
            var orderSampleStressTest = new List<string>()
            {
                
                "1c54a","1eALG","1F8Zo","1fhQq","1Guoe","1H-un","1idi4",
                "1iSYF","1lyBg","1MaiK","1mVak","1nS4b","1NvWw","1NVXs","1sEAM","1U2t6","1uBYH","1vxz4","1Wh3C",
                "1ZriP","2_fCg","2_IR6","2_xrO","2CJak","2E1fK","2eW6q","2f5Y7","2FV_c","2GqxW","2h34K","2H82P",
                "2Hi7I","2jRHl","2mqnQ","2qS09","2sdEJ","2sTQn","2t6K8","2Tkkc","2V6La","2xOqG","3CoiN",
                "3iQhM","3kHin","3Mqus","3oX2-","3pxza","3-QS7","3r5nY","3sVDq","3thfz","3W3qL","3zaP5",
                "4C6Y5","4gAPt","4GKQ1","4l67F","4P3K6","4Q8mq","4RMqU","4rQZy","4tBvu","4TKGm","4tuzE",
                "4TZCD","4VK2-","4woQn","4yGpj","4ZS7V","4ztTB","5CAbI","5cDzz","5D5_M","5EEh8","5exq9",
                "5ghAD","5LcQC","5PBY5","5pVjD","5QRmn","5uYVN","5YHzJ","5Zcd7","5zTLK","5ZWdI","6BFS8",
                "6hkS_","6LlRf","6qvCh","6RBE7","6sAzR","6-v2h","6VyzH","6wbdn","6XXz_","6Z0cE","6zHdl","6Zieu",
                "7cmln","7cTK2","7cUuL","7ehF5","7FdxP","7g_5F","7G_Qm","7Jj5b","7LDbv","7mhol","7nKMA",
                "7Nn2B","7PDlw","7qQfz","7rVYb","7SeFm","7TRZ1","7um2b","7W3lM","7Z25C","7zD2s","8bihR",
                "8bjC2","8cOwj","8E3mq","8ePDP","8eQi3","8fMsb","8gyS3","8HNR0","8HRzI","8Mdyh","8mn1R",
                "8q4Ir","8QK9-","8Tft6","8TTj3","8v1q7","8vD-4","8wOxc","8wqJy","8yHT9","9-5WX","9a17a",
                "9b8l_","9b-J7","9eYhr","9FXr2","9hBT5","9HKBi","9jz1y","9LB44","9LP_G","9PkVk","9reUU","9Wdl7",
                "9ytGi","17Ojl","20G0W","20k5f","23Qv0","25Aet","36aHq","40ERX","40fYQ","43e_I","49MAr",
                "58A7l","58pHe","62Qvv","66EXt","69dfg","69EsO","82IdY","88knC","90ZGR","95yL9","206yg","267G_",
                "357Lx","553Pa","A0iJX","A0QGC","a5E6p","a736Q","aCKh4","aFVbf","ahE9S","aHPA_","ajcr7","AL2TU",
                "aL4lL","alpXu","AmWN-","aN2a-","aO5-F","aO8-j","AR0T0","aRrvd","arsbN","aSNgO","AsqLX","a-TvG",
                "avVpl","aW2DY","AWa3m","axC-L","aYp5r","a-YvR","AyWYM","B-2ag","B4R3w","b9s4v","b9vMc","B85C-",
                "bBs3C","BCMcu","be6BM","bEhRG","bgGUv","bhbJs","bHv3e","bi4T8","BkDgK","BLU9m","bMce4",
                "Bnsal","bNsP7","BNV2G","Bogdb","BRrbj","Bs9x_","bsXG1","BUCTk","BUhsx","bvM8c","Bvmf1","bwFBH",
                "BYFPD","byt8M","bZcOx","bzUgj","bZYSN","c7yUJ","c8qdT","C59ka","CB3hc","cB4he","CdaAF",
                "CDk8I","Cdl54","Cdm0k","cEFc7","CFYgZ","CHyO3","ci6T1","C-iKk","cJjZF","CK7Id","cMfpJ","cmHEZ",
                "CNAhr","coGtb","cOuXA","CowhC","CpBG3","CpsrY","Cq0v4","c-QHA","CqvGw","cQvl0","CsN7V","CsnER",
                "csPxU","cSQ5F","cUHCY","CUM4F","cvSI9","cydBu","CZcpP","CZNap","D0BR9","d0WRB","D1OQW","D05bc",
                "d5py0","d6fCY","d7AvW","D7wSo","d8hYc","D9Xdy","dB3Ji","DB9J6","DDfnH","ddHnN","dfw5X","Dg0qQ",
                "dh8pi","DhUL6","dM9Kn","dme_b","DMRhB","DOSvg","dP8a5","DQVYB","Dr8yQ","DSVV4","dSWQh","DtoAy",
                "DtxxC","duHIY","DuwiZ","DvPhJ","dvSll","dX88S","dxgUf","dxrWX","e_-d9","E2D26","e2Fd_","E8CZW",
                "e9teS","e57_j","EA9af","Eb_hU","EDsKR","ee-I3","eejVe","EeZYx","EfjoA","EGYOe","eibUe","ejuHn",
                "ElhuC","EMNkm","Envok","eOkTW","eoTFt","eP33C","eqNDJ","ERJ7t","EsE4z","ESM1A","eTaSI","EuEc5",
                "evAra","EvTr4","ex9KB","exM_y","eymBb","ezbby","eZUyJ","f1A-B","f3fV3","F5Z9K","F7RF1","f61RY",
                "fB6mW","fbeV5","FD925","FE5fQ","feOq5","ff5A_","FGaDO","FgrxS","fhd_P","FHxHC","FIdY9","fjSuy",
                "fkX94","FMra3","FNiVT","fo37F","FOdQu","FoRRD","F-pDp","fQQRY","Fricg","fSJ6i","fSr0X",
                "FTO2K","Fu9Rs","FUjdN","FX_yN","fxKJ1","G0J0w","g2bTh","g3i6X","GCtkn","gDRsl","gELFg","GFbjI",
                "GIzg-","gJneB","GJOKu","GjVbR","gLHOp","gMD5F","GmUrI","gNbrM","gO06G","Go6jE","GPXaw","gQU5B",
                "gRm0t","gSUyR","GSVBp","Gt1yx","GTp0s","guEij","Gw_5C","GweEF","GwLYJ","Gxbko","gxuE_","GXxWN",
                "GYmXe","GyquW","GZw_D","H_K5F","h3Py7","h4G07","H5a9A","H5tWL","H6KDQ","H6oOL","h7k2O",
                "h7L2H","H8grD","H8rfS","H45kO","hABHg","hBD6f","hc78k","hcFa4","hCqwE","H-dXx","hEPdX","hhtYn",
                "hIEGl","Hk_Pu","HkbbT","hLpEb","HNKWG","hNnXT","hNyK1","hOMkl","HOmz9","HOqJs","hp_4Y","HpJS4",
                "HpNdm","hPuYR","hRSoX","hTa-9","Htiei","htUN1","HuYEf","HyfnH","HyXsq","hz7FP","i3X2T","i6JcT",
                "I8zH1","i9aBt","I1540","IaraE","iBbSh","IBFus","iCE-H","IGBmF","igDjW","igtmw","iGU9W","iiGLY",
                "iih5S","IJevQ","IqHG0","IqJo4","Iqu-7","IscMW","iSnhn","IVIOR","IWbnA","IWLUi","iwvUI","IXRRk",
                "iY1XY","IyKwX","IYyDB","j_C2i","j0OXu","j0WiS","J3Spb","j4rT1","J6fuV","JAKDq","jaRSR","jAtmN",
                "jcbij","JCcRV","jCndN","JDRln","JEQDP","Jf3hL","JFg3s","Jfo16","JG9Qf","jhq9t","jiMjw","JJJo1",
                "jKPV1","JlIqH","JmGDD","JmnSV","jnLi3","joLig","JRbIh","JTvux","jU_7h","juvFl","jUWvE",
                "jVwfb","jWUNN","jwzT6","jYFsB","jywpm","jzEuH","K0dRR","k0jKL","K6rl0","K7KO3","KbhWw","kBYoh",
                "kCFaa","KdpSp","Ke4Jn","KEraO","kEuhV","kfliS","KFmVx","kgaYk","Kgl12","KhLxN","KI0l0","kIcKi",
                "KkJgS","kmZhG","KN151","knJhI","KNksk","KS0Ua","kshjg","KshQG","kv5HC","KWZxN","KxR_m","Ky9Ef",
                "kZYbA","L5kQw","L7Dk8","l7T_C","L9gQh","l75ss","laTNj","lb2Z7","LBr1p","ldGJN","LdrNC","ldSJB",
                "leJOb","LH8pD","LHiBw","lhtR3","LI_mw","l-khw","Llywm","lm7ob","lMdGG","lMNxc","LNBAx","lO0lg",
                "Lo6Vb","lodif","loSgW","LPMJt","LqeiO","ls8Vb","lSOc_","lTjxc","Lu8il","lWk5c","ly4ej","lyR9u",
                "lYZlV","LZBbw","LZgLs","LzlLp","M1NBx","M2onH","m3nxu","m3RLi","m4E4c","M07xc","M7b-Y","M7nGx",
                "m8itM","mA2UV","MAT_h","mbOhD","MBQGU","mcFLw","Md0-C","mE9Bc","meHKr","MF-jX","MGqKp","MHOA6",
                "MkBrt","mKppU","ML0rc","mLhwt","M-Mtq","mNCyu","mpSNi","mq4R5","MRuMZ","MrVnM","MsiJR","mSZcY",
                "mTxvB","mWsW_","MWTa1","M-WUo","mXfSt","mxNr1","MX-Yq","My13z","mZLHx","n-_i9","n2MRq","N7U4n",
                "N12Sj","naLxD","NaxoJ","NbF-u","nBiC1","nBoHo","ncV0O","NdZmd","ne-LG","nhMEq","NhsZW",
                "Ni1Vq","NID2U","nIQg9","nIWpU","nk6E7","nl9Lm","NPk3-","nQGX3","N-QHV","nqJJw","nR3bU","Nrzka",
                "NSI7u","nsoxE","n-sY8","Nuw0U","Nvoma","Nw5NT","nwfah","nwJ2f","nWrVL","NWue5","Nxsqw","NyekB",
                "NzDw9","O1Z28","o4s57","o6S3j","o8K-R","O37cT","OA18A","oCehe","OFGv9","OGqxd","OHyOC","oJgsv",
                "OjJkO","Ol8lY","omtsl","Oo8DD","ooeQn","opR4H","OPwqm","oRQN_","osCaa","otCNq","OU2Nm","ovmO4",
                "OWGDd","OXNJo","oXR9s","OyAQL","oYO-e","oz_4R","Oz7Po","oZfes","Ozn1Y","P_fQk","P2CLn","P5M3Y",
                "p5-zT","P7nio","P83jL","paGAb","pawmX","pc8rL","PEpSv","p-Eyc","pEYkb","PGA5s","Ph6vD","pHFG7",
                "phogp","piM2N","PiOX8","PIVqw","pj_FZ","pkgAy","pKUgS","PnkBO","PnMNX","PNqnm","PNW4l","pO9tn",
                "poiC5","POn0u","ppOlG","Pq9bC","pqANy","PQApM","PQWQ0","pseOl","PT-5A","PVd5z","pxiUY","PZcRC",
                "Q6X8E","Q8yRO","qC8MX","qC80q","qdJUE","qDWgO","qe7ib","QeDaU","qELrG","QffVI","QGkFc","qGXYC",
                "QgYG9","qhxpr","QHZNn","QjTJ9","QKSN1","qMZl0","QNkw6","qNThs","QOi50","qOpAw","QQlHo","QqPYv",
                "qSYbA","QtlMD","qVHOo","qWMe_","QwrRK","QWVV7","qWWeF","QWwUL","QyA53","qZwkK","r2U32",
                "R3Npl","R8NRw","r9baj","R-9tw","rAv8K","RcOVh","RDrBT","RDxfj","Rgf7v","RgX2V","RhGak","Rhsrt",
                "RHuC2","rhvbR","RiJUy","rKBjB","rkbvv","rkfQI","rL7oT","rLL6t","rlw8H","rn8i8","RNLar","ROcwU",
                "rPeOk","RRS7e","RtIFu","RTl2L","Rur-i","rveQ3","rvQfl","rVxej","Ry3v7","rYq7J",
                "S_Cag","s0hGb","S0Uut","S1FBc","S2mEF","S2QGv","s3HBn","S6vzd","S9B8l","sAU6F","SBrbJ","sBrJT",
                "s-d7C","sI5SG","SIW2y","six2m","SkGrK",
                "Skyline-64_1_4_0_4421.zip","Skyline-64_1_4_0_4422.zip","Skyline-64_2_5_0_5675.zip","Skyline-64_20_1_0_155.zip",
                "SM_9d","SMP4O","sna_M","soBNX","SPkR6","sprQw",
                "sqVpD","srzYs","STpr1","SUGz6","Svw8U","SWIW9","SwvOY","syhEY","SyjtP","szFB5","szMUT","T_gxI",
                "T1d-m","T3KhJ","T3oqE","T5Hgq","t7aSK","t9YtG","T093H","TAAbp","TachS","taDt_","tchMy","Tgmug",
                "tgXKx","TH_Wu","ThIN1","thxs7","tiZNw","TLZRY","tNe7F","TnG1f","tNJV-","tP0lD","tPNqI","tPVBw",
                "tSW4s","ttNVz","ttVVL","TVB1P","TVzX7","tWw6J","Tx214","U_aDj","u0sZU","u1LRx","U4sCZ","u5oh0",
                "u5yMi","u17mQ","UAERJ","UaXHu","Ub2aG","ubMlW","u-d09","ue7uO","uEd03","UEgaC","uf09S","uFkkO",
                "UIQF2","UJt7P","UjY-6","uk2zH","UN_mf","UNGYS","uNZ49","UoTD9","uP0_z","UpBUn","UPLhZ",
                "UPTFO","uREtq","urPvG","uRRfr","usQpl","Usuof","USZgP","USZi3","Ut8mt","uuL71","uus4v","uw7Qx",
                "UwQiJ","uXJ3Z","uYGFC","uygkU","Uznaa","uz-Oq","v0Bii","V3c_T","vAA99","vBJBq","vDfH7","vdsCT",
                "Ve6Ta","ve7Ml","VEoAO","Vf6ec","vfINc","VipmQ","VKQTr","vKRM9","vlpJ-","Vm7DS","vNfbo","vnOtG",
                "vnyz6","Vo1-6","vp_WA","vp4ad","vPjSH","vpr5l","vqlAw","vscw3","vssyk","VSxLB","vt3g_","vTjAk",
                "vy-8M","Vz811","vZkWO","W_17N","W_zAH","w0W0o","W2ffg","w3bah","w5m84","W5TN-","W7_Gg","W8XKm",
                "w19Ht","w430-","WadIY","waINu","w-ayL","wAYVn","Wb1iY","wb9eC","wcnac","Wc-Se","WCwth","Wcyyt",
                "wD-Mr","WEMDm","WfG49","wgnCt","wH7z5","Whe2M","wHeOF","WHs9b","wj-Sy","wkzjo","wlz9S","WObZ2",
                "WohAe","woie2","WOrKF","wp9GI","wSEvL","wSmeO","WstBf","wTpm1","WvIMM","WXnQH","Wzi2E","X_-fO",
                "x_wnm","X7bAs","x8W-O","X78mS","xcEvh","xcmmi","xcqX_","xdiAw","xdnqq","XEgEz","xESjA","xfkcU",
                "XfWhr","XfY9w","xgThP","XiSQ3","XJ0Ij","xJl17","XjWWj","XlGMw","xlt5k","XMgjD","XPCHC",
                "XPxAQ","XQkWA","xRiK2","XSkvk","XUO_k","XWPlc","Xx0hO","XXA97","XxgDy","XZ_VJ","xZfiP","Y6Egb",
                "Y6KYN","y7C8P","y7VP8","Y8P0V","Y8tqk","YBhcq","YbyUR","yE59F","yewLE","YFavT","yFJtl","YFnAx",
                "yIaqR","YJ_Bw","ykUfT","Ylp6y","ymA2t","yPNFs","YS_68","YsfqU","YtEmi","ytYCY","YtZZ8",
                "yUzgJ","YZ09K","yZeZY","Yzlgb","Z-0NM","z1hKB","z7DVy","Z8FV9","Z8qKD","Z96lA","zAu-r","ZBE3X",
                "ZbPja","zbUz-","ZDjTb","Zdrnb","zFjSv","zFunl","zIW8u","zjUA6","zL3Mn","ZLImV","ZLpfb","zMVI0",
                "zOpUQ","Zuzaq","ZvFIb","zXdQ1","zxWHd","zz0Pq","ZzIxa","zzlPp","ZZPVw"
            };

            // Run each test 5 time to ensure consistency 
            for (int i = 0; i < 5; i++)
            {
                SortAndTest(orderedSample); // Test sort on a smaller sample set
                SortAndTest(orderSampleStressTest); // Test sort on large stress test sample
            }
            

            // Shuffle an input list and sort. Compares against original in order version to ensure consistency
            void SortAndTest(List<string> ordered)
            {
                List<string> misOrdered = Shuffle(ordered); // Shuffle

                misOrdered.Sort((x, y) => NaturalComparer.Compare(x, y)); // Naturally sort

                // Compare with original
                for (int i = 0; i < ordered.Count; i++)
                {
                    AssertEx.AreEqual(ordered[i], misOrdered[i], string.Format("Test Natural is not sorting in the correct order: at position {0}: {1} vs {2}", i, ordered[i], misOrdered[i]) + "\nOrdered List:\n" + ordered.Aggregate((a,b) => a + "\n" + b));
                }
            }

            // Returns a shuffled copy the input list
            List<string> Shuffle(List<string> inOrder)
            {
                List<string> misOrdered = new List<string>(inOrder); // Copy list
                Random rand = new Random();
                
                // Shuffle by swapping elements positions randomly
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
