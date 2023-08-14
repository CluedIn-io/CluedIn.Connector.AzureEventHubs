using System;

namespace CluedIn.Connector.AzureEventHub.Services
{
    public interface IClockService
    {
        DateTimeOffset Now { get; }
    }

    internal class ClockService : IClockService
    {
        public DateTimeOffset Now => DateTimeOffset.Now;
    }
}
