using System;
using System.Collections.Generic;
using Chemistry;
using Omics.Fragmentation;

namespace MetaMorpheusGUI;

public class MassResult
{
    public List<Adduct> AdductCombination { get; }
    public double TotalMass { get; }
    public Dictionary<int, double> MzValues { get; }
    public string AdductString => string.Join(" ", AdductCombination);

    public Product Product { get; }

    public MassResult(List<Adduct> adductCombination, double totalMass, int minCharge, int maxCharge)
    {
        AdductCombination = adductCombination;
        TotalMass = totalMass;
        MzValues = new();
        CalculateMzValues(minCharge, maxCharge);
    }

    public MassResult(Product product, int minCharge, int maxCharge)
    {
        Product = product;
        TotalMass = Product.NeutralMass;
        MzValues = new();
        AdductCombination = new();
        CalculateMzValues(minCharge, maxCharge);
    }

    private void CalculateMzValues(int minCharge, int maxCharge)
    {
        for (int i = minCharge; i <= maxCharge; i++)
        {
            MzValues.Add(i, TotalMass.ToMz(i));
        }
    }

    public override string ToString()
    {
        return $"{TotalMass} {string.Join(" ", AdductCombination)}";
    }
}