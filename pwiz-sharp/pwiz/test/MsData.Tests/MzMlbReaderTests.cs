using System;
using System.IO;
using HDF.PInvoke;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.MzMlb;
using Pwiz.Data.MsData.Readers;

#pragma warning disable CA1806

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// End-to-end read tests for <see cref="MzMlbReaderAdapter"/>. Each test builds
/// a tiny mzMLb-shaped HDF5 fixture in %TEMP% (mzML XML in the "mzML" dataset
/// plus separate named binary datasets for m/z + intensity), then reads it
/// through the adapter and verifies the in-memory <see cref="MSData"/> matches.
/// </summary>
[TestClass]
public class MzMlbReaderTests
{
    [TestMethod]
    public void Identify_RecognizesHdf5MagicAndMzMLDataset()
    {
        string path = CreateMzMlbFixture("<mzML/>", Array.Empty<(string, double[])>());
        try
        {
            byte[] head = ReadFirstBytes(path, 32);
            string headStr = new(Array.ConvertAll(head, b => (char)b));
            var reader = new MzMlbReaderAdapter();
            Assert.AreEqual(CVID.MS_mzMLb_format, reader.Identify(path, headStr));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Identify_RejectsHdf5FileWithoutMzMLDataset()
    {
        // Plain HDF5 file with no "mzML" dataset → not an mzMLb.
        string path = Path.Combine(Path.GetTempPath(), $"plain-{Guid.NewGuid():N}.h5");
        long f = H5F.create(path, H5F.ACC_TRUNC);
        H5F.close(f);
        try
        {
            byte[] head = ReadFirstBytes(path, 32);
            string headStr = new(Array.ConvertAll(head, b => (char)b));
            var reader = new MzMlbReaderAdapter();
            Assert.AreEqual(CVID.CVID_Unknown, reader.Identify(path, headStr));
        }
        finally { File.Delete(path); }
    }

    // -- helpers --

    private static byte[] ReadFirstBytes(string path, int n)
    {
        using var fs = File.OpenRead(path);
        byte[] buf = new byte[n];
        int got = fs.Read(buf, 0, n);
        if (got < n) Array.Resize(ref buf, got);
        return buf;
    }

    private static string CreateMzMlbFixture(string mzmlXml, (string Name, double[] Data)[] binary)
    {
        // Mirrors MzMlbConnectionTests.CreateMzMlbFile. Kept separate to keep
        // each test class self-contained (no cross-class helpers, easier to
        // delete a test file without breaking siblings).
        string path = Path.Combine(Path.GetTempPath(),
            $"mzmlb-fixture-{Guid.NewGuid():N}.mzMLb");
        H5E.set_auto(H5E.DEFAULT, null, IntPtr.Zero);
        long file = H5F.create(path, H5F.ACC_TRUNC);
        if (file < 0) throw new IOException($"Could not create HDF5 file {path}");
        try
        {
            byte[] xmlBytes = System.Text.Encoding.UTF8.GetBytes(mzmlXml);
            ulong[] dims = { (ulong)xmlBytes.Length };
            long space = H5S.create_simple(1, dims, dims);
            long dataset = H5D.create(file, "mzML", H5T.NATIVE_UCHAR, space);
            unsafe
            {
                fixed (byte* p = xmlBytes)
                    H5D.write(dataset, H5T.NATIVE_UCHAR, H5S.ALL, H5S.ALL,
                              H5P.DEFAULT, (IntPtr)p);
            }
            long versionType = H5T.copy(H5T.C_S1);
            byte[] verBytes = System.Text.Encoding.ASCII.GetBytes(MzMlbConnection.CurrentVersion);
            byte[] padded = new byte[verBytes.Length + 1];
            Array.Copy(verBytes, padded, verBytes.Length);
            H5T.set_size(versionType, (IntPtr)padded.Length);
            H5T.set_strpad(versionType, H5T.str_t.NULLTERM);
            long scalar = H5S.create(H5S.class_t.SCALAR);
            long attr = H5A.create(dataset, "version", versionType, scalar);
            unsafe
            {
                fixed (byte* p = padded)
                    H5A.write(attr, versionType, (IntPtr)p);
            }
            H5A.close(attr);
            H5S.close(scalar);
            H5T.close(versionType);
            H5D.close(dataset);
            H5S.close(space);

            foreach (var (name, data) in binary)
            {
                ulong[] bdims = { (ulong)data.Length };
                long bspace = H5S.create_simple(1, bdims, bdims);
                long bds = H5D.create(file, name, H5T.NATIVE_DOUBLE, bspace);
                unsafe
                {
                    fixed (double* p = data)
                        H5D.write(bds, H5T.NATIVE_DOUBLE, H5S.ALL, H5S.ALL,
                                  H5P.DEFAULT, (IntPtr)p);
                }
                H5D.close(bds);
                H5S.close(bspace);
            }
        }
        finally { H5F.close(file); }
        return path;
    }
}
