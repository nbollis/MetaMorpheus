﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Markup;
using Chemistry;
using MassSpectrometry;
using MzLibUtil;
using ThermoFisher.CommonCore.Data.Business;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test")]
namespace EngineLayer.Util
{
    /// <summary>
    /// Stores a set of unique <see cref="Precursor"/> instances, where uniqueness
    /// is defined by m/absCharge within a specified tolerance.
    /// </summary>
    public class PrecursorSet : IEnumerable<Precursor>
    {
        private static ListPool<Precursor> _listPool = new(10);
        public const double MzScaleFactor = 100.0; // Used to convert m/z to an integer key by multiplying and rounding
        public readonly Tolerance Tolerance;
        /// <summary>
        /// This dictionary contains precursors indexed by their integer representation.
        /// The integer representation is calculated as the rounded m/absCharge value multiplied by 100.
        /// </summary>
        internal Dictionary<int, List<Precursor>> PrecursorDictionary { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrecursorSet"/> class.
        /// </summary>
        /// <param name="mzTolerance">
        /// The m/absCharge tolerance for determining if two precursors are considered equivalent.
        /// Must be positive.
        /// </param>
        public PrecursorSet(Tolerance tolerance)
        {
            Tolerance = tolerance;
            PrecursorDictionary = new Dictionary<int, List<Precursor>>();
        }

        public bool IsDirty { get; private set; } = false;

        /// <summary>
        /// Gets the number of precursors in the set.
        /// </summary>
        public int Count => PrecursorDictionary.Sum(kvp => kvp.Value.Count);

        /// <summary>
        /// Attempts to add a precursor to the set.
        /// </summary>
        /// <param name="precursor">The precursor to add.</param>
        /// <returns>
        /// True if the precursor was added; false if an equivalent precursor already exists.
        /// </returns>
        public bool Add(Precursor precursor)
        {
            if (precursor == null)
                return false;

            var integerKey = (int)Math.Round(precursor.MonoisotopicPeakMz * MzScaleFactor);
            if (!PrecursorDictionary.TryGetValue(integerKey, out var precursorsInBucket))
            {
                precursorsInBucket = new List<Precursor>();
                PrecursorDictionary[integerKey] = precursorsInBucket;
            }
            // Add the precursor to the appropriate bucket
            precursorsInBucket.Add(precursor);

            IsDirty = true;
            return true;
        }

        /// <summary>
        /// Checks whether the set contains an equivalent precursor within the specified m/absCharge tolerance.
        /// </summary>
        /// <param name="precursor">The precursor to compare.</param>
        /// <returns>True if an equivalent precursor exists; otherwise, false.</returns>
        public bool ContainsEquivalent(Precursor precursor, out int integerKey)
        {
            if(precursor == null)
            {
                integerKey = 0;
                return false;
            }

            integerKey = (int)Math.Round(precursor.MonoisotopicPeakMz * MzScaleFactor);

            for (int i = integerKey - 1; i <= integerKey + 1; i++)
            {
                if(PrecursorDictionary.TryGetValue(i, out var precursorsInBucket))
                {
                    if (precursorsInBucket.Any(existingPrecursor => existingPrecursor.Equals(precursor, Tolerance)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void Sanitize()
        {
            if (!IsDirty)
                return;

            // Aggregate all precursors into a single list
            var allPrecursors = _listPool.Get();
            allPrecursors.AddRange(PrecursorDictionary.Values.SelectMany(list => list));

            if (allPrecursors.Count > 0)
            {
                MergeSplitEnvelopes(in allPrecursors, Tolerance);
                RemoveLowHarmonics(in allPrecursors, Tolerance);
                RemoveDuplicates(in allPrecursors);
                RepopulateDictionary(in allPrecursors); // Call this one last as it cleans and repopulates the dictionary
            }

            IsDirty = false;
            _listPool.Return(allPrecursors);
        }

        #region Santization 

        public void RepopulateDictionary(in List<Precursor> allPrecursors)
        {
            // Re-bin the unique precursors
            PrecursorDictionary.Clear();
            foreach (var precursor in allPrecursors)
            {
                Add(precursor);
            }
        }

        /// <summary>
        /// Currently only takes the first, future work would be to merge them
        /// </summary>
        public void RemoveDuplicates(in List<Precursor> allPrecursors)
        {
            // Remove duplicates across all bins
            var uniquePrecursors = _listPool.Get();
            foreach (var precursor in allPrecursors)
            {
                bool isDuplicate = uniquePrecursors.Any(existing => precursor.Equals(existing, Tolerance));
                if (!isDuplicate)
                {
                    uniquePrecursors.Add(precursor);
                }
            }

            allPrecursors.Clear();
            allPrecursors.AddRange(uniquePrecursors);
            _listPool.Return(uniquePrecursors);
        }

        /// <summary>
        /// Cleans up precursors and merges those that should be a part of the same envelope. 
        /// Handles:
        ///   1) Split envelopes (two partial envelopes that are actually sequential)
        ///   2) Single-peak orphans (e.g., absCharge=1 and Peaks.Count == 1) that sit one expectedIsotopicSpacing away
        /// Criterion: last peak of left and first peak of right are separated by ~ (C13-C12)/absCharge within Tolerance.
        /// </summary>
        public static void MergeSplitEnvelopes(in List<Precursor> allPrecursors, Tolerance tolerance)
        {
            if (allPrecursors == null || allPrecursors.Count < 2)
                return;

            // Only consider items with an envelope
            var groups = allPrecursors
                .Where(p => p.Envelope != null && p.Envelope.Peaks.Count > 0)
                .GroupBy(p => p.Charge);

            var list = _listPool.Get();
            // We'll rebuild 'allPrecursors' in place by removing mergedPeaks-rights as we go
            foreach (var g in groups)
            {
                list.Clear();
                int charge = g.Key;
                int absCharge = Math.Abs(charge);
                double spacing = Constants.C13MinusC12 / absCharge;

                // Order by monoisotopic m/z then envelope lowest m/z so "left" is the earlier envelope
                list.AddRange(g.OrderBy(p => p.MonoisotopicPeakMz)
                    .ThenBy(p => p.Envelope!.Peaks.Min(m => m.mz)));

                bool mergedSomething;
                do
                {
                    mergedSomething = false;

                    for (int i = 0; i < list.Count - 1; i++)
                    {
                        var left = list[i];

                        // Try to merge immediate successors while possible
                        int j = i + 1;
                        while (j < list.Count)
                        {
                            var right = list[j];

                            // Require same absolute charge for stitching
                            if (Math.Abs(right.Charge) != absCharge)
                            {
                                j++;
                                continue;
                            }

                            if (AreSequentialAtSpacing(left, right, spacing, tolerance))
                            {
                                // Build mergedPeaks envelope
                                var mergedPrecursor = MergePrecursors(left, right, charge, tolerance);

                                // Remove both from lists
                                allPrecursors.Remove(left);
                                allPrecursors.Remove(right);
                                list.RemoveAt(j); // Remove right first
                                list.RemoveAt(i); // Remove left

                                // Insert merged precursor at position k
                                list.Insert(i, mergedPrecursor);
                                allPrecursors.Add(mergedPrecursor);

                                mergedSomething = true;
                                // The new merged precursor is now at position k, so the next iteration will use it as 'left'
                            }
                            else
                            {
                                // Short-circuit if we've moved far beyond plausible adjacency
                                if (left.Envelope!.Peaks.Max(p => p.mz) - right.Envelope.Peaks.Min(p => p.mz) > 2 * spacing)
                                    break;

                                j++;
                            }
                        }
                    }

                } while (mergedSomething);
            }

            _listPool.Return(list);
        }

        /// <summary>
        /// Harmonics occur when a precursor appears at absCharge and also at an integer divisor of absCharge due to peak skipping (e.g., 15 vs 5).
        /// We keep the higher charge and remove the lower if they project to each other's m/absCharge within Tolerance.
        /// Uses .ToMass(charge) / .ToMz(charge).
        /// </summary>
        public static void RemoveLowHarmonics(in List<Precursor> allPrecursors, Tolerance tolerance)
        {
            if (allPrecursors == null || allPrecursors.Count < 2)
                return;

            var toRemove = new HashSet<Precursor>();
            var ordered = _listPool.Get();
            ordered.AddRange(allPrecursors
                .OrderBy(p => p.MonoisotopicPeakMz.ToMass(Math.Abs(p.Charge))));

            for (int i = 0; i < ordered.Count; i++)
            {
                var high = ordered[i];
                int zhi = Math.Abs(high.Charge);
                if (zhi < 2) continue;

                var highPeaks = high.Envelope?.Peaks;
                if (highPeaks == null || highPeaks.Count < 2) continue;

                for (int j = 0; j < ordered.Count; j++)
                {
                    if (i == j) continue;
                    var low = ordered[j];
                    int zlo = Math.Abs(low.Charge);
                    if (zlo < 2 || zhi % zlo != 0 || zlo == zhi) continue;

                    var lowPeaks = low.Envelope?.Peaks;
                    if (lowPeaks == null || lowPeaks.Count < 2) continue;

                    int step = zhi / zlo;
                    int maxStart = highPeaks.Count / 2;

                    // Try to align lowPeaks to any subsequence of highPeaks with correct spacing
                    for (int start = 0; start < maxStart; start++)
                    {
                        int matches = 0;
                        for (int k = 0; k < lowPeaks.Count; k++)
                        {
                            int highIdx = start + k * step;
                            if (highIdx >= highPeaks.Count) break;
                            if (tolerance.Within(lowPeaks[k].mz, highPeaks[highIdx].mz))
                                matches++;
                        }

                        // If majority of low's peaks match, mark as harmonic
                        if (matches >= lowPeaks.Count / 2 && matches > 1)
                        {
                            toRemove.Add(low);
                            break; // No need to check other alignments
                        }
                    }
                }
            }

            if (toRemove.Count > 0)
                allPrecursors.RemoveAll(toRemove.Contains);

            _listPool.Return(ordered);
        }

        /// <summary>
        /// True if 'right' begins exactly one isotopic step after 'left' (sequential boundary),
        /// tested using the configured Tolerance: Within(leftLast + expectedIsotopicSpacing, rightFirst).
        /// </summary>
        public static bool AreSequentialAtSpacing(Precursor left, Precursor right, double expectedIsotopicSpacing, Tolerance tolerance)
        {
            var leftEnvelope = left.Envelope;
            var rightEnvelope = right.Envelope;

            if (leftEnvelope == null || rightEnvelope == null || leftEnvelope.Peaks.Count == 0 || rightEnvelope.Peaks.Count == 0 || left.Charge != right.Charge)
                return false;

            // Find min/max 
            double leftLast = double.MinValue;
            foreach (var pk in leftEnvelope.Peaks)
                if (pk.mz > leftLast) leftLast = pk.mz;

            double rightFirst = double.MaxValue;
            foreach (var pk in rightEnvelope.Peaks)
                if (pk.mz < rightFirst) rightFirst = pk.mz;

            // "Correct expectedIsotopicSpacing" evaluated in m/absCharge space with your Tolerance
            return tolerance.Within(leftLast + expectedIsotopicSpacing, rightFirst);
        }

        // Merge right into left by creating a *new* IsotopicEnvelope with stitched peaks
        public static Precursor MergePrecursors(Precursor left, Precursor right, int charge, Tolerance tolerance)
        {
            // TODO: Handle the case of precursors without envelopes. 
            var leftEnvelope = left.Envelope;
            var rightEnvelope = right.Envelope;
            var lp = leftEnvelope!.Peaks.OrderBy(p => p.mz).ToList();
            var rp = rightEnvelope!.Peaks.OrderBy(p => p.mz).ToList();

            // Stitch with boundary de-duplication (if any boundary peaks overlap within tolerance)
            var mergedPeaks = new List<(double mz, double intensity)>(lp.Count + rp.Count);
            mergedPeaks.AddRange(lp);

            foreach (var pk in rp)
            {
                if (mergedPeaks.Count > 0 && tolerance.Within(mergedPeaks[^1].mz, pk.mz))
                {
                    // Prefer higher intensity at same m/z
                    if (pk.intensity > mergedPeaks[^1].intensity)
                        mergedPeaks[^1] = pk;
                }
                else
                {
                    mergedPeaks.Add(pk);
                }
            }

            // Recompute attributes for the mergedPeaks envelope
            var referencePrecursor = leftEnvelope.Peaks.Max(p => p.intensity) >= rightEnvelope.Peaks.Max(p => p.intensity) ? left : right;

            // For monoisotopic mass: choose the envelope with the most intense peak, as this is what we use to calculate the averagine. 
            double monoMass = referencePrecursor.Mass;
            int id = referencePrecursor.Envelope?.PrecursorId == null ? referencePrecursor.Envelope!.PrecursorId : -1;

            // Check if we are using summed intensity or most abundant (set in common params during search). 
            double maxIntensity = referencePrecursor.Envelope.Peaks.Max(p => p.intensity);
            double mergedIntensity = Math.Abs(referencePrecursor.Intensity - maxIntensity) < 0.001
                ? maxIntensity
                : mergedPeaks.Sum(p => p.intensity);

            // For score: conservative choice—carry forward the max of both (prevents artificial inflation)
            double mergedScore = Math.Max(leftEnvelope.Score, rightEnvelope.Score);

            // FractionalIntensity: Sum if both are available, else take the one that exists, else null
            double? mergedFractionalIntensity = left.FractionalIntensity switch
            {
                not null when right.FractionalIntensity is not null => left.FractionalIntensity.Value + right.FractionalIntensity.Value,
                not null => left.FractionalIntensity.Value,
                null when right.FractionalIntensity is not null => right.FractionalIntensity.Value,
                _ => null
            };

            var mergedEnvelope = new IsotopicEnvelope(id, mergedPeaks, monoMass, charge, mergedIntensity, mergedScore);
            var mergedPrecursor = new Precursor(mergedEnvelope, mergedIntensity, mergedFractionalIntensity);
            return mergedPrecursor;
        }

        #endregion

        public void Clear()
        {
            foreach (var kvp in PrecursorDictionary)
            {
                kvp.Value.Clear();
            }
            PrecursorDictionary.Clear();
            IsDirty = false;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the precursors in the set.
        /// </summary>
        public IEnumerator<Precursor> GetEnumerator()
        {
            if (IsDirty)
                Sanitize();
            return PrecursorDictionary.SelectMany(kvp => kvp.Value).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
