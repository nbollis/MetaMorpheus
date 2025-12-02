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
    [Test]
    public void TestMethod()
    {
        string searchDir = @"D:\Projects\BacterialProteomics\BigRun_notEV\Task1-ManySearchTask";

        string proteomeDir = @"B:\Users\Nic\BacterialProteomics\Uniprot\Bacteria_Reviewed";

        string TOMLPath = @"D:\Projects\BacterialProteomics\BigRun_notEV\Task Settings\Task1-ManySearchTaskconfig.toml";

        var decorator = new PostHocDecorator(searchDir, proteomeDir, TOMLPath);

        decorator.DecorateAndWrite();

    }   
}
