﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;
using Microsoft.Extensions.Logging;
using Smartstore.Core.Localization;
using Smartstore.IO;

namespace Smartstore.Core.DataExchange.Export.Deployment
{
    public interface IFilePublisher
    {
        /// <summary>
        /// Publishes the result of a file based data export.
        /// </summary>
        /// <param name="deployment">Export deployment.</param>
        /// <param name="context">Deployment context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PublishAsync(ExportDeployment deployment, ExportDeploymentContext context, CancellationToken cancellationToken);
    }

    public class ExportDeploymentContext
    {
        public Localizer T { get; init; }
        public ILogger Log { get; init; }
        public IExportProfileService ExportProfileService { get; init; }
        public IDirectory ExportDirectory { get; init; }
        public IFile ZipFile { get; init; }
        public bool CreateZipArchive { get; init; }

        public DataDeploymentResult Result { get; set; }

        public async Task<IList<IFile>> GetDeploymentFilesAsync(CancellationToken cancellationToken)
        {
            if (CreateZipArchive)
            {
                if (ZipFile?.Exists ?? false)
                {
                    return new List<IFile> { ZipFile };
                }
            }
            else
            {
                // Avoid accidents with incalculable consequences due to hundreds of files.
                if (ExportDirectory?.SubPath?.HasValue() ?? false)
                {
                    var files = await ExportDirectory.FileSystem
                        .EnumerateFilesAsync(ExportDirectory.SubPath, "*", true)
                        .ToListAsync(cancellationToken);

                    return files;
                }
            }

            return new List<IFile>();
        }
    }
}
