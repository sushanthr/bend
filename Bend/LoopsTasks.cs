using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bend
{
    public sealed class AgentCommandProfile
    {
        public string Name { get; set; }
        public string CommandTemplate { get; set; }
    }

    public sealed class LoopDefinition
    {
        public string Name { get; set; }
        public string FolderPath { get; set; }
        public string PromptPath { get { return Path.Combine(FolderPath, "Prompt.md"); } }
        public int MaxIterations { get; set; }
        public string Agent { get; set; }
    }

    public sealed class TaskDefinition
    {
        public string Name { get; set; }
        public string FolderPath { get; set; }
        public string ConfigPath { get { return Path.Combine(FolderPath, "config.yaml"); } }
        public TimeSpan Repeat { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public string Run { get; set; }
        public string Trigger { get; set; }
        public bool Enabled { get; set; }
        public string LastStatus { get; set; }
        public DateTime? LastRun { get; set; }
        public string LastLogPath { get; set; }
    }

    public static class LoopsTasksStorage
    {
        public static string Root { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bend"); } }
        public static string LoopsPath { get { return Path.Combine(Root, "Loops"); } }
        public static string TasksPath { get { return Path.Combine(Root, "Tasks"); } }

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(LoopsPath);
            Directory.CreateDirectory(TasksPath);
        }

        public static string NewFolder(string parent, string prefix)
        {
            EnsureFolders();
            int index = 1;
            string path;
            do { path = Path.Combine(parent, prefix + " " + index++); } while (Directory.Exists(path));
            Directory.CreateDirectory(path);
            return path;
        }

        public static string LoopTemplate = "<!--\r\nmax_iterations: 10\r\n\r\nWrite the objective, context, constraints, and verification steps below.\r\nKeep each iteration focused and leave the workspace in a testable state.\r\nWhen the objective is complete, the final agent message must contain /terminate_loop.\r\n-->\r\n\r\n# Objective\r\n\r\nDescribe the outcome this loop should achieve.\r\n\r\n# Instructions\r\n\r\nProvide context, constraints, and best practices for the agent.\r\n\r\n# Verification\r\n\r\nDescribe how the agent should verify progress.\r\n";

        public static string TaskTemplate = "# Bend scheduled task configuration\r\n# repeat accepts values such as 24h, 5h, 30m, or 1d.\r\nrepeat: 24h\r\n# Optional local ISO-8601 timestamps.\r\nstart:\r\nend:\r\n# Set to Prompt.md or main.py.\r\nrun: Prompt.md\r\n# Optional script. Its output must contain /triggered to proceed.\r\ntrigger: Trigger.py\r\n# All prompt tasks use the configured agent profile from Bend settings.\r\nenabled: true\r\n";

        public static LoopDefinition CreateLoop()
        {
            string folder = NewFolder(LoopsPath, "Loop");
            File.WriteAllText(Path.Combine(folder, "Prompt.md"), LoopTemplate, Encoding.UTF8);
            return LoadLoop(folder);
        }

        public static TaskDefinition CreateTask()
        {
            string folder = NewFolder(TasksPath, "Task");
            File.WriteAllText(Path.Combine(folder, "config.yaml"), TaskTemplate, Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "Prompt.md"), "# Task prompt\r\n\r\nDescribe what the scheduled agent should do.\r\n", Encoding.UTF8);
            return LoadTask(folder);
        }

        public static List<LoopDefinition> LoadLoops()
        {
            EnsureFolders();
            return Directory.GetDirectories(LoopsPath).Select(LoadLoop).ToList();
        }

        public static List<TaskDefinition> LoadTasks()
        {
            EnsureFolders();
            return Directory.GetDirectories(TasksPath).Select(LoadTask).ToList();
        }

        public static string GetConfiguredAgentTemplate()
        {
            string name = String.IsNullOrWhiteSpace(PersistantStorage.StorageObject.DefaultAgentCli) ? "copilot" : PersistantStorage.StorageObject.DefaultAgentCli.Trim();
            string configured = PersistantStorage.StorageObject.AgentCommandTemplates ?? "";
            foreach (string raw in configured.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim(); int equals = line.IndexOf('=');
                if (equals > 0 && line.Substring(0, equals).Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) return line.Substring(equals + 1).Trim();
            }
            return name + " {prompt}";
        }

        public static LoopDefinition LoadLoop(string folder)
        {
            string text = File.Exists(Path.Combine(folder, "Prompt.md")) ? File.ReadAllText(Path.Combine(folder, "Prompt.md")) : "";
            return new LoopDefinition { Name = Path.GetFileName(folder), FolderPath = folder, MaxIterations = ReadInt(text, "max_iterations", 10), Agent = ReadValue(text, "agent") ?? "default" };
        }

        public static TaskDefinition LoadTask(string folder)
        {
            TaskDefinition result = new TaskDefinition { Name = Path.GetFileName(folder), FolderPath = folder, Enabled = true, Repeat = TimeSpan.FromHours(24), Run = "Prompt.md", Trigger = "Trigger.py" };
            string[] lines = File.Exists(Path.Combine(folder, "config.yaml")) ? File.ReadAllLines(Path.Combine(folder, "config.yaml")) : new string[0];
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int colon = line.IndexOf(':'); if (colon < 0) continue;
                string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                string value = line.Substring(colon + 1).Trim().Trim('\'', '"');
                if (key == "repeat") { TimeSpan interval; if (TryParseDuration(value, out interval)) result.Repeat = interval; }
                else if (key == "start") result.Start = ParseDate(value);
                else if (key == "end") result.End = ParseDate(value);
                else if (key == "run" && value.Length > 0) result.Run = value;
                else if (key == "trigger") result.Trigger = value;
                else if (key == "enabled") result.Enabled = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
            }
            LoadLastRun(result);
            return result;
        }

        public static bool TryParseDuration(string value, out TimeSpan duration)
        {
            duration = TimeSpan.Zero; if (String.IsNullOrWhiteSpace(value)) return false;
            double number; if (!double.TryParse(value.Substring(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return false;
            switch (Char.ToLowerInvariant(value[value.Length - 1])) { case 'm': duration = TimeSpan.FromMinutes(number); break; case 'h': duration = TimeSpan.FromHours(number); break; case 'd': duration = TimeSpan.FromDays(number); break; default: return false; }
            return duration > TimeSpan.Zero;
        }

        private static DateTime? ParseDate(string value) { DateTime date; return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date) ? (DateTime?)date : null; }
        private static string ReadValue(string text, string key) { foreach (string line in text.Split(new[] { '\r', '\n' })) { string t = line.Trim(); int c = t.IndexOf(':'); if (c >= 0 && t.Substring(0, c).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return t.Substring(c + 1).Trim().Trim('\'', '"'); } return null; }
        private static int ReadInt(string text, string key, int fallback) { int value; return Int32.TryParse(ReadValue(text, key), out value) && value > 0 ? value : fallback; }
        private static void LoadLastRun(TaskDefinition task)
        {
            string logs = Path.Combine(task.FolderPath, "logs"); if (!Directory.Exists(logs)) return;
            string folder = Directory.GetDirectories(logs).OrderByDescending(Directory.GetLastWriteTime).FirstOrDefault(); if (folder == null) return;
            task.LastLogPath = folder; task.LastRun = Directory.GetLastWriteTime(folder);
            string status = Path.Combine(folder, "status.txt"); task.LastStatus = File.Exists(status) ? File.ReadAllText(status).Trim() : "completed";
        }
    }

    public static class LoopsTasksRunner
    {
        public static async Task RunLoopAsync(LoopDefinition loop, string commandTemplate, Action<int, string> progress, CancellationToken token)
        {
            string prompt = File.ReadAllText(loop.PromptPath); for (int i = 1; i <= loop.MaxIterations; i++) { token.ThrowIfCancellationRequested(); string output = await RunProcessAsync(commandTemplate, prompt, loop.FolderPath, token); progress(i, output); if (output.IndexOf("/terminate_loop", StringComparison.OrdinalIgnoreCase) >= 0) break; }
        }

        public static async Task<string> RunProcessAsync(string commandTemplate, string prompt, string workingDirectory, CancellationToken token)
        {
            string command = commandTemplate.Replace("{prompt}", Quote(prompt)).Replace("{prompt_file}", Quote(Path.Combine(workingDirectory, "Prompt.md")));
            ProcessStartInfo info = new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/d /s /c " + Quote(command), WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (Process process = Process.Start(info)) { StringBuilder output = new StringBuilder(); process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); }; process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); }; process.BeginOutputReadLine(); process.BeginErrorReadLine(); try { while (!process.HasExited) { token.ThrowIfCancellationRequested(); await Task.Delay(100, token); } } catch { try { if (!process.HasExited) process.Kill(); } catch { } throw; } return output.ToString(); }
        }
        private static string Quote(string value) { return "\"" + (value ?? "").Replace("\"", "\\\"") + "\""; }
    }

    public static class ScheduledTaskEngine
    {
        private static readonly object sync = new object();
        private static readonly HashSet<string> running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static async Task RunDueTasksAsync()
        {
            foreach (TaskDefinition task in LoopsTasksStorage.LoadTasks())
            {
                if (!task.Enabled || task.Start.HasValue && DateTime.Now < task.Start.Value || task.End.HasValue && DateTime.Now > task.End.Value) continue;
                if (task.LastRun.HasValue && DateTime.Now - task.LastRun.Value < task.Repeat) continue;
                bool acquired; lock (sync) acquired = running.Add(task.FolderPath); if (!acquired) continue;
                try { await RunTaskAsync(task); } catch { } finally { lock (sync) running.Remove(task.FolderPath); }
            }
        }

        public static async Task RunTaskAsync(TaskDefinition task)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string logFolder = Path.Combine(task.FolderPath, "logs", stamp); Directory.CreateDirectory(logFolder);
            File.WriteAllText(Path.Combine(logFolder, "wakeup.log"), DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            try
            {
                bool triggered = true;
                if (!String.IsNullOrWhiteSpace(task.Trigger) && File.Exists(Path.Combine(task.FolderPath, task.Trigger)))
                {
                    string triggerOutput = await LoopsTasksRunner.RunProcessAsync("python " + QuoteValue(task.Trigger), "", task.FolderPath, CancellationToken.None);
                    File.WriteAllText(Path.Combine(logFolder, "trigger.log"), triggerOutput);
                    triggered = triggerOutput.IndexOf("/triggered", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (!triggered) { File.WriteAllText(Path.Combine(logFolder, "status.txt"), "not triggered"); return; }
                string output;
                string runPath = Path.Combine(task.FolderPath, task.Run ?? "Prompt.md");
                if (String.Equals(Path.GetExtension(runPath), ".py", StringComparison.OrdinalIgnoreCase)) output = await LoopsTasksRunner.RunProcessAsync("python " + QuoteValue(Path.GetFileName(runPath)), "", task.FolderPath, CancellationToken.None);
                else output = await LoopsTasksRunner.RunProcessAsync(LoopsTasksStorage.GetConfiguredAgentTemplate(), File.Exists(runPath) ? File.ReadAllText(runPath) : "", task.FolderPath, CancellationToken.None);
                File.WriteAllText(Path.Combine(logFolder, "run.log"), output); File.WriteAllText(Path.Combine(logFolder, "status.txt"), "completed");
            }
            catch
            {
                try { File.WriteAllText(Path.Combine(logFolder, "status.txt"), "failed"); } catch { }
                throw;
            }
        }
        private static string QuoteValue(string value) { return "\"" + (value ?? "").Replace("\"", "\\\"") + "\""; }
    }

    public static class BendTaskScheduler
    {
        public static void Reconcile()
        {
            List<TaskDefinition> tasks = LoopsTasksStorage.LoadTasks().Where(t => t.Enabled && t.Repeat > TimeSpan.Zero).ToList();
            if (tasks.Count == 0) { Run("/Delete /TN \"Bend Scheduled Tasks\" /F"); return; }
            double minutes = tasks.Min(t => t.Repeat.TotalMinutes); int interval = Math.Max(1, (int)Math.Ceiling(minutes));
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            Run("/Create /TN \"Bend Scheduled Tasks\" /SC MINUTE /MO " + interval + " /TR \"\\\"" + executable + "\\\"\" /F");
        }
        private static void Run(string arguments)
        {
            try { using (Process process = Process.Start(new ProcessStartInfo { FileName = "schtasks.exe", Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })) process.WaitForExit(5000); } catch { }
        }
    }
}
