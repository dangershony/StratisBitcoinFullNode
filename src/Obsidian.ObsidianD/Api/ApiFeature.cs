using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.ObsidianD.Api
{
    /// <summary>
    /// Provides an Api to the full node
    /// </summary>
    public sealed class ApiFeature : FullNodeFeature
    {
        readonly IFullNodeBuilder fullNodeBuilder;
        readonly FullNode fullNode;
        readonly ApiSettings apiSettings;
        readonly ILogger logger;


        public ApiFeature(
            IFullNodeBuilder fullNodeBuilder,
            FullNode fullNode,
            ApiSettings apiSettings,
            ILoggerFactory loggerFactory)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.apiSettings = apiSettings;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);

            this.InitializeBeforeBase = true;
        }

        public override Task InitializeAsync()
        {
            this.logger.LogInformation("API starting on URL '{0}'.", this.apiSettings.ApiUri);
            Initialize(this.fullNodeBuilder.Services, this.fullNode, this.apiSettings, new WebHostBuilder());

            return Task.CompletedTask;
        }

        static void Initialize(IEnumerable<ServiceDescriptor> services, FullNode fullNode, ApiSettings apiSettings,
            IWebHostBuilder webHostBuilder)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(webHostBuilder, nameof(webHostBuilder));

            Uri apiUri = apiSettings.ApiUri;

            webHostBuilder
                .UseKestrel(options =>
                {
                    if (apiSettings.UseHttps)
                        throw new NotSupportedException("Please use Stratis.Bitcoin.Features.Api if HTTPS is required.");

                })
                .UseUrls(apiUri.ToString())
                .UseStartup<Startup>()
                .ConfigureServices(collection =>
                {
                    if (services == null)
                    {
                        return;
                    }

                    // copies all the services defined for the full node to the Api.
                    // also copies over singleton instances already defined
                    foreach (ServiceDescriptor service in services)
                    {
                        // open types can't be singletons
                        if (service.ServiceType.IsGenericType || service.Lifetime == ServiceLifetime.Scoped)
                        {
                            collection.Add(service);
                            continue;
                        }

                        try
                        {
                            object obj = fullNode.Services.ServiceProvider.GetService(service.ServiceType);
                            if (obj != null && service.Lifetime == ServiceLifetime.Singleton && service.ImplementationInstance == null)
                            {
                                collection.AddSingleton(service.ServiceType, obj);
                            }
                            else
                            {
                                collection.Add(service);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }
                });

            IWebHost host = webHostBuilder.Build();

            host.Start();
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param Command="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            ApiSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param Command="builder">The string builder to add the settings to.</param>
        /// <param Command="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            ApiSettings.BuildDefaultConfigurationFile(builder, network);
        }

      
    }

    

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class ApiFeatureExtension
    {
        public static IFullNodeBuilder UseApiSlim(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<ApiFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton(fullNodeBuilder);
                        services.AddSingleton<ApiSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
