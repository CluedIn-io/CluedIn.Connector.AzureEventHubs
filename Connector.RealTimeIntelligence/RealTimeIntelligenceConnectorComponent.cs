using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Castle.MicroKernel.Registration;
using CluedIn.Connector.AzureEventHub;
using CluedIn.Connector.AzureEventHub.Connector;
using CluedIn.Core;
using CluedIn.Core.Accounts;
using CluedIn.Core.Providers;
using CluedIn.Core.Server;
using CluedIn.Core.Streams;
using CluedIn.Core.Streams.Models;
using ComponentHost;
using Microsoft.Extensions.Logging;

namespace CluedIn.Connector.RealTimeIntelligence
{
    [Component(RealTimeIntelligenceConstants.ProviderName, "Providers", ComponentType.Service, ServerComponents.ProviderWebApi, Components.Server, Components.DataStores, Isolation = ComponentIsolation.NotIsolated)]
    public sealed class RealTimeIntelligenceConnectorComponent : ServiceApplicationComponent<IServer>
    {
        /**********************************************************************************************************
         * CONSTRUCTOR
         **********************************************************************************************************/

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureEventHubConnectorComponent" /> class.
        /// </summary>
        /// <param name="componentInfo">The component information.</param>
        public RealTimeIntelligenceConnectorComponent(ComponentInfo componentInfo) : base(componentInfo)
        {
            // Dev. Note: Potential for compiler warning here ... CA2214: Do not call overridable methods in constructors
            //   this class has been sealed to prevent the CA2214 waring being raised by the compiler
            Container.Register(Component.For<RealTimeIntelligenceConnectorComponent>().Instance(this));

            //Container.Register(Component.For<ISqlClient>().ImplementedBy<SqlClient>().OnlyNewServices());
        }

        /**********************************************************************************************************
         * METHODS
         **********************************************************************************************************/

        /// <summary>Starts this instance.</summary>
        public override void Start()
        {

            //Container.Install(new InstallComponents());

            var asm = Assembly.GetExecutingAssembly();
            Container.Register(Types.FromAssembly(asm).BasedOn<IProvider>().WithServiceFromInterface().If(t => !t.IsAbstract).LifestyleSingleton());
           // Container.Register(Types.FromAssembly(asm).BasedOn<IEntityActionBuilder>().WithServiceFromInterface().If(t => !t.IsAbstract).LifestyleSingleton());

            this.Log.LogInformation("[RealTimeIntelligence] RealTime Intelligence Registered");
            State = ServiceState.Started;
        }

        /// <summary>Stops this instance.</summary>
        public override void Stop()
        {
            if (State == ServiceState.Stopped)
                return;

            State = ServiceState.Stopped;
        }
    }
}
