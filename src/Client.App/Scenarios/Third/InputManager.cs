using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Client.App.Services;
using FluentColorConsole;
using Microsoft.Azure.Management.ServiceFabricMesh.Models;
using Serilog;

namespace Client.App.Scenarios.Third
{
    public class InputManager
    {
        private enum Phase
        {
            Off,
            Started,
            Upscaled
        }
        private readonly ILogger _logger;
        private readonly MeshController _meshController;
        private readonly Arguments _arguments;

        public InputManager(ILogger logger, MeshController meshController, Arguments arguments)
        {
            _logger = logger;
            _meshController = meshController;
            _arguments = arguments;
        }

        public int Start()
        {
            ColorConsole.WithBlueText.WriteLine("Press [ENTER] to Start the build agent mesh");
            ColorConsole.WithBlueText.WriteLine("Press [Q] to quit the simulation");

            var phase = Phase.Off;
            Func<(IObservable<Unit> request, IObservable<Unit> complete)> meshStopFunc = null;
            var applicationResourceName = _arguments.Name + Common.CleanGuid();

            try
            {
                do
                {
                    var read = Console.ReadKey(true);
                    if (read.Key == ConsoleKey.Enter)
                    {
                        if (phase == Phase.Off)
                        {
                            var (request, complete, stop) = _meshController.Start(applicationResourceName,
                                _arguments.ImageRegistryServer, _arguments.ImageRegistryUsername,
                                _arguments.ImageRegistryPassword, _arguments.ImageName, _arguments.AzurePipelinesUrl,
                                _arguments.AzurePipelinesToken, _arguments.ResourceGroup);

                            meshStopFunc = stop;

                            request.Wait();
                            complete.Wait();

                            _logger.Information("Started");
                            phase = Phase.Started;
                        }
                        else if (phase == Phase.Started)
                        {
                            _logger.Information("Upscaled");
                            phase = Phase.Upscaled;
                        }
                        else
                        {
                            var (request, complete) = meshStopFunc();

                            request.Wait();
                            complete.Wait();

                            _logger.Information("Stopped");
                            phase = Phase.Off;
                        }

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