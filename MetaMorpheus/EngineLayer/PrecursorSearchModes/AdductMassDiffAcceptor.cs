using System;
using Chemistry;
using MzLibUtil;
using System.Collections.Generic;
using System.Linq;

namespace EngineLayer.PrecursorSearchModes;

public class Adduct : IHasMass
{
    public string Name { get; set; }
    public int MaxFrequency { get; }
    public double MonoisotopicMass { get; }

    public Adduct(string name, int maxFrequency, double monoIsotopicMass)
    {
        Name = name;
        MaxFrequency = maxFrequency;
        MonoisotopicMass = monoIsotopicMass;
    }

    public override string ToString()
    {
        return $"{Name}:{MaxFrequency}:{MonoisotopicMass}";
    }

    public static Adduct FromString(string adductString)
    {
        var parts = adductString.Split(':');
        if (parts.Length != 3)
            throw new ArgumentException($"Invalid adduct string: {adductString}");

        string name = parts[0];
        int maxFrequency = int.Parse(parts[1]);
        double mass = double.Parse(parts[2]);
        return new Adduct(name, maxFrequency, mass);
    }
}

public class AdductMassDiffAcceptor : MassDiffAcceptor
{
    protected readonly List<Adduct> Adducts;
    protected readonly List<(double mass, string description)> AdductCombinations;
    protected readonly Tolerance Tolerance;
    protected readonly int MaxAdductsPerNotch;

    /// <summary>
    /// Expose adduct combination descriptions by notch index
    /// </summary>
    public IReadOnlyList<string> NotchDescriptions { get; }

    public AdductMassDiffAcceptor(
        IEnumerable<Adduct> adducts,
        Tolerance tolerance,
        int maxAdductsPerNotch = 3)
        : base("_Adducts")
    {
        Adducts = adducts.ToList();
        Tolerance = tolerance;
        MaxAdductsPerNotch = maxAdductsPerNotch;
        AdductCombinations = GenerateAdductCombinations();
        NumNotches = AdductCombinations.Count;
        NotchDescriptions = AdductCombinations.Select(c => string.IsNullOrEmpty(c.description) ? "Unadducted" : c.description).ToList();
    }

    /// <summary>
    /// If acceptable, returns 0 or greater (the index of the given notch), negative means does not accept
    /// </summary>
    public override int Accepts(double scanPrecursorMass, double peptideMass)
    {
        for (int i = 0; i < AdductCombinations.Count; i++)
        {
            var (adductMass, _) = AdductCombinations[i];
            double expectedMass = peptideMass + adductMass;
            if (Tolerance.Within(scanPrecursorMass, expectedMass))
            {
                return i;
            }
        }
        return -1;
    }

    public override IEnumerable<AllowedIntervalWithNotch> GetAllowedPrecursorMassIntervalsFromTheoreticalMass(double peptideMonoisotopicMass)
    {
        for (int i = 0; i < AdductCombinations.Count; i++)
        {
            var (adductMass, _) = AdductCombinations[i];
            var min = Tolerance.GetMinimumValue(peptideMonoisotopicMass + adductMass);
            var max = Tolerance.GetMaximumValue(peptideMonoisotopicMass + adductMass);
            yield return new AllowedIntervalWithNotch(min, max, i);
        }
    }

    public override IEnumerable<AllowedIntervalWithNotch> GetAllowedPrecursorMassIntervalsFromObservedMass(double observedMass)
    {
        for (int i = 0; i < AdductCombinations.Count; i++)
        {
            var (adductMass, _) = AdductCombinations[i];
            var min = Tolerance.GetMinimumValue(observedMass - adductMass);
            var max = Tolerance.GetMaximumValue(observedMass - adductMass);
            yield return new AllowedIntervalWithNotch(min, max, i);
        }
    }

    public override string ToProseString()
    {
        return $"{Tolerance} around adducts: {string.Join(", ", Adducts.Select(a => $"{a.Name} (max {a.MaxFrequency})"))}";
    }

    private List<(double mass, string description)> GenerateAdductCombinations()
    {
        var combinations = new List<(double, string)>();

        void Recurse(int depth, int[] counts, double totalMass, int totalAdducts)
        {
            if (totalAdducts > MaxAdductsPerNotch)
                return;

            if (depth == Adducts.Count)
            {
                var desc = string.Concat(
                    Adducts.Select((a, idx) => counts[idx] > 0 ? $"{a.Name}{counts[idx]}" : "")
                );
                combinations.Add((totalMass, string.IsNullOrEmpty(desc) ? "Unadducted" : desc));
                return;
            }
            for (int i = 0; i <= Adducts[depth].MaxFrequency; i++)
            {
                counts[depth] = i;
                Recurse(depth + 1, counts, totalMass + i * Adducts[depth].MonoisotopicMass, totalAdducts + i);
            }
        }

        Recurse(0, new int[Adducts.Count], 0.0, 0);
        return combinations.OrderBy(p => p.Item1).ToList();
    }

    public override string ToString()
    {
        return string.Join(",", Adducts) + $";{MaxAdductsPerNotch}";
    }

    public static AdductMassDiffAcceptor FromString(string tomlString, Tolerance tolerance)
    {
        var splits = tomlString.Split(';');
        if (splits.Length != 2)
            throw new ArgumentException($"Invalid adduct string: {tomlString}");

        var maxAdductsPerNotch = int.Parse(splits[1]);
        var adductString = splits[0];

        var adducts = adductString.Split(',')
            .Select(Adduct.FromString)
            .ToList();
        return new AdductMassDiffAcceptor(adducts, tolerance, maxAdductsPerNotch);
    }
}
