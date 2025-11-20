namespace api.DTOs
{
    public class RegisterRequest
    {
        public string AccountId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
    }
}
