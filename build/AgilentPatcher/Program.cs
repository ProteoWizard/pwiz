// Cecil-patches Agilent SDK assemblies to remove uses of Delegate.BeginInvoke/EndInvoke,
// which .NET 5+ throws PlatformNotSupportedException on. Run AFTER the SDK is extracted
// from vendor_api_Agilent.7z. Patched DLLs are written in-place under
// vendor-assemblies/Agilent/ (gitignored) — never committed.
//
// Wiring: Agilent.csproj's PatchAgilentBeginInvoke target invokes this tool with the
// vendor-assemblies directory as the single argument, after ExtractAgilentAssemblies and
// before the SDK is referenced.
//
// Patches:
// - BaseDataAccess.dll: DataFileMgr.OpenDataFile / RefreshDataFile — replace
//   ReadNonMSInfoDelegate.BeginInvoke + AsyncCallback prologue + pop with synchronous
//   Invoke + stfld m_bNewNonMSDataAdded + stfld m_bNonMSReadDataComplete=true (mirrors
//   what the original AsyncCallback did before EndInvoke).
// - BaseCommon.dll: EventHelper.FireEventAsynchronously — replace AsyncFire.BeginInvoke
//   with a direct synchronous call to EventHelper.InvokeDelegate (the static method that
//   AsyncFire was bound to anyway).
// - BaseDataAccess.dll: MsDataReader.UncompressData — route its two
//   Stream.Read(byte[], int, int) calls (one on a DeflateStream, one on a GZipStream)
//   through a looping ReadFully helper. See PatchUncompressDataPartialReads for why.
//
// The two AsyncCallback methods (ReadNonMSDeviceRelatedInfoCallBack /
// ReadPendingFileInfoDelegateCallBack) become unreachable; their bodies still reference
// System.Runtime.Remoting.Messaging.AsyncResult (also removed in .NET 5+) but the JIT
// never compiles them so it doesn't matter.

using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

sealed class AgilentPatcher
{
    static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("usage: AgilentPatcher <vendor-assemblies-dir>");
            return 2;
        }
        string dir = args[0];
        if (!Directory.Exists(dir))
        {
            Console.Error.WriteLine($"AgilentPatcher: directory not found: {dir}");
            return 2;
        }

        try
        {
            PatchBaseDataAccess(Path.Combine(dir, "BaseDataAccess.dll"));
            PatchBaseCommon(Path.Combine(dir, "BaseCommon.dll"));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"AgilentPatcher failed: {e.Message}");
            Console.Error.WriteLine(e.StackTrace);
            return 1;
        }
        return 0;
    }

    static void PatchBaseDataAccess(string path)
    {
        string backup = path + ".prepatched";
        if (!File.Exists(backup)) File.Copy(path, backup, overwrite: false);
        // Always re-read from the prepatched copy so the patcher is idempotent.
        File.Copy(backup, path, overwrite: true);

        var dir = Path.GetDirectoryName(path);
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(dir);
        var rp = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true };
        using var module = ModuleDefinition.ReadModule(path, rp);

        var dataFileMgr = module.GetType("Agilent.MassSpectrometry.DataAnalysis.DataFileMgr")
            ?? throw new InvalidOperationException("DataFileMgr type not found");
        var nestedDel = dataFileMgr.NestedTypes
            .First(t => t.Name == "ReadNonMSInfoDelegate");
        var invokeMethod = nestedDel.Methods.First(m => m.Name == "Invoke");

        var fldNewNonMSDataAdded = dataFileMgr.Fields.First(f => f.Name == "m_bNewNonMSDataAdded");
        var fldNonMSReadDataComplete = dataFileMgr.Fields.First(f => f.Name == "m_bNonMSReadDataComplete");

        // Patch all OpenDataFile overloads + RefreshDataFile that contain the BeginInvoke call.
        int patched = 0;
        foreach (var m in dataFileMgr.Methods)
        {
            if (m.Name != "OpenDataFile" && m.Name != "RefreshDataFile") continue;
            if (!m.HasBody) continue;
            if (PatchReadNonMSInfoBeginInvoke(m, invokeMethod, fldNewNonMSDataAdded, fldNonMSReadDataComplete))
            {
                Console.WriteLine($"  patched {m.FullName}");
                patched++;
            }
        }
        if (patched == 0)
            throw new InvalidOperationException("No BeginInvoke patches applied to DataFileMgr — IL pattern may have changed.");

        PatchUncompressDataPartialReads(module);

        module.Write();
    }

    // .NET Framework's DeflateStream/GZipStream.Read(byte[], int, int) looped internally until
    // the caller's buffer was full or the stream ended. .NET Core 3.0 changed them to return as
    // soon as the inflater has produced anything — in practice ~13.5 KB per call regardless of
    // how much more is available (verified: a single Read of a 279560-byte deflate payload
    // returns 13588 on .NET 8).
    //
    // Agilent's MsDataReader.UncompressData issues exactly ONE Read for the whole spectrum and
    // then compares the returned count with MSSpectrumParams.UncompressedByteCount:
    //
    //     new DeflateStream(new MemoryStream(compressed), Decompress, true)
    //         .Read(outBuff, 0, uncompressedByteCount) == uncompressedByteCount
    //         ? ok : throw new InvalidDataException(
    //               "The data bytes read from the uncompressed data do not match the bytes stored")
    //
    // So on .NET 8 every spectrum whose uncompressed block exceeds ~13.5 KB fails to read. The
    // reader sees an exception for each such spectrum; the C++ reader, hosted on .NET Framework,
    // reads them all. Concretely: wash2.d's ~35000-point profile spectra are ALL unreadable
    // under .NET 8 and readable under C++.
    //
    // Rewriting the two Read call sites to a looping helper restores the .NET Framework
    // semantics the SDK was written against. UncompressData is the only method in the whole SDK
    // that decompresses (verified by scanning every BaseDataAccess / BaseCommon / BaseTof /
    // MassSpecDataReader / MIDAC method for a DeflateStream/GZipStream construction), so the
    // patch is confined to it: every other Stream.Read in the SDK is over a FileStream or
    // MemoryStream, which never returned short counts on either runtime.
    static void PatchUncompressDataPartialReads(ModuleDefinition module)
    {
        var msDataReader = module.GetType("Agilent.MassSpectrometry.DataAnalysis.MsDataReader")
            ?? throw new InvalidOperationException("MsDataReader type not found");
        var uncompress = msDataReader.Methods.FirstOrDefault(m => m.Name == "UncompressData" && m.HasBody)
            ?? throw new InvalidOperationException("MsDataReader.UncompressData not found");

        var readCalls = uncompress.Body.Instructions
            .Where(i => (i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Call) &&
                        i.Operand is MethodReference mr && IsStreamRead(mr))
            .ToList();
        if (readCalls.Count == 0)
            throw new InvalidOperationException(
                "No Stream.Read(byte[],int,int) call found in MsDataReader.UncompressData — IL pattern may have changed.");

        var streamRead = (MethodReference)readCalls[0].Operand;
        var readFully = EnsureReadFullyHelper(module, streamRead);

        var il = uncompress.Body.GetILProcessor();
        foreach (var call in readCalls)
            il.Replace(call, il.Create(OpCodes.Call, readFully));

        Console.WriteLine($"  patched {uncompress.FullName} ({readCalls.Count} partial-read call site(s))");
    }

    static bool IsStreamRead(MethodReference mr)
    {
        if (mr.Name != "Read" || mr.Parameters.Count != 3) return false;
        string declaring = mr.DeclaringType?.FullName ?? string.Empty;
        return declaring == "System.IO.Stream" ||
               declaring == "System.IO.Compression.DeflateStream" ||
               declaring == "System.IO.Compression.GZipStream";
    }

    // Injects, once per module:
    //
    //     internal static int Pwiz.AgilentPatch.StreamCompat.ReadFully(
    //         Stream stream, byte[] buffer, int offset, int count)
    //     {
    //         int total = 0;
    //         while (total < count)
    //         {
    //             int n = stream.Read(buffer, offset + total, count - total);
    //             if (n <= 0) break;
    //             total += n;
    //         }
    //         return total;
    //     }
    //
    // Its signature is (Stream, byte[], int, int) -> int so a `callvirt Stream::Read` can be
    // swapped for a `call ReadFully` with no other stack changes: the instance becomes arg 0.
    static MethodReference EnsureReadFullyHelper(ModuleDefinition module, MethodReference streamRead)
    {
        const string Namespace = "Pwiz.AgilentPatch";
        const string TypeName = "StreamCompat";
        const string MethodName = "ReadFully";

        var existing = module.GetType(Namespace, TypeName);
        if (existing != null)
            return existing.Methods.First(m => m.Name == MethodName);

        var holder = new TypeDefinition(Namespace, TypeName,
            TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Abstract |
            TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            module.TypeSystem.Object);
        module.Types.Add(holder);

        var method = new MethodDefinition(MethodName,
            MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig,
            module.TypeSystem.Int32);
        method.Parameters.Add(new ParameterDefinition("stream", ParameterAttributes.None, streamRead.DeclaringType));
        method.Parameters.Add(new ParameterDefinition("buffer", ParameterAttributes.None, new ArrayType(module.TypeSystem.Byte)));
        method.Parameters.Add(new ParameterDefinition("offset", ParameterAttributes.None, module.TypeSystem.Int32));
        method.Parameters.Add(new ParameterDefinition("count", ParameterAttributes.None, module.TypeSystem.Int32));
        holder.Methods.Add(method);

        var body = method.Body;
        body.InitLocals = true;
        var total = new VariableDefinition(module.TypeSystem.Int32);   // V_0
        var read = new VariableDefinition(module.TypeSystem.Int32);    // V_1
        body.Variables.Add(total);
        body.Variables.Add(read);

        var il = body.GetILProcessor();
        var loopStart = il.Create(OpCodes.Ldarg_0);
        var loopCondition = il.Create(OpCodes.Ldloc_0);
        var done = il.Create(OpCodes.Ldloc_0);

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Br, loopCondition);

        il.Append(loopStart);                       // stream
        il.Emit(OpCodes.Ldarg_1);                   // buffer
        il.Emit(OpCodes.Ldarg_2);                   // offset
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Add);                       // offset + total
        il.Emit(OpCodes.Ldarg_3);                   // count
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Sub);                       // count - total
        il.Emit(OpCodes.Callvirt, streamRead);
        il.Emit(OpCodes.Stloc_1);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ble, done);                 // n <= 0 -> stop (end of stream)
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc_0);                   // total += n

        il.Append(loopCondition);
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Blt, loopStart);            // total < count -> keep reading

        il.Append(done);
        il.Emit(OpCodes.Ret);

        return method;
    }

    static bool PatchReadNonMSInfoBeginInvoke(MethodDefinition method,
        MethodReference invokeMethod, FieldDefinition fldDataAdded, FieldDefinition fldComplete)
    {
        var body = method.Body;
        var instrs = body.Instructions;
        // Find the callvirt to ReadNonMSInfoDelegate::BeginInvoke.
        Instruction beginInvokeInsn = null;
        for (int i = 0; i < instrs.Count; i++)
        {
            var ins = instrs[i];
            if ((ins.OpCode == OpCodes.Callvirt || ins.OpCode == OpCodes.Call) &&
                ins.Operand is MethodReference mref &&
                mref.Name == "BeginInvoke" &&
                mref.DeclaringType.Name == "ReadNonMSInfoDelegate")
            {
                beginInvokeInsn = ins;
                break;
            }
        }
        if (beginInvokeInsn == null) return false;

        // Walk back from BeginInvoke to find the start of its argument-load sequence.
        // Stack at BeginInvoke: [delegate, list1, list2, AsyncCallback, state]
        // We need to find where the delegate (top-of-stack at start) was loaded.
        // The exact prologue is:
        //   ldloc.s V_<delegate>      <-- start
        //   ldloc.<n> (list1)
        //   ldloc.<n> (list2)
        //   ldarg.0
        //   ldftn ReadNonMSDeviceRelatedInfoCallBack
        //   newobj AsyncCallback::.ctor
        //   ldnull                    (state)
        //   callvirt BeginInvoke      <-- beginInvokeInsn
        //   pop                       (discards IAsyncResult)
        //
        // Walk back exactly 7 instructions from the callvirt to land on the delegate-load.
        Instruction cursor = beginInvokeInsn;
        for (int back = 0; back < 7; back++)
        {
            cursor = cursor.Previous ?? throw new InvalidOperationException(
                $"Couldn't walk back from BeginInvoke in {method.FullName} (only {back} instructions before).");
        }
        Instruction startInsn = cursor;
        Instruction popInsn = beginInvokeInsn.Next ?? throw new InvalidOperationException("BeginInvoke not followed by pop");
        if (popInsn.OpCode != OpCodes.Pop)
            throw new InvalidOperationException($"Expected pop after BeginInvoke in {method.FullName}, got {popInsn.OpCode}");

        // Capture the original instructions we'll consume:
        //   startInsn:        load delegate (ldloc.s V_<n> or similar)
        //   startInsn.Next:   load list1
        //   startInsn.Next.Next: load list2
        // Keep those three and discard the AsyncCallback setup + BeginInvoke + pop.
        var ldDelegate = startInsn;
        var ldList1 = startInsn.Next;
        var ldList2 = startInsn.Next.Next;

        // Add a fresh local for the bool result.
        var resultLocal = new VariableDefinition(method.Module.TypeSystem.Boolean);
        body.Variables.Add(resultLocal);

        var il = body.GetILProcessor();

        // Build replacement IL:
        //   ldDelegate
        //   ldList1
        //   ldList2
        //   callvirt Invoke (List, List) -> bool
        //   stloc resultLocal
        //   ldarg.0
        //   ldloc resultLocal
        //   stfld m_bNewNonMSDataAdded
        //   ldarg.0
        //   ldc.i4.1
        //   stfld m_bNonMSReadDataComplete
        //
        // Replace the original 10-instruction span (ldDelegate..pop) with the 11 new ones
        // by rewriting the ones we keep + replacing the rest.

        // Step A: rewrite ldDelegate..ldList2 in-place — they're already correct, leave them.
        // Step B: rewrite ldList2.Next (which was ldarg.0 for AsyncCallback) onward.
        var rewriteCursor = ldList2.Next;
        // Anchor: end-of-region exclusive is popInsn.Next.
        var afterPop = popInsn.Next;

        // Remove instructions from rewriteCursor up to and including popInsn.
        Instruction toRemove = rewriteCursor;
        while (toRemove != null && toRemove != afterPop)
        {
            var nxt = toRemove.Next;
            il.Remove(toRemove);
            toRemove = nxt;
        }

        // Now insert our new instructions BEFORE afterPop (or at end if afterPop is null).
        Instruction anchor = afterPop;

        Instruction[] toInsert = new[]
        {
            il.Create(OpCodes.Callvirt, method.Module.ImportReference(invokeMethod)),
            il.Create(OpCodes.Stloc, resultLocal),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Ldloc, resultLocal),
            il.Create(OpCodes.Stfld, fldDataAdded),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Ldc_I4_1),
            il.Create(OpCodes.Stfld, fldComplete),
        };
        foreach (var ins in toInsert)
        {
            if (anchor != null) il.InsertBefore(anchor, ins);
            else il.Append(ins);
        }

        // Strip exception handlers that referenced removed instructions. The original
        // BeginInvoke region has no try/catch around it (the try is in the callback), so
        // nothing to do — but a sanity check: ensure no handler points into the removed span.
        // (If it did, Cecil would crash on Write; clearer to fail here with a message.)

        return true;
    }

    static void PatchBaseCommon(string path)
    {
        string backup = path + ".prepatched";
        if (!File.Exists(backup)) File.Copy(path, backup, overwrite: false);
        File.Copy(backup, path, overwrite: true);

        var dir = Path.GetDirectoryName(path);
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(dir);
        var rp = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true };
        using var module = ModuleDefinition.ReadModule(path, rp);

        var eventHelper = module.GetType("Agilent.MassSpectrometry.DataAnalysis.EventHelper")
            ?? throw new InvalidOperationException("EventHelper type not found");
        var invokeDelegate = eventHelper.Methods.First(m => m.Name == "InvokeDelegate" && m.IsStatic);

        var fireEvent = eventHelper.Methods.First(m => m.Name == "FireEventAsynchronously");
        if (!PatchFireEventAsynchronously(fireEvent, invokeDelegate))
            throw new InvalidOperationException("FireEventAsynchronously patch failed — IL pattern may have changed.");
        Console.WriteLine($"  patched {fireEvent.FullName}");
        module.Write();
    }

    static bool PatchFireEventAsynchronously(MethodDefinition method, MethodReference invokeDelegateStatic)
    {
        var body = method.Body;
        var instrs = body.Instructions;
        // Find the callvirt to AsyncFire::BeginInvoke.
        Instruction beginInvokeInsn = null;
        for (int i = 0; i < instrs.Count; i++)
        {
            var ins = instrs[i];
            if ((ins.OpCode == OpCodes.Callvirt || ins.OpCode == OpCodes.Call) &&
                ins.Operand is MethodReference mref &&
                mref.Name == "BeginInvoke" &&
                mref.DeclaringType.Name == "AsyncFire")
            {
                beginInvokeInsn = ins;
                break;
            }
        }
        if (beginInvokeInsn == null) return false;

        // Original prologue (matching the dump):
        //   ldnull          (callback)
        //   ldnull          (state)
        //   callvirt AsyncFire::BeginInvoke    <-- beginInvokeInsn
        //   pop
        //
        // Stack just before the two ldnulls: [AsyncFire-delegate, del, args]
        // We want to swap the AsyncFire delegate for a direct synchronous call to
        // EventHelper::InvokeDelegate(del, args).
        //
        // Walk back to find the AsyncFire .ctor newobj + the ldftn that built the delegate.
        // Pattern (from the dump):
        //   ldloc.3
        //   ldloc.s V_4
        //   ldelem.ref
        //   stloc.2
        //   ldnull
        //   ldftn EventHelper::InvokeDelegate
        //   newobj AsyncFire::.ctor   <-- want to find this
        //   stloc.1
        //   ldloc.1                   <-- AsyncFire delegate goes onto stack here
        //   ldloc.2
        //   ldarg.1
        //   ldnull                    (callback)
        //   ldnull                    (state)
        //   callvirt BeginInvoke
        //   pop
        //
        // Strategy: replace the entire span from "ldnull (callback)" through "pop" with:
        //   <existing ldloc.1 / ldloc.2 / ldarg.1 already on stack>: pop the AsyncFire del,
        //   push the args, call static InvokeDelegate(Delegate, Object[]).

        // Walk back from BeginInvoke through: ldnull, ldnull → 2 instructions.
        Instruction firstLdnull = beginInvokeInsn.Previous?.Previous
            ?? throw new InvalidOperationException("Couldn't walk back from AsyncFire BeginInvoke");
        if (firstLdnull.OpCode != OpCodes.Ldnull || firstLdnull.Next.OpCode != OpCodes.Ldnull)
            throw new InvalidOperationException(
                $"Expected ldnull;ldnull before BeginInvoke in {method.FullName}, got {firstLdnull.OpCode};{firstLdnull.Next.OpCode}");

        Instruction popInsn = beginInvokeInsn.Next ?? throw new InvalidOperationException("BeginInvoke not followed by pop");
        if (popInsn.OpCode != OpCodes.Pop)
            throw new InvalidOperationException($"Expected pop after BeginInvoke in {method.FullName}");

        // The instruction BEFORE firstLdnull is the load of args (ldarg.1).
        // The one before that is ldloc.2 (delegate from arr[i]).
        // The one before THAT is ldloc.1 (AsyncFire delegate).
        // We want to keep ldloc.2 + ldarg.1, drop the ldloc.1 (AsyncFire) and the two ldnulls,
        // and replace the callvirt with `call EventHelper::InvokeDelegate`.
        Instruction ldArgs = firstLdnull.Previous;        // ldarg.1
        Instruction ldDel = ldArgs.Previous;              // ldloc.2
        Instruction ldAsyncFire = ldDel.Previous;         // ldloc.1

        var il = body.GetILProcessor();

        // Remove ldloc.1 (AsyncFire), the two ldnulls, and the pop.
        il.Remove(ldAsyncFire);
        il.Remove(firstLdnull.Next); // second ldnull
        il.Remove(firstLdnull);
        il.Remove(popInsn);

        // Replace beginInvokeInsn with `call EventHelper::InvokeDelegate(Delegate, Object[])`.
        var newCall = il.Create(OpCodes.Call, method.Module.ImportReference(invokeDelegateStatic));
        il.Replace(beginInvokeInsn, newCall);

        return true;
    }
}
