using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using HWND = System.IntPtr;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class WinUtilitiesModified
    {
        /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
        /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
        /// 

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);


        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint FindWindow(string className, string processId);
        private static IDictionary<HWND, string> GetOpenWindows()
        {
            HWND shellWindow = GetShellWindow();
            Dictionary<HWND, string> windows = new Dictionary<HWND, string>();

            EnumWindows(delegate (HWND hWnd, int lParam)
            {
                if (hWnd == shellWindow) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                //if (length == 0) return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                GetWindowThreadProcessId(hWnd, out uint pid);

                windows[hWnd] = builder.ToString();
                return true;

            }, 0);

            return windows;
        }

        private static IDictionary<HWND, string> GetOpenChildWindows(HWND ptr)
        {
            HWND shellWindow = GetShellWindow();
            Dictionary<HWND, string> windows = new Dictionary<HWND, string>();

            EnumChildWindows(ptr, delegate (HWND hWnd, int lParam)
            {
                if (hWnd == shellWindow) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                //if (length == 0) return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                GetWindowThreadProcessId(hWnd, out uint pid);

                windows[hWnd] = builder.ToString();
                return true;

            }, 0);

            return windows;
        }

        private delegate bool EnumWindowsProc(HWND hWnd, int lParam);

        [DllImport("USER32.DLL", CharSet = CharSet.Auto)]
        private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("USER32.DLL", CharSet = CharSet.Auto)]
        private static extern bool EnumChildWindows(HWND ptr, EnumWindowsProc enumFunc, int lParam);

        [DllImport("USER32.DLL", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("USER32.DLL", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(HWND hWnd);

        [DllImport("USER32.DLL", CharSet = CharSet.Auto)]
        private static extern bool IsWindowVisible(HWND hWnd);

        [DllImport("USER32.DLL", CharSet = CharSet.Auto)]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        private const UInt32 WM_CLOSE = 0x0010;

        private static void CloseWindow(IntPtr hwnd)
        {
            SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Utility class to launch a separate thread that listens for popup windows. If the pop up window contains any of the keywords listed, 
        /// it is a warning window from eclipse and should be auto closed
        /// </summary>
        /// <param name="token"></param>
        /// <param name="fileName"></param>
        public static void LaunchWindowsClosingThread(CancellationToken token, string fileName)
        {
            Thread thread = new Thread(() =>
            {
                double timeoutDuration = 20; //min
                timeoutDuration *= 60; //min to seconds
                timeoutDuration *= 1000; //seconds to milliseconds
                CancellationToken ct = token;
                //StringBuilder sb = new StringBuilder();
                int pid = Process.GetCurrentProcess().Id;
                string prevMessage = "";
                Stopwatch timer = new Stopwatch();
                timer.Start();
                while (!ct.IsCancellationRequested && timer.ElapsedMilliseconds < (timeoutDuration))
                {
                    foreach (var x in GetOpenWindows())
                    {
                        var a = GetOpenChildWindows(x.Key);
                        GetWindowThreadProcessId(x.Key, out uint windowPid);
                        if (windowPid == pid)
                        {
                            //only report errors once!
                            foreach (var item in a)
                            {
                                string body = item.Value.ToLower();
                                if (!string.Equals(prevMessage, body))
                                {
                                    if (body.Contains("warning:") || body.Contains("error:") || body.Contains("couch")
                                        || body.Contains("invalidate")
                                        || body.Contains("electron")
                                        || body.Contains("body")
                                        || body.Contains("machine model in treatment plan ")
                                        || body.Contains("field has an opening that is smaller than the smallest measured")
                                        || body.Contains("density")
                                        || body.Contains("field")
                                        || body.Contains("calculat")
                                        || body.Contains("minimum hu value in the image")
                                        || body.Contains("conversion curve is correctly calibrated"))
                                    {
                                        CloseWindow(x.Key);
                                        prevMessage = body;
                                        UpdateErrorWarningsLog(body, fileName);
                                    }
                                }
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Helper method to update the error and warnings temp log file
        /// </summary>
        /// <param name="output"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool UpdateErrorWarningsLog(string output, string fileName)
        {
            if (Directory.Exists(Path.GetDirectoryName(fileName)))
            {
                output += Environment.NewLine + Environment.NewLine;
                File.AppendAllText(fileName, output);
                return false;
            }
            else return true;
        }
    }

}
