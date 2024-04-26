using Quartz;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz.Logging;

namespace CronService.Models;

public class WorkerJob : IJob
{
    public WorkerJob(string jobName, string jobGroup, string triggerName, string triggerGroup,
    string cron, Type jobType)
    {
        JobName = jobName;
        JobGroup = jobGroup;
        TriggerName = triggerName;
        TriggerGroup = triggerGroup;
        Cron = cron;
        JobType = jobType;
    }
    public string JobName { get; set; }
    public string JobGroup { get; set; }
    public string TriggerName { get; set; }
    public string TriggerGroup { get; set; }
    public string Cron { get; set; }
    public Type JobType { get; set; }

    public JobKey JobKey
    {
        get
        {
            return new JobKey(JobName, JobGroup);
        }
    }

    public TriggerKey GetTriggerKey
    {
        get
        {
            return new TriggerKey(TriggerName, TriggerGroup);
        }
    }

    public virtual Task Execute(IJobExecutionContext context)
    {
        return Task.CompletedTask;
    }

    public virtual IJob GetJob(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<WorkerJob>();
    }

    public async Task<IScheduler> ConfigureScheduler(IScheduler scheduler)
    {
        var triggerTime = AppSettings.GetValue(Cron);
        
        var job = JobBuilder.Create(JobType)
            .WithIdentity(JobName, JobGroup)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(TriggerName, TriggerGroup)
            .WithCronSchedule(triggerTime)
            .Build();

        await scheduler.ScheduleJob(job, trigger);
        return scheduler;
    }
}
