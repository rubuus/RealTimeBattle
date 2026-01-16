using api.Data;
using api.DTOs;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDBContext _db;
        private readonly AuthService _authService;

        public AuthController(ApplicationDBContext db, AuthService authService)
        {
            _db = db;
            _authService = authService;
        }

        // 재발급 토큰 검증
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return BadRequest(new { Message = "RefreshToken is empty" });

            // 기존 Refresh Token 만료 기한 검증
            var refreshToken = await _db.RefreshToken
            .FirstOrDefaultAsync(r =>
                r.Token == req.RefreshToken &&
                r.ExpiresAt > DateTime.UtcNow
            );

            if (refreshToken == null)
                return Unauthorized(new { Message = "Invalid refresh token" });

            // User 조회
            var user = await _db.Users.FindAsync(refreshToken.UserId);
            if (user == null || user.State == "deleted")
                return Unauthorized(new { Message = "User is invalid" });

            // 토큰 재발급
            var tokens = await _authService.GenerateTokensAsync(user);

            return Ok(new RefreshResponse
            {
                AccessToken = tokens.RefreshToken
            });
        }
    }
}