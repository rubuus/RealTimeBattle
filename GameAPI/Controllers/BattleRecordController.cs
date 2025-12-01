using api.Data;
using api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Controllers
{
    [ApiController]
    [Route("battle")]
    public class BattleRecordController : ControllerBase
    {
        private readonly ApplicationDBContext _db;

        public BattleRecordController(ApplicationDBContext db)
        {
            _db = db;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBattleRecord(int id)
        {
            var user = await _db.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            var records = await _db.BattleRecords
            .Where(r => r.MyUserId == id)
            .Select(r => new BattleRecordResponse
            {
                RecordId = r.Id,
                Result = r.Result,
                FinishedTime = r.FinishedTime,

                // EnemyUserId → User 테이블에서 닉네임 조회
                EnemyUserId = r.EnemyUserId,
                EnemyNickname = _db.Users
                    .Where(u => u.Id == r.EnemyUserId)
                    .Select(u => u.Nickname)
                    .FirstOrDefault() ?? "Unknown" // 만약 유저가 Soft Delete 되었거나 없으면
            })
            .OrderByDescending(r => r.FinishedTime)
            .ToListAsync();

            return Ok(records);
        }
    }
}