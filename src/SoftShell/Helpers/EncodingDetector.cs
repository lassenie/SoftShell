using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoftShell.Helpers
{
    internal static class EncodingDetector
    {
        public static Encoding GetTextEncoding(byte[] data)
        {
            var knownEncodings = System.Text.Encoding.GetEncodings().Select(enc => enc.GetEncoding()).OrderByDescending(enc => enc.GetPreamble()?.Length ?? 0).ToList();

            // First try all known encodings checking for matching BOM
            foreach (var encoding in knownEncodings)
            {
                try
                {
                    var bom = encoding.GetPreamble() ?? new byte[0];

                    // Matching BOM?
                    if ((bom.Length > 0) && (data.Length >= bom.Length) && data.Take(bom.Length).ToArray().Equals(bom))
                    {
                        return encoding;
                    }
                }
                catch { }
            }

            // Try common encodings without depending on BOM
            try
            {
                var _ = Encoding.UTF8.GetString(data, 0, data.Length);
                return Encoding.UTF8;
            }
            catch
            {
                try
                {
                    // UTF-16, LE
                    var _ = Encoding.Unicode.GetString(data, 0, data.Length);
                    return Encoding.Unicode;
                }
                catch
                {
                    try
                    {
                        // UTF-16, BE
                        var _ = Encoding.BigEndianUnicode.GetString(data, 0, data.Length);
                        return Encoding.Unicode;
                    }
                    catch
                    {
                        try
                        {
                            // UTF-32, LE
                            var _ = Encoding.UTF32.GetString(data, 0, data.Length);
                            return Encoding.UTF32;
                        }
                        catch
                        {
                            try
                            {
                                // UTF-32, BE
                                var encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
                                var _ = encoding.GetString(data, 0, data.Length);
                                return encoding;
                            }
                            catch
                            {
                                try
                                {
                                    var _ = Encoding.ASCII.GetString(data, 0, data.Length);
                                    return Encoding.ASCII;
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
            }

            // Giving up
            return null;
        }
    }
}
