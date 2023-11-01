using System.Collections.Generic;
using Chemistry;

namespace MetaMorpheusGUI;

public class Adduct : IHasChemicalFormula
{

    #region Static

    public static List<Adduct> Adducts => new ()
    {
        new Adduct("Na", ChemicalFormula.ParseFormula("Na1H-1")),
        new Adduct("K", ChemicalFormula.ParseFormula("K1H-1")),
    };

    #endregion


    public string Name { get; }
    public double MonoisotopicMass { get; }
    public ChemicalFormula ThisChemicalFormula { get; }

    public Adduct(string name, ChemicalFormula formula, double? mass = null)
    {
        Name = name;
        ThisChemicalFormula = formula;
        MonoisotopicMass =  mass ?? ThisChemicalFormula.MonoisotopicMass;
    }

    public override string ToString()
    {
        return Name;
    }
}