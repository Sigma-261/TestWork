using CronService.Infrastructure;
using CronService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz.Logging;
using Quartz;
using System;

namespace CronService
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices(async services => {
                    services.AddQuartz();
                    services.AddOptions();
                    ServiceRegistration.AddServices(services);
                    await SchedulerConfiguration.Configure(services);

                });
        }
    }
}
