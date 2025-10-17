using System.Collections.Generic;
using System.Linq;

namespace ChatGPTContextExporter.Editor
{
    public class FileStagingManager
    {
        public HashSet<string> StagedFiles { get; private set; } = new();
        public HashSet<string> UnstagedFiles { get; private set; } = new();
        public void RefreshFiles(IEnumerable<string> allFiles)
        {
            HashSet<string> current = new(allFiles);
            StagedFiles.RemoveWhere(f => !current.Contains(f));
            UnstagedFiles = new(current.Except(StagedFiles));
        }
        public void Stage(string f) { if (UnstagedFiles.Remove(f)) StagedFiles.Add(f); }
        public void Unstage(string f) { if (StagedFiles.Remove(f)) UnstagedFiles.Add(f); }
        public void StageAll() { foreach (var f in UnstagedFiles.ToList()) StagedFiles.Add(f); UnstagedFiles.Clear(); }
        public void UnstageAll() { foreach (var f in StagedFiles.ToList()) UnstagedFiles.Add(f); StagedFiles.Clear(); }
    }
}