using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using CluedIn.Connector.AzureEventHub.Services;

namespace CluedIn.Connector.AzureEventHub
{
    public class InstallComponents : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            container.Register(Component.For<IClockService>().ImplementedBy<ClockService>().LifestyleSingleton());
        }
    }
}
