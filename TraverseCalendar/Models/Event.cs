using System;

namespace TraverseCalendar.Models
{
    public class Event
    {
        public string Uid { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime DateUTC { get; set; } = DateTime.MinValue;
    }
}
