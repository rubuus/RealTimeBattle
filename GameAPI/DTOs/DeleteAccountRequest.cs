namespace api.DTOs
{
    public class DeleteAccountRequest
    {
        public int Id { get; set; }
        public string AccessToken { get; set; } = string.Empty;
    }
}