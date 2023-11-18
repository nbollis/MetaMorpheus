using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity;
using Unity.Injection;
using Unity.Lifetime;

namespace YeastyBois.Database
{
    public class ContainerBootStrapper
    {
        public static void RegisterTypes(IUnityContainer container)
        {
            container.RegisterType<IYeastyBoiData, YeastyBoiDataDirectClient>("YeastyBoi",
                new TransientLifetimeManager(), new InjectionConstructor(false));

            container.RegisterType<IYeastyBoiData, MockedYeastyBoiDataClient>("MockedData",
                new TransientLifetimeManager(), new InjectionConstructor(10));
        }
    }
}
