using System;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

namespace ChatGPTContextExporter.Editor
{
    public static class GitIntegration
    {
        public static string GetDiffBetweenDates(string since, string until, string author = "", string grep = "")
        {
            try
            {
                string logArgs = $"log --since=\"{since}\" --until=\"{until}\" --pretty=format:\"Commit %h by %an on %ad%n%s%n\" --date=short";
                string diffArgs = $"diff --since=\"{since}\" --until=\"{until}\"";

                if (!string.IsNullOrEmpty(author)) logArgs += $" --author=\"{author}\"";
                if (!string.IsNullOrEmpty(grep)) logArgs += $" --grep=\"{grep}\"";

                string logOutput = RunGitCommand(logArgs);
                string diffOutput = RunGitCommand(diffArgs);

                if (string.IsNullOrWhiteSpace(logOutput) && string.IsNullOrWhiteSpace(diffOutput))
                    return "No commits or diffs found in the specified range.";

                return $"=== GIT FILTERS ===\nAuthor: {(string.IsNullOrEmpty(author) ? "All" : author)}\nGrep: {(string.IsNullOrEmpty(grep) ? "None" : grep)}\n\n=== GIT LOG ===\n{logOutput}\n\n=== GIT DIFF ===\n{diffOutput}";
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Git diff failed: " + ex.Message);
                return "Git not available or error occurred.";
            }
        }

        private static string RunGitCommand(string args)
        {
            ProcessStartInfo info = new("git", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };
            
            using Process proc = new();
            proc.StartInfo = info;
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            
            if (!string.IsNullOrEmpty(error)) 
                output += "\n--- Git Error ---\n" + error;
            
            return output;
        }
    }
}