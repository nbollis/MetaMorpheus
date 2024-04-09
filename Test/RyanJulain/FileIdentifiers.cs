using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public static class FileIdentifiers
    {
        // Ryan Julian
        public static string TryptophanFragmentIndex => "TryptophanFragments";
        public static string TryptophanFragmentCountHistogram => "FragmentCountHistogram";
        public static string MinFragmentNeededHistogram => "MinFragmentsNeededHistogram";



        // Chimera Analysis
        public static string ChimeraCountingFile => "ChimeraCounting.csv";
        public static string InternalChimeraComparison => "InternalChimeraComparison.csv";
        public static string BottomUpResultComparison => "BottomUpResultComparison.csv";
        public static string IndividualFraggerFileComparison => "IndividualFraggerFileComparison.csv";
    }
}
