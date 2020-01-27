using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base
{
    public interface IEntity
    {
        string Id { get; }
        DateTimeOffset Created { get; }
    }
}
