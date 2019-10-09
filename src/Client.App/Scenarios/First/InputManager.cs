using System;
using System.Reactive;
using System.Reactive.Linq;
using Client.App.Services;
using FluentColorConsole;
using Microsoft.Azure.Management.ServiceFabricMesh.Models;
using Serilog;

namespace Client.App.Scenarios.First
{
    public class InputManager
    {
        private readonly Arguments _arguments;
        private readonly ILogger _logger;
        private readonly MeshController _meshController;

        public InputManager(ILogger logger, MeshController meshController, Arguments arguments)
        {
            _logger = logger;
            _meshController = meshController;
            _arguments = arguments;
        }

        public int Start()
        {
            ColorConsole.WithBlueText.WriteLine("Press [ENTER] to CREATE/DELETE the build agent mesh");
            ColorConsole.WithBlueText.WriteLine("Press [Q] to quit the simulation");

            var started = false;
            Func<(IObservable<Unit> request, IObservable<Unit> complete)> stopFunction = null;

            try
            {
                do
                {
                    var read = Console.ReadKey(true);
                    if (read.Key == ConsoleKey.Enter)
                    {
                        if (!started)
                        {
                            var applicationResourceName = _arguments.Name + Common.CleanGuid();
                            var (request, complete, stop) = _meshController.Start(applicationResourceName,
                                                            _arguments.ImageRegistryServer, _arguments.ImageRegistryUsername,
                                                            _arguments.ImageRegistryPassword, _arguments.ImageName, _arguments.AzurePipelinesUrl,
                                                            _arguments.AzurePipelinesToken, _arguments.ResourceGroup);

                            stopFunction = stop;

                            request.Wait();
                            complete.Wait();
                        }
                        else
                        {
                            var (request, complete) = stopFunction();

                            request.Wait();
                            complete.Wait();
                        }

                        started = !started;
                        continue;
                    }

                    if (read.Key == ConsoleKey.Q) break;
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
                if (started)
                {
                    var (request, _) = stopFunction();
                    request.Wait();
                }
            }

            return 0;
        }
    }
}