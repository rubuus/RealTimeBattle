using System.Security.Cryptography;

namespace api.Services
{
    // 인증 토큰 만료 시, 재발급 토큰
    public class RefreshTokenService
    {
        public string GenerateRefreshToken()
        {
            byte[] bytes = new byte[64]; // 512 bits
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
