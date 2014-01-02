using System;
using System.Threading;
using AzureWebFarm.OctopusDeploy.Infrastructure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Octopus.Client;
using Serilog;

namespace AzureWebFarm.OctopusDeploy
{
    public class WebRole
    {
        private readonly ConfigSettings _config;
        private readonly Infrastructure.OctopusDeploy _octopusDeploy;

        public WebRole(string machineName = null)
        {
            Log.Logger = Logging.GetAzureLogger();
            _config = new ConfigSettings(RoleEnvironment.GetConfigurationSettingValue);

            machineName = machineName ?? AzureEnvironment.GetMachineName(_config);
            var octopusRepository = new OctopusRepository(new OctopusServerEndpoint(_config.OctopusServer, _config.OctopusApiKey));
            var processRunner = new ProcessRunner();
            _octopusDeploy = new Infrastructure.OctopusDeploy(machineName, _config, octopusRepository, processRunner);

            AzureEnvironment.RequestRecycleIfConfigSettingChanged(_config);
        }

        public bool OnStart()
        {
            _octopusDeploy.ConfigureTentacle();
            _octopusDeploy.DeployAllCurrentReleasesToThisMachine();
            return true;
        }

        public void Run()
        {
            // Don't want to configure IIS if we are emulating; just sleep forever
            if (RoleEnvironment.IsEmulated)
                Thread.Sleep(-1);

            while (true)
            {
                try
                {
                    IisEnvironment.ActivateAppInitialisationModuleForAllSites();
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Failure to configure IIS");
                }

                Thread.Sleep(TimeSpan.FromMinutes(10));
            }
        // ReSharper disable FunctionNeverReturns
        }
        // ReSharper restore FunctionNeverReturns

        public void OnStop()
        {
            _octopusDeploy.DeleteMachine();
            IisEnvironment.WaitForAllHttpRequestsToEnd();
        }
    }

    
}
