namespace ProcessWatcherShared
{
    public class ProcessInfoDto
    {
        public int ProcessId { get; set; }
        public string Name { get; set; } = null!;
        public string FullPath { get; set; } = null!;
        public DateTime StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
    }
}
