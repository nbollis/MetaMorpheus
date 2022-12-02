using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer
{
    public class SearchResultParser
    {

        /// <summary>
        /// Finds specific file within the main results file
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="filetype"></param>
        /// <returns></returns>
        public static string GetFilePath(string directoryPath, FileTypes filetype)
        {
            string subdirectory;
            switch (filetype)
            {
                case FileTypes.AllPSMs:
                    subdirectory = Directory.GetDirectories(directoryPath).First(p => p.Split(@"\").Last().Contains("Search"));
                    return Directory.GetFiles(subdirectory).First(p => p.Contains(filetype.ToString()));
                    
                case FileTypes.AllProteoforms:
                    subdirectory = Directory.GetDirectories(directoryPath).First(p => p.Split(@"\").Last().Contains("Search"));
                    return Directory.GetFiles(subdirectory).First(p => p.Contains(filetype.ToString()));

                case FileTypes.AllPeptides:
                    subdirectory = Directory.GetDirectories(directoryPath).First(p => p.Split(@"\").Last().Contains("Search"));
                    return Directory.GetFiles(subdirectory).First(p => p.Contains(filetype.ToString()));

                default:
                    throw new ArgumentException("file type not yet implemented");
            }
        }


        public static string[] GetSpecificSearchFolderInfo(string folderpath, string type, string fileSearched = null)
        {
            string delimiter = "\t";

            string[] output;
            switch (type)
            {
                case "KHBStyle":
                    if (fileSearched == null) throw new ArgumentException("Files searched cannot be null");
                    GenerateTxtOfSearchResultsKHB(folderpath, delimiter, fileSearched, out output);
                    break;

                case "Ambiguity":
                    GenerateTxtOfSearchResultsAmbiguityInfo(folderpath, delimiter, out output);
                    break;

                default:
                    output = null;
                    break;
            }
            return output;
        }

        public static void GenerateTxtOfSearchResultsAmbiguityInfo(string folderpath, string delimiter, out string[] output)
        {
            string resultsHeader = "Folder Name" + delimiter + "Complimentary?" + delimiter + "Internal?" + delimiter + "PrSMs" + delimiter +
                "Proteoforms" + delimiter + "Proteins" + delimiter + "SearchTime (min)" + delimiter + "PSMs" + delimiter+ "1" + delimiter + "2A" + 
                delimiter + "2B" + delimiter + "2C" + delimiter + "2D" + delimiter + "3" + delimiter + "4" + delimiter + "5";

            string[] searchFolders = Directory.GetDirectories(folderpath);
            output = new string[searchFolders.Length + 1];

            string filepath;
            for (int i = 0; i < searchFolders.Length; i++)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(searchFolders[i].Replace(folderpath + "\\", "") + delimiter);

                // complimentary and internal
                filepath = FindFileInSearchFolder(searchFolders[i], "Task Settings", "SearchTaskconfig");
                bool complimentary = UsedComplimentaryIons(filepath);
                bool internalions = UsedInternalIons(filepath, out int minLength);

                filepath = FindFileInSearchFolder(searchFolders[i], "SearchTask", "results");
                string results = GetSearchTaskResults(filepath, delimiter);
                string time = GetSearchTaskTime(filepath);

                sb.Append(complimentary + delimiter);
                sb.Append(internalions + delimiter);
                sb.Append(results + delimiter);
                sb.Append(time + delimiter);

                filepath = FindFileInSearchFolder(searchFolders[i], "SearchTask", "AllPSMs.psmtsv");
                string ambiguityResults = GetSearchResultAmbiguity(filepath, delimiter);
                sb.Append(ambiguityResults + delimiter);
                output[i] = sb.ToString();

            }
            string[] temp = new string[output.Length];
            Array.Copy(output, temp, output.Length);

            output[0] = resultsHeader;
            for (int i = 0; i < output.Length - 1; i++)
            {
                output[i + 1] = temp[i];
            }
        }

        // KHB format
        public static void GenerateTxtOfSearchResultsKHB(string folderPath, string delimiter, string filesSearched, out string[] output)
        {
            string resultsHeader = "Files Searched:Folder name:Calibration Settings" +
            ":GPTMD Simple Settings:GPTMD Full Settings:Search Fixed:Search Variable:" +
            "PrSMs:Proteoforms:Proteins:Search Time";

            string[] searchFolders = Directory.GetDirectories(folderPath);
            output = new string[searchFolders.Length + 1];

            string filepath;
            for (int i = 0; i < searchFolders.Length; i++)
            {
                StringBuilder sb = new();
                sb.Append(filesSearched + delimiter);
                sb.Append(searchFolders[i].Replace(folderPath + "\\", "") + delimiter);

                if (TaskFolderExists(searchFolders[i], "CalibrateTask"))
                {
                    filepath = FindFileInSearchFolder(searchFolders[i], "Task Settings", "CalibrateTaskconfig");
                    string caliSettings = GetCalibrationSettings(filepath);
                    sb.Append(caliSettings + delimiter);
                }
                else
                {
                    sb.Append("No Calibration" + delimiter);
                }

                if (TaskFolderExists(searchFolders[i], "GPTMDTask"))
                {
                    filepath = FindFileInSearchFolder(searchFolders[i], "Task Settings", "GPTMDTaskconfig");
                    string gptmdSimpleSettings = GetSimpleGPTMDSettings(filepath);
                    string gptmdFullSettings = GetFullGPTMDSettings(filepath);
                    sb.Append(gptmdSimpleSettings + delimiter);
                    sb.Append(gptmdFullSettings + delimiter);
                }
                else
                {
                    sb.Append("No GPTMD" + delimiter);
                }

                if (TaskFolderExists(searchFolders[i], "SearchTask"))
                {
                    filepath = FindFileInSearchFolder(searchFolders[i], "Task Settings", "SearchTaskconfig");
                    string fixedMods = GetSearchTaskFixedMods(filepath);
                    string variableMods = GetSearchTaskVariableMods(filepath);

                    filepath = FindFileInSearchFolder(searchFolders[i], "SearchTask", "results");
                    string results = GetSearchTaskResults(filepath, delimiter);
                    string time = GetSearchTaskTime(filepath);

                    sb.Append(fixedMods + delimiter);
                    sb.Append(variableMods + delimiter);
                    sb.Append(results + delimiter);
                    sb.Append(time + delimiter);
                }
                output[i] = sb.ToString();
            }

            string[] temp = new string[output.Length];
            Array.Copy(output, temp, output.Length);

            output[0] = resultsHeader;
            for (int i = 0; i < output.Length - 1; i++)
            {
                output[i + 1] = temp[i];
            }
        }

        #region Pulling Specific Info
        public static string GetCalibrationSettings(string filepath)
        {
            string[] lines = File.ReadAllLines(filepath);
            string line = lines.Where(p => p.Contains("ListOfModsFixed")).FirstOrDefault();
            line = line.Replace("\\t", " ");
            line = line.Replace("\"", "");
            line = line.Split('=')[1].Trim();
            if (line.Equals("Common Fixed Carbamidomethyl on C  Common Fixed Carbamidomethyl on U"))
                line = "Common Fixed Cambamidomethyl on CU";
            return line == null ? "Error" : line;
        }
        public static string GetSimpleGPTMDSettings(string filepath)
        {
            string[] lines = File.ReadAllLines(filepath);
            string config = lines.Where(p => p.Contains("ListOfModsGptmd")).FirstOrDefault().Replace("\t", " ");
            string line = "";
            if (config.Contains("Oxidation on M"))
            {
                line += "Ox M, ";
            }
            if (config.Contains("Common Biological"))
            {
                line += "common bio, ";
            }
            if (config.Contains("Metal"))
            {
                line += "metals, ";
            }
            if (config.Contains("Common Artifact"))
            {
                line += "common art ";
            }
            return line;
        }
        public static string GetFullGPTMDSettings(string filepath)
        {
            string[] lines = File.ReadAllLines(filepath);
            string config = lines.Where(p => p.Contains("ListOfModsGptmd")).FirstOrDefault().Split(" = ")[1];
            config = config.Substring(1, config.Length - 2);
            StringBuilder sb = new StringBuilder();
            string[] unique = config.Split("\\t\\t").Distinct().ToArray();
            var groupedMods = unique.ToList().GroupBy(p => p.Split("\\t")[0]);
            foreach (var group in groupedMods)
            {
                sb.Append(EliminateGroupRedundancies(group));
            }

            return sb.ToString();
        }
        public static string GetSearchTaskFixedMods(string filepath)
        {
            string[] lines = File.ReadAllLines(filepath);
            StringBuilder sb = new StringBuilder();
            string line = lines.Where(p => p.Contains("ListOfModsFixed")).FirstOrDefault().Replace("\t", " ");
            line = line.Split(" = ")[1];
            line = line.Substring(1, line.Length - 2);
            string[] unique = line.Split("\\t\\t").Distinct().ToArray();
            var grouped = unique.ToList().GroupBy(p => p.Split("\\t")[0]);
            foreach (var group in grouped)
            {
                sb.Append(EliminateGroupRedundancies(group));
            }
            return sb.ToString();
        }
        public static string GetSearchTaskVariableMods(string filepath)
        {
            string[] lines = File.ReadAllLines(filepath);
            StringBuilder sb = new StringBuilder();
            string line = lines.Where(p => p.Contains("ListOfModsVariable")).FirstOrDefault();
            if (!line.Equals("ListOfModsVariable = \"\""))
            {
                line = line.Split(" = ")[1];
                line = line.Substring(1, line.Length - 2);
                string[] unique = line.Split("\\t\\t").Distinct().ToArray();
                var grouped = unique.ToList().GroupBy(p => p.Split("\\t")[0]);
                foreach (var group in grouped)
                {
                    sb.Append(EliminateGroupRedundancies(group));
                }
            }
            else
                line = "No Variable Mods";
            return sb.ToString();
        }
        public static string GetSearchTaskResults(string filepath, string delimiter)
        {
            string[] lines = File.ReadAllLines(filepath);
            string psms = lines.Where(p => p.Contains("All target PSMS")).First().Replace("All target PSMS within 1% FDR: ", "");
            string proteoforms = lines.Where(p => p.Contains("All target proteoforms")).First().Replace("All target proteoforms within 1% FDR: ", "");
            string proteins = lines.Where(p => p.Contains("All target protein groups")).First().Replace("All target protein groups within 1% FDR: ", "");
            string line = psms + delimiter + proteoforms + delimiter + proteins;
            return line;
        }
        public static string GetSearchTaskTime(string filepath)
        {
            string[] lines = File.ReadAllLines(filepath);
            string[] timeSplit = lines.Where(p => p.Contains("Time to run task: ")).FirstOrDefault().Split(":");
            if (timeSplit[1].Split('.').Length > 1)
            {
                var split = timeSplit[1].Split('.');
                int days = int.Parse(split[0]);
                timeSplit[1] = int.Parse(split[1] + (24 * days)).ToString();
            }
            TimeSpan time = new(int.Parse(timeSplit[1]), int.Parse(timeSplit[2]), (int)double.Parse(timeSplit[3]));
            return time.TotalMinutes.ToString();
        }
        public static bool UsedComplimentaryIons(string filepath)
        {
            string[] lines = File.ReadAllLines(filepath);
            string line = lines.First(p => p.Contains("AddCompIons"));
            string val = line.Split('=')[1].Trim();
            return bool.Parse(val);
        }

        public static bool UsedInternalIons(string filepath, out int minLength)
        {
            string[] lines = File.ReadAllLines(filepath);
            string line = lines.First(p => p.Contains("MinAllowedInternalFragmentLength"));
            string val = line.Split('=')[1].Trim();
            minLength = int.Parse(val);
            if (minLength == 0)
                return false;
            else
                return true;
        }

        public static string GetSearchResultAmbiguity(string filepath, string delimiter)
        {
            StringBuilder sb = new StringBuilder();

            List<PsmFromTsv> psms = PsmTsvReader.ReadTsv(filepath, out List<string> warnings).Where(p => p.QValue <= 0.01 && p.DecoyContamTarget == "T").ToList() ;
            int one = psms.Where(p => p.AmbiguityLevel == "1").Count();
            int twoA = psms.Where(p => p.AmbiguityLevel == "2A").Count();
            int twoB = psms.Where(p => p.AmbiguityLevel == "2B").Count();
            int twoC = psms.Where(p => p.AmbiguityLevel == "2C").Count();
            int twoD = psms.Where(p => p.AmbiguityLevel == "2D").Count();
            int three = psms.Where(p => p.AmbiguityLevel == "3").Count();
            int four = psms.Where(p => p.AmbiguityLevel == "4").Count();
            int five = psms.Where(p => p.AmbiguityLevel == "5").Count();

            sb.Append(psms.Count() + delimiter);
            sb.Append(one + delimiter);
            sb.Append(twoA + delimiter);
            sb.Append(twoB + delimiter);
            sb.Append(twoC + delimiter);
            sb.Append(twoD + delimiter);
            sb.Append(three + delimiter);
            sb.Append(four + delimiter);
            sb.Append(five + delimiter);

            return sb.ToString();
        }


        #endregion

        public static string EliminateGroupRedundancies(IGrouping<string, string> groups)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(groups.Key + " - ");
            var newGroups = groups.GroupBy(p => p.Split(" on ")[0]);
            string temp = "";
            foreach (var group in newGroups)
            {
                temp = EliminateIndividualRedundancies(group).Replace("\\t", " ");
                sb.Append(temp.Trim() + ", ");
            }
            sb.Remove(sb.Length - 2, 2);
            sb.Append(" ");
            return sb.ToString();
        }

        public static string EliminateIndividualRedundancies(IGrouping<string, string> group)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(group.Key.Split("\\t")[1] + " ");
            string temp = "";
            foreach (var item in group)
            {
                temp = item.Split(" on ")[1];
                sb.Append(temp.Trim());
            }

            return sb.ToString();
        }

        public static bool TaskFolderExists(string directoryToSearch, string toSearchFor)
        {
            string[] taskFolders = Directory.GetDirectories(directoryToSearch);
            if (taskFolders.Any(p => p.Contains(toSearchFor)))
                return true;
            else
                return false;
        }

        public static string FindFileInSearchFolder(string searchPath, string taskType, string filename)
        {
            string[] taskFolders = Directory.GetDirectories(searchPath);
            foreach (var task in taskFolders.Where(p => p.Contains(taskType)))
            {
                return Directory.GetFiles(task).FirstOrDefault(p => p.Contains(filename));
            }
            return "-\t";
        }




        /// <summary>
        /// Example Method(folder, "SearchTask", "AllPSMs.psmtsv")
        /// will pull all psmtsv files
        /// </summary>
        /// <param name="folderPath">main folder to look in each folder within</param>
        /// <param name="taskType">name of within each search result to locate</param>
        /// <param name="fileName">file to be found </param>
        /// <returns></returns>
        public static List<string> FindSpecificFileInFolderOfFolders(string folderPath, string taskType, string fileName)
        {
            List<string> result = new List<string>();
            string[] searchFolders = Directory.GetDirectories(folderPath);
            foreach (var folder in searchFolders)
            {
                string[] taskFolders = Directory.GetDirectories(folder);
                foreach (var task in taskFolders)
                {
                    if (task.Contains(taskType))
                    {
                        result.AddRange(Directory.GetFiles(task).Where(p => p.Contains(fileName)));
                    }
                }
            }
            return result;
        }

        public static Dictionary<string, List<PsmFromTsv>> GetPSMsFromNumerousSearchResults(string folderpath, bool filterToQ = false, bool targetsOnly = false)
        {
            Dictionary<string, List<PsmFromTsv>> allPsmsWithFileNames = new();
            List<string> psmFiles = FindSpecificFileInFolderOfFolders(folderpath, "SearchTask", "AllPSMs.psmtsv");
            foreach (var psmFile in psmFiles)
            {
                if (!filterToQ && !targetsOnly)
                {
                    List<PsmFromTsv> psms = PsmTsvReader.ReadTsv(psmFile, out List<string> warnings);
                    allPsmsWithFileNames.Add(psmFile.Split("\\")[4], psms);
                }

                else if (filterToQ && !targetsOnly)
                {
                    List<PsmFromTsv> psms = PsmTsvReader.ReadTsv(psmFile, out List<string> warnings).Where(p => p.QValue <= 0.01).ToList();
                    allPsmsWithFileNames.Add(psmFile.Split("\\")[4], psms);
                }

                else if (!filterToQ && targetsOnly)
                {
                    List<PsmFromTsv> psms = PsmTsvReader.ReadTsv(psmFile, out List<string> warnings).Where(p => p.DecoyContamTarget.Equals("T")).ToList();
                    allPsmsWithFileNames.Add(psmFile.Split("\\")[4], psms);
                }
                
                else if (filterToQ && targetsOnly)
                {
                    List<PsmFromTsv> psms = PsmTsvReader.ReadTsv(psmFile, out List<string> warnings).Where(p => p.QValue <= 0.01 && p.DecoyContamTarget.Equals("T")).ToList();
                    allPsmsWithFileNames.Add(psmFile.Split("\\")[4], psms);
                }
            }

            return allPsmsWithFileNames;
        }
    }


    public enum FileTypes
    {
        AllPSMs,
        AllProteoforms,
        AllPeptides, 
        CalibratedSpectra,
        AveragedSpectra,
    }
}
