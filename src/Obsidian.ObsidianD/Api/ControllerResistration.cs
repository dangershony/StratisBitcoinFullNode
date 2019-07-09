using Microsoft.Extensions.DependencyInjection;
using Obsidian.Features.SegWitWallet.Web;

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
