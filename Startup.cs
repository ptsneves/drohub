using DroHub.Areas.DHub.Models;
using DroHub.Areas.DHub.SignalRHubs;
using DroHub.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Net;

using DroHub.Helpers;
using DroHub.Helpers.Thrift;
using Microsoft.AspNetCore.HttpOverrides;

using Thrift.Protocol;
using Thrift.Server;
using Thrift.Transport;
using Thrift.Transport.Server;

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

            services.Configure<DropboxRepositorySettings>(Configuration.GetSection("RepositoriesConfiguration").GetSection("Dropbox"));
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
            services.AddMvc().AddRazorPagesOptions(options =>
            {
                options.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "/Account/Login");
            }).SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.MapWebSocketManager("/ws");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });


            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "areas",
                    template: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
                );
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseSignalR(route =>
            {
                route.MapHub<NotificationsHub>("/notificationshub");
                route.MapHub<TelemetryHub>("/telemetryhub");
            });
        }
    }
}
