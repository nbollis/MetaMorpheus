using System;
using System.Collections.Generic;
using System.IO;

namespace GuiFunctions
{
    public class EngineResultsFromTxt
    {
        public EngineType EngineType { get; init; }
        public TimeSpan Time { get; init; }

        public EngineResultsFromTxt(EngineType engineType, TimeSpan time)
        {
            EngineType = engineType;
            Time = time;
        }

        public static IEnumerable<EngineResultsFromTxt> GetResultsFromTxtFile(string filePath)
        {
            return GetResultsFromTxtFile(File.ReadAllLines(filePath));
        }
        
        public static IEnumerable<EngineResultsFromTxt> GetResultsFromTxtFile(string[] resultsText)
        {
            for (var i = 0; i < resultsText.Length; i++)
            {
                var line = resultsText[i];
                if (line.Contains("Time to run engine:"))
                {
                    var timeString = line.Split("engine:")[1].Trim();
                    var typeString = resultsText[i - 1].Split(':')[1].Trim();

                    var time = TimeSpan.Parse(timeString);
                    var type = Enum.Parse<EngineType>(typeString);

                    yield return new EngineResultsFromTxt(type, time);
                }
            }
        }
    }
}
