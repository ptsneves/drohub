using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using DroHub.Areas.DHub.API;
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

namespace DroHub
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<DroHubContext>(options => {
                var db_provider = Configuration.GetValue<string>(Program.DATABASE_PROVIDER_KEY);
                options.UseMySql(Configuration.GetConnectionString(db_provider));
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
            services.AddSingleton<IConfiguration>(provider => Configuration);

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
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

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
