using System.Reactive.Concurrency;

namespace Client.App
{
    public class SchedulerProvider
    {
        public IScheduler TaskPool => TaskPoolScheduler.Default;
    }
}