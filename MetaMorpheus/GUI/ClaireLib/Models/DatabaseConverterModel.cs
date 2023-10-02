using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaMorpheusGUI
{
    public class DatabaseConverterModel : DatabaseConverterViewModel
    {

        public DatabaseConverterModel Instance => new DatabaseConverterModel();
        public DatabaseConverterModel() : base()
        {
            DatabasePaths.Add(@"C:\Users\wpratt\Documents\GitHub\MetaMorpheus\GUI\bin\Debug\Mods\Mods.xml");
            DatabasePaths.Add(@"C:\Users\wpratt\Documents\GitHub\MetaMorpheus\GUI\bin\Debug\Mods\Mods2.xml");
            DatabasePaths.Add(@"C:\Users\wpratt\Documents\GitHub\MetaMorpheus\GUI\bin\Debug\Mods\Mods3.xml");
        }
    }
}
