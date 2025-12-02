using api.Data;
using api.DTOs;
using api.Models;
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

        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] SaveRecordRequest req)
        {
            var record = BattleRecord.Create(req.WinnerId, req.LoserId, req.Result);
            _db.BattleRecords.Add(record);
            try
            {
                await _db.SaveChangesAsync();
                Console.WriteLine("✅ SaveChanges 성공");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ SaveChanges 실패");
                Console.WriteLine(ex.ToString());   // ← 이거 출력되는 거 그대로 나한테 붙여줘
                return StatusCode(500, ex.Message);
            }

            return Ok();
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBattleRecord(int id)
        {
            var user = await _db.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            var records = await _db.BattleRecords
            .Where(r => r.WinnerId == id)
            .Select(r => new BattleRecordResponse
            {
                MyUserId = r.Id,
                Result = r.Result,
                FinishedTime = r.FinishedTime,

                // EnemyUserId → User 테이블에서 닉네임 조회
                EnemyUserId = r.LoserId,
                EnemyNickname = _db.Users
                    .Where(u => u.Id == r.LoserId)
                    .Select(u => u.Nickname)
                    .FirstOrDefault() ?? "Unknown" // 만약 유저가 Soft Delete 되었거나 없으면
            })
            .OrderByDescending(r => r.FinishedTime)
            .ToListAsync();

            return Ok(records);
        }
    }
}