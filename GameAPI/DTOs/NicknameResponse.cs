namespace api.DTOs
{
    public class NicknameResponse
    {
        public int Id { get; set; }
        public string AccountId { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
    }
}