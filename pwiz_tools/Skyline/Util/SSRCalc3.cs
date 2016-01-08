using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Util
{
    /**
    /*
    /* reference, O. V. Krokhin, R. Craig, V. Spicer, W. Ens, K. G. Standing, R. C. Beavis, J. A. Wilkins
    /* An improved model for prediction of retention times of tryptic peptides in ion-pair reverse-phase HPLC:
    /* its application to protein peptide mapping by off-line HPLC-MALDI MS
    /* Molecular and Cellular Proteomics 2004 Sep;3(9):908-19.
    /* URL, http://hs2.proteome.ca/SSRCalc/SSRCalc.html
    /*
    /*
    /* These subroutines are based on web version SSRCalculator of the Copyright holder listed as in the following:
    /*
    /* Version 3.0   2005.02.28
    /* Copyright (c) 2005 John Wilkins
    /* Sequence Specific Retention Calculator
    /* Authors: Oleg Krokhin, Vic Spicer, John Cortens
     */

    /* Translated from perl to C, Ted Holzman FHCRC, 6/2006  */
    /* Retranslated from C to Java, Ted Holzman FHCRC 7/2006 */
    /* Translated from Java to C#, Brendan MacLean UW 10/2008 */
    /* NB: This is a version 0.1 direct translation.
    /*     An attempt has been made to keep function names, variable names, and algorithms
    /*     as close as possible to the original perl.
     */


// ReSharper disable InconsistentNaming
// ReSharper disable CharImplicitlyConvertedToNumeric
    public class SSRCalc3 : IRetentionScoreCalculator
    {
        /* Lookup table data.  These are translations of the .h table in C which is a    */
        /* translation of the ReadParmFile perl routine.  This does not read a parameter */
        /* file; it makes static initializers for the parameter data.                    */

        public const String VERSION = "Krokhin,3.0"; // Not L10N

        private class AAParams
        {
            //Retention Factors
            public double RC { get; private set; }
            public double RC1 { get; private set; }
            public double RC2 { get; private set; }
            public double RCN { get; private set; }
            public double RCN2 { get; private set; }
            //Short peptide retention factors
            public double RCS { get; private set; }
            public double RC1S { get; private set; }
            public double RC2S { get; private set; }
            public double RCNS { get; private set; }
            public double RCN2S { get; private set; }

            public double UndKRH { get; private set; } //Factors for aa's near undigested KRH
            public double AMASS { get; private set; }  //aa masses in Daltons
            //isoelectric factors
            public double CT { get; private set; }
            public double NT { get; private set; }
            public double PK { get; private set; }
            //helicity2 bascore & connector multiplier
            public double H2BASCORE { get; private set; }
            public double H2CMULT { get; private set; }

            public AAParams(
               double rc, double rc1, double rc2, double rcn, double rcn2,
               double rcs, double rc1s, double rc2s, double rcns, double rcn2s,
               double undkrh, double amass,
               double ct, double nt, double pk,
               double h2bascore, double h2cmult
            )
            {
                RC = rc; RC1 = rc1; RC2 = rc2; RCN = rcn; RCN2 = rcn2;
                RCS = rcs; RC1S = rc1s; RC2S = rc2s; RCNS = rcns; RCN2S = rcn2s;
                UndKRH = undkrh; AMASS = amass;
                CT = ct; NT = nt; PK = pk;
                H2BASCORE = h2bascore; H2CMULT = h2cmult;
            }
        }

        public IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides, out int minCount)
        {
            minCount = 0;
            return peptides;
        }

        public IEnumerable<string> GetStandardPeptides(IEnumerable<string> peptides)
        {
            return new string[] {};
        }

        public RetentionScoreCalculatorSpec Initialize(IProgressMonitor loadMonitor)
        {
            return null;
        }

        private static readonly CLUSTCOMB_List CLUSTCOMB = new CLUSTCOMB_List();
        private static readonly Dictionary<string, double> HlxScore4 = new Dictionary<string, double>();
        private static readonly Dictionary<string, double> HlxScore5 = new Dictionary<string, double>();
        private static readonly Dictionary<string, double> HlxScore6 = new Dictionary<string, double>();

        private sealed class CLUSTCOMB_List : List<KeyValuePair<Regex, double>>
        {
            public void Add(string pattern, double value)
            {
                Add(new KeyValuePair<Regex, double>(new Regex(pattern), value));
            }
        }

        static SSRCalc3()
        {

            /*
              Translator1 note:  For the Java version we are prepending and appending 0s to the "pick" (key) column.  This
              is done dynamically and repeatedly in the perl code.  As far as I can tell, pick is never used
              without the surrounding 0s.
            */

            // ReSharper disable NonLocalizedString
            CLUSTCOMB.Add("0110", 0.3);
            CLUSTCOMB.Add("0150", 0.4);
            CLUSTCOMB.Add("0510", 0.4);
            CLUSTCOMB.Add("0550", 1.3);
            CLUSTCOMB.Add("01110", 0.5);
            CLUSTCOMB.Add("01150", 0.7);
            CLUSTCOMB.Add("01510", 0.7);
            CLUSTCOMB.Add("01550", 2.1);
            CLUSTCOMB.Add("05110", 0.7);
            CLUSTCOMB.Add("05150", 2.1);
            CLUSTCOMB.Add("05510", 2.1);
            CLUSTCOMB.Add("05550", 2.8);
            CLUSTCOMB.Add("011110", 0.7);
            CLUSTCOMB.Add("011150", 0.9);
            CLUSTCOMB.Add("011510", 0.9);
            CLUSTCOMB.Add("011550", 2.2);
            CLUSTCOMB.Add("015110", 0.9);
            CLUSTCOMB.Add("015150", 2.2);
            CLUSTCOMB.Add("015510", 0.9);
            CLUSTCOMB.Add("015550", 3.0);
            CLUSTCOMB.Add("051110", 0.9);
            CLUSTCOMB.Add("051150", 2.2);
            CLUSTCOMB.Add("051510", 2.2);
            CLUSTCOMB.Add("051550", 3.0);
            CLUSTCOMB.Add("055110", 2.2);
            CLUSTCOMB.Add("055150", 3.0);
            CLUSTCOMB.Add("055510", 3.0);
            CLUSTCOMB.Add("055550", 3.5);
            CLUSTCOMB.Add("0111110", 0.9);
            CLUSTCOMB.Add("0111150", 1.0);
            CLUSTCOMB.Add("0111510", 1.0);
            CLUSTCOMB.Add("0111550", 2.3);
            CLUSTCOMB.Add("0115110", 1.0);
            CLUSTCOMB.Add("0115150", 2.3);
            CLUSTCOMB.Add("0115510", 2.3);
            CLUSTCOMB.Add("0115550", 3.1);
            CLUSTCOMB.Add("0151110", 1.0);
            CLUSTCOMB.Add("0151150", 2.3);
            CLUSTCOMB.Add("0151510", 2.3);
            CLUSTCOMB.Add("0151550", 3.1);
            CLUSTCOMB.Add("0155110", 2.3);
            CLUSTCOMB.Add("0155150", 3.1);
            CLUSTCOMB.Add("0155510", 3.1);
            CLUSTCOMB.Add("0155550", 3.6);
            CLUSTCOMB.Add("0511110", 1.0);
            CLUSTCOMB.Add("0511150", 2.3);
            CLUSTCOMB.Add("0511510", 2.3);
            CLUSTCOMB.Add("0511550", 3.1);
            CLUSTCOMB.Add("0515110", 3.6);
            CLUSTCOMB.Add("0515150", 2.3);
            CLUSTCOMB.Add("0515510", 3.1);
            CLUSTCOMB.Add("0515550", 3.6);
            CLUSTCOMB.Add("0551110", 2.3);
            CLUSTCOMB.Add("0551150", 3.1);
            CLUSTCOMB.Add("0551510", 3.1);
            CLUSTCOMB.Add("0551550", 3.6);
            CLUSTCOMB.Add("0555110", 3.1);
            CLUSTCOMB.Add("0555150", 3.6);
            CLUSTCOMB.Add("0555510", 3.6);
            CLUSTCOMB.Add("0555550", 4.0);
            CLUSTCOMB.Add("01111110", 1.1);
            CLUSTCOMB.Add("01111150", 1.7);
            CLUSTCOMB.Add("01111510", 1.7);
            CLUSTCOMB.Add("01111550", 2.5);
            CLUSTCOMB.Add("01115110", 1.7);
            CLUSTCOMB.Add("01115150", 2.5);
            CLUSTCOMB.Add("01115510", 2.5);
            CLUSTCOMB.Add("01115550", 3.3);
            CLUSTCOMB.Add("01151110", 1.7);
            CLUSTCOMB.Add("01151150", 2.5);
            CLUSTCOMB.Add("01151510", 2.5);
            CLUSTCOMB.Add("01151550", 3.3);
            CLUSTCOMB.Add("01155110", 2.5);
            CLUSTCOMB.Add("01155150", 3.3);
            CLUSTCOMB.Add("01155510", 3.3);
            CLUSTCOMB.Add("01155550", 3.7);
            CLUSTCOMB.Add("01511110", 1.7);
            CLUSTCOMB.Add("01511150", 2.5);
            CLUSTCOMB.Add("01511510", 2.5);
            CLUSTCOMB.Add("01511550", 3.3);
            CLUSTCOMB.Add("01515110", 2.5);
            CLUSTCOMB.Add("01515150", 3.3);
            CLUSTCOMB.Add("01515510", 3.3);
            CLUSTCOMB.Add("01515550", 3.7);
            CLUSTCOMB.Add("01551110", 2.5);
            CLUSTCOMB.Add("01551150", 3.3);
            CLUSTCOMB.Add("01551510", 3.3);
            CLUSTCOMB.Add("01551550", 3.7);
            CLUSTCOMB.Add("01555110", 3.3);
            CLUSTCOMB.Add("01555150", 3.7);
            CLUSTCOMB.Add("01555510", 3.7);
            CLUSTCOMB.Add("01555550", 4.1);
            CLUSTCOMB.Add("05111110", 1.7);
            CLUSTCOMB.Add("05111150", 2.5);
            CLUSTCOMB.Add("05111510", 2.5);
            CLUSTCOMB.Add("05111550", 3.3);
            CLUSTCOMB.Add("05115110", 2.5);
            CLUSTCOMB.Add("05115150", 3.3);
            CLUSTCOMB.Add("05115510", 3.3);
            CLUSTCOMB.Add("05115550", 3.7);
            CLUSTCOMB.Add("05151110", 2.5);
            CLUSTCOMB.Add("05151150", 3.3);
            CLUSTCOMB.Add("05151510", 3.3);
            CLUSTCOMB.Add("05151550", 3.7);
            CLUSTCOMB.Add("05155110", 3.3);
            CLUSTCOMB.Add("05155150", 3.7);
            CLUSTCOMB.Add("05155510", 3.7);
            CLUSTCOMB.Add("05155550", 4.1);
            CLUSTCOMB.Add("05511110", 2.5);
            CLUSTCOMB.Add("05511150", 3.3);
            CLUSTCOMB.Add("05511510", 3.3);
            CLUSTCOMB.Add("05511550", 3.7);
            CLUSTCOMB.Add("05515110", 3.3);
            CLUSTCOMB.Add("05515150", 3.7);
            CLUSTCOMB.Add("05515510", 3.7);
            CLUSTCOMB.Add("05515550", 4.1);
            CLUSTCOMB.Add("05551110", 3.3);
            CLUSTCOMB.Add("05551150", 3.7);
            CLUSTCOMB.Add("05551510", 3.7);
            CLUSTCOMB.Add("05551550", 4.1);
            CLUSTCOMB.Add("05555110", 3.7);
            CLUSTCOMB.Add("05555150", 4.1);
            CLUSTCOMB.Add("05555510", 4.1);
            CLUSTCOMB.Add("05555550", 4.5);

            HlxScore4.Add("XXUX", 0.8);
            HlxScore4.Add("XZOX", 0.8);
            HlxScore4.Add("XUXX", 0.8);
            HlxScore4.Add("XXOX", 0.7);
            HlxScore4.Add("XOXX", 0.7);
            HlxScore4.Add("XZUX", 0.7);
            HlxScore4.Add("XXOZ", 0.7);
            HlxScore4.Add("ZXOX", 0.7);
            HlxScore4.Add("XOZZ", 0.7);
            HlxScore4.Add("ZOXX", 0.7);
            HlxScore4.Add("ZOZX", 0.7);
            HlxScore4.Add("ZUXX", 0.7);
            HlxScore4.Add("ZXUX", 0.5);
            HlxScore4.Add("XOZX", 0.5);
            HlxScore4.Add("XZOZ", 0.5);
            HlxScore4.Add("XUZX", 0.5);
            HlxScore4.Add("ZZOX", 0.2);
            HlxScore4.Add("ZXOZ", 0.2);
            HlxScore4.Add("ZOXZ", 0.2);
            HlxScore4.Add("XOXZ", 0.2);
            HlxScore4.Add("ZZUZ", 0.2);
            HlxScore4.Add("XUXZ", 0.2);
            HlxScore4.Add("ZUXZ", 0.2);
            HlxScore4.Add("XZUZ", 0.2);
            HlxScore4.Add("XUZZ", 0.2);
            HlxScore4.Add("ZXUZ", 0.2);
            HlxScore4.Add("ZOZZ", 0.2);
            HlxScore4.Add("ZZOZ", 0.2);
            HlxScore4.Add("ZZUX", 0.2);
            HlxScore4.Add("ZUZX", 0.2);
            HlxScore4.Add("XXUZ", 0.2);
            HlxScore4.Add("ZUZZ", 0.2);

            HlxScore5.Add("XXOXX", 3.75);
            HlxScore5.Add("XXOXZ", 3.75);
            HlxScore5.Add("XXOZX", 3.75);
            HlxScore5.Add("XZOXX", 3.75);
            HlxScore5.Add("ZXOXX", 3.75);
            HlxScore5.Add("XXOZZ", 2.7);
            HlxScore5.Add("XZOXZ", 2.7);
            HlxScore5.Add("XZOZX", 2.7);
            HlxScore5.Add("ZXOXZ", 2.7);
            HlxScore5.Add("ZXOZX", 2.7);
            HlxScore5.Add("ZZOXX", 2.7);
            HlxScore5.Add("ZXOZZ", 1.3);
            HlxScore5.Add("XZOZZ", 1.3);
            HlxScore5.Add("ZZOXZ", 1.3);
            HlxScore5.Add("ZZOZX", 1.3);
            HlxScore5.Add("ZZOZZ", 1.3);
            HlxScore5.Add("XXUXX", 3.75);
            HlxScore5.Add("XXUXZ", 3.75);
            HlxScore5.Add("XXUZX", 3.75);
            HlxScore5.Add("XZUXX", 3.75);
            HlxScore5.Add("ZXUXX", 3.75);
            HlxScore5.Add("XXUZZ", 1.1);
            HlxScore5.Add("XZUXZ", 1.1);
            HlxScore5.Add("XZUZX", 1.1);
            HlxScore5.Add("ZXUZX", 1.1);
            HlxScore5.Add("ZXUXZ", 1.1);
            HlxScore5.Add("ZZUXX", 1.1);
            HlxScore5.Add("XZUZZ", 1.3);
            HlxScore5.Add("ZXUZZ", 1.3);
            HlxScore5.Add("ZZUXZ", 1.3);
            HlxScore5.Add("ZZUZX", 1.3);
            HlxScore5.Add("ZZUZZ", 1.3);
            HlxScore5.Add("XXOOX", 1.25);
            HlxScore5.Add("ZXOOX", 1.25);
            HlxScore5.Add("XZOOX", 1.25);
            HlxScore5.Add("XOOXX", 1.25);
            HlxScore5.Add("XOOXZ", 1.25);
            HlxScore5.Add("XOOZX", 1.25);
            HlxScore5.Add("XXOOZ", 1.25);
            HlxScore5.Add("ZXOOZ", 1.25);
            HlxScore5.Add("XZOOZ", 1.25);
            HlxScore5.Add("ZZOOX", 1.25);
            HlxScore5.Add("ZZOOZ", 1.25);
            HlxScore5.Add("ZOOXX", 1.25);
            HlxScore5.Add("ZOOXZ", 1.25);
            HlxScore5.Add("ZOOZX", 1.25);
            HlxScore5.Add("XOOZZ", 1.25);
            HlxScore5.Add("ZOOZZ", 1.25);
            HlxScore5.Add("XXOUX", 1.25);
            HlxScore5.Add("ZXOUX", 1.25);
            HlxScore5.Add("XXUOX", 1.25);
            HlxScore5.Add("ZXUOX", 1.25);
            HlxScore5.Add("XOUXX", 1.25);
            HlxScore5.Add("XOUXZ", 1.25);
            HlxScore5.Add("XUOXX", 1.25);
            HlxScore5.Add("XUOXZ", 1.25);
            HlxScore5.Add("XXOUZ", 0.75);
            HlxScore5.Add("ZXOUZ", 0.75);
            HlxScore5.Add("XZOUX", 0.75);
            HlxScore5.Add("XZOUZ", 0.75);
            HlxScore5.Add("ZZOUX", 0.75);
            HlxScore5.Add("ZZOUZ", 0.75);
            HlxScore5.Add("XXUOZ", 0.75);
            HlxScore5.Add("ZXUOZ", 0.75);
            HlxScore5.Add("XZUOX", 0.75);
            HlxScore5.Add("XZUOZ", 0.75);
            HlxScore5.Add("ZZUOX", 0.75);
            HlxScore5.Add("ZZUOZ", 0.75);
            HlxScore5.Add("ZOUXX", 0.75);
            HlxScore5.Add("ZOUXZ", 0.75);
            HlxScore5.Add("XOUZX", 0.75);
            HlxScore5.Add("ZOUZX", 0.75);
            HlxScore5.Add("XOUZZ", 0.75);
            HlxScore5.Add("ZOUZZ", 0.75);
            HlxScore5.Add("ZUOXX", 0.75);
            HlxScore5.Add("ZUOXZ", 0.75);
            HlxScore5.Add("XUOZX", 0.75);
            HlxScore5.Add("ZUOZX", 0.75);
            HlxScore5.Add("XUOZZ", 0.75);
            HlxScore5.Add("ZUOZZ", 0.75);
            HlxScore5.Add("XUUXX", 1.25);
            HlxScore5.Add("XXUUX", 1.25);
            HlxScore5.Add("XXUUZ", 0.6);
            HlxScore5.Add("ZXUUX", 0.6);
            HlxScore5.Add("ZXUUZ", 0.6);
            HlxScore5.Add("XZUUX", 0.6);
            HlxScore5.Add("XZUUZ", 0.6);
            HlxScore5.Add("ZZUUX", 0.6);
            HlxScore5.Add("ZZUUZ", 0.6);
            HlxScore5.Add("ZUUXX", 0.6);
            HlxScore5.Add("XUUXZ", 0.6);
            HlxScore5.Add("ZUUXZ", 0.6);
            HlxScore5.Add("XUUZX", 0.6);
            HlxScore5.Add("ZUUZX", 0.6);
            HlxScore5.Add("XUUZZ", 0.6);
            HlxScore5.Add("ZUUZZ", 0.6);

            HlxScore6.Add("XXOOXX", 3.0);
            HlxScore6.Add("XXOOXZ", 3.0);
            HlxScore6.Add("ZXOOXX", 3.0);
            HlxScore6.Add("ZXOOXZ", 3.0);
            HlxScore6.Add("XXOUXX", 3.0);
            HlxScore6.Add("XXOUXZ", 3.0);
            HlxScore6.Add("XXUOXX", 3.0);
            HlxScore6.Add("XXUOXZ", 3.0);
            HlxScore6.Add("ZXUOXX", 3.0);
            HlxScore6.Add("ZXOUXX", 3.0);
            HlxScore6.Add("XXOOZX", 1.6);
            HlxScore6.Add("XXOOZZ", 1.6);
            HlxScore6.Add("XZOOXX", 1.6);
            HlxScore6.Add("XZOOXZ", 1.6);
            HlxScore6.Add("XZOOZX", 1.6);
            HlxScore6.Add("XZOOZZ", 1.6);
            HlxScore6.Add("ZXOOZX", 1.6);
            HlxScore6.Add("ZXOOZZ", 1.6);
            HlxScore6.Add("ZZOOXX", 1.6);
            HlxScore6.Add("ZZOOXZ", 1.6);
            HlxScore6.Add("ZXOUXZ", 1.6);
            HlxScore6.Add("XZUOXX", 1.6);
            HlxScore6.Add("ZXUOXZ", 1.6);
            HlxScore6.Add("ZZOOZX", 1.5);
            HlxScore6.Add("ZZOOZZ", 1.5);
            HlxScore6.Add("XXOUZX", 1.5);
            HlxScore6.Add("XXOUZZ", 1.5);
            HlxScore6.Add("XZOUXX", 1.5);
            HlxScore6.Add("XZOUXZ", 1.5);
            HlxScore6.Add("ZXOUZX", 1.5);
            HlxScore6.Add("ZXOUZZ", 1.5);
            HlxScore6.Add("ZZOUXX", 1.5);
            HlxScore6.Add("ZZOUXZ", 1.5);
            HlxScore6.Add("XXUOZX", 1.5);
            HlxScore6.Add("XXUOZZ", 1.5);
            HlxScore6.Add("XZUOXZ", 1.5);
            HlxScore6.Add("ZXUOZX", 1.5);
            HlxScore6.Add("ZXUOZZ", 1.5);
            HlxScore6.Add("ZZUOXX", 1.5);
            HlxScore6.Add("ZZUOXZ", 1.5);
            HlxScore6.Add("ZZUOZX", 1.25);
            HlxScore6.Add("ZZUOZZ", 1.25);
            HlxScore6.Add("ZZOUZX", 1.25);
            HlxScore6.Add("ZZOUZZ", 1.25);
            HlxScore6.Add("XZOUZX", 1.25);
            HlxScore6.Add("XZOUZZ", 1.25);
            HlxScore6.Add("XZUOZX", 1.25);
            HlxScore6.Add("XZUOZZ", 1.25);
            HlxScore6.Add("XXUUXX", 1.25);
            HlxScore6.Add("XXUUXZ", 1.25);
            HlxScore6.Add("ZXUUXX", 1.25);
            HlxScore6.Add("XXUUZX", 1.25);
            HlxScore6.Add("XXUUZZ", 1.25);
            HlxScore6.Add("XZUUXX", 1.25);
            HlxScore6.Add("XZUUXZ", 1.25);
            HlxScore6.Add("XZUUZX", 0.75);
            HlxScore6.Add("XZUUZZ", 0.75);
            HlxScore6.Add("ZXUUXZ", 1.25);
            HlxScore6.Add("ZXUUZX", 1.25);
            HlxScore6.Add("ZXUUZZ", 1.25);
            HlxScore6.Add("ZZUUXX", 1.25);
            HlxScore6.Add("ZZUUXZ", 1.25);
            HlxScore6.Add("ZZUUZX", 0.75);
            HlxScore6.Add("ZZUUZZ", 0.75);
            // ReSharper restore NonLocalizedString
        }

        public enum Column { A300, A100 }

        private readonly AAParams[] AAPARAMS = new AAParams[128];

        public SSRCalc3(string name, Column column)
        {
            Name = name;

            AAParams NULLPARAM = new AAParams(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            for (int i = 0; i < AAPARAMS.Length; i++)
                AAPARAMS[i] = NULLPARAM;

            switch (column)
            {
                case Column.A300:
                    A300Column();
                    break;
                case Column.A100:
                    A100Column();
                    break;
            }
        }

        public string Name { get; private set; }

        private void A300Column()
        {
            // a                        |   Weights for reg peptide       |  weights for short peptide      |       |         | iso-elec vals    | heli2
            // a                        | RC  | RC1  | RC2  | RN   | RN-1 | RCs  | RC1s | RC2s | RNs  |RN-1s|  krh  |  mass   | Ctrm| Ntrm| pk1  | bsc| cmu
            AAPARAMS['A'] = new AAParams(01.10, 00.35, 00.50, 00.80, -0.10, 00.80, -0.30, 00.10, 00.80, -0.50, 00.00, 071.0370, 3.55, 7.59, 00.00, 1.0, 1.2);
            AAPARAMS['C'] = new AAParams(00.45, 00.90, 00.20, -0.80, -0.50, 00.50, 00.40, 00.00, -0.80, -0.50, 00.00, 103.0090, 3.55, 7.50, 00.00, 0.0, 1.0);
            AAPARAMS['D'] = new AAParams(00.15, 00.50, 00.40, -0.50, -0.50, 00.30, 00.30, 00.70, -0.50, -0.50, 00.00, 115.0270, 4.55, 7.50, 04.05, 0.0, 1.1);
            AAPARAMS['E'] = new AAParams(00.95, 01.00, 00.00, 00.00, -0.10, 00.50, 00.10, 00.00, 00.00, -0.10, 00.00, 129.0430, 4.75, 7.70, 04.45, 0.0, 1.1);
            AAPARAMS['F'] = new AAParams(10.90, 07.50, 09.50, 10.50, 10.30, 11.10, 08.10, 09.50, 10.50, 10.30, -0.10, 147.0638, 3.55, 7.50, 00.00, 0.5, 1.0);
            AAPARAMS['G'] = new AAParams(-0.35, 00.20, 00.15, -0.90, -0.70, 00.00, 00.00, 00.10, -0.90, -0.70, 00.00, 057.0210, 3.55, 7.50, 00.00, 0.0, 0.3);
            AAPARAMS['H'] = new AAParams(-1.45, -0.10, -0.20, -1.30, -1.70, -1.00, 00.10, -0.20, -1.30, -1.70, 00.00, 137.0590, 3.55, 7.50, 05.98, 0.0, 0.6);
            AAPARAMS['I'] = new AAParams(08.00, 05.20, 06.60, 08.40, 07.70, 07.70, 05.00, 06.80, 08.40, 07.70, 00.15, 113.0840, 3.55, 7.50, 00.00, 3.5, 1.4);
            AAPARAMS['K'] = new AAParams(-2.05, -0.60, -1.50, -1.90, -1.45, -0.20, -1.40, -1.30, -2.20, -1.45, 00.00, 128.0950, 3.55, 7.50, 10.00, 0.0, 1.0);
            AAPARAMS['L'] = new AAParams(09.30, 05.55, 07.40, 09.60, 09.30, 09.20, 06.00, 07.90, 09.60, 08.70, 00.30, 113.0840, 3.55, 7.50, 00.00, 1.6, 1.6);
            AAPARAMS['M'] = new AAParams(06.20, 04.40, 05.70, 05.80, 06.00, 06.20, 05.00, 05.70, 05.80, 06.00, 00.00, 131.0400, 3.55, 7.00, 00.00, 1.8, 1.0);
            AAPARAMS['N'] = new AAParams(-0.85, 00.20, -0.20, -1.20, -1.10, -0.85, 00.20, -0.20, -1.20, -1.10, 00.00, 114.0430, 3.55, 7.50, 00.00, 0.0, 0.4);
            AAPARAMS['P'] = new AAParams(02.10, 02.10, 02.10, 00.20, 02.10, 03.00, 01.00, 01.50, 00.20, 02.10, 00.00, 097.0530, 3.55, 8.36, 00.00, 0.0, 0.3);
            AAPARAMS['Q'] = new AAParams(-0.40, -0.70, -0.20, -0.90, -1.10, -0.40, -0.80, -0.20, -0.90, -1.10, 00.00, 128.0590, 3.55, 7.50, 00.00, 0.0, 1.0);
            AAPARAMS['R'] = new AAParams(-1.40, 00.50, -1.10, -1.30, -1.10, -0.20, 00.50, -1.10, -1.20, -1.10, 00.00, 156.1010, 3.55, 7.50, 12.00, 0.0, 1.0);
            AAPARAMS['S'] = new AAParams(-0.15, 00.80, -0.10, -0.80, -1.20, -0.50, 00.40, 00.10, -0.80, -1.20, 00.00, 087.0320, 3.55, 6.93, 00.00, 0.0, 1.0);
            AAPARAMS['T'] = new AAParams(00.65, 00.80, 00.60, 00.40, 00.00, 00.60, 00.80, 00.40, 00.40, 00.00, 00.00, 101.0480, 3.55, 6.82, 00.00, 0.0, 1.0);
            AAPARAMS['V'] = new AAParams(05.00, 02.90, 03.40, 05.00, 04.20, 05.10, 02.70, 03.40, 05.00, 04.20, -0.30, 099.0680, 3.55, 7.44, 00.00, 1.4, 1.2);
            AAPARAMS['W'] = new AAParams(12.25, 11.10, 11.80, 11.00, 12.10, 12.40, 11.60, 11.80, 11.00, 12.10, 00.15, 186.0790, 3.55, 7.50, 00.00, 1.6, 1.0);
            AAPARAMS['Y'] = new AAParams(04.85, 03.70, 04.50, 04.00, 04.40, 05.10, 04.20, 04.50, 04.00, 04.40, -0.20, 163.0630, 3.55, 7.50, 10.00, 0.2, 1.0);

            AAPARAMS['B'] = new AAParams(00.15, 00.50, 00.40, -0.50, -0.50, 00.30, 00.30, 00.70, -0.50, -0.50, 00.00, 115.0270, 4.55, 7.50, 04.05, 0.0, 1.1); //?
            AAPARAMS['X'] = new AAParams(00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 000.0000, 0.00, 0.00, 00.00, 0.0, 1.0); //?
            AAPARAMS['Z'] = new AAParams(00.95, 01.00, 00.00, 00.00, -0.10, 00.50, 00.10, 00.00, 00.00, -0.10, 00.00, 129.0430, 4.75, 7.70, 04.45, 0.0, 1.1); //?
        }

        // Note: The 100 A version is not yet verified.
        private void A100Column()
        {
            // a                        |   Weights for reg peptide       |  weights for short peptide      |       |         | iso-elec vals    | heli2
            // a                        | RC  | RC1  | RC2  | RN   | RN-1 | RCs  | RC1s | RC2s | RNs  |RN-1s|  krh  |  mass   | Ctrm| Ntrm| pk1  | bsc| cmu
            AAPARAMS['A'] = new AAParams(01.02, -0.35, 00.35, 01.02, -0.20, 00.50, -0.05, 00.10, 00.50, -0.30, 00.00, 071.0370, 3.55, 7.59, 00.00, 1.0, 1.2);
            AAPARAMS['C'] = new AAParams(00.10, 00.40, 00.20, 00.10, -0.40, 00.60, 00.60, 01.00, 00.60, -0.50, 00.00, 103.0090, 3.55, 7.50, 00.00, 0.0, 1.0);
            AAPARAMS['D'] = new AAParams(00.15, 00.90, 00.60, 00.15, -0.40, 00.60, 00.30, 00.20, 00.60, -0.50, 00.00, 115.0270, 4.55, 7.50, 04.05, 0.0, 1.1);
            AAPARAMS['E'] = new AAParams(01.00, 01.00, -0.20, 01.00, -0.10, 00.70, 00.45, 00.50, 00.00, 00.25, 00.00, 129.0430, 4.75, 7.70, 04.45, 0.0, 1.1);
            AAPARAMS['F'] = new AAParams(11.67, 07.60, 09.70, 11.67, 11.50, 11.30, 08.40, 10.00, 11.30, 10.85, -0.10, 147.0638, 3.55, 7.50, 00.00, 0.5, 1.0);
            AAPARAMS['G'] = new AAParams(-0.35, 00.15, 00.15, -0.35, -0.40, 00.00, 00.15, 00.20, 00.00, -0.70, 00.00, 057.0210, 3.55, 7.50, 00.00, 0.0, 0.3);
            AAPARAMS['H'] = new AAParams(-3.00, -1.40, -1.00, -3.00, -1.90, -1.30, -1.30, -1.10, -1.30, -1.70, 00.00, 137.0590, 3.55, 7.50, 05.98, 0.0, 0.6);
            AAPARAMS['I'] = new AAParams(07.96, 04.95, 06.30, 07.96, 06.60, 07.25, 04.50, 06.50, 07.25, 07.20, 00.15, 113.0840, 3.55, 7.50, 00.00, 3.5, 1.4);
            AAPARAMS['K'] = new AAParams(-3.40, -1.85, -2.30, -2.10, -2.10, -1.75, -1.50, -1.75, -2.30, -2.50, 00.00, 128.0950, 3.55, 7.50, 10.00, 0.0, 1.0);
            AAPARAMS['L'] = new AAParams(09.40, 05.57, 07.40, 09.40, 09.30, 08.70, 05.50, 07.70, 08.70, 08.50, 00.30, 113.0840, 3.55, 7.50, 00.00, 1.6, 1.6);
            AAPARAMS['M'] = new AAParams(06.27, 05.20, 05.70, 06.27, 05.80, 06.25, 04.20, 05.70, 06.25, 05.60, 00.00, 131.0400, 3.55, 7.00, 00.00, 1.8, 1.0);
            AAPARAMS['N'] = new AAParams(-0.95, 01.20, -0.10, -0.95, -1.30, -0.65, 00.40, -0.05, -0.65, -1.20, 00.00, 114.0430, 3.55, 7.50, 00.00, 0.0, 0.4);
            AAPARAMS['P'] = new AAParams(01.85, 01.70, 01.75, 01.85, 01.20, 02.50, 01.70, 02.10, 02.50, 01.90, 00.00, 097.0530, 3.55, 8.36, 00.00, 0.0, 0.3);
            AAPARAMS['Q'] = new AAParams(-0.60, -0.50, -0.20, -0.60, -1.10, -0.40, -0.20, -0.70, -0.40, -1.30, 00.00, 128.0590, 3.55, 7.50, 00.00, 0.0, 1.0);
            AAPARAMS['R'] = new AAParams(-2.55, -1.40, -1.50, -1.10, -1.30, -1.00, 00.40, -1.00, -1.10, -1.90, 00.00, 156.1010, 3.55, 7.50, 12.00, 0.0, 1.0);
            AAPARAMS['S'] = new AAParams(-0.14, 01.10, -0.10, -0.14, -1.00, -0.40, 00.20, -0.30, -0.40, -1.20, 00.00, 087.0320, 3.55, 6.93, 00.00, 0.0, 1.0);
            AAPARAMS['T'] = new AAParams(00.64, 00.95, 00.60, 00.64, -0.10, 00.40, 00.30, 00.40, 00.40, -0.50, 00.00, 101.0480, 3.55, 6.82, 00.00, 0.0, 1.0);
            AAPARAMS['V'] = new AAParams(04.68, 02.10, 03.40, 04.68, 03.90, 04.40, 02.10, 03.00, 04.40, 04.40, -0.30, 099.0680, 3.55, 7.44, 00.00, 1.4, 1.2);
            AAPARAMS['W'] = new AAParams(13.35, 11.50, 11.80, 13.35, 13.00, 13.90, 11.80, 13.00, 13.90, 12.90, 00.15, 186.0790, 3.55, 7.50, 00.00, 1.6, 1.0);
            AAPARAMS['Y'] = new AAParams(05.35, 04.30, 05.10, 05.35, 05.00, 05.70, 05.00, 05.40, 05.70, 05.30, -0.20, 163.0630, 3.55, 7.50, 10.00, 0.2, 1.0);

            AAPARAMS['B'] = new AAParams(00.15, 00.50, 00.40, -0.50, -0.50, 00.30, 00.30, 00.70, -0.50, -0.50, 00.00, 115.0270, 4.55, 7.50, 04.05, 0.0, 1.1); //?
            AAPARAMS['X'] = new AAParams(00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 00.00, 000.0000, 0.00, 0.00, 00.00, 0.0, 1.0); //?
            AAPARAMS['Z'] = new AAParams(00.95, 01.00, 00.00, 00.00, -0.10, 00.50, 00.10, 00.00, 00.00, -0.10, 00.00, 129.0430, 4.75, 7.70, 04.45, 0.0, 1.1); //?
        }

        // control variables, 0 means leaving them ON, 1 means turning them OFF
        // Translator1 note:  Some day these may be turned into options.  For the
        //    time being they are unchanging, and the tests for them in each function
        //    are superfluous and absurd.
        // Translator2 note:  To avoid warnings on unreachable code, these were changed
        //    to auto-implemented properties, which means they can now be set.

        public int NOELECTRIC { get; set; }
        public int NOCLUSTER { get; set; }
        public int NODIGEST { get; set; }
        public int NOSMALL { get; set; }
        public int NOHELIX1 { get; set; }
        public int NOHELIX2 { get; set; }
        public int NOEHEL { get; set; }

        //Translator1 note:  This constant controls whether "bugs" in the original
        //perl code are maintained.  A conversation with the developers has revealed
        //that the constant data in the static initialization blocks has been "tuned"
        //to the algorithm in its undebugged state.  In other words, using a correct
        //algorithm would invalidate the results.
        private const bool DUPLICATE_ORIGINAL_CODE = true;
        //Translator1 note:  Some code is supposed to be executed only when
        // $SSRCVERSION==3.  SSRCVERSION was commented out in my version of the perl
        // code.  This may need some reworking.  Speaking with the developers, it
        // was determined that it ought not to have been commented out.  So --
        // ALGORITHM_VERSION may be used to choose the older or newer code
        private const int ALGORITHM_VERSION = 3;

        // Length Scaling length limits and scaling factors
        private const int LPLim = 20;
        private const int SPLim = 8;
        private const double LPSFac = 0.0270;
        private const double SPSFac = -0.055;

        // UnDigested (missed cuts) scaling Factors
        private const double UDF21 = 0.0, UDF22 = 0.0;    // rightmost
        private const double UDF31 = 1.0, UDF32 = 0.0;    // inside string

        // total correction values, 20..30 / 30..40 / 40..50 /50..500
        private const double SUMSCALE1 = 0.27, SUMSCALE2 = 0.33, SUMSCALE3 = 0.38, SUMSCALE4 = 0.447;

        // clusterness scaling: i.e. weight to give cluster correction.
        private const double KSCALE = 0.4;

        // isoelectric scaling factors
        private const double Z01 = -0.03, Z02 = 0.60, NDELTAWT = 0.8;   // negative delta values
        private const double Z03 = 0.00, Z04 = 0.00, PDELTAWT = 1.0;   // positive delta values

        // proline chain scores
        private const double PPSCORE = 1.2, PPPSCORE = 3.5, PPPPSCORE = 5.0;

        // helix scaling factors
        private const double HELIX1SCALE = 1.6, HELIX2SCALE = 0.255;

        /// <summary>
        /// No such thing as an unkown score for this calculator.  ScoreSequence
        /// always returns a value.
        /// </summary>
        public double UnknownScore
        {
            get { return 0; }
        }

        public double? ScoreSequence(string seq)
        {
            seq = FastaSequence.StripModifications(seq);
            double tsum3 = 0.0;
            int i;

            // Core summation

            int sze = seq.Length;
            if (sze < 4)                          // peptide is too short ot have any retention
                return tsum3;
            if (sze < 10)                         // short peptides use short peptide retention weights
            {
                tsum3 =
                   AAPARAMS[seq[0]].RC1S +        // Sum weights for 1st
                   AAPARAMS[seq[1]].RC2S +        // second,
                   AAPARAMS[seq[sze - 1]].RCNS +    // ultimate
                   AAPARAMS[seq[sze - 2]].RCN2S;    // and penultimate aa

                for (i = 2; i < sze - 2; i++)            // add weights for aa's in the middle
                {
                    tsum3 += AAPARAMS[seq[i]].RCS;
                }
            }
            else                                  // longer peptides use regular retention weights
            {
                tsum3 =
                   AAPARAMS[seq[0]].RC1 +         // Sum weights for 1st
                   AAPARAMS[seq[1]].RC2 +         // second,
                   AAPARAMS[seq[sze - 1]].RCN +     // ultimate
                   AAPARAMS[seq[sze - 2]].RCN2;     // and penultimate aa

                for (i = 2; i < sze - 2; i++)            // add weights for aa's in the middle
                {
                    tsum3 += AAPARAMS[seq[i]].RC;
                }
            }
            //_log.debug("Core = "+tsum3);

            // 1- smallness - adjust based on tsum score of peptides shorter than 20 aa's.
            tsum3 += smallness(sze, tsum3);
            //_log.debug("smallness = "+tsum3);
            // 2- undigested parts
            tsum3 -= undigested(seq);
            //_log.debug("undigested = "+tsum3);
            // 3- clusterness # NB:weighting of v1 is now done in subrtn.
            tsum3 -= clusterness(seq);
            //_log.debug("clusterness = "+tsum3);
            // 4- proline fix
            tsum3 -= proline(seq);
            //_log.debug("proline = "+tsum3);
            // 5- length scaling correction
            tsum3 *= length_scale(sze);
            //_log.debug("length_scale = "+tsum3);
            // 6- total sum correction
            if (tsum3 >= 20 && tsum3 < 30) tsum3 -= ((tsum3 - 18) * SUMSCALE1);
            if (tsum3 >= 30 && tsum3 < 40) tsum3 -= ((tsum3 - 18) * SUMSCALE2);
            if (tsum3 >= 40 && tsum3 < 50) tsum3 -= ((tsum3 - 18) * SUMSCALE3);
            if (tsum3 >= 50) tsum3 -= ((tsum3 - 18) * SUMSCALE4);
            //_log.debug("total sum = "+tsum3);
            // 7- isoelectric change
            tsum3 += newiso(seq, tsum3);
            //_log.debug("isoelectric = "+tsum3);
            // 8- helicity corrections  #NB: HELIX#SCALE-ing is now done in subrtn.
            tsum3 += helicity1(seq);
            //_log.debug("helicity1 = "+tsum3);
            tsum3 += helicity2(seq);
            //_log.debug("helicity2 = "+tsum3);
            tsum3 += helectric(seq);
            //_log.debug("helectric = "+tsum3);
            return tsum3;
        }

        private double smallness(int sqlen, double tsum)
        {
            if (NOSMALL == 1)
                return 0.0;
            if (sqlen < 20)
            {
                if ((tsum / sqlen) < 0.9)
                    return 3.5 * (0.9 - (tsum / sqlen));
            }
            if (sqlen < 15)
            {
                if ((tsum / sqlen) > 2.8)
                    return 2.6 * ((tsum / sqlen) - 2.8);
            }
            return 0.0;
        }

        private double undigested(String sq)
        {
            if (NODIGEST == 1)
                return 0.0;

            char op1, op2;

            int xx = sq.Length - 1;
            char re = sq[xx];
            double csum = 0.0;

            // rightmost
            if (re == 'R' || re == 'K' || re == 'H')
            {
                op1 = sq[xx - 1];                          // left by 1
                op2 = sq[xx - 2];                          // left by 2
                csum = UDF21 * AAPARAMS[op1].UndKRH + UDF22 * AAPARAMS[op2].UndKRH;
            }
            // scan through string, starting at second and ending two before left
            //    --Translator1 note:
            //      the perl code does not jibe with the comment above, and will probably need repair
            //      possibly dd should start out as 2, not 0; and should loop to xx-2, not xx.

            //      Negative indices on the perl substr function make substrings offset from right
            //      (instead of left) end of string.  The perl loop gets negative indices.  This may be a
            //      a problem.
            for (int dd = 0; dd < xx; dd++)
            {
                re = sq[dd];
                if (re == 'K' || re == 'R' || re == 'H')
                {
                    char op3, op4;
                    op1 = op2 = op3 = op4 = '\0';
                    if (dd - 1 >= 0 && dd - 1 <= xx)
                        op1 = sq[dd - 1];    //left by 1
                    if (dd - 2 >= 0 && dd - 2 <= xx)
                        op2 = sq[dd - 2];    //left by 2
// ReSharper disable ConditionIsAlwaysTrueOrFalse
                    if (DUPLICATE_ORIGINAL_CODE)
// ReSharper restore ConditionIsAlwaysTrueOrFalse
                    {
                        if (dd - 1 < 0 && (-(dd - 1)) <= xx)
                            op1 = sq[xx + (dd - 1) + 1];
                        if (dd - 2 < 0 && (-(dd - 2)) <= xx)
                            op2 = sq[xx + (dd - 2) + 1];
                    }
                    if (dd + 1 >= 0 && dd + 1 <= xx)
                        op3 = sq[dd + 1];    //right by 1
                    if (dd + 2 >= 0 && dd + 2 <= xx)
                        op4 = sq[dd + 2];    //right by 2;

                    csum = csum +
                        (UDF31 * (AAPARAMS[op1].UndKRH + AAPARAMS[op3].UndKRH)) +
                        (UDF32 * (AAPARAMS[op2].UndKRH + AAPARAMS[op4].UndKRH));
                }
            }
            return csum;
        }

        // ============================================================
        // compute clusterness of a string - v 2,3 algorithm
        // code W,L,F,I as 5
        // code M,Y,V as 1
        // code all others as 0

        private double clusterness(String sq)
        {
            if (NOCLUSTER == 1)
                return 0.0;

            string cc = "0" + sq + "0"; // Not L10N
// ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (ALGORITHM_VERSION == 3)
// ReSharper restore ConditionIsAlwaysTrueOrFalse
            {
                cc = cc.ReplaceAAs("LIW", "5"); // Not L10N
                cc = cc.ReplaceAAs("AMYV", "1"); // Not L10N
                cc = cc.ReplaceAAs("A-Z", "0"); // Not L10N
            }
            else
            // Suppress the unreachable code warning
#pragma warning disable 162
// ReSharper disable HeuristicUnreachableCode
            {
                cc = cc.ReplaceAAs("LIWF", "5"); // Not L10N
                cc = cc.ReplaceAAs("MYV", "1"); // Not L10N
                cc = cc.ReplaceAAs("A-Z", "0"); // Not L10N
            }
// ReSharper restore HeuristicUnreachableCode
#pragma warning restore 162

            double score = 0.0;
            //
            // Translator1 note:  check on true meaning of the algorithm that defines 'occurs'
            // Should an encoded aa string such as 015101510 match pick "01510" once or twice?
            // The perl code seems to match once.  0151001510 would match twice.

            foreach (var pair in CLUSTCOMB)
            {
                int occurs = 0;
                Match m = pair.Key.Match(cc);
                while (m.Success)
                {
                    occurs++;
                    m = m.NextMatch();
                }
                if (occurs > 0)
                {
                    double sk = pair.Value;
                    double addit = sk * occurs;
                    score += addit;
                }
            }
            return score * KSCALE;
        }

        // ============================================================
        //  process based on proline - v 2,3 algorithm
        private static double proline(String sq)
        {
            double score = 0.0;
            if (sq.Contains("PPPP")) // Not L10N
                score = PPPPSCORE;
            else if (sq.Contains("PPP")) // Not L10N
                score = PPPSCORE;
            else if (sq.Contains("PP")) // Not L10N
                score = PPSCORE;
            return score;
        }

        // ============================================================
        //  scaling based on length - v 1,2,3 algorithms
        private static double length_scale(int sqlen)
        {
            double LS = 1.0;
            if (sqlen < SPLim)
            {
                LS = 1.0 + SPSFac * (SPLim - sqlen);
            }
            else
            {
                if (sqlen > LPLim)
                {
                    LS = 1.0 / (1.0 + LPSFac * (sqlen - LPLim));
                }
            }
            return LS;
        }

        private static int eMap(char aa)
        {
            switch (aa)
            {
                case 'K': return 0;
                case 'R': return 1;
                case 'H': return 2;
                case 'D': return 3;
                case 'E': return 4;
                case 'C': return 5;
                case 'Y': return 6;
                default: return -1;
            }
        }

        // ============================================================
        // compute partial charge - v 2,3 algorithms
        private static double _partial_charge(double pK, double pH)
        {
            double cr = Math.Pow(10.0, (pK - pH));
            return cr / (cr + 1.0);
        }

        // ============================================================
        //    - v 2,3 algorithms
        private double electric(String sq)
        {
            int[] aaCNT = { 0, 0, 0, 0, 0, 0, 0 };

            // Translator1 Note: this is commented out in the perl source
            // if (NOELECTRIC == 1) { return 1.0; }

            // get c and n terminus acids
            int ss = sq.Length;
            char s1 = sq[0];
            char s2 = sq[ss - 1];
            double pk0 = AAPARAMS[s1].CT;
            double pk1 = AAPARAMS[s2].NT;

            // count them up
            for (int i = 0; i < ss; i++)
            {
                char e = sq[i];
                int index = eMap(e);
                if (index >= 0)
                    aaCNT[index]++;
            }

            // cycle through pH values looking for closest to zero
            // coarse pass
            double best = 0.0; double min = 100000; const double step1 = 0.3;

            for (double z = 0.01; z <= 14.0; z = z + step1)
            {
                double check = CalcR(z, pk0, pk1, aaCNT);
                if (check < 0)
                    check = 0 - check;
                if (check < min)
                {
                    min = check;
                    best = z;
                }
            }

            double best1 = best;

            // fine pass
            min = 100000;
            for (double z = best1 - step1; z <= best1 + step1; z = z + 0.01)
            {
                double check = CalcR(z, pk0, pk1, aaCNT);
                if (check < 0)
                    check = 0 - check;
                if (check < min)
                {
                    min = check;
                    best = z;
                }
            }
            return best;
        }

        // ============================================================
        // compute R - v 2,3 algorithms
        private double CalcR(double pH, double PK0, double PK1, int[] CNTref)
        {
            double cr0 =
                                     _partial_charge(PK0, pH)                    // n terminus
               + CNTref[eMap('K')] * _partial_charge(AAPARAMS['K'].PK, pH)  // lys // Not L10N
               + CNTref[eMap('R')] * _partial_charge(AAPARAMS['R'].PK, pH)  // arg // Not L10N 
               + CNTref[eMap('H')] * _partial_charge(AAPARAMS['H'].PK, pH)  // his // Not L10N
               - CNTref[eMap('D')] * _partial_charge(pH, AAPARAMS['D'].PK)  // asp // Not L10N
               - CNTref[eMap('E')] * _partial_charge(pH, AAPARAMS['E'].PK)  // glu // Not L10N
               - CNTref[eMap('Y')] * _partial_charge(pH, AAPARAMS['Y'].PK)  // try // Not L10N
               - _partial_charge(pH, PK1); // c terminus
            /*
            // The following was taken out of the formula for R
            //  - $CNTref->{C} * _partial_charge( $pH,      $PK{C} )    // cys
            */
            return cr0;
        }

        private double newiso(string sq, double tsum)
        {
            if (NOELECTRIC == 1)
                return 0.0;

            // compute mass
            double mass = 0.0;
            foreach (char cf1 in sq)
            {
                mass += AAPARAMS[cf1].AMASS;
            }
            // compute isoelectric value
            double pi1 = electric(sq);
            double lmass = 1.8014 * Math.Log(mass);

            // make mass correction
            double delta1 = pi1 - 19.107 + lmass;
            //apply corrected value as scaling factor

            double corr01 = 0.0;
            if (delta1 < 0.0)
            {
                corr01 = (tsum * Z01 + Z02) * NDELTAWT * delta1;
            }
            else if (delta1 > 0.0)
            {
                corr01 = (tsum * Z03 + Z04) * PDELTAWT * delta1;
            }
            return corr01;
        }

        // ============================================================
        // called by helicity1  - v 3 algorithm
        private static double heli1TermAdj(string ss1, int ix2, int sqlen)
        {
            int where = 0;

            for (int i = 0; i < ss1.Length; i++)
            {
                char m = ss1[i];
                if (m == 'O' || m == 'U')
                {
                    where = i;
                    // Suppress unreachable code warning
#pragma warning disable 162
// ReSharper disable ConditionIsAlwaysTrueOrFalse
                    if (!DUPLICATE_ORIGINAL_CODE)
// ReSharper restore ConditionIsAlwaysTrueOrFalse
// ReSharper disable HeuristicUnreachableCode
                        break;
// ReSharper restore HeuristicUnreachableCode
#pragma warning restore 162
                }
            }

            where += ix2;

            if (where < 2) { return 0.20; }
            if (where < 3) { return 0.25; }
            if (where < 4) { return 0.45; }

            if (where > sqlen - 3) { return 0.2; }
            if (where > sqlen - 4) { return 0.75; }
            if (where > sqlen - 5) { return 0.65; }

            return 1.0;
        }

        // ============================================================
        // helicity1 adjust for short helices or sections - v 3 algorithm
        //
        private double helicity1(string sq)
        {
            if (NOHELIX1 == 1)
                return 0.0;

            string hc = sq; //helicity coded sq

            /* Translator1 note:  notice lowercase 'z'.  This never appears in any patterns to which this
               string is compared, and will never match any helicity patterns.
            */
            hc = hc.ReplaceAAs("PHRK", "z"); // Not L10N
            hc = hc.ReplaceAAs("WFIL", "X"); // Not L10N
            hc = hc.ReplaceAAs("YMVA", "Z"); // Not L10N
            hc = hc.ReplaceAAs("DE", "O"); // Not L10N
            hc = hc.ReplaceAAs("GSPCNKQHRT", "U"); // Not L10N

            double sum = 0.0;
            int sqlen = hc.Length;

            // Translator1 note: this loop should be reviewed carefully

            for (int i = 0; i < sqlen - 3; i++)
            {
                string hc4 = string.Empty, hc5 = string.Empty, hc6 = string.Empty;
                double sc4 = 0.0, sc5 = 0.0, sc6 = 0.0;

                if (hc.Substring(i).Length >= 6)
                {
                    hc6 = hc.Substring(i, 6);
                    sc6 = 0.0;
                    if (HlxScore6.ContainsKey(hc6))
                    {
                        sc6 = HlxScore6[hc6];
                    }
                }
                if (sc6 > 0)
                {
                    double trmAdj6 = heli1TermAdj(hc6, i, sqlen);
                    sum += (sc6 * trmAdj6);
                    i = i + 1; //??
                    continue;
                }

                if (hc.Substring(i).Length >= 5)
                {
                    hc5 = hc.Substring(i, 5);
                    sc5 = 0.0;
                    if (HlxScore5.ContainsKey(hc5))
                    {
                        sc5 = HlxScore5[hc5];
                    }
                }
                if (sc5 > 0)
                {
                    double trmAdj5 = heli1TermAdj(hc5, i, sqlen);
                    sum += (sc5 * trmAdj5);
                    i = i + 1; //??
                    continue;
                }

                if (hc.Substring(i).Length >= 4)
                {
                    hc4 = hc.Substring(i, 4);
                    sc4 = 0.0;
                    if (HlxScore4.ContainsKey(hc4))
                    {
                        sc4 = HlxScore4[hc4];
                    }
                }
                if (sc4 > 0)
                {
                    double trmAdj4 = heli1TermAdj(hc4, i, sqlen);
                    sum += (sc4 * trmAdj4);
                    i = i + 1; //??
                    // continue;
                }
            }
            return HELIX1SCALE * sum;
        }

        // ============================================================
        // called by heli2calc  - v 3 algorithm
        private double evalH2pattern(String pattern, String testsq, int posn, char etype)
        {
            char f01 = pattern[0];
            double prod1 = AAPARAMS[f01].H2BASCORE;
            int iss = 0;
            const int OFF1 = 2;
            int acount = 1;
            char far1 = '\0';
            char far2 = '\0';

            char testAAl = testsq[OFF1 + posn];
            char testAAr = testsq[OFF1 + posn + 2];
            string testsqCopy = testsq.Substring(OFF1 + posn + 1);
            double mult = connector(f01, testAAl, testAAr, "--", far1, far2); // Not L10N
            prod1 = prod1 * mult;
            if (etype == '*') // Not L10N
                prod1 = prod1 * 25.0;
            if (mult == 0.0)
            {
                return 0.0;
            }
            for (int i = 1; i < pattern.Length - 2; i = i + 3)
            {
                string fpart = pattern.Substring(i, 2);
                char gpart = (i + 2) < pattern.Length ? pattern[i + 2] : '\0'; // Not L10N
                double s3 = AAPARAMS[gpart].H2BASCORE;
                if (fpart.Equals("--")) // Not L10N
                {
                    iss = 0; far1 = '\0'; far2 = '\0'; // Not L10N
                }
                if (fpart.Equals("<-")) // Not L10N
                {
                    iss = 1; far1 = testsqCopy[i + 1]; far2 = '\0'; // Not L10N
                }
                if (fpart.Equals("->")) // Not L10N
                {
                    iss = -1; far1 = '\0'; far2 = testsqCopy[i + 3]; // Not L10N
                }

                testAAl = testsqCopy[i + 1 + iss];
                testAAr = testsqCopy[i + 3 + iss];

                mult = connector(gpart, testAAl, testAAr, fpart, far1, far2);

                if (etype == '*') // Not L10N
                {
                    if (mult != 0.0 || acount < 3)
                    {
                        prod1 = prod1 * 25.0 * s3 * mult;
                    }
                }

                if (etype == '+') // Not L10N
                {
                    prod1 = prod1 + s3 * mult;
                }

                if (mult == 0.0)
                {
                    return prod1;
                }

                acount++;
            }
            return prod1;
        }

        // ============================================================
        // called by evalH2pattern  - v 3 algorithm
        private double connector(char acid, char lp, char rp, String ct, char far1, char far2)
        {
            double mult = 1.0;

            if (ct.Contains("<-")) { mult *= 0.2; } // Not L10N
            if (ct.Contains("->")) { mult *= 0.1; } // Not L10N

            mult *= AAPARAMS[lp].H2CMULT;
            if (lp != rp) mult *= AAPARAMS[rp].H2CMULT;

            if (acid == 'A' || acid == 'Y' || acid == 'V' || acid == 'M') // Not L10N
            {
                if (lp == 'P' || lp == 'G' || rp == 'P' || rp == 'G') mult = 0.0; // Not L10N
                if (ct.Contains("->") || ct.Contains("<-")) mult = 0.0; // Not L10N
            }

            if (acid == 'L' || acid == 'W' || acid == 'F' || acid == 'I') // Not L10N
            {
                if (((lp == 'P' || lp == 'G') || (rp == 'P' || rp == 'G')) && (!ct.Contains("--"))) mult = 0.0; // Not L10N
                if (((far1 == 'P' || far1 == 'G') || (far2 == 'P' || far2 == 'G')) && (ct.Contains("<-") || ct.Contains("->"))) mult = 0.0; // Not L10N
            }
            return mult;
        }

        private const int HISC = 0;
        private const int GSC = 1;

        // ============================================================
        // called by helicity2  - v 3 algorithm
        private double[] heli2Calc(String sq)
        {
            // Translator1 note: in the original perl and translated C, this function
            // was void and returned values through double pointer arguments. Like this:
            //
            // void  heli2Calc(char *sq, double *hisc, double *gsc)
            //

            double[] ret = new double[2];
            string traps; //not my()'ed in perl source
            string best = string.Empty;
            const int llim = 50;
            double hiscore = 0.0;
            int best_pos = 0;

            if (sq.Length < 11)
            {
                ret[HISC] = 0.0;
                ret[GSC] = 0.0;
                return ret;
            }

            string prechop = sq;
            string sqCopy = sq.Substring(2, sq.Length - 4);

            string pass1 = sqCopy.ReplaceAAs("WFILYMVA", "1"); // Not L10N
            pass1 = pass1.ReplaceAAs("GSPCNKQHRTDE", "0"); // Not L10N

            for (int i = 0; i < pass1.Length; i++)
            {
                char m = pass1[i];
                if (m == '1') // Not L10N
                {
                    string lc = pass1.Substring(i);
                    string sq2 = sqCopy.Substring(i);
                    string pat = string.Empty;
                    int zap = 0;
                    int subt = 0;

                    while (zap <= llim && subt < 2)
                    {
                        char f1 = (zap < 0 || zap >= lc.Length ? '0' : lc[zap]);
                        char f2 = (zap - 1 < 0 || zap - 1 >= lc.Length ? '0' : lc[zap - 1]); // Not L10N
                        char f3 = (zap + 1 < 0 || zap + 1 >= lc.Length ? '0' : lc[zap + 1]); // Not L10N

                        if (f1 == '1') // Not L10N
                        {
                            if (zap > 0)
                                pat += "--"; // Not L10N
                            pat += sq2.Substring(zap, 1);
                        }
                        else
                        {
                            if (f2 == '1' && f1 == '0') // Not L10N
                            {
                                subt++;
                                if (subt < 2)
                                {
                                    pat += "->"; // Not L10N
                                    pat += sq2.Substring(zap - 1, 1);
                                }
                            }
                            else
                            {
                                if (f3 == '1' && f1 == '0') // Not L10N
                                {
                                    subt++;
                                    if (subt < 2)
                                    {
                                        pat += "<-"; // Not L10N
                                        pat += sq2.Substring(zap + 1, 1);
                                    }
                                }
                            }
                        }

                        if (f1 == '0' && f2 == '0' && f3 == '0') // Not L10N
                            zap = 1000;
                        zap += 3;
                    }

                    if (pat.Length > 4)
                    {
                        traps = prechop;
                        double skore = evalH2pattern(pat, traps, i - 1, '*'); // Not L10N
                        if (skore >= hiscore)
                        {
                            hiscore = skore;
                            best = pat;
                            best_pos = i;
                        }
                    }
                }
            }

            if (hiscore > 0.0)
            {
                double gscore = hiscore; //not my()'ed in perl source
                traps = prechop;
                hiscore = evalH2pattern(best, traps, best_pos - 1, '+'); // Not L10N

                ret[HISC] = hiscore;
                ret[GSC] = gscore;
                return ret;
            }
            
            ret[HISC] = 0.0;
            ret[GSC] = 0.0;
            return ret;
        }

        // ============================================================
        // helicity2 adjust for long helices - v 3 algorithm
        private double helicity2(string sq)
        {
            if (NOHELIX2 == 1)
                return 0.0;
            string Bksq = sq.Backwards();
            double[] fhg = heli2Calc(sq);
            double FwHiscor = fhg[HISC];
            double FwGscor = fhg[GSC];
            double[] rhg = heli2Calc(Bksq);
            double BkHiscor = rhg[HISC];
            double BkGscor = rhg[GSC];
            double h2FwBk = BkGscor > FwGscor ? BkHiscor : FwHiscor;
            double lenMult = 0.0;
            if (sq.Length > 30)
            {
                lenMult = 1;
            }
            double NoPMult = 0.75;
            if (sq.Contains("P")) // Not L10N
                NoPMult = 0.0;
            double h2mult = 1.0 + lenMult + NoPMult;
            return HELIX2SCALE * h2mult * h2FwBk;
        }

        private double helectric(String sq)
        {
            if (NOEHEL == 1 || sq.Length > 14 || sq.Length < 4)
                return 0.0;
            string mpart = sq.Substring(sq.Length - 4);

            if (mpart[0] == 'D' || mpart[0] == 'E') // Not L10N
            {
                mpart = mpart.Substring(1, 2);
                if (mpart.ContainsAA("PGKRH")) // Not L10N
                    return 0.0;
                mpart = mpart.ReplaceAAs("LI", "X"); // Not L10N
                mpart = mpart.ReplaceAAs("AVYFWM", "Z"); // Not L10N
                mpart = mpart.ReplaceAAs("GSPCNKQHRTDE", "U"); // Not L10N

                switch (mpart)
                {
                    // ReSharper disable NonLocalizedString
                    case "XX": return 1.0;
                    case "ZX": return 0.5;
                    case "XZ": return 0.5;
                    case "ZZ": return 0.4;
                    case "XU": return 0.4;
                    case "UX": return 0.4;
                    case "ZU": return 0.2;
                    case "UZ": return 0.2;
                    // ReSharper restore NonLocalizedString
                }
            }
            return 0;
        }

        /*
         * Translator2 note: The code for the Isoparams array was found in
         *      the Java version, but never used.  Refering to the Perl
         *      version showed that the only place these values were used
         *      was in the electric_scale() function, which in turn was never
         *      used.  Both the array and function are included here for
         *      completeness, but commented out, since they are never used.
         *
        private class Isoparams
        {
            public double emin { get; private set; }
            public double emax { get; private set; }
            public double eK { get; private set; }

            public Isoparams(double EMIN, double EMAX, double EK)
            {
                emin = EMIN; emax = EMAX; eK = EK;
            }
        }

        private static readonly Isoparams[] ISOPARAMS = new[]
        {
            new Isoparams(3.8, 4.0, 0.880),
            new Isoparams(4.0, 4.2, 0.900),
            new Isoparams(4.2, 4.4, 0.920),
            new Isoparams(4.4, 4.6, 0.940),
            new Isoparams(4.6, 4.8, 0.960),
            new Isoparams(4.8, 5.0, 0.980),
            new Isoparams(5.0, 6.0, 0.990),
            new Isoparams(6.0, 7.0, 0.995),
            new Isoparams(7.0, 8.0, 1.005),
            new Isoparams(8.0, 9.0, 1.010),
            new Isoparams(9.0, 9.2, 1.020),
            new Isoparams(9.2, 9.4, 1.030),
            new Isoparams(9.4, 9.6, 1.040),
            new Isoparams(9.6, 9.8, 1.060),
            new Isoparams(9.8, 10.0, 1.080)
        };

        // convert electric to scaler - v 2,3 algorithms
        private static double electric_scale(double v)
        {
	        double best=1.0;

            // Translator2 Note: this is commented out in the perl source
            // if (NOELECTRIC==1) { return 1.0; }
        	
	        foreach (Isoparams p in ISOPARAMS)
	        {
		        if (v > p.emin && v < p.emax)
                    best= p.eK;
	        }

	        return best;            
        }
        */
    }

    internal static class HelpersLocal
    {
        /// <summary>
        /// Replace amino acids in a sequence string with some other value.
        /// </summary>
        /// <param name="s">The sequence string with AAs in uppercase</param>
        /// <param name="aas">The amino acid characters, or A-Z for all, to replace</param>
        /// <param name="newValue">The value to use as a replacement</param>
        /// <returns>Modified string with specified AAs replaced</returns>
        public static string ReplaceAAs(this IEnumerable<char> s, string aas, string newValue)
        {
            StringBuilder sb = new StringBuilder();
            bool allAAs = (aas == "A-Z"); // Not L10N
            foreach (char c in s)
            {
                if (!allAAs && aas.IndexOf(c) != -1)
                    sb.Append(newValue);
                else if (allAAs && char.IsLetter(c) && char.IsUpper(c))
                    sb.Append(newValue);
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Inspects a sequence of amino acids, and returns true if it contains
        /// any of the designated amino acid characters.
        /// </summary>
        /// <param name="s">Amino acid sequence</param>
        /// <param name="aas">List of characters to search for</param>
        /// <returns>True if any of the amino acid characters are found</returns>
        public static bool ContainsAA(this IEnumerable<char> s, string aas)
        {
            foreach (char c in s)
            {
                if (aas.IndexOf(c) != -1)
                    return true;
            }
            return false;
        }

        public static string Backwards(this IEnumerable<char> s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s.Reverse())
                sb.Append(c);
            return sb.ToString();
        }        
    }
    // ReSharper restore CharImplicitlyConvertedToNumeric
    // ReSharper restore InconsistentNaming
}
