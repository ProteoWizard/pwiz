// The .blib schema is defined twice for now: here in the port (Pwiz.Tools.BiblioSpec.BlibSchema,
// which BlibMaker writes with) and in pwiz_tools/Shared/BiblioSpec/BiblioSpec.IO (pwiz.BiblioSpec.IO.BlibSchema,
// which the BlibWriter Osprey and CarafeSharp share writes with). This test holds the two
// identical until BlibMaker itself writes through the Shared writer, when the port's copy goes.

using System.Reflection;
using SharedSchema = pwiz.BiblioSpec.IO.BlibSchema;

namespace Pwiz.Tools.BiblioSpec.Tests;

[TestClass]
public class BlibSchemaIdentity
{
    /// <summary>
    /// Every public static member of the port's schema has a member of the same name and the
    /// same value in the Shared schema: the version numbers, every CREATE statement, every
    /// index statement and name list, and the small-molecule columns.
    /// </summary>
    [TestMethod]
    public void SharedSchemaMatchesPortSchema()
    {
        var portType = typeof(BlibSchema);
        var sharedType = typeof(SharedSchema);
        var checkedMembers = 0;
        foreach (var field in portType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var shared = sharedType.GetField(field.Name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(shared, $"Shared BlibSchema has no field {field.Name}");
            AssertSameValue(field.Name, field.GetValue(null), shared.GetValue(null));
            checkedMembers++;
        }
        foreach (var property in portType.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            var shared = sharedType.GetProperty(property.Name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(shared, $"Shared BlibSchema has no property {property.Name}");
            AssertSameValue(property.Name, property.GetValue(null), shared.GetValue(null));
            checkedMembers++;
        }
        Assert.IsTrue(checkedMembers > 20, $"only {checkedMembers} members compared; the port's schema should hold more");

        // The small-molecule columns the port keeps on SmallMolMetadata, which the Shared schema
        // holds itself and splices into its RefSpectra statement.
        CollectionAssert.AreEqual(SmallMolMetadata.SqlColumns.Select(c => c.Name).ToList(),
            SharedSchema.SmallMoleculeColumns.Select(c => c.Name).ToList());
        CollectionAssert.AreEqual(SmallMolMetadata.SqlColumns.Select(c => c.Description).ToList(),
            SharedSchema.SmallMoleculeColumns.Select(c => c.Description).ToList());
        Assert.AreEqual(SmallMolMetadata.SqlColumnDeclarations(), SharedSchema.SmallMoleculeColumnDeclarations());
        Assert.AreEqual(SmallMolMetadata.SqlColumnNamesCsv(), SharedSchema.SmallMoleculeColumnNamesCsv());
    }

    private static void AssertSameValue(string name, object? port, object? shared)
    {
        if (port is IEnumerable<string> portList && shared is IEnumerable<string> sharedList)
            CollectionAssert.AreEqual(portList.ToList(), sharedList.ToList(), name);
        else
            Assert.AreEqual(port, shared, name);
    }
}
