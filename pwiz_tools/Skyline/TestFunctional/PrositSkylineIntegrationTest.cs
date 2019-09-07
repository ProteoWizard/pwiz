/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Text;
using System.Windows.Forms;
using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs.Spectrum;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Prosit;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using Tensorflow;
using Tensorflow.Serving;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PrositSkylineIntegrationTest : AbstractFunctionalTestEx
    {
        private bool RecordData { get { return false; } }

        #region DATA
        // TODO: Repopulate query for multiple runs
        private static List<PrositQuery> QUERIES = new List<PrositQuery>(new PrositQuery[] {
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("CSLPRPWALTFSYGR", 0.2500f, 2)
                },
                new[] {
                    new[] {
                        02.7065f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.1312f,
                        00.0547f, 00.0000f, 00.3962f, 00.8615f, 00.0000f, 00.1920f,
                        00.1094f, 00.0000f, 00.2935f, 01.2940f, 00.0000f, 00.1636f,
                        00.2286f, 00.0000f, 00.1851f, 00.0000f, 00.0000f, 00.2193f,
                        00.1242f, 00.0000f, 00.0000f, 02.2329f, 00.0000f, 00.2332f,
                        00.3376f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.2044f,
                        00.5388f, 00.0000f, 00.0000f, 00.1027f, 00.0000f, 00.1928f,
                        00.4974f, 00.0000f, 00.0000f, 00.4388f, 00.0000f, 00.1903f,
                        00.0000f, 00.0000f, 00.0000f, 00.5365f, 00.0000f, 00.1959f,
                        04.6350f, 00.2769f, 00.0000f, 00.6684f, 00.1292f, 00.0592f,
                        00.0000f, 00.0000f, 00.0000f, 00.2068f, 00.1374f, 00.2790f,
                        00.0000f, 01.4514f, 00.8129f, 00.8150f, 00.1397f, 00.1908f,
                        00.0000f, 00.0000f, 00.0230f, 00.1897f, 00.0013f, 00.1799f,
                        00.0000f, 00.0000f, 00.0000f, 01.3128f, 00.0242f, 00.1212f,
                        00.0397f, 00.2031f, 00.0000f, 01.0146f, 00.3139f, 00.1500f,
                        00.0000f, 00.3301f, 00.0000f, 00.0391f, 00.1806f, 00.2935f,
                        00.0000f, 00.0692f, 00.0000f, 00.0000f, 00.0000f, 00.4034f,
                        00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.6166f,
                        00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.4693f,
                        00.0990f, 00.0000f, 00.3782f, 00.0000f, 00.0000f, 00.0000f,
                        00.0000f, 00.0390f, 01.2914f, 00.4004f, 00.0000f, 00.0000f,
                        00.0000f, 00.8711f, 02.5637f, 00.5392f, 00.0000f, 00.0000f,
                        00.0000f, 01.3059f, 03.5145f, 02.2064f, 00.0000f, 00.0000f,
                        00.0000f, 00.0000f, 04.1777f, 02.3881f, 00.0000f, 00.0000f,
                        00.0000f, 00.0000f, 04.3525f, 02.6121f, 00.0000f, 00.0000f,
                        02.0565f, 00.0000f, 04.9462f, 06.1795f, 01.2043f, 00.0000f,
                        01.5243f, 00.0000f, 02.4337f, 00.0000f, 00.0000f, 00.0000f,
                        00.0000f, 00.3037f, 01.7031f, 00.0000f, 00.0000f, 00.0000f,
                        00.0000f, 00.0000f, 01.3759f, 06.6724f, 00.0000f, 00.0141f
                    }
                }
            ),
            new PrositRetentionTimeQuery(
                new[] {
                    "CSLPRPWALTFSYGR"
                },
                new[] {
                    1.2758f
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("CSLPRPWALTFSYGR", 0.2600f, 2)
                },
                new[] {
                    new[] {
                        02.9479f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.1358f,
                        00.1239f, 00.0000f, 00.3696f, 01.1136f, 00.0000f, 00.1859f,
                        00.1675f, 00.0000f, 00.3022f, 01.4638f, 00.0000f, 00.1554f,
                        00.3027f, 00.0000f, 00.1644f, 00.0000f, 00.0000f, 00.2076f,
                        00.1878f, 00.0000f, 00.0000f, 02.3082f, 00.0000f, 00.2212f,
                        00.4359f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.1947f,
                        00.6277f, 00.0000f, 00.0000f, 00.1609f, 00.0000f, 00.1832f,
                        00.6080f, 00.0000f, 00.0000f, 00.5198f, 00.0000f, 00.1814f,
                        00.0000f, 00.0000f, 00.0000f, 00.6266f, 00.0112f, 00.1885f,
                        05.3370f, 00.3215f, 00.0000f, 00.8345f, 00.1911f, 00.0280f,
                        00.0000f, 00.0000f, 00.0000f, 00.3039f, 00.1807f, 00.2743f,
                        00.0000f, 01.5845f, 00.8871f, 00.8679f, 00.1695f, 00.2118f,
                        00.0000f, 00.0000f, 00.0000f, 00.2837f, 00.0223f, 00.1881f,
                        00.0000f, 00.0335f, 00.0000f, 01.3894f, 00.0255f, 00.1286f,
                        00.0553f, 00.2413f, 00.0000f, 01.0587f, 00.2533f, 00.1723f,
                        00.0000f, 00.3773f, 00.0000f, 00.0535f, 00.1474f, 00.3019f,
                        00.0000f, 00.1010f, 00.0000f, 00.0000f, 00.0000f, 00.3821f,
                        00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.5539f,
                        00.0000f, 00.0000f, 00.0658f, 00.0000f, 00.0000f, 00.3369f,
                        00.1582f, 00.0000f, 00.8251f, 00.0000f, 00.0000f, 00.0000f,
                        00.0000f, 00.0219f, 02.2645f, 00.5306f, 00.0000f, 00.0000f,
                        00.1029f, 01.0964f, 04.1266f, 01.0470f, 00.0000f, 00.0000f,
                        00.0000f, 01.6618f, 05.1014f, 03.3660f, 00.0000f, 00.0000f,
                        00.0000f, 00.2472f, 05.4700f, 03.1781f, 00.0000f, 00.1938f,
                        00.0000f, 00.0000f, 04.9665f, 03.0916f, 00.0000f, 00.1857f,
                        00.0000f, 00.0000f, 03.7349f, 05.2528f, 02.9787f, 00.0000f,
                        01.4385f, 00.1601f, 03.4375f, 00.3215f, 00.1083f, 00.0000f,
                        00.3835f, 00.5911f, 02.2657f, 00.0000f, 00.0000f, 00.0000f,
                        00.0000f, 00.0000f, 01.4777f, 02.2136f, 00.0000f, 00.0000f
                    }
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("LGGEEVSVACK", 0.2600f, 2)
                },
                new[] {
                    new[] {
                        01.1753f, 00.0000f, 00.0255f, 00.0000f, 00.0000f, 00.0890f,
                        02.8214f, 00.0000f, 00.5992f, 03.9895f, 00.0000f, 00.0000f,
                        04.7079f, 00.0000f, 00.0000f, 02.1472f, 00.0000f, 00.0000f,
                        02.0359f, 00.0001f, 00.0000f, 02.8442f, 00.0000f, 00.0000f,
                        11.5266f, 00.0000f, 00.0000f, 02.1043f, 00.0000f, 00.0000f,
                        07.1053f, 00.0390f, 00.0000f, 01.8099f, 00.0000f, 00.0000f,
                        07.0693f, 00.2669f, 00.0000f, 00.5677f, 00.0321f, 00.0000f,
                        01.2982f, 00.1723f, 00.0000f, 00.4490f, 00.0045f, 00.0000f,
                        05.5559f, 00.7857f, 00.0000f, 00.2580f, 00.0000f, 00.0000f,
                        09.4965f, 00.8089f, 00.0000f, 00.2154f, 00.0140f, 00.0000f,
                        07.6129f, 01.1750f, 00.0000f, 00.2108f, 00.0039f, 00.0000f,
                        03.6408f, 02.8106f, 00.0438f, 00.3270f, 00.0172f, 00.0000f,
                        04.0563f, 04.0705f, 00.6959f, 00.5067f, 00.0848f, 00.0000f,
                        06.9249f, 02.2006f, 00.7846f, 00.7598f, 00.1330f, 00.0000f,
                        06.5360f, 01.1550f, 00.9829f, 01.4156f, 00.1907f, 00.0000f,
                        08.3633f, 00.9448f, 00.5071f, 02.5810f, 00.1482f, 00.0000f,
                        07.1340f, 01.1244f, 00.0000f, 02.9186f, 00.1936f, 00.0000f,
                        05.9489f, 01.1874f, 00.0000f, 00.4662f, 00.1363f, 00.0000f,
                        02.3698f, 01.5297f, 00.0000f, 00.8107f, 00.1634f, 00.0000f,
                        03.8289f, 01.2489f, 00.0962f, 01.1602f, 00.3061f, 00.0000f,
                        09.4913f, 01.6460f, 00.2217f, 00.1192f, 00.2950f, 00.0000f,
                        08.6516f, 04.0817f, 01.7418f, 00.1255f, 00.2970f, 00.0000f,
                        03.9564f, 13.8413f, 05.7802f, 00.0000f, 00.3477f, 00.0000f,
                        08.7759f, 08.6748f, 04.4983f, 03.3672f, 00.0000f, 00.0000f,
                        06.0241f, 05.0766f, 00.0576f, 08.4273f, 00.0583f, 00.0000f,
                        11.1881f, 03.4154f, 00.0000f, 04.8480f, 00.1314f, 00.0000f,
                        08.2150f, 03.2976f, 00.0000f, 01.5688f, 00.1915f, 00.0000f,
                        07.6837f, 05.9245f, 00.0936f, 01.1111f, 00.0031f, 00.0000f,
                        14.3257f, 08.1581f, 04.2569f, 00.9787f, 00.1524f, 00.0000f
                    }
                }
            ),
            new PrositRetentionTimeQuery(
                new[] {
                    "LGGEEVSVACK"
                },
                new[] {
                    -0.6807f
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("LGGEEVSVACK", 0.2700f, 2)
                },
                new[] {
                    new[] {
                        01.2001f, 00.0000f, 00.0297f, 00.0000f, 00.0000f, 00.0865f,
                        03.1309f, 00.0000f, 00.6921f, 04.1637f, 00.0000f, 00.0000f,
                        04.9597f, 00.0000f, 00.0000f, 02.3777f, 00.0000f, 00.0000f,
                        02.1258f, 00.0000f, 00.0000f, 02.7272f, 00.0000f, 00.0000f,
                        11.8386f, 00.0000f, 00.0000f, 01.9358f, 00.0000f, 00.0000f,
                        07.3578f, 00.0468f, 00.0000f, 01.5186f, 00.0011f, 00.0000f,
                        07.2697f, 00.2759f, 00.0000f, 00.5135f, 00.0334f, 00.0000f,
                        01.3888f, 00.1744f, 00.0000f, 00.3669f, 00.0065f, 00.0000f,
                        05.7812f, 00.5915f, 00.0000f, 00.2039f, 00.0000f, 00.0000f,
                        10.3678f, 00.6447f, 00.0000f, 00.1695f, 00.0151f, 00.0000f,
                        08.2329f, 00.9806f, 00.0000f, 00.1708f, 00.0000f, 00.0000f,
                        03.8091f, 02.3633f, 00.0000f, 00.2858f, 00.0000f, 00.0110f,
                        04.0784f, 03.2144f, 00.5578f, 00.4843f, 00.0315f, 00.0000f,
                        07.2090f, 01.5937f, 00.6465f, 00.6973f, 00.0524f, 00.0000f,
                        07.8010f, 00.7801f, 00.7084f, 01.3804f, 00.0982f, 00.0000f,
                        09.0837f, 00.5460f, 00.0299f, 02.4289f, 00.0908f, 00.0000f,
                        08.2416f, 00.6703f, 00.0000f, 02.3254f, 00.1488f, 00.0000f,
                        07.7887f, 00.7822f, 00.0000f, 00.3756f, 00.0936f, 00.0000f,
                        04.9870f, 01.3845f, 00.0000f, 00.3661f, 00.0868f, 00.0000f,
                        01.6557f, 00.6523f, 00.0000f, 00.9193f, 00.0494f, 00.0000f,
                        04.1267f, 00.4645f, 00.0229f, 00.8983f, 00.2762f, 00.0000f,
                        10.2360f, 01.0516f, 00.0000f, 00.2218f, 00.3141f, 00.0000f,
                        07.5631f, 03.1065f, 01.1011f, 00.2286f, 00.2657f, 00.0000f,
                        06.7648f, 11.3413f, 03.6166f, 00.5273f, 00.2270f, 00.0000f,
                        07.5019f, 02.3187f, 01.9466f, 04.4037f, 00.0665f, 00.0000f,
                        07.5991f, 01.3051f, 00.2516f, 06.8577f, 00.1161f, 00.0000f,
                        09.7545f, 01.3100f, 00.2265f, 02.0953f, 00.2235f, 00.0000f,
                        08.0150f, 01.5923f, 00.2322f, 00.4829f, 00.1725f, 00.0000f,
                        02.0093f, 02.0546f, 00.4195f, 01.2210f, 00.0933f, 00.0000f
                    }
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("VGQPGDAGAAGPVAPLCPGR", 0.2700f, 2)
                },
                new[] {
                    new[] {
                        00.3673f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.0628f,
                        00.0817f, 00.0000f, 00.0191f, 00.2013f, 00.0000f, 00.0192f,
                        03.5219f, 00.0000f, 00.0000f, 01.7400f, 00.0000f, 00.0000f,
                        01.3865f, 00.0000f, 00.0000f, 00.1317f, 00.0000f, 00.0000f,
                        00.1113f, 00.0000f, 00.0000f, 00.2898f, 00.0000f, 00.0000f,
                        09.9397f, 00.0000f, 00.0000f, 01.0007f, 00.0180f, 00.0000f,
                        03.7263f, 00.0000f, 00.0000f, 01.4522f, 00.0114f, 00.0000f,
                        00.5274f, 00.0000f, 00.0000f, 00.8445f, 00.0210f, 00.0000f,
                        05.2733f, 00.0000f, 00.0000f, 01.8860f, 00.0050f, 00.0000f,
                        08.5391f, 00.0000f, 00.0000f, 01.9729f, 00.0025f, 00.0000f,
                        05.1824f, 00.0000f, 00.0000f, 00.9329f, 00.0000f, 00.0000f,
                        01.6182f, 00.0000f, 00.0000f, 00.4181f, 00.0000f, 00.0000f,
                        04.4460f, 00.0000f, 00.0000f, 01.0258f, 00.0000f, 00.0000f,
                        02.5802f, 00.0464f, 00.0000f, 02.0502f, 00.0000f, 00.0000f,
                        00.5081f, 00.1670f, 00.0000f, 00.0000f, 00.0000f, 00.0000f,
                        00.4933f, 00.0000f, 00.0000f, 00.1583f, 00.0000f, 00.0000f,
                        02.1469f, 23.2693f, 00.0000f, 00.4488f, 00.0302f, 00.0000f,
                        00.0000f, 00.7601f, 00.1648f, 00.0464f, 00.0300f, 00.1659f,
                        00.0000f, 00.2670f, 00.0000f, 00.0000f, 00.0280f, 00.1616f,
                        00.0000f, 00.5209f, 00.1103f, 00.0020f, 00.0890f, 00.1568f,
                        00.1568f, 00.6308f, 00.1299f, 00.0000f, 00.0476f, 00.0011f,
                        00.2824f, 00.5591f, 00.1027f, 00.0000f, 00.0222f, 00.0035f,
                        00.4179f, 00.4120f, 00.2031f, 00.0000f, 00.0000f, 00.0000f,
                        00.4199f, 00.2157f, 00.2809f, 00.0000f, 00.0000f, 00.0000f,
                        00.4467f, 00.1344f, 00.4401f, 00.0000f, 00.0000f, 00.0000f,
                        01.1569f, 00.1308f, 00.7701f, 00.0000f, 00.0000f, 00.0000f,
                        01.8015f, 00.0682f, 01.1255f, 00.0000f, 00.0000f, 00.0000f,
                        01.1861f, 00.0000f, 01.2234f, 00.3294f, 00.0000f, 00.0000f,
                        02.2167f, 00.0000f, 00.8427f, 00.0000f, 00.0000f, 00.0000f
                    }
                }
            ),
            new PrositRetentionTimeQuery(
                new[] {
                    "VGQPGDAGAAGPVAPLCPGR"
                },
                new[] {
                    0.0881f
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("VGQPGDAGAAGPVAPLCPGR", 0.2800f, 2)
                },
                new[] {
                    new[] {
                        00.4122f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.0639f,
                        00.0918f, 00.0000f, 00.0416f, 00.2581f, 00.0000f, 00.0208f,
                        03.6323f, 00.0000f, 00.0000f, 01.7107f, 00.0000f, 00.0000f,
                        01.4596f, 00.0000f, 00.0000f, 00.1248f, 00.0000f, 00.0000f,
                        00.1662f, 00.0000f, 00.0000f, 00.2842f, 00.0000f, 00.0000f,
                        10.1519f, 00.0000f, 00.0000f, 00.8856f, 00.0254f, 00.0000f,
                        03.7979f, 00.0000f, 00.0000f, 01.1955f, 00.0121f, 00.0000f,
                        00.5773f, 00.0000f, 00.0000f, 00.7637f, 00.0235f, 00.0000f,
                        05.5175f, 00.0005f, 00.0000f, 01.6356f, 00.0082f, 00.0000f,
                        08.9926f, 00.0000f, 00.0000f, 01.5915f, 00.0031f, 00.0000f,
                        05.5038f, 00.0000f, 00.0000f, 00.7137f, 00.0000f, 00.0000f,
                        01.7384f, 00.0000f, 00.0000f, 00.3480f, 00.0000f, 00.0000f,
                        04.7442f, 00.0000f, 00.0000f, 00.7921f, 00.0000f, 00.0000f,
                        02.7147f, 00.0488f, 00.0000f, 01.5496f, 00.0000f, 00.0000f,
                        00.5090f, 00.1365f, 00.0000f, 00.0000f, 00.0000f, 00.0000f,
                        00.5004f, 00.0000f, 00.0000f, 00.1592f, 00.0000f, 00.0000f,
                        02.0327f, 21.8731f, 00.0000f, 00.2969f, 00.0223f, 00.0000f,
                        00.0000f, 00.6218f, 00.0532f, 00.0059f, 00.0301f, 00.1668f,
                        00.0000f, 00.2161f, 00.0000f, 00.0000f, 00.0335f, 00.1622f,
                        00.0000f, 00.5100f, 00.0688f, 00.0000f, 00.0831f, 00.1584f,
                        00.1404f, 00.6559f, 00.1183f, 00.0000f, 00.0516f, 00.0189f,
                        00.3413f, 00.5679f, 00.0882f, 00.0000f, 00.0284f, 00.0000f,
                        00.5327f, 00.4219f, 00.1606f, 00.0000f, 00.0000f, 00.0000f,
                        00.5841f, 00.2520f, 00.2434f, 00.0000f, 00.0000f, 00.0000f,
                        00.6047f, 00.1579f, 00.4060f, 00.0000f, 00.0000f, 00.0000f,
                        01.5430f, 00.1928f, 00.7164f, 00.0000f, 00.0000f, 00.0000f,
                        02.4746f, 00.1048f, 01.0021f, 00.0000f, 00.0000f, 00.0000f,
                        00.4883f, 00.0000f, 00.5548f, 00.1642f, 00.0000f, 00.0000f,
                        05.3556f, 00.0000f, 00.0817f, 00.0000f, 00.0000f, 00.0000f
                    }
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("GSYNLQDLLAQAK", 0.2800f, 2)
                },
                new[] {
                    new[] {
                        01.5784f, 00.0000f, 00.0425f, 00.0000f, 00.0036f, 00.3137f,
                        04.0217f, 00.0000f, 00.2322f, 00.4861f, 00.0000f, 00.0000f,
                        03.4199f, 00.0000f, 00.0000f, 01.0904f, 00.0000f, 00.0000f,
                        08.0363f, 00.0000f, 00.0000f, 02.7388f, 00.0000f, 00.0000f,
                        04.5245f, 00.0000f, 00.0000f, 03.3779f, 00.0000f, 00.0000f,
                        03.4030f, 00.0000f, 00.0000f, 00.7038f, 00.0000f, 00.0000f,
                        10.1399f, 00.0000f, 00.0000f, 00.4320f, 00.0000f, 00.0000f,
                        14.0828f, 00.2168f, 00.0000f, 00.4830f, 00.0000f, 00.0000f,
                        06.4538f, 00.4837f, 00.0000f, 00.3934f, 00.0061f, 00.0000f,
                        04.3536f, 00.4970f, 00.0000f, 00.3141f, 00.0290f, 00.0000f,
                        00.2813f, 01.0668f, 00.0000f, 00.1989f, 00.0136f, 00.2495f,
                        00.0000f, 00.0865f, 00.0000f, 00.1527f, 00.0294f, 00.3045f,
                        00.0000f, 00.0000f, 00.1349f, 00.1860f, 00.0000f, 00.2769f,
                        00.1678f, 00.0000f, 00.0000f, 00.4420f, 00.0000f, 00.0000f,
                        00.8349f, 00.0000f, 00.0000f, 00.3448f, 00.0000f, 00.0000f,
                        02.8001f, 00.0000f, 00.0000f, 00.5443f, 00.0000f, 00.0000f,
                        05.4801f, 00.0000f, 00.0000f, 01.1109f, 00.0000f, 00.0000f,
                        05.8228f, 00.0000f, 00.0000f, 00.2646f, 00.0000f, 00.0000f,
                        06.7092f, 00.0000f, 00.0000f, 00.1064f, 00.0000f, 00.0000f,
                        08.0636f, 00.0000f, 00.0000f, 00.0685f, 00.0000f, 00.0000f,
                        06.2093f, 00.0749f, 00.0000f, 00.1398f, 00.0000f, 00.0000f,
                        02.8279f, 00.4059f, 00.0000f, 00.1739f, 00.0000f, 00.0000f,
                        00.7102f, 00.2477f, 00.0000f, 00.0108f, 00.0000f, 00.1132f,
                        00.0258f, 00.0942f, 00.0000f, 00.1195f, 00.0000f, 00.2209f,
                        00.0000f, 00.0695f, 00.0000f, 00.0972f, 00.0000f, 00.2035f,
                        00.3786f, 00.0000f, 00.0000f, 00.3821f, 00.0000f, 00.0000f,
                        01.0310f, 00.0000f, 00.0000f, 00.4180f, 00.0000f, 00.0000f,
                        03.3174f, 00.0000f, 00.0000f, 00.4184f, 00.0000f, 00.0000f,
                        06.2367f, 00.0000f, 00.0000f, 00.8009f, 00.0000f, 00.0000f
                    }
                }
            ),
            new PrositRetentionTimeQuery(
                new[] {
                    "GSYNLQDLLAQAK"
                },
                new[] {
                    1.2877f
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("GSYNLQDLLAQAK", 0.2900f, 2)
                },
                new[] {
                    new[] {
                        01.6631f, 00.0000f, 00.0457f, 00.0000f, 00.0017f, 00.3151f,
                        04.1243f, 00.0000f, 00.2375f, 00.5476f, 00.0000f, 00.0000f,
                        03.5341f, 00.0000f, 00.0000f, 01.1146f, 00.0000f, 00.0000f,
                        08.4241f, 00.0000f, 00.0000f, 02.6149f, 00.0000f, 00.0000f,
                        04.9141f, 00.0000f, 00.0000f, 03.0356f, 00.0000f, 00.0000f,
                        03.7886f, 00.0000f, 00.0000f, 00.5790f, 00.0000f, 00.0000f,
                        10.5761f, 00.0000f, 00.0000f, 00.4038f, 00.0000f, 00.0000f,
                        14.2411f, 00.1943f, 00.0000f, 00.4129f, 00.0000f, 00.0000f,
                        06.6658f, 00.4286f, 00.0000f, 00.2959f, 00.0037f, 00.0000f,
                        04.4025f, 00.3633f, 00.0000f, 00.2478f, 00.0237f, 00.0000f,
                        00.2969f, 00.7041f, 00.0000f, 00.1306f, 00.0051f, 00.2529f,
                        00.0000f, 00.0490f, 00.0000f, 00.1005f, 00.0181f, 00.3026f,
                        00.0000f, 00.0000f, 00.0000f, 00.1737f, 00.0000f, 00.2823f,
                        00.0749f, 00.0000f, 00.0000f, 00.3455f, 00.0000f, 00.0622f,
                        00.5586f, 00.0000f, 00.0000f, 00.3153f, 00.0000f, 00.0000f,
                        02.0928f, 00.0000f, 00.0000f, 00.4843f, 00.0000f, 00.0000f,
                        04.3040f, 00.0000f, 00.0000f, 00.6589f, 00.0000f, 00.0000f,
                        06.2345f, 00.0000f, 00.0000f, 00.1757f, 00.0000f, 00.0000f,
                        06.3755f, 00.0000f, 00.0000f, 00.0311f, 00.0000f, 00.0000f,
                        06.9513f, 00.0000f, 00.0000f, 00.0000f, 00.0000f, 00.0000f,
                        06.1795f, 00.0000f, 00.0000f, 00.0614f, 00.0000f, 00.0000f,
                        02.4826f, 00.1624f, 00.0000f, 00.1086f, 00.0000f, 00.0000f,
                        00.4948f, 00.0985f, 00.0000f, 00.0000f, 00.0000f, 00.1163f,
                        00.0000f, 00.0457f, 00.0000f, 00.0715f, 00.0000f, 00.2107f,
                        00.0000f, 00.0000f, 00.0000f, 00.0198f, 00.0000f, 00.2042f,
                        00.2767f, 00.0000f, 00.0000f, 00.1995f, 00.0000f, 00.0898f,
                        00.6738f, 00.0000f, 00.0000f, 00.2301f, 00.0000f, 00.0000f,
                        02.0898f, 00.0000f, 00.0000f, 00.2706f, 00.0000f, 00.0000f,
                        04.0877f, 00.0000f, 00.0000f, 00.3059f, 00.0000f, 00.0000f
                    }
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("TSDQIHFFFAK", 0.2900f, 2)
                },
                new[] {
                    new[] {
                        04.5152f, 00.0000f, 00.0000f, 00.0421f, 00.0000f, 00.1345f,
                        04.5037f, 00.0000f, 00.1990f, 02.8671f, 00.0000f, 00.0000f,
                        03.3479f, 00.0000f, 00.0000f, 01.6581f, 00.0000f, 00.0000f,
                        03.8008f, 00.0000f, 00.0000f, 04.3050f, 00.0000f, 00.0000f,
                        05.3557f, 00.0000f, 00.0000f, 01.3558f, 00.0000f, 00.0000f,
                        11.6346f, 00.5537f, 00.0000f, 02.8391f, 00.0258f, 00.0000f,
                        04.1170f, 00.4302f, 00.0000f, 02.5444f, 00.0879f, 00.0000f,
                        01.3046f, 01.5145f, 00.0000f, 02.4978f, 00.2044f, 00.0000f,
                        02.6950f, 05.9758f, 00.0000f, 02.3036f, 00.1727f, 00.0000f,
                        00.3685f, 05.0342f, 00.6905f, 01.9129f, 00.1046f, 00.2433f,
                        00.0000f, 04.6583f, 02.2043f, 01.5566f, 00.1679f, 00.2834f,
                        00.0000f, 03.2693f, 02.1799f, 00.8232f, 00.2006f, 00.2660f,
                        01.2400f, 04.9986f, 02.4838f, 00.0000f, 00.0000f, 00.0453f,
                        03.9690f, 03.5280f, 04.9444f, 01.7765f, 00.0000f, 00.0000f,
                        03.8465f, 01.7811f, 00.0000f, 03.7701f, 00.3920f, 00.0000f,
                        02.6000f, 01.0332f, 00.0000f, 01.6902f, 00.4032f, 00.0000f,
                        06.0302f, 00.2199f, 00.0000f, 01.7007f, 00.1221f, 00.3230f,
                        08.4343f, 00.5977f, 00.0000f, 04.3243f, 00.0000f, 00.0000f,
                        10.6654f, 00.8430f, 00.0000f, 07.6008f, 00.0000f, 00.0000f,
                        02.9192f, 00.4351f, 00.0000f, 01.2209f, 00.0000f, 00.0000f,
                        01.2868f, 01.5288f, 00.4642f, 05.6702f, 01.8925f, 00.4039f,
                        01.7733f, 02.7389f, 00.5328f, 05.7862f, 00.2804f, 00.3686f,
                        00.4228f, 03.1640f, 00.0716f, 02.0862f, 00.4130f, 00.4416f,
                        00.0000f, 03.0853f, 00.6281f, 01.9221f, 00.3514f, 00.4649f,
                        00.0000f, 03.3689f, 00.4195f, 00.4631f, 00.2795f, 00.3746f,
                        00.2092f, 02.1876f, 00.5183f, 00.0453f, 00.1566f, 00.3229f,
                        00.7618f, 00.8204f, 00.0000f, 00.2539f, 00.1086f, 00.2915f,
                        00.5900f, 00.2713f, 00.0000f, 00.6874f, 00.1313f, 00.2422f,
                        02.0888f, 00.1993f, 00.0000f, 02.0343f, 00.1659f, 00.0000f
                    }
                }
            ),
            new PrositRetentionTimeQuery(
                new[] {
                    "TSDQIHFFFAK"
                },
                new[] {
                    0.8181f
                }
            ),
            new PrositIntensityQuery(
                new[] {
                    new PrositIntensityInput("TSDQIHFFFAK", 0.3000f, 2)
                },
                new[] {
                    new[] {
                        04.8801f, 00.0000f, 00.0000f, 00.0563f, 00.0000f, 00.1318f,
                        04.8529f, 00.0000f, 00.1932f, 03.1136f, 00.0000f, 00.0000f,
                        03.5770f, 00.0039f, 00.0000f, 01.7787f, 00.0000f, 00.0000f,
                        04.1026f, 00.0000f, 00.0000f, 04.4025f, 00.0000f, 00.0000f,
                        05.5400f, 00.0000f, 00.0000f, 01.1920f, 00.0000f, 00.0000f,
                        11.3984f, 00.7546f, 00.0000f, 02.7907f, 00.0408f, 00.0000f,
                        04.0174f, 00.4812f, 00.0000f, 02.6832f, 00.1273f, 00.0000f,
                        01.2172f, 01.1454f, 00.0000f, 02.5001f, 00.1819f, 00.0000f,
                        02.6244f, 04.4652f, 00.0000f, 01.9927f, 00.1216f, 00.0040f,
                        00.4106f, 04.2402f, 00.1699f, 01.3542f, 00.0828f, 00.2472f,
                        00.0000f, 03.6649f, 01.1268f, 01.0233f, 00.0884f, 00.2819f,
                        00.0000f, 02.0849f, 01.0790f, 00.4281f, 00.0820f, 00.2555f,
                        00.6046f, 02.2258f, 00.9353f, 00.0000f, 00.0000f, 00.1657f,
                        01.8121f, 00.8989f, 00.0000f, 00.0963f, 00.0000f, 00.0000f,
                        02.4101f, 00.4740f, 00.0000f, 01.4341f, 00.0000f, 00.0000f,
                        02.5255f, 00.3308f, 00.0000f, 01.5456f, 00.0000f, 00.0000f,
                        04.4845f, 00.2240f, 00.0000f, 02.7900f, 00.0000f, 00.0000f,
                        04.0887f, 00.2133f, 00.0000f, 02.1594f, 00.0000f, 00.0000f,
                        01.2870f, 00.6138f, 00.0000f, 02.9152f, 00.0543f, 00.0000f,
                        01.0837f, 01.6291f, 00.0000f, 03.7984f, 00.0345f, 00.0000f,
                        00.8339f, 01.9558f, 00.0000f, 00.9896f, 00.0000f, 00.1910f,
                        00.0000f, 01.8477f, 00.0000f, 00.9531f, 00.0000f, 00.2421f,
                        00.0311f, 01.7189f, 00.0857f, 00.2396f, 00.0000f, 00.1939f,
                        00.4447f, 00.9471f, 00.0000f, 00.0000f, 00.0000f, 00.1460f,
                        00.4975f, 00.2295f, 00.0000f, 00.1410f, 00.0000f, 00.0988f,
                        00.8425f, 00.1156f, 00.0000f, 00.2409f, 00.0000f, 00.0368f,
                        01.7933f, 00.1227f, 00.0000f, 00.2945f, 00.0000f, 00.0000f,
                        01.3749f, 00.1271f, 00.0000f, 00.6733f, 00.0000f, 00.0000f,
                        00.4913f, 00.2870f, 00.0000f, 01.9644f, 00.0000f, 00.0000f
                    }
                }
            ),
        });
        #endregion

        [TestMethod]
        public void TestPrositSkylineIntegration()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));

            var doc = SkylineWindow.Document;
            RunUI(() =>
            {
                // Add Prosit supported mods
                SkylineWindow.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideModifications(pm =>
                {
                    return pm.ChangeStaticModifications(new[]
                    {
                        UniMod.DictStructuralModNames[@"Carbamidomethyl (C)"],
                        UniMod.DictStructuralModNames[@"Oxidation (M)"]
                    });
                }), false);
            });

            WaitForDocumentChange(doc);

            // Set up library match

            // Show all ions and charges of interest
            Settings.Default.ShowBIons = true;
            Settings.Default.ShowYIons = true;
            Settings.Default.ShowCharge1 = true;
            Settings.Default.ShowCharge2 = true;
            Settings.Default.ShowCharge3 = true;

            // Enable vertical pan
            Settings.Default.LockYAxis = false;

            PrositPredictionClient.Current = RecordData
                ? new FakePrositPredictionClient(Constants.DEV_PROSIT_SERVER)
                : new FakePrositPredictionClient(QUERIES);

            if (RecordData)
                Console.WriteLine("private static List<PrositQuery> QUERIES = new List<PrositQuery>(new PrositQuery[] {");

            TestPrositOptions();
            TestPrositSinglePrecursorPredictions();
            TestLivePrositMirrorPlots();
            PauseTest();
            Assert.AreEqual(QUERIES.Count, ((FakePrositPredictionClient)PrositPredictionClient.Current).QueryIndex);
            PrositPredictionClient.Current = null;
            if (RecordData)
                Console.WriteLine("});");
            
        }

        public void TestPrositOptions()
        {
            // Enable Prosit
            Settings.Default.Prosit = true;

            // For now just set all Prosit settings
            RunDlg<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Prosit),
                dlg =>
                {
                    dlg.PrositServerCombo = null;
                    dlg.PrositIntensityModelCombo = "intensity_2";
                    dlg.PrositRetentionTimeModelCombo = "iRT";
                    dlg.CECombo = 28;
                    dlg.DialogResult = DialogResult.OK;
                });

            Assert.AreEqual(null, Settings.Default.PrositServer);
            Assert.AreEqual("intensity_2", Settings.Default.PrositIntensityModel);
            Assert.AreEqual("iRT", Settings.Default.PrositRetentionTimeModel);
            Assert.AreEqual(28, Settings.Default.PrositNCE);
        }

        public void TestPrositSinglePrecursorPredictions()
        {
            var client = (FakePrositPredictionClient) PrositPredictionClient.Current;
            // Selecting a protein should not make any predictions
            SelectNode(SrmDocument.Level.MoleculeGroups, 0);
            Assert.IsFalse(SkylineWindow.GraphSpectrum.HasSpectrum);
            Assert.AreEqual(SkylineWindow.GraphSpectrum.GraphTitle,
                Resources.UnavailableMSGraphItem_UnavailableMSGraphItem_Spectrum_information_unavailable);

            var baseCE = 25;
            Settings.Default.PrositNCE = baseCE;

            // Select several peptides and make sure they are displayed correctly
            for (int i = 0; i < 5; ++i)
            {
                // Select node, causing prosit predictions to be made
                SelectNode(SrmDocument.Level.Molecules, i); // i'th peptide
                // Get selected node, since we need it for calculating MZs. Selecting a node is instant,
                // the prediction not
                TreeNodeMS treeNodeMS = null;
                RunUI(() => treeNodeMS = SkylineWindow.SelectedNode);
                var node = treeNodeMS as PeptideTreeNode;
                Assert.IsNotNull(node);
                var pep = node.Model as PeptideDocNode;
                Assert.IsNotNull(pep);
                var precursor = pep.TransitionGroups.First();

                WaitForSpectrum(precursor, baseCE + i);

                if (!RecordData)
                    AssertIntensityAndIRTSpectrumCorrect(pep, client.QueryIndex);

                RunUI(() =>
                {
                    Assert.IsTrue(SkylineWindow.GraphSpectrum.NCEVisible);
                    Assert.IsFalse(SkylineWindow.GraphSpectrum.MirrorComboVisible);
                    Assert.AreEqual(Settings.Default.PrositNCE, SkylineWindow.GraphSpectrum.PrositNCE);

                    // Change NCE and predict again
                    ++SkylineWindow.GraphSpectrum.PrositNCE;

                    Assert.AreEqual(Settings.Default.PrositNCE, SkylineWindow.GraphSpectrum.PrositNCE);
                });

                WaitForSpectrum(precursor, baseCE + i + 1);

                if (!RecordData)
                    AssertIntensityAndIRTSpectrumCorrect(pep, client.QueryIndex, false);
            }
        }

        public void AssertIntensityAndIRTSpectrumCorrect(PeptideDocNode pep, int index, bool checkIRT = true)
        {
            var offset = checkIRT ? 2 : 1;

            // We are interested in the queries just processed
            index -= offset;

            SpectrumDisplayInfo spectrumDisplayInfo = null;
            RunUI(() => spectrumDisplayInfo = SkylineWindow.GraphSpectrum.SelectedSpectrum);
            Assert.IsNotNull(spectrumDisplayInfo);

            // There need to be at least one or two queries (ms2, irt) and (irt), depending on if the cache
            // is used
            AssertEx.IsGreaterThanOrEqual(QUERIES.Count - index, offset);

            // Get queries and make sure they match the actual spectra
            var intensityQuery = QUERIES.ElementAt(index) as PrositIntensityQuery;
            Assert.IsNotNull(intensityQuery);
            intensityQuery.AssertMatchesSpectrum(pep, spectrumDisplayInfo);

            if (checkIRT)
            {
                var rtQuery = QUERIES.ElementAt(index + 1) as PrositRetentionTimeQuery;
                Assert.IsNotNull(rtQuery);
                rtQuery.AssertMatchesSpectrum(spectrumDisplayInfo);
            }
        }

        public void WaitForSpectrum(TransitionGroupDocNode precursor, int nce)
        {
            WaitForConditionUI(() =>
            {
                if (!SkylineWindow.GraphSpectrum.HasSpectrum)
                    return false;
                var info = SkylineWindow.GraphSpectrum.SelectedSpectrum.SpectrumInfo as SpectrumInfoProsit;
                if (info == null)
                    return false;
                return ReferenceEquals(info.Precursor, precursor) && info.NCE == nce ;
            });
        }

        public void TestLivePrositMirrorPlots()
        {
        }
    }

    public interface IRecordable
    {
        string ToCode();
    }

    public abstract class PrositQuery : IRecordable
    {
        public abstract string Model { get; }
        public abstract PredictResponse Response { get; }

        public abstract void AssertMatchesQuery(PredictRequest pr);
        public abstract string ToCode();
    }

    public class PrositIntensityInput : IRecordable
    {
        public PrositIntensityInput(string modifiedSequence, float normalizedCollisionEnergy, int precursorCharge)
        {
            ModifiedSequence = modifiedSequence;
            NormalizedCollisionEnergy = normalizedCollisionEnergy;
            PrecursorCharge = precursorCharge;
        }

        public string ModifiedSequence { get; private set; }
        public float NormalizedCollisionEnergy { get; private set; }
        public int PrecursorCharge { get; private set; }

        public string ToCode()
        {
            return string.Format("new PrositIntensityInput(\"{0}\", {1:0.0000}f, {2})", ModifiedSequence, NormalizedCollisionEnergy,
                PrecursorCharge);
        }
    }

    public class PrositIntensityQuery : PrositQuery
    {
        private PrositIntensityInput[] _inputs;
        private float[][] _spectra;

        public PrositIntensityQuery(PrositIntensityInput[] inputs, float[][] spectra)
        {
            _inputs = inputs;
            _spectra = spectra;
            Assert.AreEqual(_inputs.Length, _spectra.Length);
        }

        public static PrositIntensityQuery FromTensors(PredictRequest request, PredictResponse response)
        {
            // Sequences
            var seqs = request.Inputs[PrositIntensityModel.PrositIntensityInput.PEPTIDES_KEY];
            Assert.AreEqual(seqs.TensorShape.Dim.Count, 2);
            Assert.AreEqual(seqs.TensorShape.Dim[1].Size, Constants.PEPTIDE_SEQ_LEN);
            var decodedSeqs = PrositHelpers.DecodeSequences(seqs);

            // CEs
            var ces = request.Inputs[PrositIntensityModel.PrositIntensityInput.COLLISION_ENERGY_KEY];
            Assert.AreEqual(ces.TensorShape.Dim.Count, 2);
            Assert.AreEqual(ces.TensorShape.Dim[1].Size, 1);
            var decodedCes = ces.FloatVal.ToArray();

            // Charges
            var charges = request.Inputs[PrositIntensityModel.PrositIntensityInput.PRECURSOR_CHARGE_KEY];
            Assert.AreEqual(charges.TensorShape.Dim.Count, 2);
            Assert.AreEqual(charges.TensorShape.Dim[1].Size, Constants.PRECURSOR_CHARGES);
            var decodedCharges = PrositHelpers.DecodeCharges(charges);

            var inputs = Enumerable.Range(0, decodedSeqs.Length)
                .Select(i => new PrositIntensityInput(decodedSeqs[i], decodedCes[i], decodedCharges[i])).ToArray();

            var outputsFlattened = PrositHelpers.ReLU(response.Outputs[PrositIntensityModel.PrositIntensityOutput.OUTPUT_KEY].FloatVal.ToArray());

            // Reshape and copy
            var outputs = new float[inputs.Length][];
            var batch = (Constants.PEPTIDE_SEQ_LEN - 1) * Constants.IONS_PER_RESIDUE;
            for (int i = 0; i < inputs.Length; ++i)
            {
                outputs[i] = new float[batch];
                Array.Copy(outputsFlattened, i * batch, outputs[i], 0, batch);
            }

            return new PrositIntensityQuery(inputs, outputs);
        }

        public override string ToCode()
        {
            var inputsCodeArr = _inputs.Select(i => "        " + i.ToCode());
            var inputsCode = string.Format("new[] {{\r\n{0}\r\n    }}",
                string.Join(",\r\n", inputsCodeArr));

            var spectraCodeArr = _spectra.Select(f =>
            {
                var code = new StringBuilder();
                code.AppendLine("        new[] {");
                for (int i = 0; i < Constants.PEPTIDE_SEQ_LEN - 1; ++i)
                    code.AppendLine("            " + string.Join(", ",
                                        f.Skip(i * Constants.IONS_PER_RESIDUE).Take(Constants.IONS_PER_RESIDUE)
                                            .Select(fl => string.Format("{0:00.0000}f", fl))) + ",");
                code.Remove(code.Length - 3, 3);
                code.AppendLine();
                code.Append("        }");
                return code.ToString();
            });

            var spectraCode = new StringBuilder();
            spectraCode.AppendLine("new[] {");
            spectraCode.Append(string.Join(", ", spectraCodeArr));
            spectraCode.AppendLine();
            spectraCode.Append("    }");

            return string.Format("new PrositIntensityQuery(\r\n    {0},\r\n    {1}\r\n),", inputsCode, spectraCode);
        }

        public override PredictResponse Response
        {
            get
            {
                var pr = new PredictResponse();
                pr.ModelSpec = new ModelSpec { Name = Model };

                // Construct Tensor
                var tp = new TensorProto { Dtype = DataType.DtFloat };

                var spectraFlatten = _spectra.SelectMany(f => f).ToArray();
                // Populate with data
                tp.FloatVal.AddRange(spectraFlatten);
                tp.TensorShape = new TensorShapeProto();
                tp.TensorShape.Dim.Add(new TensorShapeProto.Types.Dim { Size = _spectra.Length });
                tp.TensorShape.Dim.Add(new TensorShapeProto.Types.Dim { Size = (Constants.PEPTIDE_SEQ_LEN - 1) * Constants.PRECURSOR_CHARGES });
                pr.Outputs[PrositIntensityModel.PrositIntensityOutput.OUTPUT_KEY] = tp;

                return pr;
            }
        }

        public override string Model => "intensity_2";

        public override void AssertMatchesQuery(PredictRequest pr)
        {
            Assert.AreEqual(Model, pr.ModelSpec.Name);

            Assert.AreEqual(pr.Inputs.Count, 3);
            var keys = pr.Inputs.Keys.OrderBy(s => s).ToArray();
            Assert.AreEqual(keys[0], PrositIntensityModel.PrositIntensityInput.COLLISION_ENERGY_KEY);
            Assert.AreEqual(keys[1], PrositIntensityModel.PrositIntensityInput.PEPTIDES_KEY);
            Assert.AreEqual(keys[2], PrositIntensityModel.PrositIntensityInput.PRECURSOR_CHARGE_KEY);

            // Sequences
            var seqs = pr.Inputs[PrositIntensityModel.PrositIntensityInput.PEPTIDES_KEY];
            Assert.AreEqual(seqs.TensorShape.Dim.Count, 2);
            Assert.AreEqual(seqs.TensorShape.Dim[0].Size, _inputs.Length);
            Assert.AreEqual(seqs.TensorShape.Dim[1].Size, Constants.PEPTIDE_SEQ_LEN);
            AssertEx.AreEqualDeep(_inputs.Select(i => i.ModifiedSequence).ToArray(),
                PrositHelpers.DecodeSequences(seqs));

            // CEs
            var ces = pr.Inputs[PrositIntensityModel.PrositIntensityInput.COLLISION_ENERGY_KEY];
            Assert.AreEqual(ces.TensorShape.Dim.Count, 2);
            Assert.AreEqual(ces.TensorShape.Dim[0].Size, _inputs.Length);
            Assert.AreEqual(ces.TensorShape.Dim[1].Size, 1);
            AssertEx.AreEqualDeep(_inputs.Select(i => i.NormalizedCollisionEnergy).ToArray(), ces.FloatVal);

            // Charges
            var charges = pr.Inputs[PrositIntensityModel.PrositIntensityInput.PRECURSOR_CHARGE_KEY];
            Assert.AreEqual(charges.TensorShape.Dim.Count, 2);
            Assert.AreEqual(charges.TensorShape.Dim[0].Size, _inputs.Length);
            Assert.AreEqual(charges.TensorShape.Dim[1].Size, Constants.PRECURSOR_CHARGES);
            AssertEx.AreEqualDeep(_inputs.Select(i => i.PrecursorCharge).ToArray(), PrositHelpers.DecodeCharges(charges));
        }

        public void AssertMatchesSpectra(PeptideDocNode[] peptides, SpectrumDisplayInfo[] spectrumDisplayInfos)
        {
            for (int i = 0; i < _inputs.Length; ++i)
                AssertMatchesSpectrum(peptides[i], _inputs[i], _spectra[i], spectrumDisplayInfos[i]);
        }

        public void AssertMatchesSpectrum(PeptideDocNode pep, SpectrumDisplayInfo spectrumDisplayInfo)
        {
            AssertMatchesSpectrum(pep, _inputs[0], _spectra[0], spectrumDisplayInfo);
        }

        public static void AssertMatchesSpectrum(PeptideDocNode pep, PrositIntensityInput input, float[] spectrum, SpectrumDisplayInfo spectrumDisplayInfo)
        {
            Assert.IsNotNull(spectrumDisplayInfo);
            Assert.IsInstanceOfType(spectrumDisplayInfo.SpectrumInfo, typeof(SpectrumInfoProsit));
            Assert.AreEqual(spectrumDisplayInfo.Name, "Prosit");

            // Calculate expected number of peaks. 1 peak per residue times the number of possible charges
            var residues = FastaSequence.StripModifications(input.ModifiedSequence).Length - 1;
            var charges = Math.Min(input.PrecursorCharge, 3);
            var ionCount = 2 * residues * charges;

            Assert.AreEqual(spectrumDisplayInfo.SpectrumPeaksInfo.Peaks.Length, ionCount);
            
            // Construct a prosit output object so that we can construct a spectrum for comparison.
            // There really is no easier way to do this without rewriting a lot of code for parsing the
            // flattened intensities and adding lots of extra test code inside of Skyline code.
            var fakePrositOutputTensors = new MapField<string, TensorProto>();
            var tensor = new TensorProto();
            tensor.TensorShape = new TensorShapeProto();
            tensor.TensorShape.Dim.Add(new TensorShapeProto.Types.Dim() { Size = 1 });
            tensor.TensorShape.Dim.Add(new TensorShapeProto.Types.Dim() { Size = spectrum.Length });
            tensor.FloatVal.AddRange(spectrum);
            fakePrositOutputTensors[PrositIntensityModel.PrositIntensityOutput.OUTPUT_KEY] = tensor;

            var fakePrositOutput = new PrositIntensityModel.PrositIntensityOutput(fakePrositOutputTensors);

            var ms2Spectrum = new PrositMS2Spectrum(Program.MainWindow.Document.Settings,
                new PeptidePrecursorPair(pep, pep.TransitionGroups.First(), (int) (input.NormalizedCollisionEnergy * 100.0f)), 0,
                fakePrositOutput);

            // Compare the spectra
            AssertEx.AreEqualDeep(ms2Spectrum.SpectrumPeaks.Peaks, spectrumDisplayInfo.SpectrumPeaksInfo.Peaks);
        }
    }

    public class PrositRetentionTimeQuery : PrositQuery
    {
        private string[] _modifiedSequences;
        private float[] _iRTs;

        public PrositRetentionTimeQuery(string[] modifiedSequences, float[] iRTs)
        {
            _modifiedSequences = modifiedSequences;
            _iRTs = iRTs;
        }

        public static PrositRetentionTimeQuery FromTensors(PredictRequest request, PredictResponse response)
        {
            // Sequences
            var seqs = request.Inputs[PrositRetentionTimeModel.PrositRTInput.PEPTIDES_KEY];
            Assert.AreEqual(seqs.TensorShape.Dim.Count, 2);
            Assert.AreEqual(seqs.TensorShape.Dim[1].Size, Constants.PEPTIDE_SEQ_LEN);
            var decodedSeqs = PrositHelpers.DecodeSequences(seqs);

            var outputs = response.Outputs[PrositRetentionTimeModel.PrositRTOutput.OUTPUT_KEY].FloatVal.ToArray();

            return new PrositRetentionTimeQuery(decodedSeqs, outputs);
        }

        public override string ToCode()
        {
            var seqsCode = string.Format("new[] {{\r\n{0}\r\n    }}",
                string.Join(",\r\n", _modifiedSequences.Select(m => string.Format("        \"{0}\"", m))));

            var iRTCode = string.Format("new[] {{\r\n{0}\r\n    }}",
                string.Join(",\r\n", _iRTs.Select(irt => string.Format("        {0:0.0000}f", irt))));

            return string.Format("new PrositRetentionTimeQuery(\r\n    {0},\r\n    {1}\r\n),", seqsCode, iRTCode);
        }

        public override void AssertMatchesQuery(PredictRequest pr)
        {
            Assert.AreEqual(Model, pr.ModelSpec.Name);
            Assert.AreEqual(pr.Inputs.Count, 1);
            Assert.AreEqual(pr.Inputs.Keys.First(), PrositRetentionTimeModel.PrositRTInput.PEPTIDES_KEY);
            var tensor = pr.Inputs[PrositRetentionTimeModel.PrositRTInput.PEPTIDES_KEY];
            Assert.AreEqual(tensor.TensorShape.Dim.Count, 2);
            Assert.AreEqual(tensor.TensorShape.Dim[0].Size, _modifiedSequences.Length);
            Assert.AreEqual(tensor.TensorShape.Dim[1].Size, Constants.PEPTIDE_SEQ_LEN);
            AssertEx.AreEqualDeep(_modifiedSequences,
                PrositHelpers.DecodeSequences(pr.Inputs[PrositRetentionTimeModel.PrositRTInput.PEPTIDES_KEY]));
        }

        public void AssertMatchesSpectra(SpectrumDisplayInfo[] spectrumDisplayInfos)
        {
            for (int i = 0; i < _iRTs.Length; ++i)
                AssertMatchesSpectrum(_iRTs[i], spectrumDisplayInfos[i]);
        }

        public void AssertMatchesSpectrum(SpectrumDisplayInfo spectrumDisplayInfo)
        {
            AssertMatchesSpectrum(_iRTs[0], spectrumDisplayInfo);
        }

        public static void AssertMatchesSpectrum(float iRT, SpectrumDisplayInfo spectrumDisplayInfo)
        {
            Assert.AreEqual(
                iRT * Math.Sqrt(PrositRetentionTimeModel.PrositRTOutput.iRT_VARIANCE) +
                PrositRetentionTimeModel.PrositRTOutput.iRT_MEAN, spectrumDisplayInfo.RetentionTime);
        }

        public override string Model => "iRT";

        public override PredictResponse Response
        {
            get
            {
                var pr = new PredictResponse();
                pr.ModelSpec = new ModelSpec { Name = Model };

                // Construct Tensor
                var tp = new TensorProto { Dtype = DataType.DtFloat };

                // Populate with data
                tp.FloatVal.AddRange(_iRTs);
                tp.TensorShape = new TensorShapeProto();
                tp.TensorShape.Dim.Add(new TensorShapeProto.Types.Dim { Size = _iRTs.Length });
                pr.Outputs[PrositRetentionTimeModel.PrositRTOutput.OUTPUT_KEY] = tp;

                return pr;
            }
        }
    }

    /// <summary>
    /// A fake prediction client for logging predictions and returning cached
    /// predictions. For logging, it needs to be constructed with a server address.
    /// For returning cached predictions, a queue of expected queries should be passed in.
    /// </summary>
    public class FakePrositPredictionClient : PredictionService.PredictionServiceClient
    {
        private List<PrositQuery> _expectedQueries;

        public FakePrositPredictionClient(string server) :
            base(new Channel(server, ChannelCredentials.Insecure))
        {
            QueryIndex = 0;
        }


        public FakePrositPredictionClient(List<PrositQuery> expectedQueries)
        {
            _expectedQueries = expectedQueries;
            QueryIndex = 0;
        }

        public int QueryIndex { get; private set; }

        public override PredictResponse Predict(PredictRequest request, CallOptions options)
        {
            // Logging mode
            if (_expectedQueries == null)
            {
                var response = base.Predict(request, options);
                LogQuery(request, response);
                return response;
            }

            // Caching mode
            if (QueryIndex == _expectedQueries.Count)
                Assert.Fail("Unexpected call to Predict (No more queries). Model: {0}", request.ModelSpec.Name);

            var nextQuery = _expectedQueries[QueryIndex++];
            nextQuery.AssertMatchesQuery(request);
            return nextQuery.Response;
        }

        private void LogQuery(PredictRequest request, PredictResponse response)
        {
            if (request.ModelSpec.Name.StartsWith("intensity"))
                Console.WriteLine("    " + PrositIntensityQuery.FromTensors(request, response).ToCode().Replace("\n", "\n    "));
            else if (request.ModelSpec.Name.StartsWith("iRT"))
                Console.WriteLine("    " +
                    PrositRetentionTimeQuery.FromTensors(request, response).ToCode().Replace("\n", "\n    "));
            else
                Assert.Fail("Unknown model \"{0}\"", request.ModelSpec.Name);
        }
    }
}