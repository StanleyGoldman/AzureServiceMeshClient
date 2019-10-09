using System;
using System.Collections.Generic;
using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Client.App.Extensions;
using Client.App.Model;
using FluentColorConsole;
using Microsoft.Azure.Management.ServiceFabricMesh;
using Microsoft.Azure.Management.ServiceFabricMesh.Models;
using Microsoft.Rest.Azure;
using Serilog;

namespace Client.App.Services
{
    public class ObservableMeshClient
    {
        private const string ServiceResourceName = "AzureAgentResource";
        private const string CodePackageName = "AzureAgentContainer";
        private readonly ILogger _logger;
        private readonly SchedulerProvider _schedulerProvider;
        private readonly ServiceFabricMeshManagementClient _serviceFabricMeshManagementClient;

        public ObservableMeshClient(ServiceFabricMeshManagementClient serviceFabricMeshManagementClient,
            ILogger logger,
            SchedulerProvider schedulerProvider)
        {
            _serviceFabricMeshManagementClient = serviceFabricMeshManagementClient;
            _logger = logger;
            _schedulerProvider = schedulerProvider;
        }

        public IObservable<AgentStatusEnum> PollAgentStatus(string applicationResourceName, string resourceGroupName,
            IObservable<Unit> startPollingObservable, IObservable<Unit> applicationFailedObservable, int replica,
            bool outputContainerLogs = true)
        {
            var untilSubject = new Subject<Unit>();
            var switchSubject = new Subject<IObservable<AgentStatusEnum>>();

            var activeObservable = Observable.Create<AgentStatusEnum>(observer =>
            {
                var disposables = new CompositeDisposable();

                var (stateOutput, containerOutput) =
                    GetContainerLogs(resourceGroupName, applicationResourceName, replica)
                        .Concat(Observable
                            .Empty<(GetContainerLogsResponseEnum, string)>()
                            .Delay(TimeSpan.FromSeconds(1)))
                        .Repeat()
                        .TakeUntil(untilSubject)
                        .SplitTuple();

                var lastAgentState = AgentStatusEnum.Unknown;

                var stateOutputSubscription = stateOutput
                    .DistinctUntilChanged()
                    .CombineLatest(Observable.Return(string.Empty)
                            .Concat(containerOutput
                                .Where(s => s != null)
                                .DistinctUntilChanged()
                                .SplitRepeatedPrefixByNewline()
                                .SelectMany(strings => strings)
                                .Do(s =>
                                {
                                    if (outputContainerLogs) ColorConsole.WithDarkGreenText.WriteLine(s);
                                })),
                        (state, s) => (state, s))
                    .Subscribe(tuple =>
                    {
                        var (containerLogsResponse, s) = tuple;

                        if (lastAgentState == AgentStatusEnum.Unknown
                            && containerLogsResponse == GetContainerLogsResponseEnum.ResourceNotReady)
                        {
                            lastAgentState = AgentStatusEnum.NotReady;
                            observer.OnNext(lastAgentState);
                            return;
                        }

                        if ((lastAgentState == AgentStatusEnum.Unknown || lastAgentState == AgentStatusEnum.NotReady)
                            && containerLogsResponse == GetContainerLogsResponseEnum.Output)
                        {
                            lastAgentState = AgentStatusEnum.Starting;
                            observer.OnNext(lastAgentState);
                            return;
                        }

                        if (lastAgentState == AgentStatusEnum.Starting
                            && containerLogsResponse == GetContainerLogsResponseEnum.Output
                            && s != null
                            && s.Contains("Listening for Jobs"))
                        {
                            lastAgentState = AgentStatusEnum.Ready;
                            observer.OnNext(lastAgentState);
                            return;
                        }

                        if (lastAgentState != AgentStatusEnum.Unknown && lastAgentState != AgentStatusEnum.NotReady
                                                                      && containerLogsResponse !=
                                                                      GetContainerLogsResponseEnum.Output)
                        {
                            observer.OnNext(AgentStatusEnum.NotFound);
                            observer.OnCompleted();
                            switchSubject.OnCompleted();
                        }
                    }, observer.OnError, observer.OnCompleted);
                disposables.Add(stateOutputSubscription);

                return disposables;
            });

            startPollingObservable.Subscribe(unit => { switchSubject.OnNext(activeObservable); });

            applicationFailedObservable.Subscribe(unit =>
            {
                switchSubject.OnNext(Observable.Return(AgentStatusEnum.Failed));
                switchSubject.OnCompleted();
            });

            return Observable.Return(AgentStatusEnum.Unknown)
                .Concat(switchSubject.Switch());
        }

        private IObservable<(GetContainerLogsResponseEnum, string)> GetContainerLogs(string resourceGroupName,
            string applicationResourceName, int replica)
        {
            return Observable.DeferAsync(async cancellationToken =>
            {
                _logger.Verbose("Querying Logs");

                AzureOperationResponse<ContainerLogs> response = null;
                try
                {
                    response = await _serviceFabricMeshManagementClient
                        .CodePackage
                        .GetContainerLogsWithHttpMessagesAsync(resourceGroupName, applicationResourceName,
                            ServiceResourceName,
                            replica.ToString(),
                            CodePackageName, cancellationToken: cancellationToken);

                    var data = response.Body.Content;

                    return Observable.Return<(GetContainerLogsResponseEnum, string)>((
                        GetContainerLogsResponseEnum.Output, data));
                }
                catch (ErrorModelException e)
                {
                    if (e.Response.StatusCode == HttpStatusCode.NotFound ||
                        e.Response.StatusCode == HttpStatusCode.BadRequest)
                        return Observable.Return<(GetContainerLogsResponseEnum, string)>((
                            GetContainerLogsResponseEnum.NotFound, (string) null));

                    if (e.Body.Error.Code == "NotReady")
                        return Observable.Return<(GetContainerLogsResponseEnum, string)>((
                            GetContainerLogsResponseEnum.ResourceNotReady, (string) null));

                    _logger.Error(e, "Container Logs Error {Response}", response);
                    throw;
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Container Logs Error {Response}", response);
                    throw;
                }
                finally
                {
                    _logger.Verbose("Queried Logs");
                }
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }

        public IObservable<ApplicationStatusEnum> PollApplicationStatus(string applicationResourceName,
            string resourceGroupName)
        {
            var untilSubject = new Subject<Unit>();

            var observable = Observable.DeferAsync(async cancellationToken =>
                {
                    _logger.Verbose("Querying Application");

                    AzureOperationResponse<ApplicationResourceDescription> response = null;
                    try
                    {
                        response = await _serviceFabricMeshManagementClient
                            .Application
                            .GetWithHttpMessagesAsync(resourceGroupName, applicationResourceName,
                                cancellationToken: cancellationToken);

                        var responseData = new
                        {
                            response.Body.ProvisioningState,
                            response.Body.HealthState,
                            response.Body.Status
                        };

                        return Observable.Return<(ApplicationStatusEnum, object)>((
                            Enum.Parse<ApplicationStatusEnum>(response.Body.Status), responseData));
                    }
                    catch (ErrorModelException e)
                    {
                        if (e.Response.StatusCode == HttpStatusCode.NotFound)
                            return Observable.Return<(ApplicationStatusEnum, object)>((ApplicationStatusEnum.NotFound,
                                null));

                        _logger.Error(e, "Application Status Error {Response}", response);
                        throw;
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Application Status Error {Response}", response);
                        throw;
                    }
                    finally
                    {
                        _logger.Verbose("Queried Logs");
                    }
                })
                .Concat(Observable.Empty<(ApplicationStatusEnum, object)>().Delay(TimeSpan.FromSeconds(1)))
                .Repeat()
                .TakeUntil(untilSubject)
                .DistinctUntilChanged()
                .Select(tuple =>
                {
                    _logger.Verbose("ApplicationStatus {Status} Response Data {@Data}", tuple.Item1, tuple.Item2);

                    if (tuple.Item1 == ApplicationStatusEnum.NotFound)
                    {
                        untilSubject.OnNext(Unit.Default);
                        untilSubject.OnCompleted();
                    }

                    return tuple.Item1;
                });

            return Observable.Return(ApplicationStatusEnum.Unknown)
                .Concat(observable)
                .SubscribeOn(_schedulerProvider.TaskPool);
        }

        public IObservable<ServiceStatusEnum> PollServiceStatus(string applicationResourceName,
            string resourceGroupName, IObservable<Unit> startPollingObservable,
            IObservable<Unit> applicationFailedObservable)
        {
            var untilSubject = new Subject<Unit>();
            var switchSubject = new Subject<IObservable<ServiceStatusEnum>>();

            var activeObservable = Observable.Return(ServiceStatusEnum.Unknown)
                .Concat(Observable.DeferAsync(async cancellationToken =>
                    {
                        _logger.Verbose("Start Service.Get");

                        AzureOperationResponse<ServiceResourceDescription> response = null;
                        try
                        {
                            response = await _serviceFabricMeshManagementClient
                                .Service
                                .GetWithHttpMessagesAsync(resourceGroupName, applicationResourceName,
                                    ServiceResourceName,
                                    cancellationToken: cancellationToken);

                            var responseData = new
                            {
                                response.Body.ProvisioningState,
                                response.Body.HealthState,
                                response.Body.Status
                            };

                            return Observable.Return<(ServiceStatusEnum, object)>((
                                Enum.Parse<ServiceStatusEnum>(response.Body.Status), responseData));
                        }
                        catch (ErrorModelException e)
                        {
                            if (e.Response.StatusCode == HttpStatusCode.NotFound)
                                return Observable.Return<(ServiceStatusEnum, object)>(
                                    (ServiceStatusEnum.NotFound, null));

                            _logger.Error(e, "Service Status Error {Response}", response);
                            throw;
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e, "Service Status Error {Response}", response);
                            throw;
                        }
                        finally
                        {
                            _logger.Verbose("Queried Logs");
                        }
                    })
                    .Concat(Observable.Empty<(ServiceStatusEnum, object)>()
                        .Delay(TimeSpan.FromSeconds(1)))
                    .Repeat()
                    .TakeUntil(untilSubject)
                    .DistinctUntilChanged()
                    .Select(tuple =>
                    {
                        _logger.Verbose("ServiceStatus {Status} Response Data {@Data}", tuple.Item1, tuple.Item2);

                        if (tuple.Item1 == ServiceStatusEnum.NotFound)
                        {
                            untilSubject.OnNext(Unit.Default);
                            untilSubject.OnCompleted();

                            switchSubject.OnCompleted();
                        }

                        return tuple.Item1;
                    }))
                .SubscribeOn(_schedulerProvider.TaskPool);


            startPollingObservable.Subscribe(unit => { switchSubject.OnNext(activeObservable); });

            applicationFailedObservable.Subscribe(unit =>
            {
                switchSubject.OnNext(Observable.Return(ServiceStatusEnum.Failed));
                switchSubject.OnCompleted();
            });

            return Observable.Return(ServiceStatusEnum.Unknown)
                .Concat(switchSubject.Switch());
        }

        public IObservable<string> CreateOrEditMesh(string applicationResourceName, string imageRegistryServer,
            string imageRegistryUsername, string imageRegistryPassword, string imageName, string azurePipelinesUrl,
            string azurePipelinesToken, string resourceGroupName, int? replicaCount = null)
        {
            return Observable.DeferAsync(async token =>
            {
                var applicationResourceDescription = CreateApplicationResourceDescription(imageRegistryServer,
                    imageRegistryUsername, imageRegistryPassword, imageName, azurePipelinesUrl, azurePipelinesToken,
                    replicaCount);

                var createMeshResponse =
                    await _serviceFabricMeshManagementClient.Application.CreateWithHttpMessagesAsync(
                        resourceGroupName,
                        applicationResourceName,
                        applicationResourceDescription,
                        cancellationToken: token);

                _logger.Verbose("CreateMeshResponse.Body {@ResponseBody}", createMeshResponse.Body);

                return Observable.Return(applicationResourceName);
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }

        private static ApplicationResourceDescription CreateApplicationResourceDescription(string imageRegistryServer,
            string imageRegistryUsername, string imageRegistryPassword, string imageName, string azurePipelinesUrl,
            string azurePipelinesToken, int? replicaCount = null)
        {
            var applicationResourceDescription = new ApplicationResourceDescription
            {
                Location = "eastus",
                Services = new List<ServiceResourceDescription>(new[]
                {
                    new ServiceResourceDescription
                    {
                        Name = ServiceResourceName,
                        OsType = "linux",
                        ReplicaCount = replicaCount,
                        CodePackages = new List<ContainerCodePackageProperties>(new[]
                        {
                            new ContainerCodePackageProperties
                            {
                                Name = CodePackageName,
                                ImageRegistryCredential = new ImageRegistryCredential(
                                    imageRegistryServer,
                                    imageRegistryUsername,
                                    imageRegistryPassword),
                                Image = imageName,
                                EnvironmentVariables = new List<EnvironmentVariable>(new[]
                                {
                                    new EnvironmentVariable("AZP_AGENT_NAME", $"agent-{Common.CleanGuid()}"),
                                    new EnvironmentVariable("AZP_URL", azurePipelinesUrl),
                                    new EnvironmentVariable("AZP_TOKEN", azurePipelinesToken)
                                }),
                                Resources = new ResourceRequirements
                                {
                                    Requests = new ResourceRequests(1, 1)
                                }
                            }
                        })
                    }
                })
            };
            return applicationResourceDescription;
        }

        public IObservable<Unit> DeleteMesh(string meshName, string resourceGroupName)
        {
            return Observable.DeferAsync(async token =>
            {
                await _serviceFabricMeshManagementClient.Application.DeleteWithHttpMessagesAsync(
                    resourceGroupName,
                    meshName, cancellationToken: token);

                return Observable.Return(Unit.Default);
            });
        }

        private enum GetContainerLogsResponseEnum
        {
            NotFound,
            ResourceNotReady,
            Output
        }
    }
}