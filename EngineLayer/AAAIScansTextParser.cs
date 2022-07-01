using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace EngineLayer
{
    public class AAAIScansTextParser
    {
        public static void ParseIScans()
        {
            string filepath = @"B:\Users\Nic\InstrumentControlData\IScanSingle - Copy.txt";
            string[] alltext = File.ReadAllLines(filepath);
            int cutIndex = Array.IndexOf(alltext, alltext.Where(p => p.Contains("PossibleParameters")).First());
            var trimmedText = alltext.ToList().GetRange(cutIndex + 1, alltext.Length - cutIndex - 1).ToList();

            List<LineItem> lineItems = new();
            foreach (var line in trimmedText.Where(p => p != ""))
            {
                string[] pieces = line.Split(':');
                for (int i = 0; i < pieces.Length; i++)
                {
                    pieces[i] = pieces[i].Replace("\"", "");
                }
                LineItem lineItem = new LineItem()
                {
                    Name = pieces[3].Split(',')[0],
                    DefaultValue = pieces[1].Split(',')[0],
                    HelpText = pieces[2],
                    PossibleValues = pieces[4].Replace("},", "")
                };
                lineItems.Add(lineItem);
            }
            filepath = @"B:\Users\Nic\InstrumentControlData\IScanTranslated.txt";
            using (FileStream stream = File.Create(filepath))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.WriteLine("Name:DefaultValue:PossibleValues:HelpText");
                    foreach (var line in lineItems)
                    {
                        writer.WriteLine(line.ToString());
                    }
                }
            }

        }
    }

    public class LineItem 
    {
        public string Name { get; set; }
        public string DefaultValue { get; set; }
        public string HelpText { get; set; }
        public string PossibleValues { get; set; }

        public override string ToString()
        {
            return Name + "\t" + DefaultValue + "\t" + PossibleValues + "\t" + HelpText;
        }
    }

}
