namespace TaskLayer
{
    public class CalibrationParameters
    {
        public CalibrationParameters()
        {
            WriteIntermediateFiles = false;
            MinMS1IsotopicPeaksNeededForConfirmedIdentification = 3;
            MinMS2IsotopicPeaksNeededForConfirmedIdentification = 2;
            NumFragmentsNeededForEveryIdentification = 10;
            QValueCutoffForCalibratingPSMs = 0.01;
            WriteIndexedMzml = true;
            SearchType = SearchType.Classic;
            UseMonoisotopicPeakForMs1Calibration = false;
        }

        public bool WriteIntermediateFiles { get; set; }
        public bool WriteIndexedMzml { get; set; }
        public SearchType SearchType { get; set; }

        public int MinMS1IsotopicPeaksNeededForConfirmedIdentification { get; set; }
        public int MinMS2IsotopicPeaksNeededForConfirmedIdentification { get; set; }
        public int NumFragmentsNeededForEveryIdentification { get; set; }

        public double QValueCutoffForCalibratingPSMs { get; set; }

        /// <summary>
        /// When true, each MS1 calibration training point is labeled with the monoisotopic peak of the identified
        /// precursor's isotope envelope (experimental monoisotopic m/z versus its theoretical m/z), which is the
        /// quantity the search compares. When false (default), the point is labeled with the mean over every isotope
        /// peak found in the envelope. In dense multiply-charged envelopes the monoisotopic peak can sit 1-2 ppm away
        /// from the envelope mean, which leaves the monoisotopic precursor error off-centre after calibration.
        /// The isotope-peak search that confirms the envelope is the same in both modes.
        /// </summary>
        public bool UseMonoisotopicPeakForMs1Calibration { get; set; }
    }
}
