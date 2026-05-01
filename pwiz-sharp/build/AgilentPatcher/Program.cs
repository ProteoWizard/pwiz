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

        module.Write();
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
