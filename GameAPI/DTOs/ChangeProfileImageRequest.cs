namespace api.DTOs
{
    public class ChangeProfileImageRequest
    {
        public int ProfileImage { get; set; }
        public string AccessToken { get; set; } = string.Empty;
    }
}