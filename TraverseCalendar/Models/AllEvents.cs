using System.Collections.Generic;

namespace TraverseCalendar.Models
{
    public partial class ProcessOrchestrator
    {
        private class AllEvents
        {
            public List<Event> KnownEvents { get; set; } = new List<Event>();
            public List<Event> CurrentEvents { get; set; } = new List<Event>();
        }
    }
}