using System;
using System.IO;
using System.Text;

namespace RimTalkStyleExpand
{
    public static class FileEncodingHelper
    {
        public static string ReadAllTextWithAutoDetect(string filePath)
        {
            var encoding = DetectEncoding(filePath);
            return File.ReadAllText(filePath, encoding);
        }
        
        public static Encoding DetectEncoding(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(stream))
            {
                var bom = reader.ReadBytes(4);
                
                // Check BOM
                if (bom.Length >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                {
                    return Encoding.UTF8;
                }
                
                if (bom.Length >= 2)
                {
                    if (bom[0] == 0xFF && bom[1] == 0xFE)
                    {
                        return Encoding.Unicode;
                    }
                    if (bom[0] == 0xFE && bom[1] == 0xFF)
                    {
                        return Encoding.BigEndianUnicode;
                    }
                }
                
                stream.Position = 0;
                
                var buffer = new byte[Math.Min(4096, stream.Length)];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                
                if (IsUtf8(buffer, bytesRead))
                {
                    return Encoding.UTF8;
                }
                
                try
                {
                    var gbkEncoding = Encoding.GetEncoding("GBK");
                    var gbkDecoded = gbkEncoding.GetString(buffer, 0, bytesRead);
                    
                    if (IsValidChineseText(gbkDecoded))
                    {
                        return gbkEncoding;
                    }
                }
                catch
                {
                }
                
                return Encoding.UTF8;
            }
        }
        
        private static bool IsUtf8(byte[] buffer, int length)
        {
            int i = 0;
            while (i < length)
            {
                byte b = buffer[i];
                
                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }
                
                int byteCount = 0;
                if ((b & 0xE0) == 0xC0)
                {
                    byteCount = 2;
                }
                else if ((b & 0xF0) == 0xE0)
                {
                    byteCount = 3;
                }
                else if ((b & 0xF8) == 0xF0)
                {
                    byteCount = 4;
                }
                else
                {
                    return false;
                }
                
                if (i + byteCount > length)
                {
                    return false;
                }
                
                for (int j = 1; j < byteCount; j++)
                {
                    if ((buffer[i + j] & 0xC0) != 0x80)
                    {
                        return false;
                    }
                }
                
                i += byteCount;
            }
            
            return true;
        }
        
        private static bool IsValidChineseText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            int chineseCount = 0;
            int garbageCount = 0;
            
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    chineseCount++;
                }
                else if (c < 0x20 && c != '\r' && c != '\n' && c != '\t')
                {
                    garbageCount++;
                }
                else if (c == 0xFFFD)
                {
                    garbageCount++;
                }
            }
            
            return garbageCount == 0 && chineseCount > 0;
        }
    }
}