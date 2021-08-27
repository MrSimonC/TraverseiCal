using System;

namespace TraverseCalendar.Models
{
    public class Event : IEquatable<Event>
    {
        public string Uid { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime DateUTC { get; set; } = DateTime.MinValue;

        public bool Equals(Event? other)
        {
            if (other is null)
            {
                return false;
            }

            return DateUTC == other.DateUTC
                && Subject == other.Subject
                && Uid == other.Uid;
        }
        public override bool Equals(object? obj) => Equals(obj as Event);
        public override int GetHashCode() => (Uid, Subject, DateUTC).GetHashCode();
    }
}
