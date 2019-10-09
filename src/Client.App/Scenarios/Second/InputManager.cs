using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Client.App.Services;
using FluentColorConsole;
using Microsoft.Azure.Management.ServiceFabricMesh.Models;
using Serilog;

namespace Client.App.Scenarios.Second
{
    public class InputManager
    {
        private readonly ILogger _logger;
        private readonly Func<MeshController> _meshControllerFactory;
        private readonly Arguments _arguments;

        public InputManager(ILogger logger, Func<MeshController> meshControllerFactory, Arguments arguments)
        {
            _logger = logger;
            _meshControllerFactory = meshControllerFactory;
            _arguments = arguments;
        }

        public int Start()
        {
            ColorConsole.WithBlueText.WriteLine("Press [ENTER] to CREATE/DELETE the build agent mesh");
            ColorConsole.WithBlueText.WriteLine("Press [Q] to quit the simulation");

            var started = false;
            Func<(IObservable<Unit> request, IObservable<Unit> complete)>[] list = null;

            try
            {
                do
                {
                    var read = Console.ReadKey(true);
                    if (read.Key == ConsoleKey.Enter)
                    {
                        var semaphoreSlim = new SemaphoreSlim(_arguments.MeshOperationalParallelism);

                        if (!started)
                        {
                            list = Observable.Defer(() =>
                                {
                                    var s = _arguments.Name + Common.CleanGuid();

                                    var meshController = _meshControllerFactory();
                                    var (request, ready, stop) = meshController.Start(s,
                                        _arguments.ImageRegistryServer, _arguments.ImageRegistryUsername,
                                        _arguments.ImageRegistryPassword, _arguments.ImageName, _arguments.AzurePipelinesUrl,
                                        _arguments.AzurePipelinesToken, _arguments.ResourceGroup, false, true);

                                    var valueTuple = (request, ready, stop);
                                    return Observable.Return(valueTuple);
                                })
                                .Repeat(_arguments.MeshCount)
                                .Select((tuple, i) => Observable.DeferAsync(async token =>
                                {
                                    semaphoreSlim.Wait(token);

                                    _logger.Debug("Creating Item {Index}", i + 1);

                                    await tuple.request;

                                    var ready = await tuple.ready;

                                    _logger.Debug("Item {Index} Started {Ready}", i + 1, ready);

                                    semaphoreSlim.Release();

                                    return Observable.Return(tuple.stop);
                                }))
                                .SelectMany(observable => observable)
                                .ToArray()
                                .Wait();

                            _logger.Information("Readied {Count}", _arguments.MeshCount);
                        }
                        else
                        {
                            list.ToObservable()
                                .Select(stop => stop())
                                .Select((tuple, i) => Observable.DeferAsync(async token =>
                                {
                                    semaphoreSlim.Wait(token);

                                    _logger.Debug("Deleting Item {Index}", i + 1);

                                    await tuple.request;

                                    await tuple.complete;

                                    _logger.Debug("Item {Index} Deleted", i + 1);

                                    semaphoreSlim.Release();

                                    return Observable.Return(Unit.Default);
                                }))
                                .SelectMany(observable => observable)
                                .Wait();

                            _logger.Information("Deleted {Count}", _arguments.MeshCount);
                        }

                        started = !started;
                        continue;
                    }

                    if (read.Key == ConsoleKey.Q)
                    {
                        break;
                    }

                } while (true);
            }
            catch (ErrorModelException e)
            {
                _logger.Error(e, "ErrorModelException: {Body}", e.Body.Error.Message);
                return 1;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Exception");
                return 1;
            }
            finally
            {
                
            }

            return 0;
        }
    }
}