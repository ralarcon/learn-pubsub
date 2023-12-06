using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.Shared
{
    public static class AppConfigProvider
    {
        static readonly IConfiguration _config;
        static AppConfigProvider()
        {

            _config = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(Environment.GetCommandLineArgs())
                .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
                .Build();
        }

        //Generate a method to load configuration from a certain section which returns an object of type T, that is a generic method
        public static T LoadConfiguration<T>() where T : class, new()
        {
            T instance = new();
            _config.GetSection(instance.GetType().Name).Bind(instance);
            return instance;
        }

        public static T LoadConfiguration<T>(string section) where T : class, new()
        {
            T instance = new();
            _config.GetSection(section).Bind(instance);
            return instance;
        }
    }
}
