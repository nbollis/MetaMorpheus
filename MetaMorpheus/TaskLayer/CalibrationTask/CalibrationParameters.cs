using EngineLayer.Calibration;

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
            Ms1CalibrationLabel = Ms1CalibrationLabel.Envelope;
        }

        public bool WriteIntermediateFiles { get; set; }
        public bool WriteIndexedMzml { get; set; }
        public SearchType SearchType { get; set; }

        public int MinMS1IsotopicPeaksNeededForConfirmedIdentification { get; set; }
        public int MinMS2IsotopicPeaksNeededForConfirmedIdentification { get; set; }
        public int NumFragmentsNeededForEveryIdentification { get; set; }

        public double QValueCutoffForCalibratingPSMs { get; set; }

        /// <summary>
        /// What each MS1 calibration training point measures. Envelope (default) is the mean over every isotope peak
        /// found for the identified precursor; MonoisotopicPeak is the monoisotopic peak alone; SearchPrecursor is the
        /// precursor m/z the search scored for the MS2 scan against the identified sequence's monoisotopic m/z. In dense
        /// multiply-charged envelopes these quantities sit 1-2 ppm apart, so the choice decides which of them calibration
        /// centres. See <see cref="EngineLayer.Calibration.Ms1CalibrationLabel"/>.
        /// </summary>
        public Ms1CalibrationLabel Ms1CalibrationLabel { get; set; }
    }
}
