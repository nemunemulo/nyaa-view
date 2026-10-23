using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace NyaaView
{
    // 보기 모드: 1장, 2장(좌→우), 2장(우→좌), 웹툰(세로 무한 연결)
    public enum ViewLayout
    {
        Single = 0,    // 1장 보기
        DualLtr = 1,   // 2장 보기 (좌에서 우 ▷)
        DualRtl = 2,   // 2장 보기 (우에서 좌 ◁ - 만화/망가 표준)
        Webtoon = 3    // 웹툰 연속 스크롤 (세로 무한 연결)
    }

    // 화면 맞춤 모드
    public enum DisplayFitMode
    {
        FitWindow = 0, // 화면에 맞춤 (가로/세로 전체 영역)
        FitWidth = 1,  // 가로 폭에 맞춤
        FitHeight = 2, // 세로 높이에 맞춤
        Original = 3   // 원본 크기 (100% 1:1)
    }

    // 캐시 메모리 제한 방식
    public enum CacheLimitMode
    {
        MegaBytes = 0, // 메모리 용량 기준 (MB 단위) - 기본값
        PageCount = 1  // 페이지 수 기준 (장 단위)
    }

    // 인접 압축 파일(ZIP) 선행 프리캐싱 모드
    public enum AdjacentArchiveCacheMode
    {
        Disabled = 0, // 꺼짐 (기본값)
        NextOnly = 1, // 뒷 파일 1개 (다음 권)
        PrevOnly = 2, // 앞 파일 1개 (이전 권)
        Both = 3      // 앞/뒤 1개씩 (이전 + 다음 권)
    }

    // 이미지 보간(리샘플링) 방식
    public enum InterpolationModeType
    {
        Bicubic = 0,         // 고품질 쌍입방 (Bicubic) - 부드러운 만화/일러스트 렌더링 (기본값)
        Bilinear = 1,        // 쌍선형 (Bilinear) - 가볍고 부드러운 고속 보간
        NearestNeighbor = 2  // 최근접 이웃 (Nearest Neighbor) - 픽셀아트/도트 100% 원본 보존
    }

    // 1장/2장 보기 페이지 넘김 전환 효과
    public enum PageTransitionEffect
    {
        None = 0,     // 끄기 (즉시 전환 - 기본값)
        Fade = 1,     // 암전 / 페이드 (Fade through Black)
        Slide = 2,    // 좌우 슬라이드 (Push Slide)
        PageTurn = 3  // 페이지 넘김 (Page Turn / Wipe)
    }

    public class Settings
    {
        // 다음 / 이전 이미지 넘기기 키 (복수 키 지원)
        public List<Keys> NextKeys { get; set; }
        public List<Keys> PrevKeys { get; set; }

        // 마우스 휠 동작: 0 = 확대/축소(Zoom), 1 = 페이지 넘기기(PageFlip)
        public int WheelMode { get; set; }
        // 마우스 휠 방향 반전
        public bool WheelInvert { get; set; }
        // Ctrl + 마우스 휠 시 반대 동작 수행
        public bool WheelCtrlAlternate { get; set; }

        // 주요 기능 단축키
        public Keys ExifKey { get; set; }
        public Keys ExplorerKey { get; set; }
        public Keys FitKey { get; set; }
        public Keys RotateKey { get; set; }
        public Keys WheelToggleKey { get; set; }

        // 캐시 메모리 제한 방식: MegaBytes(0, 기본), PageCount(1)
        public CacheLimitMode CacheMode { get; set; }
        // 캐시 최대 메모리 용량 (MB 단위, 기본값 256MB, 최대 64GB까지 지원)
        public int CacheLimitMb { get; set; }
        // 압축파일 메모리 프리캐시 개수 (장 단위, 1~50장)
        public int PrecacheCount { get; set; }

        // 인접 압축 파일(ZIP) 선행 프리캐싱: Disabled(0), NextOnly(1), PrevOnly(2), Both(3)
        public AdjacentArchiveCacheMode AdjacentZipCache { get; set; }

        // 최근 열기 목록 설정
        public bool EnableRecentList { get; set; }
        public int RecentMaxCount { get; set; }
        public List<string> RecentItems { get; set; }

        // 외부 편집기 설정
        public string ExternalEditorPath { get; set; }

        // UI 자동 숨김 (상단 툴바 / 하단 바 마우스 호버 자동 표시)
        public bool AutoHideUi { get; set; }

        // 만화책/서적 보기 모드: Single(0), DualLtr(1), DualRtl(2)
        public ViewLayout ViewMode { get; set; }

        // 화면 맞춤 모드: FitWindow(0), FitWidth(1), FitHeight(2), Original(3)
        public DisplayFitMode FitMode { get; set; }

        // 작은 그림 크게 보기 (맞춤 시 100% 이상 확대 허용)
        public bool StretchSmallImages { get; set; }

        // 펼침면(스프레드) 스마트 단독 감지 (가로세로 비율 > 1.15일 때 1장 단독 표시) - 기본값: 꺼짐
        public bool SmartSpreadDetection { get; set; }

        // 웹툰 모드 마우스 휠 부드러운 스크롤 (감속 보간 부드러운 이동) - 기본값: 켜짐
        public bool WebtoonSmoothScroll { get; set; }

        // 웹툰 모드 마우스 드래그 관성 스크롤 (플릭 가속도 슬라이딩) - 기본값: 켜짐
        public bool WebtoonInertialDrag { get; set; }

        // 미니맵 내비게이션 표시 여부
        public bool ShowMinimap { get; set; }

        // 이미지 보간(리샘플링) 방식: Bicubic(0), Bilinear(1), NearestNeighbor(2)
        public InterpolationModeType InterpolationMode { get; set; }

        // 1장/2장 보기 페이지 넘김 전환 효과: None(0), Fade(1), Slide(2), PageTurn(3)
        public PageTransitionEffect PageTransition { get; set; }

        // 창 크기 및 위치 저장 설정
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }

        // 첫 실행 빠른 시작 튜토리얼 표시 여부
        public bool FirstRunTutorialShown { get; set; }

        public Settings()
        {
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            FirstRunTutorialShown = false;
            NextKeys = new List<Keys> { Keys.Right, Keys.PageDown, Keys.D };
            PrevKeys = new List<Keys> { Keys.Left, Keys.PageUp, Keys.A };

            WheelMode = 0; // 0: Zoom, 1: PageFlip
            WheelInvert = false;
            WheelCtrlAlternate = true;

            ExifKey = Keys.Tab;
            ExplorerKey = Keys.E;
            FitKey = Keys.F;
            RotateKey = Keys.R;
            WheelToggleKey = Keys.Z;

            CacheMode = CacheLimitMode.MegaBytes;
            CacheLimitMb = 1024;
            PrecacheCount = 10;
            AdjacentZipCache = AdjacentArchiveCacheMode.Disabled;

            EnableRecentList = true;
            RecentMaxCount = 10;
            RecentItems = new List<string>();
            ExternalEditorPath = "";
            AutoHideUi = true;
            ViewMode = ViewLayout.Single;
            FitMode = DisplayFitMode.FitWindow;
            StretchSmallImages = false;
            SmartSpreadDetection = false;
            WebtoonSmoothScroll = true;
            WebtoonInertialDrag = true;
            ShowMinimap = true;
            InterpolationMode = InterpolationModeType.Bicubic;
            PageTransition = PageTransitionEffect.None; // 기본값: 끄기 (즉시 전환)

            WindowX = -1;
            WindowY = -1;
            WindowWidth = 1280;
            WindowHeight = 840;
            WindowMaximized = false;
        }

        public void AddRecentItem(string path)
        {
            if (!EnableRecentList || string.IsNullOrEmpty(path)) return;
            if (RecentItems == null) RecentItems = new List<string>();

            for (int i = RecentItems.Count - 1; i >= 0; i--)
            {
                if (RecentItems[i].Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    RecentItems.RemoveAt(i);
                }
            }
            RecentItems.Insert(0, path);

            int limit = Math.Max(1, Math.Min(10, RecentMaxCount));
            while (RecentItems.Count > limit)
            {
                RecentItems.RemoveAt(RecentItems.Count - 1);
            }

            Save();
        }

        public void ClearRecentItems()
        {
            if (RecentItems != null) RecentItems.Clear();
            Save();
        }

        public static string GetConfigFilePath()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFile = Path.Combine(exeDir, "nyaa-settings.ini");

            try
            {
                // 로컬 디렉터리 쓰기 권한 테스트
                string testFile = Path.Combine(exeDir, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return localFile;
            }
            catch
            {
                // 쓰기 불가 시 AppData 사용
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "nyaa-view");
                if (!Directory.Exists(appData))
                {
                    try { Directory.CreateDirectory(appData); } catch { }
                }
                return Path.Combine(appData, "nyaa-settings.ini");
            }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            string path = GetConfigFilePath();

            if (!File.Exists(path))
            {
                return s;
            }

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("["))
                        continue;

                    int eqIdx = trimmed.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    string key = trimmed.Substring(0, eqIdx).Trim();
                    string val = trimmed.Substring(eqIdx + 1).Trim();

                    if (key.Equals("NextKeys", StringComparison.OrdinalIgnoreCase))
                    {
                        var parsed = ParseKeysList(val);
                        if (parsed.Count > 0) s.NextKeys = parsed;
                    }
                    else if (key.Equals("PrevKeys", StringComparison.OrdinalIgnoreCase))
                    {
                        var parsed = ParseKeysList(val);
                        if (parsed.Count > 0) s.PrevKeys = parsed;
                    }
                    else if (key.Equals("WheelMode", StringComparison.OrdinalIgnoreCase))
                    {
                        int wm;
                        if (int.TryParse(val, out wm)) s.WheelMode = wm;
                    }
                    else if (key.Equals("WheelInvert", StringComparison.OrdinalIgnoreCase))
                    {
                        bool wi;
                        if (bool.TryParse(val, out wi)) s.WheelInvert = wi;
                    }
                    else if (key.Equals("WheelCtrlAlternate", StringComparison.OrdinalIgnoreCase))
                    {
                        bool wca;
                        if (bool.TryParse(val, out wca)) s.WheelCtrlAlternate = wca;
                    }
                    else if (key.Equals("ExifKey", StringComparison.OrdinalIgnoreCase))
                    {
                        Keys k;
                        if (TryParseKey(val, out k)) s.ExifKey = k;
                    }
                    else if (key.Equals("ExplorerKey", StringComparison.OrdinalIgnoreCase))
                    {
                        Keys k;
                        if (TryParseKey(val, out k)) s.ExplorerKey = k;
                    }
                    else if (key.Equals("FitKey", StringComparison.OrdinalIgnoreCase))
                    {
                        Keys k;
                        if (TryParseKey(val, out k)) s.FitKey = k;
                    }
                    else if (key.Equals("RotateKey", StringComparison.OrdinalIgnoreCase))
                    {
                        Keys k;
                        if (TryParseKey(val, out k)) s.RotateKey = k;
                    }
                    else if (key.Equals("WheelToggleKey", StringComparison.OrdinalIgnoreCase))
                    {
                        Keys k;
                        if (TryParseKey(val, out k)) s.WheelToggleKey = k;
                    }
                    else if (key.Equals("CacheMode", StringComparison.OrdinalIgnoreCase))
                    {
                        int cm;
                        if (int.TryParse(val, out cm)) s.CacheMode = (CacheLimitMode)Math.Max(0, Math.Min(1, cm));
                    }
                    else if (key.Equals("CacheLimitMb", StringComparison.OrdinalIgnoreCase))
                    {
                        int clmb;
                        if (int.TryParse(val, out clmb)) s.CacheLimitMb = Math.Max(32, Math.Min(65536, clmb));
                    }
                    else if (key.Equals("PrecacheCount", StringComparison.OrdinalIgnoreCase))
                    {
                        int pc;
                        if (int.TryParse(val, out pc)) s.PrecacheCount = Math.Max(1, Math.Min(50, pc));
                    }
                    else if (key.Equals("AdjacentZipCache", StringComparison.OrdinalIgnoreCase))
                    {
                        int azc;
                        if (int.TryParse(val, out azc)) s.AdjacentZipCache = (AdjacentArchiveCacheMode)Math.Max(0, Math.Min(3, azc));
                    }
                    else if (key.Equals("EnableRecentList", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) s.EnableRecentList = b;
                    }
                    else if (key.Equals("RecentMaxCount", StringComparison.OrdinalIgnoreCase))
                    {
                        int rmc;
                        if (int.TryParse(val, out rmc)) s.RecentMaxCount = Math.Max(1, Math.Min(10, rmc));
                    }
                    else if (key.Equals("RecentItems", StringComparison.OrdinalIgnoreCase))
                    {
                        s.RecentItems = ParseRecentList(val);
                    }
                    else if (key.Equals("ExternalEditorPath", StringComparison.OrdinalIgnoreCase))
                    {
                        s.ExternalEditorPath = val;
                    }
                    else if (key.Equals("AutoHideUi", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) s.AutoHideUi = b;
                    }
                    else if (key.Equals("ViewMode", StringComparison.OrdinalIgnoreCase))
                    {
                        int vm;
                        if (int.TryParse(val, out vm)) s.ViewMode = (ViewLayout)Math.Max(0, Math.Min(3, vm));
                    }
                    else if (key.Equals("FitMode", StringComparison.OrdinalIgnoreCase))
                    {
                        int fm;
                        if (int.TryParse(val, out fm)) s.FitMode = (DisplayFitMode)Math.Max(0, Math.Min(3, fm));
                    }
                    else if (key.Equals("StretchSmallImages", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) s.StretchSmallImages = b;
                    }
                    else if (key.Equals("SmartSpreadDetection", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) s.SmartSpreadDetection = b;
                    }
                    else if (key.Equals("WebtoonSmoothScroll", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) s.WebtoonSmoothScroll = b;
                    }
                    else if (key.Equals("WebtoonInertialDrag", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) s.WebtoonInertialDrag = b;
                    }
                    else if (key.Equals("ShowMinimap", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) s.ShowMinimap = b;
                    }
                    else if (key.Equals("InterpolationMode", StringComparison.OrdinalIgnoreCase))
                    {
                        int im;
                        if (int.TryParse(val, out im)) s.InterpolationMode = (InterpolationModeType)Math.Max(0, Math.Min(2, im));
                    }
                    else if (key.Equals("PageTransition", StringComparison.OrdinalIgnoreCase))
                    {
                        int pt;
                        if (int.TryParse(val, out pt)) s.PageTransition = (PageTransitionEffect)Math.Max(0, Math.Min(3, pt));
                    }
                    else if (key.Equals("WindowX", StringComparison.OrdinalIgnoreCase))
                    {
                        int wx;
                        if (int.TryParse(val, out wx)) s.WindowX = wx;
                    }
                    else if (key.Equals("WindowY", StringComparison.OrdinalIgnoreCase))
                    {
                        int wy;
                        if (int.TryParse(val, out wy)) s.WindowY = wy;
                    }
                    else if (key.Equals("WindowWidth", StringComparison.OrdinalIgnoreCase))
                    {
                        int ww;
                        if (int.TryParse(val, out ww) && ww >= 320) s.WindowWidth = ww;
                    }
                    else if (key.Equals("WindowHeight", StringComparison.OrdinalIgnoreCase))
                    {
                        int wh;
                        if (int.TryParse(val, out wh) && wh >= 240) s.WindowHeight = wh;
                    }
                    else if (key.Equals("WindowMaximized", StringComparison.OrdinalIgnoreCase))
                    {
                        bool wm;
                        if (bool.TryParse(val, out wm)) s.WindowMaximized = wm;
                    }
                    else if (key.Equals("FirstRunTutorialShown", StringComparison.OrdinalIgnoreCase))
                    {
                        bool ft;
                        if (bool.TryParse(val, out ft)) s.FirstRunTutorialShown = ft;
                    }
                }
            }
            catch { }

            return s;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigFilePath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[NyaaView]");
                sb.AppendLine("NextKeys=" + SerializeKeysList(NextKeys));
                sb.AppendLine("PrevKeys=" + SerializeKeysList(PrevKeys));
                sb.AppendLine("WheelMode=" + WheelMode);
                sb.AppendLine("WheelInvert=" + WheelInvert);
                sb.AppendLine("WheelCtrlAlternate=" + WheelCtrlAlternate);
                sb.AppendLine("ExifKey=" + ExifKey);
                sb.AppendLine("ExplorerKey=" + ExplorerKey);
                sb.AppendLine("FitKey=" + FitKey);
                sb.AppendLine("RotateKey=" + RotateKey);
                sb.AppendLine("WheelToggleKey=" + WheelToggleKey);
                sb.AppendLine("CacheMode=" + (int)CacheMode);
                sb.AppendLine("CacheLimitMb=" + CacheLimitMb);
                sb.AppendLine("PrecacheCount=" + PrecacheCount);
                sb.AppendLine("AdjacentZipCache=" + (int)AdjacentZipCache);
                sb.AppendLine("EnableRecentList=" + EnableRecentList);
                sb.AppendLine("RecentMaxCount=" + RecentMaxCount);
                sb.AppendLine("RecentItems=" + SerializeRecentList(RecentItems));
                sb.AppendLine("ExternalEditorPath=" + (ExternalEditorPath ?? ""));
                sb.AppendLine("AutoHideUi=" + AutoHideUi);
                sb.AppendLine("ViewMode=" + (int)ViewMode);
                sb.AppendLine("FitMode=" + (int)FitMode);
                sb.AppendLine("StretchSmallImages=" + StretchSmallImages);
                sb.AppendLine("SmartSpreadDetection=" + SmartSpreadDetection);
                sb.AppendLine("WebtoonSmoothScroll=" + WebtoonSmoothScroll);
                sb.AppendLine("WebtoonInertialDrag=" + WebtoonInertialDrag);
                sb.AppendLine("ShowMinimap=" + ShowMinimap);
                sb.AppendLine("InterpolationMode=" + (int)InterpolationMode);
                sb.AppendLine("PageTransition=" + (int)PageTransition);
                sb.AppendLine("WindowX=" + WindowX);
                sb.AppendLine("WindowY=" + WindowY);
                sb.AppendLine("WindowWidth=" + WindowWidth);
                sb.AppendLine("WindowHeight=" + WindowHeight);
                sb.AppendLine("WindowMaximized=" + WindowMaximized);
                sb.AppendLine("FirstRunTutorialShown=" + FirstRunTutorialShown);

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static List<string> ParseRecentList(string raw)
        {
            List<string> list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;
            string[] parts = raw.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                string t = p.Trim();
                if (!string.IsNullOrEmpty(t) && !list.Contains(t))
                    list.Add(t);
            }
            return list;
        }

        private static string SerializeRecentList(List<string> list)
        {
            if (list == null || list.Count == 0) return "";
            return string.Join("|", list.ToArray());
        }

        private static List<Keys> ParseKeysList(string raw)
        {
            List<Keys> list = new List<Keys>();
            string[] parts = raw.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                Keys k;
                if (TryParseKey(p.Trim(), out k))
                {
                    if (!list.Contains(k)) list.Add(k);
                }
            }
            return list;
        }

        private static string SerializeKeysList(List<Keys> keys)
        {
            if (keys == null || keys.Count == 0) return "";
            List<string> names = new List<string>();
            foreach (Keys k in keys)
            {
                names.Add(k.ToString());
            }
            return string.Join(",", names.ToArray());
        }

        public static string GetKeyDisplayName(Keys key)
        {
            switch (key)
            {
                case Keys.PageDown: return "PageDown (다음 페이지)";
                case Keys.PageUp: return "PageUp (이전 페이지)";
                case Keys.Return: return "Enter (엔터)";
                case Keys.Back: return "Backspace (백스페이스)";
                case Keys.Right: return "Right (오른쪽 방향키 ▶)";
                case Keys.Left: return "Left (왼쪽 방향키 ◀)";
                case Keys.Up: return "Up (위쪽 방향키 ▲)";
                case Keys.Down: return "Down (아래쪽 방향키 ▼)";
                case Keys.Space: return "Space (스페이스바)";
                default: return key.ToString();
            }
        }

        public static bool TryParseKey(string str, out Keys key)
        {
            try
            {
                key = (Keys)Enum.Parse(typeof(Keys), str, true);
                return true;
            }
            catch
            {
                key = Keys.None;
                return false;
            }
        }
    }

    public class KeyItem
    {
        public Keys Key { get; set; }

        public KeyItem(Keys key)
        {
            Key = key;
        }

        public override string ToString()
        {
            return Settings.GetKeyDisplayName(Key);
        }

        public override bool Equals(object obj)
        {
            KeyItem other = obj as KeyItem;
            if (other != null) return other.Key == this.Key;
            return false;
        }

        public override int GetHashCode()
        {
            return (int)Key;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    public static class SystemMemoryHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private static int _detectedInstalledRamGb = -1;

        // 시스템에 장착된 물리 RAM 용량 (GB 단위로 올림하여 프로그램 시작 시 단 1회만 감지)
        public static int GetInstalledRamGb()
        {
            if (_detectedInstalledRamGb > 0) return _detectedInstalledRamGb;

            try
            {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    double rawGb = (double)memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    // 내장 그래픽(iGPU) 및 하드웨어 예약 메모리로 일부 빠지더라도
                    // Math.Ceiling을 적용하여 실제 장착된 표준 물리 RAM 규격(16GB 등)으로 1회만 산출
                    int gb = (int)Math.Ceiling(rawGb);
                    if (gb < 2) gb = 2;
                    _detectedInstalledRamGb = gb;
                    return _detectedInstalledRamGb;
                }
            }
            catch { }

            _detectedInstalledRamGb = 8; // fallback 8GB
            return _detectedInstalledRamGb;
        }

        // 1GB = 1024MB 기준으로 16GB 장착 시 정확히 16,384 MB 반환
        public static long GetTotalPhysicalMemoryMb()
        {
            return (long)GetInstalledRamGb() * 1024L;
        }

        // 슬라이더 최대 허용치 (RAM 절반): 16GB 장착 시 정확히 8,192 MB 반환
        public static int GetHalfPhysicalMemoryMb()
        {
            return GetInstalledRamGb() * 512;
        }

        // 현재 OS의 사용 가능한 물리 메모리 (MB 단위)
        public static long GetAvailablePhysicalMemoryMb()
        {
            try
            {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return (long)(memStatus.ullAvailPhys / (1024L * 1024L));
                }
            }
            catch { }
            return -1L;
        }
    }
}
