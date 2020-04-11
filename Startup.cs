using System;
using System.Net;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.DHub.SignalRHubs;
using DroHub.Data;
using DroHub.Helpers;
using DroHub.Helpers.Thrift;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
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
            services.AddDbContext<DroHubContext>(options =>
            {
                switch (Configuration.GetValue<string>("DatabaseProvider")){
                    case "mssql":
                        options.UseSqlServer(Configuration.GetConnectionString("DroHubConnectionMSSQL"));
                        break;
                    case "mysql":
                        options.UseMySql(Configuration.GetConnectionString("DroHubConnectionMySQL"));
                        break;
                    default:
                        throw new InvalidProgramException("You need to set DatabaseProvider property to mysql or mssql");
                }
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
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.Configure<JanusServiceOptions>(Configuration.GetSection("JanusServiceOptions"));
            services.AddHostedService<NotificationsHubPoller>();
            services.AddHttpClient<JanusService>();

            services.AddSignalR();
            services.AddWebSocketManager();
            services.AddMvc().AddRazorRuntimeCompilation().AddRazorPagesOptions(options =>
            {
                options.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "/Account/");
            }).AddMvcOptions(optsions => {
                optsions.EnableEndpointRouting = false;
            });
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
            app.UseCookiePolicy();
            app.MapWebSocketManager("/ws");
            app.UseAuthentication();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "areas",
                    template: "{area:exists}/{controller}/{action=Index}/{id?}"
                );
            });

            app.UseSignalR(route =>
            {
                route.MapHub<NotificationsHub>("/notificationshub");
                route.MapHub<TelemetryHub>("/telemetryhub");
            });
        }
    }
}
