using CronService.Models;
using CronService.Services.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;
using System;
using System.Collections.Generic;

namespace CronService.Services.JobFactory;

public class ZookeeperJobFactory : IJobFactory
{
    public static readonly ILogger<CreateCopiesJob> _logger;
    public static readonly List<WorkerJob> Workers = new List<WorkerJob>()
    {
        new CreateCopiesJob(_logger)
    };

    readonly IServiceProvider _serviceProvider;
    readonly IScheduler _workerScheduler;
    public ZookeeperJobFactory(IServiceProvider serviceProvider, IScheduler workerScheduler)
    {
        _serviceProvider = serviceProvider;
        _workerScheduler = workerScheduler;
    }
    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        IJob job = null;
        if(bundle.JobDetail.Key.Name == ZookeeperJob.JobName)
        {
            var zookeeperJob = _serviceProvider.GetService<ZookeeperJob>();
            zookeeperJob.AddWorkers(_workerScheduler, Workers);
            job = zookeeperJob;
        } 
        else
        {
            foreach(var worker in Workers)
            {
                if(bundle.JobDetail.Key.Name == worker.JobName)
                {
                    job = worker.GetJob(_serviceProvider);
                }
            }
        }
        return job;
    }

    public void ReturnJob(IJob job)
    {
        var disposable = job as IDisposable;
        disposable?.Dispose();
    }
}
