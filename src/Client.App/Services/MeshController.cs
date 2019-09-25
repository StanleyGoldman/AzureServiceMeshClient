using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Client.App.Model;
using Serilog;

namespace Client.App.Services
{
    public class MeshController
    {
        private readonly ILogger _logger;
        private readonly ObservableMeshClient _observableMeshClient;
        private readonly SchedulerProvider _schedulerProvider;
        private string _meshName;
        private IDisposable _pollingDisposable;
        private bool _started;
        private AutoResetEvent _autoResetEvent;

        public MeshController(ILogger logger, SchedulerProvider schedulerProvider,
            ObservableMeshClient observableMeshClient)
        {
            _logger = logger;
            _schedulerProvider = schedulerProvider;
            _observableMeshClient = observableMeshClient;
        }

        public (IObservable<Unit> request, IObservable<Unit> complete) Toggle()
        {
            return !_started ? Start() : Stop();
        }

        private (IObservable<Unit> request, IObservable<Unit> ready) Start()
        {
            var readySubject = new Subject<Unit>();
            var request = Observable.StartAsync(async token =>
            {
                _autoResetEvent = new AutoResetEvent(false);
                _logger.Information("Starting");
                _meshName = await _observableMeshClient.CreateMesh();

                _started = true;

                _logger.Information("Started Mesh {Mesh}", _meshName);

                var pollAgentStatus = _observableMeshClient.PollAgentStatus(_meshName)
                    .Replay();

                var pollApplicationStatus = _observableMeshClient.PollApplicationStatus(_meshName, _schedulerProvider)
                    .Replay();

                var pollServiceStatus = _observableMeshClient.PollServiceStatus(_meshName)
                    .Replay();

                var pollReplicaStateSubscription = pollAgentStatus
                    .SubscribeOn(_schedulerProvider.TaskPool)
                    .Subscribe(agentStateEnum => {},
                        exception => { _logger.Error("Container Logs Error: {Message}", exception.Message); });

                var applicationStateObservable = pollApplicationStatus
                    .SubscribeOn(_schedulerProvider.TaskPool)
                    .Subscribe(applicationStatusEnum => { },
                        exception =>
                        {
                            _logger.Error("Application Error: {Message}", exception.Message);
                        });

                var serviceDataSubscription = pollServiceStatus
                    .SubscribeOn(_schedulerProvider.TaskPool)
                    .Subscribe(serviceStatusEnum => { },
                        exception =>
                        {
                            _logger.Error("Service Error: {Message}", exception.Message);
                        });

                var combinedSubscription = pollApplicationStatus.CombineLatest(pollServiceStatus, pollAgentStatus,
                        (applicationStatus, serviceStatus, agentStatus) =>
                            (applicationStatus, serviceStatus, agentStatus))
                    .DistinctUntilChanged()
                    .SubscribeOn(_schedulerProvider.TaskPool)
                    .Subscribe(tuple =>
                        {
                            var (applicationStatus, serviceStatus, agentStatus) = tuple;
                            _logger.Information("Mesh {Mesh} Application {Application,10} Service {Service,10} Agent {Agent,10}", _meshName, applicationStatus, serviceStatus, agentStatus);

                            if (applicationStatus == ApplicationStatusEnum.Ready &&
                                serviceStatus == ServiceStatusEnum.Ready && agentStatus == AgentStatusEnum.Ready)
                            {
                                readySubject.OnNext(Unit.Default);
                                readySubject.OnCompleted();
                            }
                        },
                        () =>
                        {
                            _logger.Debug("Streams completed");
                            _autoResetEvent.Set();
                        });

                pollApplicationStatus.Connect();
                pollServiceStatus.Connect();
                pollAgentStatus.Connect();

                _pollingDisposable = new CompositeDisposable(pollReplicaStateSubscription, applicationStateObservable, serviceDataSubscription, combinedSubscription);
            }).SubscribeOn(_schedulerProvider.TaskPool);

            var ready = readySubject.AsObservable();
            return (request, ready);
        }

        private (IObservable<Unit> request, IObservable<Unit> complete) Stop()
        {
            IObservable<Unit> completeObservable = null;

            var request = Observable.StartAsync(async token =>
            {
                _logger.Information("Stopping");

                await _observableMeshClient.DeleteMesh(_meshName);

                completeObservable= Observable.Start(() =>
                {
                    _autoResetEvent.WaitOne();

                    _logger.Information("Stopped");

                    _started = false;

                    _pollingDisposable?.Dispose();
                    _pollingDisposable = null;
                }).SubscribeOn(_schedulerProvider.TaskPool);
            }).SubscribeOn(_schedulerProvider.TaskPool);

            return (request, completeObservable);
        }

        public IObservable<Unit> Quit()
        {
            return Observable.DeferAsync(async token =>
            {
                _logger.Information("Quitting");

                if (_started)
                {
                    var (request, complete) = Stop();
                    await request;
                }

                _logger.Debug("Quit");

                return Observable.Return(Unit.Default);
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }
    }
}