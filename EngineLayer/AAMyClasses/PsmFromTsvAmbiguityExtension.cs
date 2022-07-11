using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer
{
    public class PsmFromTsvAmbiguityExtension
    {
        public List<PsmFromTsv> AllPsms { get; set; }
        public List<PsmFromTsv> Level1 { get; set; }
        public List<PsmFromTsv> Level2A { get; set; }
        public List<PsmFromTsv> Level2B { get; set; }
        public List<PsmFromTsv> Level2C { get; set; }
        public List<PsmFromTsv> Level2D { get; set; }
        public List<PsmFromTsv> Level3AB { get; set; }
        public List<PsmFromTsv> Level3AC { get; set; }
        public List<PsmFromTsv> Level3AD { get; set; }
        public List<PsmFromTsv> Level3BC { get; set; }
        public List<PsmFromTsv> Level3BD { get; set; }
        public List<PsmFromTsv> Level3CD { get; set; }
        public List<PsmFromTsv> Level4A { get; set; }
        public List<PsmFromTsv> Level4B { get; set; }
        public List<PsmFromTsv> Level4C { get; set; }
        public List<PsmFromTsv> Level4D { get; set; }
        public List<PsmFromTsv> Level5 { get; set; }

        public PsmFromTsvAmbiguityExtension(List<PsmFromTsv> psms)
        {
            psms = psms.Where(p => p.QValue <= 0.01).ToList();
            InitializeAllLists();


            AllPsms = psms;
            Level1.AddRange(psms.Where(p => p.AmbiguityLevel == "1"));
            Level2A.AddRange(psms.Where(p => p.AmbiguityLevel == "2A"));
            Level2B.AddRange(psms.Where(p => p.AmbiguityLevel == "2B"));
            Level2C.AddRange(psms.Where(p => p.AmbiguityLevel == "2C"));
            Level2D.AddRange(psms.Where(p => p.AmbiguityLevel == "2D"));

            Level3AB.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { false, false, true, true })));
            Level3AC.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { false, true, false, true })));
            Level3AD.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { false, true, true, false })));
            Level3BC.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { true, false, false, true })));
            Level3BD.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { true, false, true, false })));
            Level3CD.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { true, true, false, false })));

            Level4A.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { true, false, false, false })));
            Level4B.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { false, true, false, false })));
            Level4C.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { false, false, true, false })));
            Level4D.AddRange(psms.Where(p => p.AmbiguityInfo.AllValues.SequenceEqual(new bool[] { false, false, false, true })));

            Level5.AddRange(psms.Where(p => p.AmbiguityLevel == "5"));
        }

        public static string ChangesInAmbiguity(PsmFromTsvAmbiguityExtension normalResults, PsmFromTsvAmbiguityExtension internalResults, string delimiter)
        {
            List<Tuple<PsmFromTsv, PsmFromTsv>> changed = new();
            foreach (var psm in internalResults.AllPsms)
            {
                var uniqueIDpairs = normalResults.AllPsms.Where(p => p.UniqueID == psm.UniqueID && p.AmbiguityInfo.AmbigType != psm.AmbiguityInfo.AmbigType).ToList();
                if (uniqueIDpairs.Count() > 1)
                    throw new Exception();
                else if (uniqueIDpairs.Count() == 1)
                    changed.Add(new Tuple<PsmFromTsv, PsmFromTsv>(uniqueIDpairs.First(), psm));
            }


            var nestedAmbigGroups =
                from pair in changed
                group pair by pair.Item1.AmbiguityInfo.AmbigType into newGroup1
                from newGroup2 in (
                    from pair in newGroup1
                    group pair by pair.Item2.AmbiguityInfo.AmbigType
                )
                group newGroup2 by newGroup1.Key;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("From" + delimiter + "To" + delimiter + "Count");
            foreach (var group in nestedAmbigGroups)
            {
                string groupKey = group.Key;
                foreach (var value in group)
                {
                    sb.AppendLine(group.Key + delimiter + value.Key + delimiter + value.Count());
                }
            }
            return sb.ToString();

        }

        public string GetAmbiguityCountString(string delimiter)
        {
            return Level1.Count() + delimiter + Level2A.Count() + delimiter + Level2B.Count() + delimiter +
                Level2C.Count() + delimiter + Level2D.Count() + delimiter + Level3AB.Count() + delimiter +
                Level3AC.Count() + delimiter + Level3AD.Count() + delimiter + Level3BC.Count() + delimiter +
                Level3BD.Count() + delimiter + Level3CD.Count() + delimiter + Level4A.Count() + delimiter +
                Level4B.Count() + delimiter + Level4C.Count() + delimiter + +Level4D.Count() + delimiter +
                Level5.Count();
        }

        public static string GetAmbiguityStringHeader(string delimiter)
        {
            return "1" + delimiter + "2A" + delimiter + "2B" + delimiter + "2C" + delimiter 
                + "2D" + delimiter + "3AB" + delimiter + "3AC" + delimiter + "3AD" + delimiter 
                + "3BC" + delimiter + "3BD" + delimiter + "3CD" + delimiter + "4A" + delimiter 
                + "4B" + delimiter  + "4C" + delimiter + "4D" + delimiter + "5";
        }


        private void InitializeAllLists()
        {
            AllPsms = new();
            Level1 = new();
            Level2A = new();
            Level2B = new();
            Level2C = new();
            Level2D = new();
            Level3AB = new();
            Level3AC = new();
            Level3AD = new();
            Level3BC = new();
            Level3BD = new();
            Level3CD = new();
            Level4A = new();
            Level4B = new();
            Level4C = new();
            Level4D = new();
            Level5 = new();
        }
    }

   
}
