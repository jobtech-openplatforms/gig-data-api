using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IPlatformManager
    {
        Task<Platform> CreatePlatform(string name,
            PlatformAuthenticationMechanism authMechanism, PlatformIntegrationType platformIntegrationType,
            RatingInfo ratingInfo, int? dataPollIntervalInSeconds, Guid externalPlatformId, string description,
            string logoUrl, IAsyncDocumentSession session, bool isInactive = false);

        Task<Platform> GetPlatform(string platformId, IAsyncDocumentSession session);
        Task<Platform> GetPlatformByExternalId(Guid externalId, IAsyncDocumentSession session);

        Task<IDictionary<string, Platform>> GetPlatforms(IList<string> platformIds,
            IAsyncDocumentSession session);

        Task<IList<Platform>> GetAllPlatforms(IAsyncDocumentSession session);

    }

    public class PlatformManager : IPlatformManager
    {

        public async Task<Platform> CreatePlatform(string name,
            PlatformAuthenticationMechanism authMechanism, PlatformIntegrationType platformIntegrationType,
            RatingInfo ratingInfo, int? dataPollIntervalInSeconds, Guid externalPlatformId, string description, string logoUrl, IAsyncDocumentSession session, bool isInactive = false)
        {
            try
            {
                await GetPlatformByExternalId(externalPlatformId, session);
                throw new PlatformAlreadyExistsException($"Platform with external id {externalPlatformId} already exists. Name: {name}");
            }
            catch (PlatformDoNotExistException)
            {
                var platform = new Platform(name, externalPlatformId, authMechanism, platformIntegrationType, ratingInfo, description, logoUrl, dataPollIntervalInSeconds, isInactive);
                await session.StoreAsync(platform);
                return platform;
            }
        }

        public async Task<Platform> GetPlatform(string platformId, IAsyncDocumentSession session)
        {
            var existingPlatform = await session.LoadAsync<Platform>(platformId);
            if (existingPlatform == null)
            {
                throw new PlatformDoNotExistException($"Platform with id {platformId} does not exist");
            }

            return existingPlatform;
        }

        public async Task<Platform> GetPlatformByExternalId(Guid externalId, IAsyncDocumentSession session)
        {
            var platformWithName = await session.Query<Platform>().SingleOrDefaultAsync(p => p.ExternalId == externalId);
            if (platformWithName == null)
            {
                throw new PlatformDoNotExistException($"Platform with external id {externalId} does not exist");
            }

            return platformWithName;
        }

        public async Task<IDictionary<string, Platform>> GetPlatforms(IList<string> platformIds, IAsyncDocumentSession session)
        {
            var existingPlatforms = await session.Query<Platform>().Where(p => p.Id.In(platformIds)).ToListAsync();

            var result = new Dictionary<string, Platform>();

            foreach (var platformId in platformIds)
            {
                var platform = existingPlatforms.SingleOrDefault(p => p.Id == platformId);
                result.Add(platformId, platform);
            }

            return result;
        }

        public async Task<IList<Platform>> GetAllPlatforms(IAsyncDocumentSession session)
        {
            return await session.Query<Platform>().Where(p => !p.IsInactive).ToListAsync();
        }
    }
}
;