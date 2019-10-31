using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Model.Prosit.Config;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using Tensorflow.Serving;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class PrositConnectionTest
    {
        [TestMethod]
        public void TestPrositConnection()
        {
            PrositPredictionClient client = PrositPredictionClient.CreateClient(PrositConfig.GetPrositConfig());
            Assert.IsTrue(TestClient(client));
        }

//        [TestMethod]
//        public void TestSecureConnection()
//        {
//            PrositPredictionClient client = PrositPredictionClient.CreateClient("protservices.proteomicsdb.in.tum.de:8500", true);
//            Assert.IsTrue(TestClient(client));
//        }

        public bool TestClient(PrositPredictionClient client)
        {
            var pingPep = new Peptide(@"PING");
            var peptide = new PeptideDocNode(pingPep);
            var precursor = new TransitionGroupDocNode(new TransitionGroup(pingPep, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
                new TransitionDocNode[0]);
            var input = new PrositIntensityModel.PeptidePrecursorNCE(peptide, precursor, 32);
            var intensityModel = PrositIntensityModel.GetInstance("intensity");
            try
            {
                intensityModel.PredictSingle(client, SrmSettingsList.GetDefault(), input, CancellationToken.None);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }

        }
    }
}
