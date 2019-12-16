﻿using Aiursoft.Pylon.Interfaces;
using Aiursoft.Pylon.Services;
using Aiursoft.Pylon.Services.ToProbeServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kahla.Server.Services
{
    public class FilesCleaner : IHostedService, IDisposable, ISingletonDependency
    {
        private readonly ILogger _logger;
        private Timer _timer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AppsContainer _appsContainer;
        private readonly IConfiguration _configuration;

        public FilesCleaner(
            ILogger<FilesCleaner> logger,
            IServiceScopeFactory scopeFactory,
            AppsContainer appsContainer,
            IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _appsContainer = appsContainer;
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is starting.");
            _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10));
            return Task.CompletedTask;
        }

        private async void DoWork(object state)
        {
            try
            {
                _logger.LogInformation("Cleaner task started!");
                using (var scope = _scopeFactory.CreateScope())
                {
                    var foldersService = scope.ServiceProvider.GetRequiredService<FoldersService>();
                    await AllClean(foldersService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred.");
            }
        }

        public async Task AllClean(FoldersService foldersService)
        {
            try
            {
                var deadline = DateTime.UtcNow - TimeSpan.FromDays(100);
                var publicSite = _configuration["UserFilesSiteName"];
                var accessToken = await _appsContainer.AccessToken();
                var rootFolders = await foldersService.ViewContentAsync(accessToken, publicSite, string.Empty);
                foreach (var conversation in rootFolders.Value.SubFolders)
                {
                    var folders = await foldersService.ViewContentAsync(accessToken, publicSite, conversation.FolderName);
                    foreach (var folder in folders.Value.SubFolders)
                    {
                        try
                        {
                            var parts = folder.FolderName.Split('-');
                            var time = new DateTime(
                                Convert.ToInt32(parts[0]),
                                Convert.ToInt32(parts[1]),
                                Convert.ToInt32(parts[2]));
                            if (time < deadline)
                            {
                                await foldersService.DeleteFolderAsync(accessToken, publicSite, $"{conversation.FolderName}/{folder.FolderName}");
                            }
                        }
                        catch
                        {
                            await foldersService.DeleteFolderAsync(accessToken, publicSite, $"{conversation.FolderName}/{folder.FolderName}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
