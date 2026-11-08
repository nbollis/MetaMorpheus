namespace EngineLayer.Calibration
{
    /// <summary>
    /// What each MS1 calibration training point measures for an identified precursor.
    /// </summary>
    public enum Ms1CalibrationLabel
    {
        /// <summary>
        /// The mean experimental m/z of every isotope peak found in the precursor's envelope against the mean of their
        /// theoretical fine-structure centroids, one point per MS1 scan and charge state (the original behaviour).
        /// </summary>
        Envelope,

        /// <summary>
        /// The isotope peaks still confirm the envelope, but the point is the monoisotopic peak alone: its experimental
        /// m/z against the theoretical lowest-mass isotope. An envelope whose monoisotopic peak was not found gives no point.
        /// </summary>
        MonoisotopicPeak,

        /// <summary>
        /// One point per identification: the precursor monoisotopic m/z the search scored for the MS2 scan (the
        /// provided or deconvoluted precursor) against the identified sequence's monoisotopic m/z at that charge,
        /// attached to the precursor MS1 scan. This is the quantity the search's precursor tolerance is applied to.
        /// </summary>
        SearchPrecursor
    }
}
