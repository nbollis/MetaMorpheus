using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using EngineLayer.PrecursorSearchModes;
using TaskLayer;

namespace GuiFunctions;

public class MassDifferenceAcceptorViewModel : BaseViewModel
{
    private int _maxAdductsPerNotch = 3;
    private string _cachedCustomMdac = string.Empty;
    private string _cachedAdductMdac = string.Empty;
    public ObservableCollection<SelectableAdduct> PredefinedAdducts { get; }
    public ObservableCollection<MassDifferenceAcceptorTypeModel> MassDiffAcceptorTypes { get; }
    public MassDifferenceAcceptorViewModel(MassDiffAcceptorType selectedType, string customText) : base()
    {
        // Almost every piece of this constructor is order dependent. Be careful when changing. 
        var models = Enum.GetValues<MassDiffAcceptorType>().Select(CreateModel);
        MassDiffAcceptorTypes = new ObservableCollection<MassDifferenceAcceptorTypeModel>(models);
        SelectedType = MassDiffAcceptorTypes.FirstOrDefault(m => m.Type == selectedType) ?? MassDiffAcceptorTypes.First();

        PredefinedAdducts = new ObservableCollection<SelectableAdduct>
        {
            new SelectableAdduct("Na\u207a", 21.981943),
            new SelectableAdduct("K\u207a", 37.955882),
            // Add more as needed
        };

        // subscribe the adducts to update the mdac string upon properties changing. 
        foreach (var adduct in PredefinedAdducts)
        {
            adduct.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(SelectableAdduct.IsSelected) or nameof(SelectableAdduct.MaxFrequency))
                {
                    UpdateCustomMdacFromAdducts();
                    OnPropertyChanged(nameof(AdductNotchCount));
                }
            };
        }

        // Parse adduct string if type is Adduct
        if (selectedType == MassDiffAcceptorType.Adduct && !string.IsNullOrWhiteSpace(customText))
        {
            // Format: "Na⁺:1:21.981943,K⁺:2:37.955882;3"
            var parts = customText.Split(';');
            var adductPart = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out int maxAdducts))
                MaxAdductsPerNotch = maxAdducts;

            var adductStrings = adductPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var adductString in adductStrings)
            {
                var adductFields = adductString.Split(':');
                if (adductFields.Length == 3)
                {
                    var name = adductFields[0];
                    if (int.TryParse(adductFields[1], out int freq))
                    {
                        // Find by name (match unicode superscript if present)
                        var match = PredefinedAdducts.FirstOrDefault(a => a.Name.Contains(name));
                        if (match != null)
                        {
                            match.IsSelected = true;
                            match.MaxFrequency = freq;
                        }
                    }
                }
            }
        }

        CustomMdac = customText;
    }

    public int AdductNotchCount
    {
        get
        {
            var selected = PredefinedAdducts.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0)
                return 1;
            var combinations = new HashSet<string>();
            void Recurse(int depth, int[] counts, int totalAdducts)
            {
                if (totalAdducts > MaxAdductsPerNotch)
                    return;
                if (depth == selected.Count)
                {
                    var desc = string.Concat(
                        selected.Select((a, idx) => counts[idx] > 0 ? $"{a.Name}{counts[idx]}" : "")
                    );
                    combinations.Add(string.IsNullOrEmpty(desc) ? "Unadducted" : desc);
                    return;
                }
                for (int i = 0; i <= selected[depth].MaxFrequency; i++)
                {
                    counts[depth] = i;
                    Recurse(depth + 1, counts, totalAdducts + i);
                }
            }
            Recurse(0, new int[selected.Count], 0);
            return combinations.Count;
        }
    }

    public int MaxAdductsPerNotch
    {
        get => _maxAdductsPerNotch;
        set
        {
            if (_maxAdductsPerNotch != value)
            {
                _maxAdductsPerNotch = value;
                OnPropertyChanged(nameof(MaxAdductsPerNotch));
                UpdateCustomMdacFromAdducts();
                OnPropertyChanged(nameof(AdductNotchCount));
            }
        }
    }

    private MassDifferenceAcceptorTypeModel _selectedType;
    public MassDifferenceAcceptorTypeModel SelectedType
    {
        get => _selectedType;
        set
        {
            if (_selectedType != null) // only true during initialization when no cache is present. 
            {
                switch (_selectedType.Type)
                {
                    // if moving away from a string input type, cache it
                    case MassDiffAcceptorType.Custom:
                        _cachedCustomMdac = CustomMdac;
                        CustomMdac = string.Empty;
                        break;
                    case MassDiffAcceptorType.Adduct:
                        _cachedAdductMdac = CustomMdac;
                        CustomMdac = string.Empty;
                        break;
                }
            }

            _selectedType = value;
            OnPropertyChanged(nameof(SelectedType));

            // if moving to string input type, populate mdac with cached string
            if (_selectedType.Type == MassDiffAcceptorType.Custom)
                CustomMdac = _cachedCustomMdac;
            else if (_selectedType.Type == MassDiffAcceptorType.Adduct)
                CustomMdac = _cachedAdductMdac;
        }
    }

    private string _customMdac = string.Empty;
    public string CustomMdac
    {
        get => _customMdac;
        set
        {
            _customMdac = value;
            OnPropertyChanged(nameof(CustomMdac));
        }
    }

    private MassDifferenceAcceptorTypeModel CreateModel(MassDiffAcceptorType type)
    {
        string label = type switch
        {
            MassDiffAcceptorType.Exact => "Exact",
            MassDiffAcceptorType.OneMM => "1 Missed Monoisotopic Peak",
            MassDiffAcceptorType.TwoMM => "1 or 2 Missed Monoisotopic Peaks",
            MassDiffAcceptorType.ThreeMM => "1, 2, or 3 Missed Monoisotopic Peaks",
            MassDiffAcceptorType.PlusOrMinusThreeMM => "+- 3 Missed Monoisotopic Peaks",
            MassDiffAcceptorType.ModOpen => "-187 and Up",
            MassDiffAcceptorType.Open => "Accept all",
            MassDiffAcceptorType.Custom => "Custom",
            MassDiffAcceptorType.Adduct => "Adduct",
            _ => throw new NotImplementedException(),
        };

        string toolTip = type switch
        {
            MassDiffAcceptorType.Exact => "Basic search where the observed and theoretical precursor masses must be equal (~0 Da precursor mass-difference). This search type assumes that there are no monoisotopic errors.",
            MassDiffAcceptorType.OneMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1 Da to allow for a 1 Da monoisotopic mass error.",
            MassDiffAcceptorType.TwoMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1 or 2 Da to allow for a 1 or 2 Da monoisotopic mass error.",
            MassDiffAcceptorType.ThreeMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1, 2, or 3 Da to allow for a 1, 2, or 3 Da monoisotopic mass error.",
            MassDiffAcceptorType.PlusOrMinusThreeMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by +-1, +-2, or +-3 Da in to allow for monoisotopic mass errors.",
            MassDiffAcceptorType.ModOpen => "An \"open-mass\" search that allows mass-differences between observed and theoretical precursor masses of -187 Da to infinity (observed can be infinitely more massive than the theoretical).\r\nThe purpose of this search type is to detect mass-differences corresponding to PTMs, amino acid variants, sample handling artifacts, etc.\r\nPlease use \"Modern Search\" mode when using this search type.",
            MassDiffAcceptorType.Open => "An \"open-mass\" search that allows mass-differences between observed and theoretical precursor masses of -infinity to infinity. The purpose of this search type is to detect mass-differences corresponding to PTMs, amino acid variants, sample handling artifacts, etc. Please use \"Modern Search\" mode when using this search type.",
            MassDiffAcceptorType.Custom => "A custom mass difference acceptor may be specified in multiple ways: * To accept a custom (other than the interval corresponding to the precursor tolerance) interval around zero daltons, specify a custom name, followed by \"ppmAroundZero\" or \"daltonsAroundZero\", followed by the numeric value corresponding to the interval width. Examples: * CustomPpmInterval ppmAroundZero 5 * CustomDaltonInterval daltonsAroundZero 2.1 * To accept a variety of pre-specified mass differences, use a custom name, followed by \"dot\", followed by a custom bin width, followed by comma separated acceptable mass differences. Examples: * CustomMissedIsotopePeaks dot 5 ppm 0,1.0029,2.0052 * CustomOxidationAllowed dot 0.1 da 0,16 * To accept mass differences in pre-specified dalton intervals, use a custom name, followed by \"interval\", followed by comma separated mass intervals in brackets. Example: * CustomPositiveIntervalAcceptror interval [0,200]",
            MassDiffAcceptorType.Adduct => "An \"adduct\" search that allows mass-differences between observed and theoretical precursor masses within Precursor Mass Tolerance. The purpose of this search type is to detect mass-differences corresponding to adducts and treat those as the precursor mass.",
            _ => throw new NotImplementedException(),
        };

        int positiveMissedMonos = type switch
        {
            MassDiffAcceptorType.Exact => 0,
            MassDiffAcceptorType.OneMM => 1,
            MassDiffAcceptorType.TwoMM => 2,
            MassDiffAcceptorType.ThreeMM => 3,
            MassDiffAcceptorType.PlusOrMinusThreeMM => 3,
            MassDiffAcceptorType.ModOpen => 0,
            MassDiffAcceptorType.Open => 0,
            MassDiffAcceptorType.Custom => 0,
            MassDiffAcceptorType.Adduct => 0,
            _ => throw new NotImplementedException(),
        };

        int negativeMissedMonos = type switch
        {
            MassDiffAcceptorType.Exact => 0,
            MassDiffAcceptorType.OneMM => 0,
            MassDiffAcceptorType.TwoMM => 0,
            MassDiffAcceptorType.ThreeMM => 0,
            MassDiffAcceptorType.PlusOrMinusThreeMM => 3,
            MassDiffAcceptorType.ModOpen => 0,
            MassDiffAcceptorType.Open => 0,
            MassDiffAcceptorType.Custom => 0,
            MassDiffAcceptorType.Adduct => 0,
            _ => throw new NotImplementedException(),
        };

        return new MassDifferenceAcceptorTypeModel
        {
            Type = type,
            Label = label,
            ToolTip = toolTip,
            PositiveMissedMonos = positiveMissedMonos,
            NegativeMissedMonos = negativeMissedMonos
        };
    }

    private void UpdateCustomMdacFromAdducts()
    {
        if (SelectedType.Type == MassDiffAcceptorType.Adduct)
        {
            var selected = PredefinedAdducts
                .Where(a => a.IsSelected)
                    .Select(a => $"{a.Name[..^1]}:{a.MaxFrequency}:{a.MonoisotopicMass}")
                .ToList();

            CustomMdac = string.Join(",", selected) + $";{MaxAdductsPerNotch}";
        }
    }
}



public class MassDifferenceAcceptorTypeModel : IEquatable<MassDiffAcceptorType>, IEquatable<MassDifferenceAcceptorTypeModel>
{
    public int PositiveMissedMonos { get; set; }
    public int NegativeMissedMonos { get; set; }
    public MassDiffAcceptorType Type { get; set; }
    public string Label { get; set; }
    public string ToolTip { get; set; }

    public bool Equals(MassDifferenceAcceptorTypeModel other)
    {
        return Type == other.Type;
    }

    public bool Equals(MassDiffAcceptorType other)
    {
        return Type == other;
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((MassDifferenceAcceptorTypeModel)obj);
    }

    public override int GetHashCode()
    {
        return (int)Type;
    }
}

[ExcludeFromCodeCoverage]
public class MassDifferenceAcceptorModel : MassDifferenceAcceptorViewModel
{
    public static MassDifferenceAcceptorModel Instance => new MassDifferenceAcceptorModel();
    public MassDifferenceAcceptorModel() : base(MassDiffAcceptorType.TwoMM, "")
    {
    
    }
}

public class SelectableAdduct : BaseViewModel
{
    public string Name { get; }
    public double MonoisotopicMass { get; }
    private int _maxFrequency;
    private bool _isSelected;

    public int MaxFrequency
    {
        get => _maxFrequency;
        set { _maxFrequency = value; OnPropertyChanged(nameof(MaxFrequency)); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public SelectableAdduct(string name, double mass, int maxFrequency = 1)
    {
        Name = name;
        MonoisotopicMass = mass;
        MaxFrequency = maxFrequency;
        IsSelected = false;
    }
}
