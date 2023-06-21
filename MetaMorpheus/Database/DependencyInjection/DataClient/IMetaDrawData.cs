using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaDrawBackend.DependencyInjection
{
    /// <summary>
    /// Used to resolve the types on the IOC container in the host application
    /// </summary>
    public interface IMetaDrawData
    {
        MetaDrawData Data { get; set; }
    }
}
