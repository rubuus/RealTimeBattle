namespace api.DTOs
{
    public class SaveRecordRequest
    {
        public int WinnerId { get; set; }
        public int LoserId { get; set; } 
        public string Result { get; set; } = string.Empty;
    }
}