using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer
{
    
    public static class TimeProfiler
    {
        private static List<TimePoint> _timePoints;

        static TimeProfiler()
        {
            _timePoints = new();
        }

        /// <summary>
        /// Marks time with your custom message and the current time.
        /// duplicate messages will be tagged and labeled as such
        /// </summary>
        /// <param name="message"></param>
        public static void MarkTime(string message)
        {
            var time = DateTime.Now;

            // set up unique label
            int index = 1;
            bool first = true;
            while (_timePoints.FirstOrDefault(p => p.Label == message) is not null)
            {
                if (first)
                {
                    message += $"({index})";
                    first = false;
                }
                else
                {
                    message = message.Replace($"({index - 1})", $"({index})");
                }
                index++;
            }

            if (!_timePoints.Any())
            {
                _timePoints.Add(new TimePoint(message, time, 0, 0));
                return;
            }
            double elapsed = (time - _timePoints.Last().Time).Seconds;
            double total = (time - _timePoints.First().Time).Seconds;
            _timePoints.Add(new TimePoint(message, time, elapsed, total));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outpath"></param>
        /// <param name="breakOnWriting"></param>
        public static void ExportProfiling(string outpath, bool breakOnWriting = true)
        {
            if (!outpath.EndsWith(".csv"))
                outpath += ".csv";

            int index = 1;
            bool first = true;
            while (File.Exists(outpath))
            {
                outpath = outpath.Replace(".csv", "");
                if (first)
                {
                    outpath += $"({index}).csv";
                    first = false;
                    index++;
                    continue;
                }

                outpath = outpath.Replace($"({index - 1})", $"({index}).csv");
                index++;
            }

            using (var sw = new StreamWriter(File.Create(outpath)))
            {
                sw.WriteLine(TimePoint.CsvHeader);
                foreach (var timePoint in _timePoints)
                {
                    sw.WriteLine(timePoint);
                }
            }

            if (breakOnWriting)
                Debugger.Break();
        }


        class TimePoint
        {
            public string Label { get; init; }
            public DateTime Time { get; init; }
            double Elapsed { get; init; }
            double TotalTime { get; init; }

            public TimePoint(string label, DateTime time, double elapsed, double totalTime)
            {
                Label = label;
                Time = time;
                Elapsed = elapsed;
                TotalTime = totalTime;
            }

            public override string ToString()
            {
                return $"{Label},{Time:T},{Elapsed},{TotalTime}";
            }

            public static string CsvHeader => "Label,Time,Elapsed,Total";
        }


    }
}
