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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace pwiz.Skyline.Model.Proteome
{
    public class AssociateProteinsResults : Immutable
    {
        public static readonly Producer<Parameters, AssociateProteinsResults> PRODUCER = new ResultsProducer();

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


        private class ResultsProducer : Producer<Parameters, AssociateProteinsResults>
        {
            public override AssociateProteinsResults ProduceResult(ProductionMonitor productionMonitor,
                Parameters parameter,
                IDictionary<WorkOrder, object> inputs)
            {
                var preliminaryResults = inputs.Values.OfType<ParsimonyIndependentResults>().First();
                return ProduceResults(productionMonitor, parameter, preliminaryResults);
            }

            public AssociateProteinsResults ProduceResults(ProductionMonitor productionMonitor, Parameters parameter, ParsimonyIndependentResults parsimonyIndependentResults) 
            {
                var results = new AssociateProteinsResults(parameter).ChangeError(parsimonyIndependentResults.ErrorMessage, parsimonyIndependentResults.ErrorException);
                if (results.IsErrorResult)
                {
                    return results;
                }
                var proteinAssociation = parsimonyIndependentResults.GetProteinAssociation();
                if (proteinAssociation?.AssociatedProteins == null)
                {
                    return results;
                }

                var parsimony = parameter.ParsimonySettings;
                proteinAssociation.ApplyParsimonyOptions(parsimony.GroupProteins, parsimony.GeneLevelParsimony,
                    parsimony.FindMinimalProteinList, parsimony.RemoveSubsetProteins, parsimony.SharedPeptides,
                    parsimony.MinPeptidesPerProtein, new ProgressImpl(productionMonitor, 30, 60));
                var documentFinal =
                    proteinAssociation.CreateDocTree(parameter.Document, new ProgressImpl(productionMonitor, 60, 100));
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
                yield return PRELIMINARY_RESULTS_PRODUCER.MakeWorkOrder(new ParsimonyIndependentParameters(parameter.Document,
                    parameter.FastaFilePath, parameter.BackgroundProteome));
            }
        }

        private static readonly Producer<ParsimonyIndependentParameters, ParsimonyIndependentResults> PRELIMINARY_RESULTS_PRODUCER =
            Producer.FromFunction<ParsimonyIndependentParameters, ParsimonyIndependentResults>(ProduceParsimonyIndependentResults);


        private class ProgressImpl : ILongWaitBroker, IProgressMonitor
        {
            private ProductionMonitor _productionMonitor;
            private int _minProgress;
            private int _maxProgress;

            public ProgressImpl(ProductionMonitor productionMonitor, int min, int max)
            {
                _productionMonitor = productionMonitor;
                _minProgress = min;
                _maxProgress = max;
            }

            public bool IsCanceled
            {
                get { return _productionMonitor.CancellationToken.IsCancellationRequested; }
            }

            public int ProgressValue
            {
                get { return -1; }
                set
                {
                    _productionMonitor.SetProgress(GetPercentComplete(value));
                }
            }

            public string Message { get; set; }

            public bool IsDocumentChanged(SrmDocument docOrig)
            {
                return false;
            }

            public void SetProgressCheckCancel(int step, int totalSteps)
            {
                CancellationToken.ThrowIfCancellationRequested();
            }

            public CancellationToken CancellationToken
            {
                get { return _productionMonitor.CancellationToken; }
            }

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                _productionMonitor.SetProgress(GetPercentComplete(status.PercentComplete));
                return _productionMonitor.CancellationToken.IsCancellationRequested
                    ? UpdateProgressResponse.cancel
                    : UpdateProgressResponse.normal;
            }

            private int GetPercentComplete(int percentComplete)
            {
                return Math.Max(_minProgress,
                    Math.Min(_maxProgress,
                        (_maxProgress * percentComplete + _minProgress * (100 - percentComplete)) / 100));
            }

            public bool HasUI
            {
                get { return false; }
            }
        }

        private static readonly Producer<ImmutableList<string>, StringSearch> _stringSearchProducer =
            Producer.FromFunction<ImmutableList<string>, StringSearch>((productionMonitor, peptides) =>
                new StringSearch(peptides, productionMonitor.CancellationToken));

        private class ParsimonyIndependentParameters
        {
            public ParsimonyIndependentParameters(SrmDocument document, string fastaFilePath, BackgroundProteome backgroundProteome)
            {
                Document = document;
                FastaFilePath = fastaFilePath;
                BackgroundProteome = backgroundProteome;
            }

            public SrmDocument Document { get; }
            public string FastaFilePath { get; }
            public BackgroundProteome BackgroundProteome { get; }

            protected bool Equals(ParsimonyIndependentParameters other)
            {
                return ReferenceEquals(Document, other.Document) && FastaFilePath == other.FastaFilePath && Equals(BackgroundProteome, other.BackgroundProteome);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ParsimonyIndependentParameters)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = RuntimeHelpers.GetHashCode(Document);
                    hashCode = (hashCode * 397) ^ (FastaFilePath != null ? FastaFilePath.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (BackgroundProteome != null ? BackgroundProteome.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private class ParsimonyIndependentResults : Immutable
        {
            private ProteinAssociation _proteinAssociation;
            public ParsimonyIndependentResults ChangeProteinAssociation(ProteinAssociation proteinAssociation)
            {
                return ChangeProp(ImClone(this), im => im._proteinAssociation = proteinAssociation);
            }

            public ProteinAssociation GetProteinAssociation()
            {
                return _proteinAssociation?.Clone();
            }

            public string ErrorMessage { get; private set; }
            public Exception ErrorException { get; private set; }
            public ParsimonyIndependentResults ChangeError(string errorMessage, Exception exception)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.ErrorMessage = errorMessage ?? exception?.Message;
                    im.ErrorException = exception;
                });
            }
        }

        private static ParsimonyIndependentResults ProduceParsimonyIndependentResults(ProductionMonitor productionMonitor, ParsimonyIndependentParameters parameter)
        {
            var document = parameter.Document;
            var stringSearch = new StringSearch(document.Peptides.Select(peptide => peptide.Target.Sequence));
            var proteinAssociation =
                new ProteinAssociation(parameter.Document, stringSearch);
            var results = new ParsimonyIndependentResults();
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
                    proteinAssociation.UseFastaFile(parameter.FastaFilePath, new ProgressImpl(productionMonitor, 0, 30));
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
                    parameter.BackgroundProteome, new ProgressImpl(productionMonitor, 0, 30));
            }
            else
            {
                return results;
            }

            return results.ChangeProteinAssociation(proteinAssociation);
        }
    }
}