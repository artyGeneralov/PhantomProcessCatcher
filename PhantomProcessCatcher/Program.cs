using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhantomProcessCatcher
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 



        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new MainWindow());
        }
    }
}
