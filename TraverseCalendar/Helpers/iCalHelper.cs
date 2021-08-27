using Ical.Net;
using Ical.Net.CalendarComponents;
using System.Collections.Generic;
using TraverseCalendar.Models;

namespace TraverseCalendar.Helpers
{
    public static class ICalHelper
    {
        public static List<Event> ConvertICalToEvents(Calendar calendar)
        {
            var result = new List<Event>();

            foreach (CalendarEvent calEvent in calendar.Events)
            {
                result.Add(new Event()
                {
                    DateUTC = calEvent.DtStart.AsUtc,
                    Subject = calEvent.Summary,
                    Uid = calEvent.Uid
                });
            }
            return result;
        }
    }
}
