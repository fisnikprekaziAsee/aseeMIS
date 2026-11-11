using dsAppMng.Core.Repositories.Interfaces;
using dsAspNet.Core.Repositories.Interfaces;
using dsMIS.Core.DTOs;
using dsMIS.Core.Services;
using dsMIS.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using dsMIS.Core.Extensions;

namespace aseeMIS.Extensions
{
    public static class ServiceCollectionExtensions
    {
        // 1) Register cache services ONLY (no DB calls here)
        public static IServiceCollection AddAppLocalization(this IServiceCollection services, IConfiguration config)
        {
            services.AddDsCoreCaches();
            services.TryAddSingleton<IAppCacheReloader, AppCacheReloader>();
            return services;
        }

        public static IServiceCollection AddAppMenu(this IServiceCollection services)
        {
            services.AddDsCoreCaches();
            services.TryAddSingleton<IAppCacheReloader, AppCacheReloader>();
            return services;
        }

        public static IServiceCollection AddFormAccess(this IServiceCollection services)
        {
            services.AddDsCoreCaches();
            services.TryAddSingleton<IAppCacheReloader, AppCacheReloader>();
            return services;
        }

        public static IServiceCollection AddUserContextCache(this IServiceCollection services)
        {
            services.AddDsCoreCaches();
            services.TryAddSingleton<IAppCacheReloader, AppCacheReloader>();
            return services;
        }
    }

    // 2) One reloader that hits DB using scoped repos and reloads the singletons
    public interface IAppCacheReloader
    {
        Task ReloadAllAsync();
        Task ReloadLocalizationAsync();
        Task ReloadMenuAsync();
        Task ReloadPermissionsAsync();
        Task ReloadUsersAsync();
    }

    public sealed class AppCacheReloader : IAppCacheReloader
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILocalizationService _localization;
        private readonly IAppFormMenuService _menu;
        private readonly IAppFormPermissionService _perm;
        private readonly IUserContextService _users;

        public AppCacheReloader(
            IServiceScopeFactory scopeFactory,
            ILocalizationService localization,
            IAppFormMenuService menu,
            IAppFormPermissionService perm,
            IUserContextService users)
        {
            _scopeFactory = scopeFactory;
            _localization = localization;
            _menu = menu;
            _perm = perm;
            _users = users;
        }

        public async Task ReloadAllAsync()
        {
            await ReloadLocalizationAsync();
            await ReloadMenuAsync();
            await ReloadPermissionsAsync();
            await ReloadUsersAsync();
        }

        public async Task ReloadLocalizationAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAppLocalizationStringRepository>();

            var localizationData = await repo.SelectAllAsync();

            var data = localizationData.Select(x => new AppLocalizationStringDto
            {
                StringType = x.StringType,
                LanguageCode = x.LanguageCode,
                StringKey = x.StringKey,
                StringValue = x.StringValue
            }).ToList();

            // Your service supports Reload/UpdateLocalizationStrings
            if (_localization is LocalizationService svc)
                svc.Reload(data);
            else
                _localization.UpdateLocalizationStrings(data);
        }

        public async Task ReloadMenuAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAppFormRepository>();

            var data = (await repo.SelectAllMenuAsync()).ToList();

            if (_menu is AppFormMenuService svc)
                svc.Reload(data);
            else
                _menu.UpdateCache(data);
        }

        public async Task ReloadPermissionsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repoRole = scope.ServiceProvider.GetRequiredService<IAspNetRoleFormRepository>();
            var repoUser = scope.ServiceProvider.GetRequiredService<IAspNetUserFormRepository>();

            var list = new List<AppFormPermissionDto>();
            list.AddRange(await repoRole.SelectFormAccessAsync());
            list.AddRange(await repoUser.SelectFormAccessAsync());

            if (_perm is AppFormPermissionService svc)
                svc.Reload(list);
            else
                _perm.UpdateCache(list);
        }

        public async Task ReloadUsersAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAspNetUserRepository>();

            var initial = await repo.SelectAllUsersStampAsync();

            var users = initial.Select(x => new ContextUserDto
            {
                UserId = x.UserId,
                UserSecurityStamp = x.UserSecurityStamp,
                LanguageCode = x.LanguageCode,
                FormContextParent = null
            }).ToList();

            // Your service supports Reload/RefreshCache
            if (_users is UserContextService svc)
                svc.Reload(users);
            else
                _users.RefreshCache(users);
        }
    }
}

