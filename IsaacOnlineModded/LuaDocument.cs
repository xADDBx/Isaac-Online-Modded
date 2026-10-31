using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IsaacModInstaller {
    public sealed record LuaDocument(List<string> Lines, string NewLine, bool HasBom) {
        public static LuaDocument Read(string path) {
            byte[] bytes = File.ReadAllBytes(path);
            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = new UTF8Encoding(false, true).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
            string newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
            return new LuaDocument(lines, newLine, hasBom);
        }

        public void Write(string path) {
            string text = string.Join(NewLine, Lines);
            string directory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException($"{path} has no parent directory.");
            string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try {
                File.WriteAllText(temporaryPath, text, new UTF8Encoding(HasBom));
                File.Move(temporaryPath, path, true);
            } finally {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
