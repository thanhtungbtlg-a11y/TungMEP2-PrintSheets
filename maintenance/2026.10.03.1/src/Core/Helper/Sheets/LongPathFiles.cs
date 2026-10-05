using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VP_RevitAPI.Helper.Sheets;

// Scoped Unicode Win32 I/O. No host-wide AppContext, registry or Revit config changes.
public static class LongPathFiles
{
    private const uint InvalidAttributes = 0xffffffff;
    private const uint DirectoryAttribute = 0x10;

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Empty path.", nameof(path));
        path = path.Replace('/', '\\');
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) path = @"\\" + path.Substring(8);
        else if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path.Substring(4);
        if (path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.IndexOf('\0') >= 0)
            throw new ArgumentException("Device paths are not output paths.", nameof(path));
        var buffer = new StringBuilder(32768);
        uint count = GetFullPathNameW(path, (uint)buffer.Capacity, buffer, IntPtr.Zero);
        if (count == 0) throw Error("Resolve path", path);
        if (count >= buffer.Capacity || count > 32750) throw new PathTooLongException("Output path exceeds Windows extended-path limits.");
        string result = buffer.ToString();
        string root = Root(result);
        foreach (string part in result.Substring(root.Length).Split('\\'))
        {
            if (part.Length > 255) throw new PathTooLongException("A path component exceeds 255 characters.");
            if (part.EndsWith(" ", StringComparison.Ordinal) || part.EndsWith(".", StringComparison.Ordinal) ||
                part.IndexOfAny(new[] { '<', '>', ':', '"', '|', '?', '*' }) >= 0)
                throw new ArgumentException("Invalid output path component: " + part);
            foreach (char ch in part) if (ch < 32) throw new ArgumentException("Control character in output path.");
        }
        return result.Length > root.Length ? result.TrimEnd('\\') : root;
    }

    private static string Root(string path)
    {
        if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == '\\') return path.Substring(0, 3);
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            int serverEnd = path.IndexOf('\\', 2);
            int shareEnd = serverEnd >= 0 ? path.IndexOf('\\', serverEnd + 1) : -1;
            if (serverEnd > 2 && serverEnd < path.Length - 1)
                return shareEnd < 0 ? path : path.Substring(0, shareEnd);
        }
        throw new ArgumentException("Expected a drive or UNC share path.");
    }

    public static string ToNativePath(string path)
    {
        string full = Normalize(path);
        return full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full.Substring(2) : @"\\?\" + full;
    }

    public static string FileName(string path)
    {
        return path.Substring(path.LastIndexOf('\\') + 1);
    }

    public static string Parent(string path)
    {
        string full = Normalize(path);
        string root = Root(full);
        if (full.Length <= root.Length) return null;
        int last = full.LastIndexOf('\\');
        return last < root.Length ? root : full.Substring(0, last);
    }

    public static bool FileExists(string path) => Exists(path, false);
    public static bool DirectoryExists(string path) => Exists(path, true);
    private static bool Exists(string path, bool directory)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        uint attributes = GetFileAttributesW(ToNativePath(path));
        return attributes != InvalidAttributes && ((attributes & DirectoryAttribute) != 0) == directory;
    }

    public static void CreateDirectory(string path)
    {
        string full = Normalize(path);
        if (DirectoryExists(full)) return;
        var pending = new Stack<string>();
        string current = full;
        while (!DirectoryExists(current))
        {
            string parent = Parent(current);
            if (parent == null) throw new IOException("Drive or network share is unavailable: " + current);
            pending.Push(current);
            current = parent;
        }
        while (pending.Count > 0)
        {
            current = pending.Pop();
            if (!CreateDirectoryW(ToNativePath(current), IntPtr.Zero) && !DirectoryExists(current))
                throw Error("Create directory", current);
        }
    }

    public static long Length(string path)
    {
        if (!GetFileAttributesExW(ToNativePath(path), 0, out AttributeData data)) throw Error("Read file size", path);
        return ((long)data.SizeHigh << 32) | data.SizeLow;
    }

    public static FileStream OpenRead(string path) => Open(path, 0x80000000, 3, FileAccess.Read);
    public static FileStream CreateNew(string path) => Open(path, 0x40000000, 1, FileAccess.Write);
    private static FileStream Open(string path, uint access, uint disposition, FileAccess fileAccess)
    {
        SafeFileHandle handle = CreateFileW(ToNativePath(path), access, 0, IntPtr.Zero, disposition, 0x80, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Error("Open file", path);
            handle.Dispose();
            throw error;
        }
        try { return new FileStream(handle, fileAccess); }
        catch { handle.Dispose(); throw; }
    }

    public static void Copy(string source, string target, bool overwrite)
    {
        if (!CopyFileW(ToNativePath(source), ToNativePath(target), !overwrite)) throw Error("Copy file", target);
    }
    public static void Move(string source, string target)
    {
        if (!MoveFileW(ToNativePath(source), ToNativePath(target))) throw Error("Move file", target);
    }
    public static void Replace(string source, string target, string backup, bool ignoreMetadataErrors)
    {
        if (!ReplaceFileW(ToNativePath(target), ToNativePath(source), ToNativePath(backup), ignoreMetadataErrors ? 2u : 0u, IntPtr.Zero, IntPtr.Zero))
            throw Error("Replace file", target);
    }
    public static void Delete(string path)
    {
        if (!DeleteFileW(ToNativePath(path)))
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 2 && error != 3) throw Error("Delete file", path, error);
        }
    }

    public static List<string> EnumerateFiles(string folder, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern.IndexOfAny(new[] { '\\', '/', ':' }) >= 0)
            throw new ArgumentException("Expected a file search pattern.");
        string full = Normalize(folder);
        var result = new List<string>();
        IntPtr handle = FindFirstFileW(ToNativePath(full) + "\\" + pattern, out FindData data);
        if (handle == new IntPtr(-1))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 2) return result;
            throw Error("List files", folder, error);
        }
        try
        {
            do
            {
                if ((data.Attributes & DirectoryAttribute) == 0) result.Add(full.TrimEnd('\\') + "\\" + data.Name);
            } while (FindNextFileW(handle, out data));
            int error = Marshal.GetLastWin32Error();
            if (error != 18) throw Error("List files", folder, error);
        }
        finally { FindClose(handle); }
        return result;
    }

    private static IOException Error(string operation, string path, int code = -1)
    {
        if (code < 0) code = Marshal.GetLastWin32Error();
        var cause = new Win32Exception(code);
        return new IOException(operation + ": " + cause.Message + " (" + path + ")", cause);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeData
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint SizeHigh, SizeLow;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FindData
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint SizeHigh, SizeLow, Reserved0, Reserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string AlternateName;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFullPathNameW(string path, uint size, StringBuilder buffer, IntPtr filePart);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFileAttributesW(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool GetFileAttributesExW(string path, int level, out AttributeData data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool CreateDirectoryW(string path, IntPtr security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint attributes, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool CopyFileW(string source, string target, bool failIfExists);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool MoveFileW(string source, string target);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool ReplaceFileW(string target, string source, string backup, uint flags, IntPtr exclude, IntPtr reserved);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool DeleteFileW(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr FindFirstFileW(string pattern, out FindData data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool FindNextFileW(IntPtr handle, out FindData data);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool FindClose(IntPtr handle);
}
