using EngineLayer.DatabaseLoading;
using MzLibUtil;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer.FragmentTypeDetection;

namespace Test.FragmentDetection;

[TestFixture]
public class Runner
{
    [Test]
    public void TestMethod()
    {
        var outputFolder = @"D:\Projects\FragmentDetection\Output";
        if (!System.IO.Directory.Exists(outputFolder))
        {
            System.IO.Directory.CreateDirectory(outputFolder);
        }


        var dbPath = @"D:\Projects\FragmentDetection\6PMIX-FRC-01MAY2023-0006.fasta";
        var specpath = @"D:\Projects\FragmentDetection\C2_UAST_6prot_Oct015.mgf";

        List<DbForTask> dbs = [new (dbPath, false)];
        List<string> spectraFiles = [specpath];

        var task = new FragmentTypeDetectionTask();

        // Manually set parameters taht changed from defaults for this specific data file
        task.CommonParameters.PrecursorMassTolerance = new PpmTolerance(20);
        task.CommonParameters.ProductMassTolerance = new PpmTolerance(50);
        task.CommonParameters.ListOfModsFixed.Clear();
        task.CommonParameters.ListOfModsVariable.Clear();
        task.CommonParameters.ListOfModsVariable.Add(("Common Fixed", "Carbamidomethyl on C"));
        task.CommonParameters.ListOfModsVariable.Add(("Common Fixed", "Carbamidomethyl on U"));
        task.CommonParameters.ListOfModsVariable.Add(("Common Variable", "Oxidation on M"));

        var results = task.RunTask(outputFolder, dbs, spectraFiles, "FragmentDetectionTest");
    }
}
