
using System;
using System.IO;

class PathUtils
{
    // From: https://stackoverflow.com/a/703292
    // Modified to match .Net 5 GetRelativePath signature
    public static string GetRelativePath(string relativeTo, string path)
    {
        Uri pathUri = new Uri(path);
        // Folders must end in a slash
        if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            relativeTo += Path.DirectorySeparatorChar;
        }
        Uri folderUri = new Uri(relativeTo);
        return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
    }
}