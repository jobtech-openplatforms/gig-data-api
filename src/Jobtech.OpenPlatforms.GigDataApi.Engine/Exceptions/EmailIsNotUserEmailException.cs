using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class EmailIsNotUserEmailException: Exception
    {
        public EmailIsNotUserEmailException(string email, Guid userId, string message = null) : base(message)
        {
            Email = email;
            UserId = userId;
        }

        public string Email { get; }
        public Guid UserId { get; }
    }
}
