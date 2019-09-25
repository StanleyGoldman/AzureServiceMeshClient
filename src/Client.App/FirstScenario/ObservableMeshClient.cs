using System;
using System.Collections.Generic;
using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Client.App.Extensions;
using Client.App.FirstScenario.Model;
using FluentColorConsole;
using Microsoft.Azure.Management.ServiceFabricMesh;
using Microsoft.Azure.Management.ServiceFabricMesh.Models;
using Serilog;

namespace Client.App.FirstScenario
{
    public class ObservableMeshClient
    {
        enum GetContainerLogsResponseEnum
        {
            NotFound,
            ResourceNotReady,
            Output
        }

        private const string ServiceResourceName = "AzureAgentResource";
        private const string CodePackageName = "AzureAgentContainer";
        private readonly Arguments _arguments;
        private readonly ILogger _logger;
        private readonly SchedulerProvider _schedulerProvider;
        private readonly ServiceFabricMeshManagementClient _serviceFabricMeshManagementClient;

        public ObservableMeshClient(ServiceFabricMeshManagementClient serviceFabricMeshManagementClient,
            ILogger logger,
            Arguments arguments,
            SchedulerProvider schedulerProvider)
        {
            _serviceFabricMeshManagementClient = serviceFabricMeshManagementClient;
            _logger = logger;
            _arguments = arguments;
            _schedulerProvider = schedulerProvider;
        }

        public IObservable<AgentStatusEnum> PollAgentStatus(string applicationResourceName, bool outputContainerLogs = true)
        {
            return Observable.Return(AgentStatusEnum.Unknown)
                .Concat(Observable.Create<AgentStatusEnum>(observer =>
                {
                    var disposables = new CompositeDisposable();

                    var (stateOutput, containerOutput) = GetContainerLogs(applicationResourceName)
                        .Concat(Observable
                            .Empty<(GetContainerLogsResponseEnum, string)>()
                            .Delay(TimeSpan.FromSeconds(1)))
                        .Repeat()
                        .SplitTuple();

                    var lastAgentState = AgentStatusEnum.Unknown;

                    var stateOutputSubscription = stateOutput
                        .DistinctUntilChanged()
                        .CombineLatest(Observable.Return(String.Empty)
                                .Concat(containerOutput
                                    .Where(s => s != null)
                                    .DistinctUntilChanged()
                                    .SplitRepeatedPrefixByNewline()
                                    .SelectMany(strings => strings)
                                    .Do(s =>
                                    {
                                        if (outputContainerLogs)
                                        {
                                            ColorConsole.WithDarkGreenText.WriteLine(s);
                                        }
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

                            if ((lastAgentState != AgentStatusEnum.Unknown && lastAgentState != AgentStatusEnum.NotReady)
                                && containerLogsResponse != GetContainerLogsResponseEnum.Output)
                            {
                                observer.OnNext(AgentStatusEnum.NotFound);
                                observer.OnCompleted();
                            }
                        }, observer.OnError, observer.OnCompleted);
                    disposables.Add(stateOutputSubscription);

                    return disposables;
                }));
        }

        private IObservable<(GetContainerLogsResponseEnum, string)> GetContainerLogs(string applicationResourceName)
        {

            return Observable.FromAsync(async cancellationToken =>
            {
                _logger.Verbose("Querying Logs");

                try
                {
                    var response = await _serviceFabricMeshManagementClient
                        .CodePackage
                        .GetContainerLogsWithHttpMessagesAsync(_arguments.ResourceGroup, applicationResourceName,
                            ServiceResourceName,
                            "0",
                            CodePackageName, cancellationToken: cancellationToken);

                    var data = response.Body.Content;

                    return (GetContainerLogsResponseEnum.Output, data);
                }
                catch (ErrorModelException e)
                {
                    if (e.Response.StatusCode == HttpStatusCode.NotFound || e.Response.StatusCode == HttpStatusCode.BadRequest)
                        return (GetContainerLogsResponseEnum.NotFound, (string) null);

                    if (e.Body.Error.Code == "NotReady")
                        return (GetContainerLogsResponseEnum.ResourceNotReady, (string) null);

                    throw;
                }
                finally
                {
                    _logger.Verbose("Queried Logs");
                }
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }

        public IObservable<ApplicationStatusEnum> PollApplicationStatus(string applicationResourceName,
            SchedulerProvider schedulerProvider)
        {
            var untilSubject = new Subject<Unit>();

            var observable = Observable.FromAsync<(ApplicationStatusEnum, object)>(async cancellationToken =>
                {
                    _logger.Verbose("Querying Application");

                    try
                    {
                        var response = await _serviceFabricMeshManagementClient
                            .Application
                            .GetWithHttpMessagesAsync(_arguments.ResourceGroup, applicationResourceName,
                                cancellationToken: cancellationToken);

                        var responseData = new
                        {
                            response.Body.ProvisioningState,
                            response.Body.HealthState,
                            response.Body.Status
                        };

                        return (Enum.Parse<ApplicationStatusEnum>(response.Body.Status), responseData);
                    }
                    catch (ErrorModelException e)
                    {
                        if (e.Response.StatusCode == HttpStatusCode.NotFound)
                            return (ApplicationStatusEnum.NotFound, null);

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
                .SubscribeOn(schedulerProvider.TaskPool);
        }

        public IObservable<ServiceStatusEnum> PollServiceStatus(string applicationResourceName)
        {
            var untilSubject = new Subject<Unit>();

            return Observable.Return(ServiceStatusEnum.Unknown)
                .Concat(Observable.FromAsync<(ServiceStatusEnum, object)>(async cancellationToken =>
                    {
                        _logger.Verbose("Start Service.Get");

                        try
                        {
                            var response = await _serviceFabricMeshManagementClient
                                .Service
                                .GetWithHttpMessagesAsync(_arguments.ResourceGroup, applicationResourceName,
                                    ServiceResourceName,
                                    cancellationToken: cancellationToken);

                            var responseData = new
                            {
                                response.Body.ProvisioningState,
                                response.Body.HealthState,
                                response.Body.Status
                            };

                            return (Enum.Parse<ServiceStatusEnum>(response.Body.Status), responseData);
                        }
                        catch (ErrorModelException e)
                        {
                            if (e.Response.StatusCode == HttpStatusCode.NotFound)
                                return (ServiceStatusEnum.NotFound, null);

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
                        }

                        return tuple.Item1;
                    }))
                .SubscribeOn(_schedulerProvider.TaskPool);
        }

        public async Task<string> CreateMesh(CancellationToken token)
        {
            var meshName = _arguments.Name + CleanGuid();
            var applicationResourceDescription = new ApplicationResourceDescription
            {
                Location = "eastus",
                Services = new List<ServiceResourceDescription>(new[]
                {
                    new ServiceResourceDescription
                    {
                        Name = ServiceResourceName,
                        OsType = "linux",
                        CodePackages = new List<ContainerCodePackageProperties>(new[]
                        {
                            new ContainerCodePackageProperties
                            {
                                Name = CodePackageName,
                                ImageRegistryCredential = new ImageRegistryCredential(
                                    _arguments.ImageRegistryServer,
                                    _arguments.ImageRegistryUsername,
                                    _arguments.ImageRegistryPassword),
                                Image = _arguments.ImageName,
                                EnvironmentVariables = new List<EnvironmentVariable>(new[]
                                {
                                    new EnvironmentVariable("AZP_AGENT_NAME", $"agent-{CleanGuid()}"),
                                    new EnvironmentVariable("AZP_URL", _arguments.AzurePipelinesUrl),
                                    new EnvironmentVariable("AZP_TOKEN", _arguments.AzurePipelinesToken)
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

            var createMeshResponse =
                await _serviceFabricMeshManagementClient.Application.CreateWithHttpMessagesAsync(
                    _arguments.ResourceGroup,
                    meshName,
                    applicationResourceDescription,
                    cancellationToken: token);

            _logger.Verbose("CreateMeshResponse.Body {@ResponseBody}", createMeshResponse.Body);

            return meshName;
        }

        public async Task DeleteMesh(string meshName, CancellationToken token)
        {
            await _serviceFabricMeshManagementClient.Application.DeleteWithHttpMessagesAsync(_arguments.ResourceGroup,
                meshName, cancellationToken: token);
        }

        private static string CleanGuid()
        {
            return Guid.NewGuid().ToString().Replace("-", String.Empty);
        }
    }
}