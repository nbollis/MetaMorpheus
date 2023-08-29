using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;

namespace GuiFunctions
{
    public class MetaMorpheusRun : ITsv
    {
        public string Name { get; }
        public Dictionary<MyTask, TaskResults> TaskResults { get; set; }
        public string DirectoryPath { get; }

        private List<EngineResultsFromTxt> _engineResults;
        public List<EngineResultsFromTxt> EngineResults => _engineResults ??= TaskResults.SelectMany(p => p.Value.EngineResults).ToList();


        public MetaMorpheusRun(string directoryPath)
        {
            DirectoryPath = directoryPath;
            Name = directoryPath.Split("\\").Last();
            TaskResults = new Dictionary<MyTask, TaskResults>();
            var directories = Directory.GetDirectories(directoryPath).Where(p => !p.Contains("Task Settings"));

            foreach (var directory in directories)
            {
                var taskTypeString = directory.Split("\\").Last().Split("-").Last();
                switch (taskTypeString)
                {
                    case "CalibrateTask":
                        CalibrationTaskResult task = new(directory, Name);
                        TaskResults.Add(task.TaskType, task);
                        break;

                    case "AveragingTask":
                        AveragingTaskResult task1 = new(directory, Name);
                        TaskResults.Add(task1.TaskType, task1);
                        break;

                    case "GPTMDTask":
                        GptmdTaskResult task2 = new(directory, Name);
                        TaskResults.Add(task2.TaskType, task2);
                        break;

                    case "SearchTask":
                        SearchTaskResult task3 = new(directory, Name);
                        TaskResults.Add(task3.TaskType, task3);
                        break;
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }

        #region Tsv Members

        public string TabSeparatedHeader
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var result in TaskResults.Where(p => p.Value is ITsv))
                {
                    sb.Append(((ITsv)result.Value).TabSeparatedHeader + '\t');
                }

                var tsvString = sb.ToString().TrimEnd('\t');
                return tsvString;
            }
        }

        public string ToTsvString()
        {
            var sb = new StringBuilder();
            foreach (var result in TaskResults.Where(p => p.Value is ITsv))
            {
                sb.Append(((ITsv)result.Value).ToTsvString() + '\t');
            }

            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }


        public void ExportAllTimeResults(bool useName = true)
        {
            ExportTaskTimeResults(useName);
            ExportEngineResults(useName);
        }

        public void ExportTaskTimeResults(bool useName = false)
        {
            string outPath = Path.Combine(DirectoryPath, useName ? $"{Name}TaskTime.csv" : "TaskTimeResults.csv");
            using (var sw = new StreamWriter(outPath))
            {
                sw.WriteLine("Task,Engine,Minutes,Time");
                foreach (var task in TaskResults)
                {
                    sw.WriteLine($"{task.Key.ToString()},,{task.Value.RunTime.TotalMinutes},{task.Value.RunTime:c}");
                    foreach (var engine in task.Value.EngineResults)
                    {
                        sw.WriteLine($"{task.Key.ToString()},{engine.EngineType.ToString()},{engine.Time.TotalMinutes},{engine.Time:c}");
                    }
                }
            }
        }

        public void ExportEngineResults(bool useName = false)
        {
            string outPath = Path.Combine(DirectoryPath, useName ? $"{Name}EngineTime.csv" : "EngineTimeResults.csv");
            using (var sw = new StreamWriter(outPath))
            {
                sw.WriteLine("Task,Engine,Minutes,Time");
                foreach (var task in TaskResults)
                {
                    foreach (var engine in task.Value.EngineResults)
                    {
                        sw.WriteLine($"{task.Key.ToString()},{engine.EngineType.ToString()},{engine.Time.TotalMinutes},{engine.Time:c}");
                    }
                }
            }
        }

        #endregion
    }
}
