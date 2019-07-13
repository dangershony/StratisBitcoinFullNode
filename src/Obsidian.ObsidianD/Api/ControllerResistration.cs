using Microsoft.Extensions.DependencyInjection;
using Obsidian.Features.X1Wallet.SecureApi;

namespace Obsidian.ObsidianD.Api
{
    public static class ControllerResistration
    {
        public static IMvcBuilder AddSecureApi(this IMvcBuilder builder, IServiceCollection services)
        {
            builder.AddApplicationPart(typeof(SecureApiControllerBase).Assembly);
            builder.AddControllersAsServices();
            return builder;
        }
    }
}
