using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ChatGPTContextExporter.Editor
{
    public class ProjectContextExporterWindow : EditorWindow
    {
        private const string k_PrefCodeRoot = "ChatGPTExporter_CodeRoot";
        private const string k_PrefInstRoot = "ChatGPTExporter_InstRoot";
        private const string k_PrefExportPath = "ChatGPTExporter_ExportPath";
        private const string k_PrefPromptMode = "ChatGPTExporter_PromptMode";
        private const string k_PrefExportGit = "ChatGPTExporter_ExportGit";
        private const string k_PrefGitAuthor = "ChatGPTExporter_GitAuthor";
        private const string k_PrefGitGrep = "ChatGPTExporter_GitGrep";
        
        private static readonly string[] s_MonthNames = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12).ToArray();
        
        private Tab m_CurrentTab = Tab.Code;

        private string m_CodeRootPath;
        private string m_InstructionsRootPath;
        private string m_ExportPath;

        private Vector2 m_CodeScrollLeft, m_CodeScrollRight;
        private Vector2 m_GitScroll;

        private List<string> m_AllCodeFiles = new();
        private List<string> m_AllInstructionFiles = new();

        private string m_SearchQuery = string.Empty;

        private PromptMode m_PromptMode = PromptMode.NewConversation;

        private bool m_ExportGitStatus = true;
        private string m_GitAuthor = string.Empty;
        private string m_GitGrep = string.Empty;
        private string m_GitDiffPreview = string.Empty;

        private bool m_AdvancedMode;
        private DateTime m_GitSinceDate = DateTime.Now.AddDays(-7);
        private DateTime m_GitUntilDate = DateTime.Now;
        private float m_GitSinceHour;
        private float m_GitUntilHour = 23.99f;
        
        private readonly FileStagingManager m_CodeStage = new();
        private readonly FileStagingManager m_InstructionStage = new();

        private string GitSince => $"{m_GitSinceDate:yyyy-MM-dd} {Mathf.FloorToInt(m_GitSinceHour):00}:{Mathf.FloorToInt((m_GitSinceHour % 1) * 60):00}";
        private string GitUntil => $"{m_GitUntilDate:yyyy-MM-dd} {Mathf.FloorToInt(m_GitUntilHour):00}:{Mathf.FloorToInt((m_GitUntilHour % 1) * 60):00}";

        [MenuItem("Tools/ChatGPT/Project Context Exporter")]
        public static void ShowWindow()
        {
            ProjectContextExporterWindow window = GetWindow<ProjectContextExporterWindow>("Project Context Exporter");
            window.minSize = new Vector2(800, 500);
            window.LoadPrefs();
            window.ScanFiles();
        }

        private void OnGUI()
        {
            m_CurrentTab = (Tab)GUILayout.Toolbar((int)m_CurrentTab, new[] { "Code", "Instructions", "Settings", "Git" });
            GUILayout.Space(10);

            switch (m_CurrentTab)
            {
                case Tab.Code:
                    DrawStageView(m_AllCodeFiles, m_CodeStage, "C#");
                    break;
                case Tab.Instructions:
                    DrawStageView(m_AllInstructionFiles, m_InstructionStage, "Markdown");
                    break;
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
                case Tab.Git:
                    DrawGitTab();
                    break;
            }

            GUILayout.FlexibleSpace();
            
            DrawBottomBar();
        }

        // ----------------- CODE / INSTRUCTION -------------------

        private void DrawStageView(List<string> allFiles, FileStagingManager stage, string type)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Search {type} Files:", GUILayout.Width(120));
            
            string newQuery = GUILayout.TextField(m_SearchQuery);
            
            if (newQuery != m_SearchQuery)
            {
                m_SearchQuery = newQuery;
                ScanFiles();
            }
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical("box", GUILayout.Width(position.width / 2 - 10));
            GUILayout.Label("Unstaged Files", EditorStyles.boldLabel);

            m_CodeScrollLeft = GUILayout.BeginScrollView(m_CodeScrollLeft);
            try
            {
                List<string> unstagedFiltered = stage.UnstagedFiles
                    .Where(f => string.IsNullOrEmpty(m_SearchQuery) || f.ToLower().Contains(m_SearchQuery.ToLower()))
                    .ToList();

                foreach (string f in unstagedFiltered)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Path.GetFileName(f), GUILayout.Width(300));
                    
                    if (GUILayout.Button("→", GUILayout.Width(25))) 
                        stage.Stage(f);
                    
                    GUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndScrollView();
            }

            if (GUILayout.Button("Stage All")) 
                stage.StageAll();
            
            GUILayout.EndVertical();

            GUILayout.BeginVertical("box", GUILayout.Width(position.width / 2 - 10));
            GUILayout.Label("Staged Files", EditorStyles.boldLabel);

            m_CodeScrollRight = GUILayout.BeginScrollView(m_CodeScrollRight);
            try
            {
                List<string> stagedFiltered = stage.StagedFiles
                    .Where(f => string.IsNullOrEmpty(m_SearchQuery) || f.ToLower().Contains(m_SearchQuery.ToLower()))
                    .ToList();

                foreach (string f in stagedFiltered)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Path.GetFileName(f), GUILayout.Width(300));

                    if (GUILayout.Button("←", GUILayout.Width(25)))
                        stage.Unstage(f);

                    GUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndScrollView();
            }

            if (GUILayout.Button("Unstage All")) 
                stage.UnstageAll();
            
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Rescan")) 
                ScanFiles();
        }

        // ----------------- SETTINGS -------------------

        private void DrawSettingsTab()
        {
            GUILayout.Label("Export Settings", EditorStyles.boldLabel);
            DrawPathField("Code Root Path", ref m_CodeRootPath);
            DrawPathField("Instructions Root Path", ref m_InstructionsRootPath);
            DrawPathField("Export Path", ref m_ExportPath, true);
        }

        private static void DrawPathField(string label, ref string path, bool isExport = false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(160));
            path = GUILayout.TextField(path ?? "");
            if (GUILayout.Button("Locate", GUILayout.Width(70)))
            {
                string selected = EditorUtility.OpenFolderPanel(label, "Assets", "");
                if (!string.IsNullOrEmpty(selected)) path = selected;
            }
            if (!isExport && GUILayout.Button("Set Default", GUILayout.Width(90)))
                path = Application.dataPath;
            GUILayout.EndHorizontal();
        }

        // ----------------- GIT TAB -------------------

        private void DrawGitTab()
        {
            GUILayout.Label("Git Diff Export", EditorStyles.boldLabel);
            GUILayout.Space(5);

            m_ExportGitStatus = GUILayout.Toggle(m_ExportGitStatus, "Export Git Status");
            GUILayout.Space(5);

            // Quick Range Buttons
            GUILayout.BeginHorizontal();
            GUILayout.Label("Quick Range:", GUILayout.Width(80));
            
            if (GUILayout.Button("Last 24h", GUILayout.Width(100))) 
                ApplyQuickRange(TimeSpan.FromDays(1));
            
            if (GUILayout.Button("Last 7 Days", GUILayout.Width(100))) 
                ApplyQuickRange(TimeSpan.FromDays(7));
            
            if (GUILayout.Button("This Month", GUILayout.Width(100))) 
                ApplyThisMonthRange();
            
            if (GUILayout.Button("All Time", GUILayout.Width(100))) 
                ApplyAllTimeRange();
            
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            m_AdvancedMode = GUILayout.Toggle(m_AdvancedMode, "Advanced Mode (manual date entry)");
            GUILayout.Space(5);

            if (!m_AdvancedMode)
            {
                m_GitSinceDate = DrawDateTimePicker("Start Date", m_GitSinceDate, ref m_GitSinceHour);
                m_GitUntilDate = DrawDateTimePicker("End Date", m_GitUntilDate, ref m_GitUntilHour);
            }
            else
            {
                GUILayout.Label("Start Time (yyyy-MM-dd HH:mm):");
                GUILayout.TextField(GitSince);
                GUILayout.Label("End Time (yyyy-MM-dd HH:mm):");
                GUILayout.TextField(GitUntil);
            }

            GUILayout.Space(10);
            GUILayout.Label("Author Filter (optional):");
            m_GitAuthor = GUILayout.TextField(m_GitAuthor);
            GUILayout.Label("Grep Filter (optional):");
            m_GitGrep = GUILayout.TextField(m_GitGrep);

            if (GUILayout.Button("Fetch Diff Preview"))
                m_GitDiffPreview = GitIntegration.GetDiffBetweenDates(GitSince, GitUntil, m_GitAuthor, m_GitGrep);

            GUILayout.Space(5);
            m_GitScroll = GUILayout.BeginScrollView(m_GitScroll);
            GUILayout.TextArea(m_GitDiffPreview, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
        }

        private void ApplyQuickRange(TimeSpan span)
        {
            m_GitSinceDate = DateTime.Now - span;
            m_GitUntilDate = DateTime.Now;
            m_GitSinceHour = 0f;
            m_GitUntilHour = 23.99f;
        }

        private void ApplyThisMonthRange()
        {
            DateTime now = DateTime.Now;
            m_GitSinceDate = new DateTime(now.Year, now.Month, 1);
            m_GitUntilDate = DateTime.Now;
            m_GitSinceHour = 0f;
            m_GitUntilHour = 23.99f;
        }

        private void ApplyAllTimeRange()
        {
            m_GitSinceDate = new DateTime(2000, 1, 1);
            m_GitUntilDate = DateTime.Now;
            m_GitSinceHour = 0f;
            m_GitUntilHour = 23.99f;
        }

        private static DateTime DrawDateTimePicker(string label, DateTime current, ref float hour)
        {
            GUILayout.Label(label, EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            int year = EditorGUILayout.IntField(current.Year, GUILayout.Width(70));
            int month = EditorGUILayout.IntPopup(current.Month, s_MonthNames, Enumerable.Range(1, 12).ToArray(), GUILayout.Width(100));
            int day = EditorGUILayout.IntField(current.Day, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            hour = EditorGUILayout.Slider("Hour", hour, 0f, 23.99f);
            try { current = new DateTime(year, month, Mathf.Clamp(day, 1, DateTime.DaysInMonth(year, month))); }
            catch { current = DateTime.Now; }
            return current;
        }

        // ----------------- EXPORT / PREFS -------------------

        private void DrawBottomBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Prompt Mode:", GUILayout.Width(100));
            m_PromptMode = (PromptMode)GUILayout.Toolbar((int)m_PromptMode, new[] { "New", "Ongoing", "Don't Open" }, GUILayout.Width(300));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Export", GUILayout.Width(120)))
            {
                SavePrefs();
                Export();
            }
            GUILayout.EndHorizontal();
        }

        private void ScanFiles()
        {
            m_AllCodeFiles = Directory.GetFiles(m_CodeRootPath ?? Application.dataPath, "*.cs", SearchOption.AllDirectories).ToList();
            m_AllInstructionFiles = Directory.GetFiles(m_InstructionsRootPath ?? Application.dataPath, "*.md", SearchOption.AllDirectories).ToList();
            m_CodeStage.RefreshFiles(m_AllCodeFiles);
            m_InstructionStage.RefreshFiles(m_AllInstructionFiles);
        }

        private void Export()
        {
            if (string.IsNullOrEmpty(m_ExportPath))
            {
                EditorUtility.DisplayDialog("Export Failed", "Please set an export path in Settings.", "OK");
                return;
            }

            string codeOutput = Path.Combine(m_ExportPath, "Code.txt");
            string instOutput = Path.Combine(m_ExportPath, "Instructions.md");
            string gitOutput = Path.Combine(m_ExportPath, "git_status.txt");

            try
            {
                EditorUtility.DisplayProgressBar("Exporting", "Writing staged files...", 0.5f);

                File.WriteAllLines(codeOutput, m_CodeStage.StagedFiles.SelectMany(File.ReadAllLines));
                File.WriteAllLines(instOutput, m_InstructionStage.StagedFiles.SelectMany(File.ReadAllLines));

                bool hasGit = false;
                if (m_ExportGitStatus)
                {
                    var diff = GitIntegration.GetDiffBetweenDates(GitSince, GitUntil, m_GitAuthor, m_GitGrep);
                    File.WriteAllText(gitOutput, diff);
                    hasGit = true;
                }

                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                if (m_PromptMode != PromptMode.DontOpen)
                    PromptWindow.ShowPrompt(codeOutput, instOutput, gitOutput, hasGit, m_PromptMode);
                else
                    EditorUtility.DisplayDialog("Export Complete", $"Files exported to:\n{m_ExportPath}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(k_PrefCodeRoot, m_CodeRootPath ?? "");
            EditorPrefs.SetString(k_PrefInstRoot, m_InstructionsRootPath ?? "");
            EditorPrefs.SetString(k_PrefExportPath, m_ExportPath ?? "");
            EditorPrefs.SetInt(k_PrefPromptMode, (int)m_PromptMode);
            EditorPrefs.SetBool(k_PrefExportGit, m_ExportGitStatus);
            EditorPrefs.SetString(k_PrefGitAuthor, m_GitAuthor ?? "");
            EditorPrefs.SetString(k_PrefGitGrep, m_GitGrep ?? "");
        }

        private void LoadPrefs()
        {
            m_CodeRootPath = EditorPrefs.GetString(k_PrefCodeRoot, Application.dataPath);
            m_InstructionsRootPath = EditorPrefs.GetString(k_PrefInstRoot, Application.dataPath);
            m_ExportPath = EditorPrefs.GetString(k_PrefExportPath, Application.dataPath);
            m_PromptMode = (PromptMode)EditorPrefs.GetInt(k_PrefPromptMode, 0);
            m_ExportGitStatus = EditorPrefs.GetBool(k_PrefExportGit, true);
            m_GitAuthor = EditorPrefs.GetString(k_PrefGitAuthor, "");
            m_GitGrep = EditorPrefs.GetString(k_PrefGitGrep, "");
        }
    }
}