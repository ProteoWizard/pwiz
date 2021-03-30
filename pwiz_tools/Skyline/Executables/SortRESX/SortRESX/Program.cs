using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SortRESX
{
	//
	// 0 command line parameters ==> input is from stdin and output is stdout.
	// 1 command line parameter  ==> input is a source .resx file (arg[0]) and output is stdout.
	// 2 command line parameters ==> input is a source .resx file (arg[0]) and output is to a target .resx file (arg[1])
	// The program reads the source and writes a sorted version of it to the output.
	//
	class Program
	{
		static void Main(string[] args)
		{
			XmlReader inputStream = null;

			if (args.Length > 2)
			{
				ShowHelp();
				return;
			}

			if (args.Length == 0) // Input resx is coming from stdin
			{
				try 
				{
					Stream s = Console.OpenStandardInput();
					inputStream = XmlReader.Create(s);
				}
				catch (Exception ex) 
				{
					Console.WriteLine("Error reading from stdin: {0}", ex.Message);
					return;
				}
			}
			else // Input resx is from file specified by first argument 
			{
				string arg0 = args[0].ToLower();
				if( arg0.StartsWith(@"/h") || arg0.StartsWith(@"/?"))
				{
					ShowHelp();
					return;
				}

				try
				{
					inputStream = XmlReader.Create(args[0]);
				}
				catch(Exception ex)
				{
					Console.WriteLine("Error opening file '{0}': {1}", args[0], ex.Message);
				}
			}
			try
			{
				// Create a linq XML document from the source.
				XDocument doc = XDocument.Load(inputStream);
				// Create a sorted version of the XML
				XDocument sortedDoc = SortDataByName(doc);
				// Save it to the target
				Console.OutputEncoding = Encoding.UTF8;
				if (args.Length == 2)
					sortedDoc.Save(args[1]);
				else
					sortedDoc.Save(Console.Out);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
			}
			return;
		}

		//
		// Use Linq to sort the elements.  The comment, schema, resheader, assembly, metadata, data appear in that order, 
		// with resheader, assembly, metadata and data elements sorted by name attribute.
		private static XDocument SortDataByName(XDocument resx)
		{
			return new XDocument(
				new XElement(resx.Root.Name,
					from comment in resx.Root.Nodes() where comment.NodeType == XmlNodeType.Comment select comment,
					from schema in resx.Root.Elements() where schema.Name.LocalName == "schema" select schema,
					from resheader in resx.Root.Elements("resheader") orderby (string) resheader.Attribute("name") select resheader,
					from assembly in resx.Root.Elements("assembly") orderby (string) assembly.Attribute("name") select assembly,
					from metadata in resx.Root.Elements("metadata") orderby (string)metadata.Attribute("name") select metadata,
					from data in resx.Root.Elements("data") orderby (string)data.Attribute("name") select data
				)
			);
		}

		//
		// Write invocation instructions to stderr.
		//
		private static void ShowHelp()
		{
			Console.Error.WriteLine(
			"0 arguments ==> Input from STDIN.  Output to STDOUT.\n" +
			"1 argument  ==> Input from specified .resx file.  Output to STDOUT.\n" +
			"2 arguments ==> Input from first specified .resx file.  Output to second specified .resx file.");
		}
	}
}
