namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class EmailPromptDoesNotExistException: Exception
    {
        public EmailPromptDoesNotExistException(string promptId, string message = null) : base(message)
        {
            PromptId = promptId;
        }

        public string PromptId { get; }
    }
}
