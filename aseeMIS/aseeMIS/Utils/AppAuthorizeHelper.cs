using dsMIS.Application.Services;
using dsMIS.Application.DTOs;
using dsMIS.Core.DTOs;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using System.Security.Claims;

namespace aseeMIS.Utils
{
    public static class AppAuthorizeHelper
    {
        // ---------------- PUBLIC API ----------------

        public static async Task<(bool IsValid, AppFormMenuDto? Form, ContextSessionDto? Context, string ProjectNamespace, string ServiceInterface, string ViewModel)>
            ValidateReadPermissionAsync(HttpContext httpContext,
                                        IAppFormPermissionService formAccessService,
                                        IAppFormMenuService menuService,
                                        IUserContextService userContextService,
                                        int formId,
                                        EnumContextFormType contextFormType)
        {
            var (ok, appForm, ctx) = (contextFormType == EnumContextFormType.CHILD)
                ? await CreateChildSessionAsync(httpContext, menuService, userContextService, formId)
                : await CreateParentSessionAsync(httpContext, formAccessService, menuService, userContextService, formId);

            if (!ok || appForm is null || ctx is null)
                return (false, null, null, string.Empty, string.Empty, string.Empty);

            var (proj, svc, vm) = BuildMetadataStrings(appForm);
            return (true, appForm, ctx, proj, svc, vm);
        }

        public static async Task<(bool IsValid, AppFormMenuDto? Form, ContextSessionDto? Context, string ProjectNamespace, string ServiceInterface, string ViewModel)>
            ValidateChildReadPermissionAsync(HttpContext httpContext,
                                             IAppFormMenuService menuService,
                                             IUserContextService userContextService,
                                             int childFormId,
                                             string? parentFormSessionId,
                                             string? childFormSessionId = null)
        {
            if (!string.IsNullOrWhiteSpace(childFormSessionId))
            {
                var (sessionOk, sessionForm, sessionCtx) = await ValidateExistingChildSessionReadAsync(
                    httpContext,
                    menuService,
                    userContextService,
                    childFormId,
                    childFormSessionId);

                if (sessionOk && sessionForm != null && sessionCtx != null)
                {
                    var (sessionProj, sessionSvc, sessionVm) = BuildMetadataStrings(sessionForm);
                    return (true, sessionForm, sessionCtx, sessionProj, sessionSvc, sessionVm);
                }
            }

            var (ok, appForm, ctx) = await CreateChildSessionFromParentAsync(
                httpContext,
                menuService,
                userContextService,
                childFormId,
                parentFormSessionId);

            if (!ok || appForm is null || ctx is null)
                return (false, null, null, string.Empty, string.Empty, string.Empty);

            var (proj, svc, vm) = BuildMetadataStrings(appForm);
            return (true, appForm, ctx, proj, svc, vm);
        }

        public static async Task<IReadOnlyDictionary<int, string>> CreateChildSessionMapFromParentAsync(HttpContext httpContext,
                                                                                                        IAppFormMenuService menuService,
                                                                                                        IUserContextService userContextService,
                                                                                                        string? parentFormSessionId,
                                                                                                        IEnumerable<int> childFormIds)
        {
            var result = new Dictionary<int, string>();

            foreach (var childFormId in childFormIds.Distinct())
            {
                var (ok, _, ctx) = await CreateChildSessionFromParentAsync(
                    httpContext,
                    menuService,
                    userContextService,
                    childFormId,
                    parentFormSessionId);

                if (ok && ctx != null && !string.IsNullOrWhiteSpace(ctx.FormSessionId))
                    result[childFormId] = ctx.FormSessionId;
            }

            return result;
        }

        public static async Task<(bool IsValid, AppFormMenuDto? Form, ContextSessionDto? Context, string ProjectNamespace, string ServiceInterface, string ViewModel)>
            ValidateActionAsync(HttpContext httpContext,
                                IAppFormPermissionService formAccessService,
                                IAppFormMenuService menuService,
                                IUserContextService userContextService,
                                string formSessionId,
                                EumFormAccessAction requiredPermission)
        {
            if (string.IsNullOrWhiteSpace(formSessionId))
                return (false, null, null, string.Empty, string.Empty, string.Empty);

            var (ok, form, ctx) = await new CrudContextResolver(formAccessService, menuService, userContextService).ResolveAsync(
                CrudActorFactory.FromPrincipal(httpContext.User), formSessionId, requiredPermission);

            if (!ok || form is null || ctx is null)
                return (false, null, null, string.Empty, string.Empty, string.Empty);

            var (proj, svc, vm) = BuildMetadataStrings(form);
            return (true, form, ctx, proj, svc, vm);
        }

        // ---------------- SESSION CREATION ----------------

        private static async Task<(bool IsValid, AppFormMenuDto? Form, ContextSessionDto? Context)> CreateParentSessionAsync(HttpContext httpContext,
                                                                                                                             IAppFormPermissionService formAccessService,
                                                                                                                             IAppFormMenuService menuService,
                                                                                                                             IUserContextService userContextService,
                                                                                                                             int formId)
        {
            var (authOk, userId, roleNames, userCtx) = ValidateAuthentication(httpContext, userContextService);
            if (!authOk || userCtx is null)
                return (false, null, null);

            var form = await menuService.FindByFormIdAsync(formId);
            if (form is null || form.IsActive != true)
                return (false, null, null);

            var grant = await formAccessService.FindByFormAndUserAsync(formId, userId, roleNames);
            if (grant is null)
                return (false, null, null);

            // Parent = AND
            var effective = form.Permissions & grant.Permissions;

            if (!effective.HasFlag(FormPermissions.Read))
                return (false, null, null);

            // keep user-level behavior flag synced from parent form (optional but good)
            userCtx.IsAutoApprove = form.IsAutoApprove;

            var ctx = BuildContextSession(userId: userId,
                                          roleNames: roleNames,
                                          languageCode: userCtx.LanguageCode,
                                          formId: form.FormId,
                                          isAutoApprove: userCtx.IsAutoApprove,
                                          effectivePermissions: effective);

            // Store parent context
            userCtx.FormContextParent = new AppFormPermissionDto
            {
                UserId = userId,
                RoleName = null,
                FormId = formId,
                Permissions = effective,
                FormSessionId = ctx.FormSessionId
            };

            userContextService.UpdateUserToCache(userCtx);
            return (true, form, ctx);
        }

        private static async Task<(bool IsValid, AppFormMenuDto? Form, ContextSessionDto? Context)> CreateChildSessionAsync(HttpContext httpContext,
                                                                                                                            IAppFormMenuService menuService,
                                                                                                                            IUserContextService userContextService,
                                                                                                                            int childFormId)
        {
            var (authOk, userId, roleNames, userCtx) = ValidateAuthentication(httpContext, userContextService);
            if (!authOk || userCtx is null)
                return (false, null, null);

            var form = await menuService.FindByFormIdAsync(childFormId);
            if (form is null || form.IsActive != true)
                return (false, null, null);

            // Child = AppForm capabilities ONLY
            var effective = form.Permissions;

            if (!effective.HasFlag(FormPermissions.Read))
                return (false, null, null);

            var ctx = BuildContextSession(
                userId: userId,
                roleNames: roleNames,
                languageCode: userCtx.LanguageCode,
                formId: form.FormId,
                isAutoApprove: userCtx.IsAutoApprove,   // ✅ inherit from user context
                effectivePermissions: effective);

            // Store child context by FormSessionId
            userCtx.SetChildContext(new AppFormPermissionDto
            {
                UserId = userId,
                RoleName = null,
                FormId = childFormId,
                Permissions = effective,
                FormSessionId = ctx.FormSessionId
            });

            userContextService.UpdateUserToCache(userCtx);
            return (true, form, ctx);
        }

        private static async Task<(bool IsValid, AppFormMenuDto? Form, ContextSessionDto? Context)> CreateChildSessionFromParentAsync(HttpContext httpContext,
                                                                                                                                      IAppFormMenuService menuService,
                                                                                                                                      IUserContextService userContextService,
                                                                                                                                      int childFormId,
                                                                                                                                      string? parentFormSessionId)
        {
            var (authOk, userId, roleNames, userCtx) = ValidateAuthentication(httpContext, userContextService);
            if (!authOk || userCtx is null)
                return (false, null, null);

            if (string.IsNullOrWhiteSpace(parentFormSessionId))
                return await CreateChildSessionAsync(httpContext, menuService, userContextService, childFormId);

            var parent = userCtx.TryGetAnyContext(parentFormSessionId);
            if (parent is null)
                parent = userCtx.FormContextParent;

            if (parent is null)
                return await CreateChildSessionAsync(httpContext, menuService, userContextService, childFormId);

            if (!parent.Permissions.HasFlag(FormPermissions.Read))
                return (false, null, null);

            var form = await menuService.FindByFormIdAsync(childFormId);
            if (form is null || form.IsActive != true)
                return (false, null, null);

            var effective = form.Permissions & parent.Permissions;
            if (!effective.HasFlag(FormPermissions.Read))
                return (false, null, null);

            var ctx = BuildContextSession(
                userId: userId,
                roleNames: roleNames,
                languageCode: userCtx.LanguageCode,
                formId: form.FormId,
                isAutoApprove: userCtx.IsAutoApprove,
                effectivePermissions: effective);

            userCtx.SetChildContext(new AppFormPermissionDto
            {
                UserId = userId,
                RoleName = null,
                FormId = childFormId,
                Permissions = effective,
                FormSessionId = ctx.FormSessionId
            });

            userContextService.UpdateUserToCache(userCtx);
            return (true, form, ctx);
        }

        private static async Task<(bool IsValid, AppFormMenuDto? Form, ContextSessionDto? Context)> ValidateExistingChildSessionReadAsync(HttpContext httpContext,
                                                                                                                                          IAppFormMenuService menuService,
                                                                                                                                          IUserContextService userContextService,
                                                                                                                                          int childFormId,
                                                                                                                                          string childFormSessionId)
        {
            var (authOk, userId, roleNames, userCtx) = ValidateAuthentication(httpContext, userContextService);
            if (!authOk || userCtx is null)
                return (false, null, null);

            var ctxPerm = userCtx.TryGetAnyContext(childFormSessionId);
            if (ctxPerm is null || ctxPerm.FormId != childFormId)
                return (false, null, null);

            if (!ctxPerm.Permissions.HasFlag(FormPermissions.Read))
                return (false, null, null);

            var form = await menuService.FindByFormIdAsync(childFormId);
            if (form is null || form.IsActive != true)
                return (false, null, null);

            var ctx = BuildContextSession(
                userId: userId,
                roleNames: roleNames,
                languageCode: userCtx.LanguageCode,
                formId: form.FormId,
                isAutoApprove: userCtx.IsAutoApprove,
                effectivePermissions: ctxPerm.Permissions);

            ctx.FormSessionId = childFormSessionId;
            return (true, form, ctx);
        }

        // ---------------- ACTION VALIDATION ----------------

        // ---------------- AUTH ----------------

        private static (bool IsValid, int UserId, List<string> RoleNames, ContextUserDto? UserCtx) ValidateAuthentication(HttpContext httpContext, IUserContextService userContextService)
            => CrudContextResolver.Authenticate(CrudActorFactory.FromPrincipal(httpContext.User), userContextService);

        // ---------------- CONTEXT BUILD ----------------

        private static ContextSessionDto BuildContextSession(int userId, List<string> roleNames, string? languageCode, int formId, bool isAutoApprove, FormPermissions effectivePermissions)
        {
            return new ContextSessionDto
            {
                UserId = userId,
                RoleNames = roleNames,
                LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "en-US" : languageCode,
                FormId = formId,
                FormSessionId = Guid.NewGuid().ToString(),
                IsAutoApprove = isAutoApprove,
                Permissions = effectivePermissions
            };
        }

        private static (string ProjectNamespace, string ServiceInterface, string ViewModel) BuildMetadataStrings(AppFormMenuDto appForm)
            => CrudContextResolver.BuildMetadataStrings(appForm);
    }
}
