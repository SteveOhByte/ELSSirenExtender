using System;
using System.IO;
using OhPluginEssentials;
using Rage;

namespace ELSSirenExtender
{
    public class Logger
    {
        private FileStream oStream;
        private StreamWriter writer;
        private TextWriter oldOut = Console.Out;
        
        private bool initialized = false;
        
        public Logger()
        {
            string dataFolderPath = AppDomain.CurrentDomain.BaseDirectory + @"\plugins\LSPDFR\ELSSirenExtender";
            // Create data folder if it doesn't exist
            if (!Directory.Exists(dataFolderPath)) Directory.CreateDirectory(dataFolderPath);

            string logsFolderPath = dataFolderPath + @"\Logs";
            // Create logs folder if it doesn't exist
            if (!Directory.Exists(logsFolderPath)) Directory.CreateDirectory(logsFolderPath);
            
            string latestLogPath = Path.Combine(logsFolderPath, "Latest.log");
            string archivedZipPath = Path.Combine(logsFolderPath, "Archived.zip");

            if (File.Exists(latestLogPath) && !File.Exists(archivedZipPath))
            {
                string newLogPath = Path.Combine(logsFolderPath, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "_Archive.log");
                File.Move(latestLogPath, newLogPath);
                Compression.Compress(newLogPath, archivedZipPath);
                File.Delete(newLogPath);
            }
            else if (File.Exists(latestLogPath) && File.Exists(archivedZipPath))
            {
                string tempFolder = Path.Combine(logsFolderPath, "Temp");
                Compression.Decompress(archivedZipPath, tempFolder);
                
                foreach (string file in Directory.GetFiles(tempFolder))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    fileName = fileName.Split('_')[0];
                    
                    DateTime fileDate = DateTime.ParseExact(fileName, "yyyy-MM-dd-HH-mm-ss", null);
                    
                    if (fileDate < DateTime.Now.AddDays(-7))
                    {
                        File.Delete(file);
                    }
                }
                
                string newLogPath = Path.Combine(tempFolder, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "_Archive.log");
                File.Move(latestLogPath, newLogPath);
                
                File.Delete(archivedZipPath);
                Compression.Compress(tempFolder, archivedZipPath);
                
                Directory.Delete(tempFolder, true);
            }
            
            oStream = new FileStream(latestLogPath, FileMode.Create, FileAccess.Write);
            writer = new StreamWriter(oStream) {AutoFlush = true};
            Console.SetOut(writer);
            
            Log("Logger initialized.");
            initialized = true;
        }
        
        // Just gets the time stamp for the log file.
        private string Time()
        {
            return DateTime.Now.ToString().Split(' ')[1] + " " + DateTime.Now.ToString().Split(' ')[2];
        }

        // This is the main method that writes a new log
        public void Log(string text)
        {
            if (!initialized) return;
            Console.WriteLine("[INFO] - " + Time() + " | " + text);
            Game.LogTrivial("ELSSirenExtender - " + text);
        }

        // Same as above, but for errors.
        public void Error(string text)
        {
            if (!initialized) return;
            Console.WriteLine("[ERROR] - " + Time() + " | " + text);
            Game.LogTrivial("ELSSirenExtender - " + text);
        }
    }
}