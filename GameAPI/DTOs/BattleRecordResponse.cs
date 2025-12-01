namespace api.DTOs
{
    public class BattleRecordResponse
    {
        public int RecordId { get; set; } 
        public string Result { get; set; } = string.Empty;
        public DateTime FinishedTime { get; set; } 
        public int MyUserId { get; set; }
        public int EnemyUserId { get; set; } 
        public string EnemyNickname { get; set; } = string.Empty;
    }
}