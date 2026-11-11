using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace aseeMIS.Services.Interfaces
{
    public interface IIdentityOptionsReloader
    {
        void Reload();
    }

    public sealed class IdentityOptionsReloader : IIdentityOptionsReloader
    {
        private readonly IOptionsMonitorCache<IdentityOptions> _cache;

        public IdentityOptionsReloader(IOptionsMonitorCache<IdentityOptions> cache)
        {
            _cache = cache;
        }

        public void Reload()
        {
            // Clear IdentityOptions cache so it recomputes (calls DbIdentityOptionsSetup again)
            _cache.TryRemove(Options.DefaultName);
        }
    }

}
