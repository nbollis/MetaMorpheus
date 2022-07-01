using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer
{
    public class AAASearchResultParser
    {
        public static string ResultsHeader = "Files Searched:Folder name:Calibration Settings" +
            ":GPTMD Simple Settings:GPTMD Full Settings:Search Fixed:Search Variable:" +
            "PrSMs:Proteoforms:Proteins:Search Time";



        public static string[] GenerateTxtOfSearchResults(string folderPath, string filesSearched)
        {
            string[] searchFolders = Directory.GetDirectories(folderPath);
            string[] output = new string[searchFolders.Length + 1];
            string delimiter = ":";

            string filepath;
            for (int i = 0; i < searchFolders.Length; i++)
            {
                StringBuilder sb = new();
                sb.Append(filesSearched + delimiter);
                sb.Append(searchFolders[i].Replace(folderPath + "\\", "") + delimiter);

                if (TaskFolderExists(searchFolders[i], "CalibrateTask"))
                {
                    filepath = FindFileInSearchFolder(searchFolders[i], "Task Settings", "CalibrateTaskconfig");
                    string caliSettings = GetCalibrationSettings(File.ReadAllLines(filepath));
                    sb.Append(caliSettings + delimiter);
                }
                else
                {
                    sb.Append("No Calibration" + delimiter);
                }

                if (TaskFolderExists(searchFolders[i], "GPTMDTask"))
                {
                    filepath = FindFileInSearchFolder(searchFolders[i], "Task Settings", "GPTMDTaskconfig");
                    string gptmdSimpleSettings = GetSimpleGPTMDSettings(File.ReadAllLines(filepath));
                    string gptmdFullSettings = GetFullGPTMDSettings(File.ReadAllLines(filepath));
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
                    string[] lines = File.ReadAllLines(filepath);
                    string fixedMods = GetSearchTaskFixedMods(lines);
                    string variableMods = GetSearchTaskVariableMods(lines);

                    filepath = FindFileInSearchFolder(searchFolders[i], "SearchTask", "results");
                    lines = File.ReadAllLines(filepath);
                    string results = GetSearchTaskResults(lines, delimiter);
                    string time = GetSearchTaskTime(lines);

                    sb.Append(fixedMods + delimiter);
                    sb.Append(variableMods + delimiter);
                    sb.Append(results + delimiter);
                    sb.Append(time + delimiter);
                }
                output[i] = sb.ToString();
            }

            string[] temp = new string[output.Length];
            Array.Copy(output, temp, output.Length);

            output[0] = ResultsHeader;
            for (int i = 0; i < output.Length - 1; i++)
            {
                output[i + 1] = temp[i];
            }

            return output;
        }

        #region Pulling Specific Info
        public static string GetCalibrationSettings(string[] lines)
        {
            string line = lines.Where(p => p.Contains("ListOfModsFixed")).FirstOrDefault();
            line = line.Replace("\\t", " ");
            line = line.Replace("\"", "");
            line = line.Split('=')[1].Trim();
            if (line.Equals("Common Fixed Carbamidomethyl on C  Common Fixed Carbamidomethyl on U"))
                line = "Common Fixed Cambamidomethyl on CU";
            return line == null ? "Error" : line;
        }

        public static string GetSimpleGPTMDSettings(string[] lines)
        {
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

        public static string GetFullGPTMDSettings(string[] lines)
        {
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

        public static string GetSearchTaskFixedMods(string[] lines)
        {
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
        public static string GetSearchTaskVariableMods(string[] lines)
        {
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
        public static string GetSearchTaskResults(string[] lines, string delimiter)
        {
            string psms = lines.Where(p => p.Contains("All target PSMS")).First().Replace("All target PSMS within 1% FDR: ", "");
            string proteoforms = lines.Where(p => p.Contains("All target proteoforms")).First().Replace("All target proteoforms within 1% FDR: ", "");
            string proteins = lines.Where(p => p.Contains("All target protein groups")).First().Replace("All target protein groups within 1% FDR: ", "");
            string line = psms + delimiter + proteoforms + delimiter + proteins;
            return line;
        }
        public static string GetSearchTaskTime(string[] lines)
        {
            string[] timeSplit = lines.Where(p => p.Contains("Time to run task: ")).FirstOrDefault().Split(":");
            TimeSpan time = new(int.Parse(timeSplit[1]), int.Parse(timeSplit[2]), (int)double.Parse(timeSplit[3]));
            return time.Hours.ToString();
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
                return Directory.GetFiles(task).Where(p => p.Contains(filename)).FirstOrDefault();
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


    }
}
