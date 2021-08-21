using TraverseCalendar.Models;

namespace TraverseCalendar.Entities
{
    public interface IEventsEntity
    {
        List<Event> GetEvents();
        void SetEvents(List<Event> events);
        void AddEvent(Event evnt);
        void RemoveEvent(Event evnt);
    }
}