#nullable enable
using Chemistry;
using MassSpectrometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer
{
    public class Ms2ScanWithSpecificMass
    {
        private readonly double[] _deconvolutedMonoisotopicMasses;
        public Ms2ScanWithSpecificMass(MsDataScan mzLibScan, double precursorMonoisotopicPeakMz, int precursorCharge, string fullFilePath, CommonParameters commonParam, 
            IsotopicEnvelope[]? neutralExperimentalFragments = null, double? precursorIntensity = null, int? envelopePeakCount = null, double? precursorFractionalIntensity = null)
        {
            PrecursorMonoisotopicPeakMz = precursorMonoisotopicPeakMz;
            PrecursorCharge = precursorCharge;
            PrecursorMass = PrecursorMonoisotopicPeakMz.ToMass(precursorCharge);
            PrecursorIntensity = precursorIntensity ?? 1;
            PrecursorEnvelopePeakCount = envelopePeakCount ?? 1;
            PrecursorFractionalIntensity = precursorFractionalIntensity ?? -1;
            FullFilePath = fullFilePath;
            ChildScans = new List<Ms2ScanWithSpecificMass>();
            NativeId = mzLibScan.NativeId;

            TheScan = mzLibScan;

            if (commonParam.DissociationType != DissociationType.LowCID)
            {
                ExperimentalFragments = neutralExperimentalFragments ?? GetNeutralExperimentalFragments(mzLibScan, commonParam);
            }
            if (ExperimentalFragments != null && ExperimentalFragments.Any())
            {
                _deconvolutedMonoisotopicMasses = ExperimentalFragments.Select(p => p.MonoisotopicMass)
                    .OrderBy(p => p)
                    .ToArray();
            }
            else
            {
                _deconvolutedMonoisotopicMasses = new double[0];
            }
        }

        public MsDataScan TheScan { get; }
        public double PrecursorMonoisotopicPeakMz { get; }
        public double PrecursorMass { get; }
        public int PrecursorCharge { get; }
        public double PrecursorIntensity { get; }
        public int PrecursorEnvelopePeakCount { get; }
        public double PrecursorFractionalIntensity { get; }
        public string FullFilePath { get; }
        public IsotopicEnvelope[] ExperimentalFragments { get; private set; }
        public List<Ms2ScanWithSpecificMass> ChildScans { get; set; } // MS2/MS3 scans that are children of this MS2 scan
        
        public string NativeId { get; } 

        public int OneBasedScanNumber => TheScan.OneBasedScanNumber;

        public int? OneBasedPrecursorScanNumber => TheScan.OneBasedPrecursorScanNumber;

        public double RetentionTime => TheScan.RetentionTime;

        public int NumPeaks => TheScan.MassSpectrum.Size;

        public double TotalIonCurrent => TheScan.TotalIonCurrent;

        public static IsotopicEnvelope[] GetNeutralExperimentalFragments(MsDataScan scan, CommonParameters commonParam)
        {
            var neutralExperimentalFragmentMasses =
                Deconvoluter.Deconvolute(scan, commonParam.ProductDeconvolutionParameters, scan.MassSpectrum.Range).ToList();

            if (commonParam.AssumeOrphanPeaksAreZ1Fragments)
            {
                HashSet<double> alreadyClaimedMzs = new HashSet<double>(neutralExperimentalFragmentMasses
                    .SelectMany(p => p.Peaks.Select(v => v.mz.RoundedDouble()!.Value)));

                for (int i = 0; i < scan.MassSpectrum.XArray.Length; i++)
                {
                    double mz = scan.MassSpectrum.XArray[i];
                    double intensity = scan.MassSpectrum.YArray[i];

                    if (!alreadyClaimedMzs.Contains(mz.RoundedDouble()!.Value))
                    {
                        neutralExperimentalFragmentMasses.Add(new IsotopicEnvelope(
                            new List<(double mz, double intensity)> { (mz, intensity) },
                            mz.ToMass(1), 1, intensity, 0));
                    }
                }
            }

            return neutralExperimentalFragmentMasses.OrderBy(p => p.MonoisotopicMass).ToArray();
        }

        public IsotopicEnvelope? GetClosestExperimentalIsotopicEnvelope(double theoreticalNeutralMass)
        {
            return _deconvolutedMonoisotopicMasses.Length == 0 
                ? null 
                : ExperimentalFragments[GetClosestFragmentMassIndex(theoreticalNeutralMass)];
        }

        private int GetClosestFragmentMassIndex(double mass)
        {
            if (_deconvolutedMonoisotopicMasses.Length == 0)
                return -1;

            // If the value is smaller than the first element, return the first element
            if (mass < _deconvolutedMonoisotopicMasses[0])
                return 0;

            // Find the range for binary search by repeated doubling
            int i = 1;
            while (i < _deconvolutedMonoisotopicMasses.Length && _deconvolutedMonoisotopicMasses[i] <= mass)
                i = i * 2;

            // Call binary search for the found range
            int low = i / 2;
            int high = Math.Min(i, _deconvolutedMonoisotopicMasses.Length);
            int index = Array.BinarySearch(_deconvolutedMonoisotopicMasses, low, high - low, mass);

            if (index >= 0)
            {
                return index;
            }
            index = ~index;

            if (index == _deconvolutedMonoisotopicMasses.Length)
            {
                return index - 1;
            }
            if (index == 0 || mass - _deconvolutedMonoisotopicMasses[index - 1] > _deconvolutedMonoisotopicMasses[index] - mass)
            {
                return index;
            }

            return index - 1;
        }

        //look for IsotopicEnvelopes which are in the range of acceptable mass 
        public IsotopicEnvelope[] GetClosestExperimentalIsotopicEnvelopeList(double minimumMass, double maxMass)
        {

            if (_deconvolutedMonoisotopicMasses.Length == 0)
            {
                return null;
            }

            //if no mass is in this range, then return null
            if (_deconvolutedMonoisotopicMasses[0] > maxMass || _deconvolutedMonoisotopicMasses.Last() < minimumMass)
            {
                return null;
            }

            int startIndex = GetClosestFragmentMassIndex(minimumMass);
            int endIndex = GetClosestFragmentMassIndex(maxMass);

            //the index we get from GetClosestFragmentMassIndex is the closest mass, while the acceptable mass we need is between minimumMass and maxMass
            //so the startIndex mass is supposed to be larger than minimumMass, if not , then startIndex increases by 1;
            //the endIndex mass is supposed to be smaller than maxMass, if not , then endIndex decreases by 1;
            if (_deconvolutedMonoisotopicMasses[startIndex]<minimumMass)
            {
                startIndex = startIndex+1;
            }
            if(_deconvolutedMonoisotopicMasses[endIndex] > maxMass)
            {
                endIndex = endIndex - 1;
            }
            int length = endIndex - startIndex + 1;

            if (length < 1)
            {
                return null;
            }
            IsotopicEnvelope[] isotopicEnvelopes = ExperimentalFragments.Skip(startIndex).Take(length).ToArray();
            return isotopicEnvelopes;
        }
    }
}