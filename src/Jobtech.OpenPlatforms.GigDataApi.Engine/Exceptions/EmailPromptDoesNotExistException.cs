﻿using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class EmailPromptDoesNotExistException: Exception
    {
        public EmailPromptDoesNotExistException(Guid promptId, string message = null) : base(message)
        {
            PromptId = promptId;
        }

        public Guid PromptId { get; }
    }
}
