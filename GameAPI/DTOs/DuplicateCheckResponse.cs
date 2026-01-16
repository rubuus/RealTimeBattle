namespace api.DTOs
{
    public class DuplicateCheckResponse
    { 
        public bool IsDuplicate { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}