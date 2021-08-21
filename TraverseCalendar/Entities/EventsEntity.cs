using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using System.Threading.Tasks;
using TraverseCalendar.Models;

namespace TraverseCalendar.Entities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class EventsEntity : IEventsEntity
    {
        [JsonProperty("events")]
        public List<Event> Events { get; set; } = new List<Event>();
        public List<Event> GetEvents() => Events;
        public void SetEvents(List<Event> events) => Events = events;
        public void AddEvent(Event evnt) => Events.Add(evnt);
        public void RemoveEvent(Event evnt) => Events.Remove(evnt);

        [FunctionName(nameof(EventsEntity))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<EventsEntity>();
    }
}
