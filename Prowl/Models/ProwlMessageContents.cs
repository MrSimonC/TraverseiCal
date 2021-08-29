using Prowl.Enums;

namespace Prowl.Models
{
    public class ProwlMessageContents
    {
        public string Description { get; set; } = string.Empty;
        public Priority Priority { get; set; } = Priority.Normal;
        public string Url { get; set; } = string.Empty;
        public string Application { get; set; } = "Prowl Message";
        public string Event { get; set; } = "Prowl Event";
    }
}
