using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Obsidian.Features.X1Wallet.SecureApi;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Obsidian.x1d.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // TODO: Check if this is required when the electron wallet is built for production, because this CORS policy allows requests from two more ports in addition to the api port (port 80 and 4200).

            services.AddCors
            (
                options =>
                {
                    options.AddPolicy
                    (
                        "CorsPolicy",

                        builder =>
                        {
                            var allowedDomains = new[] { "http://localhost", "http://localhost:4200", "http://localhost:3000", "http://localhost:1212" };

                            builder.WithOrigins(allowedDomains)
                                .AllowAnyHeader();
                        }
                    );
                });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddJsonOptions(options => Serializer.RegisterFrontConverters(options.SerializerSettings))
                .AddSecureApi(services);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(this.Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseCors("CorsPolicy");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == $"/{nameof(SecureApiController)}/{nameof(SecureApiController.ExecuteAsync)}".Replace("Controller", string.Empty))
                {
                    await next.Invoke();
                }
            });

            app.UseMvc(routes =>{
                routes.MapRoute(name: "default", template: "{controller}/{action}/{id?}");
            });
        }
    }
}