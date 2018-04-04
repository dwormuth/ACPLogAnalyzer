using System;
using System.Collections.Generic;
using System.Linq;

namespace ACPLogAnalyzer
{
    public partial class Log
    {
        /// <summary>
        /// Enumerate the contents of a log and parse various properties, stats, timings, etc.
        /// </summary>
        /// <returns>Returns true if the log parsed OK, false otherwise</returns>
        public bool ParseLog()
        {
            CurrentTarget = null;
            Targets = new List<Target>();
            IsAcpLog = false;     
            
            // Is this an ACP log?
            if (!ParseValidAcpLog())
            {
                InitVars();
                return false;  // Can't parse, it's not an ACP log
            }

            // Start parsing the log until we reach the EOF or the ending statement
            LogLineIndex = -1;
            foreach (var line in this.LogFileText)
            {
                LogLineIndex++;
                if (string.IsNullOrEmpty(line)) continue;
                
                LineLower = line.ToLower();

                    ParseAutoFocusTime();                          // Auto-focus time (intentional fall-through)
                    ParseAutoFocusCount();                         // Auto-focus count (intentional fall-through)
                    ParseFwhm();                                   // FWHM value (intentional fall-through)
                    ParsePointingError();                          // Pointing error (object slew) (intentional fall-through)
                if (ParseEndOfLog())                    break;     // End of log
                if (ParseComment())                     continue;  // Comment line
                if (ParseRepeat())                      continue;  // Starting target repeat line
                if (ParseLogPreamble())                 continue;  // Start of log preamble
                if (ParseImagingTargetStart())          continue;  // Start of new imaging target
                if (ParseImagingExposure())             continue;  // New imaging exposure
                if (ParseFwhm())                        continue;  // FWHM value
                if (ParseSlewToTargetTime())            continue;  // Slew to target time
                if (ParsePointingErrorCenterSlew())     continue;  // Pointing error (center slew)
                if (ParsePlateSolveCount())             continue;  // Plate solve count
                if (ParsePlateSolveErrorCount())        continue;  // Plate solve error count
                if (ParseAllSkyPlateSolveCount())       continue;  // All-sky plate solve count
                if (ParseAllSkyPlateSolveErrorCount())  continue;  // All-sky plate solve error count
                if (ParseHfd())                         continue;  // HFD value
                if (ParseAutoFocusErrorCount())         continue;  // Auto-focus error count
                if (ParseScriptError())                 continue;  // Script error count
                if (ParseScriptAbort())                 continue;  // Script abort count
                if (ParseGuiderStartUpTime())           continue;  // Guider start-up time
                if (ParseGuiderSettleTime())            continue;  // Guider settle time
                if (ParseFilterChangeTime())            continue;  // Filter change time
                if (ParseWaitTime())                    continue;  // Wait time
                if (ParsePointingExpPlateSolveTime())   continue;  // Pointing exposure/plate solve time
                if (ParseAllSkyPlateSolveTime())        continue;  // All-sky plate solve time
                if (ParseGuiderFailure())               continue;  // Guider failure (with carry on) count
            }

            return IsAcpLog;
        }

        /// <summary>
        /// Parse comment line ("#")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseComment()
        {
            return LineLower.Trim()[0].CompareTo('#') == 0;
        }

        /// <summary>
        /// Parse Starting Target repeat
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseRepeat()
        {
            //if (LineLower.IndexOf("starting target repeat", System.StringComparison.Ordinal) != -1)
            return (LineLower.IndexOf("starting target repeat", System.StringComparison.Ordinal) != -1);
             //       LineLower.Trim()[0].CompareTo('#') == 0;
        }

        /// <summary>
        /// Parse valid ACP log ("acp console log opened")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseValidAcpLog()
        {
            IsAcpLog = false;
            if (LogFileText == null || LogFileText.Count == 0) return false;

            foreach (var line in this.LogFileText)
            {
                var lineLower = line.ToLower();
                if (string.IsNullOrEmpty(lineLower)) continue;

                if (lineLower.IndexOf("acp console log opened", System.StringComparison.Ordinal) == -1) continue;
                
                try
                {
                    IsAcpLog = true;
                    DateTime tmpDate;
                    if (!DateTime.TryParse(lineLower.Substring(lineLower.IndexOf("opened", System.StringComparison.Ordinal) + "opened".Length + 1, "dd-mmm-yyyy hh:mm:ss".Length), out tmpDate))
                        this.StartDate = null;
                    else
                        this.StartDate = tmpDate;

                    break;
                }
                catch
                {
                    this.StartDate = null;
                }
            }

            return IsAcpLog;
        }

        /// <summary>
        /// Parse log start\preamble ("acp console log opened", "this is acp version", "licensed to")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseLogPreamble()
        {
            if (LogFileText == null || LogFileText.Count == 0) return false;

            return LineLower.IndexOf("acp console log opened", System.StringComparison.Ordinal) != -1 ||
                   LineLower.IndexOf("this is acp version", System.StringComparison.Ordinal) != -1 ||
                   LineLower.IndexOf("licensed to", System.StringComparison.Ordinal) != -1;
        }

        /// <summary> 
        /// Parse end of log ("acp console log closed")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseEndOfLog()
        {
            if (LineLower.IndexOf("acp console log closed", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    DateTime tmpDate;
                    if (!DateTime.TryParse(
                        LineLower.Substring(LineLower.IndexOf("closed", System.StringComparison.Ordinal) + "closed".Length + 1,
                        "dd-mmm-yyyy hh:mm:ss".Length), out tmpDate))
                    {
                        this.EndDate = null;
                    }
                    else
                        this.EndDate = tmpDate;
                }
                catch
                {
                    this.EndDate = null;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Parse start of new imaging target ("starting target")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseImagingTargetStart()
        {
            if (LineLower.IndexOf("starting target", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // We found a target
                    var tmpStr = LineLower.Substring(LineLower.IndexOf("target", System.StringComparison.Ordinal) + "target".Length + 1);
                    tmpStr = tmpStr.Substring(0, tmpStr.IndexOf("=", System.StringComparison.Ordinal));
                    CurrentTarget = new Target(tmpStr.Trim(), this);
                    Targets.Add(CurrentTarget);

                    // Add a log event for the new target...
                    var dtOpTime = GetOperationTime(LineLower);
                    LogEvents.Add(new LogEvent(
                        dtOpTime,
                        dtOpTime,
                        LogEventType.Target,
                        LogLineIndex,
                        true,
                        CurrentTarget,
                        CurrentTarget.Name,
                        Path));

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse start of new exposure ("imaging to")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseImagingExposure()
        {
            if (LineLower.IndexOf("imaging to", System.StringComparison.Ordinal) != -1)
            {
                DateTime? dtOpTime;
                var newExposure = FindExposure(LogLineIndex, -1, out dtOpTime);
                if (newExposure != null)
                {
                    // Add a new exposure event
                    LogEvents.Add(new LogEvent(
                        dtOpTime,
                        dtOpTime,
                        LogEventType.Exposure,
                        LogLineIndex,
                        true,
                        CurrentTarget,
                        newExposure,
                        Path));

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Parse FWHM value ("image fwhm is")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseFwhm()
        {
            if (LineLower.IndexOf("image fwhm is", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Changes re work item #129:
                    // Change FWHM measurements to work only on imaging exposures (i.e. exclude pointing FWHM's)
                    // Rule: Scan back until we encounter "imaging to" (this indicates an imaging FWHM)
                    //       If we find "updating pointing" first, we ignore this FWHM (it's a pointing update FWHM)
                    // Error:If we find end of log OR "image fwhm is" before either of the above, ignore the FWHM

                    if (!IsImagingFwhm(GetPreviousLogLineIndex(LogLineIndex), -1)) return false;  // It was a pointing update FWHM - ignore

                    // Get the FWHM...
                    var tmpStr = LineLower.Substring(LineLower.IndexOf("is", System.StringComparison.Ordinal) + "is".Length + 1);
                    tmpStr = tmpStr.Substring(0, tmpStr.IndexOf("arcsec", System.StringComparison.Ordinal));

                    double tmpFwhm;
                    if (double.TryParse(tmpStr, out tmpFwhm))
                    {
                        // Add a log event for the FWHM measurement...
                        var dtStart = GetOperationTime(LineLower);
                        LogEvents.Add(new LogEvent(
                            dtStart,
                            dtStart,
                            LogEventType.Fwhm,
                            LogLineIndex,
                            true,
                            CurrentTarget,
                            tmpFwhm,
                            Path));

                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse auto-focus time ("start slew to autofocus")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public void ParseAutoFocusTime()
        {
            //       -----------------------------------------------------------------------------------------
            // NOTE: When processing (successful or otherwise) of this directive is complete, we need to allow
            //       fall-through to the rest of the parsing rules (for the same line) so that "start to slew"
            //       can be parsed as part of pointing error (target slew) processing
            //       -----------------------------------------------------------------------------------------
            //   
            // The rule for true AF time (versions prior to 1.2 simply used the time reported by FocusMax) includes:
            //    
            //   The time to slew to the AF target
            //   Acquiring the AF star (includes a possible plate solve by FocusMax)
            //   The actual focusing by FM
            //   Re-slew back to the original target
            //   Pointing update/slew
            //    
            //   Begin:      "start slew to autofocus"
            //   End:        "autofocus finished", then find plate solve pointing error value 
            //   Exclusions: "plate solve error!" OR "no matching stars found" OR "solution is suspect" OR "**autofocus failed"
            //   Data:       Time span from Begin to End  

            if (LineLower.IndexOf("start slew to autofocus", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Preceded by "re-slew to target"? (only process if "re-slew to target" is not found prev line)
                    if (GetPreviousLogLine(LogLineIndex).ToLower().IndexOf("re-slew to target", System.StringComparison.Ordinal) == -1)
                    {
                        // Only process the "start slew to autofocus" if it was NOT part of a center slew (part of an on-going AF op)

                        var dtStart = GetOperationTime(LineLower);
                        if (dtStart != null)  // null if we can't find the start time (unlikely)
                        {
                            var endIndex = FindAutoFocusOpEnd(GetNextLogLineIndex(LogLineIndex), -1);   // OK: "autofocus finished"
                            // Err: "**autofocus failed"
                            if (endIndex != -1)  // -1 => AF failed (so we discard the mesurement)
                            {
                                DateTime? dtEnd;
                                int lineNum;
                                FindPointingError(GetNextLogLineIndex(endIndex), -1, out dtEnd, out lineNum);

                                dtEnd = CheckOpStartEndTime(dtStart, dtEnd);

                                if (dtEnd != null)  // null => an error condition (e.g. a plate solve failure) so we ignore the measure
                                {
                                    var opTsTmp = new TimeSpan(dtEnd.Value.Ticks);
                                    var opTimeSpan = opTsTmp.Subtract(new TimeSpan(dtStart.Value.Ticks));

                                    // Add a log event for the AF time...
                                    if (opTimeSpan.TotalSeconds >= 0)
                                    {
                                        LogEvents.Add(new LogEvent(
                                            dtStart,
                                            dtEnd,
                                            LogEventType.AutoFocus,
                                            LogLineIndex,
                                            true,
                                            CurrentTarget,
                                            opTimeSpan.TotalSeconds,
                                            Path));

                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Parse pointing error (object slew) ("start slew to")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public void ParsePointingError()
        {
            // The rule for a pointing error measurement after an object slew (slews to imaging targets, auto-focus targets and
            // return slews from AF targets) is:
            //
            // Begin:      "Start slew to" (or, new with ACP 7, "All-sky solution successful")
            // End:        "Pointing error is" (prior to "(slew complete)")
            // Exclusions: Anytime "Start slew to" is immediately *preceded* by "Re-slew to target"
            //             Rationale is this is a centering slew so you don’t want it
            // Exclusions: Any plate solve errors; "plate solve error!" OR "no matching stars found" OR "solution is suspect"
            // Data:       Next "Pointing error is" in the log

            if (LineLower.IndexOf("start slew to", System.StringComparison.Ordinal) != -1  ||
                LineLower.IndexOf("all-sky solution successful", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // *** Pointing error measurement (object slew) ***
                    // Preceded by "re-slew to target"?
                    if (GetPreviousLogLine(LogLineIndex).ToLower().IndexOf("re-slew to target", System.StringComparison.Ordinal) != -1)
                        return;

                    int peLineNum;
                    DateTime? dtPtErrorTime;
                    var tmpPtErr = FindPointingError(GetNextLogLineIndex(LogLineIndex), -1, out dtPtErrorTime, out peLineNum);

                    // FindPointingError():
                    // OK:       "pointing error is"
                    // Err (-1): "plate solve error!"
                    //           "no matching stars found"
                    //           "solution is suspect"
                    //           "start slew to {Target}" [where {Target} != "autofocus"]
                    //           "re-slew to target"

                    if (tmpPtErr != -1)  // return of negative pointing error signals an error - discard the measurement
                    {
                        // Have we capture this event before as part of an objec center slew event?
                        if (!HasLogEventBeenCaptured(dtPtErrorTime, LogEventType.PointingErrorCenterSlew))
                        {
                            // Add a log event for the measurement...
                            LogEvents.Add(new LogEvent(
                                dtPtErrorTime,
                                dtPtErrorTime,
                                LogEventType.PointingErrorObjectSlew,
                                peLineNum,
                                true,
                                CurrentTarget,
                                tmpPtErr,
                                Path));

                            return;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Parse slew to target time ("start slew to")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseSlewToTargetTime()
        {
            if (LineLower.IndexOf("start slew to", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // *** Slew (to target) time (not centering slews)***

                    // Preceded by "start slew to autofocus"?
                    if (GetPreviousLogLine(LogLineIndex).ToLower().IndexOf("re-slew to target", System.StringComparison.Ordinal) != -1)
                        return false;  // Ignore, it was an center slew

                    // Now get the start/end time for slew
                    var dtStart = GetOperationTime(LineLower);
                    var dtEnd = FindSlewOpEndTime(GetNextLogLineIndex(LogLineIndex), -1);  // OK: "slew complete"
                    // Error: end of log found before "slew complete"

                    dtEnd = CheckOpStartEndTime(dtStart, dtEnd);
                    if (dtEnd == null) return false;

                    var opTsTmp = new TimeSpan(dtEnd.Value.Ticks);
                    if (dtStart != null)
                    {
                        var opTimeSpan = opTsTmp.Subtract(new TimeSpan(dtStart.Value.Ticks));

                        // Add a log event for the slew time...
                        if (opTimeSpan.TotalSeconds >= 0)
                        {
                            LogEvents.Add(new LogEvent(
                                              dtStart,
                                              dtEnd,
                                              LogEventType.SlewTarget,
                                              LogLineIndex,
                                              true,
                                              CurrentTarget,
                                              opTimeSpan.TotalSeconds,
                                              Path));

                            return true;
                        }
                    }
                }
                catch
                {
                }
            }
            return false;
        }

        /// <summary>
        /// Parse pointing error (center slew) ("re-slew to target")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParsePointingErrorCenterSlew()
        {
            // The rule for a pointing error measurement after a center slew is:
            //  
            // Begin:      "Re-slew to target"
            // End:        "(slew complete)"
            // Exclusions: Anytime "Re-slew to target" is immediately followed by "Start slew to autofocus"
            //             Rationale is there is no plate-solve after the re-slew to a focus star
            // Exclusions: Any plate solve errors; "plate solve error!" OR "no matching stars found" OR "solution is suspect"
            // Data:       "Pointing error is".  
            //             I'm pretty sure this will always be the next plate-solve, but for sure it will the next 
            //             "Pointing error is" following "Plate-solve final image".  
            //             Rationale is the only time you measure the quality of the centering slew is in the final 
            //             image plate-solve.
            //             There will be cases where there is a re-slew after a "Plate-solve final image" and others 
            //             where there isn't ("Within max error tolerance, no recenter needed").  
            //             I think the rules already account for both of these situations

            if (LineLower.IndexOf("re-slew to target", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Followed by "Start slew to autofocus"?
                    if (GetNextLogLine(LogLineIndex).ToLower().IndexOf("start slew to autofocus", System.StringComparison.Ordinal) != -1)
                        return false;  // Ignore, it was an object slew

                    DateTime? dtPtErrorTime;
                    int lineNum;
                    var tmpPtErr = GetNextLogLine(LogLineIndex).ToLower().IndexOf("start slew to", System.StringComparison.Ordinal) != -1 ? 
                        FindPointingError(GetNextLogLineIndex(GetNextLogLineIndex(LogLineIndex)), -1, out dtPtErrorTime, out lineNum) : 
                            FindPointingError(GetNextLogLineIndex(LogLineIndex), -1, out dtPtErrorTime, out lineNum);

                    // FindPointingError():
                    // OK:       "pointing error is"
                    // Err (-1): "plate solve error!"
                    //           "no matching stars found"
                    //           "solution is suspect"
                    //           "start slew to {Target}" [where {Target} != "autofocus"]
                    //           "re-slew to target"

                    if (tmpPtErr != -1)  // return of negative pointing error signals an error - discard the measurement
                    {
                        // Add a log event for the measurement...
                        LogEvents.Add(new LogEvent(
                            dtPtErrorTime,
                            dtPtErrorTime,
                            LogEventType.PointingErrorCenterSlew,
                            lineNum,
                            true,
                            CurrentTarget,
                            tmpPtErr,
                            Path));

                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse plate solve count ("solved!")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParsePlateSolveCount()
        {
            if (LineLower.IndexOf("solved!", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the error
                    var dtStart = GetOperationTime(LineLower);
                    if (dtStart == null) return false;  // Can't find the start/end time of the current op

                    LogEvents.Add(new LogEvent(
                        dtStart,
                        dtStart,
                        LogEventType.PlateSolveSuccess,
                        LogLineIndex,
                        true,
                        CurrentTarget,
                        "Plate solve ok",
                        Path));

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse plate solve error count ("plate solve error!", "no matching stars found", "solution is suspect")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParsePlateSolveErrorCount()
        {
            if (LineLower.IndexOf("plate solve error!", System.StringComparison.Ordinal) != -1 ||
                LineLower.IndexOf("no matching stars found", System.StringComparison.Ordinal) != -1 ||
                LineLower.IndexOf("solution is suspect", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the error
                    var dtStart = GetOperationTime(LineLower);
                    if (dtStart == null) return false;  // Can't find the start/end time of the current op

                    LogEvents.Add(new LogEvent(
                        dtStart,
                        dtStart,
                        LogEventType.PlateSolveFail,
                        LogLineIndex,
                        false,
                        CurrentTarget,
                        "Plate solve fail",
                        Path));

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse all-sky plate solve count ("all-sky solution successful")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseAllSkyPlateSolveCount()
        {
            if (LineLower.IndexOf("all-sky solution successful", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the error
                    var dtStart = GetOperationTime(LineLower);
                    if (dtStart == null) return false;  // Can't find the start/end time of the current op

                    LogEvents.Add(new LogEvent(
                        dtStart,
                        dtStart,
                        LogEventType.AllSkySolveSuccess,
                        LogLineIndex,
                        true,
                        CurrentTarget,
                        "All-sky plate solve ok",
                        Path));

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse all-sky plate solve error count ("all-sky solution failed", "all-sky solution was incorrect")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseAllSkyPlateSolveErrorCount()
        {
            if (LineLower.IndexOf("all-sky solution failed", System.StringComparison.Ordinal) != -1 ||
                LineLower.IndexOf("all-sky solution was incorrect", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the error
                    var dtStart = GetOperationTime(LineLower);
                    if (dtStart == null) return false;  // Can't find the start/end time of the current op

                    LogEvents.Add(new LogEvent(
                        dtStart,
                        dtStart,
                        LogEventType.AllSkySolveFail,
                        LogLineIndex,
                        false,
                        CurrentTarget,
                        "All-sky plate solve fail",
                        Path));

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse auto-focus count ("auto-focus successful!")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public void ParseAutoFocusCount()
        {
            if (LineLower.IndexOf("auto-focus successful!", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the error
                    var dtStart = GetOperationTime(LineLower);
                    if (dtStart == null) return;

                    LogEvents.Add(new LogEvent(
                        dtStart,
                        dtStart,
                        LogEventType.AutoFocusSuccess,
                        LogLineIndex,
                        true,
                        CurrentTarget,
                        "AF ok",
                        Path));

                    return;
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Parse auto-focus error count ("**autofocus failed")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseAutoFocusErrorCount()
        {
            if (LineLower.IndexOf("**autofocus failed", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the error
                    var dtStart = GetOperationTime(LineLower);
                    if (dtStart == null) return false;  // Can't find the start/end time of the current op

                    LogEvents.Add(new LogEvent(
                        dtStart,
                        dtStart,
                        LogEventType.AutoFocusFail,
                        LogLineIndex,
                        false,
                        CurrentTarget,
                        "AF fail",
                        Path));

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse HFD value ("HFD =")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseHfd()
        {
            if (LineLower.IndexOf("auto-focus successful!", System.StringComparison.Ordinal) != -1)
            {
                // 01:48:36   FocusMax auto-focus successful!
                // 01:48:36     HFD = 3.26

                // Get the HFD...
                DateTime? dtOpTime;
                var tmpHfd = FindHfdOpEndTime(GetNextLogLineIndex(LogLineIndex), 10, out dtOpTime);
                if (tmpHfd != -1)
                {
                    // Add a log event for the HFD measurement...
                    LogEvents.Add(new LogEvent(
                        dtOpTime,
                        dtOpTime,
                        LogEventType.Hfd,
                        LogLineIndex + 1,
                        true,
                        CurrentTarget,
                        tmpHfd,
                        Path));

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Parse script error count ("script error")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseScriptError()
        {
            if (LineLower.IndexOf("script error", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the error
                    var dtStart = GetOperationTime(LineLower);
                    if (dtStart == null) return false;  // Can't find the start/end time of the current op

                    LogEvents.Add(new LogEvent(
                        dtStart,
                        dtStart,
                        LogEventType.ScriptError,
                        LogLineIndex,
                        false,
                        CurrentTarget,
                        "Script error",
                        Path));

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse script abort count ("script was aborted")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseScriptAbort()
        {
            if (LineLower.IndexOf("script was aborted", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the abort
                    var dtStart = GetOperationTime(LineLower);
                    if (dtStart == null) return false;  // Can't find the start/end time of the current op

                    LogEvents.Add(new LogEvent(
                        dtStart,
                        dtStart,
                        LogEventType.ScriptAbort,
                        LogLineIndex,
                        false,
                        CurrentTarget,
                        "Script abort",
                        Path));

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse guider start-up time ("trying to autoguide")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseGuiderStartUpTime()
        {
            if (LineLower.IndexOf("trying to autoguide", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for guider start-up
                    int lineNum;
                    var dtStart = GetOperationTime(LineLower);
                    var dtEnd = FindGuiderStartUpOpEndTime(GetNextLogLineIndex(LogLineIndex), -1, out lineNum);  // Look for "autoguiding at nnn"
                    
                    dtEnd = CheckOpStartEndTime(dtStart, dtEnd);
                    if (dtEnd == null) return false;

                    var opTsTmp = new TimeSpan(dtEnd.Value.Ticks);
                    if (dtStart != null)
                    {
                        var opTimeSpan = opTsTmp.Subtract(new TimeSpan(dtStart.Value.Ticks));

                        // Add a log event for the guider start-up time...
                        if (opTimeSpan.TotalSeconds >= 0)
                        {
                            LogEvents.Add(new LogEvent(
                                              dtStart,
                                              dtEnd,
                                              LogEventType.GuiderStartUp,
                                              LogLineIndex,
                                              true,
                                              CurrentTarget,
                                              opTimeSpan.TotalSeconds,
                                              Path));

                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse guider settle time ("guider check ok")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseGuiderSettleTime()
        {
            if (LineLower.IndexOf("guider check ok", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for guider settle
                    int lineNum;
                    var dtEnd = GetOperationTime(LineLower);  // Note we have the END time for when the guider settled
                    var dtStart = FindGuiderSettleOpStartTime(GetPreviousLogLineIndex(LogLineIndex), -1, out lineNum);  // Now find the start time (scan back up the log)

                    dtEnd = CheckOpStartEndTime(dtStart, dtEnd);
                    if (dtEnd == null) return false;

                    var opTsTmp = new TimeSpan(dtEnd.Value.Ticks);
                    if (dtStart != null)
                    {
                        var opTimeSpan = opTsTmp.Subtract(new TimeSpan(dtStart.Value.Ticks));

                        // Add a log event for the guider settle time...
                        if (opTimeSpan.TotalSeconds >= 0)  // Note: Changed the test from "> 0" to ">= 0" as sometimes the guider can settle 'instantly'
                        {
                            LogEvents.Add(new LogEvent(
                                              dtStart,
                                              dtEnd,
                                              LogEventType.GuiderSettle,
                                              lineNum,
                                              true,
                                              CurrentTarget,
                                              opTimeSpan.TotalSeconds,
                                              Path));

                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse filter change time ("switching from")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseFilterChangeTime()
        {
            // The rule for filter change time (versions prior to 1.2 were not correctly calulating the time) is:
            //
            // Begin:      "switching from"
            // End:        either the next line or the one after that includes "(taking"
            // Exclusions: If the next line (or one after) includes "(guide star"
            // Data:       Time span from Begin to End 
            // 
            // If not doing a pointing update there's *no way of working out the filter change time*. This is because
            // the change time is included as part of the guider start-up time. For example:
            //  
            // 00:14:29   Switching from Clear to Green filter for imaging
            // 00:14:29   Focus change of -40 steps required
            // 00:14:54   (Guide star SNR=231.5; X=74.2, Y=90.7; Aggressiveness=5)
            //
            // As part of a pointing exposure we can see the actual time taken:
            //
            // 00:10:55   (doing post-flip pointing update...)
            // 00:10:55   Updating pointing...
            // >00:10:55   Switching from Green to Clear filter for pointing exposure
            // 00:10:55   Focus change of 40 steps required  [this line is only present in systems where filters are not parfocal]
            // >00:11:07   (taking 15 sec. exposure, Clear filter, binning = 3)

            if (LineLower.IndexOf("switching from", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for filter change
                    var dtStart = GetOperationTime(LineLower);
                    var dtEnd = FindFilterChangeOpEndTime(GetNextLogLineIndex(LogLineIndex), 5);

                    dtEnd = CheckOpStartEndTime(dtStart, dtEnd);
                    if (dtEnd == null) return false;

                    var opTsTmp = new TimeSpan(dtEnd.Value.Ticks);
                    if (dtStart != null)
                    {
                        var opTimeSpan = opTsTmp.Subtract(new TimeSpan(dtStart.Value.Ticks));

                        // Add a log event for the filter change time...
                        if (opTimeSpan.TotalSeconds >= 0)
                        {
                            LogEvents.Add(new LogEvent(
                                              dtStart,
                                              dtEnd,
                                              LogEventType.FilterChange,
                                              LogLineIndex,
                                              true,
                                              CurrentTarget,
                                              opTimeSpan.TotalSeconds,
                                              Path));

                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse wait time ("wait until")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseWaitTime()
        {
            if (LineLower.IndexOf("wait until", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the wait
                    var dtStart = GetOperationTime(LineLower);
                    var dtEnd = FindWaitOpEndTime(GetNextLogLineIndex(LogLineIndex), -1);

                    dtEnd = CheckOpStartEndTime(dtStart, dtEnd);
                    if (dtEnd == null) return false;

                    var opTsTmp = new TimeSpan(dtEnd.Value.Ticks);
                    if (dtStart != null)
                    {
                        var opTimeSpan = opTsTmp.Subtract(new TimeSpan(dtStart.Value.Ticks));

                        if (opTimeSpan.TotalSeconds >= 0)
                        {
                            LogEvents.Add(new LogEvent(
                                              dtStart,
                                              dtEnd,
                                              LogEventType.Wait,
                                              LogLineIndex,
                                              true,
                                              CurrentTarget,
                                              opTimeSpan.TotalSeconds,
                                              Path));

                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse pointing exposure/plate solve time ("updating pointing")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParsePointingExpPlateSolveTime()
        {
            if (LineLower.IndexOf("updating pointing", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the plate solve
                    var dtStart = GetOperationTime(LineLower);
                    var dtEnd = FindPlateSolveOpEndTime(GetNextLogLineIndex(LogLineIndex), -1);  // Returns null if the op failed
                    
                    dtEnd = CheckOpStartEndTime(dtStart, dtEnd);
                    if (dtEnd == null) return false;

                    var opTsTmp = new TimeSpan(dtEnd.Value.Ticks);
                    if (dtStart != null)
                    {
                        var opTimeSpan = opTsTmp.Subtract(new TimeSpan(dtStart.Value.Ticks));

                        // Add a log event...
                        if (opTimeSpan.TotalSeconds >= 0)
                        {
                            LogEvents.Add(new LogEvent(
                                              dtStart,
                                              dtEnd,
                                              LogEventType.PointingExpAndPlateSolve,
                                              LogLineIndex,
                                              true,
                                              CurrentTarget,
                                              opTimeSpan.TotalSeconds,
                                              Path));

                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Parse all-sky plate solve time ("attempting all-sky plate solution")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseAllSkyPlateSolveTime()
        {
            if (LineLower.IndexOf("attempting all-sky plate solution", System.StringComparison.Ordinal) != -1)
            {
                try
                {
                    // Get the start/end time for the all-sky plate solve
                    var dtStart = GetOperationTime(LineLower);
                    var dtEnd = FindAllSkyPlateSolveOpEndTime(GetNextLogLineIndex(LogLineIndex), -1);  // Returns null if the op failed

                    dtEnd = CheckOpStartEndTime(dtStart, dtEnd);
                    if (dtEnd == null) return false;

                    var opTsTmp = new TimeSpan(dtEnd.Value.Ticks);
                    if (dtStart != null)
                    {
                        var opTimeSpan = opTsTmp.Subtract(new TimeSpan(dtStart.Value.Ticks));

                        // Add a log event...
                        if (opTimeSpan.TotalSeconds >= 0)
                        {
                            LogEvents.Add(new LogEvent(
                                              dtStart,
                                              dtEnd,
                                              LogEventType.AllSkySolveTime,
                                              LogLineIndex,
                                              true,
                                              CurrentTarget,
                                              opTimeSpan.TotalSeconds,
                                              Path));

                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Guider failure ("**autoguiding failed", "excessive guiding errors", "guider stopped or lost star")
        /// </summary>
        /// <returns>Returns true if the event was successfully parsed, false otherwise</returns>
        public bool ParseGuiderFailure()
        {
            if (LineLower.IndexOf("**autoguiding failed", System.StringComparison.Ordinal) != -1 ||
                LineLower.IndexOf("excessive guiding errors", System.StringComparison.Ordinal) != -1 ||
                LineLower.IndexOf("guider stopped or lost star", System.StringComparison.Ordinal) != -1)
            {
                // We found a guiding failure - see if ACP carried on imaging anyway (this is the condition 
                // [failure then continue] that we count)
                if (FindGuiderFailureRecovery(GetNextLogLineIndex(LogLineIndex), 5))
                {
                    var dtStart = GetOperationTime(LineLower);
                    LogEvents.Add(new LogEvent(
                            dtStart,
                            dtStart,
                            LogEventType.GuiderFail,
                            LogLineIndex,
                            false,
                            CurrentTarget,
                            true,  // true signals that imaging continued unguided
                            Path));

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the line that logically immediately precedes the line indicated by lineIndex (the current line)
        /// This method ignores any comment lines
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <returns>Returns the line that logically immediately precedes the line indicated by lineIndex (the current line), or
        /// an empty string if no suitable line is found</returns>
        private string GetPreviousLogLine(int lineIndex)
        {
            try
            {
                for (var tmpLineIndex = lineIndex - 1 /* Start BEFORE current line */; tmpLineIndex > 0; tmpLineIndex--)
                {
                    if (this.LogFileText[tmpLineIndex][0].CompareTo('#') != 0)
                        return this.LogFileText[tmpLineIndex];
                }
            }
            catch
            {
            }
            return "";  // Start of log found before suitable line
        }

        /// <summary>
        /// Returns the line that logically immediately follows the line indicated by lineIndex (the current line)
        /// This method ignores any comment lines
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <returns>Returns the line that logically immediately follows the line indicated by lineIndex (the current line), or
        /// an empty string if no suitable line is found</returns>
        private string GetNextLogLine(int lineIndex)
        {
            try
            {
                for (var tmpLineIndex = lineIndex + 1 /* Start AFTER the current line */; tmpLineIndex < LogFileText.Count; tmpLineIndex++)
                {
                    if (this.LogFileText[tmpLineIndex][0].CompareTo('#') != 0)
                        return this.LogFileText[tmpLineIndex];
                }
            }
            catch
            {
            }
            return "";  // End of log found before suitable line
        }

        /// <summary>
        /// Returns the index of the line that logically immediately precedes the line indicated by lineIndex (the current line)
        /// This method ignores (skips over) any comment lines
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <returns>Returns the index of the line that logically immediately precedes the line indicated by lineIndex (the current line),
        /// or 0 if no suitable line is found</returns>
        private int GetPreviousLogLineIndex(int lineIndex)
        {
            try
            {
                for (var tmpLineIndex = lineIndex - 1 /* Start BEFORE current line */; tmpLineIndex > 0; tmpLineIndex--)
                {
                    if (this.LogFileText[tmpLineIndex][0].CompareTo('#') != 0)
                        return tmpLineIndex;
                }
            }
            catch
            {
            }
            return 0;  // Start of log found before suitable line
        }

        /// <summary>
        /// Returns the index of the line that logically immediately follows the line indicated by lineIndex (the current line)
        /// This method ignores (skips over) any comment lines
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <returns>Returns the index of the line that logically immediately follows the line indicated by lineIndex (the current line),
        /// or the highlest line number in the log if no suitable line is found</returns>
        private int GetNextLogLineIndex(int lineIndex)
        {
            try
            {
                for (var tmpLineIndex = lineIndex + 1 /* Start AFTER the current line */; tmpLineIndex < LogFileText.Count; tmpLineIndex++)
                {
                    if (this.LogFileText[tmpLineIndex][0].CompareTo('#') != 0)
                        return tmpLineIndex;
                }
            }
            catch
            {
            }
            return LogFileText.Count;  // End of log found before suitable line
        }

        /// <summary>
        /// Returns true if the current line is a comment line (starts with '#')
        /// </summary>
        /// <param name="lineIndex">Log line index</param>
        /// <returns>Returns true if the current line is a comment line (starts with '#'), false otherwise</returns>
        private bool IsCommentLine(int lineIndex)
        {
            return this.LogFileText[lineIndex][0].CompareTo('#') == 0;
        }

        /// <summary>
        /// Scan up the log until we encounter "imaging to" (this indicates an imaging FWHM)
        /// If we find "updating pointing" first, we return false (it's a pointing update FWHM)
        /// If we find start-of-log OR "image fwhm is" before either of the above, ignore the FWHM (returns false)
        /// </summary>
        /// <param name="lineIndex">Log line index</param>
        /// <param name="maxLinesToScan">Maximum number of lines to scan before giving up</param>
        /// <returns>Returns true if the FWHM is part of an imaging exposure, false otherwise</returns>
        private bool IsImagingFwhm(int lineIndex, int maxLinesToScan)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1)
                    maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex > 0 && lineScanCount < maxLinesToScan; tmpLineIndex--)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("imaging to", System.StringComparison.Ordinal) != -1)
                        return true;
                    
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("updating pointing", System.StringComparison.Ordinal) != -1 ||
                        (this.LogFileText[tmpLineIndex].ToLower().IndexOf("image fwhm is", System.StringComparison.Ordinal) != -1))
                        return false;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Start scanning down the log until we discover an imaging exposure
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <param name="opDateTime"></param>
        /// <returns>Returns a valid Exposure object if the exposure is found, null otherwise</returns>
        private Exposure FindExposure(int lineIndex, int maxLinesToScan, out DateTime? opDateTime)
        {
            var lineScanCount = 0;
            if (maxLinesToScan == -1)
                maxLinesToScan = int.MaxValue;  // Unlimited

            try
            {
                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    var lineLower = this.LogFileText[tmpLineIndex].ToLower();

                    if (lineLower.IndexOf("taking", System.StringComparison.Ordinal) == -1) continue;

                    // The line here will take the format:
                    // (taking {duration} sec. exposure, {filterName} filter, binning = {bin})
                    var tmpExposure = new Exposure();

                    // Get the exposure duration...
                    var tmpStr = lineLower.Substring(lineLower.IndexOf("taking", System.StringComparison.Ordinal) + "taking".Length + 1);
                    tmpStr = tmpStr.Substring(0, tmpStr.IndexOf("s", System.StringComparison.Ordinal));
                    double tmpDuration;
                    tmpExposure.Duration = !double.TryParse(tmpStr, out tmpDuration) ? 0 : tmpDuration;

                    // Get the exposure filter name...
                    tmpStr = lineLower.Substring(lineLower.IndexOf("exposure", System.StringComparison.Ordinal) + "exposure".Length + 1);
                    tmpStr = tmpStr.Substring(0, tmpStr.IndexOf("filter", System.StringComparison.Ordinal));
                    tmpExposure.Filter = tmpStr.Trim();

                    // Get the binning value...
                    tmpStr = lineLower.Substring(lineLower.IndexOf("binning", System.StringComparison.Ordinal) + "binning".Length + 1);
                    tmpStr = tmpStr.Substring(0, tmpStr.IndexOf(")", System.StringComparison.Ordinal));
                    tmpStr = tmpStr.Substring(tmpStr.IndexOf("=", System.StringComparison.Ordinal) + 1);

                    int tmpBin;
                    tmpExposure.Bin = !int.TryParse(tmpStr, out tmpBin) ? 0 : tmpBin;

                    opDateTime = GetOperationTime(lineLower);
                    return tmpExposure;
                }
            }
            catch
            {
            }

            opDateTime = null;
            return null;
        }

        /// <summary>
        /// Start scanning down the log until we find the next pointing error.
        /// If we encounter any of the following, an error is returned and the pointing error measurement should be discarded:
        /// "plate solve error!" OR "no matching stars found" OR "solution is suspect" OR "start slew to" OR "re-slew to target"
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <param name="opDateTime">A nullable DateTime that will contain the time of the pointing error</param>
        /// <param name="lineNum"></param>
        /// <returns>Returns the pointing error value, -1 otherwise (or an error was encountered)</returns>
        private double FindPointingError(int lineIndex, int maxLinesToScan, out DateTime? opDateTime, out int lineNum)
        {
            var lineScanCount = 0;
            if (maxLinesToScan == -1)
                maxLinesToScan = int.MaxValue;  // Unlimited

            try
            {
                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    var tmpLine = this.LogFileText[tmpLineIndex].ToLower();
                    if (tmpLine.IndexOf("plate solve error!", System.StringComparison.Ordinal) != -1 ||
                        tmpLine.IndexOf("no matching stars found", System.StringComparison.Ordinal) != -1 ||
                        tmpLine.IndexOf("solution is suspect", System.StringComparison.Ordinal) != -1 ||
                        tmpLine.IndexOf("re-slew to target", System.StringComparison.Ordinal) != -1)
                    {
                        // Finding any of the above before we find the point error value is an error condition
                        break;
                    }
                    
                    if (tmpLine.IndexOf("start slew to", System.StringComparison.Ordinal) != -1)
                    {
                        if (tmpLine.IndexOf("start slew to autofocus", System.StringComparison.Ordinal) != -1)
                            continue;  // That's OK - continue scanning for the the pointing error measurement
                        
                        break;  // Slew to a target other than AF is an error at this point
                    }

                    if (tmpLine.IndexOf("pointing error is", System.StringComparison.Ordinal) == -1) continue;
                    
                    // Get the pointing error...
                    var tmpStr = tmpLine.Substring(tmpLine.IndexOf("is", System.StringComparison.Ordinal) + "is".Length + 1);
                    tmpStr = tmpStr.Substring(0, tmpStr.IndexOf("arcmin", System.StringComparison.Ordinal));

                    double tmpPtErr;
                    if (!double.TryParse(tmpStr, out tmpPtErr)) continue;

                    opDateTime = GetOperationTime(tmpLine);
                    lineNum = tmpLineIndex;
                    return tmpPtErr;
                }
            }
            catch
            {
            }

            opDateTime = null;
            lineNum = -1;
            return -1;
        }

        /// <summary>
        /// Start scanning down the log until we find the next pointing error.
        /// If we encounter any of the following, an error is returned and the pointing error measurement should be discarded:
        /// "plate solve error!" OR "no matching stars found" OR "solution is suspect" OR "start slew to" OR "re-slew to target"
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <returns>Returns the pointing error value, -1 otherwise (or an error was encountered)</returns>
        private double FindPointingError(int lineIndex, int maxLinesToScan)
        {
            DateTime? tmpDate;
            int lineNum;
            return FindPointingError(lineIndex, maxLinesToScan, out tmpDate, out lineNum);
        }

        /// <summary>
        /// Start scanning down the log until we discover the time when AF has completed
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>/// 
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <returns>Returns the index of the the ending statement</returns>
        private int FindAutoFocusOpEnd(int lineIndex, int maxLinesToScan)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1)
                    maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("**autofocus failed", System.StringComparison.Ordinal) != -1)
                        return -1;  // AF failed
                    
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("autofocus finished", System.StringComparison.Ordinal) != -1)
                        return tmpLineIndex;
                }
            }
            catch
            {
            }
            return -1;  // Error - couldn't find the end of AF
        }

        /// <summary>
        /// Start scanning up the log until we discover the start of guider settle time
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <param name="lineNum"></param>
        /// <returns>Return the DateTime of the event</returns>
        private DateTime? FindGuiderSettleOpStartTime(int lineIndex, int maxLinesToScan, out int lineNum)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex > 0 && lineScanCount < maxLinesToScan; tmpLineIndex--)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("imaging to", System.StringComparison.Ordinal) == -1) continue;

                    lineNum = tmpLineIndex;
                    return GetOperationTime(this.LogFileText[tmpLineIndex]);
                }
            }
            catch
            {
            }

            lineNum = -1;
            return null;
        }

        /// <summary>
        /// Start scanning down the log until we when the filter change completed
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <returns>Return the DateTime of the event</returns>
        private DateTime? FindFilterChangeOpEndTime(int lineIndex, int maxLinesToScan)
        {
            // End:        "(taking"
            // Exclusions: "(guide star"
            // Data:       Time span from Begin to End 
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("(taking", System.StringComparison.Ordinal) != -1)
                        return GetOperationTime(this.LogFileText[tmpLineIndex]);
                    
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("(guide star", System.StringComparison.Ordinal) != -1)
                        return null;
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Start scanning down the log until we discover the time when the autoguider start-up has completed
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <param name="lineNum"></param>
        /// <returns>Return the DateTime of the event</returns>
        private DateTime? FindGuiderStartUpOpEndTime(int lineIndex, int maxLinesToScan, out int lineNum)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower() .IndexOf("autoguiding at", System.StringComparison.Ordinal) == -1) continue;

                    lineNum = tmpLineIndex;
                    return GetOperationTime(this.LogFileText[tmpLineIndex]);
                }
            }
            catch
            {
            }

            lineNum = -1;
            return null;
        }

        /// <summary>
        /// Start scanning down the log until we discover the time when the slew has completed
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <returns>Return the DateTime of the event</returns>
        private DateTime? FindSlewOpEndTime(int lineIndex, int maxLinesToScan)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("slew complete", System.StringComparison.Ordinal) != -1)
                        return GetOperationTime(this.LogFileText[tmpLineIndex]);
                    
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("updating pointing", System.StringComparison.Ordinal) != -1 ||
                        this.LogFileText[tmpLineIndex].ToLower().IndexOf("re-slew to target", System.StringComparison.Ordinal) != -1 ||
                        this.LogFileText[tmpLineIndex].ToLower().IndexOf("start slew to", System.StringComparison.Ordinal) != -1)
                        return null;
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Start scanning down the log until we discover the time when the wait has completed
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <returns>Return the DateTime of the event</returns>
        private DateTime? FindWaitOpEndTime(int lineIndex, int maxLinesToScan)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("wait finished", System.StringComparison.Ordinal) != -1)
                        return GetOperationTime(this.LogFileText[tmpLineIndex]);
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Start scanning down the log until we discover the time when the plate solve failed/succeeded
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <returns>Returns the datetime for the end of the op, or null if the time could not be determined (or the op failed)</returns>
        private DateTime? FindPlateSolveOpEndTime(int lineIndex, int maxLinesToScan)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("target is now centered", System.StringComparison.Ordinal) != -1)
                        return GetOperationTime(this.LogFileText[tmpLineIndex]);
                    
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("**aiming failed", System.StringComparison.Ordinal) != -1)
                        return null;  // Tell the caller the op failed
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Start scanning down the log until we discover the time when the all-sky plate solve failed/succeeded
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <returns>Returns the datetime for the end of the op, or null if the time could not be determined (or the op failed)</returns>
        private DateTime? FindAllSkyPlateSolveOpEndTime(int lineIndex, int maxLinesToScan)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("all-sky solution successful", System.StringComparison.Ordinal) != -1)
                        return GetOperationTime(this.LogFileText[tmpLineIndex]);

                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("all-sky solution was incorrect", System.StringComparison.Ordinal) != -1 ||
                        this.LogFileText[tmpLineIndex].ToLower().IndexOf("all-sky solution failed", System.StringComparison.Ordinal) != -1)
                        return null;  // Tell the caller the op failed
                }
            }
            catch
            {
            }
            return null;  // Couldn't find the end of the op
        }

        /// <summary>
        /// Start scanning down the log until we discover the time when ACP reports the HFD from a successful AF run
        /// </summary>
        /// <param name="lineIndex">Log line index to start searching from</param>
        /// <param name="maxLinesToScan">Number of lines to scan before reporting an error</param>
        /// <param name="opDateTime"></param>
        /// <returns>Returns the datetime for the end of the op, or null if the time could not be determined (or the op failed)</returns>
        private double FindHfdOpEndTime(int lineIndex, int maxLinesToScan, out DateTime? opDateTime)
        {
            opDateTime = null;

            try
            {
                // Example:
                //
                // 01:48:36   FocusMax auto-focus successful!
                // 01:48:36     HFD = 3.26
                //

                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    var tmpStr = this.LogFileText[tmpLineIndex].ToLower();
                    if (tmpStr.IndexOf("hfd =", System.StringComparison.Ordinal) != -1)
                    {
                        tmpStr = tmpStr.Substring(tmpStr.IndexOf("=", System.StringComparison.Ordinal) + 1);
                        double tmpHfd;
                        if (double.TryParse(tmpStr, out tmpHfd))
                        {
                            opDateTime = GetOperationTime(this.LogFileText[tmpLineIndex]);
                            return tmpHfd;
                        }
                    }
                    else if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("auto-focus successful!", System.StringComparison.Ordinal) != -1)
                        return -1;  // Tell the caller the op failed
                }
            }
            catch
            {
            }
            return -1;
        }

        /// <summary>
        /// Start scanning down the log until we discover if ACP recovered from a guiding failure
        /// </summary>
        /// <param name="lineIndex">The index of the line from which we start scanning</param>
        /// <param name="maxLinesToScan">The max number of lines to look for the op end condition before giving up</param>
        /// <returns>Returns true if ACP carried on imaging unguided after a guider failure, or otherwise</returns>
        private bool FindGuiderFailureRecovery(int lineIndex, int maxLinesToScan)
        {
            try
            {
                var lineScanCount = 0;
                if (maxLinesToScan == -1) maxLinesToScan = int.MaxValue;  // Unlimited

                for (var tmpLineIndex = lineIndex; tmpLineIndex < LogFileText.Count && lineScanCount < maxLinesToScan; tmpLineIndex++)
                {
                    if (IsCommentLine(tmpLineIndex)) continue;

                    lineScanCount++;
                    if (this.LogFileText[tmpLineIndex].ToLower().IndexOf("will try image again, this time unguided", System.StringComparison.Ordinal) != -1 ||
                        this.LogFileText[tmpLineIndex].ToLower().IndexOf("**guiding failed, continuing unguided", System.StringComparison.Ordinal) != -1)
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Extract the DateTime from a string. Assumes the datetime stamp starts at position zero in the string
        /// </summary>
        /// <param name="text">The string containing the datetime</param>
        /// <returns>A nullable DateTime object containing the datetime of the operation (which could be null)</returns>
        public DateTime? GetOperationTime(string text)
        {
            DateTime? dt = null;
            try
            {
                var tmpStr = text.Trim().Substring(0, "xx:xx:xx".Length);
                var hrs = int.Parse(tmpStr.Substring(0, 2));
                var mins = int.Parse(tmpStr.Substring(3, 2));
                var secs = int.Parse(tmpStr.Substring(6, 2));

                dt = this.StartDate != null ? new DateTime(this.StartDate.Value.Year, this.StartDate.Value.Month, this.StartDate.Value.Day, hrs, mins, secs, 0) : new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, hrs, mins, secs, 0);
            }
            catch
            {
            }

            return dt;
        }

        /// <summary>
        /// Extract the DateTime (as a string) from a string. Assumes the datetime stamp starts at position zero in the string
        /// </summary>
        /// <param name="text">The string containing the datetime</param>
        /// <returns>A string representation of a DateTime object containing the datetime of the operation (which could be null)</returns>
        public string GetOperationTimeText(string text)
        {
            try
            {
                return text.Trim().Substring(0, "xx:xx:xx".Length);
            }
            catch
            {
            }

            return "";
        }

        /// <summary>
        /// Scans the list of log events to see if the event has previously been recorded
        /// </summary>
        /// <param name="startDate">The date of the event</param>
        /// <param name="eventType">The type of event</param>
        /// <returns></returns>
        private bool HasLogEventBeenCaptured(DateTime? startDate, LogEventType eventType)
        {
            if (startDate == null)
                return false;

            var queryResults =
                from le in this.LogEvents
                where (le.EventType == eventType && 
                       le.StartDate != null && 
                       le.StartDate.Value.CompareTo(startDate.Value) == 0)
                select le;

            return queryResults.Any();
        }

        private DateTime? CheckOpStartEndTime(DateTime? startDate, DateTime? endDate)
        {
            if (startDate == null || endDate == null) return null;

            if (endDate < startDate) 
                endDate = endDate.Value.AddDays(1);  // Can happen in a log when the time flips over to 00:00:00 at midnight

            return endDate;
        }
    }
}
