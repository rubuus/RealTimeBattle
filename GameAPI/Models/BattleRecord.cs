namespace api.Models
{
    // Record 관리 모델
    public class BattleRecord
    {
        public int Id { get; private set; }

        public int WinnerId { get; private set; }
        public User WinnerUser { get; set; }

        public int LoserId { get; private set; }
        public User LoserUser { get; set; }

        public DateTime FinishedTime { get; private set; }

        // 전적 객체 return
        public static BattleRecord Create(int winner, int loser)
        {
            return new BattleRecord
            {
                WinnerId = winner,
                LoserId = loser,
                FinishedTime = DateTime.UtcNow
            };
        }
    }
}