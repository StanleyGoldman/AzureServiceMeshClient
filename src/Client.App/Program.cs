using System;
using System.Linq;
using System.Net.Http;
using Autofac;
using Autofac.Core;
using AutofacSerilogIntegration;
using Client.App.Extensions;
using Client.App.FirstScenario;
using Client.App.Serilog;
using Client.App.Services;
using FluentColorConsole;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ServiceFabricMesh;
using Microsoft.Rest;
using Serilog;

namespace Client.App
{
    internal class Program
    {
        static Program()
        {
            var loggerConfiguration = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .Enrich.With<CustomEnrichers>();

            if (IsTracingEnabled)
                loggerConfiguration = loggerConfiguration.MinimumLevel.Verbose();
            else if (IsDebug) loggerConfiguration = loggerConfiguration.MinimumLevel.Debug();

            loggerConfiguration
                .WriteTo.Console(
                    outputTemplate:
                    "{Timestamp:HH:mm:ss} [{Level:u4}] ({PaddedThreadId}) {ShortSourceContext} {Message}{NewLineIfException}{Exception}{NewLine}");

            Log.Logger = loggerConfiguration.CreateLogger();
        }

        public static bool IsDebug
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsTracingEnabled =>
            !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("DDOAAMC_EnableTracing"));

        public static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "Docker DevOps Agent Azure Mesh Client",
                Description = "An experiment to deploy an DevOps build agent to Azure Service Fabric Mesh"
            };

            app.HelpOption(true);

            const string subscriptionIdEnvVar = "DDOAAMC_SubscriptionId";
            const string tenantIdEnvVar = "DDOAAMC_TenantId";
            const string clientSecretEnvVar = "DDOAAMC_ClientSecret";
            const string imageRegistryPasswordEnvVar = "DDOAAMC_ImageRegistryPassword";
            const string azurePipelinesTokenEnvVar = "DDOAAMC_AzurePipelinesToken";

            var name = app
                .Option("-n|--name", "Mesh Name",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var imageName = app
                .Option("-i|--imageName", "Image Name",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var resourceGroup = app
                .Option("-rg|--resourceGroup", "Resource Group",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var clientId = app
                .Option("-ci|--clientId", "Client ID", 
                    CommandOptionType.SingleValue)
                .IsRequired();

            var clientSecret = app
                .Option("-cs|--clientSecret", $"Client Secret (or env:'{clientSecretEnvVar}')",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var tenantId = app
                .Option("-t|--tenantId", $"Tenant ID (or env:'{tenantIdEnvVar}')",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var subscriptionId = app
                .Option("-s|--subscriptionId", $"Subscription ID (or env:'{subscriptionIdEnvVar}')",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var imageRegistryServer = app
                .Option("-irs|--imageRegistryServer", "Docker Image Registry Server",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var imageRegistryUsername = app
                .Option("-iru|--imageRegistryUsername", "Docker Image Registry Username",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var imageRegistryPassword = app
                .Option("-irp|--imageRegistryPassword",
                    "Docker Image Registry Password (or env:'" + imageRegistryPasswordEnvVar + "')",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var azpUrl = app
                .Option("-azpu|--azurePipelinesUrl", "Azure Pipelines Url",
                    CommandOptionType.SingleValue)
                .IsRequired();

            var azpToken = app
                .Option("-azpt|--azurePipelinesToken",
                    "Azure Pipelines Token (or env:'" + imageRegistryPasswordEnvVar + "')",
                    CommandOptionType.SingleValue)
                .IsRequired();

            app.OnParsingComplete(context =>
            {
                if (!clientSecret.Values.Any())
                {
                    var value = Environment.GetEnvironmentVariable(clientSecretEnvVar);
                    if (!string.IsNullOrWhiteSpace(value)) clientSecret.Values.Add(value);
                }

                if (!imageRegistryPassword.Values.Any())
                {
                    var value = Environment.GetEnvironmentVariable(imageRegistryPasswordEnvVar);
                    if (!string.IsNullOrWhiteSpace(value)) imageRegistryPassword.Values.Add(value);
                }

                if (!azpToken.Values.Any())
                {
                    var value = Environment.GetEnvironmentVariable(azurePipelinesTokenEnvVar);
                    if (!string.IsNullOrWhiteSpace(value)) azpToken.Values.Add(value);
                }

                if (!subscriptionId.Values.Any())
                {
                    var value = Environment.GetEnvironmentVariable(subscriptionIdEnvVar);
                    if (!string.IsNullOrWhiteSpace(value)) subscriptionId.Values.Add(value);
                }

                if (!tenantId.Values.Any())
                {
                    var value = Environment.GetEnvironmentVariable(tenantIdEnvVar);
                    if (!string.IsNullOrWhiteSpace(value)) tenantId.Values.Add(value);
                }
            });

            app.OnValidationError(result =>
            {
                ColorConsole.WithRedText.WriteLine(result.ToString());
                Console.WriteLine();
                app.ShowHelp();
            });

            app.OnExecute(() =>
            {
                var arguments = new Arguments
                {
                    Name = name.Value(),
                    ImageName = imageName.Value(),
                    ClientId = clientId.Value(),
                    ClientSecret = clientSecret.Value(),
                    TenantId = tenantId.Value(),
                    SubscriptionId = subscriptionId.Value(),
                    ImageRegistryServer = imageRegistryServer.Value(),
                    ImageRegistryUsername = imageRegistryUsername.Value(),
                    ImageRegistryPassword = imageRegistryPassword.Value(),
                    ResourceGroup = resourceGroup.Value(),
                    AzurePipelinesUrl = azpUrl.Value(),
                    AzurePipelinesToken = azpToken.Value()
                };

                var credentials = SdkContext.AzureCredentialsFactory
                    .FromServicePrincipal(arguments.ClientId,
                        arguments.ClientSecret,
                        arguments.TenantId,
                        AzureEnvironment.AzureGlobalCloud);

                var containerBuilder = GetContainerBuilder();

                containerBuilder.RegisterType<InputManager>()
                    .AsSelf();

                containerBuilder.RegisterType<MeshController>()
                    .AsSelf();

                containerBuilder.RegisterType<ObservableMeshClient>()
                    .AsSelf();

                containerBuilder.RegisterInstance(arguments)
                    .AsSelf();

                containerBuilder.RegisterInstance(credentials)
                    .As<ServiceClientCredentials>();

                containerBuilder.RegisterType<ServiceFabricMeshManagementClient>()
                    .UsingConstructor(typeof(ServiceClientCredentials), typeof(DelegatingHandler[]))
                    .WithParameter("handlers", new DelegatingHandler[0])
                    .OnActivated(eventArgs =>
                    {
                        eventArgs.Instance.SubscriptionId = arguments.SubscriptionId;
                    })
                    .AsSelf();

                var container = containerBuilder.Build();

                var inputManager = container.Resolve<InputManager>();

                return inputManager.Start();
            });

            return app.Execute(args);
        }

        private static ContainerBuilder GetContainerBuilder()
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterLogger();

            containerBuilder.RegisterType<SchedulerProvider>()
                .AsSelf();
            return containerBuilder;
        }
    }
}