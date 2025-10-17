using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChatGPTContextExporter.Editor
{
    public class PromptWindow : EditorWindow
    {
        private static string s_CodePath;
        private static string s_InstructionPath;
        private static string s_GitPath;
        private static bool s_HasGitStatus;
        private static int s_PromptModeIndex;

        private Vector2 m_Scroll;

        public static void ShowPrompt(string codePath, string instructionPath, string gitPath, bool hasGit, Enum mode)
        {
            s_CodePath = codePath;
            s_InstructionPath = instructionPath;
            s_GitPath = gitPath;
            s_HasGitStatus = hasGit;
            s_PromptModeIndex = Convert.ToInt32(mode);

            EditorApplication.delayCall += () =>
            {
                PromptWindow window = GetWindow<PromptWindow>("Initial Prompt", true);
                window.minSize = new Vector2(520, 260);
                window.ShowUtility();
                window.Focus();
            };
        }

        private void OnGUI()
        {
            GUILayout.Label("ChatGPT Prompt Generator", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            string prompt = BuildPrompt();
            
            m_Scroll = GUILayout.BeginScrollView(m_Scroll);
            EditorGUILayout.TextArea(prompt, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(25)))
            {
                EditorGUIUtility.systemCopyBuffer = prompt;
                EditorUtility.DisplayDialog("Copied!", "Prompt copied to clipboard.", "OK");
            }
            
            if (GUILayout.Button("Close", GUILayout.Height(25), GUILayout.Width(100)))
                Close();
            
            GUILayout.EndHorizontal();
        }

        private static string BuildPrompt()
        {
            string codeFile = Path.GetFileName(s_CodePath);
            string instFile = Path.GetFileName(s_InstructionPath);
            string gitFile = s_HasGitStatus ? Path.GetFileName(s_GitPath) : null;

            string prompt = s_PromptModeIndex switch
            {
                0 => $"I'm developing a Unity game. The project code is in `{codeFile}` and additional instructions are in `{instFile}`.",
                1 => $"We have an ongoing discussion about a Unity project. The latest updates are included in `{codeFile}` and `{instFile}`.",
                _ => ""
            };

            if (s_HasGitStatus)
                prompt += $" A git diff summary of recent changes is available in `{gitFile}`.";

            prompt += " Please use these files as context for this conversation.";
            return prompt;
        }
    }
}