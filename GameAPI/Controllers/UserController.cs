using Microsoft.AspNetCore.Mvc;
using api.Data;
using api.Models;
using api.DTOs;
using api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace api.Controllers
{
    [ApiController]
    [Route("users")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDBContext _db;

        public UserController(ApplicationDBContext db)
        {
            _db = db;
        }

        // 똑같은 AccoutId 있는지 검사 후, 결과 response
        [HttpPost("check-account")]
        public async Task<IActionResult> CheckAccount([FromBody] AccountCheckRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.AccountId))
                return BadRequest(new { Message = "AccountId is Empty" });

            bool exists = await _db.Users.AnyAsync(u => u.AccountId == req.AccountId);

            return Ok(new DuplicateCheckResponse
            {
                IsDuplicate = exists,
                Message = exists ? "Duplicated" : "Success"
            });
        }
        
        // 똑같은 Nickname 있는지 검사 후, 결과 response
        [HttpPost("check-nickname")]
        public async Task<IActionResult> CheckNickname([FromBody] NicknameCheckRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Nickname))
                return BadRequest(new { Message = "Nickname is Empty" });

            bool exists = await _db.Users.AnyAsync(u => u.Nickname == req.Nickname);

            return Ok(new DuplicateCheckResponse {
                IsDuplicate = exists,
                Message = exists ? "Duplicated" : "Success"
            });
        }

        // 회원가입 성공하면 DB 저장 후,클라이언트에 해당 유저 PK response
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var user = new User(req.AccountId, req.Password, req.Nickname);
            _db.Users.Add(user);

            await _db.SaveChangesAsync();

            return Ok(new RegisterResponse
            {
                Message = "회원가입 성공",
                UserId = user.Id
            });
        }

        // 유저 상태 및 비밀번호 식별 후, 해당 유저 데이터 response
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req, [FromServices] AuthService authService)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.AccountId == req.AccountId);
            if (user == null || 
                !user.VerifyPassword(req.Password) || 
                user.State == "deleted")
            {
                return Unauthorized(new { Message = "Login Failed" });
            }
                
            // 토큰 발급
            var tokens = await authService.GenerateTokensAsync(user);

            return Ok(new LoginResponse
            {
                Message = "로그인 성공",
                UserId = user.Id,
                Nickname = user.Nickname,
                ProfileImage = user.ProfileImage,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken
            });
        }

        // 해당 유저 데이터 response
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _db.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            return Ok(new ProfileResponse
            {
                Id = user.Id,
                AccountId = user.AccountId,
                Nickname = user.Nickname,
                ProfileImage = user.ProfileImage
            });
        }

        // 해당 유저 프로필 변경
        [Authorize]
        [HttpPost("profile-image")]
        public async Task<IActionResult> ChangeProfileImage([FromBody] ChangeProfileImageRequest req)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var user = await _db.Users.FindAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            // 현재 이미지와 같은 이미지 클릭 시, DB 쓰기 X
            if (user.ProfileImage == req.ProfileImage)
                return Ok();

            user.ChangeProfileImage(req.ProfileImage);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Change Profile Image successfully" });
        }

        // 닉네임 변경 후, DB 저장
        [Authorize]
        [HttpPost("change-nickname")]
        public async Task<IActionResult> ChangeNickname([FromBody] ChangeNicknameRequest req)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var user = await _db.Users.FindAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            user.ChangeNickname(req.Nickname);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "change nickname successfully" });
        }

        // 계정 삭제 (soft delete)
        [Authorize]
        [HttpPost("delete-account")]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest req)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            var user = await _db.Users.FindAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            if (!user.VerifyPassword(user.ComputeHash(req.Password, user.PasswordSalt)))
                return BadRequest(new { Message = "Different password" } );

            user.ChangeState("deleted");
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Account deleted successfully" });
        }

        // JWT Token에 저장돼있는 userId 가져오기
        private bool TryGetUserId(out int userId)
        {
            userId = 0;

            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
          ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub); 

            return id != null && int.TryParse(id, out userId);
        }
    }
}