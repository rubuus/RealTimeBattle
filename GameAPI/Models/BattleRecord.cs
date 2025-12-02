namespace api.Models
{
    public class BattleRecord
    {
        public int Id { get; private set; }

        public int WinnerId { get; private set; }
        public User WinnerUser { get; set; }

        public int LoserId { get; private set; }
        public User LoserUser { get; set; }

        public string Result { get; private set; } = string.Empty;
        public DateTime FinishedTime { get; private set; }

        private BattleRecord() { }

        public static BattleRecord Create(int winner, int loser, string result)
        {
            return new BattleRecord
            {
                WinnerId = winner,
                LoserId = loser,
                Result = result,
                FinishedTime = DateTime.UtcNow
            };
        }
    }
}