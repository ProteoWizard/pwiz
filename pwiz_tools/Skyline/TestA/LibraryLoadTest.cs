/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for NistLibraryTest
    /// </summary>
    [TestClass]
    public class LibraryLoadTest : AbstractUnitTest
    {
        public const string PATH_NIST_LIB = @"C:\Libraries\NIST_yeast_v2.0_2008-07-11.msp";
        public const string PATH_NIST_LIB_CACHE = @"C:\Libraries\NIST_yeast_v2.0_2008-07-11" + NistLibrary.EXT_CACHE;
        public const string PATH_HUNTER_LIB = @"C:\Libraries\yeast_cmp_20.hlf";
        public const string PATH_BIBLIOSPEC_LIB = @"C:\Libraries\yeast.lib";
        // Cannot find a way to use anything but a file with SQLite
        // public const string _bibliospecLiteLibPath = @"C:\Libraries\yeast.blib";

        [TestMethod]
        public void NistLoadLibrary()
        {
            var streamManager = new MemoryStreamManager();
            streamManager.TextFiles.Add(PATH_NIST_LIB, TEXT_LIB_YEAST_NIST);
            var loader = new TestLibraryLoader {StreamManager = streamManager};

            var librarySpec = new NistLibSpec("Yeast (NIST)", PATH_NIST_LIB);

            Library lib1 = librarySpec.LoadLibrary(loader);
            CheckLibrary(lib1, 100);

            Assert.AreEqual(streamManager.BinaryFiles.Count, 1);
            Assert.IsTrue(streamManager.IsCached(PATH_NIST_LIB, PATH_NIST_LIB_CACHE));

            // Corrupt the cache, and make sure it still possible to get
            // a valid library.
            byte[] cacheBytes = streamManager.BinaryFiles[PATH_NIST_LIB_CACHE];
            int len = cacheBytes.Length;
            byte[] corruptedBytes = new byte[len/2];
            Array.Copy(cacheBytes, corruptedBytes, corruptedBytes.Length);
            streamManager.BinaryFiles[PATH_NIST_LIB_CACHE] = corruptedBytes;

            Library lib2 = librarySpec.LoadLibrary(loader);
            CheckLibrary(lib2, 100);

            Assert.AreEqual(len, streamManager.BinaryFiles[PATH_NIST_LIB_CACHE].Length);
            Assert.IsTrue(lib1.IsSameLibrary(lib2));
            Assert.AreEqual(0, lib1.CompareRevisions(lib2));
        }

        [TestMethod]
        public void HunterLoadLibrary()
        {
            var streamManager = new MemoryStreamManager();
            var loader = new TestLibraryLoader { StreamManager = streamManager };

            Library libNist = CreateHunterFile(streamManager, loader, TEXT_LIB_YEAST_NIST);

            Assert.AreEqual(1, streamManager.BinaryFiles.Count);

            var librarySpecHunter = new XHunterLibSpec("Yeast (X!)", PATH_HUNTER_LIB);

            Library libHunter = librarySpecHunter.LoadLibrary(loader);
            CheckLibrary(libHunter, 20);

            Assert.IsFalse(libNist.IsSameLibrary(libHunter));
        }

        public static Library CreateHunterFile(MemoryStreamManager streamManager, ILoadMonitor loader, string nistText)
        {
            return CreateHunterFile(streamManager, loader, nistText, false);
        }

        public static Library CreateHunterFile(MemoryStreamManager streamManager, ILoadMonitor loader,
            string nistText, bool lowIntensity)
        {
            return CreateLibraryFile(streamManager, loader, nistText,
                (sm, lib) => XHunterLibrary.Write(sm, PATH_HUNTER_LIB, lib, lowIntensity));
        }

        [TestMethod]
        public void BiblioSpecLoadLibrary()
        {
            var streamManager = new MemoryStreamManager();
            var loader = new TestLibraryLoader { StreamManager = streamManager };

            Library libNist = CreateBiblioFile(streamManager, loader, TEXT_LIB_YEAST_NIST);

            Assert.AreEqual(1, streamManager.BinaryFiles.Count);

            var librarySpecBiblio = new BiblioSpecLibSpec("Yeast (BS)", PATH_BIBLIOSPEC_LIB);

            Library libBiblioSpec = librarySpecBiblio.LoadLibrary(loader);
            CheckLibrary(libBiblioSpec, 100);

            Assert.IsFalse(libNist.IsSameLibrary(libBiblioSpec));
        }

        public static Library CreateBiblioFile(MemoryStreamManager streamManager, ILoadMonitor loader, string nistText)
        {
            return CreateLibraryFile(streamManager, loader, nistText,
                (sm, lib) => BiblioSpecLibrary.Write(sm, PATH_BIBLIOSPEC_LIB, lib));
        }

        private static Library CreateLibraryFile(MemoryStreamManager streamManager, ILoadMonitor loader,
            string nistText, Action<IStreamManager, Library> write)
        {
            streamManager.TextFiles[PATH_NIST_LIB] = nistText;

            var librarySpec = new NistLibSpec("Temporary (NIST)", PATH_NIST_LIB);

            Library libNist = librarySpec.LoadLibrary(loader);
            Assert.IsNotNull(libNist);
            write(streamManager, libNist);

            streamManager.TextFiles.Remove(PATH_NIST_LIB);
            streamManager.Delete(PATH_NIST_LIB_CACHE);

            return libNist;
        }

        private static void CheckLibrary(Library lib, int minPeaks)
        {
            Assert.IsNotNull(lib);
            Assert.IsTrue(lib.IsLoaded);
            foreach (var key in KEYS_LIB_YEAST_NIST)
            {
                SpectrumHeaderInfo info;
                Assert.IsTrue(lib.TryGetLibInfo(key, out info));
                SpectrumPeaksInfo peaksInfo;
                Assert.IsTrue(lib.TryLoadSpectrum(key, out peaksInfo));
                Assert.IsTrue(peaksInfo.Peaks.Length >= minPeaks);
            }            
        }

        public class TestLibraryLoader : ILoadMonitor
        {
            public IStreamManager StreamManager { get; set; }

            public bool IsCanceled
            {
                get { return false; }
            }

            public UpdateProgressResponse UpdateProgress(ProgressStatus status)
            {
                return UpdateProgressResponse.normal;
            }

            public bool HasUI { get { return false; } }
        }

        public static readonly LibKey[] KEYS_LIB_YEAST_NIST =
        {
            new LibKey("QQGPLEPTVGNSTAITEER", 2),
            new LibKey("IPSAIHLQISNK", 2),
            new LibKey("NSTDIPLQDATETYR", 2),
            new LibKey("VLHNFLTTPSK", 2),
        };

        public const string TEXT_LIB_YEAST_NIST =
            TEXT_LIB_YEAST_NIST1 +
            "\n" +
            TEXT_LIB_YEAST_NIST2 +
            "\n" +
            TEXT_LIB_YEAST_NIST3 +
            "\n" +
            TEXT_LIB_YEAST_NIST4;

        public const string TEXT_LIB_YEAST_NIST1 =
            "Name: QQGPLEPTVGNSTAITEER/2\n" +
            "MW: 2028.012\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=K.QQGPLEPTVGNSTAITEER.R/2 Mods=0 Parent=1014.006 Inst=it Mz_diff=0.264 Mz_exact=1014.0062 Mz_av=1014.596 Protein=\"gi|6319311|ref|NP_009394.1| Fun14p [Saccharomyces cerevisiae]; gi|349745|gb|AAC04950.1| Fun14p [Saccharomyces cerevisiae]; gi|45269305|gb|AAS56033.1| YAL008W [Saccharomyces cerevisiae]; gi|629999|pir||S43447 FUN14 protein - yeast (Saccharomyces cerevisiae); gi|731265|sp|P18411|YAA8_YEAST Hypothetical 22.1 kDa protein in SPO7-ERP2 intergenic region\" Pseq=1 Organism=\"yeast\" Se=5^M1:sc=51.65/0,td=49.4/0,sr=24.94/0,sd=49.4/0,bs=24.94,bd=49.4^X6:ex=2.9e-011/8e-006,td=1.77e+011/1.324e+011,sd=0/0,hs=57.85/5.033,bs=4.9e-012,b2=5.9e-012,bd=1.61e+011^O5:ex=2.69e-006/1.486e-005,td=1.86e+007/1.956e+008,pr=2.89e-010/1.772e-009,bs=9.33e-008,b2=1.11e-007,bd=5.36e+008^S1:sc=3.16/0,pb=0.9994/0,dc=0.45/0,ps=454.9/0,pr=1/0,bs=0.9994,bd=0.45^P4:sc=27.4/3,dc=18.9/2.5,ps=3.43/0.275,bs=0 Sample=8/cbs00174_001_01_cam,1,1/cbs00174_001_02_cam,1,1/cbs00174_01_06_cam,1,1/cbs00174_01_07_cam,0,1/cbs00174_01_10_cam,1,1/cbs00175_02_05_cam,0,1/cbs00175_02_06_cam,1,1/yeast_comp12vs12standscx_cam,1,1 Nreps=6/9 Missing=0.0630/0.0510 Parent_med=1014.21/0.34 Max2med_orig=629.5/170.9 Dotfull=0.861/0.032 Dot_cons=0.936/0.042 Unassign_all=0.086 Unassigned=0.017 Dotbest=0.94 Flags=0,0,2 Naa=19 DUScorr=10/2/2.9 Dottheory=0.93 Pfin=6.1e+013 Probcorr=20 Tfratio=1.7e+010 Pfract=0.17\n" +
            "Num peaks: 178\n" +
            "304.2\t205\t\"y2/0.04,b6-46^2/0.04 6/6 0.5\"\n" +
            "314.2\t39\t\"b3/0.06 4/4 0.1\"\n" +
            "334.1\t50\t\"? 2/2 1.0\"\n" +
            "340.2\t298\t\"? 6/6 0.5\"\n" +
            "348.3\t54\t\"? 3/5 0.4\"\n" +
            "351.0\t78\t\"y6-17^2/-0.19,y6-18^2/0.31 3/5 0.4\"\n" +
            "365.4\t43\t\"? 3/5 0.1\"\n" +
            "366.2\t55\t\"b7-18^2/-0.49,b4-45/0.01 3/5 0.2\"\n" +
            "378.1\t46\t\"? 3/5 0.1\"\n" +
            "379.2\t52\t\"?i 4/5 0.0\"\n" +
            "380.2\t64\t\"?i 3/5 1.3\"\n" +
            "393.9\t126\t\"b4-17/-0.29 6/6 1.2\"\n" +
            "397.2\t307\t\"? 6/6 1.1\"\n" +
            "398.2\t91\t\"?i 5/5 2.0\"\n" +
            "415.3\t38\t\"y3-18/0.10 3/4 0.1\"\n" +
            "433.4\t196\t\"y3/0.20 6/6 0.7\"\n" +
            "434.3\t41\t\"y3i/1.10 4/5 0.2\"\n" +
            "461.3\t210\t\"? 6/6 0.3\"\n" +
            "462.4\t176\t\"?i 6/6 0.4\"\n" +
            "469.3\t56\t\"? 5/5 0.2\"\n" +
            "472.4\t87\t\"? 3/5 0.5\"\n" +
            "479.3\t775\t\"b5-45*/0.02 6/6 2.7\"\n" +
            "480.2\t156\t\"b5-45i/0.92 6/6 2.0\"\n" +
            "489.4\t76\t\"? 5/5 0.3\"\n" +
            "490.2\t114\t\"y4-44/-0.05 5/6 0.6\"\n" +
            "505.2\t61\t\"? 4/6 1.4\"\n" +
            "506.4\t335\t\"b5-18/0.12 6/6 2.5\"\n" +
            "507.3\t824\t\"b5-17/0.02 5/6 1.9\"\n" +
            "508.3\t188\t\"b5-17i/1.02 6/6 1.0\"\n" +
            "521.4\t113\t\"? 3/2 4.0\"\n" +
            "524.4\t74\t\"b5/0.12 5/5 0.2\"\n" +
            "525.2\t81\t\"b5i/0.92 5/6 2.5\"\n" +
            "534.3\t1280\t\"y4/0.05 6/6 2.2\"\n" +
            "535.3\t196\t\"y4i/1.05 6/6 1.0\"\n" +
            "556.5\t43\t\"? 4/5 0.1\"\n" +
            "590.3\t68\t\"? 4/5 0.2\"\n" +
            "591.0\t88\t\"?i 4/6 1.2\"\n" +
            "601.4\t47\t\"y5-46/0.06 3/5 0.2\"\n" +
            "607.4\t79\t\"b6-46/0.08 5/5 1.1\"\n" +
            "608.3\t65\t\"b6-45/-0.02 6/5 0.3\"\n" +
            "618.3\t296\t\"? 6/6 1.8\"\n" +
            "619.4\t133\t\"?i 6/6 0.7\"\n" +
            "635.3\t1324\t\"b6-18/-0.02 6/6 2.3\"\n" +
            "636.3\t1557\t\"b6-17/-0.02 6/6 8.9\"\n" +
            "637.2\t262\t\"b6-17i/0.88 6/6 1.3\"\n" +
            "647.4\t603\t\"y5/0.06 6/6 1.1\"\n" +
            "648.3\t106\t\"y5i/0.96 5/6 0.4\"\n" +
            "653.3\t431\t\"b6/-0.02 6/6 0.3\"\n" +
            "654.3\t106\t\"b6i/0.98 6/6 0.5\"\n" +
            "676.5\t79\t\"? 3/5 1.9\"\n" +
            "679.2\t51\t\"y13-17^2/-0.15,y13-18^2/0.35 4/5 0.1\"\n" +
            "688.0\t507\t\"y13^2/0.15 6/6 6.4\"\n" +
            "688.5\t179\t\"? 4/6 4.2\"\n" +
            "694.4\t58\t\"? 4/5 0.3\"\n" +
            "710.4\t76\t\"? 6/6 0.6\"\n" +
            "718.4\t484\t\"y6/0.03 5/6 0.6\"\n" +
            "719.5\t111\t\"y6i/1.13 5/6 0.5\"\n" +
            "733.5\t63\t\"b7-17/0.13 3/5 0.2\"\n" +
            "752.3\t86\t\"y14^2/-0.07 4/5 0.5\"\n" +
            "798.2\t51\t\"b16^2/0.30 4/5 0.9\"\n" +
            "816.2\t76\t\"? 5/5 0.5\"\n" +
            "819.5\t343\t\"y7/0.08 6/6 0.8\"\n" +
            "820.5\t83\t\"y7i/1.08 6/6 0.4\"\n" +
            "823.3\t66\t\"a8/-0.12 5/5 0.5\"\n" +
            "824.4\t50\t\"a8i/0.98 4/5 0.1\"\n" +
            "833.4\t69\t\"b8-18/-0.02 3/5 0.2\"\n" +
            "839.3\t48\t\"b17-46^2/-0.12 3/5 0.2\"\n" +
            "841.4\t113\t\"? 6/6 1.7\"\n" +
            "848.8\t94\t\"y16-17^2/-0.14,y16-18^2/0.36 4/5 0.9\"\n" +
            "849.6\t37\t\"y16-17i^2/0.66 3/4 0.0\"\n" +
            "851.4\t42\t\"b8/-0.02 3/5 0.1\"\n" +
            "857.7\t399\t\"y16^2/0.26 6/6 2.2\"\n" +
            "877.4\t197\t\"y17-17^2/-0.05,y17-18^2/0.45 4/6 0.3\"\n" +
            "878.0\t70\t\"y17-17i^2/0.55 3/5 0.3\"\n" +
            "886.1\t1723\t\"y17^2/0.15 6/6 4.8\"\n" +
            "887.1\t62\t\"y17i^2/1.15 3/5 0.3\"\n" +
            "888.4\t75\t\"y8-18/-0.05 6/5 0.2\"\n" +
            "889.3\t42\t\"y8-17/-0.15 4/5 0.1\"\n" +
            "896.0\t37\t\"? 3/4 0.1\"\n" +
            "897.4\t79\t\"? 5/5 0.2\"\n" +
            "898.5\t65\t\"?i 5/5 0.2\"\n" +
            "899.6\t36\t\"?i 3/4 0.2\"\n" +
            "905.5\t59\t\"b9-45/0.01 3/5 0.4\"\n" +
            "906.4\t516\t\"y8/-0.05,b9-44/-0.09 6/6 1.9\"\n" +
            "907.5\t193\t\"y8i/1.05 5/6 1.0\"\n" +
            "909.4\t40\t\"? 2/2 0.2\"\n" +
            "915.3\t628\t\"? 5/6 0.9\"\n" +
            "916.3\t196\t\"?i 5/6 0.6\"\n" +
            "922.5\t89\t\"? 3/3 3.5\"\n" +
            "932.3\t253\t\"b9-18/-0.19 6/6 1.6\"\n" +
            "933.5\t357\t\"b9-17/0.01 6/6 2.6\"\n" +
            "934.4\t82\t\"b9-17i/0.91 6/5 0.7\"\n" +
            "941.3\t73\t\"y18-17^2/-0.18,y18-18^2/0.32 4/6 0.5\"\n" +
            "947.9\t42\t\"? 3/2 0.1\"\n" +
            "950.5\t490\t\"b9/0.01 6/6 1.1\"\n" +
            "951.5\t166\t\"b9i/1.01 6/6 1.8\"\n" +
            "958.9\t50\t\"? 2/2 0.5\"\n" +
            "970.8\t60\t\"? 3/5 0.2\"\n" +
            "972.6\t61\t\"? 4/5 0.2\"\n" +
            "974.8\t67\t\"y9-46*/0.30 5/5 0.5\"\n" +
            "978.7\t74\t\"? 4/6 0.3\"\n" +
            "979.2\t107\t\"? 4/5 0.5\"\n" +
            "983.2\t239\t\"? 5/6 1.0\"\n" +
            "986.3\t54\t\"? 3/2 0.5\"\n" +
            "987.6\t254\t\"?i 5/6 1.0\"\n" +
            "988.6\t62\t\"?i 3/5 0.3\"\n" +
            "989.5\t68\t\"b10-18/-0.01 4/5 0.2\"\n" +
            "990.1\t72\t\"b10-17/-0.41,b10-18/0.59 3/5 0.8\"\n" +
            "990.7\t85\t\"p-46/-0.31,b10-17/0.19 3/5 0.4\"\n" +
            "991.7\t62\t\"p-45/0.19,p-44/-0.31 5/5 0.2\"\n" +
            "994.5\t195\t\"? 4/6 1.1\"\n" +
            "996.3\t2849\t\"p-35/-0.20,p-36/0.30 6/6 6.7\"\n" +
            "997.0\t1105\t\"p-35i/0.50 5/6 3.2\"\n" +
            "999.3\t53\t\"? 3/5 0.3\"\n" +
            "1003.5\t51\t\"y9-17/0.00 6/5 0.2\"\n" +
            "1005.2\t950\t\"p-18/0.20,p-17/-0.31 6/6 16.1\"\n" +
            "1005.6\t257\t\"p-17/0.09 4/6 2.9\"\n" +
            "1042.1\t57\t\"? 3/5 0.3\"\n" +
            "1059.5\t116\t\"y10-18/-0.02 6/6 0.3\"\n" +
            "1060.5\t133\t\"y10-17/-0.02 5/6 0.4\"\n" +
            "1061.5\t57\t\"y10-17i/0.98 6/5 0.4\"\n" +
            "1071.4\t48\t\"? 3/5 0.1\"\n" +
            "1077.5\t3561\t\"y10/-0.02,b11-44/-0.05 6/6 4.5\"\n" +
            "1078.5\t1357\t\"y10i/0.98 6/6 6.7\"\n" +
            "1103.6\t47\t\"b11-18/0.05 4/4 0.2\"\n" +
            "1104.5\t63\t\"b11-17/-0.05 4/5 0.1\"\n" +
            "1121.6\t58\t\"b11/0.05 5/5 0.1\"\n" +
            "1176.6\t484\t\"y11/0.01 5/6 0.5\"\n" +
            "1177.6\t193\t\"y11i/1.01 5/6 0.6\"\n" +
            "1190.6\t54\t\"b12-18/0.02 4/5 0.2\"\n" +
            "1191.5\t85\t\"b12-17/-0.08 5/5 0.3\"\n" +
            "1192.6\t53\t\"b12-17i/1.02 4/5 0.2\"\n" +
            "1200.5\t108\t\"? 3/5 2.9\"\n" +
            "1208.6\t94\t\"b12/0.02 4/6 2.0\"\n" +
            "1237.6\t42\t\"? 3/5 0.1\"\n" +
            "1277.6\t172\t\"y12/-0.03 6/6 1.3\"\n" +
            "1278.7\t64\t\"y12i/1.07 3/5 0.2\"\n" +
            "1291.8\t63\t\"b13-18/0.17 3/5 0.4\"\n" +
            "1310.4\t111\t\"? 4/6 4.3\"\n" +
            "1338.8\t48\t\"? 5/5 0.1\"\n" +
            "1339.8\t62\t\"?i 5/5 0.3\"\n" +
            "1345.6\t99\t\"? 6/5 0.4\"\n" +
            "1346.4\t58\t\"?i 4/5 0.2\"\n" +
            "1356.6\t404\t\"y13-18/-0.09 6/6 2.5\"\n" +
            "1357.6\t385\t\"y13-17/-0.09 6/6 0.8\"\n" +
            "1358.7\t128\t\"y13-17i/1.01 5/6 0.8\"\n" +
            "1362.6\t96\t\"b14-18/-0.07 5/6 0.3\"\n" +
            "1363.6\t48\t\"b14-17/-0.07 4/5 0.2\"\n" +
            "1374.6\t10000\t\"y13/-0.09 6/6 0.0\"\n" +
            "1375.6\t5241\t\"y13i/0.91 6/6 16.5\"\n" +
            "1376.5\t799\t\"y13i/1.81 5/6 13.1\"\n" +
            "1380.6\t87\t\"b14/-0.07 4/6 0.3\"\n" +
            "1381.6\t74\t\"b14i/0.93 4/5 1.3\"\n" +
            "1458.5\t76\t\"y14-45/-0.23 5/5 0.2\"\n" +
            "1459.7\t80\t\"y14-44/-0.03 3/5 0.3\"\n" +
            "1475.7\t155\t\"b15-18/-0.05 4/6 0.2\"\n" +
            "1476.7\t276\t\"b15-17/-0.05 6/6 0.7\"\n" +
            "1477.5\t124\t\"b15-17i/0.75 6/6 0.5\"\n" +
            "1485.6\t151\t\"y14-18/-0.13 6/6 0.4\"\n" +
            "1486.8\t79\t\"y14-17/0.07 6/6 0.4\"\n" +
            "1493.8\t274\t\"b15/0.05 6/6 1.0\"\n" +
            "1494.6\t121\t\"b15i/0.85 5/6 0.2\"\n" +
            "1503.6\t731\t\"y14/-0.13 5/6 0.7\"\n" +
            "1504.8\t478\t\"y14i/1.07 6/6 2.4\"\n" +
            "1576.8\t106\t\"b16-18/0.00 3/5 2.8\"\n" +
            "1595.0\t44\t\"b16/0.20 3/4 0.2\"\n" +
            "1705.8\t260\t\"b17-18/-0.04 5/6 0.5\"\n" +
            "1706.6\t180\t\"b17-17/-0.24 6/6 1.2\"\n" +
            "1713.5\t183\t\"y16/-0.37 6/6 1.2\"\n" +
            "1714.9\t156\t\"? 6/6 1.2\"\n" +
            "1723.7\t78\t\"b17/-0.14 4/5 0.2\"\n" +
            "1724.9\t51\t\"y17-46/0.01 4/5 0.2\"\n" +
            "1770.8\t382\t\"y17/-0.09 6/6 0.6\"\n" +
            "1771.9\t323\t\"y17i/1.01 6/6 1.2\"\n" +
            "1834.8\t148\t\"b18-18/-0.09 5/6 0.6\"\n" +
            "1835.7\t153\t\"b18-17/-0.19 5/6 0.6\"\n" +
            "1852.8\t77\t\"b18/-0.09,y18-46/-0.15 5/5 0.2\"\n" +
            "1854.0\t81\t\"y18-45*/0.05 4/5 0.5\"\n";

        public const string TEXT_LIB_YEAST_NIST2 =
            "Name: IPSAIHLQISNK/2\n" +
            "MW: 1321.772\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=R.IPSAIHLQISNK.S/2 Mods=0 Parent=660.886 Inst=it Mz_diff=0.114 Mz_exact=660.8859 Mz_av=661.285 Protein=\"gi|171865|gb|AAC04947.1| Mdm10p: Mitochondria outer membrane protein [Saccharomyces cerevisiae]\" Pseq=2 Organism=\"yeast\" Se=3^X2:ex=0.14/0.14,td=1e+009/1e+009,sd=0/0,hs=37.2/18,bs=4.9e-010,b2=0.28,bd=2e+009^O1:ex=2.29e-006/0,td=2.19e+007/0,pr=2.89e-010/0,bs=2.29e-006,bd=2.19e+007^P1:sc=28.8/0,dc=21.2/0,ps=3.68/0,bs=0 Sample=4/cbs00174_001_01_cam,0,1/cbs00174_01_07_cam,1,1/cbs00175_02_01_cam,1,1/yeast_ffey1_none,0,1 Nreps=2/4 Missing=0.1592/0.0033 Parent_med=661.00/0.23 Max2med_orig=506.6/251.6 Dotfull=0.617/0.000 Dot_cons=0.857/0.002 Unassign_all=0.336 Unassigned=0.422 Dotbest=0.86 Flags=0,0,0 Naa=12 DUScorr=5.3/0.17/2.9 Dottheory=0.92 Pfin=1.9e+013 Probcorr=1 Tfratio=1e+009 Pfract=0\n" +
            "Num peaks: 106\n" +
            "280.3\t213\t\"b3-18*/0.13 2/2 2.8\"\n" +
            "295.1\t270\t\"y5^2/-0.07 2/2 1.7\"\n" +
            "296.3\t61\t\"y5i^2/1.13 2/1 0.4\"\n" +
            "316.3\t61\t\"? 2/1 0.4\"\n" +
            "327.2\t53\t\"? 2/1 0.4\"\n" +
            "331.3\t106\t\"y3-17/0.11 2/2 1.4\"\n" +
            "341.2\t317\t\"a4/-0.01 2/2 4.2\"\n" +
            "348.3\t507\t\"y3/0.11 2/2 7.8\"\n" +
            "357.4\t638\t\"b7-18^2/-0.32 2/2 7.7\"\n" +
            "358.4\t188\t\"b7-18i^2/0.68 2/2 2.6\"\n" +
            "365.3\t307\t\"? 2/2 2.5\"\n" +
            "369.2\t390\t\"b4/-0.01 2/2 5.7\"\n" +
            "370.7\t106\t\"? 2/1 1.1\"\n" +
            "420.7\t97\t\"y7^2/0.46 2/1 1.1\"\n" +
            "439.8\t64\t\"? 2/1 0.4\"\n" +
            "443.4\t166\t\"y4-18/0.13 2/2 0.5\"\n" +
            "444.3\t73\t\"y4-17/0.03 2/2 0.7\"\n" +
            "454.4\t309\t\"a5/0.11 2/2 4.9\"\n" +
            "455.6\t107\t\"a5i/1.31 2/1 0.9\"\n" +
            "461.4\t509\t\"y4/0.13 2/2 6.9\"\n" +
            "462.4\t91\t\"y4i/1.13 2/2 1.0\"\n" +
            "464.4\t122\t\"b5-18/0.11,b9-45^2/-0.39 2/2 1.4\"\n" +
            "470.5\t149\t\"? 2/2 2.1\"\n" +
            "474.4\t72\t\"? 2/1 0.4\"\n" +
            "476.9\t144\t\"y8^2/0.12 2/2 1.7\"\n" +
            "479.4\t61\t\"? 2/1 0.3\"\n" +
            "482.4\t285\t\"b5/0.11 2/2 4.9\"\n" +
            "483.4\t100\t\"b5i/1.11 2/2 1.3\"\n" +
            "486.4\t94\t\"? 2/2 0.4\"\n" +
            "492.4\t88\t\"? 2/2 0.1\"\n" +
            "506.3\t221\t\"? 2/2 3.8\"\n" +
            "512.5\t404\t\"y9^2/0.20 2/2 4.4\"\n" +
            "522.5\t87\t\"b10-17^2/0.20 2/2 0.9\"\n" +
            "546.8\t808\t\"y10-18^2/-0.02 2/2 10.3\"\n" +
            "553.3\t84\t\"? 2/2 0.6\"\n" +
            "556.0\t1714\t\"y10^2/0.18 2/2 29.4\"\n" +
            "556.5\t125\t\"? 2/2 0.8\"\n" +
            "561.1\t175\t\"? 2/2 1.2\"\n" +
            "569.4\t55\t\"b11-36^2/-0.43 2/1 0.3\"\n" +
            "571.6\t148\t\"y5-18/0.27 2/2 1.5\"\n" +
            "572.5\t198\t\"y5-17/0.17 2/2 2.7\"\n" +
            "573.5\t70\t\"y5-17i/1.17 2/1 0.7\"\n" +
            "578.7\t123\t\"b11-18^2/-0.13 2/1 1.4\"\n" +
            "586.6\t105\t\"? 2/2 1.4\"\n" +
            "589.4\t672\t\"y5/0.07 2/2 6.9\"\n" +
            "590.5\t125\t\"y5i/1.17 2/2 1.1\"\n" +
            "591.5\t79\t\"a6/0.15 2/2 0.3\"\n" +
            "595.6\t606\t\"y11-17^2/-0.24,y11-18^2/0.26 2/2 9.7\"\n" +
            "596.4\t276\t\"y11-17i^2/0.56 2/1 4.2\"\n" +
            "598.6\t75\t\"? 2/2 0.8\"\n" +
            "601.4\t169\t\"b6-18/0.05 2/2 2.3\"\n" +
            "604.6\t5871\t\"y11^2/0.26 2/2 88.9\"\n" +
            "607.6\t3774\t\"? 2/2 42.8\"\n" +
            "615.6\t94\t\"? 2/1 0.2\"\n" +
            "617.5\t917\t\"? 2/2 12.5\"\n" +
            "618.5\t201\t\"?i 2/2 1.6\"\n" +
            "619.7\t973\t\"b6/0.35 2/2 8.1\"\n" +
            "620.6\t389\t\"b6i/1.25 2/2 1.1\"\n" +
            "621.7\t81\t\"b6i/2.35 2/1 0.3\"\n" +
            "625.0\t1876\t\"? 2/2 12.6\"\n" +
            "625.8\t128\t\"?i 2/2 0.1\"\n" +
            "628.7\t10000\t\"? 2/2 69.4\"\n" +
            "629.5\t1022\t\"?i 2/2 5.1\"\n" +
            "630.7\t127\t\"?i 2/2 0.0\"\n" +
            "632.2\t98\t\"? 2/2 0.2\"\n" +
            "634.7\t245\t\"? 2/2 2.8\"\n" +
            "638.6\t873\t\"p-45/0.21,p-44/-0.29 2/2 6.2\"\n" +
            "639.6\t229\t\"p-45i/1.21 2/2 0.6\"\n" +
            "642.8\t813\t\"p-36/-0.08 2/2 9.7\"\n" +
            "643.5\t1294\t\"p-35/0.12 2/2 19.2\"\n" +
            "644.6\t261\t\"p-35i/1.22 2/2 4.2\"\n" +
            "645.9\t237\t\"p-35i/2.52 2/2 1.6\"\n" +
            "648.6\t73\t\"? 2/1 0.6\"\n" +
            "651.4\t3379\t\"p-18/-0.48 2/2 47.0\"\n" +
            "652.5\t2566\t\"p-17/0.11 2/2 20.4\"\n" +
            "683.3\t55\t\"? 2/1 0.4\"\n" +
            "702.5\t942\t\"y6/0.08 2/2 14.8\"\n" +
            "703.5\t240\t\"y6i/1.08 2/2 3.4\"\n" +
            "730.3\t209\t\"? 2/2 0.7\"\n" +
            "732.4\t492\t\"b7/-0.03 2/2 7.5\"\n" +
            "822.5\t68\t\"y7-17/0.03 2/1 0.4\"\n" +
            "826.1\t92\t\"? 2/1 0.6\"\n" +
            "830.4\t100\t\"? 2/2 0.1\"\n" +
            "839.5\t2091\t\"y7/0.03 2/2 32.8\"\n" +
            "840.5\t608\t\"y7i/1.03 2/2 9.5\"\n" +
            "860.5\t599\t\"b8/0.01 2/2 8.9\"\n" +
            "861.5\t309\t\"b8i/1.01 2/2 0.1\"\n" +
            "875.8\t206\t\"? 2/2 0.0\"\n" +
            "928.7\t77\t\"b9-45/0.12 2/1 0.4\"\n" +
            "947.5\t100\t\"? 2/2 0.0\"\n" +
            "952.5\t508\t\"y8/-0.06 2/2 6.3\"\n" +
            "953.6\t236\t\"y8i/1.04 2/2 3.7\"\n" +
            "973.5\t869\t\"b9/-0.08 2/2 13.6\"\n" +
            "974.7\t436\t\"b9i/1.12 2/2 3.2\"\n" +
            "987.4\t73\t\"? 2/1 0.6\"\n" +
            "1023.7\t442\t\"y9/0.10 2/2 7.2\"\n" +
            "1024.6\t160\t\"y9i/1.00 2/2 2.4\"\n" +
            "1043.0\t125\t\"b10-18/0.39 2/1 1.1\"\n" +
            "1043.6\t216\t\"b10-17/-0.01 2/2 0.1\"\n" +
            "1044.5\t95\t\"b10-17i/0.89 2/2 0.3\"\n" +
            "1060.6\t326\t\"b10/-0.01 2/2 5.0\"\n" +
            "1061.4\t158\t\"b10i/0.79 2/2 2.3\"\n" +
            "1110.6\t563\t\"y10/-0.03 2/2 7.8\"\n" +
            "1111.7\t389\t\"y10i/1.07 2/2 6.5\"\n" +
            "1174.8\t301\t\"b11/0.15 2/2 4.2\"\n" +
            "1175.7\t192\t\"b11i/1.05 2/2 3.2\"\n";

        public const string TEXT_LIB_YEAST_NIST3 =
            "Name: NSTDIPLQDATETYR/2\n" +
            "MW: 1724.827\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=R.NSTDIPLQDATETYR.Q/2 Mods=0 Parent=862.413 Inst=it Mz_diff=0.307 Mz_exact=862.4134 Mz_av=862.915 Protein=\"gi|171865|gb|AAC04947.1| Mdm10p: Mitochondria outer membrane protein [Saccharomyces cerevisiae]\" Pseq=2 Organism=\"yeast\" Se=3^X4:ex=3.95e-007/8.997e-006,td=2.05e+007/1.367e+007,sd=0/0,hs=45.25/3.475,bs=2e-008,b2=3.9e-007,bd=3.95e+007^O4:ex=2.195e-005/1.595e-005,td=2.29e+006/1.78e+007,pr=2.945e-009/2.577e-009,bs=6.97e-007,b2=2.05e-005,bd=7.17e+007^P4:sc=31.1/2.775,dc=21.45/2.2,ps=3.705/0.2425,bs=0 Sample=7/cbs00174_001_01_cam,0,1/cbs00174_01_06_cam,0,1/cbs00174_01_10_cam,1,1/cbs00175_02_01_cam,1,1/cbs00175_02_02_cam,0,1/cbs00175_02_05_cam,0,1/cbs00175_02_06_cam,2,2 Nreps=4/8 Missing=0.1111/0.0353 Parent_med=862.72/0.06 Max2med_orig=758.8/183.5 Dotfull=0.858/0.014 Dot_cons=0.913/0.024 Unassign_all=0.121 Unassigned=0.055 Dotbest=0.89 Flags=0,0,0 Naa=15 DUScorr=10/1.4/2.9 Dottheory=0.97 Pfin=9.3e+014 Probcorr=1 Tfratio=1.4e+010 Pfract=0\n" +
            "Num peaks: 150\n" +
            "268.2\t51\t\"? 4/4 0.2\"\n" +
            "285.2\t52\t\"y4^2/0.56,b3-18/0.08 4/4 0.2\"\n" +
            "286.2\t70\t\"b3-17/0.08 3/4 0.3\"\n" +
            "296.2\t25\t\"? 2/2 0.0\"\n" +
            "303.3\t27\t\"b3/0.18 2/2 0.1\"\n" +
            "304.2\t42\t\"b3i/1.08 4/4 0.1\"\n" +
            "325.1\t26\t\"? 2/2 0.1\"\n" +
            "338.2\t111\t\"y2/0.01 4/4 0.1\"\n" +
            "343.3\t54\t\"? 4/4 0.3\"\n" +
            "353.1\t47\t\"? 3/4 0.1\"\n" +
            "354.3\t45\t\"?i 4/4 0.1\"\n" +
            "363.3\t27\t\"? 2/2 0.1\"\n" +
            "364.2\t66\t\"?i 3/4 0.4\"\n" +
            "365.3\t48\t\"?i 3/4 0.3\"\n" +
            "382.1\t94\t\"? 4/4 0.2\"\n" +
            "383.2\t157\t\"?i 4/4 0.3\"\n" +
            "384.3\t48\t\"?i 3/4 0.3\"\n" +
            "389.4\t52\t\"? 4/4 0.4\"\n" +
            "399.4\t71\t\"? 3/4 0.1\"\n" +
            "400.3\t267\t\"b4-18/0.15 4/4 0.7\"\n" +
            "401.1\t138\t\"b4-17/-0.05 4/4 0.5\"\n" +
            "418.1\t459\t\"b4/-0.05 4/4 1.8\"\n" +
            "419.2\t79\t\"y7-18^2/0.00,y7-17^2/-0.50 4/4 0.3\"\n" +
            "433.2\t25\t\"? 2/2 0.1\"\n" +
            "434.3\t27\t\"?i 2/3 0.2\"\n" +
            "439.3\t529\t\"y3/0.06 4/4 1.7\"\n" +
            "440.4\t92\t\"y3i/1.16 4/4 0.4\"\n" +
            "441.2\t29\t\"y3i/1.96 2/3 0.1\"\n" +
            "450.2\t38\t\"? 4/4 0.1\"\n" +
            "454.4\t62\t\"? 4/4 0.4\"\n" +
            "460.2\t64\t\"? 4/4 0.2\"\n" +
            "467.4\t64\t\"? 4/4 0.3\"\n" +
            "468.4\t76\t\"?i 4/4 0.4\"\n" +
            "469.2\t72\t\"y8-46^2/-0.03,b9-46^2/-0.53 4/4 0.2\"\n" +
            "478.2\t139\t\"? 4/4 0.3\"\n" +
            "479.1\t57\t\"?i 4/4 0.3\"\n" +
            "485.3\t122\t\"b5-46/0.07 4/4 0.3\"\n" +
            "486.4\t231\t\"b5-45/0.17 4/4 0.3\"\n" +
            "487.1\t55\t\"b5-44/-0.13 4/4 0.6\"\n" +
            "495.2\t249\t\"? 4/4 0.3\"\n" +
            "496.2\t315\t\"?i 4/4 0.5\"\n" +
            "497.1\t61\t\"?i 4/4 0.3\"\n" +
            "503.2\t321\t\"a5/-0.03 4/4 0.3\"\n" +
            "504.3\t76\t\"a5i/1.07 4/4 0.3\"\n" +
            "513.2\t829\t\"b5-18/-0.03 4/4 1.2\"\n" +
            "514.3\t547\t\"b5-17/0.07 4/4 1.0\"\n" +
            "515.3\t71\t\"b5-17i/1.07 4/4 0.2\"\n" +
            "531.3\t1332\t\"b5/0.07 4/4 1.2\"\n" +
            "532.4\t175\t\"b5i/1.17 4/4 0.4\"\n" +
            "565.5\t27\t\"? 2/1 0.1\"\n" +
            "568.3\t304\t\"y4/0.02 4/4 0.3\"\n" +
            "569.4\t81\t\"b11-18^2/-0.37 4/4 0.4\"\n" +
            "588.5\t59\t\"y10-18^2/0.20,y10-17^2/-0.30 3/4 0.2\"\n" +
            "592.2\t29\t\"? 2/3 0.2\"\n" +
            "597.5\t359\t\"y10^2/0.20 4/4 0.6\"\n" +
            "645.2\t44\t\"y11-17^2/-0.14,y11-18^2/0.36 3/4 0.2\"\n" +
            "651.8\t30\t\"y5-18/0.47,y5-17/-0.53 2/2 0.1\"\n" +
            "653.8\t319\t\"y11^2/-0.04 4/4 0.8\"\n" +
            "654.5\t149\t\"y11i^2/0.66 3/4 0.4\"\n" +
            "669.4\t324\t\"y5/0.07 4/4 0.7\"\n" +
            "670.4\t76\t\"b13-46^2/-0.42 4/4 0.2\"\n" +
            "697.3\t24\t\"b7-44/-0.07 2/1 0.1\"\n" +
            "711.5\t43\t\"y12^2/0.15 3/4 0.4\"\n" +
            "723.4\t62\t\"b7-18/0.03,y6-17/0.04 4/4 0.1\"\n" +
            "724.3\t53\t\"b7-17/-0.07 3/4 0.0\"\n" +
            "740.4\t492\t\"y6/0.04 4/4 1.2\"\n" +
            "741.5\t245\t\"b7/0.13 4/4 0.8\"\n" +
            "742.5\t30\t\"b7i/1.13 2/3 0.1\"\n" +
            "744.0\t32\t\"? 2/3 0.0\"\n" +
            "744.3\t96\t\"? 3/4 1.1\"\n" +
            "753.1\t268\t\"y13-18^2/0.22,b14-44^2/-0.25 4/4 1.0\"\n" +
            "757.4\t51\t\"? 3/3 0.6\"\n" +
            "761.9\t294\t\"y13^2/0.02 4/4 0.3\"\n" +
            "762.5\t139\t\"? 4/4 0.8\"\n" +
            "780.2\t33\t\"? 2/3 0.1\"\n" +
            "783.5\t33\t\"? 2/1 0.3\"\n" +
            "789.0\t54\t\"? 3/4 0.3\"\n" +
            "797.0\t87\t\"y14-17^2/0.11 4/4 0.3\"\n" +
            "801.2\t61\t\"? 3/1 0.5\"\n" +
            "804.2\t193\t\"? 4/4 2.4\"\n" +
            "805.5\t143\t\"y14^2/0.11 4/4 1.0\"\n" +
            "806.0\t29\t\"? 2/3 0.1\"\n" +
            "808.1\t21\t\"? 2/1 0.1\"\n" +
            "811.4\t34\t\"y7-44/0.01 2/1 0.3\"\n" +
            "813.4\t35\t\"? 3/4 0.1\"\n" +
            "815.8\t31\t\"? 2/3 0.3\"\n" +
            "818.4\t53\t\"? 4/4 0.2\"\n" +
            "820.5\t392\t\"? 3/4 3.2\"\n" +
            "821.7\t72\t\"?i 4/4 0.4\"\n" +
            "822.1\t183\t\"? 4/4 2.3\"\n" +
            "827.0\t47\t\"? 3/4 0.1\"\n" +
            "828.5\t611\t\"? 3/4 10.9\"\n" +
            "829.6\t70\t\"?i 3/4 0.6\"\n" +
            "831.7\t84\t\"? 4/4 0.4\"\n" +
            "833.1\t48\t\"? 3/4 0.3\"\n" +
            "834.2\t29\t\"?i 2/3 0.1\"\n" +
            "835.6\t81\t\"? 4/4 0.4\"\n" +
            "836.1\t89\t\"? 3/4 0.6\"\n" +
            "837.4\t73\t\"y7-18/0.01 3/4 0.3\"\n" +
            "838.4\t78\t\"y7-17/0.01 3/4 0.4\"\n" +
            "841.0\t94\t\"p-44/0.59,a8/-0.43 3/4 0.6\"\n" +
            "843.3\t126\t\"? 4/4 0.8\"\n" +
            "844.4\t974\t\"p-36/-0.00 4/4 5.0\"\n" +
            "845.1\t672\t\"p-35/0.19 3/4 2.7\"\n" +
            "846.5\t303\t\"? 3/4 2.6\"\n" +
            "849.8\t64\t\"? 4/4 0.3\"\n" +
            "855.5\t148\t\"y7/0.11 4/4 0.4\"\n" +
            "965.6\t24\t\"y8-18/0.15 2/2 0.1\"\n" +
            "966.7\t47\t\"b9-18/0.24,y8-17/0.25 4/4 0.2\"\n" +
            "983.5\t376\t\"y8/0.05 4/4 1.1\"\n" +
            "984.4\t213\t\"b9/-0.06 4/4 0.5\"\n" +
            "985.4\t34\t\"b9i/0.94 3/4 0.2\"\n" +
            "1013.6\t29\t\"? 2/2 0.1\"\n" +
            "1019.5\t29\t\"? 2/3 0.1\"\n" +
            "1021.3\t26\t\"? 2/2 0.1\"\n" +
            "1038.5\t47\t\"b10-17/0.01 4/4 0.2\"\n" +
            "1039.6\t45\t\"b10-17i/1.11 3/4 0.1\"\n" +
            "1055.4\t116\t\"b10/-0.09 4/4 0.4\"\n" +
            "1084.6\t33\t\"? 2/3 0.0\"\n" +
            "1096.4\t145\t\"y9/-0.13 4/4 0.3\"\n" +
            "1097.5\t79\t\"y9i/0.97 4/4 0.2\"\n" +
            "1121.3\t29\t\"? 2/3 0.1\"\n" +
            "1138.7\t75\t\"b11-18/0.16 4/4 0.1\"\n" +
            "1139.6\t57\t\"b11-17/0.06 3/4 0.2\"\n" +
            "1156.5\t107\t\"b11/-0.04 4/4 0.5\"\n" +
            "1157.7\t48\t\"b11i/1.16 3/4 0.3\"\n" +
            "1175.6\t248\t\"y10-18/0.01 4/4 0.2\"\n" +
            "1176.5\t180\t\"y10-17/-0.09 4/4 0.5\"\n" +
            "1193.5\t10000\t\"y10/-0.09 4/4 0.0\"\n" +
            "1194.6\t4444\t\"y10i/1.01 4/4 10.0\"\n" +
            "1195.3\t280\t\"y10i/1.71 4/4 3.5\"\n" +
            "1214.0\t29\t\"? 2/3 0.0\"\n" +
            "1249.3\t21\t\"? 2/1 0.1\"\n" +
            "1267.7\t131\t\"b12-18/0.12 4/4 0.4\"\n" +
            "1268.4\t89\t\"b12-17/-0.18 3/4 0.1\"\n" +
            "1285.6\t253\t\"b12/0.02 4/4 0.4\"\n" +
            "1286.5\t97\t\"b12i/0.92 4/4 0.3\"\n" +
            "1306.6\t392\t\"y11/-0.07 4/4 0.8\"\n" +
            "1307.7\t230\t\"y11i/1.03 4/4 0.5\"\n" +
            "1368.6\t38\t\"b13-18/-0.03 2/1 0.5\"\n" +
            "1386.7\t59\t\"b13/0.07 4/4 0.2\"\n" +
            "1387.7\t49\t\"b13i/1.07 4/4 0.3\"\n" +
            "1403.7\t24\t\"y12-18/0.00 2/2 0.1\"\n" +
            "1421.7\t289\t\"y12/0.00 4/4 0.2\"\n" +
            "1422.6\t206\t\"y12i/0.90 4/4 1.6\"\n" +
            "1514.6\t38\t\"? 3/4 0.3\"\n" +
            "1522.7\t41\t\"y13/-0.04 3/4 0.1\"\n" +
            "1532.6\t56\t\"b14-17/-0.10 4/4 0.2\"\n" +
            "1549.7\t153\t\"b14/0.00 4/4 0.4\"\n" +
            "1550.7\t46\t\"b14i/1.00 3/4 0.1\"\n";

        public const string TEXT_LIB_YEAST_NIST4 =
            "Name: VLHNFLTTPSK/2\n" +
            "MW: 1257.708\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=R.VLHNFLTTPSK.F/2 Mods=0 Parent=628.854 Inst=it Mz_diff=0.026 Mz_exact=628.8541 Mz_av=629.242 Protein=\"gi|171865|gb|AAC04947.1| Mdm10p: Mitochondria outer membrane protein [Saccharomyces cerevisiae]\" Pseq=4 Organism=\"yeast\" Se=3^X2:ex=0.017585/0.01742,td=759.3/710.7,sd=0/0,hs=27.45/5.15,bs=0.00017,b2=0.035,bd=1470^O1:ex=0.00966/0,td=5180/0,pr=1.87e-006/0,bs=0.00966,bd=5180^P1:sc=19.2/0,dc=12.3/0,ps=2.7/0,bs=0 Sample=1/cbs00174_001_01_cam,2,2 Nreps=2/5 Missing=0.2237/0.0377 Parent_med=628.88/0.08 Max2med_orig=110.1/36.0 Dotfull=0.693/0.000 Dot_cons=0.846/0.036 Unassign_all=0.331 Unassigned=0.238 Dotbest=0.88 Flags=0,0,0 Naa=11 DUScorr=10/0.17/1.7 Dottheory=0.95 Pfin=3.1e+007 Probcorr=1 Tfratio=1.2e+005 Pfract=0\n" +
            "Num peaks: 113\n" +
            "213.1\t283\t\"b2/-0.05 2/2 1.9\"\n" +
            "252.1\t166\t\"? 2/2 1.0\"\n" +
            "253.0\t179\t\"?i 2/1 2.2\"\n" +
            "260.5\t176\t\"? 2/2 2.0\"\n" +
            "270.3\t212\t\"? 2/2 2.8\"\n" +
            "331.2\t624\t\"y3/0.00 2/2 3.2\"\n" +
            "332.3\t506\t\"b3-18/0.09 2/2 0.8\"\n" +
            "341.3\t250\t\"? 2/2 1.9\"\n" +
            "350.3\t614\t\"b3/0.09 2/2 0.2\"\n" +
            "381.6\t204\t\"? 2/1 1.5\"\n" +
            "383.3\t363\t\"? 2/2 1.4\"\n" +
            "386.3\t251\t\"? 2/2 1.0\"\n" +
            "387.2\t213\t\"?i 2/1 1.4\"\n" +
            "393.3\t168\t\"? 2/2 1.2\"\n" +
            "399.2\t162\t\"? 2/2 1.6\"\n" +
            "413.2\t204\t\"b7^2/-0.03 2/2 2.1\"\n" +
            "414.5\t176\t\"y4-18/0.25 2/2 1.4\"\n" +
            "432.4\t829\t\"y4/0.15 2/2 2.9\"\n" +
            "435.4\t312\t\"? 2/2 4.6\"\n" +
            "440.7\t204\t\"b8-45^2/-0.55 2/2 0.0\"\n" +
            "446.6\t196\t\"b4-18/0.35 2/1 2.8\"\n" +
            "458.7\t176\t\"? 2/1 2.0\"\n" +
            "464.3\t176\t\"b4/0.05 2/2 1.4\"\n" +
            "471.3\t211\t\"? 2/1 1.7\"\n" +
            "480.5\t218\t\"? 2/2 0.4\"\n" +
            "482.0\t185\t\"? 2/2 1.2\"\n" +
            "486.5\t201\t\"? 2/1 1.3\"\n" +
            "489.6\t516\t\"b9-45*^2/-0.18 2/1 8.1\"\n" +
            "497.5\t183\t\"? 2/1 2.1\"\n" +
            "498.9\t397\t\"? 2/2 1.4\"\n" +
            "505.2\t321\t\"? 2/2 1.0\"\n" +
            "514.0\t815\t\"y9-18^2/0.22,y9-17^2/-0.28 2/2 2.4\"\n" +
            "514.9\t260\t\"y5-18/-0.39 2/1 4.3\"\n" +
            "523.0\t10000\t\"y9^2/0.22 2/2 4.9\"\n" +
            "525.4\t122\t\"? 2/1 0.9\"\n" +
            "526.3\t188\t\"?i 2/1 2.1\"\n" +
            "529.7\t159\t\"? 2/1 1.4\"\n" +
            "533.4\t550\t\"y5/0.11,b10-45^2/0.11 2/2 0.9\"\n" +
            "535.3\t300\t\"? 2/2 2.5\"\n" +
            "538.5\t180\t\"b10-35^2/0.21 2/1 1.3\"\n" +
            "547.0\t210\t\"b10-18^2/0.21,b10-17^2/-0.29 2/2 2.6\"\n" +
            "553.5\t332\t\"? 2/2 2.0\"\n" +
            "556.9\t153\t\"? 2/1 1.8\"\n" +
            "557.6\t182\t\"?i 2/1 1.0\"\n" +
            "560.8\t247\t\"? 2/2 1.1\"\n" +
            "561.8\t235\t\"?i 2/2 1.0\"\n" +
            "572.1\t253\t\"? 2/2 0.1\"\n" +
            "575.0\t391\t\"b5-36/-0.32 2/2 7.0\"\n" +
            "576.2\t519\t\"?i 2/2 7.5\"\n" +
            "579.6\t1122\t\"y10^2/0.28 2/2 2.3\"\n" +
            "582.9\t549\t\"a5/-0.42 2/2 6.7\"\n" +
            "584.7\t455\t\"? 2/2 3.0\"\n" +
            "585.6\t841\t\"?i 2/2 10.5\"\n" +
            "587.9\t303\t\"? 2/2 3.3\"\n" +
            "588.8\t332\t\"?i 2/1 4.0\"\n" +
            "592.4\t845\t\"? 2/2 2.6\"\n" +
            "593.5\t1228\t\"b5-18/0.18 2/2 5.7\"\n" +
            "594.3\t519\t\"b5-17/-0.02 2/2 1.5\"\n" +
            "596.3\t203\t\"? 2/1 3.0\"\n" +
            "598.3\t869\t\"? 2/2 3.6\"\n" +
            "599.2\t222\t\"?i 2/1 3.2\"\n" +
            "602.9\t170\t\"? 2/2 0.3\"\n" +
            "605.1\t582\t\"? 2/2 5.9\"\n" +
            "606.4\t599\t\"p-45/0.05,p-44/-0.45 2/2 9.9\"\n" +
            "607.0\t565\t\"p-44/0.15 2/2 3.2\"\n" +
            "607.6\t356\t\"? 2/2 4.7\"\n" +
            "610.7\t1875\t\"p-36/-0.14 2/2 11.6\"\n" +
            "611.5\t2983\t\"b5/0.18,p-35/0.15 2/2 0.9\"\n" +
            "612.3\t446\t\"b5i/0.98 2/2 0.8\"\n" +
            "614.1\t316\t\"? 2/2 1.1\"\n" +
            "615.4\t290\t\"?i 2/2 0.6\"\n" +
            "617.0\t398\t\"? 2/2 5.2\"\n" +
            "618.7\t4875\t\"? 2/2 89.4\"\n" +
            "619.3\t2155\t\"p-18/-0.55 2/2 1.7\"\n" +
            "620.1\t2134\t\"p-18/0.25,p-17/-0.25 2/2 19.6\"\n" +
            "621.1\t303\t\"p-18i/1.25 2/1 3.3\"\n" +
            "639.4\t162\t\"? 2/2 1.6\"\n" +
            "646.4\t261\t\"y6/0.02 2/2 0.8\"\n" +
            "647.4\t144\t\"y6i/1.02 2/1 0.5\"\n" +
            "663.4\t622\t\"? 2/2 4.6\"\n" +
            "666.4\t213\t\"? 2/2 1.4\"\n" +
            "691.6\t237\t\"? 2/1 2.7\"\n" +
            "696.3\t131\t\"a6/-0.11 2/1 0.7\"\n" +
            "707.5\t260\t\"b6-17/0.09 2/1 2.3\"\n" +
            "724.3\t465\t\"b6/-0.11 2/2 1.5\"\n" +
            "725.5\t259\t\"b6i/1.09 2/2 1.2\"\n" +
            "754.7\t198\t\"? 2/1 1.0\"\n" +
            "793.4\t720\t\"y7/-0.05 2/2 4.3\"\n" +
            "794.6\t358\t\"y7i/1.15 2/2 2.5\"\n" +
            "798.8\t251\t\"? 2/2 1.0\"\n" +
            "807.4\t168\t\"b7-18/-0.05 2/1 1.2\"\n" +
            "808.4\t268\t\"b7-17/-0.05 2/2 3.0\"\n" +
            "811.8\t234\t\"? 2/1 1.9\"\n" +
            "826.5\t337\t\"? 2/2 1.0\"\n" +
            "847.3\t214\t\"? 2/2 2.5\"\n" +
            "849.5\t167\t\"? 2/1 2.1\"\n" +
            "865.5\t442\t\"? 2/2 5.3\"\n" +
            "869.4\t667\t\"? 2/2 5.3\"\n" +
            "871.5\t271\t\"? 2/2 3.3\"\n" +
            "880.5\t297\t\"? 2/2 2.2\"\n" +
            "890.0\t339\t\"y8-17/-0.49,y8-18/0.51 2/2 1.3\"\n" +
            "906.9\t268\t\"y8/-0.59 2/2 0.4\"\n" +
            "907.5\t1996\t\"y8/0.01 2/2 0.5\"\n" +
            "908.5\t844\t\"b8-18/-0.00 2/2 3.8\"\n" +
            "910.5\t210\t\"? 2/2 1.1\"\n" +
            "926.6\t531\t\"b8/0.10 2/2 1.4\"\n" +
            "941.5\t131\t\"? 2/1 0.7\"\n" +
            "995.4\t367\t\"? 2/2 4.3\"\n" +
            "996.1\t270\t\"?i 2/1 4.2\"\n" +
            "1044.5\t549\t\"y9/-0.05 2/2 5.2\"\n" +
            "1045.7\t333\t\"y9i/1.15 2/2 1.1\"\n" +
            "1070.8\t268\t\"? 2/2 1.0\"\n" +
            "1110.6\t387\t\"b10/0.01 2/2 2.2\"\n";
    }
}