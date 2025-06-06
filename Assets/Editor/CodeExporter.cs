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
        string cleanOutputPath = "Assets/_CyberPickle/Code/AllCode_Clean.txt";

        StringBuilder fullContent = new StringBuilder();
        StringBuilder cleanContent = new StringBuilder();

        // Recursively get all .cs files
        string[] csFiles = Directory.GetFiles(sourceFolder, "*.cs", SearchOption.AllDirectories);

        foreach (string filePath in csFiles)
        {
            string relativePath = filePath.Replace(sourceFolder, "").Replace("\\", "/");
            string fileContent = File.ReadAllText(filePath);

            // Generate full version (original logic)
            fullContent.AppendLine($"// File: {relativePath}");
            fullContent.AppendLine($"// Size: {fileContent.Length} characters");
            fullContent.AppendLine("// ---");
            fullContent.AppendLine(fileContent);
            fullContent.AppendLine("// --- END FILE ---");
            fullContent.AppendLine();

            // Generate clean version (stripped content)
            string cleanFileContent = StripCommentsAndDebugLogs(fileContent);
            if (!string.IsNullOrWhiteSpace(cleanFileContent))
            {
                cleanContent.AppendLine($"// File: {relativePath}");
                cleanContent.AppendLine("// ---");
                cleanContent.AppendLine(cleanFileContent);
                cleanContent.AppendLine("// --- END FILE ---");
                cleanContent.AppendLine();
            }
        }

        // Write both output files
        File.WriteAllText(outputPath, fullContent.ToString());
        File.WriteAllText(cleanOutputPath, cleanContent.ToString());

        AssetDatabase.Refresh();

        // Calculate and display statistics
        var fullSize = new FileInfo(outputPath).Length;
        var cleanSize = new FileInfo(cleanOutputPath).Length;
        var reductionPercent = (1.0 - (double)cleanSize / fullSize) * 100;

        Debug.Log($"Exported {csFiles.Length} .cs files");
        Debug.Log($"Full version: {outputPath} ({fullSize:N0} bytes)");
        Debug.Log($"Clean version: {cleanOutputPath} ({cleanSize:N0} bytes)");
        Debug.Log($"Size reduction: {reductionPercent:F1}%");
    }

    private static string StripCommentsAndDebugLogs(string content)
    {
        var lines = content.Split('\n');
        var output = new StringBuilder();

        bool inMultiLineComment = false;
        bool inStringLiteral = false;
        bool inXmlDocBlock = false;

        // Important comment prefixes to preserve
        string[] importantPrefixes = {
            "// TODO:",
            "// FIXME:",
            "// HACK:",
            "// NOTE:",
            "// IMPORTANT:",
            "// WARNING:",
            "// BUG:",
            "// REVIEW:"
        };

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            string processedLine = ProcessLine(line, ref inMultiLineComment, ref inStringLiteral, ref inXmlDocBlock, importantPrefixes);

            if (!string.IsNullOrWhiteSpace(processedLine))
            {
                output.AppendLine(processedLine);
            }
            else if (ShouldPreserveEmptyLine(lines, lineIndex))
            {
                output.AppendLine();
            }
        }

        return output.ToString();
    }

    private static string ProcessLine(string line, ref bool inMultiLineComment, ref bool inStringLiteral, ref bool inXmlDocBlock, string[] importantPrefixes)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        var trimmed = line.TrimStart();

        // Handle XML documentation comments (remove /// comments)
        if (trimmed.StartsWith("/// "))
        {
            inXmlDocBlock = true;
            return "";
        }

        if (inXmlDocBlock && !trimmed.StartsWith("/// ") && !string.IsNullOrWhiteSpace(trimmed))
        {
            inXmlDocBlock = false;
        }

        if (inXmlDocBlock)
            return "";

        // Keep important comments
        if (trimmed.StartsWith("//"))
        {
            foreach (var prefix in importantPrefixes)
            {
                if (trimmed.StartsWith(prefix))
                    return line;
            }
        }

        // Process the line for regular comments
        string result = ProcessLineForComments(line, ref inMultiLineComment, ref inStringLiteral);

        // Remove Debug.Log statements (but keep Debug.LogError and Debug.LogWarning)
        if (IsDebugLogLine(result))
            return "";

        return result;
    }

    private static string ProcessLineForComments(string line, ref bool inMultiLineComment, ref bool inStringLiteral)
    {
        var result = new StringBuilder();
        bool inSingleLineComment = false;

        for (int i = 0; i < line.Length; i++)
        {
            char current = line[i];
            char next = i + 1 < line.Length ? line[i + 1] : '\0';

            // Handle string literals (preserve everything inside strings)
            if (current == '"' && !inMultiLineComment && !inSingleLineComment)
            {
                bool isEscaped = false;
                int backslashCount = 0;
                for (int j = i - 1; j >= 0 && line[j] == '\\'; j--)
                    backslashCount++;
                isEscaped = backslashCount % 2 == 1;

                if (!isEscaped)
                    inStringLiteral = !inStringLiteral;
            }

            if (inStringLiteral)
            {
                result.Append(current);
                continue;
            }

            // Handle multi-line comments /* */
            if (current == '/' && next == '*' && !inSingleLineComment)
            {
                inMultiLineComment = true;
                i++;
                continue;
            }

            if (current == '*' && next == '/' && inMultiLineComment)
            {
                inMultiLineComment = false;
                i++;
                continue;
            }

            if (inMultiLineComment)
                continue;

            // Handle single line comments //
            if (current == '/' && next == '/' && !inSingleLineComment)
            {
                // Don't remove URLs (http://, https://)
                if (i >= 4 && line.Substring(i - 4, 4) == "http")
                {
                    result.Append(current);
                    continue;
                }

                inSingleLineComment = true;
                continue;
            }

            if (inSingleLineComment)
                continue;

            result.Append(current);
        }

        return result.ToString().TrimEnd();
    }

    private static bool IsDebugLogLine(string line)
    {
        var trimmed = line.Trim();

        // Remove Debug.Log but keep Debug.LogError and Debug.LogWarning
        return (trimmed.StartsWith("Debug.Log(") || trimmed.Contains("Debug.Log(")) &&
               !trimmed.Contains("Debug.LogError(") &&
               !trimmed.Contains("Debug.LogWarning(");
    }

    private static bool ShouldPreserveEmptyLine(string[] lines, int currentIndex)
    {
        if (currentIndex == 0 || currentIndex >= lines.Length - 1)
            return false;

        var prevLine = lines[currentIndex - 1].Trim();
        var nextLine = lines[currentIndex + 1].Trim();

        // Preserve empty lines between methods, classes, namespaces
        if (prevLine.EndsWith("}") && !string.IsNullOrEmpty(nextLine))
            return true;

        if (!string.IsNullOrEmpty(prevLine) &&
            (nextLine.StartsWith("public ") ||
             nextLine.StartsWith("private ") ||
             nextLine.StartsWith("protected ") ||
             nextLine.StartsWith("internal ")))
            return true;

        return false;
    }
}