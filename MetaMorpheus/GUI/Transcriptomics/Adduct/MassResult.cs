using System;
using System.Collections.Generic;
using Chemistry;

namespace MetaMorpheusGUI
{
    public class MassResult
    {
        public List<Adduct> AdductCombination { get; }
        public double TotalMass { get; }
        public Dictionary<int,double> MzValues { get; }
        public string AdductString => string.Join(" ", AdductCombination);

        public MassResult(List<Adduct> adductCombination, double totalMass, int minCharge, int maxCharge)
        {
            AdductCombination = adductCombination;
            TotalMass = totalMass;
            MzValues = new();
            var test = new double[maxCharge - minCharge + 1];
            for (int i = minCharge; i <= maxCharge; i++)
            {
                MzValues.Add(i, (TotalMass - Math.Abs(i * Constants.ProtonMass)) / Math.Abs(i));
                test[i - minCharge] = TotalMass.ToMz(i);
            }
        }

        public override string ToString()
        {
            return $"{TotalMass} {string.Join(" ", AdductCombination)}";
        }
    }
}
