namespace api.DTOs{
    // 서버 로컬 저장용
    public class AuthTokenResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}