using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base
{
    public abstract class BaseEntity : IEntity
    {
        protected BaseEntity()
        {
            Created = DateTimeOffset.UtcNow;
        }

        public string Id { get; private set; }
        public DateTimeOffset Created { get; private set; }
    }
}
