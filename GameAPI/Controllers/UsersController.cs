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
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDBContext _db;

        public UsersController(ApplicationDBContext db)
        {
            _db = db;
        }

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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req, [FromServices] JwtService jwtService)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.AccountId == req.AccountId);
            if (user == null || !user.VerifyPassword(req.Password) || user.State == "deleted")
                return Unauthorized(new { Message = "Login Failed" });

            // ✅ 토큰 발급
            var token = jwtService.GenerateToken(user);

            return Ok(new LoginResponse
            {
                Message = "로그인 성공",
                UserId = user.Id,
                Nickname = user.Nickname,
                AccessToken = token
            });
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Ok(new { AccountId = accountId });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _db.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            return Ok(new NicknameResponse
            {
                Id = user.Id,
                AccountId = user.AccountId,
                Nickname = user.Nickname
            });
        }

        [Authorize]
        [HttpPost("delete-account")]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest req)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.Id);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            user.ChangeState("deleted");
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Account deleted successfully" });
        }

        [Authorize]
        [HttpPost("change-nickname")]
        public async Task<IActionResult> ChangeNickname([FromBody] ChangeNicknameRequest req)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.Id);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            user.ChangeNickname(req.Nickname);

            await _db.SaveChangesAsync();

            return Ok(new { Message = "change nickname successfully" });
        }
    }
}