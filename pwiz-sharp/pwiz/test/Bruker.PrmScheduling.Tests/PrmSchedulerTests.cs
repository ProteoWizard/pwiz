using System.Data.SQLite;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Bruker.PrmScheduling.Tests;

/// <summary>
/// Port of cpp's <c>PrmSchedulerTest.cpp</c>, which drives the legacy C++/CLI
/// <c>pwiz.CLI.Bruker.PrmScheduling.Scheduler</c> end to end over the checked-in
/// <c>timstof_prm_scheduler.prmsqlite</c> template. Both implementations call the same native
/// <c>prmscheduler.dll</c>, so every number below is Bruker's, not ours: the four reference
/// tables (100 input targets, 134 scheduling entries, 197 time segments, 47 concurrent-frame
/// points) are copied row for row from that test, and a disagreement can only come from how
/// this port marshals structs across the P/Invoke boundary.
/// </summary>
/// <remarks>
/// Two details of cpp's setup are load-bearing and should not be "improved": it sets only
/// <c>ms1_repetition_time</c> on the measurement parameters (leaving
/// <c>default_pasef_collision_energies</c> false), and it compares its tables as a prefix of the
/// results (<c>expected.size() &lt;= actual.Count</c>) because the tail of the schedule was
/// truncated when they were captured.
///
/// The test carries <c>[TestCategory("RequiresPrmScheduler")]</c> and self-skips via
/// <see cref="Assert.Inconclusive(string)"/> where the native x64 Windows DLL cannot load.
/// </remarks>
[TestClass]
public class PrmSchedulerTests
{
    /// <summary>One row of cpp's <c>testTargets</c> table.</summary>
    private readonly record struct TargetRow(
        int Id, double MinRt, double MaxRt, double IsolationMz,
        double MinInverseK0, double MaxInverseK0, int Charge, double Ce);

    // What Bruker's DLL reports for the checked-in template; these move only if that file does.
    private const double MethodInfoMobilityGap = 0.006;
    private const double MethodInfoFrameRate = 10;
    private const double MethodInfoOneOverK0LowerLimit = 0.7;
    private const double MethodInfoOneOverK0UpperLimit = 1.2;

    /// <summary>cpp's <c>testTargets</c>: 100 PRM targets, each with a 200-second RT window.</summary>
    private static readonly TargetRow[] Targets =
    {
        new(1, -53.2000, 146.8000, 784.8662, 1.0216, 1.0497, 2, -1),
        new(2, -15.4000, 184.6000, 523.7778, 0.8433, 0.8716, 2, -1),
        new(3, 5.0000, 205.0000, 576.2796, 0.8592, 0.8874, 2, -1),
        new(4, 10.4000, 210.4000, 569.2863, 0.9010, 0.9292, 2, -1),
        new(5, 27.8000, 227.8000, 506.2826, 0.8218, 0.8501, 2, -1),
        new(6, 52.4000, 252.4000, 516.7964, 0.8625, 0.8908, 2, -1),
        new(7, 324.2000, 524.2000, 624.8007, 0.9304, 0.9586, 2, -1),
        new(8, 377.6000, 577.6000, 543.7419, 0.8648, 0.8931, 2, -1),
        new(9, 430.4000, 630.4000, 546.7847, 0.8637, 0.8920, 2, -1),
        new(10, 440.0000, 640.0000, 562.2582, 0.8399, 0.8682, 3, -1),
        new(11, 444.2000, 644.2000, 403.7084, 0.7490, 0.7775, 2, -1),
        new(12, 454.4000, 654.4000, 552.7078, 0.8671, 0.8954, 2, -1),
        new(13, 470.0000, 670.0000, 507.7625, 0.8229, 0.8512, 2, -1),
        new(14, 497.0000, 697.0000, 632.8273, 0.9349, 0.9631, 2, -1),
        new(15, 519.2000, 719.2000, 846.3635, 1.0541, 1.0822, 2, -1),
        new(16, 532.4000, 732.4000, 497.2509, 0.8422, 0.8705, 2, -1),
        new(17, 537.8000, 737.8000, 506.2352, 0.8195, 0.8478, 3, -1),
        new(18, 551.0000, 751.0000, 551.2719, 0.8603, 0.8886, 2, -1),
        new(19, 566.0000, 766.0000, 424.7079, 0.7570, 0.7854, 2, -1),
        new(20, 573.2000, 773.2000, 656.2852, 0.9247, 0.9529, 2, -1),
        new(21, 578.0000, 778.0000, 628.6362, 0.9428, 0.9710, 3, -1),
        new(22, 599.6000, 799.6000, 446.2139, 0.7991, 0.8274, 2, -1),
        new(23, 601.4000, 801.4000, 450.9626, 0.7866, 0.8149, 4, -1),
        new(24, 608.6000, 808.6000, 733.3406, 1.0137, 1.0418, 2, -1),
        new(25, 616.4000, 816.4000, 630.2897, 0.9033, 0.9315, 2, -1),
        new(26, 618.2000, 818.2000, 612.8113, 0.9247, 0.9529, 2, -1),
        new(27, 623.6000, 823.6000, 541.2774, 0.8603, 0.8886, 2, -1),
        new(28, 626.0000, 826.0000, 918.9001, 1.1213, 1.1493, 2, -1),
        new(29, 632.6000, 832.6000, 532.7829, 0.9010, 0.9292, 2, -1),
        new(30, 649.4000, 849.4000, 645.3029, 0.9507, 0.9788, 2, -1),
        new(31, 658.4000, 858.4000, 595.7817, 0.9462, 0.9743, 2, -1),
        new(32, 660.8000, 860.8000, 572.3076, 0.7763, 0.8047, 2, -1),
        new(33, 662.0000, 862.0000, 611.7973, 0.9089, 0.9371, 2, -1),
        new(34, 681.8000, 881.8000, 568.8375, 0.9360, 0.9642, 2, -1),
        new(35, 686.0000, 886.0000, 634.3214, 0.9495, 0.9777, 2, -1),
        new(36, 695.6000, 895.6000, 559.2877, 0.8829, 0.9112, 2, -1),
        new(37, 698.6000, 898.6000, 523.7668, 0.8852, 0.9134, 2, -1),
        new(38, 737.6000, 937.6000, 559.7789, 0.7763, 0.8047, 2, -1),
        new(39, 738.8000, 938.8000, 425.7623, 0.8274, 0.8558, 2, -1),
        new(40, 746.0000, 946.0000, 511.2840, 0.8444, 0.8727, 2, -1),
        new(41, 759.2000, 959.2000, 582.8019, 0.9157, 0.9439, 2, -1),
        new(42, 761.0000, 961.0000, 567.2759, 0.8886, 0.9168, 2, -1),
        new(43, 776.0000, 976.0000, 436.8953, 0.7422, 0.7706, 3, -1),
        new(44, 826.4000, 1026.4000, 625.3088, 0.9360, 0.9642, 2, -1),
        new(45, 840.2000, 1040.2000, 718.3262, 0.9856, 1.0137, 2, -1),
        new(46, 845.6000, 1045.6000, 844.9134, 1.1336, 1.1616, 2, -1),
        new(47, 849.8000, 1049.8000, 598.7850, 0.8546, 0.8829, 2, -1),
        new(48, 850.4000, 1050.4000, 566.7635, 0.8863, 0.9146, 2, -1),
        new(49, 893.0000, 1093.0000, 510.5775, 0.8286, 0.8569, 3, -1),
        new(50, 907.4000, 1107.4000, 661.8174, 0.9383, 0.9665, 2, -1),
        new(51, 932.0000, 1132.0000, 547.2603, 0.8659, 0.8942, 2, -1),
        new(52, 937.4000, 1137.4000, 497.2480, 0.8331, 0.8614, 2, -1),
        new(53, 960.2000, 1160.2000, 554.2818, 0.9146, 0.9428, 2, -1),
        new(54, 971.6000, 1171.6000, 416.5597, 0.7342, 0.7627, 3, -1),
        new(55, 983.6000, 1183.6000, 585.2758, 0.9078, 0.9360, 2, -1),
        new(56, 1011.2000, 1211.2000, 597.8005, 0.9134, 0.9417, 2, -1),
        new(57, 1030.4000, 1230.4000, 709.8488, 0.9586, 0.9867, 2, -1),
        new(58, 1035.2000, 1235.2000, 812.4302, 1.0339, 1.0620, 2, -1),
        new(59, 1037.0000, 1237.0000, 540.2689, 0.8976, 0.9259, 2, -1),
        new(60, 1037.0000, 1237.0000, 552.7919, 0.8739, 0.9021, 2, -1),
        new(61, 1047.8000, 1247.8000, 702.3322, 0.9484, 0.9766, 2, -1),
        new(62, 1062.8000, 1262.8000, 508.2897, 0.8433, 0.8716, 2, -1),
        new(63, 1081.4000, 1281.4000, 504.2500, 0.8773, 0.9055, 2, -1),
        new(64, 1095.2000, 1295.2000, 608.3073, 0.9067, 0.9349, 2, -1),
        new(65, 1116.2000, 1316.2000, 558.2849, 0.8807, 0.9089, 2, -1),
        new(66, 1118.0000, 1318.0000, 663.8350, 0.9292, 0.9574, 2, -1),
        new(67, 1130.6000, 1330.6000, 411.5495, 0.7559, 0.7843, 3, -1),
        new(68, 1136.0000, 1336.0000, 665.8304, 0.9811, 1.0092, 2, -1),
        new(69, 1167.8000, 1367.8000, 784.8865, 1.0227, 1.0508, 2, -1),
        new(70, 1177.4000, 1377.4000, 563.7825, 0.8897, 0.9180, 2, -1),
        new(71, 1178.0000, 1378.0000, 601.3045, 0.9202, 0.9484, 2, -1),
        new(72, 1201.4000, 1401.4000, 689.3143, 0.9462, 0.9743, 2, -1),
        new(73, 1212.8000, 1412.8000, 664.8245, 0.9619, 0.9901, 2, -1),
        new(74, 1230.2000, 1430.2000, 602.3242, 0.8908, 0.9191, 2, -1),
        new(75, 1236.2000, 1436.2000, 522.2866, 0.8614, 0.8897, 2, -1),
        new(76, 1241.6000, 1441.6000, 626.8223, 0.9439, 0.9721, 2, -1),
        new(77, 1259.6000, 1459.6000, 583.7274, 0.9134, 0.9417, 2, -1),
        new(78, 1275.8000, 1475.8000, 608.3088, 0.8365, 0.8648, 3, -1),
        new(79, 1299.2000, 1499.2000, 876.9327, 1.1515, 1.1794, 2, -1),
        new(80, 1342.4000, 1542.4000, 690.3640, 0.9619, 0.9901, 2, -1),
        new(81, 1349.0000, 1549.0000, 533.2799, 0.8773, 0.9055, 2, -1),
        new(82, 1357.4000, 1557.4000, 867.8643, 1.0990, 1.1269, 2, -1),
        new(83, 1391.6000, 1591.6000, 861.3508, 1.0721, 1.1001, 2, -1),
        new(84, 1392.8000, 1592.8000, 505.9179, 0.8535, 0.8818, 3, -1),
        new(85, 1394.0000, 1594.0000, 569.2892, 0.8965, 0.9247, 2, -1),
        new(86, 1397.6000, 1597.6000, 499.2657, 0.8478, 0.8761, 3, -1),
        new(87, 1399.4000, 1599.4000, 660.3359, 0.9743, 1.0025, 2, -1),
        new(88, 1421.6000, 1621.6000, 464.2482, 0.8320, 0.8603, 2, -1),
        new(89, 1423.4000, 1623.4000, 580.7875, 0.8999, 0.9281, 2, -1),
        new(90, 1436.0000, 1636.0000, 587.3204, 0.9112, 0.9394, 2, -1),
        new(91, 1437.2000, 1637.2000, 723.8474, 0.9878, 1.0160, 2, -1),
        new(92, 1447.4000, 1647.4000, 893.4597, 1.1538, 1.1816, 2, -1),
        new(93, 1449.2000, 1649.2000, 760.8802, 1.0463, 1.0743, 2, -1),
        new(94, 1454.6000, 1654.6000, 758.8840, 1.0799, 1.1079, 2, -1),
        new(95, 1460.0000, 1660.0000, 566.8092, 0.9304, 0.9586, 2, -1),
        new(96, 1460.6000, 1660.6000, 601.3046, 0.9495, 0.9777, 2, -1),
        new(97, 1464.8000, 1664.8000, 704.3431, 1.0137, 1.0418, 2, -1),
        new(98, 1473.2000, 1673.2000, 621.8176, 0.9089, 0.9371, 2, -1),
        new(99, 1506.8000, 1706.8000, 760.8781, 1.0485, 1.0766, 2, -1),
        new(100, 1514.0000, 1714.0000, 544.3140, 0.9168, 0.9450, 2, -1),
    };

    /// <summary>cpp's <c>testTimeSegments</c>. Its last segment (ending at DBL_MAX) is
    /// commented out there, so this table is a prefix of the real result.</summary>
    private static readonly (double Begin, double End)[] TimeSegmentBounds =
    {
        (-53.2, -15.4),
        (-15.4, 5.0),
        (5.0, 10.4),
        (10.4, 27.8),
        (27.8, 52.4),
        (52.4, 146.8),
        (146.8, 184.6),
        (184.6, 205.0),
        (205.0, 210.4),
        (210.4, 227.8),
        (227.8, 252.4),
        (252.4, 324.2),
        (324.2, 377.6),
        (377.6, 430.4),
        (430.4, 440.0),
        (440.0, 444.2),
        (444.2, 454.4),
        (454.4, 470.0),
        (470.0, 497.0),
        (497.0, 519.2),
        (519.2, 524.2),
        (524.2, 532.4),
        (532.4, 537.8),
        (537.8, 551.0),
        (551.0, 566.0),
        (566.0, 573.2),
        (573.2, 577.6),
        (577.6, 578.0),
        (578.0, 599.6),
        (599.6, 601.4),
        (601.4, 608.6),
        (608.6, 616.4),
        (616.4, 618.2),
        (618.2, 623.6),
        (623.6, 626.0),
        (626.0, 630.4),
        (630.4, 632.6),
        (632.6, 640.0),
        (640.0, 644.2),
        (644.2, 649.4),
        (649.4, 654.4),
        (654.4, 658.4),
        (658.4, 660.8),
        (660.8, 662.0),
        (662.0, 670.0),
        (670.0, 681.8),
        (681.8, 686.0),
        (686.0, 695.6),
        (695.6, 697.0),
        (697.0, 698.6),
        (698.6, 719.2),
        (719.2, 732.4),
        (732.4, 737.6),
        (737.6, 737.8),
        (737.8, 738.8),
        (738.8, 746.0),
        (746.0, 751.0),
        (751.0, 759.2),
        (759.2, 761.0),
        (761.0, 766.0),
        (766.0, 773.2),
        (773.2, 776.0),
        (776.0, 778.0),
        (778.0, 799.6),
        (799.6, 801.4),
        (801.4, 808.6),
        (808.6, 816.4),
        (816.4, 818.2),
        (818.2, 823.6),
        (823.6, 826.0),
        (826.0, 826.4),
        (826.4, 832.6),
        (832.6, 840.2),
        (840.2, 845.6),
        (845.6, 849.4),
        (849.4, 849.8),
        (849.8, 850.4),
        (850.4, 858.4),
        (858.4, 860.8),
        (860.8, 862.0),
        (862.0, 881.8),
        (881.8, 886.0),
        (886.0, 893.0),
        (893.0, 895.6),
        (895.6, 898.6),
        (898.6, 907.4),
        (907.4, 932.0),
        (932.0, 937.4),
        (937.4, 937.6),
        (937.6, 938.8),
        (938.8, 946.0),
        (946.0, 959.2),
        (959.2, 960.2),
        (960.2, 961.0),
        (961.0, 971.6),
        (971.6, 976.0),
        (976.0, 983.6),
        (983.6, 1011.2),
        (1011.2, 1026.4),
        (1026.4, 1030.4),
        (1030.4, 1035.2),
        (1035.2, 1037.0),
        (1037.0, 1040.2),
        (1040.2, 1045.6),
        (1045.6, 1047.8),
        (1047.8, 1049.8),
        (1049.8, 1050.4),
        (1050.4, 1062.8),
        (1062.8, 1081.4),
        (1081.4, 1093.0),
        (1093.0, 1095.2),
        (1095.2, 1107.4),
        (1107.4, 1116.2),
        (1116.2, 1118.0),
        (1118.0, 1130.6),
        (1130.6, 1132.0),
        (1132.0, 1136.0),
        (1136.0, 1137.4),
        (1137.4, 1160.2),
        (1160.2, 1167.8),
        (1167.8, 1171.6),
        (1171.6, 1177.4),
        (1177.4, 1178.0),
        (1178.0, 1183.6),
        (1183.6, 1201.4),
        (1201.4, 1211.2),
        (1211.2, 1212.8),
        (1212.8, 1230.2),
        (1230.2, 1230.4),
        (1230.4, 1235.2),
        (1235.2, 1236.2),
        (1236.2, 1237.0),
        (1237.0, 1241.6),
        (1241.6, 1247.8),
        (1247.8, 1259.6),
        (1259.6, 1262.8),
        (1262.8, 1275.8),
        (1275.8, 1281.4),
        (1281.4, 1295.2),
        (1295.2, 1299.2),
        (1299.2, 1316.2),
        (1316.2, 1318.0),
        (1318.0, 1330.6),
        (1330.6, 1336.0),
        (1336.0, 1342.4),
        (1342.4, 1349.0),
        (1349.0, 1357.4),
        (1357.4, 1367.8),
        (1367.8, 1377.4),
        (1377.4, 1378.0),
        (1378.0, 1391.6),
        (1391.6, 1392.8),
        (1392.8, 1394.0),
        (1394.0, 1397.6),
        (1397.6, 1399.4),
        (1399.4, 1401.4),
        (1401.4, 1412.8),
        (1412.8, 1421.6),
        (1421.6, 1423.4),
        (1423.4, 1430.2),
        (1430.2, 1436.0),
        (1436.0, 1436.2),
        (1436.2, 1437.2),
        (1437.2, 1441.6),
        (1441.6, 1447.4),
        (1447.4, 1449.2),
        (1449.2, 1454.6),
        (1454.6, 1459.6),
        (1459.6, 1460.0),
        (1460.0, 1460.6),
        (1460.6, 1464.8),
        (1464.8, 1473.2),
        (1473.2, 1475.8),
        (1475.8, 1499.2),
        (1499.2, 1506.8),
        (1506.8, 1514.0),
        (1514.0, 1542.4),
        (1542.4, 1549.0),
        (1549.0, 1557.4),
        (1557.4, 1591.6),
        (1591.6, 1592.8),
        (1592.8, 1594.0),
        (1594.0, 1597.6),
        (1597.6, 1599.4),
        (1599.4, 1621.6),
        (1621.6, 1623.4),
        (1623.4, 1636.0),
        (1636.0, 1637.2),
        (1637.2, 1647.4),
        (1647.4, 1649.2),
        (1649.2, 1654.6),
        (1654.6, 1660.0),
        (1660.0, 1660.6),
        (1660.6, 1664.8),
        (1664.8, 1673.2),
        (1673.2, 1706.8),
        (1706.8, 1714.0),
    };

    /// <summary>cpp's <c>testSchedulingEntries</c>, in its field order
    /// (frame_id, target_id, time_segment_id) so the rows diff against cpp line for line.</summary>
    private static readonly (uint FrameId, uint TargetId, uint TimeSegmentId)[] SchedulingEntries =
    {
        (0, 0, 0),
        (0, 0, 1),
        (0, 1, 1),
        (0, 0, 2),
        (0, 1, 2),
        (1, 0, 2),
        (1, 2, 2),
        (0, 0, 3),
        (0, 1, 3),
        (0, 3, 3),
        (1, 0, 3),
        (1, 2, 3),
        (1, 3, 3),
        (0, 0, 4),
        (0, 1, 4),
        (0, 3, 4),
        (1, 0, 4),
        (1, 2, 4),
        (1, 3, 4),
        (1, 4, 4),
        (0, 0, 5),
        (0, 1, 5),
        (0, 3, 5),
        (1, 0, 5),
        (1, 2, 5),
        (1, 3, 5),
        (1, 4, 5),
        (2, 0, 5),
        (2, 3, 5),
        (2, 4, 5),
        (2, 5, 5),
        (0, 1, 6),
        (0, 3, 6),
        (1, 2, 6),
        (1, 3, 6),
        (1, 4, 6),
        (2, 3, 6),
        (2, 4, 6),
        (2, 5, 6),
        (0, 2, 7),
        (0, 3, 7),
        (0, 4, 7),
        (1, 3, 7),
        (1, 4, 7),
        (1, 5, 7),
        (0, 3, 8),
        (0, 4, 8),
        (0, 5, 8),
        (0, 4, 9),
        (0, 5, 9),
        (0, 5, 10),
        (0, 6, 12),
        (0, 6, 13),
        (0, 7, 13),
        (0, 6, 14),
        (0, 7, 14),
        (1, 6, 14),
        (1, 8, 14),
        (0, 6, 15),
        (0, 7, 15),
        (1, 6, 15),
        (1, 8, 15),
        (2, 6, 15),
        (2, 9, 15),
        (0, 6, 16),
        (0, 7, 16),
        (0, 10, 16),
        (1, 6, 16),
        (1, 8, 16),
        (1, 10, 16),
        (2, 6, 16),
        (2, 9, 16),
        (2, 10, 16),
        (0, 6, 17),
        (0, 7, 17),
        (0, 10, 17),
        (1, 6, 17),
        (1, 8, 17),
        (1, 10, 17),
        (2, 6, 17),
        (2, 9, 17),
        (2, 10, 17),
        (3, 6, 17),
        (3, 10, 17),
        (3, 11, 17),
        (0, 6, 18),
        (0, 7, 18),
        (0, 10, 18),
        (0, 12, 18),
        (1, 6, 18),
        (1, 8, 18),
        (1, 10, 18),
        (1, 12, 18),
        (2, 6, 18),
        (2, 9, 18),
        (2, 10, 18),
        (3, 6, 18),
        (3, 10, 18),
        (3, 11, 18),
        (3, 12, 18),
        (0, 6, 19),
        (0, 7, 19),
        (0, 10, 19),
        (0, 12, 19),
        (1, 8, 19),
        (1, 10, 19),
        (1, 12, 19),
        (1, 13, 19),
        (2, 9, 19),
        (2, 10, 19),
        (2, 13, 19),
        (3, 10, 19),
        (3, 11, 19),
        (3, 12, 19),
        (3, 13, 19),
        (0, 6, 20),
        (0, 7, 20),
        (0, 10, 20),
        (0, 12, 20),
        (0, 14, 20),
        (1, 8, 20),
        (1, 10, 20),
        (1, 12, 20),
        (1, 13, 20),
        (1, 14, 20),
        (2, 9, 20),
        (2, 10, 20),
        (2, 13, 20),
        (2, 14, 20),
        (3, 10, 20),
        (3, 11, 20),
        (3, 12, 20),
        (3, 13, 20),
        (3, 14, 20),
    };

    /// <summary>cpp's <c>testConcurrentFrames</c>: the CONCURRENT_FRAMES metric's (x, y) points.</summary>
    private static readonly (double X, double Y)[] ConcurrentFrames =
    {
        (-53.2, 1),
        (-15.4, 1),
        (-15.4, 1),
        (5, 1),
        (5, 2),
        (10.4, 2),
        (10.4, 2),
        (27.8, 2),
        (27.8, 2),
        (52.4, 2),
        (52.4, 3),
        (146.8, 3),
        (146.8, 3),
        (184.6, 3),
        (184.6, 2),
        (205, 2),
        (205, 1),
        (210.4, 1),
        (210.4, 1),
        (227.8, 1),
        (227.8, 1),
        (252.4, 1),
        (252.4, 1),
        (324.2, 1),
        (324.2, 1),
        (377.6, 1),
        (377.6, 1),
        (430.4, 1),
        (430.4, 2),
        (440, 2),
        (440, 3),
        (444.2, 3),
        (444.2, 3),
        (454.4, 3),
        (454.4, 4),
        (470, 4),
        (470, 4),
        (497, 4),
        (497, 4),
        (519.2, 4),
        (519.2, 4),
        (524.2, 4),
        (524.2, 4),
        (532.4, 4),
        (532.4, 5),
        (537.8, 5),
        (537.8, 5),
    };

    /// <summary>Injected by MSTest; lets us surface diagnostics.</summary>
    public TestContext? TestContext { get; set; }

    [TestMethod]
    [TestCategory("RequiresPrmScheduler")]
    public void EndToEnd_AddTargets_GetScheduling_ProducesExpectedSchedule()
    {
        if (!IsNativeDllAvailable())
            Assert.Inconclusive("prmscheduler.dll could not be loaded; this environment cannot run the scheduler.");

        // prm_scheduling_file_open writes to the file it opens, so work on a throwaway copy.
        var workingCopy = CopyTemplateToTempDirectory();
        try
        {
            using (var s = new Scheduler(workingCopy))
                ScheduleReferenceTargets(s);

            // The native handle is closed, so the file is ours to read - and has to be, or the
            // scheduler is leaking a lock on every method export.
            AssertWrittenTargetsMatchInput(workingCopy);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(workingCopy)!, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// cpp's <c>test()</c> body: the parameters it sets, the 100 targets it adds, and every
    /// comparison it makes against the results.
    /// </summary>
    private void ScheduleReferenceTargets(Scheduler s)
    {
        // cpp sets ms1_repetition_time and nothing else, leaving default_pasef_collision_energies
        // false; both feed the scheduler, so neither is safe to "improve".
        var parameters = new AdditionalMeasurementParameters { ms1_repetition_time = 10 };
        s.SetAdditionalMeasurementParameters(parameters);

        foreach (var t in Targets)
        {
            var target = new InputTarget
            {
                time_in_seconds_begin = t.MinRt,
                time_in_seconds_end = t.MaxRt,
                isolation_mz = t.IsolationMz,
                monoisotopic_mz = t.IsolationMz,
                one_over_k0_lower_limit = t.MinInverseK0,
                one_over_k0_upper_limit = t.MaxInverseK0,
                charge = t.Charge,
                collision_energy = t.Ce,
                isolation_width = 3,
                one_over_k0 = (t.MaxInverseK0 + t.MinInverseK0) / 2,
                time_in_seconds = (t.MaxRt + t.MinRt) / 2,
            };
            s.AddInputTarget(target, t.Id.ToString(CultureInfo.InvariantCulture), string.Empty);
        }

        AssertMethodInfoMatchesTemplate(s);

        var timeSegments = new TimeSegmentList();
        var schedulingEntries = new SchedulingEntryList();
        var progressCalls = 0;
        s.GetScheduling(timeSegments, schedulingEntries, _ =>
        {
            ++progressCalls;
            return false;
        });
        Assert.IsTrue(progressCalls > 0, "the scheduler never reported progress");

        TestContext?.WriteLine(
            $"Scheduled {Targets.Length} targets into {schedulingEntries.Count} entries " +
            $"across {timeSegments.Count} time segments");

        AssertSchedulingEntriesMatchReference(schedulingEntries);
        AssertTimeSegmentsMatchReference(timeSegments);
        AssertConcurrentFramesMatchReference(s.GetSchedulingMetrics(SchedulingMetrics.CONCURRENT_FRAMES));

        s.WriteScheduling();

        // Leaves both lists empty, so anything reading them must come before this.
        AssertCancellingStopsScheduling(s, timeSegments, schedulingEntries);
    }

    /// <summary>
    /// The template's method parameters come from Bruker's DLL reading the checked-in
    /// .prmsqlite, so they are fixed until that file changes. Pinning all four catches a
    /// marshaling error the scheduling tables cannot see - a swapped pair of doubles in
    /// <c>PrmMethodInfo</c> never reaches the schedule.
    /// </summary>
    private void AssertMethodInfoMatchesTemplate(Scheduler s)
    {
        var methodInfoList = s.GetPrmMethodInfo();
        Assert.AreEqual(1, methodInfoList.Count, "expected exactly one MethodInfo for the template");

        var mi = methodInfoList[0];
        TestContext?.WriteLine(
            $"MethodInfo: mobility_gap={mi.mobility_gap} frame_rate={mi.frame_rate} " +
            $"1/K0=[{mi.one_over_k0_lower_limit}, {mi.one_over_k0_upper_limit}]");

        Assert.AreEqual(MethodInfoMobilityGap, mi.mobility_gap, 1e-8, "mobility_gap");
        Assert.AreEqual(MethodInfoFrameRate, mi.frame_rate, 1e-8, "frame_rate");
        Assert.AreEqual(MethodInfoOneOverK0LowerLimit, mi.one_over_k0_lower_limit, 1e-8, "one_over_k0_lower_limit");
        Assert.AreEqual(MethodInfoOneOverK0UpperLimit, mi.one_over_k0_upper_limit, 1e-8, "one_over_k0_upper_limit");
    }

    /// <summary>cpp compares all three ids of every entry in its table, in order.</summary>
    private static void AssertSchedulingEntriesMatchReference(SchedulingEntryList actual)
    {
        Assert.IsTrue(SchedulingEntries.Length <= actual.Count,
            $"expected at least {SchedulingEntries.Length} scheduling entries, got {actual.Count}");

        for (var i = 0; i < SchedulingEntries.Length; ++i)
        {
            var expected = SchedulingEntries[i];
            var entry = actual[i];
            Assert.AreEqual(expected.TimeSegmentId, entry.time_segment_id, $"entry {i}: time_segment_id");
            Assert.AreEqual(expected.FrameId, entry.frame_id, $"entry {i}: frame_id");
            Assert.AreEqual(expected.TargetId, entry.target_id, $"entry {i}: target_id");
        }
    }

    /// <summary>Segment bounds are echoes of the input RT windows; cpp compares them exactly.</summary>
    private static void AssertTimeSegmentsMatchReference(TimeSegmentList actual)
    {
        Assert.IsTrue(TimeSegmentBounds.Length <= actual.Count,
            $"expected at least {TimeSegmentBounds.Length} time segments, got {actual.Count}");

        for (var i = 0; i < TimeSegmentBounds.Length; ++i)
        {
            var expected = TimeSegmentBounds[i];
            var segment = actual[i];
            Assert.AreEqual(expected.Begin, segment.time_in_seconds_begin, $"segment {i}: time_in_seconds_begin");
            Assert.AreEqual(expected.End, segment.time_in_seconds_end, $"segment {i}: time_in_seconds_end");
        }
    }

    /// <summary>cpp allows 1e-8 here because y is a computed frame count, not a copied bound.</summary>
    private static void AssertConcurrentFramesMatchReference(DataPointList actual)
    {
        Assert.IsTrue(ConcurrentFrames.Length <= actual.Count,
            $"expected at least {ConcurrentFrames.Length} concurrent-frame points, got {actual.Count}");

        for (var i = 0; i < ConcurrentFrames.Length; ++i)
        {
            var expected = ConcurrentFrames[i];
            var point = actual[i];
            Assert.AreEqual(expected.X, point.x, 1e-8, $"concurrent frame {i}: x");
            Assert.AreEqual(expected.Y, point.y, 1e-8, $"concurrent frame {i}: y");
        }
    }

    /// <summary>
    /// cpp's <c>ShowProgressCancelAt50</c>: returning true past the halfway mark must stop the
    /// scheduler, and it must not call back again afterwards (cpp throws if it does). The managed
    /// <see cref="Scheduler.GetScheduling"/> swallows the native "user request" error, so the
    /// lists it was handed must come back the way they went in - empty.
    /// </summary>
    private static void AssertCancellingStopsScheduling(Scheduler s, TimeSegmentList timeSegments, SchedulingEntryList schedulingEntries)
    {
        timeSegments.Clear();
        schedulingEntries.Clear();

        var canceled = false;
        var callsAfterCancel = 0;
        var highestPercentage = double.NegativeInfinity;
        s.GetScheduling(timeSegments, schedulingEntries, percentage =>
        {
            if (canceled)
            {
                ++callsAfterCancel;
                return true;
            }
            highestPercentage = Math.Max(highestPercentage, percentage);
            if (percentage <= 50)
                return false;
            canceled = true;
            return true;
        });

        Assert.IsTrue(canceled, $"progress never passed 50% (highest was {highestPercentage})");
        Assert.AreEqual(0, callsAfterCancel, "progress callback was called after cancelling");
        Assert.AreEqual(0, schedulingEntries.Count, "cancelled scheduling still produced entries");
        Assert.AreEqual(0, timeSegments.Count, "cancelled scheduling still produced time segments");
    }

    /// <summary>
    /// The schedule is built from RT and mobility alone, so most of <see cref="InputTarget"/> never
    /// reaches it: isolation m/z, isolation width, collision energy, charge and the apex values
    /// could each be marshaled into the wrong native field with every assertion above still
    /// passing, while an exported instrument method fragmented the wrong ion. They resurface in the
    /// .prmsqlite the scheduler writes, and the checked-in template - itself a written copy of
    /// cpp's run - shows what Bruker stores for exactly these targets: a collision energy of -1
    /// becomes NULL, and the apex 1/K0 and RT are the midpoints cpp computes.
    /// </summary>
    private static void AssertWrittenTargetsMatchInput(string schedulingFile)
    {
        using var connection = new SQLiteConnection($"Data Source={schedulingFile};Version=3;Read Only=True");
        connection.Open();

        var specifications = ReadTable(connection,
            "SELECT IsolationMz, IsolationWidth, OneOverK0LowerLimit, OneOverK0UpperLimit, CollisionEnergy " +
            "FROM PrmTargetSpecification ORDER BY Id");
        var characteristics = ReadTable(connection,
            "SELECT ExternalId, OneOverK0, MonoisotopicMz, TimeInSeconds, Charge " +
            "FROM PrmTargetAdditionalCharacteristics ORDER BY Id");

        Assert.AreEqual(Targets.Length, specifications.Count, "rows written to PrmTargetSpecification");
        Assert.AreEqual(Targets.Length, characteristics.Count, "rows written to PrmTargetAdditionalCharacteristics");

        for (var i = 0; i < Targets.Length; ++i)
        {
            var t = Targets[i];

            var specification = specifications[i];
            Assert.AreEqual(t.IsolationMz, specification[0], $"target {t.Id}: IsolationMz");
            Assert.AreEqual(3.0, specification[1], $"target {t.Id}: IsolationWidth");
            Assert.AreEqual(t.MinInverseK0, specification[2], $"target {t.Id}: OneOverK0LowerLimit");
            Assert.AreEqual(t.MaxInverseK0, specification[3], $"target {t.Id}: OneOverK0UpperLimit");
            Assert.AreEqual(DBNull.Value, specification[4], $"target {t.Id}: CollisionEnergy");

            var characteristic = characteristics[i];
            Assert.AreEqual(t.Id.ToString(CultureInfo.InvariantCulture), characteristic[0], $"target {t.Id}: ExternalId");
            Assert.AreEqual((t.MaxInverseK0 + t.MinInverseK0) / 2, characteristic[1], $"target {t.Id}: OneOverK0");
            Assert.AreEqual(t.IsolationMz, characteristic[2], $"target {t.Id}: MonoisotopicMz");
            Assert.AreEqual((t.MaxRt + t.MinRt) / 2, characteristic[3], $"target {t.Id}: TimeInSeconds");
            Assert.AreEqual((long)t.Charge, characteristic[4], $"target {t.Id}: Charge");
        }
    }

    private static List<object[]> ReadTable(SQLiteConnection connection, string sql)
    {
        using var command = new SQLiteCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var rows = new List<object[]>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }
        return rows;
    }

    /// <summary>Returns true iff prmscheduler.dll can be loaded by the OS loader. The csproj drops
    /// the repo's x64 build next to the test runner, so on Windows this should always succeed.</summary>
    private static bool IsNativeDllAvailable()
    {
        try
        {
            return NativeLibrary.TryLoad("prmscheduler", out _);
        }
        catch (DllNotFoundException) { return false; }
        catch (BadImageFormatException) { return false; }
    }

    /// <summary>
    /// Copies the .prmsqlite template the csproj staged beside the test binary into a fresh temp
    /// directory. Scheduling writes to the file, so the checked-in copy must never be opened.
    /// </summary>
    private static string CopyTemplateToTempDirectory()
    {
        var template = Path.Combine(AppContext.BaseDirectory, "timstof_prm_scheduler.prmsqlite");
        Assert.IsTrue(File.Exists(template), $"the scheduling template was not staged to {template}");

        var tempDir = Path.Combine(Path.GetTempPath(), "prm_scheduler_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dest = Path.Combine(tempDir, "timstof_prm_scheduler.prmsqlite");
        File.Copy(template, dest, overwrite: true);
        return dest;
    }
}
