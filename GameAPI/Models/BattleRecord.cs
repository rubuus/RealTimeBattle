namespace api.Models
{
    public class BattleRecord
    {
        public int Id { get; private set; }

        public int MyUserId { get; private set; }
        public User MyUser { get; set; }

        public int EnemyUserId { get; private set; }

        public string Result { get; private set; } = string.Empty;
        public DateTime FinishedTime { get; private set; }

        private BattleRecord() { }

        public static BattleRecord Create(int myUserId, int enemyUserId, string result)
        {
            return new BattleRecord
            {
                MyUserId = myUserId,
                EnemyUserId = enemyUserId,
                Result = result,
                FinishedTime = DateTime.UtcNow
            };
        }
    }
}