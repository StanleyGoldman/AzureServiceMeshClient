using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Client.App.Extensions;
using Client.App.FirstScenario.Model;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ServiceFabricMesh;
using Microsoft.Azure.Management.ServiceFabricMesh.Models;
using Microsoft.Rest.Azure;
using Serilog;

namespace Client.App.FirstScenario
{
    public class MeshController
    {
        private const string ServiceResourceName = "AzureAgentResource";
        private const string ServiceContainerName = "AzureAgentContainer";
        private readonly ILogger _logger;
        private readonly SchedulerProvider _schedulerProvider;
        private Arguments _arguments;
        private bool _initialized;
        private string _meshName;
        private IDisposable _pollingDisposable;
        private ServiceFabricMeshManagementClient _serviceFabricMeshManagementClient;
        private bool _started;

        public MeshController(ILogger logger, SchedulerProvider schedulerProvider)
        {
            _logger = logger;
            _schedulerProvider = schedulerProvider;
        }

        private static string CleanGuid()
        {
            return Guid.NewGuid().ToString().Replace("-", string.Empty);
        }

        public void Initialize(Arguments arguments)
        {
            if (_serviceFabricMeshManagementClient == null)
            {
                var credentials = SdkContext.AzureCredentialsFactory
                    .FromServicePrincipal(arguments.ClientId,
                        arguments.ClientSecret,
                        arguments.TenantId,
                        AzureEnvironment.AzureGlobalCloud);

                _serviceFabricMeshManagementClient = new ServiceFabricMeshManagementClient(credentials)
                {
                    SubscriptionId = arguments.SubscriptionId
                };
            }

            _initialized = true;
            _arguments = arguments;
        }

        public IObservable<Unit> Toggle()
        {
            return !_started ? Start() : Stop();
        }

        private IObservable<Unit> Start()
        {
            return Observable.FromAsync(async token =>
            {
                if (!_initialized) throw new InvalidOperationException("MeshController not initialized");

                _logger.Information("Starting");
                _meshName = _arguments.Name + CleanGuid();

                var applicationResourceDescription = await CreateMesh(_meshName, token);

                _started = true;

                _logger.Information("Mesh Requested {Name}", _meshName);
                _logger.Information("Started");

                var serviceReadySubject = new Subject<IObservable<string>>();

                var serviceReadyObservable = serviceReadySubject.AsObservable()
                    .Switch();

                var serviceReady = false;

                var runningPolls = new[]
                {
                    serviceReadySubject,

                    serviceReadyObservable
                        .SplitRepeatedPrefixByNewline()
                        .SelectMany(strings => strings)
                        .Subscribe(item =>
                            {
                                _logger.Information("Container: {@Output}", item);
                            },
                            exception => { _logger.Error("Container Logs Error: {Message}", exception.Message); }),

                    GetApplicationData()
                        .Concat(Observable
                            .Empty<(AzureOperationResponse<ApplicationResourceDescription> response, ApplicationData
                                data)>()
                            .Delay(TimeSpan.FromSeconds(1)))
                        .Repeat()
                        .WhenChanges(tuple => tuple.data)
                        .Subscribe(tuple =>
                            {
                                _logger.Verbose("Get Application Response {@Body}", tuple.response.Body);
                                _logger.Information("Application {@Info}", tuple.data);
                            },
                            exception => { _logger.Error("Application Error: {Message}", exception.Message); }),

                    GetServiceData()
                        .Concat(Observable
                            .Empty<(AzureOperationResponse<ServiceResourceDescription> response, ServiceData data)>()
                            .Delay(TimeSpan.FromSeconds(1)))
                        .Repeat()
                        .WhenChanges(tuple => tuple.data)
                        .Subscribe(tuple =>
                            {
                                _logger.Verbose("Get Service Response {@Body}", tuple.response.Body);
                                _logger.Information("Service {@Info}", tuple.data);

                                if (!serviceReady && tuple.data.Status == "Ready")
                                {
                                    _logger.Debug("Starting Container Output Polling");

                                    var logDataObservable = GetLogData()
                                        .Concat(Observable
                                            .Empty<string>()
                                            .Delay(TimeSpan.FromSeconds(1)))
                                        .Repeat();

                                    serviceReadySubject.OnNext(logDataObservable);
                                    serviceReady = true;
                                }
                            },
                            exception => { _logger.Error("Service Error: {Message}", exception.Message); })
                };

                _pollingDisposable = Disposable.Create(() =>
                {
                    foreach (var disposable in runningPolls) disposable.Dispose();
                });

                return Unit.Default;
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }

        private IObservable<(AzureOperationResponse<ServiceResourceDescription> response, ServiceData data)>
            GetServiceData()
        {
            return Observable.FromAsync(async cancellationToken =>
            {
                _logger.Verbose("Querying Service");

                var response = await _serviceFabricMeshManagementClient
                    .Service
                    .GetWithHttpMessagesAsync(_arguments.ResourceGroup, _meshName, ServiceResourceName,
                        cancellationToken: cancellationToken);

                _logger.Verbose("Queried Service");

                var data = new ServiceData
                {
                    Status = response.Body.Status,
                    HealthState = response.Body.HealthState,
                    ProvisioningState = response.Body.ProvisioningState,
                    CodePackages = response.Body.CodePackages.Select(properties => new ServiceCodePackage
                    {
                        Name = properties.Name
                    }).ToArray()
                };

                return (response, data);
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }

        private IObservable<string> GetLogData()
        {
            return Observable.DeferAsync(async cancellationToken =>
            {
                _logger.Verbose("Querying Logs");

                try
                {
                    var response = await _serviceFabricMeshManagementClient
                        .CodePackage
                        .GetContainerLogsWithHttpMessagesAsync(_arguments.ResourceGroup, _meshName, ServiceResourceName,
                            "0", ServiceContainerName, cancellationToken: cancellationToken);

                    var data = response.Body.Content;

                    return Observable.Return(data);
                }
                catch (ErrorModelException e)
                {
                    if (e.Response.StatusCode == HttpStatusCode.NotFound
                        || e.Body.Error.Code == "ResourceNotReady")
                        return Observable.Empty<string>();

                    throw;
                }
                finally
                {
                    _logger.Verbose("Queried Logs");
                }
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }

        private IObservable<(AzureOperationResponse<ApplicationResourceDescription> response, ApplicationData data)>
            GetApplicationData()
        {
            return Observable.FromAsync(async cancellationToken =>
            {
                _logger.Verbose("Querying Application");

                var response = await _serviceFabricMeshManagementClient
                    .Application
                    .GetWithHttpMessagesAsync(_arguments.ResourceGroup, _meshName,
                        cancellationToken: cancellationToken);

                _logger.Verbose("Queried Application");

                var data = new ApplicationData
                {
                    Status = response.Body.Status,
                    HealthState = response.Body.HealthState,
                    ProvisioningState = response.Body.ProvisioningState,
                    Services = response.Body.Services?.Select((service, i) => new ApplicationServiceData
                    {
                        HealthState = service.HealthState
                    }).ToArray()
                };

                return (response, data);
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }

        private async Task<ApplicationResourceDescription> CreateMesh(string meshName, CancellationToken token)
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
                        CodePackages = new List<ContainerCodePackageProperties>(new[]
                        {
                            new ContainerCodePackageProperties
                            {
                                Name = ServiceContainerName,
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

            return createMeshResponse.Body;
        }

        private IObservable<Unit> Stop()
        {
            return Observable.DeferAsync(async token =>
            {
                _logger.Information("Stop");

                await DeleteMesh(_meshName, token);

                _started = false;

                _pollingDisposable?.Dispose();
                _pollingDisposable = null;

                _logger.Information("Stopped");

                return Observable.Return(Unit.Default);
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }

        private async Task DeleteMesh(string meshName, CancellationToken token)
        {
            _logger.Information("Delete Mesh {Mesh}", meshName);
            await _serviceFabricMeshManagementClient.Application.DeleteWithHttpMessagesAsync(_arguments.ResourceGroup,
                meshName, cancellationToken: token);
        }

        public IObservable<Unit> Quit()
        {
            return Observable.DeferAsync(async token =>
            {
                _logger.Information("Quitting");

                if (_started) await Stop();

                _logger.Debug("Quit");

                return Observable.Return(Unit.Default);
            }).SubscribeOn(_schedulerProvider.TaskPool);
        }
    }
}