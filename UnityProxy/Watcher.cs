namespace UnityProxy
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;

    /// <summary>
    ///     Watches the Unity log file and redirects it to standard output.
    /// </summary>
    public class Watcher
    {
        internal const string COLOR_ESC = "\x1b[39m";
        internal const string COLOR_GREEN = "\x1b[92m";
        internal const string COLOR_BLUE = "\x1b[94m";
        internal const string COLOR_DEBUG = "\x1b[90m";
        internal const string COLOR_INFO = "\x1b[97m";
        internal const string COLOR_WARN = "\x1b[93m";
        internal const string COLOR_ERROR = "\x1b[31m";

        /// <summary>
        ///     Magic string for detecting progress bar messages.
        /// </summary>
        private const string ProgressBarMarker = "DisplayProgressbar: ";

        /// <summary>
        ///     Reduces noise messages.
        /// </summary>
        private static readonly Regex[] ExcludedPatterns =
        {
            // Exclude RefreshProfiler details
            new Regex(@"\t+.*", RegexOptions.Compiled),
            new Regex(@"MadLibs\.Build\..*", RegexOptions.Compiled),
            new Regex(@"\*\*\* .* replaces .* at path .*", RegexOptions.Compiled),
            new Regex(@"Refresh: trashing asset .*", RegexOptions.Compiled)
        };

        /// <summary>
        ///     Size of the log when it was previously read.
        /// </summary>
        private static long previousLogSize;

        /// <summary>
        ///     Path to the log file.
        /// </summary>
        private readonly string logPath;

        /// <summary>
        ///     Indicates if this thread should stop.
        /// </summary>
        private volatile bool shouldStop;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Watcher" /> class that will read the log from the specified path.
        /// </summary>
        /// <param name="logPath">
        ///     The log Path.
        /// </param>
        public Watcher(string logPath)
        {
            this.logPath = logPath;
        }

        /// <summary>
        ///     Gets the full log text.
        /// </summary>
        public string FullLog { get; private set; } = string.Empty;

        /// <summary>
        ///     The run.
        /// </summary>
        public void Run()
        {
            while (true)
            {
                if (File.Exists(this.logPath))
                {
                    using (FileStream stream = new FileStream(
                        this.logPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite))
                    {
                        stream.Position = previousLogSize;
                        previousLogSize = stream.Length;

                        using (var reader = new StreamReader(stream))
                        {
                            var newText = reader.ReadToEnd();
                            newText = this.LogProgressMessages(newText);
                            this.FullLog += newText;
                            // Console.Write(newText);
                        }
                    }
                }

                if (this.shouldStop)
                {
                    break;
                }

                Thread.Sleep(1000);
            }
        }

        /// <summary>
        ///     Stops the watcher.
        /// </summary>
        public void Stop()
        {
            // Wait for last log lines to be flushed then stop.
            Thread.Sleep(1500);
            this.shouldStop = true;
        }

        /// <summary>
        ///     Searches for progress bar messages and forwards them to TeamCity.
        /// </summary>
        /// <param name="text">
        ///     The text.
        /// </param>
        /// <returns>
        ///     The <see cref="string" />.
        /// </returns>
        private string LogProgressMessages(string text)
        {
            var result = string.Empty;
            var lines = text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (ExcludedPatterns.Any(p => p.IsMatch(line)))
                {
                    continue;
                }

                if (line.StartsWith(ProgressBarMarker))
                {
                    var progressName = line.Substring(ProgressBarMarker.Length);
                    Console.WriteLine();
                    Console.WriteLine($"{COLOR_BLUE}##[section]{progressName}{COLOR_ESC}");

                    // Console.WriteLine("##teamcity[progressMessage '" + progressName + "']");
                } else if (line.Contains(Program.SuccessMagicString) || line.Contains("##utp:"))
                {
                    Console.WriteLine($"{COLOR_GREEN}{line}{COLOR_ESC}");
                } else
                {
                    if (line.Contains("error ") || line.Contains("Error "))
                    {
                        Console.WriteLine($"{COLOR_ERROR}##[error]{line}{COLOR_ESC}");
                    } else if (line.Contains("##[info]") || line.Contains("##[section]"))
                    {
                        Console.WriteLine($"{COLOR_BLUE}{line}{COLOR_ESC}");
                    } else if (line.Contains("##[warning]") || line.Contains("warning ") || line.Contains("Warning "))
                    {
                        Console.WriteLine($"{COLOR_WARN}{line}{COLOR_ESC}");
                    } else if (line.Contains("##[error]"))
                    {
                        Console.WriteLine($"{COLOR_ERROR}{line}{COLOR_ESC}");
                    } else if (line.Contains("##[debug]"))
                    {
                        Console.WriteLine($"{COLOR_DEBUG}{line}{COLOR_ESC}");
                    } else
                    {
                        Console.WriteLine(line);
                    }
                    
                }

                result += line + Environment.NewLine;
            }

            return result;
        }
    }
}