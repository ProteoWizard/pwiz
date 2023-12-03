using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class ConsoleAddAnnotationsTest : AbstractUnitTestEx
    {
        private const string ANNOTATION_VALUES = "Great,Good,Potentially,Bad";
        private const string INVALID_VALUE = "-la";
        private const string ANNOTATION_NAME = "Peptide quality";
        private const string ANNOTATION_TARGETS = "molecule, replicate";
        private const string ANNOTATION_TYPE = "value_list";
        private const string INVALID_TARGETS_LIST = ANNOTATION_TARGETS + INVALID_VALUE;
        
        [TestMethod]
        public void ConsoleAddAnnotationsFromArgumentsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleAddAnnotationsTest.zip");
            var newDocumentPath = TestFilesDir.GetTestPath("out.sky");
            var annotationValuesArray = ANNOTATION_VALUES.Split(',');
            // Test define (from arguments)
            var output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-name=" + ANNOTATION_NAME, // Name the annotation
                "--annotation-targets=" + ANNOTATION_TARGETS, // Specify the targets
                "--annotation-type=" + ANNOTATION_TYPE, // Specify the type
                "--annotation-values=" + ANNOTATION_VALUES, // Specify the values
                "--save"
            );
            var logMessage = new DetailLogMessage(LogLevel.all_info, MessageType.added_to,
                SrmDocument.DOCUMENT_TYPE.proteomic,
                string.Empty, false,
                "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                "\"Peptide quality\"");
            var expectedText = logMessage.ToString();
            CommandLineTest.CheckRunCommandOutputContains(string.Format(
                    expectedText, ANNOTATION_NAME),
                output);
            // Assert that the document has the correct number of annotations
            AssertDocumentAnnotationCount(newDocumentPath, 1);
            // Assert that the definition matches the one we defined
            AssertAnnotationInDocumentAndSettings(newDocumentPath,
                ANNOTATION_NAME,
                AnnotationDef.AnnotationTargetSet.OfValues(AnnotationDef.AnnotationTarget.peptide, AnnotationDef.AnnotationTarget.replicate),
                AnnotationDef.AnnotationType.value_list,
                annotationValuesArray);
            // Test default behavior of resolving environment conflicts through overwriting
            output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-name=" + ANNOTATION_NAME, // Name the annotation
                "--annotation-targets=" + ANNOTATION_TARGETS, // Specify the targets
                "--annotation-type=" + ANNOTATION_TYPE, // Specify the type
                "--annotation-values=" + ANNOTATION_VALUES, // Specify the values
                "--save"
            );
            CommandLineTest.CheckRunCommandOutputContains(string.Format(
                    Resources.CommandLine_SetAnnotations_Warning__The_annotation___0___was_overwritten_, ANNOTATION_NAME),
                output);
            // Test resolving conflicts through skipping
            output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite",
                "--annotation-name=" + ANNOTATION_NAME, // Name the annotation
                "--annotation-targets=" + ANNOTATION_TARGETS, // Specify the targets
                "--annotation-type=" + ANNOTATION_TYPE, // Specify the type
                "--annotation-values=" + ANNOTATION_VALUES, // Specify the values
                "--annotation-conflict-resolution=" + "skip", // Skip conflicting annotations
                "--save"
            );
            CommandLineTest.CheckRunCommandOutputContains(string.Format(
                Resources.CommandLine_SetAnnotations_Warning__Skipping_annotation___0___due_to_a_name_conflict_
                , ANNOTATION_NAME), output);
            // Test resolving conflicts through overwriting (using the annotation-conflict-resolution argument)
            output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite",
                "--annotation-name=" + ANNOTATION_NAME, // Name the annotation
                "--annotation-targets=" + ANNOTATION_TARGETS, // Specify the targets
                "--annotation-type=" + ANNOTATION_TYPE, // Specify the type
                "--annotation-values=" + ANNOTATION_VALUES, // Specify the values
                "--annotation-conflict-resolution=" + "overwrite", // Overwrite conflicting annotations
                "--save"
            );
            CommandLineTest.CheckRunCommandOutputContains(string.Format(
                Resources.CommandLine_SetAnnotations_Warning__The_annotation___0___was_overwritten_
                , ANNOTATION_NAME), output);
            // Test specifying an annotation name without any other arguments. This should find the annotation in the environment
            // and add it to the document
            output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-name=" + ANNOTATION_NAME, // Specify an annotation we added to the environment in a previous step
                "save"
            );
            CommandLineTest.CheckRunCommandOutputContains(
                string.Format(
                    expectedText, ANNOTATION_NAME),
                output);
            // Assert that the document has the correct number of annotations
            AssertDocumentAnnotationCount(newDocumentPath, 1);
            // Assert that the definition matches the one we defined
            AssertAnnotationInDocumentAndSettings(newDocumentPath,
                ANNOTATION_NAME,
                AnnotationDef.AnnotationTargetSet.OfValues(AnnotationDef.AnnotationTarget.peptide, AnnotationDef.AnnotationTarget.replicate),
                AnnotationDef.AnnotationType.value_list,
                annotationValuesArray);
        }

        [TestMethod]
        public void ConsoleAddInvalidTargetListTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleAddAnnotationsTest.zip");
            var newDocumentPath = TestFilesDir.GetTestPath("out.sky");
            // Test error (invalid annotation-targets value)
            var output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-name=" + ANNOTATION_NAME, // Name the annotation
                "--annotation-targets=" + INVALID_TARGETS_LIST // Input an invalid list of targets
            );
            CommandLineTest.CheckRunCommandOutputContains(
                new CommandArgs.ValueInvalidAnnotationTargetListException(
                    CommandArgs.ARG_ADD_ANNOTATIONS_TARGETS, INVALID_TARGETS_LIST,
                    CommandArgs.ANNOTATION_TARGET_LIST_VALUE.Invoke()).Message,
                output);
        }

        [TestMethod]
        public void ConsoleInvalidValueListArgumentsTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleAddAnnotationsTest.zip");
            var newDocumentPath = TestFilesDir.GetTestPath("out.sky");
            // Test error (specifying a value_list type annotation without providing a list of values through
            // --annotation-values)
            var output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-name=" + ANNOTATION_NAME, // Name the annotation
                "--annotation-targets=" + ANNOTATION_TARGETS, // Specify the targets
                "--annotation-type=" + "value_list", // Specify the type
                "--save"
            );
            CommandLineTest.CheckRunCommandOutputContains(
                string.Format(
                    Resources.CommandLine_AddAnnotationsFromArguments_Error__Cannot_add_a__0__type_annotation_without_providing_a_list_values_of_through__1__,
                    AnnotationDef.AnnotationType.value_list.ToString(), CommandArgs.ARG_ADD_ANNOTATIONS_VALUES.ArgumentText), output);
        }

        [TestMethod]
        public void ConsoleInvalidAnnotationTypeTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleAddAnnotationsTest.zip");
            var newDocumentPath = TestFilesDir.GetTestPath("out.sky");
            // Test error (invalid annotation-type value)
            var output = RunCommand("--new=" + newDocumentPath, // Create a new document
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-name=" + ANNOTATION_NAME, // Name the annotation
                "--annotation-targets=" + ANNOTATION_TARGETS, // Specify the target
                "--annotation-type=" + INVALID_VALUE // Specify an invalid type value
            );
            CommandLineTest.CheckRunCommandOutputContains(string.Format(
                Resources.ValueInvalidException_ValueInvalidException_The_value___0___is_not_valid_for_the_argument__1___Use_one_of__2_,
                INVALID_VALUE,
                "--annotation-type",
                string.Join(@", ", ListPropertyType.ListPropertyTypes().Select(c =>
                    c.AnnotationType.ToString()).ToArray())), output);
        }

        [TestMethod]
        public void ConsoleAddXmlDefinedAnnotationTest()
        {
            var annotationValuesArray = ANNOTATION_VALUES.Split(',');
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleAddAnnotationsTest.zip");
            var annotationsXml = TestFilesDir.GetTestPath("Annotations.xml");
            var annotationsXmlBadFormatting = TestFilesDir.GetTestPath("AnnotationsIncorrectFormatting.xml");
            var newDocumentPath = TestFilesDir.GetTestPath("out.sky");
            // Test define (from .xml file)
            var output = RunCommand("--new=" + newDocumentPath, // Create a document (without annotations)
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-file=" + annotationsXml, // Specify file path
                "save"
            );
            CommandLineTest.CheckRunCommandOutputContains(string.Format(
                    Resources.CommandLine_AddAnnotations_Annotations_successfully_defined_from_file__0__, annotationsXml)
                , output);
            // Assert that the document has the correct number of annotations
            AssertDocumentAnnotationCount(newDocumentPath, 2);
            // Assert that the annotations in the .xml file appear in the document
            AssertAnnotationInDocumentAndSettings(newDocumentPath,
                "Peptide Quality",
                AnnotationDef.AnnotationTargetSet.OfValues(AnnotationDef.AnnotationTarget.peptide),
                AnnotationDef.AnnotationType.value_list,
                annotationValuesArray);
            AssertAnnotationInDocumentAndSettings(newDocumentPath,
                "BioReplicate",
                AnnotationDef.AnnotationTargetSet.OfValues(AnnotationDef.AnnotationTarget.replicate),
                AnnotationDef.AnnotationType.number,
                Array.Empty<string>());
            // Test error (.xml file with incorrect formatting)
            output = RunCommand("--new=" + newDocumentPath, // Create a document (without annotations)
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-file=" + annotationsXmlBadFormatting, // Specify file path
                "save"
            );
            CommandLineTest.CheckRunCommandOutputContains(string.Format(
                    Resources.CommandLine_AddAnnotations_Error__Unable_to_read_annotations_from_file__0__, annotationsXmlBadFormatting),
                output);
        }

        [TestMethod]
        public void ConsoleNonexistentAnnotationErrorTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\ConsoleAddAnnotationsTest.zip");
            var newDocumentPath = TestFilesDir.GetTestPath("out.sky");
            // Test error (specifying an annotation that does not exist in the environment)
            var output = RunCommand("--new=" + newDocumentPath, // Create a document (without annotations)
                "--overwrite", // Overwrite, as the file may already exist in the bin
                "--annotation-name=" + INVALID_VALUE // Specify an annotation that does not exist
            );
            CommandLineTest.CheckRunCommandOutputContains(string.Format(
                Resources.CommandLine_AddAnnotationFromEnvironment_Error__Cannot_add_new_annotation___0___without_providing_at_least_one_target_through__1__,
                INVALID_VALUE, CommandArgs.ARG_ADD_ANNOTATIONS_TARGETS.ArgumentText), output);
        }

        private static void AssertAnnotationInDocumentAndSettings(string documentPath, string annotationName,
            AnnotationDef.AnnotationTargetSet annotationTargets, AnnotationDef.AnnotationType annotationType, string[] annotationValues)
        {
            var doc = ResultsUtil.DeserializeDocument(documentPath);
            var annotationDefs = doc.Settings.DataSettings.AnnotationDefs;
            var annotationInDocument = annotationDefs.Any(def =>
                def.Name.Equals(annotationName) &&
                def.AnnotationTargets.Equals(annotationTargets) &&
                def.Type.Equals(annotationType) &&
                def.Items.SequenceEqual(annotationValues));

            Assert.IsTrue(annotationInDocument);

            var annotationInSettings = Settings.Default.AnnotationDefList.Any(def =>
                def.Name.Equals(annotationName) &&
                def.AnnotationTargets.Equals(annotationTargets) &&
                def.Type.Equals(annotationType) &&
                def.Items.SequenceEqual(annotationValues));

            Assert.IsTrue(annotationInSettings);
        }

        private static void AssertDocumentAnnotationCount(string documentPath, int annotationCount)
        {
            Assert.AreEqual(annotationCount, ResultsUtil.DeserializeDocument(documentPath).Settings.DataSettings.AnnotationDefs.Count);
        }
    }

}