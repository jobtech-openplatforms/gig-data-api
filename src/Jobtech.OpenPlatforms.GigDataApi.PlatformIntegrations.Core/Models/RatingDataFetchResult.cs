using System;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models
{
    public class RatingDataFetchResult
    {
        private RatingDataFetchResult()
        {
            Identifier = Guid.NewGuid();
        }

        public RatingDataFetchResult(decimal value) : this()
        {
            Value = value;
        }

        public Guid Identifier { get; private set; }
        public decimal Value { get; private set; }
    }
}