using System;
using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Client.App.Extensions;
using Microsoft.Azure.Management.ServiceFabricMesh;
using Microsoft.Azure.Management.ServiceFabricMesh.Models;
using Serilog;

namespace Client.App.FirstScenario
{
    public static class LogDataExtensions
    {
        public enum GetAgentStateEnum
        {
            NotReady,
            Starting,
            Running,
            Shutdown
        }

        public enum GetContainerLogsResponseEnum
        {
            NotFound,
            ResourceNotReady,
            Output
        }

        public static IObservable<GetAgentStateEnum> GetAgentStateObservable(
            this ServiceFabricMeshManagementClient serviceFabricMeshManagementClient,
            ILogger logger, IObservable<Unit> startPollingObservable,
            string argumentsResourceGroup, string applicationResourceName, string serviceResourceName,
            string replicaName, string serviceContainerName, SchedulerProvider schedulerProvider)
        {
            return Observable.Return(GetAgentStateEnum.NotReady)
                .Concat(Observable.Create<GetAgentStateEnum>(observer =>
                {
                    var disposables = new CompositeDisposable();

                    var pollingSubject = new Subject<IObservable<(GetContainerLogsResponseEnum, string)>>();
                    disposables.Add(pollingSubject);

                    var pollingObservable = pollingSubject
                        .Do(observable =>
                        {
                            ;
                        })
                        .Switch()
                        .Do(tuple =>
                        {
                            ;
                        });

                    var startPollingSubscription = startPollingObservable
                        .Subscribe(unit =>
                        {
                            logger.Debug("Start Polling internal");

                            var observable = serviceFabricMeshManagementClient.GetContainerLogs(logger,
                                    argumentsResourceGroup, applicationResourceName, schedulerProvider,
                                    serviceResourceName, replicaName, serviceContainerName)
                                .Concat(Observable
                                    .Empty<(GetContainerLogsResponseEnum, string)>()
                                    .Delay(TimeSpan.FromSeconds(1)))
                                .Repeat();

                            pollingSubject.OnNext(observable);
                        });
                    disposables.Add(startPollingSubscription);

                    var (stateOutput, containerOutput) = pollingObservable
                        .Do(tuple =>
                        {
                            ;
                        }).SplitTuple(schedulerProvider.TaskPool);

                    var lastAgentState = GetAgentStateEnum.NotReady;

                    var stateOutputSubscription = stateOutput.WhenChanges()
                        .CombineLatest(containerOutput
                                .WhenChanges()
                                .SplitRepeatedPrefixByNewline()
                                .SelectMany(strings => strings)
                                .Do(s => logger.Information("Container: {@Output}", s)),
                            (state, s) => (state, s))
                        .Subscribe(tuple =>
                        {
                            var (state, s) = tuple;

                            if (lastAgentState == GetAgentStateEnum.NotReady
                                && state == GetContainerLogsResponseEnum.Output)
                            {
                                lastAgentState = GetAgentStateEnum.Starting;
                                observer.OnNext(lastAgentState);
                                return;
                            }

                            if (lastAgentState == GetAgentStateEnum.Starting
                                && state == GetContainerLogsResponseEnum.Output
                                && s != null
                                && s.Contains("Listening for Jobs"))
                            {
                                lastAgentState = GetAgentStateEnum.Running;
                                observer.OnNext(lastAgentState);
                                return;
                            }

                            if (lastAgentState != GetAgentStateEnum.NotReady
                                && state != GetContainerLogsResponseEnum.Output)
                            {
                                observer.OnNext(GetAgentStateEnum.Shutdown);
                                observer.OnCompleted();
                            }
                        }, observer.OnError, observer.OnCompleted);
                    disposables.Add(stateOutputSubscription);

                    return disposables;
                }));
        }

        public static IObservable<(GetContainerLogsResponseEnum, string)> GetContainerLogs(
            this ServiceFabricMeshManagementClient serviceFabricMeshManagementClient, ILogger logger,
            string argumentsResourceGroup, string applicationResourceName, SchedulerProvider schedulerProvider,
            string serviceResourceName, string replicaName, string serviceContainerName)
        {
            return Observable.DeferAsync(async cancellationToken =>
            {
                logger.Verbose("Querying Logs");

                try
                {
                    var response = await serviceFabricMeshManagementClient
                        .CodePackage
                        .GetContainerLogsWithHttpMessagesAsync(argumentsResourceGroup, applicationResourceName,
                            serviceResourceName,
                            replicaName, serviceContainerName, cancellationToken: cancellationToken);

                    var data = response.Body.Content;

                    return Observable.Return((GetContainerLogsResponseEnum.Output, data));
                }
                catch (ErrorModelException e)
                {
                    if (e.Response.StatusCode == HttpStatusCode.NotFound)
                        return Observable.Return((GetContainerLogsResponseEnum.NotFound, (string) null));

                    if (e.Body.Error.Code == "ResourceNotReady")
                        return Observable.Return((GetContainerLogsResponseEnum.ResourceNotReady, (string) null));

                    throw;
                }
                finally
                {
                    logger.Verbose("Queried Logs");
                }
            }).SubscribeOn(schedulerProvider.TaskPool);
        }
    }
}