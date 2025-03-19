/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Proteome
{
    public class AssociateProteinsResults : Immutable
    {
        public static readonly Producer<Parameters, AssociateProteinsResults> PRODUCER = new Producer();

        public class Parameters : Immutable
        {
            public Parameters(SrmDocument document)
            {
                Document = document;
            }

            public SrmDocument Document { get; }

            public string FastaFilePath { get; private set; }

            public Parameters ChangeFastaFilePath(string value)
            {
                return ChangeProp(ImClone(this), im => im.FastaFilePath = value);
            }

            public BackgroundProteome BackgroundProteome { get; private set; }

            public Parameters ChangeBackgroundProteome(BackgroundProteome value)
            {
                return ChangeProp(ImClone(this), im => im.BackgroundProteome = value);
            }

            public ProteinAssociation.SharedPeptides SharedPeptides { get; private set; }

            public Parameters ChangeSharedPeptides(ProteinAssociation.SharedPeptides value)
            {
                return ChangeProp(ImClone(this), im => im.SharedPeptides = value);
            }

            public ProteinAssociation.ParsimonySettings ParsimonySettings { get; private set; }

            public Parameters ChangeParsimonySettings(ProteinAssociation.ParsimonySettings value)
            {
                return ChangeProp(ImClone(this), im => im.ParsimonySettings = value);
            }

            public IrtStandard IrtStandard { get; private set; }

            public Parameters ChangeIrtStandard(IrtStandard value)
            {
                return ChangeProp(ImClone(this), im => im.IrtStandard = value);
            }

            public string DecoyGenerationMethod { get; private set; }

            public Parameters ChangeDecoyGenerationMethod(string value)
            {
                return ChangeProp(ImClone(this), im => im.DecoyGenerationMethod = value);
            }

            public double DecoysPerTarget { get; private set; }

            public Parameters ChangeDecoysPerTarget(double value)
            {
                return ChangeProp(ImClone(this), im => im.DecoysPerTarget = value);
            }

            protected bool Equals(Parameters other)
            {
                return ReferenceEquals(Document, other.Document) && FastaFilePath == other.FastaFilePath &&
                       Equals(BackgroundProteome, other.BackgroundProteome) &&
                       SharedPeptides == other.SharedPeptides && Equals(ParsimonySettings, other.ParsimonySettings) &&
                       Equals(IrtStandard, other.IrtStandard) && DecoyGenerationMethod == other.DecoyGenerationMethod &&
                       DecoysPerTarget.Equals(other.DecoysPerTarget);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Parameters)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = RuntimeHelpers.GetHashCode(Document);
                    hashCode = (hashCode * 397) ^ (FastaFilePath != null ? FastaFilePath.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (int)SharedPeptides;
                    hashCode = (hashCode * 397) ^ (ParsimonySettings != null ? ParsimonySettings.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (IrtStandard != null ? IrtStandard.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (DecoyGenerationMethod != null ? DecoyGenerationMethod.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ DecoysPerTarget.GetHashCode();
                    hashCode = (hashCode & 397) ^ (BackgroundProteome != null ? BackgroundProteome.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }


        public AssociateProteinsResults(Parameters parameters)
        {
            Params = parameters;
        }
        public Parameters Params { get; }
        public string ErrorMessage { get; private set; }
        public Exception ErrorException { get; private set; }

        public bool IsErrorResult
        {
            get
            {
                return ErrorException != null || !string.IsNullOrEmpty(ErrorMessage);
            }
        }

        public AssociateProteinsResults ChangeError(string errorMessage, Exception exception)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ErrorMessage = errorMessage ?? exception?.Message;
                im.ErrorException = exception;
            });
        }

        public ProteinAssociation ProteinAssociation { get; private set; }

        public AssociateProteinsResults ChangeProteinAssociation(ProteinAssociation value)
        {
            return ChangeProp(ImClone(this), im => im.ProteinAssociation = value);
        }

        public SrmDocument DocumentFinal { get; private set; }

        public AssociateProteinsResults ChangeDocumentFinal(SrmDocument value)
        {
            return ChangeProp(ImClone(this), im => im.DocumentFinal = value);
        }


        private class Producer : Producer<Parameters, AssociateProteinsResults>
        {
            public override AssociateProteinsResults ProduceResult(ProductionMonitor productionMonitor,
                Parameters parameter,
                IDictionary<WorkOrder, object> inputs)
            {
                var results = new AssociateProteinsResults(parameter);
                var stringSearch = inputs.Values.OfType<StringSearch>().First();
                var proteinAssociation =
                    new ProteinAssociation(parameter.Document, stringSearch);
                var longWaitBroker = new LongWaitBrokerImpl(productionMonitor);
                if (!string.IsNullOrEmpty(parameter.FastaFilePath))
                {
                    if (!File.Exists(parameter.FastaFilePath))
                    {
                        return results.ChangeError(string.Format(
                            Resources.ChromCacheBuilder_BuildNextFileInner_The_file__0__does_not_exist,
                            parameter.FastaFilePath), null);
                    }
                    try
                    {
                        proteinAssociation.UseFastaFile(parameter.FastaFilePath, longWaitBroker);
                    }
                    catch (Exception ex)
                    {
                        return results.ChangeError(TextUtil.LineSeparate(
                            Resources
                                .AssociateProteinsDlg_UseFastaFile_An_error_occurred_during_protein_association_,
                            ex.Message), ex);
                    }
                }

                else if (parameter.BackgroundProteome != null)
                {
                    proteinAssociation.UseBackgroundProteome(
                        parameter.BackgroundProteome, longWaitBroker);
                }
                else
                {
                    return results;
                }

                if (proteinAssociation.AssociatedProteins == null)
                {
                    return results;
                }

                var parsimony = parameter.ParsimonySettings;
                proteinAssociation.ApplyParsimonyOptions(parsimony.GroupProteins, parsimony.GeneLevelParsimony,
                    parsimony.FindMinimalProteinList, parsimony.RemoveSubsetProteins, parsimony.SharedPeptides,
                    parsimony.MinPeptidesPerProtein, longWaitBroker);
                var documentFinal =
                    proteinAssociation.CreateDocTree(parameter.Document, new SilentProgressMonitor());
                if (documentFinal != null && parameter.DecoysPerTarget != 0)
                {
                    var numDecoys = (int)Math.Round(parameter.DecoysPerTarget *
                                                    documentFinal.PeptideGroups.Sum(pepGroup =>
                                                        pepGroup.PeptideCount));
                    if (numDecoys > 0)
                    {
                        documentFinal = new RefinementSettings
                                { DecoysMethod = parameter.DecoyGenerationMethod, NumberOfDecoys = numDecoys }
                            .GenerateDecoys(documentFinal);
                    }
                }

                if (documentFinal != null && null != parameter.IrtStandard)
                {
                    documentFinal =
                        ImportPeptideSearch.AddStandardsToDocument(documentFinal, parameter.IrtStandard);
                }

                return results.ChangeProteinAssociation(proteinAssociation).ChangeDocumentFinal(documentFinal);
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
            {
                var document = parameter.Document;
                var peptideSequences = document.Peptides.Select(peptide => peptide.Target.Sequence).ToImmutable();
                yield return _stringSearchProducer.MakeWorkOrder(peptideSequences);
            }
        }

        private class LongWaitBrokerImpl : ILongWaitBroker
        {
            private ProductionMonitor _productionMonitor;
            public LongWaitBrokerImpl(ProductionMonitor productionMonitor)
            {
                _productionMonitor = productionMonitor;
            }

            public bool IsCanceled
            {
                get { return _productionMonitor.CancellationToken.IsCancellationRequested; }
            }

            public int ProgressValue
            {
                get
                {
                    return -1;
                }
                set
                {
                    _productionMonitor.SetProgress(value);
                }
            }

            public string Message { get; set; }
            public bool IsDocumentChanged(SrmDocument docOrig)
            {
                return false;
            }

            public System.Windows.Forms.DialogResult ShowDialog(Func<System.Windows.Forms.IWin32Window, System.Windows.Forms.DialogResult> show)
            {
                throw new InvalidOperationException();
            }

            public void SetProgressCheckCancel(int step, int totalSteps)
            {
                CancellationToken.ThrowIfCancellationRequested();
            }

            public CancellationToken CancellationToken
            {
                get { return _productionMonitor.CancellationToken; }
            }
        }
        private static readonly Producer<ImmutableList<string>, StringSearch> _stringSearchProducer = new StringSearchProducer();

        private class StringSearchProducer : Producer<ImmutableList<string>, StringSearch>
        {
            public override StringSearch ProduceResult(ProductionMonitor productionMonitor, ImmutableList<string> parameter, IDictionary<WorkOrder, object> inputs)
            {
                return new StringSearch(parameter, productionMonitor.CancellationToken);
            }
        }
    }
}