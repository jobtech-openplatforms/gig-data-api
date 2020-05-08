using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class EmailPromptExpiredException : Exception
    {
        public EmailPromptExpiredException(Guid promptId, string message = null) : base(message)
        {
            PromptId = promptId;
        }

        public Guid PromptId { get; }
    }
}