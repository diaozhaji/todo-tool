using System;
using System.Threading;
using System.Windows.Forms;

namespace TodoTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex single = new Mutex(true, "TodoTool_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("待办清单已在运行中（可能已最小化到系统托盘）。",
                        "待办清单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                GC.KeepAlive(single);
            }
        }
    }
}
