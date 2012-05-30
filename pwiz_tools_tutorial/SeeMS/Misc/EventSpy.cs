//
// $Id$
//

////////////////////////////////////////////////////////////////
// MSDN Magazine -- May 2004
// If this code works, it was written by Paul DiLascia.
// If not, I don't know who wrote it.
// Compiles with Visual Studio .NET on Windows XP. Tab size=3.
//
using System;
using System.Diagnostics;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

///////////////////
// SpyTools provides the EventSpy class, which lets you spy on events raised
// by any .NET Framework object.
//
namespace SpyTools
{
	// the EventSpy will call your delegate whenever an event occurs.
	public delegate void SpyEventHandler(object sender, SpyEventArgs e);

	// Event args to report the name of the event.
	public class SpyEventArgs : EventArgs {
		private String evName;
		private EventArgs args;

		public String EventName
		{
			get { return evName; }
		}

		public EventArgs EventArgs
		{
			get { return args; }
		}

		public SpyEventArgs(String s, EventArgs e)
		{
			evName = s;
			args = e;
		}
	}

	// Main EventSpy class
	public class EventSpy : Object
	{
		// EventSpy raises a SpyEvent any time the spied-upon object raises an
		// event--whew!
		public event SpyEventHandler SpyEvent;

		// Create EventSpy.
		// Arguments are a name and object to spy on (the spy target).
		// 
		public EventSpy (String spyname, Object o)
		{
			// create dynamic assembly
			AssemblyName name = new AssemblyName();
			name.Name = "EventSpy" + spyname;
			AssemblyBuilder asm = AppDomain.CurrentDomain.DefineDynamicAssembly(name,
				AssemblyBuilderAccess.Run);

			// create dynamic module
			ModuleBuilder mod = asm.DefineDynamicModule("EventSpyModule",true);

			// What follows is a whole bunch of grody code, all of whose purpose
			// is to generate a dynamic class that looks more or less like this: 
			// 
			// public class EvSpyImpl
			// {
			// 	private EventSpy spy;
			// 	public EvSpyImpl(EventSpy s)
			// 	{
			// 		spy = s;
			// 	}
			// 	public void OnEventXxx(object o, EventXxxArgs e)
			// 	{
			// 		spy.ReportEvent("EventXxx", o, e);
			// 	}
			// }
			//
			
			// First, create the class
			TypeBuilder helperClass = mod.DefineType("EvSpyImpl",
				TypeAttributes.Public);

			// Add a field called "spy" of type EventSpy.
			FieldBuilder fld = helperClass.DefineField("spy", typeof(EventSpy),
				FieldAttributes.Private);

			// Create constructor to initialize the "spy" field.
			// In C#, the ctor would look like this:
			//
			//		public EvSpyImpl(EventSpy s)
			//		{
			//			spy = s;
			//		}
			//
			// The MSIL for this looks like this (from ILDASM):
			//
			//		ldarg.0
			//		call instance void [mscorlib]System.Object::.ctor()
			//		ldarg.0
			//		ldarg.1
			//		stfld class SpyTools.EventSpy SpyTools.EventSpy::spy
			//		ret
			//
			ConstructorBuilder ctor = helperClass.DefineConstructor(
				MethodAttributes.Public,
				CallingConventions.Standard,
				new Type[] { typeof(EventSpy)});
			ILGenerator ilctor = ctor.GetILGenerator();
			ilctor.Emit(OpCodes.Ldarg_0);
			ConstructorInfo basector = typeof(Object).GetConstructor(new Type[0]);
			ilctor.Emit(OpCodes.Call,basector);
			ilctor.Emit(OpCodes.Ldarg_0);
			ilctor.Emit(OpCodes.Ldarg_1);
			ilctor.Emit(OpCodes.Stfld, fld);
			ilctor.Emit(OpCodes.Ret);

			// Now create an OnEventXxx method for every event Xxx exposed by
			// the target object (o). Each method looks like this:
			//
			// 	public void OnEventXxx(object o, EventXxxArgs e)
			// 	{
			// 		spy.ReportEvent("EventXxx", o, e);
			// 	}
			// }
			//
			// The MSIL for this looks like this (from ILDASM):
			//
			//		ldarg.0
			//		ldfld class SpyTools.EventSpy SpyTools.EventSpy::spy
			//		ldstr "EventXxx"
			//		ldarg.1
			//		ldarg.2
			//		callvirt	instance void SpyTools.EventSpy::ReportEvent(string,
			//				object, class [mscorlib]System.EventArgs)
			//		ret
			//
			Type targType = o.GetType();
			BindingFlags whichEvents = BindingFlags.Instance|BindingFlags.Public;
			EventInfo[] allEvents = targType.GetEvents(whichEvents);

			MethodInfo miReportEvent = this.GetType().GetMethod("ReportEvent");

			// loop over all events to create each handler
			foreach (EventInfo ev in allEvents) {
                if( ev.Name.Contains( "MouseMove" ) )
                    continue;

				// get event handler (delegate) Type
				Type delgType = ev.EventHandlerType; 

				// To get parameter types (eg., FooEventArgs), need to get
				// parameter types of the delegate's Invoke method.
				MethodInfo invoke = delgType.GetMethod("Invoke");
				ParameterInfo[] prams = invoke.GetParameters();
				int len = prams.Length;

				// Copy parameter types into an array 
				Type[] mthparams = new Type[len];
				for (int i=0; i<len; i++) {
					mthparams[i] = prams[i].ParameterType;
				}

				// name of method is "On" + eventName, eg., "OnFooEvent"
				String mthname = "On" + ev.Name;

				// create the method with proper signature
				MethodBuilder mthd = helperClass.DefineMethod(
					mthname,
					MethodAttributes.Public,
                    invoke.ReturnType,
					mthparams);

				// Now generate the MSIL as described in comment above.
				ILGenerator il = mthd.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldfld,fld);
				il.Emit(OpCodes.Ldstr,ev.Name);
				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Ldarg_2);
				il.Emit(OpCodes.Callvirt, miReportEvent);
				il.Emit(OpCodes.Ret);
			}

			// Once all fields and methods are defined, finally create the Type.
			Type mytype = helperClass.CreateType();

			// Now create an instance of it with "this" as the spy object.
			Object spyimpl = Activator.CreateInstance(mytype,new Object[]{this});

			// Now hook up each event handler to its event in the target object.
			foreach (EventInfo ev in allEvents) {
                if( ev.Name.Contains( "MouseMove" ) )
                    continue;

				ev.AddEventHandler(o,
					Delegate.CreateDelegate(ev.EventHandlerType,
					spyimpl, "On" + ev.Name));
			}
		}

		// Each event handler in the helper class calls this function to report
		// the event. I simply raise a SpyEvent to anyone who's listening.
		public void ReportEvent(String name, Object sender, EventArgs e)
		{
			SpyEvent(sender, new SpyEventArgs(name, e));
		}

		// This method dumps all the events defined by a class to the
		// diagnostic stream.
		public void DumpEvents(Type type)
		{
			Trace.WriteLine(String.Format("Event dump for {0}", type.FullName));
			foreach (EventInfo ev in type.GetEvents()) {
				Trace.Write(ev.Name + "(");
				MethodInfo mi = ev.EventHandlerType.GetMethod("Invoke");
				bool first = true;
				foreach (ParameterInfo pi in mi.GetParameters()) {
					if (!first) {
						Trace.Write(",");
					}
					Trace.Write(pi.ParameterType);
					first=false;
				}
				Trace.WriteLine(")");
			}
		}
		
#if true
		// The following code is not used--it's here only so you can see the
		// MSIL generated. To see the MSIL, remove the comments and compile,
		// then run ILDASM and inspect the methods below.
		private EventSpy spy;
		public EventSpy(EventSpy s)
		{
			spy = s;
		}
		public void OnEventXxx(object o, EventArgs e)
		{
			spy.ReportEvent("EventXxx", o, e);
		}
#endif
	}
}


