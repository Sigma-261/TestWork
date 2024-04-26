using CronService.Models;
using Quartz;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CronService.Services.Jobs;

//[DisallowConcurrentExecution] - не позволяет создать задачу, которая уже существует
public class CreateCopiesJob : WorkerJob
{
    public static string JobName = "CreateCopiesJob";
    public static string JobGroup = "CreateCopiesJobGroup";
    public static string TriggerName = "CreateCopiesTrigger";
    public static string TriggerGroup = "CreateCopiesTriggerGroup";
    public readonly ILogger<IJob> _logger;

    public CreateCopiesJob(ILogger<IJob> logger) : base(JobName, JobGroup, TriggerName, TriggerGroup, AppSettings.Cron, typeof(CreateCopiesJob))
    {
        _logger = logger;
    }

    public override IJob GetJob(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<CreateCopiesJob>();
    }
    public override Task Execute(IJobExecutionContext context)
    {
        try
        {
            if(context.Scheduler.GetCurrentlyExecutingJobs().Result.Count > 1)
            {
                _logger.LogWarning("Процесс копирование все еще выполняется!");
                return null;
            }
            _logger.LogInformation($"Процесс копирование начался в {DateTime.Now}");
            var pathIn = AppSettings.GetValue(AppSettings.PathIn);
            var pathOut = AppSettings.GetValue(AppSettings.PathOut);

            CreateCopies(pathIn, pathOut);

            _logger.LogInformation($"Процесс копирование закончился в {DateTime.Now}");
            return Task.CompletedTask;
        }
        catch (Exception e)
        {
            _logger.LogError($"Получена ошибка при выполнении копирования: {e.Message}");
            return Task.FromException(e);
        }
    }

    private void CreateCopies(string pathIn, string pathOut)
    {
        #region Подготовление данных
        var dirsIn = Directory.GetDirectories(pathIn, "*", SearchOption.AllDirectories).Select(x => x.Replace(pathIn, "")).ToList();

        var topDirsOut = Directory.GetDirectories(pathOut, "*", SearchOption.TopDirectoryOnly).Select(x => x.Replace(pathOut, "")).ToList();
        var topDirsIn = Directory.GetDirectories(pathIn, "*", SearchOption.TopDirectoryOnly).Select(x => x.Replace(pathIn, "")).ToList();

        var matchTopDirsOut = topDirsOut.Intersect(topDirsIn).ToList();
        topDirsOut = topDirsOut.Except(matchTopDirsOut).ToList();

        var dirsMatchOut = new List<string>();

        List<string> newDirs = new List<string>();
        List<string> newFiles = new List<string>();

        var dirsOut = topDirsOut.SelectMany(x => Directory.GetDirectories(pathOut + x, "*", SearchOption.AllDirectories).Select(s => s.Replace(pathOut + x, ""))).ToList();
        #endregion

        #region Список новых директорий
        foreach (var topDir in matchTopDirsOut)
        {
            var gluedPathIn = pathIn + topDir;
            var gluedPathOut = pathOut + topDir;

            var dirsMatchIn = Directory.GetDirectories(gluedPathIn, "*", SearchOption.AllDirectories).Select(x => x.Replace(pathIn, "")).ToList();
            dirsMatchOut.AddRange(Directory.GetDirectories(gluedPathOut, "*", SearchOption.AllDirectories).Select(x => x.Replace(pathOut, "")).ToList());

            newDirs.AddRange(dirsMatchIn.Except(dirsMatchOut).ToList());
        }

        var newDir = dirsIn.Except(dirsOut).ToList();
        newDirs = newDir.Except(dirsMatchOut).ToList();
        #endregion

        #region Работа с файлами
        var filesIn = Directory.GetFiles(pathIn, "*.*", SearchOption.AllDirectories).Select(x => x.Replace(pathIn, "")).ToList();
        var filesOut = Directory.GetFiles(pathOut, "*.*", SearchOption.AllDirectories).Select(x => x.Replace(pathOut, "")).ToList();

        //Удаление в путях верхних директорий
        foreach (var dir in topDirsOut)
        {
            filesOut = filesOut.Select(x => x.Replace(dir, "")).ToList();
        }

        //Полные пути к файлам
        var filesPathIn = Directory.GetFiles(pathIn, "*.*", SearchOption.AllDirectories).ToList();
        var filesPathOut = Directory.GetFiles(pathOut, "*.*", SearchOption.AllDirectories).ToList();

        //Работа с файлами
        foreach (var fileIn in filesIn)
        {
            var existFile = filesOut.FirstOrDefault(x => x == fileIn);
            if (existFile is null)
                newFiles.Add(pathIn + fileIn);
            else
            {
                if (fileIn.Count(x => x == '\\' || x == '/') == 1)
                {
                    var files = topDirsOut.Select(x => x + fileIn).ToList();
                    List<FileInfo> fileInfos = new List<FileInfo>();
                    foreach (var file in files)
                    {
                        var fileFullOut = pathOut + file;
                        fileInfos.Add(new FileInfo(fileFullOut));
                    }
                    fileInfos.Add(new FileInfo(pathOut + existFile));
                    var testFile = fileInfos.Where(x => x.Exists == true).OrderByDescending(x => x.CreationTimeUtc).First();

                    var hashCodeFileIn = GetHashCode(pathIn + existFile, SHA256.Create());
                    var hashCodeFileOut = GetHashCode(testFile.FullName, SHA256.Create());

                    if (hashCodeFileIn != hashCodeFileOut)
                        newFiles.Add(pathIn + existFile);
                }
                else
                {
                    var fullPathsOut = filesPathOut.Where(x => x.Contains(existFile)).ToList();
                    var fullPathIn = filesPathIn.FirstOrDefault(x => x.Contains(existFile));
                    var fullPathOut = fullPathsOut[0];
                    if (fullPathsOut.Count > 1)
                    {
                        List<FileInfo> fileInfos = new List<FileInfo>();
                        foreach (var filePath in fullPathsOut)
                        {
                            fileInfos.Add(new FileInfo(filePath));
                        }
                        fullPathOut = fileInfos.OrderByDescending(x => x.CreationTimeUtc).First().FullName;
                    }

                    var hashCodeFileIn = GetHashCode(fullPathIn, SHA256.Create());
                    var hashCodeFileOut = GetHashCode(fullPathOut, SHA256.Create());

                    if (hashCodeFileIn != hashCodeFileOut)
                        newFiles.Add(pathIn + existFile);

                }
            }
        }

        #endregion

        #region Обозначение конечного путя длля копирования
        if (Directory.GetDirectories(pathOut, "*", SearchOption.TopDirectoryOnly).Any(x => x.Replace(pathOut, "").Contains("base")))
        {
            DateTime date = DateTime.Now;
            pathOut = Path.Combine(pathOut, $"inc_{date.Year}_{date.Month}_{date.Day}_{date.Hour}_{date.Minute}_{date.Second}");
        }
        else
        {
            pathOut = Path.Combine(pathOut, "base");
        }
        #endregion

        #region Копирование
        if (newDirs.Count == 0 && newFiles.Count == 0)
            _logger.LogInformation($"Элементы в исходной папке совпадают с элементами в папке назначения");

        foreach (var dir in newDirs)
        {
            Directory.CreateDirectory(pathOut + dir);
        }

        foreach (var file in newFiles)
        {
            FileInfo fileInfo = new FileInfo(file.Replace(pathIn, pathOut));
            if (!fileInfo.Exists)
                Directory.CreateDirectory(fileInfo.Directory.FullName);

            File.Copy(file, file.Replace(pathIn, pathOut));
        }
        #endregion
    }

    private string GetHashCode(string filePath, HashAlgorithm cryptoService)
    {
        using (cryptoService)
        {
            using (var fileStream = new FileStream(filePath,
                                                   FileMode.Open,
                                                   FileAccess.Read,
                                                   FileShare.ReadWrite))
            {
                var hash = cryptoService.ComputeHash(fileStream);
                var hashString = Convert.ToBase64String(hash);
                return hashString.TrimEnd('=');
            }
        }
    }
}
