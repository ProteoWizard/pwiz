using System.Text;
using Pwiz.MsData.NativeAot;

namespace Pwiz.MsData.NativeAot.Tests;

/// <summary>
/// Direct tests against the AOT shim's <see cref="ExportsImpl"/> class. These run
/// against the MANAGED side of the shim — they verify the marshaling logic, GCHandle
/// lifecycle, error codes, and grow-on-truncate string convention without needing the
/// AOT-published native DLL. End-to-end AOT-DLL coverage lives in
/// <c>examples/cpp-aot-reader/CMakeLists.txt</c> (CTest-driven; see B).
/// </summary>
/// <remarks>
/// We target <see cref="ExportsImpl"/> rather than <see cref="Exports"/> because the
/// latter's methods are decorated [UnmanagedCallersOnly] and can only be called from
/// managed code via function pointer. Exports forwards 1:1 to ExportsImpl, so testing
/// the impl side covers everything except the attribute placement itself (which the
/// CTest end-to-end smoke test catches).
/// </remarks>
[TestClass]
public class ExportsTests
{
    // The reference mzML lives in the repo's example_data folder. Tests are run from
    // bin/Debug/net8.0/ so we walk up to find it.
    private static string TinyMzmlPath
    {
        get
        {
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                string candidate = Path.Combine(dir, "example_data", "tiny.pwiz.1.1.mzML");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException(
                "example_data/tiny.pwiz.1.1.mzML not found by walking up from " + AppContext.BaseDirectory);
        }
    }

    // ---------- helpers ----------

    private static unsafe IntPtr OpenOrFail(string path)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(path + "\0");
        IntPtr handle;
        fixed (byte* p = utf8)
        {
            int rc = ExportsImpl.Open(p, &handle);
            Assert.AreEqual(0, rc, $"open '{path}' rc={rc}, lastError='{ReadLastError()}'");
        }
        Assert.AreNotEqual(IntPtr.Zero, handle);
        return handle;
    }

    private static unsafe int TryOpen(string path, out IntPtr handle)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(path + "\0");
        IntPtr h;
        fixed (byte* p = utf8)
        {
            int rc = ExportsImpl.Open(p, &h);
            handle = h;
            return rc;
        }
    }

    private unsafe delegate int FillBuf(byte* buf, int len);

    private static unsafe string ReadGrowOnTruncate(FillBuf fill)
    {
        byte[] buf = new byte[256];
        while (true)
        {
            int n;
            fixed (byte* p = buf) n = fill(p, buf.Length);
            if (n < 0) return string.Empty;       // error path; caller asserts the int
            if (n < buf.Length)
            {
                // The C API null-terminates inside the supplied buffer; n is the
                // pre-terminator length.
                return Encoding.UTF8.GetString(buf, 0, n);
            }
            buf = new byte[n + 1];
        }
    }

    private static unsafe string ReadSpectrumId(IntPtr handle, int index)
    {
        return ReadGrowOnTruncate((b, l) => ExportsImpl.SpectrumId(handle, index, b, l));
    }

    private static unsafe string ReadSourceId(IntPtr handle)
    {
        return ReadGrowOnTruncate((b, l) => ExportsImpl.SourceId(handle, b, l));
    }

    private static unsafe string ReadLastError()
    {
        return ReadGrowOnTruncate((b, l) => ExportsImpl.GetLastError(b, l));
    }

    // ---------- tests ----------

    [TestMethod]
    public void Open_ValidFile_ReturnsHandleWithSpectra()
    {
        IntPtr handle = OpenOrFail(TinyMzmlPath);
        try
        {
            int count = ExportsImpl.SpectrumCount(handle);
            Assert.AreEqual(4, count, "tiny.pwiz.1.1.mzML has 4 spectra");
            Assert.AreEqual("urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz", ReadSourceId(handle));
            Assert.AreEqual("scan=19", ReadSpectrumId(handle, 0));
        }
        finally
        {
            ExportsImpl.Close(handle);
        }
    }

    [TestMethod]
    public void Open_MissingFile_ReturnsIoErrorAndSetsLastError()
    {
        string fake = Path.Combine(Path.GetTempPath(), "does-not-exist-" + System.Guid.NewGuid() + ".mzML");
        int rc = TryOpen(fake, out IntPtr handle);
        Assert.IsTrue(rc < 0, $"expected negative rc, got {rc}");
        Assert.AreEqual(IntPtr.Zero, handle, "handle must be zeroed on failure");
        string err = ReadLastError();
        Assert.IsTrue(err.Length > 0, "last_error should be populated on failure");
    }

    [TestMethod]
    public unsafe void Open_NullPathOrOutHandle_ReturnsInvalidArg()
    {
        // null path
        IntPtr handle = (IntPtr)0xDEAD;
        int rc1 = ExportsImpl.Open(null, &handle);
        Assert.AreEqual(-2, rc1, "null path => ErrInvalidArg(-2)");

        // null outHandle
        byte[] p = Encoding.UTF8.GetBytes("anything\0");
        fixed (byte* pp = p)
        {
            int rc2 = ExportsImpl.Open(pp, null);
            Assert.AreEqual(-2, rc2, "null outHandle => ErrInvalidArg(-2)");
        }
    }

    [TestMethod]
    public void SpectrumCount_NullHandle_ReturnsErrInvalidHandle()
    {
        // IntPtr.Zero is the documented sentinel. Truly bogus pointers (random bytes) are
        // undefined-behavior per the .NET docs for GCHandle.FromIntPtr — we don't test
        // that case because the platform doesn't guarantee a graceful failure.
        Assert.AreEqual(-1, ExportsImpl.SpectrumCount(IntPtr.Zero), "ErrInvalidHandle(-1)");
    }

    [TestMethod]
    public void SpectrumId_IndexOutOfRange_ReturnsErrIndex()
    {
        IntPtr handle = OpenOrFail(TinyMzmlPath);
        try
        {
            int rc = ExportsImpl.SpectrumCount(handle);
            Assert.AreEqual(4, rc);

            // index = -1 and index = count both out of range
            byte[] buf = new byte[64];
            unsafe
            {
                fixed (byte* p = buf)
                {
                    Assert.AreEqual(-3, ExportsImpl.SpectrumId(handle, -1, p, buf.Length));
                    Assert.AreEqual(-3, ExportsImpl.SpectrumId(handle, rc, p, buf.Length));
                }
            }
        }
        finally
        {
            ExportsImpl.Close(handle);
        }
    }

    [TestMethod]
    public unsafe void SpectrumId_TruncatedBuffer_NullTerminatesAndReturnsFullLength()
    {
        IntPtr handle = OpenOrFail(TinyMzmlPath);
        try
        {
            // The first spectrum's id is "scan=19" (7 UTF-8 bytes). Pass a 4-byte
            // buffer: contract is "writes truncated prefix + null terminator, returns
            // the FULL length so caller can grow + retry".
            byte[] tiny = new byte[4];
            int fullLen;
            fixed (byte* p = tiny)
            {
                fullLen = ExportsImpl.SpectrumId(handle, 0, p, tiny.Length);
            }
            Assert.AreEqual(7, fullLen, "full id length is 7 bytes ('scan=19')");
            Assert.AreEqual((byte)0, tiny[3], "byte at bufLen-1 must be the null terminator");
            Assert.AreEqual("sca", Encoding.UTF8.GetString(tiny, 0, 3),
                "truncated prefix should be the first 3 chars of 'scan=19'");

            // bufLen=0 must not segfault and must return the full length.
            int rc = ExportsImpl.SpectrumId(handle, 0, null, 0);
            Assert.AreEqual(7, rc);
        }
        finally
        {
            ExportsImpl.Close(handle);
        }
    }

    [TestMethod]
    public void SpectrumPeakCount_FirstSpectrum_ReadsBinaryDataLazily()
    {
        IntPtr handle = OpenOrFail(TinyMzmlPath);
        try
        {
            // tiny.pwiz.1.1.mzML's first spectrum has 15 peaks.
            int peaks = ExportsImpl.SpectrumPeakCount(handle, 0);
            Assert.AreEqual(15, peaks);
        }
        finally
        {
            ExportsImpl.Close(handle);
        }
    }

    [TestMethod]
    public void Close_NullHandle_IsNoOp()
    {
        // Must not throw, must not crash.
        ExportsImpl.Close(IntPtr.Zero);
    }

    [TestMethod]
    public void Close_AlreadyClosedHandle_DoesNotCrash()
    {
        IntPtr handle = OpenOrFail(TinyMzmlPath);
        ExportsImpl.Close(handle);
        // Calling Close again with the freed GCHandle should be a defensive no-op.
        ExportsImpl.Close(handle);
        // And a subsequent SpectrumCount call must return ErrInvalidHandle, not crash.
        Assert.AreEqual(-1, ExportsImpl.SpectrumCount(handle));
    }

    [TestMethod]
    public void LastError_FreshThread_StartsEmpty()
    {
        // On a thread that hasn't seen an error yet, GetLastError returns 0 length.
        // We can't fully isolate this from sibling tests (MSTest may share threads),
        // but we can at least confirm the convention: after a successful call the
        // last-error state isn't appended to.
        IntPtr handle = OpenOrFail(TinyMzmlPath);
        try
        {
            // Use Assembly.GetExecutingAssembly to force a fresh thread for the
            // isolation check.
            string err = string.Empty;
            var t = new System.Threading.Thread(() => err = ReadLastError());
            t.Start();
            t.Join();
            Assert.AreEqual(string.Empty, err, "fresh thread's last_error must start empty");
        }
        finally
        {
            ExportsImpl.Close(handle);
        }
    }
}
