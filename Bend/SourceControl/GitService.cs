using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bend.SourceControl
{
    public enum GitChangeLayer { Staged, Unstaged, Untracked, Conflict }
    public enum GitResetMode { Soft, Mixed, Hard }

    public sealed class GitChange
    {
        public string Path { get; set; }
        public string OriginalPath { get; set; }
        public char IndexStatus { get; set; }
        public char WorkTreeStatus { get; set; }
        public GitChangeLayer Layer { get; set; }
        public string StatusText { get { return Layer == GitChangeLayer.Conflict ? "!" : (Layer == GitChangeLayer.Untracked ? "U" : (Layer == GitChangeLayer.Staged ? IndexStatus.ToString() : WorkTreeStatus.ToString())); } }
        public string ActionGlyph { get { return Layer == GitChangeLayer.Staged ? "\uEB3B" : "\uEA60"; } }
        public string ActionToolTip { get { return Layer == GitChangeLayer.Staged ? "Unstage changes" : "Stage changes"; } }
    }

    public sealed class GitRepositoryStatus
    {
        public string RepositoryRoot { get; set; }
        public string Branch { get; set; }
        public bool IsDetached { get; set; }
        public List<GitChange> Changes { get; private set; } = new List<GitChange>();
    }

    public sealed class GitCommit
    {
        public string Hash { get; set; }
        public string ShortHash { get; set; }
        public string Author { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Subject { get; set; }
        public string Display { get { return ShortHash + "  " + Subject; } }
    }
    public sealed class GitReflogEntry
    {
        public string Hash { get; set; }
        public string ShortHash { get; set; }
        public string Selector { get; set; }
        public string Subject { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Display { get { return Selector + "  " + Subject; } }
    }
    public sealed class GitFileComparison { public string BaseText { get; set; } public string CurrentText { get; set; } }

    public sealed class GitResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Success { get { return ExitCode == 0; } }
    }

    public interface IGitService
    {
        Task<GitRepositoryStatus> GetStatusAsync(string workspace, CancellationToken token);
        Task<IList<string>> GetBranchesAsync(string repository, CancellationToken token);
        Task<IList<GitCommit>> GetLogAsync(string repository, string branch, int maximum, CancellationToken token);
        Task<IList<GitReflogEntry>> GetReflogAsync(string repository, int maximum, CancellationToken token);
        Task<string> GetFileDiffAsync(string repository, GitChange change, CancellationToken token);
        Task<GitFileComparison> GetFileComparisonAsync(string repository, GitChange change, CancellationToken token);
        Task<string> GetWorkingFileBaseAsync(string workspace, string fullFilePath, CancellationToken token);
        Task<string> GetCommitDiffAsync(string repository, string commit, CancellationToken token);
        Task<GitResult> StageAsync(string repository, string path, CancellationToken token);
        Task<GitResult> UnstageAsync(string repository, string path, CancellationToken token);
        Task<GitResult> DiscardAsync(string repository, GitChange change, CancellationToken token);
        Task<GitResult> CommitAsync(string repository, string message, CancellationToken token);
        Task<GitResult> PushAsync(string repository, bool forceWithLease, CancellationToken token);
        Task<GitResult> PullAsync(string repository, CancellationToken token);
        Task<GitResult> FetchAsync(string repository, CancellationToken token);
        Task<GitResult> CheckoutAsync(string repository, string revision, CancellationToken token);
        Task<GitResult> CheckoutRemoteBranchAsync(string repository, string remote, string branch, string localBranch, CancellationToken token);
        Task<GitResult> CreateBranchAsync(string repository, string name, string startPoint, CancellationToken token);
        Task<GitResult> RenameCurrentBranchAsync(string repository, string name, CancellationToken token);
        Task<GitResult> DeleteBranchAsync(string repository, string name, CancellationToken token);
        Task<GitResult> ResetAsync(string repository, string revision, GitResetMode mode, CancellationToken token);
        Task<GitResult> RevertAsync(string repository, string revision, CancellationToken token);
    }

    public sealed class GitService : IGitService
    {
        public async Task<GitRepositoryStatus> GetStatusAsync(string workspace, CancellationToken token)
        {
            GitResult rootResult = await RunAsync(workspace, new[] { "rev-parse", "--show-toplevel" }, token);
            if (!rootResult.Success) throw new InvalidOperationException(CleanError(rootResult));
            string root = rootResult.Output.Trim();
            GitResult result = await RunAsync(root, new[] { "status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all" }, token);
            if (!result.Success) throw new InvalidOperationException(CleanError(result));
            return ParseStatus(root, result.Output);
        }

        public async Task<IList<string>> GetBranchesAsync(string repository, CancellationToken token)
        {
            GitResult result = await RunAsync(repository, new[] { "for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/remotes" }, token);
            if (!result.Success) throw new InvalidOperationException(CleanError(result));
            return result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !x.EndsWith("/HEAD", StringComparison.Ordinal)).Distinct().ToList();
        }

        public async Task<IList<GitCommit>> GetLogAsync(string repository, string branch, int maximum, CancellationToken token)
        {
            var args = new List<string> { "log", "--date=iso-strict", "--format=%H%x1f%h%x1f%an%x1f%aI%x1f%s", "-n", Math.Max(1, maximum).ToString() };
            if (!string.IsNullOrWhiteSpace(branch)) args.Add(branch);
            GitResult result = await RunAsync(repository, args, token);
            if (!result.Success)
            {
                if (result.Error.IndexOf("does not have any commits", StringComparison.OrdinalIgnoreCase) >= 0) return new List<GitCommit>();
                throw new InvalidOperationException(CleanError(result));
            }
            var commits = new List<GitCommit>();
            foreach (string line in result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Split(new[] { '\x1f' }, 5);
                DateTimeOffset date;
                if (fields.Length == 5 && DateTimeOffset.TryParse(fields[3], out date))
                    commits.Add(new GitCommit { Hash = fields[0], ShortHash = fields[1], Author = fields[2], Date = date, Subject = fields[4] });
            }
            return commits;
        }

        public async Task<IList<GitReflogEntry>> GetReflogAsync(string repository, int maximum, CancellationToken token)
        {
            GitResult result = await RunAsync(repository, new[] { "reflog", "--date=iso-strict", "--format=%H%x1f%h%x1f%gD%x1f%gs%x1f%aI", "-n", Math.Max(1, maximum).ToString() }, token);
            if (!result.Success) throw new InvalidOperationException(CleanError(result));
            var entries = new List<GitReflogEntry>();
            foreach (string line in result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Split(new[] { '\x1f' }, 5);
                DateTimeOffset date;
                if (fields.Length == 5 && DateTimeOffset.TryParse(fields[4], out date))
                    entries.Add(new GitReflogEntry { Hash = fields[0], ShortHash = fields[1], Selector = fields[2], Subject = fields[3], Date = date });
            }
            return entries;
        }

        public async Task<string> GetFileDiffAsync(string repository, GitChange change, CancellationToken token)
        {
            var args = new List<string> { "diff", "--no-ext-diff", "--no-color", "--find-renames", "--binary" };
            if (change.Layer == GitChangeLayer.Staged) args.Add("--cached");
            if (change.Layer == GitChangeLayer.Conflict) args.Add("--cc");
            if (change.Layer == GitChangeLayer.Untracked)
            {
                // A no-index comparison supplies a normal patch for a file not yet in Git.
                args = new List<string> { "diff", "--no-index", "--no-color", "--", "NUL", change.Path };
            }
            else { args.Add("--"); args.Add(change.Path); }
            GitResult result = await RunAsync(repository, args, token, change.Layer == GitChangeLayer.Untracked ? new[] { 0, 1 } : new[] { 0 });
            if (!result.Success) throw new InvalidOperationException(CleanError(result));
            return result.Output;
        }

        public async Task<GitFileComparison> GetFileComparisonAsync(string repository, GitChange change, CancellationToken token)
        {
            string baseText = String.Empty, currentText = String.Empty;
            if (change.Layer == GitChangeLayer.Untracked)
            {
                string fullPath = Path.GetFullPath(Path.Combine(repository, change.Path));
                currentText = File.Exists(fullPath) ? await Task.Run(() => File.ReadAllText(fullPath), token) : String.Empty;
            }
            else if (change.Layer == GitChangeLayer.Staged)
            {
                currentText = await ReadGitBlobAsync(repository, ":" + change.Path, token, false);
                baseText = await ReadGitBlobAsync(repository, "HEAD:" + (change.OriginalPath ?? change.Path), token, true);
            }
            else
            {
                string fullPath = Path.GetFullPath(Path.Combine(repository, change.Path));
                currentText = File.Exists(fullPath) ? await Task.Run(() => File.ReadAllText(fullPath), token) : String.Empty;
                baseText = await ReadGitBlobAsync(repository, ":" + (change.OriginalPath ?? change.Path), token, true);
                if (change.Layer == GitChangeLayer.Conflict && String.IsNullOrEmpty(baseText))
                    baseText = await ReadGitBlobAsync(repository, "HEAD:" + (change.OriginalPath ?? change.Path), token, true);
            }
            return new GitFileComparison { BaseText = baseText, CurrentText = currentText };
        }

        public async Task<string> GetWorkingFileBaseAsync(string workspace, string fullFilePath, CancellationToken token)
        {
            GitResult rootResult = await RunAsync(workspace, new[] { "rev-parse", "--show-toplevel" }, token);
            if (!rootResult.Success) throw new InvalidOperationException(CleanError(rootResult));
            string root = Path.GetFullPath(rootResult.Output.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string file = Path.GetFullPath(fullFilePath);
            string rootPrefix = root + Path.DirectorySeparatorChar;
            if (!file.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The file is outside the active Git repository.");
            string relativePath = file.Substring(rootPrefix.Length).Replace(Path.DirectorySeparatorChar, '/');
            // Only a tracked HEAD blob is a meaningful automatic editor base.
            return await ReadGitBlobAsync(root, "HEAD:" + relativePath, token, false);
        }

        private async Task<string> ReadGitBlobAsync(string repository, string revision, CancellationToken token, bool allowMissing)
        {
            GitResult result = await RunAsync(repository, new[] { "show", revision }, token);
            if (!result.Success)
            {
                if (allowMissing) return String.Empty;
                throw new InvalidOperationException(CleanError(result));
            }
            return result.Output;
        }

        public async Task<string> GetCommitDiffAsync(string repository, string commit, CancellationToken token)
        {
            GitResult result = await RunAsync(repository, new[] { "show", "--first-parent", "--format=fuller", "--no-ext-diff", "--no-color", "--find-renames", "--binary", "--unified=2147483647", commit }, token);
            if (!result.Success) throw new InvalidOperationException(CleanError(result));
            return result.Output;
        }

        public Task<GitResult> StageAsync(string repository, string path, CancellationToken token) { return RunAsync(repository, new[] { "add", "--", path }, token); }
        public Task<GitResult> UnstageAsync(string repository, string path, CancellationToken token) { return RunAsync(repository, new[] { "reset", "-q", "HEAD", "--", path }, token); }
        public Task<GitResult> CommitAsync(string repository, string message, CancellationToken token) { return RunAsync(repository, new[] { "commit", "-m", message }, token); }
        public Task<GitResult> PushAsync(string repository, bool forceWithLease, CancellationToken token)
        {
            return RunAsync(repository, forceWithLease ? new[] { "push", "--force-with-lease" } : new[] { "push" }, token);
        }
        public Task<GitResult> PullAsync(string repository, CancellationToken token) { return RunAsync(repository, new[] { "pull" }, token); }
        public Task<GitResult> FetchAsync(string repository, CancellationToken token) { return RunAsync(repository, new[] { "fetch", "--all", "--prune" }, token); }
        public Task<GitResult> CheckoutAsync(string repository, string revision, CancellationToken token) { return RunAsync(repository, new[] { "checkout", revision }, token); }
        public async Task<GitResult> CheckoutRemoteBranchAsync(string repository, string remote, string branch, string localBranch, CancellationToken token)
        {
            string remoteTrackingBranch = remote + "/" + branch;
            GitResult fetch = await RunAsync(repository, new[] { "fetch", remote, "+refs/heads/" + branch + ":refs/remotes/" + remoteTrackingBranch }, token);
            if (!fetch.Success) return fetch;
            return await RunAsync(repository, new[] { "checkout", "-b", localBranch, "--track", remoteTrackingBranch }, token);
        }
        public Task<GitResult> CreateBranchAsync(string repository, string name, string startPoint, CancellationToken token) { return RunAsync(repository, new[] { "checkout", "-b", name, startPoint }, token); }
        public Task<GitResult> RenameCurrentBranchAsync(string repository, string name, CancellationToken token) { return RunAsync(repository, new[] { "branch", "-m", name }, token); }
        public Task<GitResult> DeleteBranchAsync(string repository, string name, CancellationToken token) { return RunAsync(repository, new[] { "branch", "-d", name }, token); }
        public Task<GitResult> ResetAsync(string repository, string revision, GitResetMode mode, CancellationToken token)
        {
            string option = mode == GitResetMode.Soft ? "--soft" : (mode == GitResetMode.Hard ? "--hard" : "--mixed");
            return RunAsync(repository, new[] { "reset", option, revision }, token);
        }
        public Task<GitResult> RevertAsync(string repository, string revision, CancellationToken token) { return RunAsync(repository, new[] { "revert", "--no-edit", revision }, token); }

        public Task<GitResult> DiscardAsync(string repository, GitChange change, CancellationToken token)
        {
            if (change.Layer == GitChangeLayer.Untracked)
            {
                string fullPath = Path.GetFullPath(Path.Combine(repository, change.Path));
                string fullRoot = Path.GetFullPath(repository).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Refusing to remove a path outside the repository.");
                if (File.Exists(fullPath)) File.Delete(fullPath);
                return Task.FromResult(new GitResult { ExitCode = 0, Output = "", Error = "" });
            }
            return RunAsync(repository, new[] { "checkout", "--", change.Path }, token);
        }

        internal static GitRepositoryStatus ParseStatus(string root, string output)
        {
            var status = new GitRepositoryStatus { RepositoryRoot = root, Branch = "HEAD" };
            string[] records = output.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < records.Length; i++)
            {
                string record = records[i];
                if (record.StartsWith("# branch.head "))
                {
                    status.Branch = record.Substring(14);
                    status.IsDetached = status.Branch == "(detached)";
                    continue;
                }
                if (record.StartsWith("? ")) { status.Changes.Add(NewChange(record.Substring(2), '?', '?', GitChangeLayer.Untracked)); continue; }
                if (record.StartsWith("u "))
                {
                    string[] f = record.Split(new[] { ' ' }, 11);
                    if (f.Length == 11) status.Changes.Add(NewChange(f[10], f[1][0], f[1][1], GitChangeLayer.Conflict));
                    continue;
                }
                if (record.StartsWith("1 ") || record.StartsWith("2 "))
                {
                    bool rename = record[0] == '2';
                    string[] f = record.Split(new[] { ' ' }, rename ? 10 : 9);
                    int pathIndex = rename ? 9 : 8;
                    if (f.Length <= pathIndex) continue;
                    char x = f[1][0], y = f[1][1];
                    string original = null;
                    if (rename && i + 1 < records.Length) original = records[++i];
                    if (x != '.') { GitChange c = NewChange(f[pathIndex], x, y, GitChangeLayer.Staged); c.OriginalPath = original; status.Changes.Add(c); }
                    if (y != '.') { GitChange c = NewChange(f[pathIndex], x, y, GitChangeLayer.Unstaged); c.OriginalPath = original; status.Changes.Add(c); }
                }
            }
            return status;
        }

        private static GitChange NewChange(string path, char x, char y, GitChangeLayer layer) { return new GitChange { Path = path, IndexStatus = x, WorkTreeStatus = y, Layer = layer }; }
        private static string CleanError(GitResult result) { string value = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error; return string.IsNullOrWhiteSpace(value) ? "Git command failed." : value.Trim(); }

        private static Task<GitResult> RunAsync(string workingDirectory, IEnumerable<string> arguments, CancellationToken token, int[] acceptedExitCodes = null)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var info = new ProcessStartInfo("git.exe", string.Join(" ", arguments.Select(Quote)))
                {
                    WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                using (var process = new Process { StartInfo = info })
                {
                    try { process.Start(); }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
                    { return new GitResult { ExitCode = -1, Error = "Git is not installed or could not be started: " + ex.Message, Output = "" }; }
                    using (token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch { } }))
                    {
                        string stdout = process.StandardOutput.ReadToEnd();
                        string stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        token.ThrowIfCancellationRequested();
                        int code = process.ExitCode;
                        if (acceptedExitCodes != null && acceptedExitCodes.Contains(code)) code = 0;
                        return new GitResult { ExitCode = code, Output = stdout, Error = stderr };
                    }
                }
            }, token);
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            if (value.Length > 0 && value.All(c => !char.IsWhiteSpace(c) && c != '\"')) return value;
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '\"') { b.Append('\\', slashes * 2 + 1).Append(c); slashes = 0; continue; }
                b.Append('\\', slashes).Append(c); slashes = 0;
            }
            b.Append('\\', slashes * 2).Append('\"'); return b.ToString();
        }
    }
}
