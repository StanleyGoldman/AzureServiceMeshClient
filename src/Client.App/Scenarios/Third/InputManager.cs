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

            try
            {
                do
                {
                    var read = Console.ReadKey(true);
                    if (read.Key == ConsoleKey.Enter)
                    {
                        if (!started)
                        {
                            _logger.Information("Started");
                        }
                        else
                        {
                            _logger.Information("Stopped");
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