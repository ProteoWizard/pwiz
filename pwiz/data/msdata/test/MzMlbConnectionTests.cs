using System;
using System.IO;
using System.Linq;
using HDF.PInvoke;
using Pwiz.Data.MsData.MzMlb;

#pragma warning disable CA1806 // HDF5 close() ints — see MzMlbConnection.cs

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Round-trip tests for <see cref="MzMlbConnection"/>. We don't have any
/// pre-existing .mzMLb fixtures in the repo, so each test builds its own tiny
/// HDF5 file in <see cref="Path.GetTempPath"/> with the mzMLb shape (chunked
/// "mzML" dataset with a "version" attribute + named binary datasets), then
/// reads it back via the connection and asserts.
/// </summary>
[TestClass]
public class MzMlbConnectionTests
{
    [TestMethod]
    public void MzMlStream_SupportsSeek()
    {
        string xml = "0123456789";
        string path = CreateMzMlbFile(xml, binaryDatasets: Array.Empty<(string, double[])>());
        try
        {
            using var conn = MzMlbConnection.OpenForRead(path);
            using var stream = conn.OpenMzMlStream();
            Assert.AreEqual(10, stream.Length);

            stream.Seek(5, SeekOrigin.Begin);
            byte[] buf = new byte[3];
            Assert.AreEqual(3, stream.Read(buf, 0, 3));
            Assert.AreEqual("567", System.Text.Encoding.ASCII.GetString(buf));

            stream.Seek(-2, SeekOrigin.End);
            Assert.AreEqual(2, stream.Read(buf, 0, 3));
            Assert.AreEqual('8', (char)buf[0]);
            Assert.AreEqual('9', (char)buf[1]);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void ReadDoubles_PullsNamedBinaryDataset()
    {
        var mz = new[] { 100.1, 200.2, 300.3, 400.4, 500.5 };
        var intensity = new[] { 1000.0, 2000.0, 3000.0, 4000.0, 5000.0 };
        string path = CreateMzMlbFile("<mzML/>",
            binaryDatasets: new[]
            {
                ("spectrum_MS_1000514_float64", mz),
                ("spectrum_MS_1000515_float64", intensity),
            });
        try
        {
            using var conn = MzMlbConnection.OpenForRead(path);
            Assert.IsTrue(conn.Exists("spectrum_MS_1000514_float64"));
            Assert.IsFalse(conn.Exists("nope"));

            var readMz = new double[5];
            Assert.AreEqual(5, conn.ReadDoubles("spectrum_MS_1000514_float64", 0, readMz));
            CollectionAssert.AreEqual(mz, readMz);

            // Offset + partial read.
            var readIntensity = new double[3];
            Assert.AreEqual(3, conn.ReadDoubles("spectrum_MS_1000515_float64", 2, readIntensity));
            CollectionAssert.AreEqual(intensity.Skip(2).Take(3).ToArray(), readIntensity);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void IdentifyOnly_AcceptsFileWithoutVersionString()
    {
        // identifyOnly=true: we relax the version check so Reader_MzMlb.Identify
        // can answer "yes, this looks like an mzMLb file" without rejecting
        // arbitrary mzMLb-shaped HDF5 files that don't have the version attr
        // (the cpp behavior).
        string xml = "<mzML/>";
        string path = CreateMzMlbFile(xml, binaryDatasets: Array.Empty<(string, double[])>(),
            includeVersionAttribute: false);
        try
        {
            // Should not throw.
            using (MzMlbConnection.OpenForRead(path, identifyOnly: true)) { }
            // Without identifyOnly, missing version is an error.
            Assert.ThrowsException<IOException>(() => MzMlbConnection.OpenForRead(path));
        }
        finally { File.Delete(path); }
    }

    // -- helpers --

    private static string CreateMzMlbFile(
        string mzmlXml,
        (string Name, double[] Data)[] binaryDatasets,
        bool includeVersionAttribute = true)
    {
        string path = Path.Combine(Path.GetTempPath(),
            $"mzmlb-fixture-{Guid.NewGuid():N}.mzMLb");

        H5E.set_auto(H5E.DEFAULT, null, IntPtr.Zero);
        long file = H5F.create(path, H5F.ACC_TRUNC);
        if (file < 0) throw new IOException($"Could not create HDF5 file {path}");
        try
        {
            // --- mzML dataset (fixed-size, char). The real mzMLb writer uses a
            //     chunked dataset with UNLIMITED max dim because writing happens
            //     in append-mode. For a read-only test fixture, a fixed-size 1-D
            //     dataset is enough and lets us skip the chunk-property-list dance. ---
            byte[] xmlBytes = System.Text.Encoding.UTF8.GetBytes(mzmlXml);
            ulong[] dims = { (ulong)xmlBytes.Length };
            long space = H5S.create_simple(1, dims, dims);
            long dataset = H5D.create(file, "mzML", H5T.NATIVE_UCHAR, space);
            if (dataset < 0) throw new IOException("H5D.create failed for 'mzML'");
            unsafe
            {
                fixed (byte* p = xmlBytes)
                {
                    H5D.write(dataset, H5T.NATIVE_UCHAR, H5S.ALL, H5S.ALL,
                              H5P.DEFAULT, (IntPtr)p);
                }
            }
            if (includeVersionAttribute)
            {
                long versionType = H5T.copy(H5T.C_S1);
                byte[] verBytes = System.Text.Encoding.ASCII.GetBytes(MzMlbConnection.CurrentVersion);
                // +1 for NUL terminator — matches the cpp writer (and our own
                // MzMlbConnection.WriteVersionAttribute). Without it the cpp
                // reader treats the attribute as a C string and reads adjacent
                // memory.
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
            }
            H5D.close(dataset);
            H5S.close(space);

            // --- binary datasets (double, 1-D, contiguous) ---
            foreach (var (name, data) in binaryDatasets)
            {
                ulong[] bdims = { (ulong)data.Length };
                long bspace = H5S.create_simple(1, bdims, bdims);
                long bds = H5D.create(file, name, H5T.NATIVE_DOUBLE, bspace);
                unsafe
                {
                    fixed (double* p = data)
                    {
                        H5D.write(bds, H5T.NATIVE_DOUBLE, H5S.ALL, H5S.ALL,
                                  H5P.DEFAULT, (IntPtr)p);
                    }
                }
                H5D.close(bds);
                H5S.close(bspace);
            }
        }
        finally
        {
            H5F.close(file);
        }
        return path;
    }
}
