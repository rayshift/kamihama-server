using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KamihamaWeb.Interfaces;
using KamihamaWeb.Services;
using KamihamaWeb.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace KamihamaWeb
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
            /*services.AddStackExchangeRedisCache(action =>
            {
                action.InstanceName = Configuration["DatabaseConfiguration:RedisInstanceName"];
                action.Configuration = Configuration["DatabaseConfiguration:RedisConfiguration"];
            });*/
            services.AddHttpCacheHeaders();
            services.AddControllersWithViews();

            services.AddResponseCompression(o =>
            {
                o.Providers.Add<GzipCompressionProvider>();
                o.EnableForHttps = true; // TODO: Is BREACH an issue?
                o.MimeTypes =
                    ResponseCompressionDefaults.MimeTypes.Concat(
                        new[] { "application/json" });
            });

            // Dependencies
            services.AddTransient<IRestSharpTransient, RestSharpClient>();
            services.AddSingleton<IMasterSingleton, MasterService>();
            services.AddSingleton<IDiskCacheSingleton, DiskCacheService>();
            services.AddTransient<IMasterListBuilder, MasterListBuilder>();

            // Jobs
            services.AddTransient<MasterUpdateJob>();

            services.AddQuartz(q =>
            {
                // base quartz scheduler, job and trigger configuration
                q.SchedulerId = "Scheduler-Core";

                q.UseMicrosoftDependencyInjectionJobFactory(options =>
                {
                    // if we don't have the job in DI, allow fallback 
                    // to configure via default constructor
                    options.AllowDefaultConstructor = true;
                });

                var jobKey = new JobKey("Master Update Check", "Master");
                q.AddJob<MasterUpdateJob>(jobKey, j => j.WithDescription("Update master asset list if required."));

                q.AddTrigger(t => t.WithIdentity("Master Update Check Trigger")
                    .ForJob(jobKey)
                    .StartNow()
                    .WithSimpleSchedule(s => s.
                        WithInterval(TimeSpan.FromMinutes(2))
                        .RepeatForever()
                    )
                    .WithDescription("Trigger for Master Update Check to update asset list."));
            });

            // ASP.NET Core hosting
            services.AddQuartzServer(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpCacheHeaders();
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseResponseCompression();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
