using System;
using System.Collections.Generic;
using System.Linq;

namespace ACPLogAnalyzer
{
    /// <summary>
    /// The Log class encapsulates the properties of an ACP log file, including general info and Targets
    /// </summary>
    public partial class Log
    {
        /// <summary>
        /// The physical path to the log file
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The current target when parsing
        /// </summary>
        public Target CurrentTarget { get; set; }

        /// <summary>
        /// List of events in the log (plate solves, guider failures, etc.)
        /// </summary>
        public List<LogEvent> LogEvents { get; set; }

        /// <summary>
        /// The current log line text converted to lower case
        /// </summary>
        public string LineLower { get; set; }

        /// <summary>
        /// The current log line index
        /// </summary>
        public int LogLineIndex { get; set; }

        /// <summary>
        /// A collection of strings (one per line) that constitute the log
        /// </summary>
        public List<string> LogFileText { get; set; }

        /// <summary>
        /// List of Targets in the log
        /// </summary>
        public List<Target> Targets { get; set; }

        /// <summary>
        /// true if the log is actually a valid ACP log, flse otherwise
        /// </summary>
        public bool IsAcpLog { get; set; }

        /// <summary>
        /// The date/time when the log run was started
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// The date/time when the log was ended
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// The number of targets in the log
        /// </summary>
        public int NumberOfTargets
        {
            get 
            {
                if (Targets != null)
                    return Targets.Count;
                else
                    return 0;
            }
        }

        /// <summary>
        /// List of WAIT times the log contains (e.g. for ACP #WAIT directives)
        /// </summary>
        public List<double> WaitTime
        {
            get { return GetLogList<double>(LogEventType.Wait); }
        }

        /// <summary>
        /// Returns the total WAIT time (in secs) in the log
        /// </summary>
        public double TotalWaitTime
        {
            get 
            {
                return WaitTime.Sum(); 
            }
        }

        public TimeSpan TotalWaitTimeAsTimeSpan
        {
            get
            {
                try
                {
                    return TimeSpan.FromSeconds(TotalWaitTime);
                }
                catch
                {
                }

                return new TimeSpan(0);
            }
        }

        /// <summary>
        /// Returns the total log runtime (start of observing...end). The value returned is in seconds (not ticks)
        /// </summary>
        public double TotalLogRunTime
        {
            get
            {
                try
                {
                    if (EndDate != null && StartDate != null) return (EndDate.Value.Ticks - StartDate.Value.Ticks) / TimeSpan.TicksPerSecond;
                }
                catch
                {
                }

                return 0;
            }
        }

        public TimeSpan TotalLogRunTimeAsTimeSpan
        {
            get
            {
                try
                {
                    return TimeSpan.FromSeconds(TotalLogRunTime);
                }
                catch
                {
                }

                return new TimeSpan(0);
            }
        }

        /// <summary>
        /// The toal number of imaging (not including pointing) exposures
        /// </summary>
        public double TotalExposures
        {
            get
            {
                double totalExposures;
                try
                {
                    totalExposures = Targets.Sum(tgt => tgt.ExposureSummaryList.Sum(expSummary => expSummary.Count));
                }
                catch
                {
                    totalExposures = 0;
                }

                return totalExposures;
            }
        }

        /// <summary>
        /// Returns the sum of all imaging exposure durations for the entire log. The value returned is in seconds (not ticks)
        /// </summary>
        public double TotalImagingTime
        {
            get
            {
                double totalImagingTime;
                try
                {
                    // For each target, and for each ExposureSummary in the target, create a total imaging time value
                    var imagingTime =
                        from target in Targets
                        from exposureSummary in target.ExposureSummaryList
                        select exposureSummary.Count * exposureSummary.Exposure.Duration;  // This gives us a collection of n results, one for each target

                    totalImagingTime = imagingTime.Sum();  // Sum the resuts from each target
                }
                catch
                {
                    totalImagingTime = 0;
                }

                return totalImagingTime;
            }
        }

        public TimeSpan TotalImagingTimeAsTimeSpan
        {
            get
            {
                try
                {
                    return TimeSpan.FromSeconds(TotalImagingTime);
                }
                catch
                {
                }

                return new TimeSpan(0);
            }
        }

        /// <summary>
        /// Time not spent imaging or waiting
        /// </summary>
        public double OtherTime
        {
            get
            {
                try
                {
                    return TotalLogRunTime - (TotalWaitTime + TotalImagingTime);
                }
                catch
                {
                }

                return 0;
            }
        }

        public TimeSpan OtherTimeAsTimeSpan
        {
            get
            {
                try
                {
                    return TimeSpan.FromSeconds(OtherTime);
                }
                catch
                {
                }

                return new TimeSpan(0);
            }
        }

        /// <summary>
        /// Total log runtime minus any waits
        /// </summary>
        public double RunTimeMinusWaits
        {
            get
            {
                return TotalLogRunTime - TotalWaitTime;
            }
        }

        public TimeSpan RunTimeMinusWaitsAsTimeSpan
        {
            get
            {
                try
                {
                    return TimeSpan.FromSeconds(RunTimeMinusWaits);
                }
                catch
                {
                }

                return new TimeSpan(0);
            }
        }

        /// <summary>
        /// Imaging as a percentage of runtime, excluding waits
        /// </summary>
        public double ImagingTimeAsPercentageOfRunTime
        {
            get
            {
                try
                {
                    return Math.Round(100 * (TotalImagingTime / RunTimeMinusWaits), 0);
                }
                catch
                {
                }

                return 0;
            }
        }

        /// <summary>
        /// Assess the consistency and reliability of log summary totals. Log errors and script aborts can cause issues when aggregating totals for all logs
        /// </summary>
        public bool LogTotalsAreConsistent
        {
            get
            {
                if (TotalLogRunTime == 0 ||
                    TotalLogRunTime < 0 ||
                    TotalImagingTime < 0 ||
                    TotalWaitTime < 0 ||
                    OtherTime < 0 ||
                    ImagingTimeAsPercentageOfRunTime > 100 ||
                    TotalLogRunTime < TotalImagingTime)

                    return false;  // Ignore the totals for this log 
                else
                    return true;  // Totals are OK
            }
        }

        public Log() : this("", null) {}
        public Log(string path, List<string> logText)
        {
            EndDate = null;
            StartDate = null;
            Path = path;
            CurrentTarget = null;
            LogFileText = logText;
            LogLineIndex = -1;
            LineLower = null;
            LogEvents = new List<LogEvent>();
        }

        private void InitVars()
        {
            LogLineIndex = -1;
            LineLower = null;
            IsAcpLog = false;
            StartDate = null;
            EndDate = null;
            LogEvents = null;
        }

        /// <summary>
        /// Generic method that returns an event list for the specified event type
        /// </summary>
        /// <typeparam name="T">The type of data stored in the event's Data property (e.g. the list type required)</typeparam>
        /// <param name="logEventType">The event type</param>
        /// <returns>Returns an event list for the specified event type, or null if an exception occurs</returns>
        public List<T> GetLogList<T>(LogEventType logEventType)
        {
            try
            {
                var listQuery =
                    from logEvent in LogEvents
                    where ( logEvent != null &&
                            logEvent.Data != null &&
                            logEvent.EventType == logEventType)
                    select (T)logEvent.Data;

                return listQuery.ToList();
            }
            catch
            {
            }

            return null;
        }
    }
}
