namespace SysMeter
{
    public class DailySystemRecord
    {
        public string DateString { get; set; }
        public double PeakCpuPercent { get; set; }
        public double PeakRamPercent { get; set; }
        public double PeakDiskPercent { get; set; }
        public double SumCpuPercent { get; set; }
        public double SumRamPercent { get; set; }
        public double SumDiskPercent { get; set; }
        public long SampleCount { get; set; }
        public long TotalRamBytes { get; set; }
        public bool MidnightReported { get; set; }

        public DailySystemRecord()
        {
            DateString = "";
        }

        public double AvgCpuPercent
        {
            get { return SampleCount > 0 ? SumCpuPercent / SampleCount : 0; }
        }

        public double AvgRamPercent
        {
            get { return SampleCount > 0 ? SumRamPercent / SampleCount : 0; }
        }

        public double AvgDiskPercent
        {
            get { return SampleCount > 0 ? SumDiskPercent / SampleCount : 0; }
        }
    }
}
