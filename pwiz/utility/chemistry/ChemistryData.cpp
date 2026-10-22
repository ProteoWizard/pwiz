//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#define PWIZ_SOURCE

#include "ChemistryData.hpp"


namespace pwiz {
namespace chemistry {
namespace detail {


using namespace pwiz::chemistry::Element;


Isotope isotopes_H[] = { {1.0078250321, 0.999885}, {2.014101778, 0.000115}, {3.0160492675, 0}, };
const int isotopes_H_size = sizeof(isotopes_H)/sizeof(Isotope);

Isotope isotopes_2H[] = { {2.014101778, 1} };
const int isotopes_2H_size = sizeof(isotopes_2H)/sizeof(Isotope);

Isotope isotopes_3H[] = { {3.0160492675, 1} };
const int isotopes_3H_size = sizeof(isotopes_3H)/sizeof(Isotope);

Isotope isotopes_He[] = { {3.0160293097, 1.37e-06}, {4.0026032497, 0.99999863}, };
const int isotopes_He_size = sizeof(isotopes_He)/sizeof(Isotope);

Isotope isotopes_Li[] = { {6.0151223, 0.0759}, {7.016004, 0.9241}, };
const int isotopes_Li_size = sizeof(isotopes_Li)/sizeof(Isotope);

Isotope isotopes_Be[] = { {9.0121821, 1}, };
const int isotopes_Be_size = sizeof(isotopes_Be)/sizeof(Isotope);

Isotope isotopes_B[] = { {10.012937, 0.199}, {11.0093055, 0.801}, };
const int isotopes_B_size = sizeof(isotopes_B)/sizeof(Isotope);

Isotope isotopes_C[] = { {12, 0.9893}, {13.0033548378, 0.0107}, {14.003241988, 0}, };
const int isotopes_C_size = sizeof(isotopes_C)/sizeof(Isotope);

Isotope isotopes_13C[] = { {13.0033548378, 1} };
const int isotopes_13C_size = sizeof(isotopes_13C)/sizeof(Isotope);

Isotope isotopes_N[] = { {14.0030740052, 0.99632}, {15.0001088984, 0.00368}, };
const int isotopes_N_size = sizeof(isotopes_N)/sizeof(Isotope);

Isotope isotopes_15N[] = { {15.0001088984, 1} };
const int isotopes_15N_size = sizeof(isotopes_15N)/sizeof(Isotope);

Isotope isotopes_O[] = { {15.9949146221, 0.99757}, {16.9991315, 0.00038}, {17.9991604, 0.00205}, };
const int isotopes_O_size = sizeof(isotopes_O)/sizeof(Isotope);

Isotope isotopes_18O[] = { {17.9991604, 1} };
const int isotopes_18O_size = sizeof(isotopes_18O)/sizeof(Isotope);

Isotope isotopes_F[] = { {18.9984032, 1}, };
const int isotopes_F_size = sizeof(isotopes_F)/sizeof(Isotope);

Isotope isotopes_Ne[] = { {19.9924401759, 0.9048}, {20.99384674, 0.0027}, {21.99138551, 0.0925}, };
const int isotopes_Ne_size = sizeof(isotopes_Ne)/sizeof(Isotope);

Isotope isotopes_Na[] = { {22.98976967, 1}, };
const int isotopes_Na_size = sizeof(isotopes_Na)/sizeof(Isotope);

Isotope isotopes_Mg[] = { {23.9850419, 0.7899}, {24.98583702, 0.1}, {25.98259304, 0.1101}, };
const int isotopes_Mg_size = sizeof(isotopes_Mg)/sizeof(Isotope);

Isotope isotopes_Al[] = { {26.98153844, 1}, };
const int isotopes_Al_size = sizeof(isotopes_Al)/sizeof(Isotope);

Isotope isotopes_Si[] = { {27.9769265327, 0.922297}, {28.97649472, 0.046832}, {29.97377022, 0.030872}, };
const int isotopes_Si_size = sizeof(isotopes_Si)/sizeof(Isotope);

Isotope isotopes_P[] = { {30.97376151, 1}, };
const int isotopes_P_size = sizeof(isotopes_P)/sizeof(Isotope);

Isotope isotopes_S[] = { {31.97207069, 0.9493}, {32.9714585, 0.0076}, {33.96786683, 0.0429}, {35.96708088, 0.0002}, };
const int isotopes_S_size = sizeof(isotopes_S)/sizeof(Isotope);

Isotope isotopes_Cl[] = { {34.96885271, 0.7578}, {36.9659026, 0.2422}, };
const int isotopes_Cl_size = sizeof(isotopes_Cl)/sizeof(Isotope);

Isotope isotopes_Ar[] = { {35.96754628, 0.003365}, {37.9627322, 0.000632}, {39.962383123, 0.996003}, };
const int isotopes_Ar_size = sizeof(isotopes_Ar)/sizeof(Isotope);

Isotope isotopes_K[] = { {38.9637069, 0.932581}, {39.96399867, 0.000117}, {40.96182597, 0.067302}, };
const int isotopes_K_size = sizeof(isotopes_K)/sizeof(Isotope);

Isotope isotopes_Ca[] = { {39.9625912, 0.96941}, {41.9586183, 0.00647}, {42.9587668, 0.00135}, {43.9554811, 0.02086}, {45.9536928, 4e-05}, {47.952534, 0.00187}, };
const int isotopes_Ca_size = sizeof(isotopes_Ca)/sizeof(Isotope);

Isotope isotopes_Sc[] = { {44.9559102, 1}, };
const int isotopes_Sc_size = sizeof(isotopes_Sc)/sizeof(Isotope);

Isotope isotopes_Ti[] = { {45.9526295, 0.0825}, {46.9517638, 0.0744}, {47.9479471, 0.7372}, {48.9478708, 0.0541}, {49.9447921, 0.0518}, };
const int isotopes_Ti_size = sizeof(isotopes_Ti)/sizeof(Isotope);

Isotope isotopes_V[] = { {49.9471628, 0.0025}, {50.9439637, 0.9975}, };
const int isotopes_V_size = sizeof(isotopes_V)/sizeof(Isotope);

Isotope isotopes_Cr[] = { {49.9460496, 0.04345}, {51.9405119, 0.83789}, {52.9406538, 0.09501}, {53.9388849, 0.02365}, };
const int isotopes_Cr_size = sizeof(isotopes_Cr)/sizeof(Isotope);

Isotope isotopes_Mn[] = { {54.9380496, 1}, };
const int isotopes_Mn_size = sizeof(isotopes_Mn)/sizeof(Isotope);

Isotope isotopes_Fe[] = { {53.9396148, 0.05845}, {55.9349421, 0.91754}, {56.9353987, 0.02119}, {57.9332805, 0.00282}, };
const int isotopes_Fe_size = sizeof(isotopes_Fe)/sizeof(Isotope);

Isotope isotopes_Co[] = { {58.9332002, 1}, };
const int isotopes_Co_size = sizeof(isotopes_Co)/sizeof(Isotope);

Isotope isotopes_Ni[] = { {57.9353479, 0.680769}, {59.9307906, 0.262231}, {60.9310604, 0.011399}, {61.9283488, 0.036345}, {63.9279696, 0.009256}, };
const int isotopes_Ni_size = sizeof(isotopes_Ni)/sizeof(Isotope);

Isotope isotopes_Cu[] = { {62.9296011, 0.6917}, {64.9277937, 0.3083}, };
const int isotopes_Cu_size = sizeof(isotopes_Cu)/sizeof(Isotope);

Isotope isotopes_Zn[] = { {63.9291466, 0.4863}, {65.9260368, 0.279}, {66.9271309, 0.041}, {67.9248476, 0.1875}, {69.925325, 0.0062}, };
const int isotopes_Zn_size = sizeof(isotopes_Zn)/sizeof(Isotope);

Isotope isotopes_Ga[] = { {68.925581, 0.60108}, {70.924705, 0.39892}, };
const int isotopes_Ga_size = sizeof(isotopes_Ga)/sizeof(Isotope);

Isotope isotopes_Ge[] = { {69.9242504, 0.2084}, {71.9220762, 0.2754}, {72.9234594, 0.0773}, {73.9211782, 0.3628}, {75.9214027, 0.0761}, };
const int isotopes_Ge_size = sizeof(isotopes_Ge)/sizeof(Isotope);

Isotope isotopes_As[] = { {74.9215964, 1}, };
const int isotopes_As_size = sizeof(isotopes_As)/sizeof(Isotope);

Isotope isotopes_Se[] = { {73.9224766, 0.0089}, {75.9192141, 0.0937}, {76.9199146, 0.0763}, {77.9173095, 0.2377}, {79.9165218, 0.4961}, {81.9167, 0.0873}, };
const int isotopes_Se_size = sizeof(isotopes_Se)/sizeof(Isotope);

Isotope isotopes_Br[] = { {78.9183376, 0.5069}, {80.916291, 0.4931}, };
const int isotopes_Br_size = sizeof(isotopes_Br)/sizeof(Isotope);

Isotope isotopes_Kr[] = { {77.920386, 0.0035}, {79.916378, 0.0228}, {81.9134846, 0.1158}, {82.914136, 0.1149}, {83.911507, 0.57}, {85.9106103, 0.173}, };
const int isotopes_Kr_size = sizeof(isotopes_Kr)/sizeof(Isotope);

Isotope isotopes_Rb[] = { {84.9117893, 0.7217}, {86.9091835, 0.2783}, };
const int isotopes_Rb_size = sizeof(isotopes_Rb)/sizeof(Isotope);

Isotope isotopes_Sr[] = { {83.913425, 0.0056}, {85.9092624, 0.0986}, {86.9088793, 0.07}, {87.9056143, 0.8258}, };
const int isotopes_Sr_size = sizeof(isotopes_Sr)/sizeof(Isotope);

Isotope isotopes_Y[] = { {88.9058479, 1}, };
const int isotopes_Y_size = sizeof(isotopes_Y)/sizeof(Isotope);

Isotope isotopes_Zr[] = { {89.9047037, 0.5145}, {90.905645, 0.1122}, {91.9050401, 0.1715}, {93.9063158, 0.1738}, {95.908276, 0.028}, };
const int isotopes_Zr_size = sizeof(isotopes_Zr)/sizeof(Isotope);

Isotope isotopes_Nb[] = { {92.9063775, 1}, };
const int isotopes_Nb_size = sizeof(isotopes_Nb)/sizeof(Isotope);

Isotope isotopes_Mo[] = { {91.90681, 0.1484}, {93.9050876, 0.0925}, {94.9058415, 0.1592}, {95.9046789, 0.1668}, {96.906021, 0.0955}, {97.9054078, 0.2413}, {99.907477, 0.0963}, };
const int isotopes_Mo_size = sizeof(isotopes_Mo)/sizeof(Isotope);

Isotope isotopes_Tc[] = { {96.906365, 0}, {97.907216, 0}, {98.9062546, 0}, };
const int isotopes_Tc_size = sizeof(isotopes_Tc)/sizeof(Isotope);

Isotope isotopes_Ru[] = { {95.907598, 0.0554}, {97.905287, 0.0187}, {98.9059393, 0.1276}, {99.9042197, 0.126}, {100.9055822, 0.1706}, {101.9043495, 0.3155}, {103.90543, 0.1862}, };
const int isotopes_Ru_size = sizeof(isotopes_Ru)/sizeof(Isotope);

Isotope isotopes_Rh[] = { {102.905504, 1}, };
const int isotopes_Rh_size = sizeof(isotopes_Rh)/sizeof(Isotope);

Isotope isotopes_Pd[] = { {101.905608, 0.0102}, {103.904035, 0.1114}, {104.905084, 0.2233}, {105.903483, 0.2733}, {107.903894, 0.2646}, {109.905152, 0.1172}, };
const int isotopes_Pd_size = sizeof(isotopes_Pd)/sizeof(Isotope);

Isotope isotopes_Ag[] = { {106.905093, 0.51839}, {108.904756, 0.48161}, };
const int isotopes_Ag_size = sizeof(isotopes_Ag)/sizeof(Isotope);

Isotope isotopes_Cd[] = { {105.906458, 0.0125}, {107.904183, 0.0089}, {109.903006, 0.1249}, {110.904182, 0.128}, {111.9027572, 0.2413}, {112.9044009, 0.1222}, {113.9033581, 0.2873}, {115.904755, 0.0749}, };
const int isotopes_Cd_size = sizeof(isotopes_Cd)/sizeof(Isotope);

Isotope isotopes_In[] = { {112.904061, 0.0429}, {114.903878, 0.9571}, };
const int isotopes_In_size = sizeof(isotopes_In)/sizeof(Isotope);

Isotope isotopes_Sn[] = { {111.904821, 0.0097}, {113.902782, 0.0066}, {114.903346, 0.0034}, {115.901744, 0.1454}, {116.902954, 0.0768}, {117.901606, 0.2422}, {118.903309, 0.0859}, {119.9021966, 0.3258}, {121.9034401, 0.0463}, {123.9052746, 0.0579}, };
const int isotopes_Sn_size = sizeof(isotopes_Sn)/sizeof(Isotope);

Isotope isotopes_Sb[] = { {120.903818, 0.5721}, {122.9042157, 0.4279}, };
const int isotopes_Sb_size = sizeof(isotopes_Sb)/sizeof(Isotope);

Isotope isotopes_Te[] = { {119.90402, 0.0009}, {121.9030471, 0.0255}, {122.904273, 0.0089}, {123.9028195, 0.0474}, {124.9044247, 0.0707}, {125.9033055, 0.1884}, {127.9044614, 0.3174}, {129.9062228, 0.3408}, };
const int isotopes_Te_size = sizeof(isotopes_Te)/sizeof(Isotope);

Isotope isotopes_I[] = { {126.904468, 1}, };
const int isotopes_I_size = sizeof(isotopes_I)/sizeof(Isotope);

Isotope isotopes_Xe[] = { {123.9058958, 0.0009}, {125.904269, 0.0009}, {127.9035304, 0.0192}, {128.9047795, 0.2644}, {129.9035079, 0.0408}, {130.9050819, 0.2118}, {131.9041545, 0.2689}, {133.9053945, 0.1044}, {135.90722, 0.0887}, };
const int isotopes_Xe_size = sizeof(isotopes_Xe)/sizeof(Isotope);

Isotope isotopes_Cs[] = { {132.905447, 1}, };
const int isotopes_Cs_size = sizeof(isotopes_Cs)/sizeof(Isotope);

Isotope isotopes_Ba[] = { {129.90631, 0.00106}, {131.905056, 0.00101}, {133.904503, 0.02417}, {134.905683, 0.06592}, {135.90457, 0.07854}, {136.905821, 0.11232}, {137.905241, 0.71698}, };
const int isotopes_Ba_size = sizeof(isotopes_Ba)/sizeof(Isotope);

Isotope isotopes_La[] = { {137.907107, 0.0009}, {138.906348, 0.9991}, };
const int isotopes_La_size = sizeof(isotopes_La)/sizeof(Isotope);

Isotope isotopes_Ce[] = { {135.90714, 0.00185}, {137.905986, 0.00251}, {139.905434, 0.8845}, {141.90924, 0.11114}, };
const int isotopes_Ce_size = sizeof(isotopes_Ce)/sizeof(Isotope);

Isotope isotopes_Pr[] = { {140.907648, 1}, };
const int isotopes_Pr_size = sizeof(isotopes_Pr)/sizeof(Isotope);

Isotope isotopes_Nd[] = { {141.907719, 0.272}, {142.90981, 0.122}, {143.910083, 0.238}, {144.912569, 0.083}, {145.913112, 0.172}, {147.916889, 0.057}, {149.920887, 0.056}, };
const int isotopes_Nd_size = sizeof(isotopes_Nd)/sizeof(Isotope);

Isotope isotopes_Pm[] = { {144.912744, 0}, {146.915134, 0}, };
const int isotopes_Pm_size = sizeof(isotopes_Pm)/sizeof(Isotope);

Isotope isotopes_Sm[] = { {143.911995, 0.0307}, {146.914893, 0.1499}, {147.914818, 0.1124}, {148.91718, 0.1382}, {149.917271, 0.0738}, {151.919728, 0.2675}, {153.922205, 0.2275}, };
const int isotopes_Sm_size = sizeof(isotopes_Sm)/sizeof(Isotope);

Isotope isotopes_Eu[] = { {150.919846, 0.4781}, {152.921226, 0.5219}, };
const int isotopes_Eu_size = sizeof(isotopes_Eu)/sizeof(Isotope);

Isotope isotopes_Gd[] = { {151.919788, 0.002}, {153.920862, 0.0218}, {154.922619, 0.148}, {155.92212, 0.2047}, {156.923957, 0.1565}, {157.924101, 0.2484}, {159.927051, 0.2186}, };
const int isotopes_Gd_size = sizeof(isotopes_Gd)/sizeof(Isotope);

Isotope isotopes_Tb[] = { {158.925343, 1}, };
const int isotopes_Tb_size = sizeof(isotopes_Tb)/sizeof(Isotope);

Isotope isotopes_Dy[] = { {155.924278, 0.0006}, {157.924405, 0.001}, {159.925194, 0.0234}, {160.92693, 0.1891}, {161.926795, 0.2551}, {162.928728, 0.249}, {163.929171, 0.2818}, };
const int isotopes_Dy_size = sizeof(isotopes_Dy)/sizeof(Isotope);

Isotope isotopes_Ho[] = { {164.930319, 1}, };
const int isotopes_Ho_size = sizeof(isotopes_Ho)/sizeof(Isotope);

Isotope isotopes_Er[] = { {161.928775, 0.0014}, {163.929197, 0.0161}, {165.93029, 0.3361}, {166.932045, 0.2293}, {167.932368, 0.2678}, {169.93546, 0.1493}, };
const int isotopes_Er_size = sizeof(isotopes_Er)/sizeof(Isotope);

Isotope isotopes_Tm[] = { {168.934211, 1}, };
const int isotopes_Tm_size = sizeof(isotopes_Tm)/sizeof(Isotope);

Isotope isotopes_Yb[] = { {167.933894, 0.0013}, {169.934759, 0.0304}, {170.936322, 0.1428}, {171.9363777, 0.2183}, {172.9382068, 0.1613}, {173.9388581, 0.3183}, {175.942568, 0.1276}, };
const int isotopes_Yb_size = sizeof(isotopes_Yb)/sizeof(Isotope);

Isotope isotopes_Lu[] = { {174.9407679, 0.9741}, {175.9426824, 0.0259}, };
const int isotopes_Lu_size = sizeof(isotopes_Lu)/sizeof(Isotope);

Isotope isotopes_Hf[] = { {173.94004, 0.0016}, {175.9414018, 0.0526}, {176.94322, 0.186}, {177.9436977, 0.2728}, {178.9458151, 0.1362}, {179.9465488, 0.3508}, };
const int isotopes_Hf_size = sizeof(isotopes_Hf)/sizeof(Isotope);

Isotope isotopes_Ta[] = { {179.947466, 0.00012}, {180.947996, 0.99988}, };
const int isotopes_Ta_size = sizeof(isotopes_Ta)/sizeof(Isotope);

Isotope isotopes_W[] = { {179.946706, 0.0012}, {181.948206, 0.265}, {182.9502245, 0.1431}, {183.9509326, 0.3064}, {185.954362, 0.2843}, };
const int isotopes_W_size = sizeof(isotopes_W)/sizeof(Isotope);

Isotope isotopes_Re[] = { {184.9529557, 0.374}, {186.9557508, 0.626}, };
const int isotopes_Re_size = sizeof(isotopes_Re)/sizeof(Isotope);

Isotope isotopes_Os[] = { {183.952491, 0.0002}, {185.953838, 0.0159}, {186.9557479, 0.0196}, {187.955836, 0.1324}, {188.9581449, 0.1615}, {189.958445, 0.2626}, {191.961479, 0.4078}, };
const int isotopes_Os_size = sizeof(isotopes_Os)/sizeof(Isotope);

Isotope isotopes_Ir[] = { {190.960591, 0.373}, {192.962924, 0.627}, };
const int isotopes_Ir_size = sizeof(isotopes_Ir)/sizeof(Isotope);

Isotope isotopes_Pt[] = { {189.95993, 0.00014}, {191.961035, 0.00782}, {193.962664, 0.32967}, {194.964774, 0.33832}, {195.964935, 0.25242}, {197.967876, 0.07163}, };
const int isotopes_Pt_size = sizeof(isotopes_Pt)/sizeof(Isotope);

Isotope isotopes_Au[] = { {196.966552, 1}, };
const int isotopes_Au_size = sizeof(isotopes_Au)/sizeof(Isotope);

Isotope isotopes_Hg[] = { {195.965815, 0.0015}, {197.966752, 0.0997}, {198.968262, 0.1687}, {199.968309, 0.231}, {200.970285, 0.1318}, {201.970626, 0.2986}, {203.973476, 0.0687}, };
const int isotopes_Hg_size = sizeof(isotopes_Hg)/sizeof(Isotope);

Isotope isotopes_Tl[] = { {202.972329, 0.29524}, {204.974412, 0.70476}, };
const int isotopes_Tl_size = sizeof(isotopes_Tl)/sizeof(Isotope);

Isotope isotopes_Pb[] = { {203.973029, 0.014}, {205.974449, 0.241}, {206.975881, 0.221}, {207.976636, 0.524}, };
const int isotopes_Pb_size = sizeof(isotopes_Pb)/sizeof(Isotope);

Isotope isotopes_Bi[] = { {208.980383, 1}, };
const int isotopes_Bi_size = sizeof(isotopes_Bi)/sizeof(Isotope);

Isotope isotopes_Po[] = { {208.982416, 0}, {209.982857, 0}, };
const int isotopes_Po_size = sizeof(isotopes_Po)/sizeof(Isotope);

Isotope isotopes_At[] = { {209.987131, 0}, {210.987481, 0}, };
const int isotopes_At_size = sizeof(isotopes_At)/sizeof(Isotope);

Isotope isotopes_Rn[] = { {210.990585, 0}, {220.0113841, 0}, {222.0175705, 0}, };
const int isotopes_Rn_size = sizeof(isotopes_Rn)/sizeof(Isotope);

Isotope isotopes_Fr[] = { {223.0197307, 0}, };
const int isotopes_Fr_size = sizeof(isotopes_Fr)/sizeof(Isotope);

Isotope isotopes_Ra[] = { {223.018497, 0}, {224.020202, 0}, {226.0254026, 0}, {228.0310641, 0}, };
const int isotopes_Ra_size = sizeof(isotopes_Ra)/sizeof(Isotope);

Isotope isotopes_Ac[] = { {227.027747, 0}, };
const int isotopes_Ac_size = sizeof(isotopes_Ac)/sizeof(Isotope);

Isotope isotopes_Th[] = { {230.0331266, 0}, {232.0380504, 1}, };
const int isotopes_Th_size = sizeof(isotopes_Th)/sizeof(Isotope);

Isotope isotopes_Pa[] = { {231.0358789, 1}, };
const int isotopes_Pa_size = sizeof(isotopes_Pa)/sizeof(Isotope);

Isotope isotopes_U[] = { {233.039628, 0}, {234.0409456, 5.5e-05}, {235.0439231, 0.0072}, {236.0455619, 0}, {238.0507826, 0.992745}, };
const int isotopes_U_size = sizeof(isotopes_U)/sizeof(Isotope);

Isotope isotopes_Np[] = { {237.0481673, 0}, {239.0529314, 0}, };
const int isotopes_Np_size = sizeof(isotopes_Np)/sizeof(Isotope);

Isotope isotopes_Pu[] = { {238.0495534, 0}, {239.0521565, 0}, {240.0538075, 0}, {241.0568453, 0}, {242.0587368, 0}, {244.064198, 0}, };
const int isotopes_Pu_size = sizeof(isotopes_Pu)/sizeof(Isotope);

Isotope isotopes_Am[] = { {241.0568229, 0}, {243.0613727, 0}, };
const int isotopes_Am_size = sizeof(isotopes_Am)/sizeof(Isotope);

Isotope isotopes_Cm[] = { {243.0613822, 0}, {244.0627463, 0}, {245.0654856, 0}, {246.0672176, 0}, {247.070347, 0}, {248.072342, 0}, };
const int isotopes_Cm_size = sizeof(isotopes_Cm)/sizeof(Isotope);

Isotope isotopes_Bk[] = { {247.070299, 0}, {249.07498, 0}, };
const int isotopes_Bk_size = sizeof(isotopes_Bk)/sizeof(Isotope);

Isotope isotopes_Cf[] = { {249.074847, 0}, {250.0764, 0}, {251.07958, 0}, {252.08162, 0}, };
const int isotopes_Cf_size = sizeof(isotopes_Cf)/sizeof(Isotope);

Isotope isotopes_Es[] = { {252.08297, 0}, };
const int isotopes_Es_size = sizeof(isotopes_Es)/sizeof(Isotope);

Isotope isotopes_Fm[] = { {257.095099, 0}, };
const int isotopes_Fm_size = sizeof(isotopes_Fm)/sizeof(Isotope);

Isotope isotopes_Md[] = { {256.09405, 0}, {258.098425, 0}, };
const int isotopes_Md_size = sizeof(isotopes_Md)/sizeof(Isotope);

Isotope isotopes_No[] = { {259.10102, 0}, };
const int isotopes_No_size = sizeof(isotopes_No)/sizeof(Isotope);

Isotope isotopes_Lr[] = { {262.10969, 0}, };
const int isotopes_Lr_size = sizeof(isotopes_Lr)/sizeof(Isotope);

Isotope isotopes_Rf[] = { {261.10875, 0}, };
const int isotopes_Rf_size = sizeof(isotopes_Rf)/sizeof(Isotope);

Isotope isotopes_Db[] = { {262.11415, 0}, };
const int isotopes_Db_size = sizeof(isotopes_Db)/sizeof(Isotope);

Isotope isotopes_Sg[] = { {266.12193, 0}, };
const int isotopes_Sg_size = sizeof(isotopes_Sg)/sizeof(Isotope);

Isotope isotopes_Bh[] = { {264.12473, 0}, };
const int isotopes_Bh_size = sizeof(isotopes_Bh)/sizeof(Isotope);

Isotope isotopes_Hs[] = { {0, 0}, };
const int isotopes_Hs_size = sizeof(isotopes_Hs)/sizeof(Isotope);

Isotope isotopes_Mt[] = { {268.13882, 0}, };
const int isotopes_Mt_size = sizeof(isotopes_Mt)/sizeof(Isotope);

Isotope isotopes_Uun[] = { {0, 0}, };
const int isotopes_Uun_size = sizeof(isotopes_Uun)/sizeof(Isotope);

Isotope isotopes_Uuu[] = { {272.15348, 0}, };
const int isotopes_Uuu_size = sizeof(isotopes_Uuu)/sizeof(Isotope);

Isotope isotopes_Uub[] = { {0, 0}, };
const int isotopes_Uub_size = sizeof(isotopes_Uub)/sizeof(Isotope);

Isotope isotopes_Uuq[] = { {0, 0}, };
const int isotopes_Uuq_size = sizeof(isotopes_Uuq)/sizeof(Isotope);

Isotope isotopes_Uuh[] = { {0, 0}, };
const int isotopes_Uuh_size = sizeof(isotopes_Uuh)/sizeof(Isotope);


PWIZ_API_DECL Element elements_[] =
{
    { H, "H", 1, 1.00794, isotopes_H, isotopes_H_size },
    { _2H, "_2H", 1, isotopes_2H[0].mass, isotopes_2H, isotopes_2H_size, "D" }, // D is IUPAC shorthand for 2H
    { _3H, "_3H", 1, isotopes_3H[0].mass, isotopes_3H, isotopes_3H_size, "T" }, // T is IUPAC shorthand for 3H
    { He, "He", 2, 4.002602, isotopes_He, isotopes_He_size },
    { Li, "Li", 3, 6.941, isotopes_Li, isotopes_Li_size },
    { Be, "Be", 4, 9.012182, isotopes_Be, isotopes_Be_size },
    { B, "B", 5, 10.811, isotopes_B, isotopes_B_size },
    { C, "C", 6, 12.0107, isotopes_C, isotopes_C_size },
    { _13C, "_13C", 6, isotopes_13C[0].mass, isotopes_13C, isotopes_13C_size },
    { N, "N", 7, 14.0067, isotopes_N, isotopes_N_size },
    { _15N, "_15N", 7, isotopes_15N[0].mass, isotopes_15N, isotopes_15N_size },
    { O, "O", 8, 15.9994, isotopes_O, isotopes_O_size },
    { _18O, "_18O", 8, isotopes_18O[0].mass, isotopes_18O, isotopes_18O_size },
    { F, "F", 9, 18.9984032, isotopes_F, isotopes_F_size },
    { Ne, "Ne", 10, 20.1797, isotopes_Ne, isotopes_Ne_size },
    { Na, "Na", 11, 22.98977, isotopes_Na, isotopes_Na_size },
    { Mg, "Mg", 12, 24.305, isotopes_Mg, isotopes_Mg_size },
    { Al, "Al", 13, 26.981538, isotopes_Al, isotopes_Al_size },
    { Si, "Si", 14, 28.0855, isotopes_Si, isotopes_Si_size },
    { P, "P", 15, 30.973761, isotopes_P, isotopes_P_size },
    { S, "S", 16, 32.065, isotopes_S, isotopes_S_size },
    { Cl, "Cl", 17, 35.453, isotopes_Cl, isotopes_Cl_size },
    { Ar, "Ar", 18, 39.948, isotopes_Ar, isotopes_Ar_size },
    { K, "K", 19, 39.0983, isotopes_K, isotopes_K_size },
    { Ca, "Ca", 20, 40.078, isotopes_Ca, isotopes_Ca_size },
    { Sc, "Sc", 21, 44.95591, isotopes_Sc, isotopes_Sc_size },
    { Ti, "Ti", 22, 47.867, isotopes_Ti, isotopes_Ti_size },
    { V, "V", 23, 50.9415, isotopes_V, isotopes_V_size },
    { Cr, "Cr", 24, 51.9961, isotopes_Cr, isotopes_Cr_size },
    { Mn, "Mn", 25, 54.938049, isotopes_Mn, isotopes_Mn_size },
    { Fe, "Fe", 26, 55.845, isotopes_Fe, isotopes_Fe_size },
    { Co, "Co", 27, 58.9332, isotopes_Co, isotopes_Co_size },
    { Ni, "Ni", 28, 58.6934, isotopes_Ni, isotopes_Ni_size },
    { Cu, "Cu", 29, 63.546, isotopes_Cu, isotopes_Cu_size },
    { Zn, "Zn", 30, 65.409, isotopes_Zn, isotopes_Zn_size },
    { Ga, "Ga", 31, 69.723, isotopes_Ga, isotopes_Ga_size },
    { Ge, "Ge", 32, 72.64, isotopes_Ge, isotopes_Ge_size },
    { As, "As", 33, 74.9216, isotopes_As, isotopes_As_size },
    { Se, "Se", 34, 78.96, isotopes_Se, isotopes_Se_size },
    { Br, "Br", 35, 79.904, isotopes_Br, isotopes_Br_size },
    { Kr, "Kr", 36, 83.798, isotopes_Kr, isotopes_Kr_size },
    { Rb, "Rb", 37, 85.4678, isotopes_Rb, isotopes_Rb_size },
    { Sr, "Sr", 38, 87.62, isotopes_Sr, isotopes_Sr_size },
    { Y, "Y", 39, 88.90585, isotopes_Y, isotopes_Y_size },
    { Zr, "Zr", 40, 91.224, isotopes_Zr, isotopes_Zr_size },
    { Nb, "Nb", 41, 92.90638, isotopes_Nb, isotopes_Nb_size },
    { Mo, "Mo", 42, 95.94, isotopes_Mo, isotopes_Mo_size },
    { Tc, "Tc", 43, 0, isotopes_Tc, isotopes_Tc_size },
    { Ru, "Ru", 44, 101.07, isotopes_Ru, isotopes_Ru_size },
    { Rh, "Rh", 45, 102.9055, isotopes_Rh, isotopes_Rh_size },
    { Pd, "Pd", 46, 106.42, isotopes_Pd, isotopes_Pd_size },
    { Ag, "Ag", 47, 107.8682, isotopes_Ag, isotopes_Ag_size },
    { Cd, "Cd", 48, 112.411, isotopes_Cd, isotopes_Cd_size },
    { In, "In", 49, 114.818, isotopes_In, isotopes_In_size },
    { Sn, "Sn", 50, 118.71, isotopes_Sn, isotopes_Sn_size },
    { Sb, "Sb", 51, 121.76, isotopes_Sb, isotopes_Sb_size },
    { Te, "Te", 52, 127.6, isotopes_Te, isotopes_Te_size },
    { I, "I", 53, 126.90447, isotopes_I, isotopes_I_size },
    { Xe, "Xe", 54, 131.293, isotopes_Xe, isotopes_Xe_size },
    { Cs, "Cs", 55, 132.90545, isotopes_Cs, isotopes_Cs_size },
    { Ba, "Ba", 56, 137.327, isotopes_Ba, isotopes_Ba_size },
    { La, "La", 57, 138.9055, isotopes_La, isotopes_La_size },
    { Ce, "Ce", 58, 140.116, isotopes_Ce, isotopes_Ce_size },
    { Pr, "Pr", 59, 140.90765, isotopes_Pr, isotopes_Pr_size },
    { Nd, "Nd", 60, 144.24, isotopes_Nd, isotopes_Nd_size },
    { Pm, "Pm", 61, 0, isotopes_Pm, isotopes_Pm_size },
    { Sm, "Sm", 62, 150.36, isotopes_Sm, isotopes_Sm_size },
    { Eu, "Eu", 63, 151.964, isotopes_Eu, isotopes_Eu_size },
    { Gd, "Gd", 64, 157.25, isotopes_Gd, isotopes_Gd_size },
    { Tb, "Tb", 65, 158.92534, isotopes_Tb, isotopes_Tb_size },
    { Dy, "Dy", 66, 162.5, isotopes_Dy, isotopes_Dy_size },
    { Ho, "Ho", 67, 164.93032, isotopes_Ho, isotopes_Ho_size },
    { Er, "Er", 68, 167.259, isotopes_Er, isotopes_Er_size },
    { Tm, "Tm", 69, 168.93421, isotopes_Tm, isotopes_Tm_size },
    { Yb, "Yb", 70, 173.04, isotopes_Yb, isotopes_Yb_size },
    { Lu, "Lu", 71, 174.967, isotopes_Lu, isotopes_Lu_size },
    { Hf, "Hf", 72, 178.49, isotopes_Hf, isotopes_Hf_size },
    { Ta, "Ta", 73, 180.9479, isotopes_Ta, isotopes_Ta_size },
    { W, "W", 74, 183.84, isotopes_W, isotopes_W_size },
    { Re, "Re", 75, 186.207, isotopes_Re, isotopes_Re_size },
    { Os, "Os", 76, 190.23, isotopes_Os, isotopes_Os_size },
    { Ir, "Ir", 77, 192.217, isotopes_Ir, isotopes_Ir_size },
    { Pt, "Pt", 78, 195.078, isotopes_Pt, isotopes_Pt_size },
    { Au, "Au", 79, 196.96655, isotopes_Au, isotopes_Au_size },
    { Hg, "Hg", 80, 200.59, isotopes_Hg, isotopes_Hg_size },
    { Tl, "Tl", 81, 204.3833, isotopes_Tl, isotopes_Tl_size },
    { Pb, "Pb", 82, 207.2, isotopes_Pb, isotopes_Pb_size },
    { Bi, "Bi", 83, 208.98038, isotopes_Bi, isotopes_Bi_size },
    { Po, "Po", 84, 0, isotopes_Po, isotopes_Po_size },
    { At, "At", 85, 0, isotopes_At, isotopes_At_size },
    { Rn, "Rn", 86, 0, isotopes_Rn, isotopes_Rn_size },
    { Fr, "Fr", 87, 0, isotopes_Fr, isotopes_Fr_size },
    { Ra, "Ra", 88, 0, isotopes_Ra, isotopes_Ra_size },
    { Ac, "Ac", 89, 0, isotopes_Ac, isotopes_Ac_size },
    { Th, "Th", 90, 232.0381, isotopes_Th, isotopes_Th_size },
    { Pa, "Pa", 91, 231.03588, isotopes_Pa, isotopes_Pa_size },
    { U, "U", 92, 238.02891, isotopes_U, isotopes_U_size },
    { Np, "Np", 93, 0, isotopes_Np, isotopes_Np_size },
    { Pu, "Pu", 94, 0, isotopes_Pu, isotopes_Pu_size },
    { Am, "Am", 95, 0, isotopes_Am, isotopes_Am_size },
    { Cm, "Cm", 96, 0, isotopes_Cm, isotopes_Cm_size },
    { Bk, "Bk", 97, 0, isotopes_Bk, isotopes_Bk_size },
    { Cf, "Cf", 98, 0, isotopes_Cf, isotopes_Cf_size },
    { Es, "Es", 99, 0, isotopes_Es, isotopes_Es_size },
    { Fm, "Fm", 100, 0, isotopes_Fm, isotopes_Fm_size },
    { Md, "Md", 101, 0, isotopes_Md, isotopes_Md_size },
    { No, "No", 102, 0, isotopes_No, isotopes_No_size },
    { Lr, "Lr", 103, 0, isotopes_Lr, isotopes_Lr_size },
    { Rf, "Rf", 104, 0, isotopes_Rf, isotopes_Rf_size },
    { Db, "Db", 105, 0, isotopes_Db, isotopes_Db_size },
    { Sg, "Sg", 106, 0, isotopes_Sg, isotopes_Sg_size },
    { Bh, "Bh", 107, 0, isotopes_Bh, isotopes_Bh_size },
    { Hs, "Hs", 108, 0, isotopes_Hs, isotopes_Hs_size },
    { Mt, "Mt", 109, 0, isotopes_Mt, isotopes_Mt_size },
    { Uun, "Uun", 110, 0, isotopes_Uun, isotopes_Uun_size },
    { Uuu, "Uuu", 111, 0, isotopes_Uuu, isotopes_Uuu_size },
    { Uub, "Uub", 112, 0, isotopes_Uub, isotopes_Uub_size },
    { Uuq, "Uuq", 114, 0, isotopes_Uuq, isotopes_Uuq_size },
    { Uuh, "Uuh", 116, 0, isotopes_Uuh, isotopes_Uuh_size },
};


PWIZ_API_DECL const int elementsSize_ = sizeof(elements_)/sizeof(Element);


PWIZ_API_DECL Element* elements()
{
    return elements_;
}


PWIZ_API_DECL int elementsSize()
{
    return elementsSize_;
}


} // namespace detail
} // namespace chemistry
} // namespace pwiz

