namespace api.DTOs
{
    public class LoginResponse
    {
        public string Message { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public int ProfileImage { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
