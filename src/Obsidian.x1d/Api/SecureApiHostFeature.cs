using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Obsidian.Features.X1Wallet.SecureApi;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.x1d.Api
{
    /// <summary>
    /// Provides an encrypted password-protected endpoint to the node.
    /// </summary>
    public sealed class SecureApiHostFeature : Stratis.Bitcoin.Builder.Feature.FullNodeFeature
    {
        readonly IFullNodeBuilder fullNodeBuilder;
        readonly FullNode fullNode;
        readonly SecureApiSettings secureApiSettings;
        readonly ILogger logger;


        public SecureApiHostFeature(
            IFullNodeBuilder fullNodeBuilder,
            FullNode fullNode,
            SecureApiSettings secureApiSettings,
            ILoggerFactory loggerFactory)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.secureApiSettings = secureApiSettings;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);

            this.InitializeBeforeBase = true;
        }

        public override Task InitializeAsync()
        {
            if (this.secureApiSettings.IsSecureApiEnabled)
            {
                string path = $"{nameof(SecureApiController)}/{nameof(SecureApiController.ExecuteAsync)}".Replace("Controller", string.Empty);
                string message =
                    $"{nameof(SecureApiHostFeature)} listening at {this.secureApiSettings.SecureApiHostBinding}:{this.secureApiSettings.SecureApiPort}/{path}.";
                if (!string.IsNullOrWhiteSpace(this.secureApiSettings.SecureApiUser) &&
                    !string.IsNullOrWhiteSpace(this.secureApiSettings.SecureApiPassword))
                    message += " Credentials are required.";
                else message += " No credentials are required.";
                this.logger.LogInformation(message);
            }
            else
            {
                this.logger.LogInformation($"{nameof(SecureApiHostFeature)} is initialized but requests are disabled by configuration/arguments.");
            }

            Initialize(this.fullNodeBuilder.Services, this.fullNode, this.secureApiSettings, new WebHostBuilder());

            return Task.CompletedTask;
        }

        static void Initialize(IEnumerable<ServiceDescriptor> services, FullNode fullNode, SecureApiSettings secureApiSettings, IWebHostBuilder webHostBuilder)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(webHostBuilder, nameof(webHostBuilder));

            string secureApiListening = $"{secureApiSettings.SecureApiHostBinding}:{secureApiSettings.SecureApiPort}";

            webHostBuilder
                .UseKestrel(options =>
                {


                })
                .UseUrls(secureApiListening)
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
    }
}
