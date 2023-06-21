using GuiFunctions;
using MetaDrawBackend.DependencyInjection;
using Unity;

namespace MetaDrawGUI.ViewModels
{
    /// <summary>
    /// A base view model that fires Property Changed events as needed and contains a reference to the database
    /// </summary>
    public class BaseMetaDrawViewModel : BaseViewModel
    {
        protected MetaDrawData Data;
        protected IUnityContainer Container { get; init; }

        public BaseMetaDrawViewModel()
        {
            Container = new UnityContainer();
            ContainerBootStrapper.RegisterTypes(Container);

            Data = Container.Resolve<IMetaDrawData>("MockedData").Data;
        }
    }
}
