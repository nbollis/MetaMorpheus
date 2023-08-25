using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Easy.Common.Extensions;
using EngineLayer;
using FlashLFQ;
using GuiFunctions;
using MassSpectrometry;
using MathNet.Numerics;
using Proteomics.Fragmentation;

namespace MetaMorpheusGUI
{
    public class FragmentFrequencyVM : BaseViewModel
    {
        private ObservableCollection<string> psmFileList;
        public ObservableCollection<string> PsmFileList
        {
            get => psmFileList;
            set { psmFileList = value; OnPropertyChanged(nameof(PsmFileList)); }
        }


        //public ObservableCollection<DissociationType> DissociationTypes { get; set; }
        //private DissociationType dissociationType;
        //public DissociationType DissociationType
        //{
        //    get => dissociationType;
        //    set { dissociationType = value; OnPropertyChanged(nameof(DissociationType));}
        //}

        private ObservableCollection<GroupedPsmsVM> groupedPsms;
        public ObservableCollection<GroupedPsmsVM> GroupedPsms
        {
            get => groupedPsms;
            set { groupedPsms = value; OnPropertyChanged(nameof(GroupedPsms));}
        }

        private GroupedPsmsVM selectedGroup;
        public GroupedPsmsVM SelectedGroup
        {
            get => selectedGroup;
            set { selectedGroup = value; OnPropertyChanged(nameof(SelectedGroup));}
        }

        internal List<PsmFromTsv> AllPsms { get; set; }

        private bool groupByCharge;
        public bool GroupByCharge
        {
            get => groupByCharge;
            set { groupByCharge = value; OnPropertyChanged(nameof(GroupByCharge));}
        }

        public FragmentFrequencyVM()
        {
            AllPsms = new();
            PsmFileList = new();
            GroupedPsms = new();
            GroupByCharge = false;

            //DissociationType = DissociationType.HCD;
            //DissociationTypes = new ObservableCollection<DissociationType>(Enum.GetValues<DissociationType>());

            LoadPsmsCommand = new RelayCommand(LoadAllPsms);
            ResetPsmsCommand = new RelayCommand(ResetPsms);
            RunAnalysisCommand = new RelayCommand(RunAnalysis);
        }

        public ICommand LoadPsmsCommand { get; set; }
        private void LoadAllPsms()
        {
            AllPsms.Clear();
            foreach (var file in PsmFileList)
            {
                AllPsms.AddRange(PsmTsvReader.ReadTsv(file, out List<string> warnings)
                    .Where(p => p.QValue <= 0.01 && p.AmbiguityLevel == "1"));
            }
        }

        public ICommand ResetPsmsCommand { get; set; }
        private void ResetPsms()
        {
            AllPsms.Clear();
            PsmFileList.Clear();
            GroupedPsms.Clear();
        }

        public ICommand RunAnalysisCommand { get; set; }
        private void RunAnalysis()
        {
            if (GroupByCharge)
            {
                foreach (var group in AllPsms.GroupBy(p => new { p.FullSequence, p.PrecursorCharge}))
                {
                    var newGroup = new GroupedPsmsVM(group, true);
                    newGroup.FragmentAnalysis();
                    GroupedPsms.Add(newGroup);
                }
            }
            else
            {
                foreach (var group in AllPsms.GroupBy(p => p.FullSequence))
                {
                    var newGroup = new GroupedPsmsVM(group);
                    newGroup.FragmentAnalysis();
                    GroupedPsms.Add(newGroup);
                }
            }
            
        }

        internal void FileDropped(string path)
        {
            if (!path.EndsWith(".psmtsv"))
            {
                MessageBox.Show($"Only .psmtsv files supported, you added {path}");
                return;
            }
            PsmFileList.Add(path);
        }
    }



    public class GroupedPsmsVM : BaseViewModel
    {
        private List<PsmFromTsv> psms;
        public string FullSequence { get; set; }
        public string Accession { get; }
        public int Count => psms.Count;
        public double AverageScore => psms.Average(p => p.Score).Round(2);
        public double BestScore => psms.Max(p => p.Score);
        public List<Inbetweener> FrequencyDictionary { get; set; }

        public string Charge { get; }

        public class Inbetweener
        {
            public string Ion { get; set; }
            public double Percent { get; set; }
            public List<FragmentInfo> FragmentInfo { get; set; }
            public int FoundCount { get; set; }
            public Inbetweener(string ion, double percent, List<FragmentInfo> fragmentInfo, int foundCount)
            {
                Ion = ion;
                Percent = percent;
                FragmentInfo = fragmentInfo;
                FoundCount = foundCount;
            }
        }

        public class FragmentInfo
        {
            public FragmentInfo(int charge, double mz, double count, double intensity)
            {
                Charge = charge;
                Mz = mz;
                Count = count;
                Intensity = intensity;
            }

            public double Intensity { get; set; }
            public int Charge { get; set; }
            public double Mz { get; set; }
            public double Count { get; set; }
        }

        public GroupedPsmsVM(IGrouping<object, PsmFromTsv> group = null, bool byCharge = false)
        {
            psms = group?.ToList() ?? new List<PsmFromTsv>();
            FullSequence = psms?.First().FullSequence ?? "PEPTIDE";
            Accession = psms?.First().ProteinAccession ?? "Tacos";
            FrequencyDictionary = new();

            if (byCharge)
                Charge = psms?.First().PrecursorCharge.ToString() ?? "0";
            else
            {
                Charge = String.Join(",",(psms ?? throw new InvalidOperationException()).Select(p => p.PrecursorCharge).Distinct().OrderBy(p => p));
            }
        }

        internal void FragmentAnalysis()
        {
            var products = psms.SelectMany(p => p.MatchedIons.Select(m => m.NeutralTheoreticalProduct)).DistinctBy(P => P.Annotation).ToList();
            foreach (var key in products)
            {
                var found = 0;
                Dictionary<int, FragmentInfo> charges = new();
                foreach (var psm in psms)
                {
                    var matched = psm.MatchedIons.Where(ion => key.Equals(ion.NeutralTheoreticalProduct)).ToList();
                    if (!matched.Any()) continue;

                    found++;
                    foreach (var match in matched)
                    {
                        if (charges.TryGetValue(match.Charge, out var charge))
                            charge.Count++;
                        else
                            charges.Add(match.Charge, new FragmentInfo(match.Charge, match.Mz, 1, match.Intensity));
                    }
                }
                
                var percent = (found / (double)Count * 100).Round(2);
                FrequencyDictionary.Add(new Inbetweener(key.Annotation, percent, charges.Values.ToList(), found));
            }
            OnPropertyChanged(nameof(FrequencyDictionary));
        }
    }

    public class GroupedPsmsModel : GroupedPsmsVM
    {
        public static GroupedPsmsModel Instance => new GroupedPsmsModel();
        public GroupedPsmsModel()
        {

        }
    }

}
