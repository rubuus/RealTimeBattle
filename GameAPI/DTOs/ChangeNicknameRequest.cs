namespace api.DTOs
{
    public class ChangeNicknameRequest
    {
        public int Id { get; set; }
        public string Nickname { get; set; } = string.Empty;
    }
}