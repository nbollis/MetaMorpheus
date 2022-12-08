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
        public Dictionary<MyTask, TaskResults> TaskResults { get; set; }

        public MetaMorpheusRun(string directoryPath)
        {
            TaskResults = new Dictionary<MyTask, TaskResults>();
            var directories = Directory.GetDirectories(directoryPath).Where(p => !p.Contains("Task Settings"));

            foreach (var directory in directories)
            {
                TaskResults task = new(directory);
                TaskResults.Add(task.TaskType, task);
            }
        }
    }
}
