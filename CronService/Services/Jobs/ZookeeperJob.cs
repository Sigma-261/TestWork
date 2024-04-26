using CronService.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CronService.Services.Jobs;

public class ZookeeperJob : IJob
{
    public const string JobName = "ZookeeperJob";
    public const string JobGroup = "ZookeeperJobGroup";
    public const string TriggerName = "ZookeeperTrigger";
    public const string TriggerGroup = "ZookeeperTriggerGroup";

    private IScheduler _workerScheduler;
    private List<WorkerJob> _workers;

    public void AddWorkers(IScheduler workerScheduler, List<WorkerJob> workers)
    {
        _workerScheduler = workerScheduler;
        _workers = workers;
    }

    static ITrigger GetNewTrigger(string name, string group, string schedule)
    {
        return TriggerBuilder.Create()
            .WithIdentity(name, group)
            .WithCronSchedule(schedule)
            .Build();
    }
    async Task RescheduleJob()
    {
        foreach (var worker in _workers)
        {
            var trigger = await _workerScheduler.GetTrigger(worker.GetTriggerKey);

            var cronTrigger = (ICronTrigger)trigger;
            var newExpression = AppSettings.GetValue(worker.Cron);

            if (cronTrigger.CronExpressionString != newExpression)
            {
                Console.WriteLine("Изменение расписания");
                var newTrigger = GetNewTrigger(trigger.Key.Name, trigger.Key.Group, newExpression);
                await _workerScheduler.RescheduleJob(trigger.Key, newTrigger);
            }
        }
    }
    public Task Execute(IJobExecutionContext context)
    {
        try
        {
            var task = Task.Run(RescheduleJob);
            task.Wait();

            if (_workerScheduler != null && !_workerScheduler.IsStarted)
            {
                _workerScheduler.Start();
            }

            return Task.CompletedTask;
        } 
        catch(Exception e)
        {
            Console.WriteLine("Received an error when executing Zookeeper job. " + e.Message);
            return Task.FromException(e);
        }
    }
}
