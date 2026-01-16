using api.Data;
using api.DTOs;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services
{
    // 인증 서비스 관리
    public class AuthService
    {
        private readonly ApplicationDBContext _db;
        private readonly JwtService _jwtService;
        private readonly RefreshTokenService _refreshTokenService;

        public AuthService(
            ApplicationDBContext db,
            JwtService jwtService,
            RefreshTokenService refreshTokenService)
        {
            _db = db;
            _jwtService = jwtService;
            _refreshTokenService = refreshTokenService;
        }

        // DB에 마지막까지 남아있는 토큰 재발급
        public async Task<AuthTokenResult> GenerateTokensAsync(User user)
        {
            string accessToken = _jwtService.GenerateAccessToken(user);
            string refreshToken = _refreshTokenService.GenerateRefreshToken();

            var old = await _db.RefreshToken
                .FirstOrDefaultAsync(r => r.UserId == user.Id);

            if (old != null)
                _db.RefreshToken.Remove(old);

            _db.RefreshToken.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

            await _db.SaveChangesAsync();

            return new AuthTokenResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }
    }
}
