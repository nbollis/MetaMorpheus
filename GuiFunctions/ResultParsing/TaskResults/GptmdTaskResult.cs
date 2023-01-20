using EngineLayer;
using Proteomics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;
using UsefulProteomicsDatabases;

namespace GuiFunctions
{
    public class GptmdTaskResult : TaskResults, ITsv
    {
        #region Private Properties

        private string customDatabasePath;
        private string candidatesPath;
        private string originalDatabasePath;
        private List<PsmFromTsv> gptmdCandidates;
        private List<Protein> gptmdDatabaseProteins;
        private List<Protein> originalDatabaseProteins;
        private List<Protein> proteinsModifiedByGptmd;

        #endregion

        #region Public Properties

        public List<PsmFromTsv> GptmdCandidates
        {
            get
            {
                if (gptmdCandidates.Any()) return gptmdCandidates;
                gptmdCandidates = PsmTsvReader.ReadTsv(candidatesPath, out List<string> warnings);
                return gptmdCandidates;
            }
        }

        public List<Protein> GptmdDatabaseProteins
        {
            get
            {
                if (gptmdDatabaseProteins.Any()) return gptmdDatabaseProteins;
                string theExtension = Path.GetExtension(customDatabasePath).ToLowerInvariant();
                if (theExtension.Equals(".fasta") || theExtension.Equals(".fa"))
                {

                    gptmdDatabaseProteins = ProteinDbLoader.LoadProteinFasta(customDatabasePath, true, DecoyType.None, false, out List<string> errors,
                        ProteinDbLoader.UniprotAccessionRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotGeneNameRegex,
                        ProteinDbLoader.UniprotOrganismRegex, 1);
                }
                else
                {
                    Dictionary<string, Modification> outStuff = new();
                    gptmdDatabaseProteins = ProteinDbLoader.LoadProteinXML(customDatabasePath, true, DecoyType.None, GlobalVariables.AllModsKnown,
                        false, new List<string>(), out outStuff, 1, 4, 1);
                }
                return gptmdDatabaseProteins;
            }
        }

        public List<Protein> OriginalDatabaseProteins
        {
            get
            {
                if (originalDatabaseProteins.Any()) return originalDatabaseProteins;
                string theExtension = Path.GetExtension(originalDatabasePath).ToLowerInvariant();
                if (theExtension.Equals(".fasta") || theExtension.Equals(".fa"))
                {

                    originalDatabaseProteins = ProteinDbLoader.LoadProteinFasta(originalDatabasePath, true, DecoyType.None, false, out List<string> errors,
                        ProteinDbLoader.UniprotAccessionRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotFullNameRegex, ProteinDbLoader.UniprotGeneNameRegex,
                        ProteinDbLoader.UniprotOrganismRegex, 1);
                }
                else
                {
                    Dictionary<string, Modification> outStuff = new();
                    originalDatabaseProteins = ProteinDbLoader.LoadProteinXML(originalDatabasePath, true, DecoyType.None, GlobalVariables.AllModsKnown,
                        false, new List<string>(), out outStuff, 1, 4, 1);
                }
                return originalDatabaseProteins;
            }
        }

        public double ModsAddedByGptmdCount { get; set; } = 0;
        public List<Protein> ProteinsModifiedByGptmd
        {
            get
            {
                if (!proteinsModifiedByGptmd.Any())
                {
                    for (var i = 0; i < originalDatabaseProteins.Count; i++)
                    {
                        if (originalDatabaseProteins[i].Accession == GptmdDatabaseProteins[i].Accession)
                        {
                            var ogModCount = originalDatabaseProteins[i].OneBasedPossibleLocalizedModifications
                                .Sum(p => p.Value.Count);
                            var gptmdModCount = GptmdDatabaseProteins[i].OneBasedPossibleLocalizedModifications
                                .Sum(p => p.Value.Count);
                            if (ogModCount != gptmdModCount)
                            {
                                ModsAddedByGptmdCount += gptmdModCount - ogModCount;
                                proteinsModifiedByGptmd.Add(GptmdDatabaseProteins[i]);
                            }
                                
                        }
                        else
                        {
                            Debugger.Break();
                        }
                    }
                }
                return proteinsModifiedByGptmd;
            }
        }

        #endregion

        #region Constructor

        public GptmdTaskResult(string taskDirectory, string name) : base(taskDirectory, MyTask.Gptmd, name)
        {
            var files = Directory.GetFiles(taskDirectory);
            customDatabasePath = files.First(p => p.Contains("GPTMD.xml") && !p.Contains("Contaminant"));
            candidatesPath = files.First(p => p.Contains("GPTMD_Candidates"));
            gptmdCandidates = new();
            gptmdDatabaseProteins = new();
            originalDatabaseProteins = new();
            proteinsModifiedByGptmd = new();

            var ogDatabaseStartIndex = Array.IndexOf(ManuscriptProse, "Databases:") + 1;
            var ogDatabaseString = ManuscriptProse[ogDatabaseStartIndex].Replace(@"\t", "");
            var removeFromIndex = ogDatabaseString.IndexOf(" Downloaded", StringComparison.InvariantCulture);
            originalDatabasePath = ogDatabaseString.Remove(removeFromIndex).Trim();
        }

        #endregion


        #region Processing Methods




        #endregion

        #region ITsv Members

        public string TabSeparatedHeader
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("MM Run Name" + "\t");
                sb.Append("Database Size" + "\t");
                sb.Append("Proteins Modified" + "\t");
                sb.Append("Mods Added" + '\t');

                var tsvString = sb.ToString().TrimEnd('\t');
                return tsvString;
            }
        }

        public string ToTsvString()
        {
            var sb = new StringBuilder();
            sb.Append(Name + "\t");
            sb.Append(OriginalDatabaseProteins.Count + "\t");
            sb.Append(ProteinsModifiedByGptmd.Count + "\t");
            sb.Append(ModsAddedByGptmdCount + '\t');

            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }

        #endregion


    }
}
