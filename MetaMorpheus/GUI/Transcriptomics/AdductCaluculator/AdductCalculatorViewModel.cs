using GuiFunctions;
using MzLibUtil;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Chemistry;
using Transcriptomics;

namespace MetaMorpheusGUI;

public class AdductCalculatorViewModel : BaseViewModel
{
    private string _sequence { get; set; }

    public string Sequence
    {
        get => _sequence;
        set
        {
            _sequence = value;
            Rna = new(value);
            OnPropertyChanged(nameof(Sequence));
        }
    }

    private RNA _rna;
    public RNA Rna
    {
        get => _rna;
        set
        {
            _rna = value;
            OnPropertyChanged(nameof(Rna));
        }
    }

    private double _targetMz;
    public double TargetMz
    {
        get => _targetMz;
        set { _targetMz = value; OnPropertyChanged(nameof(TargetMz)); }
    }

    private int _minCharge;
    public int MinCharge
    {
        get => _minCharge;
        set { _minCharge = value; OnPropertyChanged(nameof(MinCharge)); }
    }

    private int _maxCharge;
    public int MaxCharge
    {
        get => _maxCharge;
        set { _maxCharge = value; OnPropertyChanged(nameof(MaxCharge)); }
    }

    private int _tolerance;
    public int Tolerance
    {
        get => _tolerance;
        set
        {
            _tolerance = value;
            OnPropertyChanged(nameof(Tolerance));
            PpmTolerance = new(value);
        }
    }

    private bool _findSpecific;
    public bool FindSpecific
    {
        get => _findSpecific;
        set { _findSpecific = value; OnPropertyChanged(nameof(FindSpecific)); }
    }

    public PpmTolerance PpmTolerance { get; set; }

    public ObservableCollection<AdductViewModel> Adducts { get; set; }

    public ObservableCollection<MassResult> MassResults { get; set; }



    public AdductCalculatorViewModel()
    {
        Adducts = new ObservableCollection<AdductViewModel>(Adduct.Adducts.Select(p => new AdductViewModel(p)));
        MassResults = new();
        MinCharge = -6;
        MaxCharge = -1;
        Tolerance = 50;
        FindSpecific = false;

        ClearAllDataCommand = new RelayCommand(ClearAllData);
        GetPossibleAdductsCommand = new RelayCommand(GetPossibleCombinations);
    }

    public ICommand ClearAllDataCommand { get; set; }

    private void ClearAllData()
    {
        Sequence = "";
        MassResults.Clear();
    }

    public ICommand GetPossibleAdductsCommand { get; set; }
    private void GetPossibleCombinations()
    {
        MassResults.Clear();
        double rnaMass = Rna.MonoisotopicMass;
        var adductCombinations = GenerateUniqueAdductCombinations(Adducts.ToList());
        var results = CalculateMassResults(rnaMass, adductCombinations);
        results.ForEach(p => MassResults.Add(p));
    }

    List<MassResult> CalculateMassResults(double rnaMass, List<List<Adduct>> adductCombinations)
    {
        var results = new List<MassResult>();
        // Calculate the total mass for each combination
        foreach (var combination in adductCombinations)
        {
            double totalMass = rnaMass + combination.Sum(a => a.MonoisotopicMass);
            results.Add(new MassResult(combination, totalMass, MinCharge, MaxCharge));
        }
        return results.OrderBy(p => p.TotalMass).ToList();
    }

    static List<List<Adduct>> GenerateUniqueAdductCombinations(List<AdductViewModel> adducts)
    {
        var combinations = new List<List<Adduct>>();
        GenerateUniqueAdductCombinationsRecursive(adducts, 0, new List<Adduct>(), combinations);
        return combinations;
    }

    static void GenerateUniqueAdductCombinationsRecursive(List<AdductViewModel> adducts, int index, List<Adduct> currentCombination, List<List<Adduct>> combinations)
    {
        if (index == adducts.Count)
        {
            combinations.Add(new List<Adduct>(currentCombination));
            return;
        }

        var adduct = adducts[index];

        // Include the current adduct in the combination and check the individual maximum count
        int adductCount = currentCombination.Count(a => a.Name == adduct.Name);
        if (adductCount < adduct.MaxCount)
        {
            currentCombination.Add(adduct.Adduct);
            GenerateUniqueAdductCombinationsRecursive(adducts, index, currentCombination, combinations);
            currentCombination.Remove(adduct.Adduct);
        }

        // Skip the current adduct
        GenerateUniqueAdductCombinationsRecursive(adducts, index + 1, currentCombination, combinations);
    }

}


public class AdductViewModel : BaseViewModel
{
    public Adduct Adduct { get; set; }

    private int maxCount;
    public int MaxCount
    {
        get => maxCount;
        set { maxCount = value; OnPropertyChanged(nameof(MaxCount)); }
    }

    public string Name => Adduct.Name;

    public AdductViewModel(Adduct adduct, int maxCount = 2)
    {
        MaxCount = maxCount;
        Adduct = adduct;
    }
}



public class Adduct : IHasChemicalFormula
{

    #region Static

    public static List<Adduct> Adducts => new()
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
        MonoisotopicMass = mass ?? ThisChemicalFormula.MonoisotopicMass;
    }

    public override string ToString()
    {
        return Name;
    }
}