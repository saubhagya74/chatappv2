namespace app.DTO
{
    public class NotificationDto
    {
        public string RequesterName { get; set; }
        public string RequesterId { get; set; }
        public string RequesterPicUrl { get; set; }
        public string RequestToName { get; set; }
        public string RequestToId { get; set; }
        public string RequestToPicUrl { get; set; }
        public DateTime RequestTime { get; set; }
        public string RequestStatus { get; set; }
    }
}
