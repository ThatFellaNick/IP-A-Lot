// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// Program.cs owns application startup and keeps WinForms bootstrap code isolated
// from the scanner UI and network services.
// -----------------------------------------------------------------------------

using System;
using System.Windows.Forms;

namespace IPALot
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
