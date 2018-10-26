using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ACPLogAnalyzer
{
	/// <summary>
	/// The LogReader class reads a log file's text from disk
	/// </summary>
	class LogReader
	{
		private string _path;  // Path/filename of log to be read

	    /// <summary>
	    /// List of strings (one string per line) that make up the file
	    /// </summary>
	    public List<string> LogFileText { get; set; }

	    private LogReader() : this("") {}
		public LogReader(string path)
		{
			_path = path;
		}

		public bool ReadLogFile(string path)
		{
			_path = path;
			return ReadLogFile();
		}

		public bool ReadLogFile()
		{
			// Read log file
			LogFileText = new List<string>();

			if(string.IsNullOrEmpty(_path))
				return false;

			StreamReader sr = null;

			try
			{
                Encoding encoding;
                if(Properties.Settings.Default.Parser_Encoding.Equals("system")) {
                    encoding = Encoding.Default;
                } else {
                    encoding = Encoding.GetEncoding(Properties.Settings.Default.Parser_Encoding); 
                }
				sr = new StreamReader(_path, encoding);
				while(true)
				{
					var line = sr.ReadLine();  // Holds the current line we're reading
					LogFileText.Add(line);
					if(line == null)
						break;
				}
			}
			catch
			{
				return false; // Ignore the error
			}
			finally
			{
				if(sr != null)
					sr.Close();
			}

			return true;
		}
	}
}

