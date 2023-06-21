using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MetaDrawBackend.DependencyInjection;
using Unity;
using Unity.Injection;
using Unity.Lifetime;

namespace MetaDrawGUI
{
    public class ContainerBootStrapper
    {
        public static void RegisterTypes(IUnityContainer container)
        {
            container.RegisterType<IMetaDrawData, MockMetaDrawDatabaseDirectClient>("MockedData",
                new TransientLifetimeManager(), new InjectionConstructor(10));

            container.RegisterType<IMetaDrawData, MetaDrawDatabaseDirectClient>("DbData",
                new TransientLifetimeManager(), new InjectionConstructor(false));
        }
    }
}
