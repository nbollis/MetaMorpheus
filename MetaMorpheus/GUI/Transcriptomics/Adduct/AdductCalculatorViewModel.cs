using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GuiFunctions;
using MzLibUtil;
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