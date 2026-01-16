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

        // 전적 저장
        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] SaveRecordRequest req)
        {
            var record = BattleRecord.Create(req.WinnerId, req.LoserId);
            _db.BattleRecords.Add(record);
            
            try
            {
                await _db.SaveChangesAsync();
                Console.WriteLine("✅ SaveChanges 성공");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ SaveChanges 실패");
                return StatusCode(500, ex.Message);
            }

            return Ok();
        }


        // 클라이언트에서 전적 요청 시, 3개씩 보내줌
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBattleRecord(int id, int page = 1, int pageSize = 3)
        {
            var user = await _db.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            int skip = (page - 1) * pageSize;

            var records = await _db.BattleRecords
            .Where(r => id == user.Id)
            .OrderByDescending(r => r.FinishedTime)
            .Skip(skip)
            .Take(pageSize)
            .Select(r => new BattleRecordResponse
            {
                Id = r.Id,
                WinnerId = r.WinnerId,
                FinishedTime = r.FinishedTime,

                // User 테이블에서 닉네임 조회
                LoserId = r.LoserId,
                WinnerNickname = _db.Users
                    .Where(u => u.Id == r.WinnerId)
                    .Select(u => u.Nickname)
                    .FirstOrDefault() ?? "Unknown", // 만약 유저가 Soft Delete 되었거나 없으면
                
                LoserNickname = _db.Users
                    .Where(u => u.Id == r.LoserId)
                    .Select(u => u.Nickname)
                    .FirstOrDefault() ?? "Unknown" // 만약 유저가 Soft Delete 되었거나 없으면
            })
            .ToListAsync();

            return Ok(records);
        }
    }
}