namespace api.DTOs
{
    public class BattleRecordResponse
    {
        public int Id { get; set; } 
        public string Result { get; set; } = string.Empty;
        public DateTime FinishedTime { get; set; } 
        public int WinnerId { get; set; }
        public int LoserId { get; set; } 
        public string WinnerNickname { get; set; } = string.Empty;
        public string LoserNickname { get; set; } = string.Empty;
    }
}