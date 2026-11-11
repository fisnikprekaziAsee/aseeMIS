using dsDim.Core.Extensions;
using dsEtlEngine.Extensions;
using dsMIS.Application.Extensions;
using dsMIS.Application.Services;
using dsMIS.Application.Services.Interfaces;
using dsMIS.Core.Services;
using dsMIS.Core.Services.Interfaces;
using dsIdentity.Core.Data;
using dsIdentity.Core.Models;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Areas.Credit.Services;
using aseeMIS.Areas.Credit.Services.Interfaces;
using aseeMIS.Extensions;
using aseeMIS.Integration;
using aseeMIS.Services;
using aseeMIS.Services.Interfaces;
using dsPurchase.Core.Services;
using dsPurchase.Core.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Syncfusion.Licensing;

var builder = WebApplication.CreateBuilder(args);  

builder.WebHost.UseStaticWebAssets();

if (builder.Environment.IsProduction())
    builder.Configuration.AddUserSecrets<Program>(optional: true);

// Register through configuration. In production use the
// Syncfusion__LicenseKey environment variable.
var syncfusionLicenseKey = builder.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
}
else if (builder.Environment.IsProduction())
{
    throw new InvalidOperationException(
        "Syncfusion license key is missing. Configure Syncfusion__LicenseKey.");
}

builder.Services.AddDsApplicationModules(builder.Configuration);
builder.Services.AddDimCore(builder.Configuration);
builder.Services.AddEtlEngineCore(builder.Configuration);
builder.Services.AddGrpc();

// ✅ Integration caller validation
builder.Services.AddScoped<IIntegrationCallerValidator, IntegrationCallerValidator>();

// ✅ aseeMIS services
builder.Services.AddDsCrudApplication();
builder.Services.AddScoped<ToolbarItemsProvider, ToolbarItemsProvider>();
builder.Services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddHttpClient<IBillAiService, BillAiService>();
builder.Services.AddScoped<IEmailSenderService, EmailSenderService>();
builder.Services.AddScoped<ICreditCaseLoanAmortPlanService, CreditCaseLoanAmortPlanService>();
builder.Services.AddScoped<ICreditCaseFactoringInvoicePricingService, CreditCaseFactoringInvoicePricingService>();
builder.Services.AddScoped<ApiCamelCaseResultFilter>();
builder.Services.AddSingleton<ISecureFileStorage, SecureFileStorage>();

// ✅ Attribute Preset Service
builder.Services.AddSingleton<IAttributePresetService, AttributePresetService>();

// ✅ Session Services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAppSessionService, AppSessionService>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ✅ Authentication and Identity
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<DBContextIdentity>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// 👇 options setup + your “reload” mechanism (implementation must really reload)
builder.Services.AddSingleton<IConfigureOptions<IdentityOptions>, DbIdentityOptionsSetup>();
builder.Services.AddSingleton<IIdentityOptionsReloader, IdentityOptionsReloader>();

// ✅ Modular App Services (REQUIRED for IAppCacheReloader)
builder.Services.AddAppLocalization(builder.Configuration)
                .AddAppMenu()
                .AddFormAccess()
                .AddUserContextCache();
// ✅ MVC
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddGrpc().AddJsonTranscoding();

// 2. Shto integrimin e gRPC në Swagger
builder.Services.AddGrpcSwagger();

builder.Services.AddSwaggerGen(c =>
{
    // 1. Defino të dyja versionet
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "aseeMIS API v1", Version = "v1" });
    c.SwaggerDoc("v2", new OpenApiInfo { Title = "aseeMIS API v2", Version = "v2" });

    // 2. Defino skemat e sigurisë (Kjo vetëm i deklaron ato)
    var securitySchemes = new Dictionary<string, string>
    {
        { "ApiKey", "x-api-key" },
        { "RequestUser", "x-request-user" },
        { "CorrelationId", "x-correlation-id" },
        { "ChannelCode", "x-channel-code" }
    };

    foreach (var scheme in securitySchemes)
    {
        c.AddSecurityDefinition(scheme.Key, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = scheme.Value,
            Description = $"Shkruaj vlerën për {scheme.Value}"
        });
    }

    // 3. Aplikoni sigurinë VETËM për v2
    // Kjo bën që v1 të mbetet "flat" (pa dry) në interface
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            }, Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "RequestUser" }
            }, Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "CorrelationId" }
            }, Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ChannelCode" }
            }, Array.Empty<string>()
        }
    });

    // Opsionale: Diferenconi operacionet sipas versionit nëse nuk e bën automatikisht
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        if (!apiDesc.TryGetMethodInfo(out var methodInfo)) return false;

        // Merr versionin nga Namespace ose nga Route
        var versions = methodInfo.DeclaringType?.FullName ?? "";
        if (docName == "v2") return versions.Contains(".v2");
        if (docName == "v1") return versions.Contains(".v1");

        return true;
    });
});
var app = builder.Build();

// ✅ preload caches + prewarm presets (one scope)
using (var scope = app.Services.CreateScope())
{
    var reloader = scope.ServiceProvider.GetRequiredService<IAppCacheReloader>();
    await reloader.ReloadAllAsync();

    _ = scope.ServiceProvider.GetRequiredService<IAttributePresetService>();
}

// ✅ Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "aseeMIS API v1");
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "aseeMIS API v2");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "aseeMIS API v1");
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "aseeMIS API v2");
        c.RoutePrefix = "swagger";
    });
}





app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

//app.MapGrpcService<BankPartyOnboardingGrpcService>();
//app.MapGrpcService<CatalogGrpcService>();

app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


