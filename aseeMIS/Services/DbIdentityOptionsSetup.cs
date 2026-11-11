using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using dsAppMng.Core.Services.Interfaces;
namespace aseeMIS.Services
{

    public sealed class DbIdentityOptionsSetup : IConfigureOptions<IdentityOptions>
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public DbIdentityOptionsSetup(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public void Configure(IdentityOptions options)
        {
            //// IMPORTANT:
            //// IConfigureOptions is singleton -> use a scope to read DB/services.
            //using var scope = _scopeFactory.CreateScope();

            //// Replace this with YOUR service that returns current settings (cached or DB)
            //var settings = await scope.ServiceProvider.GetRequiredService<IAppIdentitySettingService>()
            //                                   .GetByIdAsync(1); // sync or async->sync (recommended: cached)

            //options.Password.RequiredLength = settings.PasswordRequiredLength;
            //options.Password.RequireDigit = settings.PasswordRequireDigit;
            //options.Password.RequireLowercase = settings.PasswordRequireLowercase;
            //options.Password.RequireUppercase = settings.PasswordRequireUppercase;
            //options.Password.RequireNonAlphanumeric = settings.PasswordRequireNonAlphanumeric;
            //options.Password.RequiredUniqueChars = settings.PasswordRequiredUniqueChars;

            //options.User.RequireUniqueEmail = settings.RequireUniqueEmail;

            //options.SignIn.RequireConfirmedEmail = settings.RequireConfirmedEmail;

            //options.Lockout.AllowedForNewUsers = settings.LockoutAllowedForNewUsers;
            //options.Lockout.MaxFailedAccessAttempts = settings.LockoutMaxFailedAccessAttempts;
            //options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(settings.LockoutMinutes);

            //options.User.AllowedUserNameCharacters =
            //    settings.AllowedUserNameCharacters;
        }
    }

}
