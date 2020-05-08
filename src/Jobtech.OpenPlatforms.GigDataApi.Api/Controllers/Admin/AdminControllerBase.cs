using System;
using Jobtech.OpenPlatforms.GigDataApi.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers.Admin
{
    public abstract class AdminControllerBase : ControllerBase
    {
        private readonly Options _options;

        protected AdminControllerBase(IOptions<Options> options)
        {
            _options = options.Value;
        }

        protected void ValidateAdminKey(Guid adminKey)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                throw new UnauthorizedAdminCallException("Call was made with unrecognized admin key");
            }
        }
    }
}
