namespace api.DTOs
{
    public class DeleteRequest
    {
        public string AccountId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}