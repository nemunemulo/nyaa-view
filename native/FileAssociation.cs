using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NyaaView
{
    public static class FileAssociation
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        public const string ProgIdImage = "NyaaView.Image";
        public const string ProgIdArchive = "NyaaView.Archive";
        public const string ProgIdPdf = "NyaaView.Pdf";

        // 분류별 지원 확장자 목록
        public static readonly string[] StandardImageExts = new string[]
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
        };

        public static readonly string[] ComicAndDocExts = new string[]
        {
            ".zip", ".cbz", ".pdf"
        };

        public static readonly string[] GraphicsExts = new string[]
        {
            ".psd", ".dds", ".tga", ".qoi", ".tif", ".tiff", ".jxr", ".wdp", ".hdp", ".ico", ".ppm", ".pgm", ".pnm"
        };

        public static List<string> GetAllSupportedExts()
        {
            var list = new List<string>();
            list.AddRange(StandardImageExts);
            list.AddRange(ComicAndDocExts);
            list.AddRange(GraphicsExts);
            return list;
        }

        public static string GetProgIdForExt(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return ProgIdImage;
            string lower = ext.ToLowerInvariant();
            if (lower == ".zip" || lower == ".cbz") return ProgIdArchive;
            if (lower == ".pdf") return ProgIdPdf;
            return ProgIdImage;
        }

        public static string GetDescriptionForProgId(string progId)
        {
            if (progId == ProgIdArchive) return "nyaa-view 만화 압축 파일";
            if (progId == ProgIdPdf) return "nyaa-view PDF 문서";
            return "nyaa-view 이미지 파일";
        }

        public static bool IsAssociated(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            string targetProgId = GetProgIdForExt(ext);
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ext))
                {
                    if (key != null)
                    {
                        object def = key.GetValue("");
                        if (def != null && string.Equals(def.ToString(), targetProgId, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ext + @"\OpenWithProgids"))
                {
                    if (key != null && key.GetValue(targetProgId) != null)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static bool RegisterAssociations(IEnumerable<string> exts)
        {
            string exePath = Application.ExecutablePath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;

            try
            {
                // 1. 공통 ProgIDs 등록
                EnsureProgId(ProgIdImage, "nyaa-view 이미지 파일", exePath);
                EnsureProgId(ProgIdArchive, "nyaa-view 만화 압축 파일", exePath);
                EnsureProgId(ProgIdPdf, "nyaa-view PDF 문서", exePath);

                // 2. Applications\nyaa-view.exe 등록
                using (RegistryKey appKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\nyaa-view.exe"))
                {
                    if (appKey != null)
                    {
                        appKey.SetValue("FriendlyAppName", "nyaa-view (냐-뷰)");
                        using (RegistryKey cmdKey = appKey.CreateSubKey(@"shell\open\command"))
                        {
                            if (cmdKey != null)
                            {
                                cmdKey.SetValue("", "\"" + exePath + "\" \"%1\"");
                            }
                        }
                        using (RegistryKey suppKey = appKey.CreateSubKey("SupportedTypes"))
                        {
                            if (suppKey != null)
                            {
                                foreach (string e in GetAllSupportedExts())
                                {
                                    suppKey.SetValue(e, "");
                                }
                            }
                        }
                    }
                }

                // 3. Windows 10/11 기본 앱(Capabilities & RegisteredApplications) 등록
                using (RegistryKey capKey = Registry.CurrentUser.CreateSubKey(@"Software\NyaaView\Capabilities"))
                {
                    if (capKey != null)
                    {
                        capKey.SetValue("ApplicationName", "nyaa-view");
                        capKey.SetValue("ApplicationDescription", "Windows 11 초경량 네이티브 만화 & 이미지 뷰어");
                        using (RegistryKey faKey = capKey.CreateSubKey("FileAssociations"))
                        {
                            if (faKey != null)
                            {
                                foreach (string ext in exts)
                                {
                                    string progId = GetProgIdForExt(ext);
                                    faKey.SetValue(ext, progId);
                                }
                            }
                        }
                    }
                }

                using (RegistryKey regApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
                {
                    if (regApps != null)
                    {
                        regApps.SetValue("nyaa-view", @"Software\NyaaView\Capabilities");
                    }
                }

                // 4. 각 확장자별 직접 연결 (HKCU\Software\Classes\<ext>)
                foreach (string ext in exts)
                {
                    string progId = GetProgIdForExt(ext);

                    using (RegistryKey extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ext))
                    {
                        if (extKey != null)
                        {
                            extKey.SetValue("", progId);
                            using (RegistryKey owKey = extKey.CreateSubKey("OpenWithProgids"))
                            {
                                if (owKey != null) owKey.SetValue(progId, "");
                            }
                        }
                    }

                    // Windows 탐색기 OpenWith 목록에도 즉각 등록
                    using (RegistryKey owp = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext + @"\OpenWithProgids"))
                    {
                        if (owp != null) owp.SetValue(progId, new byte[0], RegistryValueKind.None);
                    }
                }

                // 5. 윈도우 셸(탐색기) 아이콘 및 연결 캐시 즉각 새로고침 통보
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("확장자 등록 실패: " + ex.Message);
                return false;
            }
        }

        public static bool UnregisterAssociations(IEnumerable<string> exts)
        {
            try
            {
                foreach (string ext in exts)
                {
                    string progId = GetProgIdForExt(ext);

                    using (RegistryKey extKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ext, true))
                    {
                        if (extKey != null)
                        {
                            object cur = extKey.GetValue("");
                            if (cur != null && cur.ToString().StartsWith("NyaaView", StringComparison.OrdinalIgnoreCase))
                            {
                                extKey.DeleteValue("", false);
                            }
                            using (RegistryKey ow = extKey.OpenSubKey("OpenWithProgids", true))
                            {
                                if (ow != null) ow.DeleteValue(progId, false);
                            }
                        }
                    }

                    using (RegistryKey owp = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext + @"\OpenWithProgids", true))
                    {
                        if (owp != null) owp.DeleteValue(progId, false);
                    }

                    using (RegistryKey faKey = Registry.CurrentUser.OpenSubKey(@"Software\NyaaView\Capabilities\FileAssociations", true))
                    {
                        if (faKey != null) faKey.DeleteValue(ext, false);
                    }
                }

                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("확장자 해제 실패: " + ex.Message);
                return false;
            }
        }

        public static void OpenWindowsDefaultAppsSettings()
        {
            try
            {
                Process.Start("ms-settings:defaultapps");
            }
            catch
            {
                try
                {
                    Process.Start("control.exe", "/name Microsoft.DefaultPrograms");
                }
                catch { }
            }
        }

        private static void EnsureProgId(string progId, string description, string exePath)
        {
            using (RegistryKey progKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progKeyPath(progId)))
            {
                if (progKey != null)
                {
                    progKey.SetValue("", description);
                    using (RegistryKey iconKey = progKey.CreateSubKey("DefaultIcon"))
                    {
                        if (iconKey != null) iconKey.SetValue("", "\"" + exePath + "\",0");
                    }
                    using (RegistryKey cmdKey = progKey.CreateSubKey(@"shell\open\command"))
                    {
                        if (cmdKey != null) cmdKey.SetValue("", "\"" + exePath + "\" \"%1\"");
                    }
                }
            }
        }

        private static string progKeyPath(string progId)
        {
            return progId;
        }
    }
}
