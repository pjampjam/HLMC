using System;
using System.Windows.Forms;

namespace HLMCUpdater
{

    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }
        }

        private static void LogException(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "updater_error.log");
                File.WriteAllText(logPath, $"{DateTime.Now}: {ex.ToString()}");
            }
            catch
            {
                // If logging fails, ignore
            }
        }
    }
}