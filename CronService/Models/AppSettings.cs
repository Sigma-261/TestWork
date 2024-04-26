using Microsoft.Extensions.Configuration;

namespace CronService.Models
{
    public static class AppSettings
    {
        public static string Cron = "Options:Cron";
        public static string PathIn = "Options:PathIn";
        public static string PathOut = "Options:PathOut";
        public static string GetValue(string key)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            return configuration.GetSection(key).Value;
        }
    }
}
