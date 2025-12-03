namespace api.DTOs
{
    public class ChangeNicknameRequest
    {
        public string Nickname { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
    }
}