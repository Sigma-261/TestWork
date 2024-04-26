using CronService.Models;
using CronService.Services.JobFactory;
using CronService.Services.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;
using System;
using System.Threading.Tasks;

namespace CronService.Infrastructure;

public static class SchedulerConfiguration
{
    private const string _zookeeperCron = "*/10 * * * * ? *";

    static async Task<IScheduler> ConfigureWorkers(IScheduler scheduler)
    {
        foreach(var worker in ZookeeperJobFactory.Workers)
        {
            scheduler = await worker.ConfigureScheduler(scheduler);
        }
        return scheduler;
    }

    static async Task<IScheduler> ConfigureZookeeper(IScheduler zookeeperScheduler)
    {
        var zookeeperJob = JobBuilder.Create<ZookeeperJob>()
            .WithIdentity(ZookeeperJob.JobName, ZookeeperJob.JobGroup)
            .Build();    

        var zookeeperTrigger = TriggerBuilder.Create()
            .WithIdentity(ZookeeperJob.TriggerName, ZookeeperJob.TriggerGroup)
            .WithCronSchedule(_zookeeperCron)
            .Build();

        await zookeeperScheduler.ScheduleJob(zookeeperJob, zookeeperTrigger);            
        
        return zookeeperScheduler;
    }

    public static async Task Configure(IServiceCollection services)
    {
        try
        {
            var serviceProvider = services.BuildServiceProvider();

            var factory = new StdSchedulerFactory();
            var scheduler = await factory.GetScheduler();
            scheduler.JobFactory = new ZookeeperJobFactory(serviceProvider, scheduler);

            await ConfigureWorkers(scheduler);
            await ConfigureZookeeper(scheduler);

            await scheduler.Start();

            await Task.Delay(TimeSpan.FromSeconds(1));
            
        } catch(Exception e)
        {
            Console.WriteLine("Error configuring scheduler. " + e.Message);
        }
    }
}
