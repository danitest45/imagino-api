namespace Imagino.Api.Models
{
    public class RequestResult
    {
        public List<string> Errors { get; private set; }
        public bool Success => Errors.Count == 0;
        public dynamic Content { get; set; }

        public RequestResult()
        {
            Errors = [];
            Content = new object();
        }

        public RequestResult(List<string> errors)
        {
            Errors = errors;
            Content = new object();
        }

        public void AddError(string error) => Errors.Add(error);
        public void AddErrors(List<string> errors) => Errors.AddRange(errors);
    }
}
