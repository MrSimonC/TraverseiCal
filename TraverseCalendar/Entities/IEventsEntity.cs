using System.Collections.Generic;
using System.Threading.Tasks;
using TraverseCalendar.Models;

namespace TraverseCalendar.Entities
{
    public interface IEventsEntity
    {
        Task<List<Event>> GetEventsAsync();
        void SetEvents(List<Event> events);
        void AddEvent(Event evnt);
        void RemoveEvent(Event evnt);
    }
}