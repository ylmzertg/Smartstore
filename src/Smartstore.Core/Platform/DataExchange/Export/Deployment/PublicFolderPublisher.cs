﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Smartstore.Engine;

namespace Smartstore.Core.DataExchange.Export.Deployment
{
    public class PublicFolderPublisher : IFilePublisher
    {
        private readonly IApplicationContext _appContext;

        public PublicFolderPublisher(IApplicationContext appContext)
        {
            _appContext = appContext;
        }

        public async Task PublishAsync(ExportDeployment deployment, ExportDeploymentContext context, CancellationToken cancellationToken)
        {
            var deploymentDir = await context.ExportProfileService.GetDeploymentDirectoryAsync(deployment, true);
            if (deploymentDir == null)
            {
                return;
            }

            var source = _appContext.ContentRoot.AttachEntry(context.ExportDirectory);

            if (context.CreateZipArchive)
            {
                if (context?.ZipFile?.Exists ?? false)
                {
                    var zipPath = source.FileSystem.PathCombine(source.Parent.SubPath, context.ZipFile.Name);
                    var zipFile = await source.FileSystem.GetFileAsync(zipPath);

                    using var stream = await zipFile.OpenReadAsync();

                    var newPath = deploymentDir.FileSystem.PathCombine(deploymentDir.SubPath, zipFile.Name);
                    var newFile = await deploymentDir.FileSystem.CreateFileAsync(newPath, stream, true);

                    if (newFile?.Exists ?? false)
                    {
                        context.Log.Info($"Copied zipped export data to {newPath}.");
                    }
                    else
                    {
                        context.Log.Warn($"Failed to copy zipped export data to {newPath}.");
                    }
                }
            }
            else
            {
                // Ugly, but the only way I got it to work with CopyDirectoryAsync.
                var webRootDir = await _appContext.WebRoot.GetDirectoryAsync(null);
                var newPath = deploymentDir.FileSystem.PathCombine(webRootDir.Name, deploymentDir.SubPath);

                await source.FileSystem.CopyDirectoryAsync(source.SubPath, newPath);

                context.Log.Info($"Export data files are copied to {newPath}.");
            }
        }
    }
}
