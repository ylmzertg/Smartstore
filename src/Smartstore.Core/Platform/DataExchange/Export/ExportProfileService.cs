﻿using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Seo;
using Smartstore.Core.Stores;
using Smartstore.Data.Hooks;
using Smartstore.Engine;
using Smartstore.Engine.Modularity;
using Smartstore.Http;
using Smartstore.IO;
using Smartstore.Scheduling;
using Smartstore.Utilities;

namespace Smartstore.Core.DataExchange.Export
{
    public partial class ExportProfileService : AsyncDbSaveHook<ExportProfile>, IExportProfileService
    {
        private const string _defaultFileNamePattern = "%Store.Id%-%Profile.Id%-%File.Index%-%Profile.SeoName%";
        private const string _exportFileRoot = "ExportProfiles";

        private static readonly Regex _regexFolderName = new(".*/ExportProfiles/?", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private readonly SmartDbContext _db;
        private readonly IApplicationContext _appContext;
        private readonly IStoreContext _storeContext;
        private readonly ILocalizationService _localizationService;
        private readonly IUrlHelper _urlHelper;
        private readonly DataExchangeSettings _dataExchangeSettings;

        public ExportProfileService(
            SmartDbContext db,
            IApplicationContext appContext,
            IStoreContext storeContext,
            ILocalizationService localizationService,
            IUrlHelper urlHelper,
            DataExchangeSettings dataExchangeSettings)
        {
            _db = db;
            _appContext = appContext;
            _storeContext = storeContext;
            _localizationService = localizationService;
            _urlHelper = urlHelper;
            _dataExchangeSettings = dataExchangeSettings;
        }

        public Localizer T { get; set; } = NullLocalizer.Instance;

        #region Hook

        protected override Task<HookResult> OnUpdatingAsync(ExportProfile entity, IHookedEntity entry, CancellationToken cancelToken)
        {
            entity.FolderName = PathHelper.NormalizeRelativePath(entity.FolderName);

            // TODO: (mg) (core) There's no such thing like IsSafeAppRootPath anymore, because all paths are now absolute and rooted to a specific app directory. TBD with MC.
            // TODO: (mg) (core) complete hook in ExportProfileService (PathHelper.IsSafeAppRootPath required).
            //if (!PathHelper.IsSafeAppRootPath(entity.FolderName))
            //{
            //    throw new SmartException(T("Admin.DataExchange.Export.FolderName.Validate"));
            //}

            return Task.FromResult(HookResult.Ok);
        }

        #endregion

        public virtual async Task<IDirectory> GetExportDirectoryAsync(ExportProfile profile, string subpath = null, bool createIfNotExists = false)
        {
            Guard.NotNull(profile, nameof(profile));
            Guard.IsTrue(profile.FolderName.EmptyNull().Length > 2, nameof(profile.FolderName), "The export folder name must be at least 3 characters long.");

            // Legacy examples:
            // ~/App_Data/ExportProfiles/smartstorecategorycsv
            // ~/App_Data/Tenants/Default/ExportProfiles/smartstoreshoppingcartitemcsv
            var root = _appContext.TenantRoot;
            var path = root.PathCombine(_exportFileRoot, _regexFolderName.Replace(profile.FolderName, string.Empty), subpath.EmptyNull());

            if (createIfNotExists)
            {
                var _ = await root.TryCreateDirectoryAsync(path);
            }

            return await root.GetDirectoryAsync(path);
        }

        public virtual async Task<IDirectory> GetDeploymentDirectoryAsync(ExportDeployment deployment, bool createIfNotExists = false)
        {
            if (deployment != null)
            {
                if (deployment.DeploymentType == ExportDeploymentType.PublicFolder)
                {
                    var webRoot = _appContext.WebRoot;
                    var path = webRoot.PathCombine(DataExporter.PublicDirectoryName, deployment.SubFolder);

                    if (createIfNotExists)
                    {
                        _ = await webRoot.TryCreateDirectoryAsync(path);
                    }

                    return await webRoot.GetDirectoryAsync(path);
                }
                else if (deployment.DeploymentType == ExportDeploymentType.FileSystem && deployment.FileSystemPath.HasValue())
                {
                    var fullPath = deployment.FileSystemPath;

                    if (!PathHelper.IsAbsolutePhysicalPath(fullPath))
                    {
                        fullPath = CommonHelper.MapPath(PathHelper.NormalizeRelativePath(fullPath));
                    }

                    // 'fullPath' must exist for LocalFileSystem (otherwise exception).
                    if (!Directory.Exists(fullPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(fullPath);
                        }
                        catch
                        {
                            return null;
                        }
                    }

                    var root = new LocalFileSystem(fullPath);

                    return await root.GetDirectoryAsync(null);
                }
            }

            return null;
        }

        public virtual async Task<string> GetDeploymentDirectoryUrlAsync(ExportDeployment deployment, Store store = null)
        {
            if (deployment != null && deployment.DeploymentType == ExportDeploymentType.PublicFolder)
            {
                if (store == null)
                {
                    await _db.LoadReferenceAsync(deployment, x => x.Profile);

                    var filter = XmlHelper.Deserialize<ExportFilter>(deployment.Profile.Filtering);
                    var storeId = filter.StoreId;

                    if (storeId == 0)
                    {
                        var projection = XmlHelper.Deserialize<ExportProjection>(deployment.Profile.Projection);
                        storeId = projection.StoreId ?? 0;
                    }

                    store = _storeContext.GetStoreById(storeId) ?? _storeContext.CurrentStore;
                }

                // Always use IUrlHelper.Content("~/subpath") or WebHelper.ToAbsolutePath("~/subpath") for public URLs,
                // so that IIS application path can be prepended if applicable. 
                var path = WebHelper.ToAppRelativePath(_appContext.WebRoot.PathCombine(DataExporter.PublicDirectoryName, deployment.SubFolder));

                return store.Url.TrimEnd('/') + _urlHelper.Content(path).EnsureEndsWith("/");
            }

            return null;
        }

        public virtual async Task<ExportProfile> InsertExportProfileAsync(
            Provider<IExportProvider> provider,
            bool isSystemProfile = false,
            string profileSystemName = null,
            int cloneFromProfileId = 0)
        {
            Guard.NotNull(provider, nameof(provider));

            var providerSystemName = provider.Metadata.SystemName;
            var resourceName = provider.Metadata.ResourceKeyPattern.FormatInvariant(providerSystemName, "FriendlyName");
            var profileName = await _localizationService.GetResourceAsync(resourceName, 0, false, providerSystemName, true);

            var profile = await InsertExportProfileAsync(
                providerSystemName,
                profileName.NullEmpty() ?? providerSystemName,
                provider.Value.FileExtension,
                provider.Metadata.ExportFeatures,
                isSystemProfile,
                profileSystemName,
                cloneFromProfileId);

            return profile;
        }

        public virtual async Task<ExportProfile> InsertExportProfileAsync(
            string providerSystemName,
            string name,
            string fileExtension,
            ExportFeatures features,
            bool isSystemProfile = false,
            string profileSystemName = null,
            int cloneFromProfileId = 0)
        {
            Guard.NotEmpty(providerSystemName, nameof(providerSystemName));

            if (name.IsEmpty())
            {
                name = providerSystemName;
            }

            if (!isSystemProfile)
            {
                var profileCount = await _db.ExportProfiles.CountAsync(x => x.ProviderSystemName == providerSystemName);
                name = $"{T("Common.My").Value} {name} {profileCount + 1}";
            }

            TaskDescriptor task = null;
            ExportProfile cloneProfile = null;
            ExportProfile profile = null;

            if (cloneFromProfileId != 0)
            {
                cloneProfile = await _db.ExportProfiles
                    .Include(x => x.Task)
                    .Include(x => x.Deployments)
                    .FindByIdAsync(cloneFromProfileId);
            }

            if (cloneProfile == null)
            {
                task = new TaskDescriptor
                {
                    CronExpression = "0 */6 * * *",     // Every six hours.
                    Type = nameof(DataExportTask),
                    Enabled = false,
                    StopOnError = false,
                    IsHidden = true
                };
            }
            else
            {
                task = cloneProfile.Task.Clone();
            }

            task.Name = name + " Task";

            _db.TaskDescriptors.Add(task);

            // Get the task ID.
            await _db.SaveChangesAsync();

            if (cloneProfile == null)
            {
                profile = new ExportProfile
                {
                    FileNamePattern = _defaultFileNamePattern
                };

                if (isSystemProfile)
                {
                    profile.Enabled = true;
                    profile.PerStore = false;
                    profile.CreateZipArchive = false;
                    profile.Cleanup = false;
                }
                else
                {
                    // What we do here is to preset typical settings for feed creation
                    // but on the other hand they may be untypical for generic data export\exchange.
                    var projection = new ExportProjection
                    {
                        RemoveCriticalCharacters = true,
                        CriticalCharacters = "¼,½,¾",
                        PriceType = PriceDisplayType.PreSelectedPrice,
                        NoGroupedProducts = features.HasFlag(ExportFeatures.CanOmitGroupedProducts),
                        OnlyIndividuallyVisibleAssociated = true,
                        DescriptionMerging = ExportDescriptionMerging.Description
                    };

                    var filter = new ExportFilter
                    {
                        IsPublished = true,
                        ShoppingCartTypeId = (int)ShoppingCartType.ShoppingCart
                    };

                    profile.Projection = XmlHelper.Serialize(projection);
                    profile.Filtering = XmlHelper.Serialize(filter);
                }
            }
            else
            {
                profile = cloneProfile.Clone();
            }

            profile.IsSystemProfile = isSystemProfile;
            profile.Name = name;
            profile.ProviderSystemName = providerSystemName;
            profile.TaskId = task.Id;

            var cleanedSystemName = providerSystemName
                .Replace("Exports.", string.Empty)
                .Replace("Feeds.", string.Empty)
                .Replace("/", string.Empty)
                .Replace("-", string.Empty);

            var folderName = SeoHelper.BuildSlug(cleanedSystemName, true, false, false)
                .ToValidPath()
                .Truncate(_dataExchangeSettings.MaxFileNameLength);

            profile.FolderName = _appContext.TenantRoot.CreateUniqueDirectoryName(_exportFileRoot, folderName);

            profile.SystemName = profileSystemName.IsEmpty() && isSystemProfile
                ? cleanedSystemName
                : profileSystemName;

            _db.ExportProfiles.Add(profile);

            // Get the export profile ID.
            await _db.SaveChangesAsync();

            task.Alias = profile.Id.ToString();

            if (fileExtension.HasValue() && !isSystemProfile)
            {
                if (cloneProfile == null)
                {
                    if (features.HasFlag(ExportFeatures.CreatesInitialPublicDeployment))
                    {
                        var webRoot = _appContext.WebRoot;
                        var subfolder = webRoot.CreateUniqueDirectoryName(DataExporter.PublicDirectoryName, folderName);
                        _ = await webRoot.TryCreateDirectoryAsync(webRoot.PathCombine(DataExporter.PublicDirectoryName, subfolder));

                        profile.Deployments.Add(new ExportDeployment
                        {
                            ProfileId = profile.Id,
                            Enabled = true,
                            DeploymentType = ExportDeploymentType.PublicFolder,
                            Name = profile.Name,
                            SubFolder = subfolder
                        });
                    }
                }
                else
                {
                    cloneProfile.Deployments.Each(x => profile.Deployments.Add(x.Clone()));
                }
            }

            // Finally update task and export profile.
            await _db.SaveChangesAsync();

            return profile;
        }

        public virtual async Task DeleteExportProfileAsync(ExportProfile profile, bool force = false)
        {
            Guard.NotNull(profile, nameof(profile));

            if (!force && profile.IsSystemProfile)
            {
                throw new SmartException(T("Admin.DataExchange.Export.CannotDeleteSystemProfile"));
            }

            await _db.LoadCollectionAsync(profile, x => x.Deployments);
            await _db.LoadReferenceAsync(profile, x => x.Task);

            var directory = await GetExportDirectoryAsync(profile);
            var deployments = profile.Deployments.Where(x => !x.IsTransientRecord()).ToList();

            if (profile.Deployments.Any())
            {
                _db.ExportDeployments.RemoveRange(deployments);
            }

            if (profile.Task != null)
            {
                _db.TaskDescriptors.Remove(profile.Task);
            }

            _db.ExportProfiles.Remove(profile);

            await _db.SaveChangesAsync();

            if (directory.Exists)
            {
                directory.FileSystem.ClearDirectory(directory, true, TimeSpan.Zero);
            }
        }
    }
}
