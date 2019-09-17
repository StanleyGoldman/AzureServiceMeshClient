using System;
using FluentColorConsole;
using McMaster.Extensions.CommandLineUtils;

namespace Client.App.Extensions
{
    public static class CommandLineApplicationExtensions
    {
        public static void HandleValidationError(this CommandLineApplication commandLineApplication)
        {
            commandLineApplication.OnValidationError(result =>
            {
                ColorConsole.WithRedText.WriteLine(result.ToString());
                Console.WriteLine();
                commandLineApplication.ShowHelp();
            });
        }
    }
}