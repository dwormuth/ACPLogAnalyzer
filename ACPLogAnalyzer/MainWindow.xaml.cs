using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;

namespace ACPLogAnalyzer
{
    /// <summary>
    /// The MainWindow class runs the UI and starts news threads to carry out tasks like enumerating a directory
    /// structure to find log files and parsing one or more logs
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _readingLogs;                                          // Flag used to signal to the "get logs" thread that it should stop
        private bool _plottingLogs;                                         // Flag used to signal if we're plotting a graph
        private bool _forceUpdateReport;                                    // Flag used to force the showing of the latest version dlg
        private bool _configRestart;                                        // Hack for restarting the graph config to force line colors to change
        private bool _parsingLogs;                                          // Flag used to signal to the "parse all logs" thread
        private int _nonAcpLogFileCount;                                    // Keeps track of non-ACP logs the user attempted to add
        private int _configRestartTab;                                      // Hack for restarting the graph config dialog
        private string _graphTitle;                                         // The title of the graph to create
        private delegate void ReadLogsInPath(string path);                  // Delegate used to run the "get logs" thread
        private delegate void ParseLogs(string path, bool forGraph);        // Delegate used to run the "parse logs" thread
        private delegate void CheckForUpdate();                             // Delegate used to run the thread which sees if any update is available
        private ReadLogsInPath _readLogsOp;                                 // Instance of the "get logs" delegate
        private ParseLogs _parseLogsOp;                                     // Instance of the "parse logs" delegate
        private CheckForUpdate _checkForUpdateOp;                           // Instance of the delegate
        private AppVersionChecker _appVersionChecker;                       // Object checks if an update is available from the web
        private AppSettings _appSettings;                                   // Object that deals with reading/writing registry settings
        private LogReader _logReader;                                       // Object that deals with reading a log (or any text) from disk
        private Report _report;
        private List<KeyValuePair<object, object>> _grDataSeries1;          // Holds data for Data V Time type graphs
        private List<KeyValuePair<object, object>> _grDataSeries2;          // Holds data for Data V Time type graphs
        private List<KeyValuePair<object, object>> _grDataSeries3;          // Holds data for Data V Time type graphs
        private List<KeyValuePair<GraphType, string>> _graphOptions;        // Holds the list of graph types
        private GraphType _graphType;                                       // Holds the type of graph to create
        
        public bool ParsingLogs
        {
            get { return _parsingLogs; }
            set 
            { 
                _parsingLogs = value;
                if (_report != null) _report.ParsingLogs = value;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            _forceUpdateReport = false;
            _graphType = GraphType.NoSelection;

            // This allows graph child windows to be automatically closed when the main window closes
            Application.Current.MainWindow = this;
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

            InitPaths();
            InitGraphs();
        }

        private void InitPaths()
        {
            // Read ACP-related info from the registry (e.g. likely places for logs)
            _appSettings = new AppSettings();
            _appSettings.ReadRegistry();

            if (!string.IsNullOrEmpty(_appSettings.DocRoot))
                ComboBoxPath.Items.Add(_appSettings.DocRoot);

            if (!string.IsNullOrEmpty(_appSettings.LogDir))
            {
                ComboBoxPath.Items.Add(_appSettings.LogDir);
                ComboBoxPath.SelectedValue = _appSettings.LogDir;
            }

            // Do we have a most recently used search dir?
            if (string.IsNullOrEmpty(Properties.Settings.Default.Most_Recent_Search_Dir)) return;
            if (!IsInList(ComboBoxPath.Items, Properties.Settings.Default.Most_Recent_Search_Dir))
                ComboBoxPath.Items.Insert(0, Properties.Settings.Default.Most_Recent_Search_Dir);
            
            ComboBoxPath.SelectedIndex = 0;
        }

        private void InitGraphs()
        {
            // Init the combo that holds a list of possible graph types...
            _graphOptions = new List<KeyValuePair<GraphType, string>>
            {
                new KeyValuePair<GraphType, string>(GraphType.NoSelection,                  "Graphs for the selected log:"),
                new KeyValuePair<GraphType, string>(GraphType.Fwhm,                         "  FWHM measurements"),
                new KeyValuePair<GraphType, string>(GraphType.Hfd,                          "  HFD measurements"),
                new KeyValuePair<GraphType, string>(GraphType.FwhmvHfd,                     "  FWHM verses HFD"),
                new KeyValuePair<GraphType, string>(GraphType.PointErrorObj,                "  Pointing errors (object slew)"),
                new KeyValuePair<GraphType, string>(GraphType.PointErrorCntr,               "  Pointing errors (center slew)"),
                new KeyValuePair<GraphType, string>(GraphType.GuiderStartUpTime,            "  Guider start-up times"),
                new KeyValuePair<GraphType, string>(GraphType.GuiderSettleTime,             "  Guider settle times"),
                new KeyValuePair<GraphType, string>(GraphType.FilterChangeTime,             "  Filter change times"),
                new KeyValuePair<GraphType, string>(GraphType.PtExpSolveTime,               "  Pointing exp/plate solve times"),
                new KeyValuePair<GraphType, string>(GraphType.SlewTimeTgts,                 "  Slew times (targets)"), 
                new KeyValuePair<GraphType, string>(GraphType.AfTimes,                      "  Auto-focus times"),
                new KeyValuePair<GraphType, string>(GraphType.AllSkyTimes,                  "  All-sky plate solve times"),
                new KeyValuePair<GraphType, string>(GraphType.NoSelection,                  ""), 
                new KeyValuePair<GraphType, string>(GraphType.NoSelection,                  "Graphs for all logs:"),
                new KeyValuePair<GraphType, string>(GraphType.FwhmAvgAllLogs,               "  Avg FWHM measurements"),
                new KeyValuePair<GraphType, string>(GraphType.HfdAvgAllLogs,                "  Avg HFD measurements"),
                new KeyValuePair<GraphType, string>(GraphType.FwhmvHfdAllLogs,              "  Avg FWHM verses Avg HFD"),
                new KeyValuePair<GraphType, string>(GraphType.PointErrorObjAllLogs,         "  Avg pointing error (object slew)"),
                new KeyValuePair<GraphType, string>(GraphType.PointErrorCntrAllLogs,        "  Avg pointing errors (center slew)"),
                new KeyValuePair<GraphType, string>(GraphType.GuiderStartUpTimeAllLogs,     "  Avg guider start-up times"),
                new KeyValuePair<GraphType, string>(GraphType.GuiderSettleTimeLogAllLogs,   "  Avg guider settle times"),
                new KeyValuePair<GraphType, string>(GraphType.FilterChangeTimeAllLogs,      "  Avg filter change times"),
                new KeyValuePair<GraphType, string>(GraphType.PtExpSolveTimeAllLogs,        "  Avg pointing exp/plate solve times"),
                new KeyValuePair<GraphType, string>(GraphType.SlewTimeTgtsAllLogs,          "  Avg slew times (targets)"),
                new KeyValuePair<GraphType, string>(GraphType.AfTimesAllLogs,               "  Avg auto-focus times"),
                new KeyValuePair<GraphType, string>(GraphType.AllSkyTimesAllLogs,           "  Avg all-sky plate solve times")
            };

            ComboBoxGraphs.ItemsSource = _graphOptions;
            ComboBoxGraphs.DisplayMemberPath = "Value";
            ComboBoxGraphs.SelectedValuePath = "Key";
            ComboBoxGraphs.SelectedIndex = 1;
        }

        private void ButtonReadLogsClick(object sender, RoutedEventArgs e)
        {
            if (_readingLogs)
            {
                // Stop the reading process
                ResetUi();
                return;
            }

            ButtonReadLogs.Content = "Stop";
            _readingLogs = true;

            string path;
            if (ComboBoxPath.SelectedValue == null)
            {
                if (string.IsNullOrEmpty(ComboBoxPath.Text))
                {
                    ResetUi();
                    return;
                }
                
                path = ComboBoxPath.Text;
            }
            else
            {
                if (string.IsNullOrEmpty(ComboBoxPath.SelectedValue.ToString()))
                {
                    ResetUi();
                    return;
                }
                
                path = ComboBoxPath.SelectedValue.ToString();
                if (!ComboBoxPath.Items.Contains(path))
                    ComboBoxPath.Items.Add(path);
            }

            _nonAcpLogFileCount = 0;
            _readLogsOp = ReadLogFile;
            _readLogsOp.BeginInvoke(path, ReadLogsComplete, null);
        }

        private void ButtonPathClick(object sender, RoutedEventArgs e)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {

                if (ComboBoxPath.Items.Count > 0 &&
                    ComboBoxPath.SelectedIndex != -1 &&
                    System.IO.Directory.Exists(ComboBoxPath.SelectedValue.ToString()))
                    fbd.SelectedPath = ComboBoxPath.SelectedValue.ToString();

                var result = fbd.ShowDialog();
                if (result != System.Windows.Forms.DialogResult.OK) return;

                ComboBoxPath.Items.Add(fbd.SelectedPath);
                ComboBoxPath.SelectedValue = fbd.SelectedPath;

                Properties.Settings.Default.Most_Recent_Search_Dir = fbd.SelectedPath;
                Properties.Settings.Default.Save();
            }
        }

        private void ButtonLogClick(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();

            if (ComboBoxPath.Items.Count > 0 && 
                ComboBoxPath.SelectedValue != null && 
                System.IO.Directory.Exists(ComboBoxPath.SelectedValue.ToString()))
                ofd.InitialDirectory = ComboBoxPath.SelectedValue.ToString();

            ofd.Multiselect = true;
            ofd.DefaultExt = ".log"; // Default file extension
            ofd.Filter = "Logs (*.log)|*.log|All files (*.*)|*.*"; // Filter files by extension
            var result = ofd.ShowDialog();
            if (result == false)
            {
                return;
            }

            foreach (string filename in ofd.FileNames) {
                // Is this an ACP log file?
                if (!CheckLogIsAcpLog(filename))
                {
                    MessageBox.Show("File \"" + filename + "\" is not a valid ACP log", "Log Not Added");
                    continue;
                }

                if (!IsInList(ListBoxLogs.Items, filename))
                {
                    ListBoxLogs.Items.Add(filename);
                    UpdateLogListCount(ListBoxLogs.Items.Count);
                }
                else
                {
                    MessageBox.Show("File \"" + filename + "\" is already in the list", "Log Not Added");
                }
            }
        }

        private void ListBoxLogsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBoxLogs.SelectedIndex != -1)
                GenerateReport(ListBoxLogs.SelectedValue.ToString());
        }

        private void ButtonReportAllClick(object sender, RoutedEventArgs e)
        {
            if (ListBoxLogs.Items.Count == 0)
            {
                MessageBox.Show("Before generating a report you must add one or more ACP logs to the list.\nYou may either:\n\n1. Click the 'Add All Logs In Path' button to automatically add all logs found\n    in the directory structure indicated by the Path parameter\n2. Add a single log file using the 'Add Single Log' button\n3. Drag files into the logs list using Windows Explorer", "Unable to generate report");
                return;
            }

            GenerateReport(null);
        }

        private void ButtonClearLogListClick(object sender, RoutedEventArgs e)
        {
            ListBoxLogs.Items.Clear();
            UpdateLogListCount(ListBoxLogs.Items.Count);
        }

        private void MenuItemRemoveLogClick(object sender, RoutedEventArgs e)
        {
            if (ListBoxLogs.Items.Count <= 0 || ListBoxLogs.SelectedIndex == -1) return;
            
            ListBoxLogs.Items.RemoveAt(ListBoxLogs.SelectedIndex);
            ListBoxLog.Items.Clear();

            if (ListBoxLogs.Items.Count > 0)
                ListBoxLogs.SelectedIndex = 0;

            UpdateLogListCount(ListBoxLogs.Items.Count);
        }

        private void ButtonConfigReportClick(object sender, RoutedEventArgs e)
        {
            var cfgWnd = new ConfigReport();
            cfgWnd.ShowDialog();
        }

        private void ButtonSaveReportClick(object sender, RoutedEventArgs e)
        {
            if (ListBoxReport.Items.Count == 0)
                return;

            try
            {
                var timestamp = DateTime.Now.ToLongTimeString();
                timestamp = timestamp.Replace(":", "");

                var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) +
                    @"\ACPLogAnalyzer-Report-" + timestamp + ".txt";

                using (var outfile = new StreamWriter(path))
                {
                    foreach (string line in ListBoxReport.Items)
                        outfile.WriteLine(line);
                }

                MessageBox.Show("Report saved on your Desktop as:\n\n" + "ACPLogAnalyzer-Report-" + timestamp + ".txt", "Log Report Saved");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving Report");
            }
        }

        private void ListBoxLogsDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && ((DataObject)e.Data).ContainsFileDropList())
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void ListBoxLogsDrop(object sender, DragEventArgs e)
        {
            _nonAcpLogFileCount = 0;
            if (!(e.Data is DataObject) || !((DataObject) e.Data).ContainsFileDropList()) return;

            foreach (var file in ((DataObject)e.Data).GetFileDropList())
            {
                // Is this a file or a path?
                if (System.IO.Directory.Exists(file))
                {
                    // It's a directory - scan the dir structure for logs (and add the path to the Path combo)
                    ComboBoxPath.Items.Add(file);
                    ComboBoxPath.SelectedValue = file;
                    ButtonReadLogsClick(this, null);
                }
                else if (file.EndsWith(".log"))  // It's a file - make sure it has the right extn
                {
                    // Add the log if it hasn't already been added
                    if (!IsInList(ListBoxLogs.Items, file))
                    {
                        if (CheckLogIsAcpLog(file))
                        {
                            ListBoxLogs.Items.Add(file);
                            UpdateLogListCount(ListBoxLogs.Items.Count);
                        }
                        else
                        {
                            // Silently reject non-ACP logs until the end of the add process
                            _nonAcpLogFileCount++;
                        }
                    }
                }
            }

            if (_nonAcpLogFileCount > 0)
                MessageBox.Show(_nonAcpLogFileCount.ToString(CultureInfo.InvariantCulture) + 
                    " files were not added because they are not valid ACP logs", "File(s) not added");
        }

        private void ButtonHelpClick(object sender, RoutedEventArgs e)
        {
            var hlp = new Help();
            hlp.ShowDialog();
        }

        private void ButtonViewNotepadClick(object sender, RoutedEventArgs e)
        {
            // Open the selected log in the default text editor (normally Notepad.exe)
            if(ListBoxLogs.Items.Count > 0 && ListBoxLogs.SelectedIndex != -1)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ListBoxLogs.SelectedValue.ToString()));
        }

        private void ButtonHelpBrowserClick(object sender, RoutedEventArgs e)
        {
            var uri = new Uri(Properties.Settings.Default.Help_Text_Url);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri));
        }

        private void ListBoxLogMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ButtonViewNotepadClick(this, null);
        }

        private void ListBoxLogsMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ButtonViewNotepadClick(this, null);
        }

        private void ListBoxReportMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Save the report to a temporary file and then view in Notepad
            if (ListBoxReport.Items.Count == 0) return;

            try
            {
                var timestamp = DateTime.Now.ToLongTimeString();
                timestamp = timestamp.Replace(":", "");

                var path = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\Temp\" +
                    @"\ACPLogAnalyzer-Report-" + timestamp + ".txt";

                using (var outfile = new StreamWriter(path))
                {
                    foreach (string line in ListBoxReport.Items)
                        outfile.WriteLine(line);
                }

                // Open in Notepad
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path));
            }
            catch 
            {
                // Deliberate ignore - the double-click to view the report in notepad is 'hidden' feature, 
                // so we keep quiet if it fails
            }
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.Title = GetMainWndTitle();

            // Kick-off a background thread to see if an update is available...
            if (!Properties.Settings.Default.Always_Check_For_Updates) return;
            _checkForUpdateOp = DoCheckForUpdate;
            _checkForUpdateOp.BeginInvoke(CheckForUpdateComplete, null);
        }

        private void ButtonPlotClick(object sender, RoutedEventArgs e)
        {
            if (_plottingLogs) return;
            if (ComboBoxGraphs.SelectedValue == null) return;

            string log = null;
            _plottingLogs = true; 
            
            if (ListBoxLogs.SelectedIndex != -1)
                log = ListBoxLogs.SelectedValue.ToString();  // The selected log (which may or may not be used, depending on the graph type selected)

            var selectedKvp = (KeyValuePair<GraphType, string>)ComboBoxGraphs.SelectedItem;
            ParsingLogs = true;
            _graphType = selectedKvp.Key;
            _graphTitle = selectedKvp.Value;
            PreparePlot(selectedKvp.Key, log);
        }

        private void ButtonUpdateClick(object sender, RoutedEventArgs e)
        {
            if (_appVersionChecker != null) return;  // Already checking 

            // Kick-off a background thread to see if an update is available...
            _forceUpdateReport = true;
            _checkForUpdateOp = DoCheckForUpdate;
            _checkForUpdateOp.BeginInvoke(CheckForUpdateComplete, null);
        }

        private void ButtonConfigPlotClick(object sender, RoutedEventArgs e)
        {
            var cfgWnd = new ConfigPlot {
                RestartConfig = _configRestart,
                RestartTab = _configRestartTab
            };
            cfgWnd.ShowDialog();
            if (cfgWnd.RestartConfig)
            {
                _configRestart = true;
                _configRestartTab = cfgWnd.RestartTab;
                ButtonConfigPlotClick(this, null);
            }
            else
            {
                _configRestart = false;
                _configRestartTab = 0;
            }
        }

        #region Misc methods
        private void ResetUi()
        {
            ButtonReadLogs.Content = "Add All Logs In Path";
            _readingLogs = false;
        }

        private void ResetParsingAllUi()
        {
            ButtonReportAll.Content = "Generate Report On All Logs";
            ButtonPlot.IsEnabled = true;
            ParsingLogs = false;
        }

        private bool IsInList(ItemCollection list, string searchItem)
        {
            // This method is necessary as the standard Contains() method on the listbox doesn't work reliably for some reason
            return list.Cast<string>().Any(s => System.String.Compare(s, searchItem, System.StringComparison.Ordinal) == 0);
        }

        private void DoCheckForUpdate()
        {
            _appVersionChecker = new AppVersionChecker();
            _appVersionChecker.CheckForUpdate();
        }

        private void CheckForUpdateComplete(IAsyncResult result)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                if (_appVersionChecker.IsUpdateAvailable || _forceUpdateReport)
                {
                    var updateWnd = new Update(_appVersionChecker);
                    updateWnd.ShowDialog();
                }
                else
                {
                    // Remove any previous (old) installer if present...
                    _appVersionChecker.RemoveInstaller();
                }

                _appVersionChecker = null;
                _forceUpdateReport = false;
            }));
        }

        private void UpdateLogListCount(int count)
        {
            GroupBoxLogsList.Header = "Logs (" + count.ToString(CultureInfo.InvariantCulture) + ") (drag log files into this list)";
        }

        public void ScrollLogToTime(KeyValuePair<object, object> data, LogEventType logEventType, int lineNumber)
        {
            if (lineNumber == -1)
                return;

            try
            {
                ListBoxLog.ScrollIntoView(ListBoxLog.Items[lineNumber]);
                ListBoxLog.SelectedIndex = lineNumber;
            }
            catch
            {
            }
        }

        private string GetMainWndTitle()
        {
            var sb = new StringBuilder();

            sb.Append(
                "ACP Log Analyzer - Version " +
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Major.ToString(CultureInfo.InvariantCulture) +
                "." +
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString(CultureInfo.InvariantCulture) +
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Build.ToString(CultureInfo.InvariantCulture));

            return sb.ToString();
        }
        #endregion

        /// <summary>
        /// Kick-off the report generating thread. This method can be used to generate for either all reports in 
        /// listBoxLogs (the log param should be set to null or an empty string) or for a particular. In the latter case
        /// the log param should be the log selected in listBoxLogs
        /// </summary>
        /// <param name="log">The path to the log to generate a report on. Alternatively, passing null or an empty
        /// string causes a report to be generated for all items in listBoxLogs
        /// </param>
        private void GenerateReport(string log)
        {
            if (ParsingLogs)
            {
                // Stop the parsing process
                ResetParsingAllUi();
                return;
            }

            ButtonReportAll.Content = "Stop";
            ButtonPlot.IsEnabled = false;
            ParsingLogs = true;

            // Kick-off the parsing process on a new thread...
            _parseLogsOp = DoParseLogs;
            _parseLogsOp.BeginInvoke(log, false, ParseLogsComplete, null);
        }

        /// <summary>
        /// Either parses all logs in listBoxLogs or, if the path param is non-null parses the indicated log 
        /// </summary>
        /// <param name="path">Either null/empty string (parse all logs) or the path to a specific log</param>
        /// <param name="forGraph"></param>
        private void DoParseLogs(string path, bool forGraph)
        {
            _report = new Report(ListBoxLogs.Items.Cast<String>().ToList()) {ParsingLogs = true};

            // Create the report for the log(s) and save to disk. This process will enumerate all the required logs, 
            // compile the stats, and output the report to a temporary text file
            var reportName = _report.CreateReport(path, forGraph);
            if (forGraph) return;  // No further text processing required - the data is now ready for graphing

            if (string.IsNullOrEmpty(reportName))
            {
                ReportError("Error creating report");
                return;
            }

            if(!_logReader.ReadLogFile(reportName))
            {
                ReportError("Error reading report from temporary file " + reportName);
                return;
            } 

            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                ListBoxReport.Items.Clear();
                foreach (var line in _logReader.LogFileText)
                    ListBoxReport.Items.Add(line);

                if (string.IsNullOrEmpty(path))
                    return;  // The report was on all logs - leave the current log selected/displayed

                if (!_logReader.ReadLogFile(path))
                {
                    ReportError("Error reading log contents: " + path);
                    return;
                }

                ListBoxLog.Items.Clear();
                foreach (var line in _logReader.LogFileText)
                    ListBoxLog.Items.Add(line);
            }));
        }

        /// <summary>
        /// Displays an error message box
        /// </summary>
        /// <param name="msg"></param>
        private void ReportError(string msg)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() => MessageBox.Show(msg, "Report Error")));
        }

        /// <summary>
        /// Parse to see if the log is a valid ACP log
        /// </summary>
        /// <param name="path"></param>
        private bool CheckLogIsAcpLog(string path)
        {
            try
            {
                _logReader = new LogReader(path);
                if (!_logReader.ReadLogFile()) return false;

                // Create a new Log object and give it the text of its underlying log file
                var log = new Log(path, _logReader.LogFileText);

                // Is it an ACP log?
                if (log.ParseValidAcpLog()) return true;
            }
            catch
            {
            }

            return false;
        }

        private void ReadLogFile(string path)
        {
            if (!_readingLogs) return;

            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                // Show progress
                this.Title = path;
            }));

            try
            {
                var di = new System.IO.DirectoryInfo(path);  // Dir to search from
                const string filter = "*.log"; // Search filter

                // Get the files in this directory...
                foreach (var fi in di.GetFiles(filter))
                {
                    var finfo = fi;
                    this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                    {
                        // Add the log if it hasn't already been added
                        if (IsInList(ListBoxLogs.Items, finfo.FullName)) return;
                        if (CheckLogIsAcpLog(finfo.FullName))
                        {
                            ListBoxLogs.Items.Add(finfo.FullName);
                            UpdateLogListCount(ListBoxLogs.Items.Count);
                        }
                        else
                        {
                            // Silently reject non-ACP logs
                            _nonAcpLogFileCount++;
                        }
                    }));
                    
                    // Are we being stopped?
                    if (!_readingLogs) return;
                }

                // Call ourselves recursively to get files in subdirs...
                foreach (var subDir in di.GetDirectories())
                {
                    if (!_readingLogs)
                        return;

                    ReadLogFile(subDir.FullName);
                }
            }
            catch
            {
                // Ignore
            }
        }

        private void ReadLogsComplete(IAsyncResult result)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                ResetUi();
                this.Title = this.Title = GetMainWndTitle();

                if (_nonAcpLogFileCount > 0) MessageBox.Show(_nonAcpLogFileCount.ToString(CultureInfo.InvariantCulture) +
                    " files were not added because they are not valid ACP logs", "File(s) not added");
            }));
        }

        private void ParseLogsComplete(IAsyncResult result)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(ResetParsingAllUi));
        }

        private void PreparePlot(GraphType graphType, string logPath)
        {
            switch (graphType)
            {
                case GraphType.NoSelection:
                    ParsingLogs = false;
                    _plottingLogs = false;
                    return;

                case GraphType.FilterChangeTime:
                case GraphType.Fwhm:
                case GraphType.FwhmvHfd:
                case GraphType.Hfd:
                case GraphType.GuiderSettleTime:
                case GraphType.GuiderStartUpTime:
                case GraphType.PointErrorCntr:
                case GraphType.PointErrorObj:
                case GraphType.PtExpSolveTime:
                case GraphType.SlewTimeTgts:
                case GraphType.AfTimes:
                case GraphType.AllSkyTimes:
                    if (string.IsNullOrEmpty(logPath))
                    {
                        ParsingLogs = false;
                        _plottingLogs = false;
                        return;  // Can't plot properties on a single log as none is selected
                    }

                    // Kick-off the parsing process on a new thread...
                    _parseLogsOp = DoParseLogs;
                    _parseLogsOp.BeginInvoke(logPath, true, PreparePlotsComplete, null);
                    break;

                case GraphType.FilterChangeTimeAllLogs:
                case GraphType.FwhmAvgAllLogs:
                case GraphType.FwhmvHfdAllLogs:
                case GraphType.HfdAvgAllLogs:
                case GraphType.GuiderSettleTimeLogAllLogs:
                case GraphType.GuiderStartUpTimeAllLogs:
                case GraphType.PointErrorCntrAllLogs:
                case GraphType.PointErrorObjAllLogs:
                case GraphType.PtExpSolveTimeAllLogs:
                case GraphType.SlewTimeTgtsAllLogs:
                case GraphType.AfTimesAllLogs:
                case GraphType.AllSkyTimesAllLogs:
                    // Kick-off the parsing process on a new thread...
                    _parseLogsOp = DoParseLogs; 
                    _parseLogsOp.BeginInvoke(null /* null => parse all logs */, true, PreparePlotsComplete, null);
                    break;
            }
        }

        private void PreparePlotsComplete(IAsyncResult result)
        {
            // The log(s) to plot will be in m_logs
            ParsingLogs = false;

            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(Plot));
        }

        private void Plot()
        {
            if (_report == null || _report.Logs == null || _report.Logs.Count == 0)
                return;

            // Create the plot data and graph...
            _grDataSeries1 = new List<KeyValuePair<object, object>>();
            _grDataSeries2 = new List<KeyValuePair<object, object>>();
            _grDataSeries3 = new List<KeyValuePair<object, object>>();

            var graph = new Graph(this, null)
            {
                Title = _graphTitle,
                DataSourceSeries1 = _grDataSeries1,
                DataSourceSeries2 = _grDataSeries2,
                DataSourceSeries3 = _grDataSeries3
            };

            try
            {
                switch (_graphType)
                {
                    case GraphType.FilterChangeTime:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "Filter Change Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Filter Change Time"; // Legend/key
                        graph.GraphUc.LineSeries2.Title = "Average"; // Legend/key
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.FilterChange, false, true, LogEventType.None);
                        break;

                    case GraphType.FilterChangeTimeAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average Filter Change Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Avg Filter Change Time";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.FilterChange, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.Fwhm:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "FWHM";
                        graph.GraphUc.LineSeries.Title = "FWHM";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.Fwhm, false, true, LogEventType.None);
                        break;

                    case GraphType.FwhmAvgAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average FWHM";
                        graph.GraphUc.LineSeries.Title = "Avg FWHM";
                        graph.GraphUc.LineSeries2.Title = "Overall Avg FWHM";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.Fwhm, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.FwhmvHfd:
                        graph.GraphUc.XAxis.Title = "\nFWHM Time";
                        graph.GraphUc.YAxis.Title = "FWHM\\HFD";
                        graph.GraphUc.XAxisTertiary.Title = "HFD Time\n";
                        graph.GraphUc.LineSeries.Title = "FWHM";
                        graph.GraphUc.LineSeries2.Title = "Average FWHM";
                        graph.GraphUc.LineSeries3.Title = "HFD";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.Fwhm, false, true, LogEventType.Hfd);
                        break;

                    case GraphType.FwhmvHfdAllLogs:
                        graph.GraphUc.XAxis.Title = "\nFWHM Date";
                        graph.GraphUc.YAxis.Title = "Average FWHM\\HFD";
                        graph.GraphUc.XAxisTertiary.Title = "HFD Date\n";
                        graph.GraphUc.LineSeries.Title = "Avg FWHM";
                        graph.GraphUc.LineSeries2.Title = "Overall Avg FWHM";
                        graph.GraphUc.LineSeries3.Title = "Avg HFD";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.Fwhm, true, false, LogEventType.Hfd);
                        CalcOverallAverage();
                        break;

                    case GraphType.Hfd:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "HFD";
                        graph.GraphUc.LineSeries.Title = "HFD";
                        graph.GraphUc.LineSeries2.Title = "Average HFD";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.Hfd, false, true, LogEventType.None);
                        break;

                    case GraphType.HfdAvgAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average HFD";
                        graph.GraphUc.LineSeries.Title = "Avg HFD";
                        graph.GraphUc.LineSeries2.Title = "Overall Average HFD";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.Hfd, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.GuiderSettleTime:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "Guider Settle Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Guider Settle Time";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.GuiderSettle, false, true, LogEventType.None);
                        break;

                    case GraphType.GuiderSettleTimeLogAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average Guider Settle Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Avg Guider Settle Time";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.GuiderSettle, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.GuiderStartUpTime:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "Guider Start-Up Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Guider Start-Up Time";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.GuiderStartUp, false, true, LogEventType.None);
                        break;

                    case GraphType.GuiderStartUpTimeAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average Guider Start-Up Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Avg Guider Start-Up Time";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.GuiderStartUp, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.PointErrorCntr:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "Pointing Error (center slew)(arcmins)";
                        graph.GraphUc.LineSeries.Title = "Pointing Error";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.PointingErrorCenterSlew, false, true, LogEventType.None);
                        break;

                    case GraphType.PointErrorCntrAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average Pointing Error (center slew)(arcmins)";
                        graph.GraphUc.LineSeries.Title = "Avg Pointing Error";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.PointingErrorCenterSlew, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.PointErrorObj:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "Pointing Error (object slew)(arcmins)";
                        graph.GraphUc.LineSeries.Title = "Pointing Error";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.PointingErrorObjectSlew, false, true, LogEventType.None);
                        break;

                    case GraphType.PointErrorObjAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average Pointing Error (object slew)(arcmins)";
                        graph.GraphUc.LineSeries.Title = "Avg Pointing Error";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.PointingErrorObjectSlew, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.PtExpSolveTime:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "Pointing Exp/Plate Solve Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Pointing Exp/Plate Solve Time";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.PointingExpAndPlateSolve, false, true, LogEventType.None);
                        break;

                    case GraphType.PtExpSolveTimeAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average Pointing Exp/Plate Solve Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Avg Pnt Exp/Plate Solve Time";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.PointingExpAndPlateSolve, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.AllSkyTimes:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "All-Sky Plate Solve Time (secs)";
                        graph.GraphUc.LineSeries.Title = "All-Sky Plate Solve Time";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.AllSkySolveTime, false, true, LogEventType.None);
                        break;

                    case GraphType.AllSkyTimesAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average All-Sky Plate Solve Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Avg All-Sky Plate Solve Time";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.AllSkySolveTime, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.SlewTimeTgts:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "Slew Time (targets)(secs)";
                        graph.GraphUc.LineSeries.Title = "Slew Time";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.SlewTarget, false, true, LogEventType.None);
                        break;

                    case GraphType.SlewTimeTgtsAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average Slew Time (targets)(secs)";
                        graph.GraphUc.LineSeries.Title = "Average Slew Time";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.SlewTarget, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;

                    case GraphType.AfTimes:
                        graph.GraphUc.XAxis.Title = "\nTime";
                        graph.GraphUc.YAxis.Title = "Auto-Focus Time (secs)";
                        graph.GraphUc.LineSeries.Title = "AF Time";
                        graph.GraphUc.LineSeries2.Title = "Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        PlotLog(graph, _report.Logs[0], LogEventType.AutoFocus, false, true, LogEventType.None);
                        break;

                    case GraphType.AfTimesAllLogs:
                        graph.GraphUc.XAxis.Title = "\nDate";
                        graph.GraphUc.YAxis.Title = "Average Auto-Focus Time (secs)";
                        graph.GraphUc.LineSeries.Title = "Average Auto-Focus Time";
                        graph.GraphUc.LineSeries2.Title = "Overall Average";
                        graph.GraphUc.GraphTitle.Text = _graphTitle;
                        foreach (var lg in _report.Logs)
                            PlotLog(graph, lg, LogEventType.AutoFocus, true, false, LogEventType.None);
                        CalcOverallAverage();
                        break;
                }
            }
            catch
            {
                MessageBox.Show("Data error while plotting graph", "Graph Error");
                _plottingLogs = false;
                return;
            }

            graph.Show();
            _plottingLogs = false;
        }

        private void PlotLog(Graph graph, Log log, LogEventType let, bool showDate, bool showAllEvents, LogEventType letTertiary)
        {
            double tmpAvg = 0;
            var tmpCount = 0;

            graph.GraphUc.LogEventTypeMainSeries = let;
            graph.GraphUc.LogEventTypeTertiarySeries = letTertiary;
            graph.Log = log;

            if (log == null || _grDataSeries1 == null || _grDataSeries2 == null || graph == null)
                return;

            if (showAllEvents)  // showAllEvents == true means plot each event in the log, otherwise just the average for the log
            {
                if (letTertiary == LogEventType.None)
                    graph.GraphUc.ShowThirdSeries(false);

                foreach (var le in log.LogEvents)
                {
                    if (le.EventType == let && le.StartDate != null && le.Data != null)
                    {
                        // Plot all instances of the event...
                        _grDataSeries1.Add(showDate ? new KeyValuePair<object, object>(le.StartDate.Value.ToShortDateString(), le.Data) : 
                            new KeyValuePair<object, object>(le.StartDate.Value.ToLongTimeString(), le.Data));

                        tmpAvg += (double)le.Data;
                        tmpCount++;

                        graph.GraphUc.RefreshData();  // This will add the new plot point 
                    }
                    else if(le.EventType == letTertiary && le.StartDate != null && le.Data != null)
                    {
                        _grDataSeries3.Add(showDate ? new KeyValuePair<object, object>(le.StartDate.Value.ToShortDateString(), le.Data)
                                                : new KeyValuePair<object, object>(le.StartDate.Value.ToLongTimeString(), le.Data));

                        graph.GraphUc.RefreshData();
                    }
                }

                tmpAvg = tmpAvg / tmpCount;
                tmpAvg = Math.Round(tmpAvg, 2);
                foreach (var kvp in _grDataSeries1)
                    _grDataSeries2.Add(new KeyValuePair<object, object>(kvp.Key, tmpAvg));  // Add the average (i.e. we have a series with the same number of datapoints as the main series, but all with the same (avg) value)
            }

            else
            {
                // Just plot the single average value for all the events of the specified type in the log 
                double avgSeries1 = 0;
                double avgCountSeries1 = 0;
                double avgSeries3 = 0;
                double avgCountSeries3 = 0;
                foreach (var le in log.LogEvents)
                {
                    if (le.EventType == let && le.StartDate != null && le.Data != null)
                    {
                        avgCountSeries1++;
                        avgSeries1 += (double)le.Data;
                    }
                    else if (le.EventType == letTertiary && letTertiary != LogEventType.None && le.StartDate != null && le.Data != null)
                    {
                        avgCountSeries3++;
                        avgSeries3 += (double)le.Data;
                    }
                }

                if (avgSeries1 > 0 && avgCountSeries1 > 0)
                {
                    avgSeries1 = avgSeries1 / avgCountSeries1;
                    avgSeries1 = Math.Round(avgSeries1, 2);
                }
                else avgSeries1 = 0;

                if (avgSeries1 > 0)
                {
                    if (log.StartDate != null)
                        _grDataSeries1.Add(showDate ? 
                            new KeyValuePair<object, object>(log.StartDate.Value.ToShortDateString(), avgSeries1) : new KeyValuePair<object, object>(log.StartDate.Value.ToLongTimeString(), avgSeries1));

                    graph.GraphUc.RefreshData();  // This will add the new plot point 
                }

                if (letTertiary != LogEventType.None)
                {
                    if (avgSeries3 > 0 && avgCountSeries3 > 0)
                    {
                        avgSeries3 = avgSeries3 / avgCountSeries3;
                        avgSeries3 = Math.Round(avgSeries3, 2);
                    }
                    else avgSeries3 = 0;

                    if (avgSeries3 > 0)
                    {
                        if (log.StartDate != null)
                            _grDataSeries3.Add(showDate ? new KeyValuePair<object, object>(log.StartDate.Value.ToShortDateString(), avgSeries3) 
                                : new KeyValuePair<object, object>(log.StartDate.Value.ToLongTimeString(), avgSeries3));

                        graph.GraphUc.RefreshData();
                    }
                }
                else graph.GraphUc.ShowThirdSeries(false);
            }
        }

        private void CalcOverallAverage()
        {
            // Overall avg (series 2)...
            double tmpAvg = 0;
            var tmpCount = 0;

            // First, calc the avg of the main series
            foreach (var kvp in _grDataSeries1)
            {
                tmpAvg += (double)kvp.Value;
                tmpCount++;
            }

            tmpAvg = tmpAvg / tmpCount;
            tmpAvg = Math.Round(tmpAvg, 2);

            // Now create the (series 2) datapoints...
            foreach (var kvp in _grDataSeries1)
                _grDataSeries2.Add(new KeyValuePair<object, object>(kvp.Key, tmpAvg));  // Add the average
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            var cfgWnd = new ConfigParser();
            cfgWnd.ShowDialog();
        }
    }
}
