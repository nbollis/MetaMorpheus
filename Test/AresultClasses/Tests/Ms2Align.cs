using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;
using System.IO;
using Easy.Common.Extensions;
using MzLibUtil;
using static MassSpectrometry.DissociationType;

namespace Readers
{
    public enum MsAlignType
    {
        Combined = 0,
        Ms1 = 1,
        Ms2 = 2,
        Unknown,
    }

    public class Ms2Align : MsDataFile
    {

        public MsAlignType MsAlignType { get; set; }

        public Ms2Align(string filePath) : base(filePath)
        {
            Header = new();
            if (filePath.ToLower().Contains("ms1.msalign"))
            {
                MsAlignType = MsAlignType.Ms1;
            }
            else if (filePath.ToLower().Contains("ms2.msalign"))
            {
                MsAlignType = MsAlignType.Ms2;
            }
            else
            {
                MsAlignType = MsAlignType.Unknown;
            }
        }

        /// <summary>
        /// Header Properties that are outputted by MM and TopFd but not FlashDeconv
        /// </summary>
        #region Optional Ms2Align Header Properties

        public DissociationType? DissociationType { get; private set; }
        public int? Ms1ScanCount { get; private set; }
        public int? Ms2ScanCount { get; private set; }
        public string? SpectralDataType { get; private set; }
        public int? MaxAssumedChargeState { get; private set; }
        public double? MaxAssumedMonoisotopicMass { get; private set; }
        public string? PeakErrorTolerance { get; private set; }
        public double? Ms1SnRRatio { get; private set; }
        public double? Ms2SnRRatio { get; private set; }
        public double? MaxThreadsToUse { get; private set; }
        public double? PrecursorWindowSize { get; private set; }
        public bool? UseEnvCnnModel { get; private set; }
        public bool? MissMs1Spectra { get; private set; }
        public string? SoftwareVersion { get; private set; }
        public Software? Software { get; private set; }

        #endregion

        /// <summary>
        /// Enum is required as there are several different ways an msAlign header information is written
        /// </summary>
        public enum ReadingProgress
        {
            NotFound,
            Found,
            Finished
        }

        public override MsDataFile LoadAllStaticData(FilteringParams filteringParams = null, int maxThreads = 1)
        {
            // TODO: Figure out dynamic connection and have static call dynamic

            List<MsDataScan> scans = new();
            ReadingProgress headerProgress = ReadingProgress.NotFound;
            ReadingProgress entryProgress = ReadingProgress.NotFound;
            using (StreamReader sr = new StreamReader(FilePath))
            {
                string? line;
                List<string> linesToProcess = new();
                while ((line = sr.ReadLine()) is not null)
                {
                    if (line.Contains("BEGIN IONS"))
                        headerProgress = ReadingProgress.Finished;

                    // get header
                    if (headerProgress != ReadingProgress.Finished)
                    {
                        if (headerProgress == ReadingProgress.NotFound && line.Contains("##### Parameters #####"))
                            headerProgress = ReadingProgress.Found;
                        else if (headerProgress == ReadingProgress.Found && line.Contains("##### Parameters #####"))
                        {
                            headerProgress = ReadingProgress.Finished;
                            ParseHeaderLines(linesToProcess); // TODO: this method
                            linesToProcess.Clear();
                        }
                        else
                        {
                            linesToProcess.Add(line);
                            continue;
                        }
                    }
                    else
                    {
                        // each entry after header
                        if (entryProgress == ReadingProgress.NotFound && line.Contains("BEGIN IONS"))
                            entryProgress = ReadingProgress.Found;
                        else if (entryProgress == ReadingProgress.Found && line.Contains("END IONS"))
                        {
                            entryProgress = ReadingProgress.NotFound;
                            scans.Add(ParseEntryLines(linesToProcess)); // TODO: this method
                            linesToProcess.Clear();
                        }
                        else
                        {
                            linesToProcess.Add(line);
                        }
                    }
                }
            }

            Scans = scans.ToArray();
            return this;
        }

        public override SourceFile GetSourceFile()
        {
            throw new NotImplementedException();
        }

        public override MsDataScan GetOneBasedScanFromDynamicConnection(int oneBasedScanNumber, IFilteringParams filterParams = null)
        {
            throw new NotImplementedException();
        }

        public override void CloseDynamicConnection()
        {
            throw new NotImplementedException();
        }

        public override void InitiateDynamicConnection()
        {
            throw new NotImplementedException();
        }


        private Dictionary<string, string> Header { get; set; }
        private void ParseHeaderLines(List<string> headerLines)
        {
            foreach (var line in headerLines.Where(p => p.Contains('\t')))
            {
                var splits = line.Split('\t');
                Header.Add(splits[0].Trim(), splits[1].Replace("m/z","").Trim());
            }
        }

        private MsDataScan ParseEntryLines(List<string> entryLines)
        {
            // all
            int id;
            int fractionId;
            string fileName;
            int oneBasedScanNumber = 0;
            double retentionTime = 0;
            int msnOrder = 0;

            // ms2
            DissociationType? dissociationType = null;
            int? precursorScanId = null;
            int? oneBasedPrecursorScanNumber = null;
            double? precursorMz = null;
            int? precursorCharge = null;
            double? precursorMass = null;
            double? precursorIntensity = null;

            foreach (var headerLine in entryLines.Where(p => p.Contains('=')))
            {
                var splits = headerLine.Split('=');
                switch (splits[0])
                {
                    case "ID":
                        id = int.Parse(splits[1]);
                        break;
                    case "FRACTION_ID":
                        fractionId = int.Parse(splits[1]);
                        break;
                    case "FILE_NAME":
                        fileName = splits[1];
                        break;
                    case "SCANS":
                        oneBasedScanNumber = int.Parse(splits[1]);
                        break;
                    case "RETENTION_TIME":
                        retentionTime = double.Parse(splits[1]);
                        break;
                    case "LEVEL":
                        msnOrder = int.Parse(splits[1]);
                        break;
                    case "ACTIVATION":
                        dissociationType = Enum.Parse<DissociationType>(splits[1]);
                        break;
                    case "MS_ONE_ID":
                        precursorScanId = int.Parse(splits[1]);
                        break;
                    case "MS_ONE_SCAN":
                        oneBasedPrecursorScanNumber = int.Parse(splits[1]);
                        break;
                    case "PRECURSOR_MZ":
                        precursorMz = double.Parse(splits[1]);
                        break;
                    case "PRECURSOR_CHARGE":
                        precursorCharge = int.Parse(splits[1]);
                        break;
                    case "PRECURSOR_MASS":
                        precursorMass = double.Parse(splits[1]);
                        break;
                    case "PRECURSOR_INTENSITY":
                        precursorIntensity = double.Parse(splits[1]);
                        break;
                }
            }

            var peakLines = entryLines.Where(p => p.Contains('\t')).ToArray();
            var mzs = new double[peakLines.Length];
            var intensities = new double[peakLines.Length];
            var charges = new int[peakLines.Length];

            for (int i = 0; i < peakLines.Length; i++)
            {
                var splits = peakLines[i].Split('\t');

                charges[i] = int.Parse(splits[2]);
                mzs[i] = (double.Parse(splits[0]) + charges[i]) / charges[i];
                intensities[i] = double.Parse(splits[1]);
            }

            var spectrum = new MzSpectrum(mzs, intensities, true);


            // TODO replace the below with info from the header
            // centroided, precursor window, mzrange
            var t = new MsDataScan(spectrum, oneBasedScanNumber, msnOrder, true, Polarity.Positive, retentionTime,
                mzs.Any() ? new MzRange(mzs.Min(), mzs.Max()) : new MzRange(0, 2000), null, MZAnalyzerType.Orbitrap,
                intensities.Sum(), null, null, null, precursorMz, 
                precursorCharge, precursorIntensity, precursorMz,
                Header.TryGetValue("Precursor window size:", out string value) ? double.Parse(value) : 3,
                dissociationType, oneBasedPrecursorScanNumber, null, null);

            return t;
        }


    }
}
