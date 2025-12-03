namespace api.DTOs
{
    public class ProfileResponse
    {
        public int Id { get; set; }
        public string AccountId { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public int ProfileImage { get; set; }
    }
}