using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GuiFunctions;
using NUnit.Framework;

namespace Test
{
    internal class AATestUwU
    {
        [Test]
        public static void TestTruncySequences()
        {
            string expectedString = "PEPTIDE,PEPTID,PEPTI,PEPT,PEP,PE,P,EPTIDE,PTIDE,TIDE,IDE,DE,E";
            string[] expected = expectedString.Split(',');

            UwuVM vm = new();
            vm.ProteinSequence = "PEPTIDE";

            var strings = vm.GetTruncySequences().ToArray();
            Assert.That(expected.Length, Is.EqualTo(strings.Length));
            Assert.That(expected.SequenceEqual(strings));
        }

        [Test]
        public static void TestInternalSequences()
        {
            string expectedString = "PPTIDE,PETIDE,PEPIDE,PEPTDE,PEPTIE," +
                                    "PTIDE,PEIDE,PEPDE,PEPTE," +
                                    "PIDE,PEDE,PEPE," +
                                    "PDE,PEE";
            string[] expected = expectedString.Split(',');

            UwuVM vm = new();
            vm.ProteinSequence = "PEPTIDE";

            var strings = vm.GetSplicedSequences("PEPTIDE").ToList();

            Assert.That(expected.Length, Is.EqualTo(strings.Count));
            Assert.That(expected.SequenceEqual(strings));



         
            strings = vm.GetSplicedSequences("PEPTIDE").ToList();
            expectedString = "PPTIDE,PETIDE,PEPIDE,PEPTDE,PEPTIE," +
                             "PTIDE,PEIDE,PEPDE,PEPTE," +
                             "PIDE,PEDE,PEPE";
            expected = expectedString.Split(',');
            Assert.That(expected.Length, Is.EqualTo(strings.Count));
            Assert.That(expected.SequenceEqual(strings));
        }
    }
}
