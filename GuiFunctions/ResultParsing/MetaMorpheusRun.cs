using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;

namespace GuiFunctions
{
    public class MetaMorpheusRun
    {
        public string Name { get; }
        public Dictionary<MyTask, TaskResults> TaskResults { get; set; }
        public string DirectoryPath { get; }

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
                        CalibrationTaskResult task = new(directory);
                        TaskResults.Add(task.TaskType, task);
                        break;

                    case "AveragingTask":
                        AveragingTaskResult task1 = new(directory);
                        TaskResults.Add(task1.TaskType, task1);

                        break;

                    case "GPTMDTask":
                        GptmdTaskResult task2 = new(directory);
                        TaskResults.Add(task2.TaskType, task2);
                        break;

                    case "SearchTask":
                        SearchTaskResult task3 = new(directory);
                        TaskResults.Add(task3.TaskType, task3);
                        break;
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
