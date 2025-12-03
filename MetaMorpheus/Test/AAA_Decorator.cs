using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;

namespace Test;

[TestFixture]
public class AAA_Decorator
{
    static string ProteomeDir = @"B:\Users\Nic\BacterialProteomics\Uniprot\Bacteria_Reviewed";
    private record RunInfo(string SearchDir, string proteomeDir, string TOMLPath);

    static RunInfo BigRun_EV = new RunInfo(
        @"D:\Projects\BacterialProteomics\BigRun_EV\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\BigRun_EV\Task Settings\Task1-ManySearchTaskconfig.toml"
    );

    static RunInfo BigRun_NotEV = new RunInfo(
        @"D:\Projects\BacterialProteomics\BigRun_notEV\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\BigRun_notEV\Task Settings\Task1-ManySearchTaskconfig.toml"
    );

    static RunInfo VaginalSpike_Control = new RunInfo(
        @"D:\Projects\BacterialProteomics\VaginalSpike_BacterialControls\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\VaginalSpike_BacterialControls\Task Settings\Task1-ManySearchTaskconfig.toml");


    static RunInfo VaginalSpike_Ascites = new RunInfo(
        @"D:\Projects\BacterialProteomics\VaginalSpike_AllAscitesAllProteomes\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\VaginalSpike_AllAscitesAllProteomes\Task Settings\Task1-ManySearchTaskconfig.toml");

    [Test]
    public void TestMethod()
    {
        RunInfo info = VaginalSpike_Control;

        var decorator = new PostHocDecorator(info.SearchDir, info.proteomeDir, info.TOMLPath);

        decorator.DecorateAndWrite();
    }   
}
