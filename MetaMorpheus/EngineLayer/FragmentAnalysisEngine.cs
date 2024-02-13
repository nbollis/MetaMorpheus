using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Omics.Fragmentation;
using Omics.SpectrumMatch;
using Readers;
using Transcriptomics;

namespace EngineLayer
{
    public class FragmentAnalysisEngine : MetaMorpheusEngine
    {
        readonly List<string> dataFilePaths;
        private readonly string outpath;
        public FragmentAnalysisEngine(List<string> tsvPaths, string outpath) : base(new CommonParameters(), new List<(string FileName, CommonParameters Parameters)>(), new List<string>())
        {
            this.dataFilePaths = tsvPaths;
            this.outpath = outpath;
        }

        protected override MetaMorpheusEngineResults RunSpecific()
        {
            var matchesByFile = new Dictionary<string, List<SpectrumMatchFromTsv>>();
            foreach (var dataFilePath in dataFilePaths)
            {
                var key = Path.GetDirectoryName(Path.GetDirectoryName(dataFilePath)).Split('\\').Last();
                matchesByFile.Add(key, SpectrumMatchTsvReader.ReadTsv(dataFilePath, out _)
                    .Where(p => p.QValue >= 0.1).ToList());
            }
            
            // write header
            using var sw = new StreamWriter(outpath);
            sw.WriteLine("Set,TargetDecoy,Ambig,Sequence,Length,PsmCount,ProductType,Count,CountPerResidue,IntensitySum,IntensityAverage,IntensitySumPerResidue,IntensityAveragePerResidue");
            
            foreach (var file in matchesByFile)
            {
                string fileName = file.Key;
                foreach (var baseSeqGroup in file.Value.GroupBy(p => p.BaseSeq))
                {
                    string baseSeq = baseSeqGroup.Key;
                    int psmCount = baseSeqGroup.Count();
                    double length = baseSeq.Length;
                    var targetDecoy = baseSeqGroup.First().DecoyContamTarget.Contains("D") ? "Decoy" : "Target";
                    var ambiguity = baseSeqGroup.Average(p => int.Parse(p.AmbiguityLevel.First().ToString()));
                    foreach (var productDict in baseSeqGroup.Select(p =>
                                 p.MatchedIons.GroupBy(m => m.NeutralTheoreticalProduct.ProductType)
                                     .ToDictionary(n => n.Key, n => n.ToList())))
                    {
                        foreach (var productType in productDict)
                        {
                            sw.WriteLine(
                                $"{fileName},{targetDecoy},{ambiguity},{baseSeq},{length},{psmCount},{productType.Key},{productType.Value.Count},{productType.Value.Count / (double)length},{productType.Value.Sum(m => m.Intensity)},{productType.Value.Average(m => m.Intensity)},{productType.Value.Sum(m => m.Intensity) / length},{productType.Value.Average(m => m.Intensity) / length}");
                        }
                        
                    }
                    
                }
            }
            return new MetaMorpheusEngineResults(this);
        }
    }


    public class MatchedIonCounter
    {
        public ProductType ProductType { get; set; }
        public int Count { get; set; }
        public int CountPerResidue { get; set; }

        public double IntensitySum { get; set; }
        public double IntensityAverage { get; set; }
        public double IntensitySumPerResidue { get; set; }
        public double IntensityAveragePerResidue { get; set; }
    }
}
