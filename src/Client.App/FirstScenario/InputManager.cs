using System;
using System.Reactive.Linq;
using FluentColorConsole;
using Microsoft.Azure.Management.ServiceFabricMesh.Models;
using Serilog;

namespace Client.App.FirstScenario
{
    public class InputManager
    {
        private readonly MeshController _meshController;
        private readonly ILogger _logger;

        public InputManager(ILogger logger, MeshController meshController)
        {
            _logger = logger;
            _meshController = meshController;
        }

        public int Start(Arguments arguments)
        {
            _logger.Debug("Starting");

            try
            {
                _meshController.Initialize(arguments);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error Initializing");
                return 1;
            }

            ColorConsole.WithBlueText.WriteLine("Press [ENTER] to CREATE/DISPOSE of a built agent");
            ColorConsole.WithBlueText.WriteLine("Press [Q] to quit the simulation");

            try
            {
                do
                {
                    var read = Console.ReadKey(true);
                    if (read.Key == ConsoleKey.Enter)
                    {
                        _meshController.Toggle().Wait();
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
                _meshController.Quit().Wait();
            }

            return 0;
        }
    }
}