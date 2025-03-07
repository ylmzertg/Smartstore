﻿using System.Threading;
using System.Threading.Tasks;

namespace Smartstore.Core.DataExchange.Export
{
    /// <summary>
    /// Data exporter contract.
    /// </summary>
    public partial interface IDataExporter
    {
        /// <summary>
        /// Starts exporting data. An export provider is responsible for what happens to the data, 
        /// e.g. whether it should be written to a file (default).
        /// </summary>
        /// <param name="request">Contains request data for exporting.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the export.</param>
        /// <returns>Data export result.</returns>
        Task<DataExportResult> ExportAsync(DataExportRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Exports data to display them in a preview grid.
        /// </summary>
        /// <param name="request">Contains request data for exporting.</param>
        /// <param name="pageIndex">The data page index.</param>
        /// <returns>Data export preview result.</returns>
        Task<DataExportPreviewResult> PreviewAsync(DataExportRequest request, int pageIndex);
    }
}
