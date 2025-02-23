using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

public class CodeExporter
{
    [MenuItem("Tools/Export CS Files to Text")]
    public static void ExportCodeToText()
    {
        string sourceFolder = "Assets/_CyberPickle/Code/";
        string outputPath = "Assets/_CyberPickle/Code/AllCode.txt";
        StringBuilder content = new StringBuilder();

        // Recursively get all .cs files
        string[] csFiles = Directory.GetFiles(sourceFolder, "*.cs", SearchOption.AllDirectories);

        foreach (string filePath in csFiles)
        {
            string relativePath = filePath.Replace(sourceFolder, "").Replace("\\", "/");
            string fileContent = File.ReadAllText(filePath);

            // Add header with file name and path for context
            content.AppendLine($"// File: {relativePath}");
            content.AppendLine($"// Size: {fileContent.Length} characters");
            content.AppendLine("// ---");
            content.AppendLine(fileContent);
            content.AppendLine("// --- END FILE ---");
            content.AppendLine();
        }

        // Write to output file
        File.WriteAllText(outputPath, content.ToString());
        AssetDatabase.Refresh();

        Debug.Log($"Exported {csFiles.Length} .cs files to {outputPath}");
    }
}