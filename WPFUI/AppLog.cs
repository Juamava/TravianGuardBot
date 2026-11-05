using System;
using System.IO;
using System.Text;

namespace WPFUI
{
    public static class AppLog
    {
        private static readonly object Gate = new object();

        public static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "logs");

        public static void Write(string category, string message, Exception exception = null)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.Append(" [");
                builder.Append(category);
                builder.Append("] ");
                builder.Append(message);
                if (exception != null)
                {
                    builder.AppendLine();
                    builder.Append(exception);
                }

                lock (Gate)
                {
                    File.AppendAllText(Path.Combine(DirectoryPath, "app.log"), builder.AppendLine().ToString());
                }
            }
            catch
            {
                // Opening the program matters more than keeping this line.
            }
        }
    }
}
