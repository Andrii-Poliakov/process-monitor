using System.Diagnostics;

namespace ProcessManager
{
    public class ProcessHelper
    {



        public  List<ProcessInfoDto> GetProcessList()
        {
            List<ProcessInfoDto> processInfoDtos = new List<ProcessInfoDto>();

            var urrentDateTime = DateTime.Now;

            foreach (var p in SafeGetProcessesWithUi())
            {
                var exePath = TryGetExePath(p);
                if (exePath == null) continue;

                var fullPath = NormalizePath(exePath);
                var processName = p.ProcessName;

                var dto = new ProcessInfoDto
                {
                    ProcessId = p.Id,
                    Name = processName,
                    FullPath = fullPath,
                    StartDateTime = p.StartTime,
                    EndDateTime = null
                };
                processInfoDtos.Add(dto);

            }

            return processInfoDtos;

        }


        /// <summary>
        /// Retrieves a collection of processes that have a user interface (UI) by filtering processes with a valid main
        /// window handle and a non-empty window title.
        /// </summary>
        /// <remarks>This method safely enumerates all processes on the system and filters out those that
        /// do not have a user interface. Processes without a valid main window handle or with an empty or 
        /// whitespace-only window title are excluded. If access to a process is denied, it is skipped.</remarks>
        /// <returns>An enumerable collection of <see cref="Process"/> objects representing processes with a user interface.</returns>
        private static IEnumerable<Process> SafeGetProcessesWithUi()
        {
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { yield break; }

            foreach (var p in all)
            {
                IntPtr h = IntPtr.Zero;
                string title = null;

                try
                {
                    h = p.MainWindowHandle;
                    title = p.MainWindowTitle;
                }
                catch { /* Skip if no access */ }

                if (h != IntPtr.Zero && !string.IsNullOrWhiteSpace(title))
                    yield return p;
                else
                    p.Dispose();
            }
        }

        private static string TryGetExePath(Process p)
        {
            try
            {
                return p.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts the specified path to its fully qualified form, removing any leading or trailing whitespace and
        /// quotes.
        /// </summary>
        /// <param name="path">The input path to normalize. Must not be null or empty.</param>
        /// <returns>The fully qualified version of the specified path.</returns>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                // Trim spaces and surrounding quotes
                var cleaned = path.Trim().Trim('"');

                if (string.IsNullOrWhiteSpace(cleaned))
                    return string.Empty;

                // Normalize the path
                return Path.GetFullPath(cleaned);
            }
            catch (Exception)
            {
                // Return empty string if normalization fails
                // (consider logging the exception if needed)
                return string.Empty;
            }
        }
        


    }
}
