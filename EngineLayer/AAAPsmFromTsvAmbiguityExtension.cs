using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer
{
    public class AAAPsmFromTsvAmbiguityExtension
    {
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
        public List<PsmFromTsv> Level3DC { get; set; }
        public List<PsmFromTsv> Level4A { get; set; }
        public List<PsmFromTsv> Level4B { get; set; }
        public List<PsmFromTsv> Level4C { get; set; }
        public List<PsmFromTsv> Level4D { get; set; }
        public List<PsmFromTsv> Level5 { get; set; }

        public AAAPsmFromTsvAmbiguityExtension(List<PsmFromTsv> psms)
        {
            psms = psms.Where(p => p.QValue <= 0.01).ToList();
            InitializeAllLists();
            Level1.AddRange(psms.Where(p => p.AmbiguityLevel == "1"));
            Level2A.AddRange(psms.Where(p => p.AmbiguityLevel == "2A"));
            Level2B.AddRange(psms.Where(p => p.AmbiguityLevel == "2B"));
            Level2C.AddRange(psms.Where(p => p.AmbiguityLevel == "2C"));
            Level2D.AddRange(psms.Where(p => p.AmbiguityLevel == "2D"));
            Level5.AddRange(psms.Where(p => p.AmbiguityLevel == "5"));

            List<PsmFromTsv> Level3 = psms.Where(p => p.AmbiguityLevel == "3").ToList();
            List<PsmFromTsv> Level4 = psms.Where(p => p.AmbiguityLevel == "4").ToList();

            // deal with splitting the the level 3 and 4 into novel categories
            foreach (PsmFromTsv psm in Level3)
            {
                if (psm.GeneName.Contains("|"))
                {
                    psm.AmbiguityInfo.GeneID = false;
                }
            }
        }

        private void InitializeAllLists()
        {
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
            Level3DC = new();
            Level4A = new();
            Level4B = new();
            Level4C = new();
            Level4D = new();
            Level5 = new();
        }
    }

    public class AmbiguityInfo
    {
        public bool PTMLocalization { get; set; }
        public bool PTMID { get; set; }
        public bool SeqDefined { get; set; }
        public bool GeneID { get; set; }
    }
}
