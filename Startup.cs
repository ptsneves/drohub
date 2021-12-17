using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using ConfigurationScanner;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.DHub.SignalRHubs;
using DroHub.Areas.Identity.Data;
using DroHub.Areas.Identity.Pages;
using DroHub.Areas.Identity.Services;
using DroHub.Data;
using DroHub.Helpers;
using DroHub.Helpers.AuthenticationHandler;
using DroHub.Helpers.Thrift;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace DroHub
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            (Configuration as IConfigurationRoot)
                .ThrowOnConfiguredForbiddenToken();
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            services.AddRouting();
            services.AddDbContext<DroHubContext>(options => {
                options.UseMySql(Configuration.GetDroHubConnectionString());
            });

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                foreach (var addr in Dns.GetHostEntry("server").AddressList) {
                    options.KnownProxies.Add(addr);
                }
            });

            services.Configure<RepositoryOptions>(Configuration.GetSection("RepositoriesConfiguration"));
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                // options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
                services.ConfigureExternalCookie(o => {
                    o.Cookie.SameSite = SameSiteMode.None;
                });
                services.ConfigureApplicationCookie(o =>
                {
                    o.Cookie.SameSite = SameSiteMode.None;
                });
            });

            services.Configure<ApiBehaviorOptions>(o => {
                o.InvalidModelStateResponseFactory = context => {
                    //We do not want to disturb other APIs
                    if (context.ActionDescriptor.RouteValues["action"] != "UploadMedia" ||
                        context.ActionDescriptor.RouteValues["controller"] != "AndroidApplication")
                        return new BadRequestResult();

                    var model_error =
                        context.ModelState.Values.SelectMany(v => v.Errors
                            .Select(b => b.ErrorMessage)).FirstOrDefault();

                    if (!string.IsNullOrEmpty(model_error)) {
                        return new JsonResult(new {
                            result = "nok",
                            error = model_error
                        });
                    }

                    return new BadRequestResult();
                };
            });

            services.Configure<JanusServiceOptions>(Configuration.GetSection("JanusServiceOptions"));
            services.AddHostedService<NotificationsHubPoller>();
            services.AddHttpClient<JanusService>();
            services.AddSingleton(provider => Configuration);

            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(
                    Configuration.GetValue<string>("KeysDirectory")));

            services.AddSignalR();

            services.AddAuthentication(RouteAuthenticationHandler.SchemeName)
                .RouteAuthentication(IdentityConstants.ApplicationScheme, new List<AuthenticationSchemeRoute> {
                    new AuthenticationSchemeRoute {
                        SchemeName = TokenAuthenticationHandler.SchemeName,
                        StartPaths = new List<PathString>() {"/api/AndroidApplication"}
                    },
                    new AuthenticationSchemeRoute {
                        SchemeName = TelemetryAuthenticationHandler.SchemeName,
                        StartPaths = new List<PathString>() {"/ws"}
                    }
                })
                .AddTokenAuthentication()
                .AddTelemetryAuthentication();

            services.AddAuthorization();

            services.AddSubscriptionAPI();
            services.AddDeviceResourceAuthorization();
            services.AddMediaObjectResourceAuthorization();
            services.AddDroHubUserResourceAuthorization();
            services.AddMailJetEmailSenderExtensions(Configuration);
            services.AddDeviceAPI();
            services.AddDeviceConnectionSessionAPI();

            services.AddMediaObjectTagAPI();


            services.AddWebSocketManager();
            services
                .AddMvc()
                .AddRazorRuntimeCompilation()
                .AddRazorPagesOptions(options => {
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "/Account/");
                });

            services.AddScoped<IUserClaimsPrincipalFactory<DroHubUser>, DroHubClaimsIdentityFactory>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app,
            IWebHostEnvironment env,
            DroHubContext db_context,
            SignInManager<DroHubUser> sign_in_manager,
            ILogger<DroHubContext> logger)
        {
            if (!Configuration.GetValue<bool>("AutoMigration")) {
                db_context.Database.Migrate();
                logger.LogWarning("Ran migrate database");
            }

            foreach(var u in db_context.Users.ToList()) {

                var t = DroHubUser.refreshClaims(sign_in_manager, u);
                t.Wait();
                if (t.Result == IdentityResult.Failed()) {
                    logger.LogError("failed to refresh claims");
                }
            }

            logger.LogWarning("Ran user claims migration");

            {
                var t = ProgramExtensions.InitializeAdminUserHelper
                    .createAdminUser(logger, sign_in_manager, db_context);
                t.Wait();
            }

            {
                var t = LocalStorageHelper.generateVideoPreviewForConnectionDir(
                    logger, true);
                t.Wait();
                logger.LogWarning("Generated video previews");
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseStatusCodePages();
            }

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseCookiePolicy(new CookiePolicyOptions
            {
                MinimumSameSitePolicy = SameSiteMode.None
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapHub<NotificationsHub>("/notificationshub");
                endpoints.MapHub<TelemetryHub>("/telemetryhub");
                endpoints
                    .MapControllerRoute("areas", "{area:exists}/{controller}/{action=Index}/{id?}")
                    .RequireAuthorization();
                endpoints.MapControllerRoute("api", "api/{controller}/{action}");
                endpoints.MapWebSocketManager("/ws");
                endpoints.MapRazorPages();
            });
        }
    }
}
