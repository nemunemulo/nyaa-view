using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace NyaaView
{
    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        private static extern int SendMessageString(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        private const int EM_SETCUEBANNER = 0x1501;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int WM_NCLBUTTONDBLCLK = 0xA3;
        private const int HT_CAPTION = 0x2;

        // 환경 설정
        private Settings _settings;

        // UI 컨트롤
        private Panel _topBar;
        private Panel _gifBar;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _viewStatusLabel;
        private ToolStripStatusLabel _fitStatusLabel;
        private ToolStripStatusLabel _stretchStatusLabel;
        private ToolStripStatusLabel _interpolationStatusLabel;
        private ToolStripStatusLabel _wheelStatusLabel;
        private ToolStripStatusLabel _autoHideStatusLabel;
        private ToolStripStatusLabel _zoomLabel;

        // 하단 페이지 이동 & 검색 바 컨트롤
        private Panel _navBar;
        private Label _navModeBadge;
        private Button _navPrevBtn;
        private Button _navNextBtn;
        private TrackBar _navSlider;
        private Label _navPageLbl;
        private TextBox _navSearchBox;
        private Button _navGoBtn;
        private bool _isNavSliderUpdating = false;

        private Button _openBtn;
        private Button _folderBtn;
        private Button _infoBtn;
        private Button _viewModeBtn;
        private Button _fitMenuBtn;
        private Button _stretchBtn;
        private Button _filterMenuBtn;
        private Button _rotBtn;
        private Button _autoHideTopBtn;
        private Button _settingsBtn;

        // 드롭다운 컨텍스트 메뉴
        private ContextMenuStrip _viewModeContext;
        private ToolStripMenuItem _menuItemSingle;
        private ToolStripMenuItem _menuItemDualRtl;
        private ToolStripMenuItem _menuItemDualLtr;
        private ToolStripMenuItem _menuItemWebtoon;
        private ToolStripMenuItem _menuItemSmartSpread;
        private ContextMenuStrip _fitModeContext;

        // 웹툰 모드 (세로 무한 연속 스크롤) 전용 상태
        private float _webtoonScrollY = 0f;
        private float _webtoonTargetScrollY = 0f;
        private Timer _smoothScrollTimer;
        private float _webtoonZoom = 1.0f;
        private float _webtoonPanX = 0f;
        private bool _isDraggingWebtoon = false;
        private Point _dragStartWebtoonPoint;
        private float _dragStartWebtoonScrollY = 0f;
        private float _dragStartWebtoonPanX = 0f;
        private int _webtoonPrecacheGen = 0;
        private int _webtoonBoundaryCounter = 0;
        private readonly object _webtoonLock = new object();
        private readonly Dictionary<int, Bitmap> _webtoonCache = new Dictionary<int, Bitmap>();
        private readonly Dictionary<int, Size> _webtoonPageSizes = new Dictionary<int, Size>();

        // 프레임리스 창 테두리 리사이즈 서브클래스 필터 목록
        private readonly List<BorderHitTestNativeWindow> _borderHitFilters = new List<BorderHitTestNativeWindow>();

        // 창 제어 버튼 (프레임리스 타이틀바 제어)
        private Button _minBtn;
        private Button _maxBtn;
        private Button _closeBtn;

        // GIF 전용 컨트롤
        private Button _gifPlayBtn;
        private Button _gifPrevBtn;
        private Button _gifNextBtn;
        private Button _gifHideBtn;
        private TrackBar _gifSlider;
        private Label _gifFrameLbl;
        private ComboBox _gifSpeedCombo;
        private Timer _gifTimer;

        // 마우스 호버 자동 숨김 컨트롤
        private Timer _hoverTimer;
        private bool _contextMenuOpen;
        private bool _suppressBottomHover;

        // 필터 메뉴
        private ContextMenuStrip _filterContext;
        private FilterType _currentFilter = FilterType.Off;

        // 뷰어 그래픽스 상태
        private Bitmap _rawBitmap;       // 1번째 원본 비트맵
        private Bitmap _filteredBitmap;  // 1번째 필터 적용된 비트맵
        private Bitmap _rawBitmap2;      // 2번째 원본 비트맵 (2장 모드)
        private Bitmap _filteredBitmap2; // 2번째 필터 적용된 비트맵 (2장 모드)
        private bool _isDualPageDisplayed = false;
        private int _secondIndex = -1;
        private string _secondImageName = "";
        private float _scale = 1.0f;
        private PointF _pan = new PointF(0, 0);
        private int _rotation = 0;       // 0, 90, 180, 270
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private bool _emptyMouseDown = false;
        private Point _emptyMouseDownPos;

        // GIF 프레임 상태
        private bool _isGif = false;
        private int _gifFrameCount = 1;
        private int _gifCurrentFrame = 0;
        private int[] _gifDelays;
        private FrameDimension _gifDimension;
        private float _gifSpeed = 1.0f;

        // 지원 파일 포맷 목록
        public static readonly HashSet<string> SupportedImageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp",
            ".tif", ".tiff", ".jxr", ".wdp", ".hdp", ".ico",
            ".tga", ".qoi", ".psd", ".dds",
            ".ppm", ".pgm", ".pnm"
        };
        public static readonly HashSet<string> SupportedArchiveExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".cbz"
        };

        public static bool IsArchiveFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return SupportedArchiveExts.Contains(Path.GetExtension(path));
        }

        public static bool IsPdfFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        // 아카이브 & PDF & 목록 상태
        private ZipEngine _zipEngine;
        private PdfEngine _pdfEngine;
        private List<string> _singleImages = new List<string>();
        private int _currentIndex = -1;
        private ExplorerForm _explorerForm;

        // 인접 압축 파일 & PDF 탐색 상태
        private string _prevZipPath = null;
        private string _nextZipPath = null;
        private string _prevPdfPath = null;
        private string _nextPdfPath = null;
        private ZipEngine _cachedPrevZipEngine = null;
        private ZipEngine _cachedNextZipEngine = null;
        private int _adjacentPrecacheId = 0;
        private readonly object _adjacentLock = new object();

        // 미니맵(Minimap) 상태
        private bool _showMinimap = true;
        private Rectangle _minimapRect = Rectangle.Empty;
        private bool _isDraggingMinimap = false;

        // EXIF & 이미지 정보 오버레이 상태
        private bool _showExifOverlay = false;
        private ImageInfo _currentImageInfo = null;
        private Rectangle _exifCloseRect = Rectangle.Empty;

        public MainForm()
        {
            _settings = Settings.Load();
            _showMinimap = _settings.ShowMinimap;
            InitializeComponent();
            ApplyDarkTitleBar();
            UpdateTopButtonLabels();
            UpdateWheelStatusLabel();
            UpdateAutoHideUi();
            RestoreWindowBounds();
        }

        private void InitializeComponent()
        {
            this.Text = "nyaa-view (냐-뷰) - Local Image & Archive Viewer";
            this.Size = new Size(1280, 840);
            this.MinimumSize = new Size(700, 500);
            this.FormBorderStyle = FormBorderStyle.None; // 프레임리스 통합 타이틀바 (전체 화면 자동 숨김 지원)
            this.BackColor = Color.FromArgb(18, 19, 22);
            this.ForeColor = Color.FromArgb(240, 240, 245);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
            this.KeyPreview = true;
            this.AllowDrop = true;

            _zipEngine = new ZipEngine();
            _zipEngine.CacheMode = _settings.CacheMode;
            _zipEngine.CacheLimitMb = _settings.CacheLimitMb;
            _zipEngine.CacheWindow = _settings.PrecacheCount;

            _pdfEngine = new PdfEngine();
            _pdfEngine.CacheLimitMb = _settings.CacheLimitMb;

            _smoothScrollTimer = new Timer();
            _smoothScrollTimer.Interval = 15;
            _smoothScrollTimer.Tick += SmoothScrollTimer_Tick;

            // 1. 상단 통합 툴바 & 타이틀바
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(26, 28, 34),
                Padding = new Padding(8, 0, 0, 0)
            };
            _topBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
            _topBar.DoubleClick += (s, e) => ToggleMaximize();

            // 윈도우 창 제어 버튼들 (닫기, 최대화, 최소화 - 우측 끝에 배치)
            _closeBtn = new Button
            {
                Text = "\u2715",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Width = 46,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(210, 215, 225),
                Cursor = Cursors.Hand
            };
            _closeBtn.FlatAppearance.BorderSize = 0;
            _closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            _closeBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(200, 15, 30);
            _closeBtn.Click += (s, e) => this.Close();

            _maxBtn = new Button
            {
                Text = (this.WindowState == FormWindowState.Maximized) ? "\u29C9" : "\u25A1",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Width = 46,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(210, 215, 225),
                Cursor = Cursors.Hand
            };
            _maxBtn.FlatAppearance.BorderSize = 0;
            _maxBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 55, 68);
            _maxBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 44, 55);
            _maxBtn.Click += (s, e) => ToggleMaximize();

            _minBtn = new Button
            {
                Text = "\u2014",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Width = 46,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(210, 215, 225),
                Cursor = Cursors.Hand
            };
            _minBtn.FlatAppearance.BorderSize = 0;
            _minBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 55, 68);
            _minBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 44, 55);
            _minBtn.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            // 툴바 액션 버튼 컨테이너 (남은 공간 전체 차지 및 창 드래그 지원)
            Panel actionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 8, 4)
            };
            actionsPanel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
            actionsPanel.DoubleClick += (s, e) => ToggleMaximize();

            Label logo = new Label
            {
                Text = "nyaa-view",
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 159, 243),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 12, 0),
                Cursor = Cursors.Hand
            };
            logo.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
            logo.DoubleClick += (s, e) => ToggleMaximize();

            _openBtn = CreateTopButton("파일 열기", (s, e) => OpenFileDialogHandler());
            _folderBtn = CreateTopButton("탐색기 (E)", (s, e) => ToggleExplorer());
            
            // 보기 모드 드롭다운 버튼 (1장 / 2장 우→좌 / 2장 좌→우)
            _viewModeBtn = CreateTopButton("보기: 1장 ▼", (s, e) => ShowViewModeMenu());
            SetupViewModeMenu();

            // 맞춤 모드 드롭다운 버튼 (화면맞춤 / 폭맞춤 / 높이맞춤 / 원본)
            _fitMenuBtn = CreateTopButton("맞춤: 화면맞춤 ▼", (s, e) => ShowFitModeMenu());
            SetupFitModeMenu();

            // 작은 그림 크게 보기 (확대) 토글 버튼
            _stretchBtn = CreateTopButton("확대: OFF (S)", (s, e) => ToggleStretchSmallImages());

            _rotBtn = CreateTopButton("회전 (R)", (s, e) => RotateCw());

            // 필터 드롭다운 버튼
            _filterMenuBtn = CreateTopButton("필터: 끄기 ▼", (s, e) => ShowFilterMenu());
            SetupFilterMenu();

            _infoBtn = CreateTopButton("정보 (Tab)", (s, e) => ToggleExifOverlay());

            // 우측 설정 & UI 자동숨김 버튼
            _settingsBtn = new Button
            {
                Text = "설정 (F2)",
                AutoSize = true,
                Height = 30,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Padding = new Padding(4, 0, 4, 0),
                Margin = new Padding(3, 0, 0, 0)
            };
            _settingsBtn.FlatAppearance.BorderSize = 0;
            _settingsBtn.Click += (s, e) => OpenSettingsDialog();

            _autoHideTopBtn = new Button
            {
                Text = _settings.AutoHideUi ? "자동 (H)" : "고정 (H)",
                AutoSize = true,
                Height = 30,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = _settings.AutoHideUi ? Color.FromArgb(48, 70, 110) : Color.FromArgb(45, 49, 60),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Padding = new Padding(4, 0, 4, 0),
                Margin = new Padding(3, 0, 0, 0)
            };
            _autoHideTopBtn.FlatAppearance.BorderSize = 0;
            _autoHideTopBtn.Click += (s, e) => ToggleAutoHide();

            actionsPanel.Controls.Add(_settingsBtn);
            actionsPanel.Controls.Add(_autoHideTopBtn);
            actionsPanel.Controls.Add(_infoBtn);
            actionsPanel.Controls.Add(_filterMenuBtn);
            actionsPanel.Controls.Add(_rotBtn);
            actionsPanel.Controls.Add(_stretchBtn);
            actionsPanel.Controls.Add(_fitMenuBtn);
            actionsPanel.Controls.Add(_viewModeBtn);
            actionsPanel.Controls.Add(_folderBtn);
            actionsPanel.Controls.Add(_openBtn);
            actionsPanel.Controls.Add(logo);

            _topBar.Controls.Add(actionsPanel);
            _topBar.Controls.Add(_minBtn);
            _topBar.Controls.Add(_maxBtn);
            _topBar.Controls.Add(_closeBtn);

            // 2. GIF 컨트롤 바
            _gifBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                BackColor = Color.FromArgb(24, 26, 32),
                Visible = false,
                Padding = new Padding(12, 8, 12, 8)
            };

            _gifPrevBtn = new Button { Text = "◀ 1프레임", Width = 75, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(36, 40, 52), Cursor = Cursors.Hand };
            _gifPrevBtn.FlatAppearance.BorderSize = 0;
            _gifPrevBtn.Click += (s, e) => StepGifFrame(-1);

            _gifPlayBtn = new Button { Text = "일시정지", Width = 75, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(48, 70, 110), Cursor = Cursors.Hand };
            _gifPlayBtn.FlatAppearance.BorderSize = 0;
            _gifPlayBtn.Click += (s, e) => ToggleGifPlay();

            _gifNextBtn = new Button { Text = "1프레임 ▶", Width = 75, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(36, 40, 52), Cursor = Cursors.Hand };
            _gifNextBtn.FlatAppearance.BorderSize = 0;
            _gifNextBtn.Click += (s, e) => StepGifFrame(1);

            _gifFrameLbl = new Label { Text = "1 / 1", Width = 80, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Left, ForeColor = Color.FromArgb(100, 160, 255), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            _gifSlider = new TrackBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 0, TickStyle = TickStyle.None, BackColor = Color.FromArgb(24, 26, 32) };
            _gifSlider.Scroll += (s, e) => SeekGifFrame(_gifSlider.Value);

            _gifHideBtn = new Button
            {
                Text = "▼ 숨기기",
                Width = 75,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(200, 205, 220),
                BackColor = Color.FromArgb(36, 40, 52),
                Cursor = Cursors.Hand
            };
            _gifHideBtn.FlatAppearance.BorderSize = 0;
            _gifHideBtn.Click += (s, e) =>
            {
                _suppressBottomHover = true;
                _gifBar.Visible = false;
                if (_navBar != null) _navBar.Visible = false;
                _statusStrip.Visible = false;
            };

            _gifSpeedCombo = new ComboBox { Dock = DockStyle.Right, Width = 65, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(40, 44, 52), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            _gifSpeedCombo.Items.AddRange(new object[] { "0.5x", "1.0x", "1.5x", "2.0x" });
            _gifSpeedCombo.SelectedIndex = 1;
            _gifSpeedCombo.SelectedIndexChanged += (s, e) =>
            {
                string sel = _gifSpeedCombo.SelectedItem.ToString().Replace("x", "");
                _gifSpeed = float.Parse(sel);
                if (_gifTimer.Enabled && _gifDelays != null && _gifCurrentFrame < _gifDelays.Length)
                {
                    _gifTimer.Interval = Math.Max(10, (int)(_gifDelays[_gifCurrentFrame] / _gifSpeed));
                }
            };

            _gifBar.Controls.Add(_gifSlider);
            _gifBar.Controls.Add(_gifFrameLbl);
            _gifBar.Controls.Add(_gifNextBtn);
            _gifBar.Controls.Add(_gifPlayBtn);
            _gifBar.Controls.Add(_gifPrevBtn);
            _gifBar.Controls.Add(_gifHideBtn);
            _gifBar.Controls.Add(_gifSpeedCombo);

            // 2.5 하단 페이지 탐색 & 검색 바 (Page Navigation & Scrubber Bar)
            _navBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(20, 22, 28),
                Visible = false,
                Padding = new Padding(6, 3, 6, 3)
            };
            _navBar.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(45, 50, 62), 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, _navBar.Width, 0);
                }
            };

            FlowLayoutPanel navLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            _navModeBadge = new Label
            {
                Text = "[파일]",
                AutoSize = false,
                Width = 52,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255),
                Margin = new Padding(0, 1, 4, 1)
            };

            _navPrevBtn = new Button
            {
                Text = "◀",
                Width = 30,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(36, 40, 52),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(2, 2, 2, 2)
            };
            _navPrevBtn.FlatAppearance.BorderSize = 0;
            _navPrevBtn.Click += (s, e) => PrevImage();

            _navNextBtn = new Button
            {
                Text = "▶",
                Width = 30,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(36, 40, 52),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(2, 2, 6, 2)
            };
            _navNextBtn.FlatAppearance.BorderSize = 0;
            _navNextBtn.Click += (s, e) => NextImage();

            navLeft.Controls.Add(_navModeBadge);
            navLeft.Controls.Add(_navPrevBtn);
            navLeft.Controls.Add(_navNextBtn);

            FlowLayoutPanel navRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            _navPageLbl = new Label
            {
                Text = "0 / 0",
                AutoSize = false,
                Width = 90,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 175, 255),
                Margin = new Padding(4, 1, 4, 1)
            };

            _navSearchBox = new TextBox
            {
                Width = 100,
                Height = 24,
                BackColor = Color.FromArgb(30, 33, 42),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(2, 3, 2, 3)
            };
            _navSearchBox.HandleCreated += (s, e) =>
            {
                SendMessageString(_navSearchBox.Handle, EM_SETCUEBANNER, 0, "페이지/검색...");
            };
            _navSearchBox.GotFocus += (s, e) => _navSearchBox.SelectAll();
            _navSearchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ExecutePageOrSearchJump();
                }
            };

            _navGoBtn = new Button
            {
                Text = "이동 ⏎",
                Width = 52,
                Height = 25,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(48, 70, 110),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Margin = new Padding(2, 2, 4, 2)
            };
            _navGoBtn.FlatAppearance.BorderSize = 0;
            _navGoBtn.Click += (s, e) => ExecutePageOrSearchJump();

            navRight.Controls.Add(_navPageLbl);
            navRight.Controls.Add(_navSearchBox);
            navRight.Controls.Add(_navGoBtn);

            Panel navCenter = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(6, 1, 6, 1)
            };

            _navSlider = new TrackBar
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 1,
                Value = 1,
                TickStyle = TickStyle.None,
                BackColor = Color.FromArgb(20, 22, 28)
            };
            _navSlider.Scroll += NavSlider_Scroll;

            navCenter.Controls.Add(_navSlider);

            _navBar.Controls.Add(navCenter);
            _navBar.Controls.Add(navRight);
            _navBar.Controls.Add(navLeft);

            // 3. 상태바
            _statusStrip = new StatusStrip { BackColor = Color.FromArgb(26, 28, 34), ForeColor = Color.FromArgb(170, 175, 190) };
            _statusLabel = new ToolStripStatusLabel("준비됨 (파일 또는 ZIP을 드래그하여 놓으세요)") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

            _viewStatusLabel = new ToolStripStatusLabel("보기: [1장] (V)")
            {
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched,
                Padding = new Padding(6, 0, 6, 0),
                IsLink = true,
                LinkColor = Color.FromArgb(170, 175, 190)
            };
            _viewStatusLabel.Click += (s, e) => CycleViewMode();

            _fitStatusLabel = new ToolStripStatusLabel("맞춤: [화면맞춤] (F)")
            {
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched,
                Padding = new Padding(6, 0, 6, 0),
                IsLink = true,
                LinkColor = Color.FromArgb(170, 175, 190)
            };
            _fitStatusLabel.Click += (s, e) => CycleFitMode();

            _stretchStatusLabel = new ToolStripStatusLabel("확대: [OFF] (S)")
            {
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched,
                Padding = new Padding(6, 0, 6, 0),
                IsLink = true,
                LinkColor = Color.FromArgb(140, 145, 160)
            };
            _stretchStatusLabel.Click += (s, e) => ToggleStretchSmallImages();

            _interpolationStatusLabel = new ToolStripStatusLabel("보간: [Bicubic] (I)")
            {
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched,
                Padding = new Padding(6, 0, 6, 0),
                IsLink = true,
                LinkColor = Color.FromArgb(170, 175, 190)
            };
            _interpolationStatusLabel.Click += (s, e) => CycleInterpolationMode();

            _autoHideStatusLabel = new ToolStripStatusLabel(_settings.AutoHideUi ? "UI: [자동숨김] (H)" : "UI: [고정됨] (H)")
            {
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched,
                Padding = new Padding(8, 0, 8, 0),
                IsLink = true,
                LinkColor = _settings.AutoHideUi ? Color.FromArgb(255, 159, 243) : Color.FromArgb(170, 175, 190)
            };
            _autoHideStatusLabel.Click += (s, e) => ToggleAutoHide();
            
            _wheelStatusLabel = new ToolStripStatusLabel("휠: [확대/축소] (Z)")
            {
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched,
                Padding = new Padding(8, 0, 8, 0),
                IsLink = true,
                LinkColor = Color.FromArgb(170, 175, 190)
            };
            _wheelStatusLabel.Click += (s, e) => ToggleWheelMode();

            _zoomLabel = new ToolStripStatusLabel("100%") { Width = 60 };

            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Items.Add(_viewStatusLabel);
            _statusStrip.Items.Add(_fitStatusLabel);
            _statusStrip.Items.Add(_stretchStatusLabel);
            _statusStrip.Items.Add(_interpolationStatusLabel);
            _statusStrip.Items.Add(_autoHideStatusLabel);
            _statusStrip.Items.Add(_wheelStatusLabel);
            _statusStrip.Items.Add(_zoomLabel);

            this.Controls.Add(_gifBar);
            this.Controls.Add(_navBar);
            this.Controls.Add(_statusStrip);
            this.Controls.Add(_topBar);

            // GIF 타이머
            _gifTimer = new Timer();
            _gifTimer.Tick += GifTimer_Tick;

            // 마우스 호버 감지 타이머
            _hoverTimer = new Timer();
            _hoverTimer.Interval = 35;
            _hoverTimer.Tick += HoverTimer_Tick;
            _hoverTimer.Start();

            // 마우스 & 뷰어 이벤트
            this.Paint += MainForm_Paint;
            this.MouseDown += MainForm_MouseDown;
            this.MouseMove += MainForm_MouseMove;
            this.MouseUp += MainForm_MouseUp;
            this.MouseWheel += MainForm_MouseWheel;
            this.DoubleClick += (s, e) =>
            {
                if (_filteredBitmap == null)
                {
                    ToggleMaximize();
                }
                else
                {
                    if (_settings.ViewMode == ViewLayout.Webtoon)
                    {
                        if (Math.Abs(_webtoonZoom - 1.0f) > 0.01f || Math.Abs(_webtoonPanX) > 0.01f)
                        {
                            _webtoonZoom = 1.0f;
                            _webtoonPanX = 0f;
                            UpdateZoomLabelWebtoon();
                            this.Invalidate();
                        }
                        else
                        {
                            ToggleMaximize();
                        }
                    }
                    else
                    {
                        FitToScreen();
                    }
                }
            };
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;
            this.Resize += (s, e) =>
            {
                if (_maxBtn != null)
                {
                    _maxBtn.Text = (this.WindowState == FormWindowState.Maximized) ? "\u29C9" : "\u25A1";
                }
                if (_filteredBitmap != null)
                {
                    if (_settings.ViewMode == ViewLayout.Webtoon)
                    {
                        ClampWebtoonScroll();
                        this.Invalidate();
                    }
                    else
                    {
                        FitToScreen();
                    }
                }
                else
                {
                    this.Invalidate();
                }
            };

            // 모든 자식 컨트롤(상단 툴바, 하단 바 등)에 창 테두리 리사이즈 투과 필터 부착
            AttachBorderResizeFilter(this);
        }

        private Button CreateTopButton(string text, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 30,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Padding = new Padding(4, 0, 4, 0),
                Margin = new Padding(0, 0, 2, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private void UpdateTopButtonLabels()
        {
            if (_folderBtn != null) _folderBtn.Text = string.Format("탐색기 ({0})", _settings.ExplorerKey);
            if (_infoBtn != null) _infoBtn.Text = string.Format("정보 ({0})", _settings.ExifKey);
            if (_rotBtn != null) _rotBtn.Text = string.Format("회전 ({0})", _settings.RotateKey);
            UpdateViewAndFitStatusLabels();
        }

        private void SetupViewModeMenu()
        {
            _viewModeContext = new ContextMenuStrip();
            _viewModeContext.Renderer = new DarkMenuRenderer();
            _viewModeContext.ShowCheckMargin = true;
            _viewModeContext.ShowImageMargin = false;
            _viewModeContext.BackColor = Color.FromArgb(24, 26, 32);
            _viewModeContext.ForeColor = Color.FromArgb(240, 243, 250);
            _viewModeContext.Font = new Font("Segoe UI", 9F);

            _menuItemSingle = new ToolStripMenuItem("1장 단면 보기 [Ctrl+1]", null, (s, e) => SetViewMode(ViewLayout.Single));
            _menuItemDualRtl = new ToolStripMenuItem("2장 양면 보기 (우에서 좌 ◁ - 만화/망가) [Ctrl+2]", null, (s, e) => SetViewMode(ViewLayout.DualRtl));
            _menuItemDualLtr = new ToolStripMenuItem("2장 양면 보기 (좌에서 우 ▷ - 일반 서적) [Ctrl+3]", null, (s, e) => SetViewMode(ViewLayout.DualLtr));
            _menuItemWebtoon = new ToolStripMenuItem("웹툰 모드 (세로 무한 연속 스크롤) [Ctrl+4]", null, (s, e) => SetViewMode(ViewLayout.Webtoon));

            _menuItemSmartSpread = new ToolStripMenuItem("펼침면(스프레드) 스마트 단독 감지 [Shift+V]", null, (s, e) => ToggleSmartSpreadDetection());

            _viewModeContext.Items.Add(_menuItemSingle);
            _viewModeContext.Items.Add(_menuItemDualRtl);
            _viewModeContext.Items.Add(_menuItemDualLtr);
            _viewModeContext.Items.Add(_menuItemWebtoon);
            _viewModeContext.Items.Add(new ToolStripSeparator());
            _viewModeContext.Items.Add(_menuItemSmartSpread);
            ApplyDarkMenuTheme(_viewModeContext);
        }

        private void ShowViewModeMenu()
        {
            if (_viewModeBtn != null && _viewModeContext != null)
            {
                if (_menuItemSingle != null) _menuItemSingle.Checked = (_settings.ViewMode == ViewLayout.Single);
                if (_menuItemDualRtl != null) _menuItemDualRtl.Checked = (_settings.ViewMode == ViewLayout.DualRtl);
                if (_menuItemDualLtr != null) _menuItemDualLtr.Checked = (_settings.ViewMode == ViewLayout.DualLtr);
                if (_menuItemWebtoon != null) _menuItemWebtoon.Checked = (_settings.ViewMode == ViewLayout.Webtoon);
                if (_menuItemSmartSpread != null) _menuItemSmartSpread.Checked = _settings.SmartSpreadDetection;

                _viewModeContext.Show(_viewModeBtn, new Point(0, _viewModeBtn.Height));
            }
        }

        private void SetupFitModeMenu()
        {
            _fitModeContext = new ContextMenuStrip();
            _fitModeContext.Renderer = new DarkMenuRenderer();
            _fitModeContext.ShowCheckMargin = true;
            _fitModeContext.ShowImageMargin = false;
            _fitModeContext.BackColor = Color.FromArgb(24, 26, 32);
            _fitModeContext.ForeColor = Color.FromArgb(240, 243, 250);
            _fitModeContext.Font = new Font("Segoe UI", 9F);

            _fitModeContext.Items.Add("화면 맞춤 (Fit to Screen) [F]", null, (s, e) => SetFitMode(DisplayFitMode.FitWindow));
            _fitModeContext.Items.Add("가로 폭 맞춤 (Fit to Width) [W]", null, (s, e) => SetFitMode(DisplayFitMode.FitWidth));
            _fitModeContext.Items.Add("세로 높이 맞춤 (Fit to Height) [Shift+F]", null, (s, e) => SetFitMode(DisplayFitMode.FitHeight));
            _fitModeContext.Items.Add("원본 크기 (Original 100%) [O]", null, (s, e) => SetFitMode(DisplayFitMode.Original));
            _fitModeContext.Items.Add(new ToolStripSeparator());
            var stretchItem = new ToolStripMenuItem("작은 그림 크게 보기 (확대 허용) [S]");
            stretchItem.Checked = _settings.StretchSmallImages;
            stretchItem.Click += (s, e) => ToggleStretchSmallImages();
            _fitModeContext.Items.Add(stretchItem);
            ApplyDarkMenuTheme(_fitModeContext);
        }

        private void ShowFitModeMenu()
        {
            if (_fitMenuBtn != null && _fitModeContext != null)
                _fitModeContext.Show(_fitMenuBtn, new Point(0, _fitMenuBtn.Height));
        }

        public void SetViewMode(ViewLayout mode)
        {
            if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
            _settings.ViewMode = mode;
            if (mode == ViewLayout.Webtoon)
            {
                _settings.FitMode = DisplayFitMode.FitWidth;
                _webtoonZoom = 1.0f;
                _webtoonPanX = 0f;
                _webtoonScrollY = CalculateScrollYForIndex(_currentIndex);
                _webtoonTargetScrollY = _webtoonScrollY;
            }
            else
            {
                FreeWebtoonCache();
            }
            _settings.Save();
            UpdateViewAndFitStatusLabels();
            UpdateTopButtonLabels();
            DisplayCurrentItem();
        }

        public void CycleViewMode()
        {
            if (_settings.ViewMode == ViewLayout.Single) SetViewMode(ViewLayout.DualRtl);
            else if (_settings.ViewMode == ViewLayout.DualRtl) SetViewMode(ViewLayout.DualLtr);
            else if (_settings.ViewMode == ViewLayout.DualLtr) SetViewMode(ViewLayout.Webtoon);
            else SetViewMode(ViewLayout.Single);
        }

        public void ToggleSmartSpreadDetection()
        {
            _settings.SmartSpreadDetection = !_settings.SmartSpreadDetection;
            _settings.Save();
            DisplayCurrentItem();
        }

        public void SetFitMode(DisplayFitMode mode)
        {
            if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                // 웹툰 모드에서는 화면 맞춤이 가로폭에 맞춤으로 고정됨
                _settings.FitMode = DisplayFitMode.FitWidth;
                _settings.Save();
                UpdateViewAndFitStatusLabels();
                this.Invalidate();
                return;
            }
            _settings.FitMode = mode;
            _settings.Save();
            UpdateViewAndFitStatusLabels();
            FitToScreen();
        }

        public void CycleFitMode()
        {
            if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                // 웹툰 모드에서는 가로폭 맞춤 고정 유지
                return;
            }
            int next = ((int)_settings.FitMode + 1) % 4;
            SetFitMode((DisplayFitMode)next);
        }

        public void ToggleStretchSmallImages()
        {
            _settings.StretchSmallImages = !_settings.StretchSmallImages;
            _settings.Save();
            UpdateViewAndFitStatusLabels();
            FitToScreen();
        }

        public void SetInterpolationMode(InterpolationModeType mode)
        {
            _settings.InterpolationMode = mode;
            _settings.Save();
            UpdateViewAndFitStatusLabels();
            this.Invalidate();
        }

        public void CycleInterpolationMode()
        {
            int next = ((int)_settings.InterpolationMode + 1) % 3;
            SetInterpolationMode((InterpolationModeType)next);
        }

        private void UpdateViewAndFitStatusLabels()
        {
            if (_viewStatusLabel != null)
            {
                string vText = "보기: [1장] (V)";
                if (_settings.ViewMode == ViewLayout.DualRtl) vText = "보기: [2장(우→좌)] (V)";
                else if (_settings.ViewMode == ViewLayout.DualLtr) vText = "보기: [2장(좌→우)] (V)";
                else if (_settings.ViewMode == ViewLayout.Webtoon) vText = "보기: [웹툰(세로연속)] (V)";
                _viewStatusLabel.Text = vText;
                _viewStatusLabel.ForeColor = (_settings.ViewMode == ViewLayout.Webtoon) ? Color.FromArgb(78, 205, 196) : ((_settings.ViewMode != ViewLayout.Single) ? Color.FromArgb(255, 159, 243) : Color.FromArgb(170, 175, 190));
            }

            if (_fitStatusLabel != null)
            {
                string fText = "맞춤: [화면맞춤] (F)";
                if (_settings.ViewMode == ViewLayout.Webtoon)
                {
                    fText = "맞춤: [가로폭(웹툰고정)]";
                }
                else
                {
                    switch (_settings.FitMode)
                    {
                        case DisplayFitMode.FitWidth: fText = "맞춤: [폭맞춤] (W)"; break;
                        case DisplayFitMode.FitHeight: fText = "맞춤: [높이맞춤] (Shift+F)"; break;
                        case DisplayFitMode.Original: fText = "맞춤: [원본100%] (O)"; break;
                    }
                }
                _fitStatusLabel.Text = fText;
                _fitStatusLabel.ForeColor = (_settings.ViewMode == ViewLayout.Webtoon) ? Color.FromArgb(78, 205, 196) : ((_settings.FitMode != DisplayFitMode.FitWindow) ? Color.FromArgb(100, 180, 255) : Color.FromArgb(170, 175, 190));
            }

            if (_stretchStatusLabel != null)
            {
                _stretchStatusLabel.Text = _settings.StretchSmallImages ? "확대: [ON] (S)" : "확대: [OFF] (S)";
                _stretchStatusLabel.ForeColor = _settings.StretchSmallImages ? Color.FromArgb(255, 200, 80) : Color.FromArgb(140, 145, 160);
            }

            if (_interpolationStatusLabel != null)
            {
                string iText = "보간: [Bicubic] (I)";
                switch (_settings.InterpolationMode)
                {
                    case InterpolationModeType.Bilinear: iText = "보간: [Bilinear] (I)"; break;
                    case InterpolationModeType.NearestNeighbor: iText = "보간: [Nearest] (I)"; break;
                }
                _interpolationStatusLabel.Text = iText;
                _interpolationStatusLabel.ForeColor = (_settings.InterpolationMode != InterpolationModeType.Bicubic) ? Color.FromArgb(160, 220, 100) : Color.FromArgb(170, 175, 190);
            }

            if (_viewModeBtn != null)
            {
                string bText = "보기: 1장 ▼";
                if (_settings.ViewMode == ViewLayout.DualRtl) bText = "보기: 2장(우→좌) ▼";
                else if (_settings.ViewMode == ViewLayout.DualLtr) bText = "보기: 2장(좌→우) ▼";
                else if (_settings.ViewMode == ViewLayout.Webtoon) bText = "보기: 웹툰(세로연속) ▼";
                _viewModeBtn.Text = bText;
                _viewModeBtn.BackColor = (_settings.ViewMode == ViewLayout.Webtoon) ? Color.FromArgb(32, 80, 75) : ((_settings.ViewMode != ViewLayout.Single) ? Color.FromArgb(48, 70, 110) : Color.FromArgb(45, 49, 60));
            }

            if (_fitMenuBtn != null)
            {
                string fText = "맞춤: 화면맞춤 ▼";
                if (_settings.ViewMode == ViewLayout.Webtoon)
                {
                    fText = "맞춤: 가로폭(고정) ▼";
                }
                else
                {
                    switch (_settings.FitMode)
                    {
                        case DisplayFitMode.FitWidth: fText = "맞춤: 폭맞춤 (W) ▼"; break;
                        case DisplayFitMode.FitHeight: fText = "맞춤: 높이맞춤 ▼"; break;
                        case DisplayFitMode.Original: fText = "맞춤: 원본크기 (O) ▼"; break;
                    }
                }
                _fitMenuBtn.Text = fText;
            }

            if (_stretchBtn != null)
            {
                _stretchBtn.Text = _settings.StretchSmallImages ? "확대: ON (S)" : "확대: OFF (S)";
                _stretchBtn.BackColor = _settings.StretchSmallImages ? Color.FromArgb(70, 60, 120) : Color.FromArgb(45, 49, 60);
            }
        }

        private void UpdateWheelStatusLabel()
        {
            if (_wheelStatusLabel != null)
            {
                if (_settings.WheelMode == 1)
                {
                    _wheelStatusLabel.Text = string.Format("휠: [페이지 넘기기] ({0})", _settings.WheelToggleKey);
                    _wheelStatusLabel.ForeColor = Color.FromArgb(255, 159, 243);
                }
                else
                {
                    _wheelStatusLabel.Text = string.Format("휠: [확대/축소] ({0})", _settings.WheelToggleKey);
                    _wheelStatusLabel.ForeColor = Color.FromArgb(170, 175, 190);
                }
            }
        }

        private void ToggleWheelMode()
        {
            _settings.WheelMode = (_settings.WheelMode == 0) ? 1 : 0;
            _settings.Save();
            UpdateWheelStatusLabel();
        }

        private void ToggleAutoHide()
        {
            _settings.AutoHideUi = !_settings.AutoHideUi;
            _settings.Save();
            UpdateAutoHideUi();
            FitToScreen();
        }

        private void UpdateAutoHideUi()
        {
            if (_autoHideTopBtn != null)
            {
                _autoHideTopBtn.Text = _settings.AutoHideUi ? "자동 (H)" : "고정 (H)";
                _autoHideTopBtn.BackColor = _settings.AutoHideUi ? Color.FromArgb(48, 70, 110) : Color.FromArgb(45, 49, 60);
            }
            if (_autoHideStatusLabel != null)
            {
                _autoHideStatusLabel.Text = _settings.AutoHideUi ? "UI: [자동숨김] (H)" : "UI: [고정됨] (H)";
                _autoHideStatusLabel.LinkColor = _settings.AutoHideUi ? Color.FromArgb(255, 159, 243) : Color.FromArgb(170, 175, 190);
            }

            if (!_settings.AutoHideUi)
            {
                _topBar.Visible = true;
                _statusStrip.Visible = true;
                if (_navBar != null && GetTotalItems() > 0) _navBar.Visible = true;
                if (_isGif && _gifFrameCount > 1) _gifBar.Visible = true;
            }
            else
            {
                _topBar.Visible = false;
                _statusStrip.Visible = false;
                if (_navBar != null) _navBar.Visible = false;
                _gifBar.Visible = false;
            }
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            if (!_settings.AutoHideUi)
            {
                if (!_topBar.Visible) _topBar.Visible = true;
                if (!_statusStrip.Visible) _statusStrip.Visible = true;
                if (_navBar != null && GetTotalItems() > 0 && !_navBar.Visible) _navBar.Visible = true;
                if (_isGif && _gifFrameCount > 1 && !_gifBar.Visible) _gifBar.Visible = true;
                return;
            }

            if (_contextMenuOpen) return;
            if (Control.MouseButtons != MouseButtons.None) return;

            // 검색창에 포커스가 있으면 하단 바를 숨기지 않음
            if (_navSearchBox != null && _navSearchBox.Focused) return;

            Point clientPt = this.PointToClient(Cursor.Position);

            if (!this.ClientRectangle.Contains(clientPt))
            {
                if (_topBar.Visible) _topBar.Visible = false;
                if (_statusStrip.Visible) _statusStrip.Visible = false;
                if (_navBar != null && _navBar.Visible) _navBar.Visible = false;
                if (_gifBar.Visible) _gifBar.Visible = false;
                _suppressBottomHover = false;
                return;
            }

            // 1. 상단 툴바 호버 감지 (마우스가 상단 45px 이내이거나 툴바 위에 있을 때 즉시 표시)
            int topTriggerH = _topBar.Visible ? (_topBar.Height + 10) : 45;
            if (clientPt.Y <= topTriggerH)
            {
                if (!_topBar.Visible) _topBar.Visible = true;
            }
            else
            {
                if (_topBar.Visible) _topBar.Visible = false;
            }

            // 2. 하단 컨트롤 바 (상태바 & 네비게이션 바 & GIF 바) 감지
            int navH = (_navBar != null && _navBar.Visible) ? _navBar.Height : 0;
            int botContentH = _statusStrip.Height + navH + ((_isGif && _gifFrameCount > 1) ? _gifBar.Height : 0);
            int botTriggerDist = (_statusStrip.Visible || (_navBar != null && _navBar.Visible) || _gifBar.Visible) ? (botContentH + 10) : 45;
            int botTriggerY = this.ClientSize.Height - botTriggerDist;

            // 숨기기 버튼 클릭 후 마우스가 하단 영역을 벗어났을 때 suppress 해제
            if (_suppressBottomHover)
            {
                if (clientPt.Y < this.ClientSize.Height - (botContentH + 30))
                {
                    _suppressBottomHover = false;
                }
            }
            else
            {
                if (clientPt.Y >= botTriggerY)
                {
                    if (!_statusStrip.Visible) _statusStrip.Visible = true;
                    if (_navBar != null && GetTotalItems() > 0 && !_navBar.Visible) _navBar.Visible = true;
                    if (_isGif && _gifFrameCount > 1 && !_gifBar.Visible) _gifBar.Visible = true;
                }
                else
                {
                    if (_statusStrip.Visible) _statusStrip.Visible = false;
                    if (_navBar != null && _navBar.Visible) _navBar.Visible = false;
                    if (_gifBar.Visible) _gifBar.Visible = false;
                }
            }
        }

        private ToolStripMenuItem _filterItemOff;
        private ToolStripMenuItem _filterItemSharpen;
        private ToolStripMenuItem _filterItemBlur;
        private ToolStripMenuItem _filterItemMedium;
        private ToolStripMenuItem _filterItemUnsharp;
        private ToolStripMenuItem _filterItemMangaClean;
        private ToolStripMenuItem _filterItemInvert;

        private void SetupFilterMenu()
        {
            _filterContext = new ContextMenuStrip();
            _filterContext.Renderer = new DarkMenuRenderer();
            _filterContext.ShowCheckMargin = true;
            _filterContext.ShowImageMargin = false;
            _filterContext.BackColor = Color.FromArgb(24, 26, 32);
            _filterContext.ForeColor = Color.FromArgb(240, 243, 250);
            _filterContext.Font = new Font("Segoe UI", 9F);

            _filterItemOff = new ToolStripMenuItem("끄기 (Off) [0]", null, (s, e) => SetFilter(FilterType.Off));
            _filterItemSharpen = new ToolStripMenuItem("선명하게 (Sharpen) [1]", null, (s, e) => SetFilter(FilterType.Sharpen));
            _filterItemBlur = new ToolStripMenuItem("흐리게 (Blur) [2]", null, (s, e) => SetFilter(FilterType.Blur));
            _filterItemMedium = new ToolStripMenuItem("중간 부드럽게 (Medium) [3]", null, (s, e) => SetFilter(FilterType.Medium));
            _filterItemUnsharp = new ToolStripMenuItem("꿀뷰 언샤프 마스크 (Unsharp Mask) [4] - 외곽선 또렷", null, (s, e) => SetFilter(FilterType.UnsharpMask));
            _filterItemMangaClean = new ToolStripMenuItem("스캔본 만화 톤 보정 (Manga Clean) [5] - 배경 순백 & 잉크 딥블랙", null, (s, e) => SetFilter(FilterType.MangaClean));
            _filterItemInvert = new ToolStripMenuItem("야간 반전 읽기 (Invert Dark) [6] - 눈부심 방지", null, (s, e) => SetFilter(FilterType.Invert));

            _filterContext.Items.Add(_filterItemOff);
            _filterContext.Items.Add(_filterItemSharpen);
            _filterContext.Items.Add(_filterItemBlur);
            _filterContext.Items.Add(_filterItemMedium);
            _filterContext.Items.Add(_filterItemUnsharp);
            _filterContext.Items.Add(_filterItemMangaClean);
            _filterContext.Items.Add(_filterItemInvert);
            _filterContext.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem interpSub = new ToolStripMenuItem("보간 방식 (Interpolation) [I]");
            interpSub.BackColor = Color.FromArgb(24, 26, 32);
            interpSub.ForeColor = Color.FromArgb(240, 243, 250);
            var iBicubic = interpSub.DropDownItems.Add("고품질 쌍입방 (Bicubic) - 만화/일러스트 권장", null, (s, e) => SetInterpolationMode(InterpolationModeType.Bicubic));
            var iBilinear = interpSub.DropDownItems.Add("쌍선형 (Bilinear) - 부드러움 & 고속", null, (s, e) => SetInterpolationMode(InterpolationModeType.Bilinear));
            var iNearest = interpSub.DropDownItems.Add("최근접 이웃 (Nearest Neighbor) - 픽셀아트/도트 보존", null, (s, e) => SetInterpolationMode(InterpolationModeType.NearestNeighbor));

            interpSub.DropDownOpening += (s, e) =>
            {
                ((ToolStripMenuItem)iBicubic).Checked = (_settings.InterpolationMode == InterpolationModeType.Bicubic);
                ((ToolStripMenuItem)iBilinear).Checked = (_settings.InterpolationMode == InterpolationModeType.Bilinear);
                ((ToolStripMenuItem)iNearest).Checked = (_settings.InterpolationMode == InterpolationModeType.NearestNeighbor);
            };
            _filterContext.Items.Add(interpSub);

            _filterContext.Opening += (s, e) =>
            {
                _filterItemOff.Checked = (_currentFilter == FilterType.Off);
                _filterItemSharpen.Checked = (_currentFilter == FilterType.Sharpen);
                _filterItemBlur.Checked = (_currentFilter == FilterType.Blur);
                _filterItemMedium.Checked = (_currentFilter == FilterType.Medium);
                _filterItemUnsharp.Checked = (_currentFilter == FilterType.UnsharpMask);
                _filterItemMangaClean.Checked = (_currentFilter == FilterType.MangaClean);
                _filterItemInvert.Checked = (_currentFilter == FilterType.Invert);
            };

            ApplyDarkMenuTheme(_filterContext);
        }

        private void ShowFilterMenu()
        {
            _filterContext.Show(_filterMenuBtn, new Point(0, _filterMenuBtn.Height));
        }

        private void SetFilter(FilterType filter)
        {
            _currentFilter = filter;
            string filterLabel = "필터: 끄기 ▼";
            switch (filter)
            {
                case FilterType.Sharpen: filterLabel = "필터: 선명 ▼"; break;
                case FilterType.Blur: filterLabel = "필터: 흐림 ▼"; break;
                case FilterType.Medium: filterLabel = "필터: 중간 ▼"; break;
                case FilterType.UnsharpMask: filterLabel = "필터: 언샤프 ▼"; break;
                case FilterType.MangaClean: filterLabel = "필터: 만화톤 ▼"; break;
                case FilterType.Invert: filterLabel = "필터: 야간반전 ▼"; break;
                default: filterLabel = "필터: 끄기 ▼"; break;
            }
            _filterMenuBtn.Text = filterLabel;
            _filterMenuBtn.BackColor = (filter != FilterType.Off) ? Color.FromArgb(70, 50, 110) : Color.FromArgb(45, 49, 60);

            if (_rawBitmap != null)
            {
                if (_filteredBitmap != null && _filteredBitmap != _rawBitmap)
                {
                    _filteredBitmap.Dispose();
                }
                _filteredBitmap = FilterEngine.ApplyFilter(_rawBitmap, _currentFilter);
            }
            if (_rawBitmap2 != null)
            {
                if (_filteredBitmap2 != null && _filteredBitmap2 != _rawBitmap2)
                {
                    _filteredBitmap2.Dispose();
                }
                _filteredBitmap2 = FilterEngine.ApplyFilter(_rawBitmap2, _currentFilter);
            }
            this.Invalidate();
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
                int cornerPreference = 2; // DWMWCP_ROUND (Windows 11 둥근 모서리)
                DwmSetWindowAttribute(this.Handle, 33, ref cornerPreference, sizeof(int));
            }
            catch { }
        }

        private void DragWindow()
        {
            ReleaseCapture();
            SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private long _lastMaximizeTicks = 0;

        private void ToggleMaximize()
        {
            long now = DateTime.UtcNow.Ticks;
            if (now - _lastMaximizeTicks < TimeSpan.FromMilliseconds(200).Ticks) return;
            _lastMaximizeTicks = now;

            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                if (_maxBtn != null) _maxBtn.Text = "\u25A1";
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
                if (_maxBtn != null) _maxBtn.Text = "\u29C9";
            }
            if (_filteredBitmap != null) FitToScreen();
            else this.Invalidate();
        }

        private void RestoreWindowBounds()
        {
            try
            {
                int x = _settings.WindowX;
                int y = _settings.WindowY;
                int w = _settings.WindowWidth;
                int h = _settings.WindowHeight;

                if (w < 400) w = 1280;
                if (h < 300) h = 840;

                Rectangle candidate = new Rectangle(x, y, w, h);
                bool isVisible = false;

                if (x != -1 && y != -1)
                {
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        Rectangle intersect = Rectangle.Intersect(screen.WorkingArea, candidate);
                        if (intersect.Width >= 120 && intersect.Height >= 120)
                        {
                            isVisible = true;
                            break;
                        }
                    }
                }

                if (isVisible)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.SetBounds(x, y, w, h);
                }
                else
                {
                    this.StartPosition = FormStartPosition.CenterScreen;
                    this.Size = new Size(w, h);
                }

                if (_settings.WindowMaximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                    if (_maxBtn != null) _maxBtn.Text = "\u29C9";
                }
                else
                {
                    if (_maxBtn != null) _maxBtn.Text = "\u25A1";
                }
            }
            catch { }
        }

        private void SyncWindowBoundsToSettings()
        {
            try
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    _settings.WindowMaximized = false;
                    _settings.WindowX = this.Location.X;
                    _settings.WindowY = this.Location.Y;
                    _settings.WindowWidth = this.Width;
                    _settings.WindowHeight = this.Height;
                }
                else
                {
                    Rectangle rb = this.RestoreBounds;
                    if (rb.Width >= 320 && rb.Height >= 240)
                    {
                        _settings.WindowX = rb.X;
                        _settings.WindowY = rb.Y;
                        _settings.WindowWidth = rb.Width;
                        _settings.WindowHeight = rb.Height;
                    }
                    _settings.WindowMaximized = (this.WindowState == FormWindowState.Maximized);
                }
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            try
            {
                SyncWindowBoundsToSettings();
                _settings.Save();
            }
            catch { }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000; // WS_MINIMIZEBOX (작업표시줄 클릭 최소화 지원)
                cp.Style |= 0x10000; // WS_MAXIMIZEBOX (윈도우 11 스냅 레이아웃 및 최대화 지원)
                cp.Style |= 0x80000; // WS_SYSMENU (시스템 메뉴 및 Alt+F4 표준 닫기 지원)
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW (윈도우 네이티브 그림자)
                return cp;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        protected override void WndProc(ref Message m)
        {
            // 캡션/타이틀/빈화면 드래그 영역 더블 클릭 시 최대화/복원 토글
            if (m.Msg == WM_NCLBUTTONDBLCLK && (int)m.WParam == HT_CAPTION)
            {
                ToggleMaximize();
                m.Result = IntPtr.Zero;
                return;
            }

            // 최대화 시 작업표시줄을 덮지 않도록 모니터 작업 영역(WorkingArea) 제한 및 최소 크기 제한
            if (m.Msg == WM_GETMINMAXINFO)
            {
                try
                {
                    Screen screen = Screen.FromHandle(this.Handle);
                    Rectangle work = screen.WorkingArea;
                    Rectangle bounds = screen.Bounds;
                    MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                    mmi.ptMaxPosition.X = Math.Abs(work.Left - bounds.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(work.Top - bounds.Top);
                    mmi.ptMaxSize.X = work.Width;
                    mmi.ptMaxSize.Y = work.Height;
                    if (this.MinimumSize.Width > 0 && this.MinimumSize.Height > 0)
                    {
                        mmi.ptMinTrackSize.X = this.MinimumSize.Width;
                        mmi.ptMinTrackSize.Y = this.MinimumSize.Height;
                    }
                    Marshal.StructureToPtr(mmi, m.LParam, true);
                    m.Result = IntPtr.Zero;
                    return;
                }
                catch { }
            }

            // 프레임리스 창 테두리 8방향 리사이즈 핸들 지원 (상단/대각선 포함)
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT && this.WindowState != FormWindowState.Maximized)
                {
                    int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                    int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                    Point p = this.PointToClient(new Point(x, y));
                    int b = 8; // 테두리 리사이즈 영역 폭

                    bool left = p.X <= b;
                    bool right = p.X >= this.ClientSize.Width - b;
                    bool top = p.Y <= b;
                    bool bottom = p.Y >= this.ClientSize.Height - b;

                    // 닫기 버튼 영역(우측 46px): 최상단 3px / 최우측 3px 극단 테두리를 제외한 본체는 닫기 클릭 허용
                    if (top && p.Y > 3 && p.X >= this.ClientSize.Width - 46 && p.X < this.ClientSize.Width - 3 && _topBar != null && _topBar.Visible)
                    {
                        // 닫기 버튼 직접 클릭 허용
                    }
                    else
                    {
                        if (top && left) { m.Result = (IntPtr)HTTOPLEFT; return; }
                        if (top && right) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                        if (bottom && left) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                        if (bottom && right) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                        if (left) { m.Result = (IntPtr)HTLEFT; return; }
                        if (right) { m.Result = (IntPtr)HTRIGHT; return; }
                        if (top) { m.Result = (IntPtr)HTTOP; return; }
                        if (bottom) { m.Result = (IntPtr)HTBOTTOM; return; }
                    }
                }
                return;
            }

            base.WndProc(ref m);
        }

        private void AttachBorderResizeFilter(Control parent)
        {
            if (parent == null) return;
            foreach (Control child in parent.Controls)
            {
                RegisterBorderResizeFilter(child);
            }
            parent.ControlAdded += (s, e) => RegisterBorderResizeFilter(e.Control);
        }

        private void RegisterBorderResizeFilter(Control ctrl)
        {
            if (ctrl == null) return;
            var filter = new BorderHitTestNativeWindow(this, ctrl);
            _borderHitFilters.Add(filter);

            foreach (Control child in ctrl.Controls)
            {
                RegisterBorderResizeFilter(child);
            }
            ctrl.ControlAdded += (s, e) => RegisterBorderResizeFilter(e.Control);
        }

        private class BorderHitTestNativeWindow : NativeWindow
        {
            private readonly MainForm _form;
            private readonly Control _ctrl;

            public BorderHitTestNativeWindow(MainForm form, Control ctrl)
            {
                _form = form;
                _ctrl = ctrl;
                if (ctrl.IsHandleCreated)
                {
                    try { this.AssignHandle(ctrl.Handle); } catch { }
                }
                ctrl.HandleCreated += (s, e) => {
                    try {
                        this.ReleaseHandle();
                        this.AssignHandle(ctrl.Handle);
                    } catch { }
                };
                ctrl.HandleDestroyed += (s, e) => {
                    try {
                        this.ReleaseHandle();
                    } catch { }
                };
            }

            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x0084;
                const int HTTRANSPARENT = -1;

                if (m.Msg == WM_NCHITTEST && _form.WindowState != FormWindowState.Maximized)
                {
                    int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                    int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                    Point p = _form.PointToClient(new Point(x, y));
                    int b = 8; // 테두리 리사이즈 영역 폭 (8px)

                    bool inBorder = (p.X <= b || p.X >= _form.ClientSize.Width - b ||
                                     p.Y <= b || p.Y >= _form.ClientSize.Height - b);

                    if (inBorder)
                    {
                        // 닫기 버튼(_closeBtn): 최상단 3px / 최우측 3px의 극단 테두리는 리사이즈에 양보하고,
                        // 그 외 본체 영역(Y > 3 && X < Width - 3)은 닫기 클릭이 정확히 동작하도록 허용
                        if (_ctrl == _form._closeBtn)
                        {
                            if (p.Y > 3 && p.X < _form.ClientSize.Width - 3)
                            {
                                base.WndProc(ref m);
                                return;
                            }
                        }

                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }

                base.WndProc(ref m);
            }
        }

        // 이미지 로드 (단일 이미지)
        public void LoadSingleImage(string filePath)
        {
            try
            {
                ClearAdjacentEngines();
                ResetWebtoonState();
                _prevZipPath = null;
                _nextZipPath = null;
                _prevPdfPath = null;
                _nextPdfPath = null;

                _zipEngine.Close();
                if (_pdfEngine != null) _pdfEngine.Close();
                _singleImages.Clear();
                _singleImages.Add(filePath);
                _currentIndex = 0;

                string dir = Path.GetDirectoryName(filePath);
                if (Directory.Exists(dir))
                {
                    _singleImages = Directory.GetFiles(dir)
                        .Where(f => SupportedImageExts.Contains(Path.GetExtension(f)))
                        .OrderBy(f => f, new NaturalStringComparer())
                        .ToList();
                    _currentIndex = _singleImages.IndexOf(filePath);
                }

                DisplayCurrentItem();
                _settings.AddRecentItem(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("이미지 로드 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 아카이브 로드 (ZIP)
        public void LoadArchive(string zipPath, int startIndex = 0)
        {
            try
            {
                ClearAdjacentEngines();
                ResetWebtoonState();
                _prevPdfPath = null;
                _nextPdfPath = null;
                if (_pdfEngine != null) _pdfEngine.Close();
                _singleImages.Clear();

                var entries = _zipEngine.LoadZip(zipPath);
                if (entries.Count == 0)
                {
                    MessageBox.Show("압축 파일 내에 지원되는 이미지가 없습니다.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _currentIndex = (startIndex < 0) ? (entries.Count - 1) : Math.Max(0, Math.Min(entries.Count - 1, startIndex));
                DisplayCurrentItem();
                _settings.AddRecentItem(zipPath);

                UpdateAdjacentZipPaths(zipPath);
                TriggerAdjacentArchivePrecache();
            }
            catch (Exception ex)
            {
                MessageBox.Show("압축 파일 열기 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // PDF 문서 로드
        public void LoadPdf(string pdfPath, int startIndex = 0)
        {
            try
            {
                ClearAdjacentEngines();
                ResetWebtoonState();
                _prevZipPath = null;
                _nextZipPath = null;
                _zipEngine.Close();
                _singleImages.Clear();

                if (!_pdfEngine.LoadPdf(pdfPath))
                {
                    MessageBox.Show("PDF 문서를 열지 못했습니다.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int total = _pdfEngine.PageCount;
                if (total <= 0)
                {
                    MessageBox.Show("PDF 페이지를 읽을 수 없습니다.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _currentIndex = (startIndex < 0) ? (total - 1) : Math.Max(0, Math.Min(total - 1, startIndex));
                DisplayCurrentItem();
                _settings.AddRecentItem(pdfPath);

                UpdateAdjacentPdfPaths(pdfPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("PDF 열기 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateAdjacentPdfPaths(string currentPdfPath)
        {
            try
            {
                _prevPdfPath = null;
                _nextPdfPath = null;

                if (string.IsNullOrEmpty(currentPdfPath)) return;
                string dir = Path.GetDirectoryName(currentPdfPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                var pdfFiles = Directory.GetFiles(dir)
                    .Where(f => IsPdfFile(f))
                    .OrderBy(f => f, new NaturalStringComparer())
                    .ToList();

                int idx = pdfFiles.FindIndex(f => f.Equals(currentPdfPath, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    if (idx > 0) _prevPdfPath = pdfFiles[idx - 1];
                    if (idx < pdfFiles.Count - 1) _nextPdfPath = pdfFiles[idx + 1];
                }
            }
            catch { }
        }

        private void UpdateAdjacentZipPaths(string currentZipPath)
        {
            try
            {
                _prevZipPath = null;
                _nextZipPath = null;

                if (string.IsNullOrEmpty(currentZipPath)) return;
                string dir = Path.GetDirectoryName(currentZipPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                var archiveExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".zip", ".cbz" };
                var zipFiles = Directory.GetFiles(dir)
                    .Where(f => archiveExts.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f, new NaturalStringComparer())
                    .ToList();

                int idx = zipFiles.FindIndex(f => f.Equals(currentZipPath, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    if (idx > 0) _prevZipPath = zipFiles[idx - 1];
                    if (idx < zipFiles.Count - 1) _nextZipPath = zipFiles[idx + 1];
                }
            }
            catch { }
        }

        private void TriggerAdjacentArchivePrecache()
        {
            AdjacentArchiveCacheMode mode = _settings.AdjacentZipCache;
            if (mode == AdjacentArchiveCacheMode.Disabled || _zipEngine == null || _zipEngine.Count == 0)
            {
                ClearAdjacentEngines();
                return;
            }

            int precacheId = Interlocked.Increment(ref _adjacentPrecacheId);

            string targetNext = (mode == AdjacentArchiveCacheMode.NextOnly || mode == AdjacentArchiveCacheMode.Both) ? _nextZipPath : null;
            string targetPrev = (mode == AdjacentArchiveCacheMode.PrevOnly || mode == AdjacentArchiveCacheMode.Both) ? _prevZipPath : null;

            lock (_adjacentLock)
            {
                if (targetNext == null && _cachedNextZipEngine != null)
                {
                    _cachedNextZipEngine.Dispose();
                    _cachedNextZipEngine = null;
                }
                if (targetPrev == null && _cachedPrevZipEngine != null)
                {
                    _cachedPrevZipEngine.Dispose();
                    _cachedPrevZipEngine = null;
                }
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                // 1. 다음 압축 파일(Next) 백그라운드 선행 프리캐싱
                if (!string.IsNullOrEmpty(targetNext) && File.Exists(targetNext))
                {
                    bool needLoadNext = true;
                    lock (_adjacentLock)
                    {
                        if (_cachedNextZipEngine != null && string.Equals(_cachedNextZipEngine.ZipPath, targetNext, StringComparison.OrdinalIgnoreCase))
                        {
                            needLoadNext = false;
                        }
                    }

                    if (needLoadNext)
                    {
                        try
                        {
                            ZipEngine nextEng = new ZipEngine();
                            nextEng.CacheMode = CacheLimitMode.MegaBytes;
                            nextEng.CacheLimitMb = 64; // 인접 파일은 1~2페이지만 가볍게 사전 로드
                            nextEng.CacheWindow = 2;
                            var entries = nextEng.LoadZip(targetNext);
                            if (entries != null && entries.Count > 0)
                            {
                                nextEng.GetImage(0);
                            }

                            lock (_adjacentLock)
                            {
                                if (_adjacentPrecacheId == precacheId)
                                {
                                    if (_cachedNextZipEngine != null) _cachedNextZipEngine.Dispose();
                                    _cachedNextZipEngine = nextEng;
                                }
                                else
                                {
                                    nextEng.Dispose();
                                }
                            }
                        }
                        catch { }
                    }
                }

                // 2. 이전 압축 파일(Prev) 백그라운드 선행 프리캐싱
                if (!string.IsNullOrEmpty(targetPrev) && File.Exists(targetPrev))
                {
                    bool needLoadPrev = true;
                    lock (_adjacentLock)
                    {
                        if (_cachedPrevZipEngine != null && string.Equals(_cachedPrevZipEngine.ZipPath, targetPrev, StringComparison.OrdinalIgnoreCase))
                        {
                            needLoadPrev = false;
                        }
                    }

                    if (needLoadPrev)
                    {
                        try
                        {
                            ZipEngine prevEng = new ZipEngine();
                            prevEng.CacheMode = CacheLimitMode.MegaBytes;
                            prevEng.CacheLimitMb = 64;
                            prevEng.CacheWindow = 2;
                            var entries = prevEng.LoadZip(targetPrev);
                            if (entries != null && entries.Count > 0)
                            {
                                prevEng.GetImage(entries.Count - 1);
                            }

                            lock (_adjacentLock)
                            {
                                if (_adjacentPrecacheId == precacheId)
                                {
                                    if (_cachedPrevZipEngine != null) _cachedPrevZipEngine.Dispose();
                                    _cachedPrevZipEngine = prevEng;
                                }
                                else
                                {
                                    prevEng.Dispose();
                                }
                            }
                        }
                        catch { }
                    }
                }
            });
        }

        private void ClearAdjacentEngines()
        {
            Interlocked.Increment(ref _adjacentPrecacheId);
            lock (_adjacentLock)
            {
                if (_cachedNextZipEngine != null)
                {
                    _cachedNextZipEngine.Dispose();
                    _cachedNextZipEngine = null;
                }
                if (_cachedPrevZipEngine != null)
                {
                    _cachedPrevZipEngine.Dispose();
                    _cachedPrevZipEngine = null;
                }
            }
        }

        private void MoveToNextArchive()
        {
            string nextPath = _nextZipPath;
            if (string.IsNullOrEmpty(nextPath) || !File.Exists(nextPath)) return;

            ZipEngine readyEngine = null;
            lock (_adjacentLock)
            {
                if (_cachedNextZipEngine != null && string.Equals(_cachedNextZipEngine.ZipPath, nextPath, StringComparison.OrdinalIgnoreCase))
                {
                    readyEngine = _cachedNextZipEngine;
                    _cachedNextZipEngine = null;
                }
            }

            if (readyEngine != null)
            {
                _zipEngine.Close();
                _zipEngine = readyEngine;
                _zipEngine.CacheMode = _settings.CacheMode;
                _zipEngine.CacheLimitMb = _settings.CacheLimitMb;
                _zipEngine.CacheWindow = _settings.PrecacheCount;

                _currentIndex = 0;
                DisplayCurrentItem();
                _settings.AddRecentItem(nextPath);
                UpdateAdjacentZipPaths(nextPath);
                TriggerAdjacentArchivePrecache();
            }
            else
            {
                LoadArchive(nextPath, 0);
            }
        }

        private void MoveToPrevArchive(bool openAtEnd = true)
        {
            string prevPath = _prevZipPath;
            if (string.IsNullOrEmpty(prevPath) || !File.Exists(prevPath)) return;

            ZipEngine readyEngine = null;
            lock (_adjacentLock)
            {
                if (_cachedPrevZipEngine != null && string.Equals(_cachedPrevZipEngine.ZipPath, prevPath, StringComparison.OrdinalIgnoreCase))
                {
                    readyEngine = _cachedPrevZipEngine;
                    _cachedPrevZipEngine = null;
                }
            }

            if (readyEngine != null)
            {
                _zipEngine.Close();
                _zipEngine = readyEngine;
                _zipEngine.CacheMode = _settings.CacheMode;
                _zipEngine.CacheLimitMb = _settings.CacheLimitMb;
                _zipEngine.CacheWindow = _settings.PrecacheCount;

                _currentIndex = openAtEnd ? Math.Max(0, _zipEngine.Count - 1) : 0;
                DisplayCurrentItem();
                _settings.AddRecentItem(prevPath);
                UpdateAdjacentZipPaths(prevPath);
                TriggerAdjacentArchivePrecache();
            }
            else
            {
                LoadArchive(prevPath, openAtEnd ? -1 : 0);
            }
        }

        private void NextArchiveOrFile()
        {
            if (_zipEngine != null && _zipEngine.Count > 0)
            {
                MoveToNextArchive();
            }
            else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
            {
                if (!string.IsNullOrEmpty(_nextPdfPath) && File.Exists(_nextPdfPath))
                {
                    LoadPdf(_nextPdfPath, 0);
                }
            }
            else if (_singleImages != null && _singleImages.Count > 0)
            {
                if (_currentIndex < _singleImages.Count - 1)
                {
                    _currentIndex++;
                    DisplayCurrentItem();
                }
            }
        }

        private void PrevArchiveOrFile(bool openAtEnd = false)
        {
            if (_zipEngine != null && _zipEngine.Count > 0)
            {
                MoveToPrevArchive(openAtEnd);
            }
            else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
            {
                if (!string.IsNullOrEmpty(_prevPdfPath) && File.Exists(_prevPdfPath))
                {
                    LoadPdf(_prevPdfPath, openAtEnd ? -1 : 0);
                }
            }
            else if (_singleImages != null && _singleImages.Count > 0)
            {
                if (_currentIndex > 0)
                {
                    _currentIndex--;
                    DisplayCurrentItem();
                }
            }
        }

        private Bitmap GetBitmapAtIndex(int index, out string name)
        {
            name = "";
            if (index < 0) return null;
            if (_zipEngine != null && _zipEngine.Count > 0)
            {
                if (index < _zipEngine.Count)
                {
                    name = _zipEngine.GetEntryName(index);
                    return _zipEngine.GetImage(index);
                }
            }
            else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
            {
                if (index < _pdfEngine.PageCount)
                {
                    string docName = Path.GetFileName(_pdfEngine.PdfPath);
                    name = string.Format("{0} [P.{1}]", docName, index + 1);
                    return _pdfEngine.GetImage(index);
                }
            }
            else if (_singleImages != null && _singleImages.Count > 0 && index < _singleImages.Count)
            {
                string path = _singleImages[index];
                name = Path.GetFileName(path);
                try { return ImageLoader.LoadFromFile(path); } catch { return null; }
            }
            return null;
        }

        private void DisplayCurrentItem()
        {
            _gifTimer.Stop();
            _isGif = false;
            _gifBar.Visible = false;

            if (_settings.AutoHideUi)
            {
                _topBar.Visible = false;
                _statusStrip.Visible = false;
            }
            else
            {
                _topBar.Visible = true;
                _statusStrip.Visible = true;
            }

            FreeCurrentBitmaps(false);
            _isDualPageDisplayed = false;
            _secondIndex = -1;
            _secondImageName = "";

            string name1;
            Bitmap bmp1 = GetBitmapAtIndex(_currentIndex, out name1);
            if (bmp1 == null) return;

            // GIF 애니메이션 검사 (1번째 파일)
            bool isGif = name1.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
            try
            {
                if (bmp1.RawFormat.Guid == System.Drawing.Imaging.ImageFormat.Gif.Guid)
                    isGif = true;
            }
            catch { }

            if (isGif)
            {
                SetupGifPlayer(bmp1);
            }
            else
            {
                _rawBitmap = bmp1;
                _filteredBitmap = FilterEngine.ApplyFilter(_rawBitmap, _currentFilter);
            }

            int total = GetTotalItems();

            // 웹툰 모드 캐시 및 크기 등록
            if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                lock (_webtoonLock)
                {
                    _webtoonCache[_currentIndex] = _filteredBitmap;
                    _webtoonPageSizes[_currentIndex] = new Size(_rawBitmap.Width, _rawBitmap.Height);
                }
            }

            // 만화책/서적 2장 모드 검사
            if (!isGif && _settings.ViewMode != ViewLayout.Single && _settings.ViewMode != ViewLayout.Webtoon)
            {
                // 1번째 이미지가 가로로 넓은 스프레드(펼침면)인지 검사 (비율 > 1.15) - 스마트 감지 옵션 켜짐일 때만 적용
                bool isSpread1 = _settings.SmartSpreadDetection && (bmp1.Width > bmp1.Height * 1.15f);
                if (!isSpread1 && _currentIndex + 1 < total)
                {
                    string name2;
                    Bitmap bmp2 = GetBitmapAtIndex(_currentIndex + 1, out name2);
                    if (bmp2 != null)
                    {
                        bool isGif2 = name2.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
                        bool isSpread2 = _settings.SmartSpreadDetection && (bmp2.Width > bmp2.Height * 1.15f);

                        if (!isGif2 && !isSpread2)
                        {
                            // 2장 모드 조건 충족!
                            _rawBitmap2 = bmp2;
                            _filteredBitmap2 = FilterEngine.ApplyFilter(_rawBitmap2, _currentFilter);
                            _isDualPageDisplayed = true;
                            _secondIndex = _currentIndex + 1;
                            _secondImageName = name2;
                        }
                        else
                        {
                            // 다음 이미지가 스프레드 또는 GIF라면 이번에는 1장만 단독 표시
                            if ((_zipEngine == null || _zipEngine.Count == 0) && (_pdfEngine == null || _pdfEngine.PageCount == 0))
                            {
                                bmp2.Dispose();
                            }
                        }
                    }
                }
            }

            UpdateCurrentImageInfo(bmp1, name1);
            FitToScreen();
            UpdateStatusLabelText();
            UpdateNavBar();
            this.Invalidate();
        }

        private void UpdateStatusLabelText()
        {
            int total = GetTotalItems();
            if (_isGif && _gifFrameCount > 1)
            {
                string name = (_currentIndex >= 0 && _singleImages.Count > _currentIndex) ? Path.GetFileName(_singleImages[_currentIndex]) : (_zipEngine.Count > 0 ? _zipEngine.GetEntryName(_currentIndex) : "");
                _statusLabel.Text = string.Format("{0} [{1}/{2}] ({3}x{4})  [GIF: {5}프레임]",
                    name, _currentIndex + 1, total, _rawBitmap.Width, _rawBitmap.Height, _gifFrameCount);
            }
            else if (_isDualPageDisplayed && _filteredBitmap2 != null)
            {
                string n1 = "";
                if (_zipEngine != null && _zipEngine.Count > 0)
                    n1 = _zipEngine.GetEntryName(_currentIndex);
                else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
                    n1 = string.Format("P.{0}", _currentIndex + 1);
                else if (_currentIndex >= 0 && _singleImages.Count > _currentIndex)
                    n1 = Path.GetFileName(_singleImages[_currentIndex]);

                string n2 = _secondImageName;
                string dirStr = (_settings.ViewMode == ViewLayout.DualRtl) ? "우→좌 (만화)" : "좌→우 (서적)";
                string combinedName = (_settings.ViewMode == ViewLayout.DualRtl) ? (n2 + " ◀ " + n1) : (n1 + " ▶ " + n2);
                int totalW = _rawBitmap.Width + _rawBitmap2.Width;
                int maxH = Math.Max(_rawBitmap.Height, _rawBitmap2.Height);
                _statusLabel.Text = string.Format("{0} [{1}-{2}/{3}] ({4}x{5}) [2장: {6}]",
                    combinedName, _currentIndex + 1, _secondIndex + 1, total, totalW, maxH, dirStr);
            }
            else if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                string name = "";
                if (_zipEngine != null && _zipEngine.Count > 0)
                    name = _zipEngine.GetEntryName(_currentIndex);
                else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
                    name = string.Format("{0} [P.{1}]", Path.GetFileName(_pdfEngine.PdfPath), _currentIndex + 1);
                else if (_singleImages.Count > _currentIndex && _currentIndex >= 0)
                    name = Path.GetFileName(_singleImages[_currentIndex]);

                int w = (_rawBitmap != null) ? _rawBitmap.Width : 0;
                int h = (_rawBitmap != null) ? _rawBitmap.Height : 0;
                _statusLabel.Text = string.Format("{0} [{1}/{2}] ({3}x{4}) [웹툰 - 세로 연속]", name, _currentIndex + 1, total, w, h);
            }
            else
            {
                string name = "";
                if (_zipEngine != null && _zipEngine.Count > 0)
                    name = _zipEngine.GetEntryName(_currentIndex);
                else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
                    name = string.Format("{0} [P.{1}]", Path.GetFileName(_pdfEngine.PdfPath), _currentIndex + 1);
                else if (_singleImages.Count > _currentIndex && _currentIndex >= 0)
                    name = Path.GetFileName(_singleImages[_currentIndex]);

                string spreadNotice = (_settings.ViewMode != ViewLayout.Single && _settings.SmartSpreadDetection && _rawBitmap != null && _rawBitmap.Width > _rawBitmap.Height * 1.15f) ? " [펼침면]" : "";
                if (_rawBitmap != null)
                {
                    _statusLabel.Text = string.Format("{0} [{1}/{2}] ({3}x{4}){5}", name, _currentIndex + 1, total, _rawBitmap.Width, _rawBitmap.Height, spreadNotice);
                }
            }

            UpdateViewAndFitStatusLabels();
        }

        private int GetTotalItems()
        {
            if (_zipEngine != null && _zipEngine.Count > 0)
                return _zipEngine.Count;
            if (_pdfEngine != null && _pdfEngine.PageCount > 0)
                return _pdfEngine.PageCount;
            if (_singleImages != null && _singleImages.Count > 0)
                return _singleImages.Count;
            return 0;
        }

        private void NavSlider_Scroll(object sender, EventArgs e)
        {
            if (_isNavSliderUpdating) return;
            int total = GetTotalItems();
            int targetIdx = _navSlider.Value - 1;
            if (targetIdx != _currentIndex && targetIdx >= 0 && targetIdx < total)
            {
                if (_settings.ViewMode == ViewLayout.Webtoon)
                {
                    ScrollToPageIndex(targetIdx);
                }
                else
                {
                    _currentIndex = targetIdx;
                    DisplayCurrentItem();
                }
            }
        }

        private void UpdateNavBar()
        {
            int total = GetTotalItems();
            if (total <= 0)
            {
                if (_navBar != null) _navBar.Visible = false;
                return;
            }

            if (!_settings.AutoHideUi && _navBar != null && !_navBar.Visible)
            {
                _navBar.Visible = true;
            }

            _isNavSliderUpdating = true;
            try
            {
                if (_navSlider != null)
                {
                    _navSlider.Minimum = 1;
                    _navSlider.Maximum = Math.Max(1, total);
                    _navSlider.Value = Math.Max(1, Math.Min(total, _currentIndex + 1));
                }

                if (_navModeBadge != null)
                {
                    if (_zipEngine != null && _zipEngine.Count > 0)
                    {
                        _navModeBadge.Text = "[ZIP]";
                        _navModeBadge.ForeColor = Color.FromArgb(255, 159, 243);
                    }
                    else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
                    {
                        _navModeBadge.Text = "[PDF]";
                        _navModeBadge.ForeColor = Color.FromArgb(255, 107, 107);
                    }
                    else
                    {
                        _navModeBadge.Text = "[폴더]";
                        _navModeBadge.ForeColor = Color.FromArgb(100, 180, 255);
                    }
                }

                if (_navPageLbl != null)
                {
                    if (_isDualPageDisplayed && _secondIndex >= 0)
                    {
                        _navPageLbl.Text = string.Format("{0}-{1} / {2}", _currentIndex + 1, _secondIndex + 1, total);
                    }
                    else
                    {
                        _navPageLbl.Text = string.Format("{0} / {1}", _currentIndex + 1, total);
                    }
                }
            }
            finally
            {
                _isNavSliderUpdating = false;
            }
        }

        private void ExecutePageOrSearchJump()
        {
            if (_navSearchBox == null) return;
            string query = _navSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            int total = GetTotalItems();
            if (total <= 0) return;

            int pageNum;
            bool isNumber = int.TryParse(query, out pageNum);

            // 1. 숫자가 1 ~ total 범위 내인 경우: 정확한 페이지 번호 점프!
            if (isNumber && pageNum >= 1 && pageNum <= total)
            {
                _currentIndex = pageNum - 1;
                DisplayCurrentItem();
                this.ActiveControl = null;
                return;
            }

            // 2. 숫자가 아니거나 범위를 벗어난 경우: 파일명 검색 이동 (현재 위치 다음부터 순환 탐색)
            int foundIdx = -1;
            for (int step = 1; step <= total; step++)
            {
                int checkIdx = (_currentIndex + step) % total;
                string fn = "";
                if (_zipEngine != null && _zipEngine.Count > 0)
                {
                    fn = _zipEngine.GetEntryName(checkIdx);
                }
                else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
                {
                    fn = string.Format("Page {0}", checkIdx + 1);
                }
                else if (_singleImages != null && checkIdx < _singleImages.Count)
                {
                    fn = Path.GetFileName(_singleImages[checkIdx]);
                }

                if (!string.IsNullOrEmpty(fn) && fn.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foundIdx = checkIdx;
                    break;
                }
            }

            if (foundIdx >= 0)
            {
                _currentIndex = foundIdx;
                DisplayCurrentItem();
                this.ActiveControl = null;
                return;
            }

            // 3. 파일명 매칭도 없고 숫자였던 경우 (범위 초과 시): 가장 가까운 첫/마지막 페이지로 이동
            if (isNumber)
            {
                _currentIndex = (pageNum > total) ? (total - 1) : 0;
                DisplayCurrentItem();
                this.ActiveControl = null;
                return;
            }

            // 4. 검색 결과 없음
            System.Media.SystemSounds.Beep.Play();
            if (_statusLabel != null)
            {
                _statusLabel.Text = string.Format("'{0}' 검색 결과가 없습니다.", query);
            }
        }

        private void UpdateCurrentImageInfo(Bitmap bmp, string name)
        {
            try
            {
                string fullPath = "";
                string archivePath = "";
                long size = 0;
                DateTime? dt = null;

                if (_zipEngine != null && _zipEngine.Count > 0)
                {
                    archivePath = _zipEngine.ZipPath;
                    size = _zipEngine.GetEntrySize(_currentIndex);
                    dt = _zipEngine.GetEntryDateTime(_currentIndex);
                }
                else if (_pdfEngine != null && _pdfEngine.PageCount > 0)
                {
                    archivePath = _pdfEngine.PdfPath;
                    if (File.Exists(_pdfEngine.PdfPath))
                    {
                        FileInfo fi = new FileInfo(_pdfEngine.PdfPath);
                        size = fi.Length;
                        dt = fi.LastWriteTime;
                    }
                }
                else if (_singleImages != null && _singleImages.Count > 0 && _currentIndex >= 0 && _currentIndex < _singleImages.Count)
                {
                    fullPath = _singleImages[_currentIndex];
                    if (File.Exists(fullPath))
                    {
                        FileInfo fi = new FileInfo(fullPath);
                        size = fi.Length;
                        dt = fi.LastWriteTime;
                    }
                }

                int curDelay = (_gifDelays != null && _gifCurrentFrame >= 0 && _gifCurrentFrame < _gifDelays.Length)
                    ? _gifDelays[_gifCurrentFrame]
                    : 0;

                _currentImageInfo = MetadataEngine.Extract(
                    bmp,
                    name,
                    fullPath,
                    archivePath,
                    size,
                    dt,
                    _isGif,
                    _gifFrameCount,
                    _gifCurrentFrame,
                    curDelay
                );
            }
            catch { }
        }

        // GIF 프레임 디코더 연동
        private void SetupGifPlayer(Bitmap gifBmp)
        {
            try
            {
                _rawBitmap = gifBmp;
                if (_rawBitmap.FrameDimensionsList == null || _rawBitmap.FrameDimensionsList.Length == 0)
                {
                    _isGif = false;
                    _gifBar.Visible = false;
                    if (_filteredBitmap != null && _filteredBitmap != _rawBitmap) _filteredBitmap.Dispose();
                    _filteredBitmap = FilterEngine.ApplyFilter(_rawBitmap, _currentFilter);
                    return;
                }

                _gifDimension = new FrameDimension(_rawBitmap.FrameDimensionsList[0]);
                _gifFrameCount = _rawBitmap.GetFrameCount(_gifDimension);

                if (_gifFrameCount > 1)
                {
                    _isGif = true;
                    _gifCurrentFrame = 0;
                    _gifBar.Visible = !_settings.AutoHideUi;

                    _gifSlider.Maximum = _gifFrameCount - 1;
                    _gifSlider.Value = 0;

                    // 프레임별 딜레이 추출
                    _gifDelays = new int[_gifFrameCount];
                    byte[] propValue = null;
                    try
                    {
                        PropertyItem prop = _rawBitmap.GetPropertyItem(0x5100);
                        if (prop != null) propValue = prop.Value;
                    }
                    catch { }

                    for (int i = 0; i < _gifFrameCount; i++)
                    {
                        int delay = 100;
                        if (propValue != null && (i + 1) * 4 <= propValue.Length)
                        {
                            int raw = BitConverter.ToInt32(propValue, i * 4) * 10;
                            if (raw > 0) delay = raw;
                        }
                        _gifDelays[i] = delay;
                    }

                    // 첫 프레임 즉각 렌더링
                    RenderGifFrame();

                    _gifTimer.Interval = Math.Max(10, (int)(_gifDelays[0] / _gifSpeed));
                    _gifTimer.Start();
                    _gifPlayBtn.Text = "일시정지";
                }
                else
                {
                    _isGif = false;
                    _gifBar.Visible = false;
                    if (_filteredBitmap != null && _filteredBitmap != _rawBitmap) _filteredBitmap.Dispose();
                    _filteredBitmap = FilterEngine.ApplyFilter(_rawBitmap, _currentFilter);
                }
            }
            catch
            {
                _isGif = false;
                _gifBar.Visible = false;
                if (_filteredBitmap != null && _filteredBitmap != _rawBitmap) _filteredBitmap.Dispose();
                _filteredBitmap = FilterEngine.ApplyFilter(_rawBitmap, _currentFilter);
            }
        }

        private void GifTimer_Tick(object sender, EventArgs e)
        {
            if (!_isGif || _gifFrameCount <= 1) return;
            _gifCurrentFrame = (_gifCurrentFrame + 1) % _gifFrameCount;
            RenderGifFrame();
            _gifTimer.Interval = Math.Max(10, (int)(_gifDelays[_gifCurrentFrame] / _gifSpeed));
        }

        private void RenderGifFrame()
        {
            try
            {
                _rawBitmap.SelectActiveFrame(_gifDimension, _gifCurrentFrame);
                if (_filteredBitmap != null && _filteredBitmap != _rawBitmap) _filteredBitmap.Dispose();
                _filteredBitmap = FilterEngine.ApplyFilter(_rawBitmap, _currentFilter);
                UpdateGifFrameUi();

                if (_showExifOverlay && _currentImageInfo != null)
                {
                    _currentImageInfo.GifCurrentFrame = _gifCurrentFrame;
                    if (_gifDelays != null && _gifCurrentFrame < _gifDelays.Length)
                        _currentImageInfo.GifDelayMs = _gifDelays[_gifCurrentFrame];
                }

                int total = _zipEngine.Count > 0 ? _zipEngine.Count : _singleImages.Count;
                string name = _zipEngine.Count > 0 ? _zipEngine.GetEntryName(_currentIndex) : (_singleImages.Count > 0 ? Path.GetFileName(_singleImages[_currentIndex]) : "");
                _statusLabel.Text = string.Format("{0} [{1}/{2}] ({3}x{4})  [GIF: {5}/{6} 프레임]", 
                    name, _currentIndex + 1, total, _rawBitmap.Width, _rawBitmap.Height, _gifCurrentFrame + 1, _gifFrameCount);

                this.Invalidate();
            }
            catch { }
        }

        private void UpdateGifFrameUi()
        {
            _gifSlider.Value = _gifCurrentFrame;
            _gifFrameLbl.Text = string.Format("{0} / {1}", _gifCurrentFrame + 1, _gifFrameCount);
        }

        private void ToggleGifPlay()
        {
            if (_gifTimer.Enabled)
            {
                _gifTimer.Stop();
                _gifPlayBtn.Text = "재생";
            }
            else
            {
                _gifTimer.Start();
                _gifPlayBtn.Text = "일시정지";
            }
        }

        private void StepGifFrame(int step)
        {
            _gifTimer.Stop();
            _gifPlayBtn.Text = "재생";
            _gifCurrentFrame = (_gifCurrentFrame + step + _gifFrameCount) % _gifFrameCount;
            RenderGifFrame();
        }

        private void SeekGifFrame(int index)
        {
            _gifTimer.Stop();
            _gifPlayBtn.Text = "재생";
            _gifCurrentFrame = Math.Max(0, Math.Min(_gifFrameCount - 1, index));
            RenderGifFrame();
        }

        // 화면 맞춤 및 크기 계산 (화면맞춤, 폭맞춤, 높이맞춤, 원본크기 지원)
        private void FitToScreen()
        {
            if (_filteredBitmap == null) return;
            int viewW = this.ClientSize.Width;
            int viewH = this.ClientSize.Height;

            int topH = (!_settings.AutoHideUi && _topBar.Visible) ? _topBar.Height : 0;
            int navH = (!_settings.AutoHideUi && _navBar != null && _navBar.Visible) ? _navBar.Height : 0;
            int botH = (!_settings.AutoHideUi) ? ((_gifBar.Visible ? _gifBar.Height : 0) + navH + (_statusStrip.Visible ? _statusStrip.Height : 0)) : 0;
            viewH -= (topH + botH);
            if (viewH < 10) viewH = 10;

            int totalW = _filteredBitmap.Width;
            int totalH = _filteredBitmap.Height;

            if (_isDualPageDisplayed && _filteredBitmap2 != null)
            {
                totalW = _filteredBitmap.Width + _filteredBitmap2.Width + 4; // 4px gap
                totalH = Math.Max(_filteredBitmap.Height, _filteredBitmap2.Height);
            }

            int rot = Math.Abs(_rotation % 180);
            int bw = (rot == 90) ? totalH : totalW;
            int bh = (rot == 90) ? totalW : totalH;

            int margin = 16;
            float availW = Math.Max(10, viewW - margin);
            float availH = Math.Max(10, viewH - margin);

            float sx = availW / bw;
            float sy = availH / bh;
            float targetScale = 1.0f;

            switch (_settings.FitMode)
            {
                case DisplayFitMode.FitWindow:
                    targetScale = Math.Min(sx, sy);
                    if (!_settings.StretchSmallImages)
                        targetScale = Math.Min(targetScale, 1.0f);
                    break;

                case DisplayFitMode.FitWidth:
                    targetScale = sx;
                    if (!_settings.StretchSmallImages)
                        targetScale = Math.Min(targetScale, 1.0f);
                    break;

                case DisplayFitMode.FitHeight:
                    targetScale = sy;
                    if (!_settings.StretchSmallImages)
                        targetScale = Math.Min(targetScale, 1.0f);
                    break;

                case DisplayFitMode.Original:
                default:
                    targetScale = 1.0f;
                    break;
            }

            _scale = Math.Max(0.02f, Math.Min(50.0f, targetScale));

            // 기본 위치 정렬
            float centerY = topH + viewH / 2f;
            // 폭 맞춤(FitWidth)이고 세로 높이가 화면보다 큰 경우 위쪽부터 읽을 수 있도록 상단 정렬
            if (_settings.FitMode == DisplayFitMode.FitWidth)
            {
                float renderedH = bh * _scale;
                if (renderedH > viewH)
                {
                    centerY = topH + (renderedH / 2f) + (margin / 2f);
                }
            }

            _pan = new PointF(viewW / 2f, centerY);

            UpdateZoomLabel();
            this.Invalidate();
        }

        private void RotateCw()
        {
            _rotation = (_rotation + 90) % 360;
            this.Invalidate();
        }

        private void UpdateZoomLabel()
        {
            _zoomLabel.Text = string.Format("{0}%", (int)(_scale * 100));
        }

        private void ZoomIn()
        {
            ZoomStep(1.20f);
        }

        private void ZoomOut()
        {
            ZoomStep(1.0f / 1.20f);
        }

        private void ZoomStep(float factor)
        {
            if (_filteredBitmap == null) return;
            float oldScale = _scale;
            _scale = Math.Max(0.05f, Math.Min(30.0f, _scale * factor));

            // 화면 중심 기준 확대/축소
            float cx = this.ClientSize.Width / 2f;
            float cy = this.ClientSize.Height / 2f;
            _pan.X = cx - (cx - _pan.X) * (_scale / oldScale);
            _pan.Y = cy - (cy - _pan.Y) * (_scale / oldScale);

            UpdateZoomLabel();
            this.Invalidate();
        }

        private void ToggleMinimap()
        {
            _showMinimap = !_showMinimap;
            _settings.ShowMinimap = _showMinimap;
            _settings.Save();
            this.Invalidate();
        }

        private void CenterViewportOnMinimap(int mouseX, int mouseY)
        {
            if (_filteredBitmap == null || _minimapRect.IsEmpty) return;

            int totalW = _filteredBitmap.Width;
            int totalH = _filteredBitmap.Height;
            if (_isDualPageDisplayed && _filteredBitmap2 != null)
            {
                totalW = _filteredBitmap.Width + _filteredBitmap2.Width + 4;
                totalH = Math.Max(_filteredBitmap.Height, _filteredBitmap2.Height);
            }

            int rot = Math.Abs(_rotation % 180);
            int bw = (rot == 90) ? totalH : totalW;
            int bh = (rot == 90) ? totalW : totalH;

            float rx = (float)(mouseX - _minimapRect.X) / _minimapRect.Width;
            float ry = (float)(mouseY - _minimapRect.Y) / _minimapRect.Height;
            rx = Math.Max(0f, Math.Min(1f, rx));
            ry = Math.Max(0f, Math.Min(1f, ry));

            float targetImgX = (rx - 0.5f) * bw;
            float targetImgY = (ry - 0.5f) * bh;

            float cx = this.ClientSize.Width / 2f;
            float cy = this.ClientSize.Height / 2f;
            _pan.X = cx - targetImgX * _scale;
            _pan.Y = cy - targetImgY * _scale;

            this.Invalidate();
        }

        private void DrawMinimap(Graphics g)
        {
            if (_filteredBitmap == null) return;

            int totalW = _filteredBitmap.Width;
            int totalH = _filteredBitmap.Height;
            if (_isDualPageDisplayed && _filteredBitmap2 != null)
            {
                totalW = _filteredBitmap.Width + _filteredBitmap2.Width + 4;
                totalH = Math.Max(_filteredBitmap.Height, _filteredBitmap2.Height);
            }

            int rot = Math.Abs(_rotation % 180);
            int bw = (rot == 90) ? totalH : totalW;
            int bh = (rot == 90) ? totalW : totalH;

            int viewW = this.ClientSize.Width;
            int viewH = this.ClientSize.Height;

            // 이미지가 화면 크기보다 크게 확대되어 일부가 가려진 경우에 미니맵 표시
            bool isZoomed = (bw * _scale > viewW + 15 || bh * _scale > viewH + 15);
            if (!isZoomed)
            {
                _minimapRect = Rectangle.Empty;
                return;
            }

            // 미니맵 크기 계산 (최대 180x130, 가로세로 비율 유지)
            float aspect = (float)bw / bh;
            int maxW = 180;
            int maxH = 130;
            int mapW, mapH;
            if (aspect >= 1.0f)
            {
                mapW = maxW;
                mapH = Math.Max(40, (int)(maxW / aspect));
            }
            else
            {
                mapH = maxH;
                mapW = Math.Max(40, (int)(maxH * aspect));
            }

            int botOffset = (!_settings.AutoHideUi && _statusStrip.Visible) ? _statusStrip.Height + 14 : 14;
            if (_navBar != null && _navBar.Visible && !_settings.AutoHideUi) botOffset += _navBar.Height;
            if (_isGif && _gifBar.Visible && !_settings.AutoHideUi) botOffset += _gifBar.Height;

            int mapX = viewW - mapW - 14;
            int mapY = viewH - mapH - botOffset;
            _minimapRect = new Rectangle(mapX, mapY, mapW, mapH);

            // 1. 미니맵 배경
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(205, 16, 18, 24)))
            {
                g.FillRectangle(bgBrush, _minimapRect);
            }

            // 2. 썸네일 그리기
            InterpolationMode prevMode = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.Low;
            if (_isDualPageDisplayed && _filteredBitmap2 != null)
            {
                Bitmap bmpLeft = (_settings.ViewMode == ViewLayout.DualRtl) ? _filteredBitmap2 : _filteredBitmap;
                Bitmap bmpRight = (_settings.ViewMode == ViewLayout.DualRtl) ? _filteredBitmap : _filteredBitmap2;

                int leftW = (int)((float)bmpLeft.Width / totalW * mapW);
                int rightW = mapW - leftW;
                int leftH = (int)((float)bmpLeft.Height / totalH * mapH);
                int rightH = (int)((float)bmpRight.Height / totalH * mapH);

                g.DrawImage(bmpLeft, new Rectangle(mapX, mapY + (mapH - leftH) / 2, leftW, leftH));
                g.DrawImage(bmpRight, new Rectangle(mapX + leftW, mapY + (mapH - rightH) / 2, rightW, rightH));
            }
            else
            {
                g.DrawImage(_filteredBitmap, _minimapRect);
            }
            g.InterpolationMode = prevMode;

            // 3. 현재 뷰포트(화면 표시 영역) 사각형 계산
            float vLeftImg = (0 - _pan.X) / _scale + (bw / 2f);
            float vTopImg = (0 - _pan.Y) / _scale + (bh / 2f);
            float vRightImg = (viewW - _pan.X) / _scale + (bw / 2f);
            float vBottomImg = (viewH - _pan.Y) / _scale + (bh / 2f);

            float scaleX = (float)mapW / bw;
            float scaleY = (float)mapH / bh;

            float vx = mapX + vLeftImg * scaleX;
            float vy = mapY + vTopImg * scaleY;
            float vw = (vRightImg - vLeftImg) * scaleX;
            float vh = (vBottomImg - vTopImg) * scaleY;

            RectangleF vpRect = new RectangleF(vx, vy, vw, vh);

            // 미니맵 영역 클리핑 처리
            Region oldClip = g.Clip;
            g.SetClip(_minimapRect);

            using (SolidBrush vpBrush = new SolidBrush(Color.FromArgb(45, 78, 140, 255)))
            using (Pen vpPen = new Pen(Color.FromArgb(230, 78, 140, 255), 1.5f))
            {
                g.FillRectangle(vpBrush, vpRect);
                g.DrawRectangle(vpPen, vpRect.X, vpRect.Y, vpRect.Width, vpRect.Height);
            }

            g.Clip = oldClip;

            // 4. 미니맵 외곽 테두리 & 미니 라벨
            using (Pen borderPen = new Pen(Color.FromArgb(80, 90, 115), 1f))
            {
                g.DrawRectangle(borderPen, _minimapRect);
            }

            using (Font fMini = new Font("Segoe UI", 7F, FontStyle.Bold))
            using (Brush bMini = new SolidBrush(Color.FromArgb(130, 145, 175)))
            {
                g.DrawString("MINIMAP", fMini, bMini, mapX + 4, mapY + 3);
            }
        }

        // 캔버스 렌더링
        private void MainForm_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.FromArgb(18, 19, 22));

            if (_filteredBitmap == null)
            {
                int topOffset = (_topBar != null && _topBar.Visible) ? _topBar.Height : 0;
                int bottomOffset = (_statusStrip != null && _statusStrip.Visible) ? _statusStrip.Height : 0;
                Rectangle canvasRect = new Rectangle(0, topOffset, this.ClientSize.Width, Math.Max(10, this.ClientSize.Height - topOffset - bottomOffset));

                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    string title = "nyaa-view (냐-뷰)";
                    string desc = "이미지 파일 (JPG, PNG, WebP, GIF, TGA, PSD, DDS, TIFF, QOI...)\n" +
                                 "만화 압축 파일 (ZIP, CBZ) 및 전자책 문서 (PDF)\n\n" +
                                 "파일 또는 압축파일을 창으로 끌어다 놓으세요.\n\n" +
                                 "[ 드래그: 창 이동 | 더블 클릭: 창 최대화/복원 | 마우스 휠: 파일 열기 ]";

                    using (Font titleFont = new Font("Segoe UI", 18F, FontStyle.Bold))
                    using (Font descFont = new Font("Segoe UI", 11F, FontStyle.Regular))
                    using (Brush titleBrush = new SolidBrush(Color.FromArgb(220, 225, 240)))
                    using (Brush descBrush = new SolidBrush(Color.FromArgb(140, 146, 165)))
                    {
                        SizeF titleSize = g.MeasureString(title, titleFont, canvasRect.Width);
                        SizeF descSize = g.MeasureString(desc, descFont, canvasRect.Width);
                        float gap = 14f;
                        float totalH = titleSize.Height + gap + descSize.Height;

                        float startY = canvasRect.Y + (canvasRect.Height - totalH) / 2f;
                        RectangleF titleRect = new RectangleF(canvasRect.X, startY, canvasRect.Width, titleSize.Height);
                        RectangleF descRect = new RectangleF(canvasRect.X, startY + titleSize.Height + gap, canvasRect.Width, descSize.Height);

                        g.DrawString(title, titleFont, titleBrush, titleRect, sf);
                        g.DrawString(desc, descFont, descBrush, descRect, sf);
                    }
                }
                return;
            }

            // 웹툰 모드 전용 렌더링 (세로 무한 연속 스크롤)
            if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                DrawWebtoon(g);
                if (_showExifOverlay && _currentImageInfo != null)
                {
                    DrawExifOverlay(g);
                }
                return;
            }

            switch (_settings.InterpolationMode)
            {
                case InterpolationModeType.NearestNeighbor:
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half; // 픽셀 아트에서 정확한 픽셀 격자 정렬
                    break;

                case InterpolationModeType.Bilinear:
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    break;

                case InterpolationModeType.Bicubic:
                default:
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    break;
            }

            // 좌표 변환 (Pan & Zoom & Rotate)
            Matrix mat = new Matrix();
            mat.Translate(_pan.X, _pan.Y);
            mat.Scale(_scale, _scale);
            mat.Rotate(_rotation);
            g.Transform = mat;

            if (_isDualPageDisplayed && _filteredBitmap2 != null)
            {
                // 보기 모드에 따라 좌측/우측 배치 (DualRtl은 오른쪽이 앞페이지, 왼쪽이 뒷페이지)
                Bitmap bmpLeft = (_settings.ViewMode == ViewLayout.DualRtl) ? _filteredBitmap2 : _filteredBitmap;
                Bitmap bmpRight = (_settings.ViewMode == ViewLayout.DualRtl) ? _filteredBitmap : _filteredBitmap2;

                int gap = 4;
                int totalW = bmpLeft.Width + bmpRight.Width + gap;
                int totalH = Math.Max(bmpLeft.Height, bmpRight.Height);

                int xLeft = -totalW / 2;
                int yLeft = -bmpLeft.Height / 2;
                g.DrawImage(bmpLeft, new Rectangle(xLeft, yLeft, bmpLeft.Width, bmpLeft.Height));

                int xRight = xLeft + bmpLeft.Width + gap;
                int yRight = -bmpRight.Height / 2;
                g.DrawImage(bmpRight, new Rectangle(xRight, yRight, bmpRight.Width, bmpRight.Height));

                // 책 중앙 접지선 (Gutter Shadow Line)
                using (Pen gutterPen = new Pen(Color.FromArgb(90, 8, 10, 14), 2))
                {
                    g.DrawLine(gutterPen, xRight - 2, -totalH / 2, xRight - 2, totalH / 2);
                }
            }
            else
            {
                int bw = _filteredBitmap.Width;
                int bh = _filteredBitmap.Height;
                Rectangle rect = new Rectangle(-bw / 2, -bh / 2, bw, bh);
                g.DrawImage(_filteredBitmap, rect);
            }
            g.ResetTransform();

            // 미니맵(Minimap) 내비게이션 렌더링
            if (_showMinimap && _filteredBitmap != null)
            {
                DrawMinimap(g);
            }

            // EXIF / 메타데이터 HUD 오버레이 그리기
            if (_showExifOverlay && _currentImageInfo != null)
            {
                DrawExifOverlay(g);
            }

            // 창 테두리 1px 아웃라인 (최대화가 아닐 때 윈도우 경계선 구분)
            if (this.WindowState != FormWindowState.Maximized)
            {
                using (Pen borderPen = new Pen(Color.FromArgb(50, 54, 66), 1))
                {
                    g.DrawRectangle(borderPen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
            }
        }

        // EXIF & 이미지 정보 HUD 오버레이 렌더링
        private void DrawExifOverlay(Graphics g)
        {
            if (_currentImageInfo == null) return;

            int x = 16;
            int y = _topBar.Visible ? (_topBar.Height + 12) : 16;
            int w = Math.Min(410, this.ClientSize.Width - 32);

            // 높이 계산
            int h = 42;
            foreach (var sec in _currentImageInfo.Sections)
            {
                h += 24;
                h += sec.Items.Count * 20;
                h += 6;
            }
            h += 12;

            int navH = (_navBar != null && _navBar.Visible) ? _navBar.Height : 0;
            int bottomMargin = (_statusStrip.Visible ? _statusStrip.Height : 0) + navH + (_gifBar.Visible ? _gifBar.Height : 0);
            int maxH = this.ClientSize.Height - y - bottomMargin - 16;
            h = Math.Min(h, maxH);

            Rectangle cardRect = new Rectangle(x, y, w, h);

            // 1. 반투명 배경 및 테두리 (다크 아크릴 HUD)
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(238, 20, 22, 28)))
            using (Pen borderPen = new Pen(Color.FromArgb(70, 75, 90), 1))
            {
                g.FillRectangle(bgBrush, cardRect);
                g.DrawRectangle(borderPen, cardRect);
            }

            // 2. 상단 헤더 바
            Rectangle headerRect = new Rectangle(x, y, w, 36);
            using (SolidBrush headBrush = new SolidBrush(Color.FromArgb(28, 31, 40)))
            using (Pen headLinePen = new Pen(Color.FromArgb(60, 65, 80), 1))
            {
                g.FillRectangle(headBrush, headerRect);
                g.DrawLine(headLinePen, x, y + 36, x + w, y + 36);
            }

            using (Font headFont = new Font("Segoe UI", 10F, FontStyle.Bold))
            using (Brush headBrush = new SolidBrush(Color.FromArgb(255, 159, 243)))
            {
                g.DrawString("🐾 이미지 정보 & EXIF", headFont, headBrush, x + 10, y + 8);
            }

            using (Font hintFont = new Font("Segoe UI", 8F, FontStyle.Regular))
            using (Brush hintBrush = new SolidBrush(Color.FromArgb(140, 145, 160)))
            {
                g.DrawString(string.Format("[{0}] / [Esc] 닫기", _settings.ExifKey), hintFont, hintBrush, x + 175, y + 11);
            }

            // 닫기 버튼 [✕]
            _exifCloseRect = new Rectangle(x + w - 28, y + 8, 20, 20);
            using (Font closeFont = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (Brush closeBrush = new SolidBrush(Color.FromArgb(200, 205, 220)))
            {
                g.DrawString("✕", closeFont, closeBrush, _exifCloseRect.X + 4, _exifCloseRect.Y + 2);
            }

            // 3. 메타데이터 섹션별 렌더링
            int curY = y + 44;
            using (Font fontCat = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (Font fontKey = new Font("Segoe UI", 8.5F, FontStyle.Regular))
            using (Font fontVal = new Font("Segoe UI", 8.5F, FontStyle.Regular))
            using (Brush brushCat = new SolidBrush(Color.FromArgb(100, 160, 255)))
            using (Brush brushKey = new SolidBrush(Color.FromArgb(150, 155, 170)))
            using (Brush brushVal = new SolidBrush(Color.FromArgb(240, 240, 248)))
            using (Pen linePen = new Pen(Color.FromArgb(45, 48, 60), 1))
            {
                foreach (var sec in _currentImageInfo.Sections)
                {
                    if (curY + 24 > y + h) break;

                    g.DrawString(sec.Title, fontCat, brushCat, x + 10, curY);
                    g.DrawLine(linePen, x + 10, curY + 18, x + w - 10, curY + 18);
                    curY += 22;

                    foreach (var kvp in sec.Items)
                    {
                        if (curY + 18 > y + h) break;

                        g.DrawString(kvp.Key, fontKey, brushKey, x + 12, curY);

                        Rectangle valRect = new Rectangle(x + 108, curY, w - 120, 18);
                        StringFormat sf = new StringFormat
                        {
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        };
                        g.DrawString(kvp.Value, fontVal, brushVal, valRect, sf);

                        curY += 19;
                    }
                    curY += 6;
                }
            }
        }

        private void ToggleExifOverlay()
        {
            _showExifOverlay = !_showExifOverlay;
            this.Invalidate();
        }

        // 마우스 조작 (줌, 팬, 휠 모드)
        private void MainForm_MouseWheel(object sender, MouseEventArgs e)
        {
            // 초기 화면(이미지가 열리지 않은 상태)에서 휠 스크롤 시 파일 열기 창 즉시 호출
            if (_filteredBitmap == null)
            {
                OpenFileDialogHandler();
                return;
            }

            if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                {
                    // Ctrl + 휠: 웹툰 줌 인/아웃
                    if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
                    float factor = (e.Delta > 0) ? 1.10f : (1.0f / 1.10f);
                    _webtoonZoom = Math.Max(0.25f, Math.Min(4.0f, _webtoonZoom * factor));
                    if (Math.Abs(_webtoonZoom - 1.0f) < 0.02f)
                    {
                        _webtoonZoom = 1.0f;
                        _webtoonPanX = 0f;
                    }
                    ClampWebtoonScroll();
                    UpdateZoomLabelWebtoon();
                    this.Invalidate();
                    return;
                }

                int deltaW = _settings.WheelInvert ? -e.Delta : e.Delta;
                float scrollAmount = -(deltaW / 120f) * 220f;
                SmoothScrollWebtoonBy(scrollAmount);
                return;
            }

            bool isPageMode = (_settings.WheelMode == 1);
            if (_settings.WheelCtrlAlternate && (Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                isPageMode = !isPageMode;
            }

            int delta = _settings.WheelInvert ? -e.Delta : e.Delta;

            if (isPageMode)
            {
                // 페이지 넘기기 모드
                // 만약 세로 길이가 화면보다 길다면 상하 스크롤 우선, 끝에 도달하면 페이지 전환
                int totalH = _filteredBitmap != null ? _filteredBitmap.Height : 0;
                if (_isDualPageDisplayed && _filteredBitmap2 != null)
                {
                    totalH = Math.Max(totalH, _filteredBitmap2.Height);
                }

                int rot = Math.Abs(_rotation % 180);
                int bh = (rot == 90) ? (_filteredBitmap != null ? _filteredBitmap.Width : 0) : totalH;
                float renderedH = bh * _scale;
                int viewH = this.ClientSize.Height;

                if (renderedH > viewH + 20)
                {
                    float step = 160f;
                    if (delta < 0)
                    {
                        // 아래로 스크롤
                        float bottomLimit = -(renderedH / 2f - viewH);
                        if (_pan.Y > bottomLimit)
                        {
                            _pan.Y = Math.Max(bottomLimit, _pan.Y - step);
                            this.Invalidate();
                            return;
                        }
                    }
                    else if (delta > 0)
                    {
                        // 위로 스크롤
                        float topLimit = (renderedH / 2f);
                        if (_pan.Y < topLimit)
                        {
                            _pan.Y = Math.Min(topLimit, _pan.Y + step);
                            this.Invalidate();
                            return;
                        }
                    }
                }

                if (delta > 0)
                {
                    PrevImage();
                }
                else if (delta < 0)
                {
                    NextImage();
                }
            }
            else
            {
                // 확대 / 축소 모드
                float oldScale = _scale;
                float factor = delta > 0 ? 1.15f : 0.87f;
                _scale = Math.Max(0.05f, Math.Min(30.0f, _scale * factor));

                // 마우스 커서 중심 줌 보정
                _pan.X = e.X - (e.X - _pan.X) * (_scale / oldScale);
                _pan.Y = e.Y - (e.Y - _pan.Y) * (_scale / oldScale);

                UpdateZoomLabel();
                this.Invalidate();
            }
        }

        private void MainForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (_showExifOverlay && _exifCloseRect.Contains(e.Location))
            {
                _showExifOverlay = false;
                this.Invalidate();
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // 빈 화면일 때:
                if (_filteredBitmap == null)
                {
                    // 더블 클릭 감지 시 즉시 창 최대화 / 이전 크기 복원 토글
                    if (e.Clicks >= 2)
                    {
                        _emptyMouseDown = false;
                        ToggleMaximize();
                        return;
                    }

                    // 마우스 누름 시작 기록 (이후 MouseMove에서 드래그 임계값 초과 시 창 이동 시작)
                    _emptyMouseDown = true;
                    _emptyMouseDownPos = e.Location;
                    return;
                }

                // 미니맵 클릭 또는 드래그 시작
                if (_showMinimap && !_minimapRect.IsEmpty && _minimapRect.Contains(e.Location))
                {
                    _isDraggingMinimap = true;
                    CenterViewportOnMinimap(e.X, e.Y);
                    return;
                }

                // 웹툰 모드 드래그 스크롤 시작
                if (_settings.ViewMode == ViewLayout.Webtoon)
                {
                    if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
                    _isDraggingWebtoon = true;
                    _dragStartWebtoonPoint = e.Location;
                    _dragStartWebtoonScrollY = _webtoonScrollY;
                    _dragStartWebtoonPanX = _webtoonPanX;
                    _webtoonTargetScrollY = _webtoonScrollY;
                    this.Cursor = Cursors.SizeAll;
                    return;
                }

                _isDragging = true;
                _dragStartPoint = e.Location;
                this.Cursor = Cursors.SizeAll;
            }
        }

        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            // 빈 화면에서 마우스 왼쪽 버튼을 누른 채 드래그 이동 감지
            if (_emptyMouseDown && _filteredBitmap == null && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                int dx = Math.Abs(e.X - _emptyMouseDownPos.X);
                int dy = Math.Abs(e.Y - _emptyMouseDownPos.Y);
                int threshold = Math.Max(4, SystemInformation.DragSize.Width);
                if (dx >= threshold || dy >= threshold)
                {
                    _emptyMouseDown = false;
                    DragWindow();
                    return;
                }
            }

            if (_isDraggingMinimap)
            {
                CenterViewportOnMinimap(e.X, e.Y);
                return;
            }

            if (_isDraggingWebtoon && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                float dy = e.Y - _dragStartWebtoonPoint.Y;
                _webtoonScrollY = _dragStartWebtoonScrollY - dy;
                ClampWebtoonScroll();
                _webtoonTargetScrollY = _webtoonScrollY;
                if (_webtoonZoom > 1.0f)
                {
                    float dx = e.X - _dragStartWebtoonPoint.X;
                    _webtoonPanX = _dragStartWebtoonPanX + dx;
                }
                UpdateWebtoonCurrentIndex();
                this.Invalidate();
                return;
            }

            if (_isDragging)
            {
                _pan.X += (e.X - _dragStartPoint.X);
                _pan.Y += (e.Y - _dragStartPoint.Y);
                _dragStartPoint = e.Location;
                this.Invalidate();
                return;
            }

            // 미니맵 위로 마우스 호버 시 손 모양 커서
            if (_showMinimap && !_minimapRect.IsEmpty && _minimapRect.Contains(e.Location))
            {
                this.Cursor = Cursors.Hand;
            }
            else
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void MainForm_MouseUp(object sender, MouseEventArgs e)
        {
            _emptyMouseDown = false;

            if (_isDraggingMinimap)
            {
                _isDraggingMinimap = false;
                this.Cursor = Cursors.Default;
            }

            if (_isDraggingWebtoon)
            {
                _isDraggingWebtoon = false;
                _webtoonTargetScrollY = _webtoonScrollY;
                this.Cursor = Cursors.Default;
            }

            if (_isDragging)
            {
                _isDragging = false;
                this.Cursor = Cursors.Default;
            }

            if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu(e.Location);
            }
        }

        // 전역 단축키 & 내비게이션 처리 (Tab, 복수 다음/이전 키, 휠 토글 등)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;

            // 검색 입력창에 포커스가 있을 때 단축키 가로채기 방지
            if (_navSearchBox != null && _navSearchBox.Focused)
            {
                if (key == Keys.Enter)
                {
                    ExecutePageOrSearchJump();
                    return true;
                }
                if (key == Keys.Escape)
                {
                    this.ActiveControl = null;
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            // Ctrl + F: 하단 검색 / 페이지 이동 입력창으로 즉시 포커스
            if (key == Keys.F && (Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                if (_navBar != null && GetTotalItems() > 0)
                {
                    if (!_navBar.Visible) _navBar.Visible = true;
                    if (_navSearchBox != null)
                    {
                        _navSearchBox.Focus();
                        _navSearchBox.SelectAll();
                    }
                    return true;
                }
            }

            // F11: 창 최대화 / 복원 토글
            if (key == Keys.F11)
            {
                ToggleMaximize();
                return true;
            }

            // 1. 정보 / EXIF 토글 (기본: Tab)
            if (key == _settings.ExifKey)
            {
                ToggleExifOverlay();
                return true;
            }

            // 2. ESC 키: 오버레이 닫기 또는 최대화 해제
            if (key == Keys.Escape)
            {
                if (_showExifOverlay)
                {
                    _showExifOverlay = false;
                    this.Invalidate();
                    return true;
                }
                if (this.WindowState == FormWindowState.Maximized)
                {
                    ToggleMaximize();
                    return true;
                }
            }

            // 3. 탐색기 창 토글 (기본: E)
            if (key == _settings.ExplorerKey)
            {
                ToggleExplorer();
                return true;
            }

            // Shift + V: 펼침면(스프레드) 스마트 단독 감지 토글
            if (key == Keys.V && (Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                ToggleSmartSpreadDetection();
                return true;
            }

            // V: 보기 모드 순환 토글 (1장 ↔ 2장 우→좌 ↔ 2장 좌→우)
            if (key == Keys.V && (Control.ModifierKeys & Keys.Shift) != Keys.Shift)
            {
                CycleViewMode();
                return true;
            }

            // I: 이미지 보간 방식 순환 토글 (Bicubic ↔ Bilinear ↔ Nearest Neighbor)
            if (key == Keys.I && (Control.ModifierKeys & Keys.Control) != Keys.Control && (Control.ModifierKeys & Keys.Alt) != Keys.Alt)
            {
                CycleInterpolationMode();
                return true;
            }

            // Ctrl + 1, 2, 3, 4: 보기 모드 직접 지정
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                if (key == Keys.D1 || key == Keys.NumPad1) { SetViewMode(ViewLayout.Single); return true; }
                if (key == Keys.D2 || key == Keys.NumPad2) { SetViewMode(ViewLayout.DualRtl); return true; }
                if (key == Keys.D3 || key == Keys.NumPad3) { SetViewMode(ViewLayout.DualLtr); return true; }
                if (key == Keys.D4 || key == Keys.NumPad4) { SetViewMode(ViewLayout.Webtoon); return true; }
                if (key == Keys.D0 || key == Keys.NumPad0)
                {
                    if (_settings.ViewMode == ViewLayout.Webtoon)
                    {
                        _webtoonZoom = 1.0f;
                        _webtoonPanX = 0f;
                        UpdateZoomLabelWebtoon();
                        this.Invalidate();
                        return true;
                    }
                }
            }

            // W: 가로 폭에 맞춤 (Fit to Width)
            if (key == Keys.W)
            {
                SetFitMode(DisplayFitMode.FitWidth);
                return true;
            }

            // Shift + F: 세로 높이에 맞춤 (Fit to Height)
            if (key == _settings.FitKey && (Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                SetFitMode(DisplayFitMode.FitHeight);
                return true;
            }

            // 4. 화면 맞춤 (기본: F)
            if (key == _settings.FitKey && (Control.ModifierKeys & Keys.Shift) != Keys.Shift)
            {
                SetFitMode(DisplayFitMode.FitWindow);
                return true;
            }

            // O: 원본 크기 (100% 1:1) - Ctrl+O(파일 열기)와 구분
            if (key == Keys.O && (Control.ModifierKeys & Keys.Control) != Keys.Control)
            {
                SetFitMode(DisplayFitMode.Original);
                return true;
            }

            // S: 작은 그림 크게 보기 (확대 허용) 토글
            if (key == Keys.S && (Control.ModifierKeys & Keys.Control) != Keys.Control)
            {
                ToggleStretchSmallImages();
                return true;
            }

            // 5. 회전 (기본: R)
            if (key == _settings.RotateKey)
            {
                RotateCw();
                return true;
            }

            // 파일 열기 (Ctrl+O) / 폴더 열기 (Ctrl+Shift+O)
            if (key == Keys.O && (Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    OpenFolderDialogHandler();
                }
                else
                {
                    OpenFileDialogHandler();
                }
                return true;
            }

            // 외부 편집기 실행 (Ctrl+E)
            if (key == Keys.E && (Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                OpenWithExternalEditor();
                return true;
            }

            // 6. 마우스 휠 동작 토글 (기본: Z)
            if (key == _settings.WheelToggleKey)
            {
                ToggleWheelMode();
                return true;
            }

            // UI 자동 숨김 토글 (H)
            if (key == Keys.H)
            {
                ToggleAutoHide();
                return true;
            }

            // 7. 환경 설정 다이얼로그 (F2 또는 Ctrl+,)
            if (key == Keys.F2 || (key == Keys.Oemcomma && (Control.ModifierKeys & Keys.Control) == Keys.Control))
            {
                OpenSettingsDialog();
                return true;
            }

            // 7. 웹툰 모드 전용 키 조작 (상하 스크롤, PageUp/PageDown, Home/End, Left/Right)
            if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                int viewH = this.ClientSize.Height;
                float pageStep = Math.Max(100f, viewH * 0.85f);

                if (key == Keys.Down)
                {
                    SmoothScrollWebtoonBy(120f);
                    return true;
                }
                if (key == Keys.Up)
                {
                    SmoothScrollWebtoonBy(-120f);
                    return true;
                }
                if (key == Keys.PageDown || (key == Keys.Space && (Control.ModifierKeys & Keys.Shift) != Keys.Shift))
                {
                    SmoothScrollWebtoonBy(pageStep);
                    return true;
                }
                if (key == Keys.PageUp || (key == Keys.Space && (Control.ModifierKeys & Keys.Shift) == Keys.Shift))
                {
                    SmoothScrollWebtoonBy(-pageStep);
                    return true;
                }
                if (key == Keys.Home)
                {
                    if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
                    _webtoonScrollY = 0f;
                    _webtoonTargetScrollY = 0f;
                    UpdateWebtoonCurrentIndex();
                    this.Invalidate();
                    return true;
                }
                if (key == Keys.End)
                {
                    if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
                    _webtoonScrollY = GetWebtoonMaxScroll();
                    _webtoonTargetScrollY = _webtoonScrollY;
                    UpdateWebtoonCurrentIndex();
                    this.Invalidate();
                    return true;
                }
                if (key == Keys.Right)
                {
                    ScrollToPageIndex(_currentIndex + 1);
                    return true;
                }
                if (key == Keys.Left)
                {
                    ScrollToPageIndex(_currentIndex - 1);
                    return true;
                }
            }

            // 7. 다음 이미지 (복수 키 검사)
            if (_settings.NextKeys != null && _settings.NextKeys.Contains(key))
            {
                NextImage();
                return true;
            }

            // 8. 이전 이미지 (복수 키 검사)
            if (_settings.PrevKeys != null && _settings.PrevKeys.Contains(key))
            {
                PrevImage();
                return true;
            }

            // 9. Space 키: GIF 애니메이션일 경우 재생/정지 토글, 정지 이미지면 다음 이미지
            if (key == Keys.Space)
            {
                if (_isGif) ToggleGifPlay();
                else NextImage();
                return true;
            }

            // 10. 필터 단축키 (0, 1, 2, 3, 4, 5, 6) - Ctrl 미누름 시
            if ((Control.ModifierKeys & Keys.Control) != Keys.Control)
            {
                if (key == Keys.D0 || key == Keys.NumPad0) { SetFilter(FilterType.Off); return true; }
                if (key == Keys.D1 || key == Keys.NumPad1) { SetFilter(FilterType.Sharpen); return true; }
                if (key == Keys.D2 || key == Keys.NumPad2) { SetFilter(FilterType.Blur); return true; }
                if (key == Keys.D3 || key == Keys.NumPad3) { SetFilter(FilterType.Medium); return true; }
                if (key == Keys.D4 || key == Keys.NumPad4) { SetFilter(FilterType.UnsharpMask); return true; }
                if (key == Keys.D5 || key == Keys.NumPad5) { SetFilter(FilterType.MangaClean); return true; }
                if (key == Keys.D6 || key == Keys.NumPad6) { SetFilter(FilterType.Invert); return true; }
            }

            // 11. M: 미니맵 내비게이션 토글 (Ctrl 미누름 시)
            if (key == Keys.M && (Control.ModifierKeys & (Keys.Control | Keys.Alt)) == 0)
            {
                ToggleMinimap();
                return true;
            }

            // 12. 돋보기 확대 (+, =)
            if (key == Keys.Oemplus || key == Keys.Add)
            {
                ZoomIn();
                return true;
            }

            // 13. 돋보기 축소 (-, _)
            if (key == Keys.OemMinus || key == Keys.Subtract)
            {
                ZoomOut();
                return true;
            }

            // 14. 다음 파일 / 다음 압축파일 (]) - 엄격한 포맷 분리(압축파일은 압축파일끼리, 이미지는 이미지끼리)
            if ((key == Keys.OemCloseBrackets || (int)key == 221) && (Control.ModifierKeys & (Keys.Control | Keys.Alt)) == 0)
            {
                NextArchiveOrFile();
                return true;
            }

            // 15. 이전 파일 / 이전 압축파일 ([) - 엄격한 포맷 분리
            if ((key == Keys.OemOpenBrackets || (int)key == 219) && (Control.ModifierKeys & (Keys.Control | Keys.Alt)) == 0)
            {
                PrevArchiveOrFile(openAtEnd: false);
                return true;
            }

            // 16. Delete: 현재 열람 중인 파일 (단일 이미지, 만화 압축파일, PDF) 휴지통으로 이동
            if (key == Keys.Delete && (Control.ModifierKeys & (Keys.Control | Keys.Alt)) == 0)
            {
                DeleteCurrentItemToRecycleBin();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DeleteCurrentItemToRecycleBin()
        {
            if (GetTotalItems() <= 0) return;

            // 1. 단일 이미지 파일 열람 중일 때 (폴더 내 낱장 이미지)
            if (_singleImages != null && _singleImages.Count > 0 && _currentIndex >= 0 && _currentIndex < _singleImages.Count)
            {
                string targetPath = _singleImages[_currentIndex];
                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) return;

                string fileName = Path.GetFileName(targetPath);
                string msg = string.Format("현재 이미지 파일을 휴지통으로 보내시겠습니까?\n\n• 파일명: {0}\n• 경로: {1}", fileName, targetPath);

                if (MessageBox.Show(this, msg, "이미지 파일 삭제 (휴지통)", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    FreeCurrentBitmaps(false);

                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        targetPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                    _singleImages.RemoveAt(_currentIndex);
                    if (_settings.RecentItems != null && _settings.RecentItems.Contains(targetPath))
                    {
                        _settings.RecentItems.Remove(targetPath);
                        _settings.Save();
                    }

                    if (_singleImages.Count > 0)
                    {
                        if (_currentIndex >= _singleImages.Count)
                        {
                            _currentIndex = _singleImages.Count - 1;
                        }
                        DisplayCurrentItem();
                    }
                    else
                    {
                        _currentIndex = -1;
                        FreeCurrentBitmaps(true);
                        this.Invalidate();
                        UpdateNavBar();
                        if (_statusLabel != null)
                        {
                            _statusLabel.Text = "폴더 내 모든 이미지가 삭제되었습니다.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "파일을 휴지통으로 보내지 못했습니다:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // 2. 압축 파일(ZIP, CBZ) 열람 중일 때
            if (_zipEngine != null && _zipEngine.Count > 0)
            {
                string zipPath = _zipEngine.ZipPath;
                if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath)) return;

                string fileName = Path.GetFileName(zipPath);
                string msg = string.Format("현재 열람 중인 압축 파일 전체를 휴지통으로 보내시겠습니까?\n\n• 압축 파일명: {0}\n• 경로: {1}\n\n※ 압축 파일 내부의 낱장 삭제가 아닌, 압축 파일 원본 전체가 휴지통으로 이동합니다.", fileName, zipPath);

                if (MessageBox.Show(this, msg, "압축 파일 삭제 (휴지통)", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    string nextZip = _nextZipPath;
                    string prevZip = _prevZipPath;

                    _zipEngine.Close();
                    FreeCurrentBitmaps(true);

                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        zipPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                    if (_settings.RecentItems != null && _settings.RecentItems.Contains(zipPath))
                    {
                        _settings.RecentItems.Remove(zipPath);
                        _settings.Save();
                    }

                    if (!string.IsNullOrEmpty(nextZip) && File.Exists(nextZip))
                    {
                        LoadArchive(nextZip, 0);
                    }
                    else if (!string.IsNullOrEmpty(prevZip) && File.Exists(prevZip))
                    {
                        LoadArchive(prevZip, 0);
                    }
                    else
                    {
                        _currentIndex = -1;
                        this.Invalidate();
                        UpdateNavBar();
                        if (_statusLabel != null)
                        {
                            _statusLabel.Text = "압축 파일이 휴지통으로 이동되었습니다.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "압축 파일을 휴지통으로 보내지 못했습니다:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // 3. PDF 문서 열람 중일 때
            if (_pdfEngine != null && _pdfEngine.PageCount > 0)
            {
                string pdfPath = _pdfEngine.PdfPath;
                if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath)) return;

                string fileName = Path.GetFileName(pdfPath);
                string msg = string.Format("현재 열람 중인 PDF 문서 전체를 휴지통으로 보내시겠습니까?\n\n• PDF 파일명: {0}\n• 경로: {1}", fileName, pdfPath);

                if (MessageBox.Show(this, msg, "PDF 문서 삭제 (휴지통)", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    string nextPdf = _nextPdfPath;
                    string prevPdf = _prevPdfPath;

                    _pdfEngine.Close();
                    FreeCurrentBitmaps(true);

                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        pdfPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                    if (_settings.RecentItems != null && _settings.RecentItems.Contains(pdfPath))
                    {
                        _settings.RecentItems.Remove(pdfPath);
                        _settings.Save();
                    }

                    if (!string.IsNullOrEmpty(nextPdf) && File.Exists(nextPdf))
                    {
                        LoadPdf(nextPdf, 0);
                    }
                    else if (!string.IsNullOrEmpty(prevPdf) && File.Exists(prevPdf))
                    {
                        LoadPdf(prevPdf, 0);
                    }
                    else
                    {
                        _currentIndex = -1;
                        this.Invalidate();
                        UpdateNavBar();
                        if (_statusLabel != null)
                        {
                            _statusLabel.Text = "PDF 문서가 휴지통으로 이동되었습니다.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "PDF 문서를 휴지통으로 보내지 못했습니다:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }
        }

        private void NextImage()
        {
            int total = GetTotalItems();
            if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                if (_currentIndex >= total - 1)
                {
                    if (_zipEngine != null && _zipEngine.Count > 0 && !string.IsNullOrEmpty(_nextZipPath) && File.Exists(_nextZipPath))
                    {
                        MoveToNextArchive();
                    }
                    else if (_pdfEngine != null && _pdfEngine.PageCount > 0 && !string.IsNullOrEmpty(_nextPdfPath) && File.Exists(_nextPdfPath))
                    {
                        LoadPdf(_nextPdfPath, 0);
                    }
                    return;
                }
                ScrollToPageIndex(_currentIndex + 1);
                return;
            }

            if (_currentIndex >= total - 1)
            {
                if (_zipEngine != null && _zipEngine.Count > 0 && !string.IsNullOrEmpty(_nextZipPath) && File.Exists(_nextZipPath))
                {
                    MoveToNextArchive();
                }
                else if (_pdfEngine != null && _pdfEngine.PageCount > 0 && !string.IsNullOrEmpty(_nextPdfPath) && File.Exists(_nextPdfPath))
                {
                    LoadPdf(_nextPdfPath, 0);
                }
                return;
            }

            if (_isDualPageDisplayed)
            {
                // 현재 2장이 표시 중이었으면 2장 전진
                _currentIndex = Math.Min(total - 1, _currentIndex + 2);
            }
            else
            {
                // 단독 1장 표시(스프레드거나 1장 모드)였으면 1장 전진
                _currentIndex++;
            }
            DisplayCurrentItem();
        }

        private void PrevImage()
        {
            if (_settings.ViewMode == ViewLayout.Webtoon)
            {
                if (_currentIndex <= 0)
                {
                    if (_zipEngine != null && _zipEngine.Count > 0 && !string.IsNullOrEmpty(_prevZipPath) && File.Exists(_prevZipPath))
                    {
                        MoveToPrevArchive(true);
                    }
                    else if (_pdfEngine != null && _pdfEngine.PageCount > 0 && !string.IsNullOrEmpty(_prevPdfPath) && File.Exists(_prevPdfPath))
                    {
                        LoadPdf(_prevPdfPath, -1);
                    }
                    return;
                }
                ScrollToPageIndex(_currentIndex - 1);
                return;
            }

            if (_currentIndex <= 0)
            {
                if (_zipEngine != null && _zipEngine.Count > 0 && !string.IsNullOrEmpty(_prevZipPath) && File.Exists(_prevZipPath))
                {
                    MoveToPrevArchive();
                }
                else if (_pdfEngine != null && _pdfEngine.PageCount > 0 && !string.IsNullOrEmpty(_prevPdfPath) && File.Exists(_prevPdfPath))
                {
                    LoadPdf(_prevPdfPath, -1);
                }
                return;
            }

            if (_settings.ViewMode != ViewLayout.Single)
            {
                // 2장 모드일 때 이전 2장으로 후진
                _currentIndex = Math.Max(0, _currentIndex - 2);
            }
            else
            {
                _currentIndex--;
            }
            DisplayCurrentItem();
        }

        // 환경 설정 다이얼로그
        private void OpenSettingsDialog()
        {
            SyncWindowBoundsToSettings();
            using (SettingsForm form = new SettingsForm(_settings))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _settings = form.CurrentSettings;
                    _zipEngine.CacheMode = _settings.CacheMode;
                    _zipEngine.CacheLimitMb = _settings.CacheLimitMb;
                    _zipEngine.CacheWindow = _settings.PrecacheCount;
                    if (_pdfEngine != null)
                    {
                        _pdfEngine.CacheLimitMb = _settings.CacheLimitMb;
                    }
                    TriggerAdjacentArchivePrecache();
                    UpdateWheelStatusLabel();
                    UpdateTopButtonLabels();
                    UpdateAutoHideUi();
                    FitToScreen();
                }
            }
        }

        // 분리된 탐색기 창 토글
        private void ToggleExplorer()
        {
            if (_explorerForm == null || _explorerForm.IsDisposed)
            {
                _explorerForm = new ExplorerForm();
                _explorerForm.OnSelectArchiveItem += (zipPath, idx) =>
                {
                    LoadArchive(zipPath, idx);
                    this.Activate();
                };
                _explorerForm.OnSelectImageItem += (imgPath, idx) =>
                {
                    if (IsPdfFile(imgPath))
                        LoadPdf(imgPath, idx);
                    else
                        LoadSingleImage(imgPath);
                    this.Activate();
                };
            }

            if (_explorerForm.Visible)
            {
                _explorerForm.Hide();
            }
            else
            {
                _explorerForm.Show(this);
            }
        }

        private bool _isOpeningFileDialog = false;

        private void OpenFileDialogHandler()
        {
            if (_isOpeningFileDialog) return;
            _isOpeningFileDialog = true;
            try
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "이미지, 만화 압축파일 또는 PDF 열기";
                    dlg.Filter = "지원되는 모든 파일|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp;*.tif;*.tiff;*.jxr;*.wdp;*.hdp;*.ico;*.tga;*.qoi;*.psd;*.dds;*.ppm;*.pgm;*.pnm;*.zip;*.cbz;*.pdf|이미지 파일 (*.jpg,*.png,*.webp,*.tga,*.psd,*.dds 등)|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp;*.tif;*.tiff;*.jxr;*.wdp;*.hdp;*.ico;*.tga;*.qoi;*.psd;*.dds;*.ppm;*.pgm;*.pnm|압축 만화 파일 (*.zip, *.cbz)|*.zip;*.cbz|PDF 문서 (*.pdf)|*.pdf|모든 파일 (*.*)|*.*";
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (IsArchiveFile(dlg.FileName))
                        {
                            LoadArchive(dlg.FileName, 0);
                        }
                        else if (IsPdfFile(dlg.FileName))
                        {
                            LoadPdf(dlg.FileName, 0);
                        }
                        else
                        {
                            LoadSingleImage(dlg.FileName);
                        }
                    }
                }
            }
            finally
            {
                _isOpeningFileDialog = false;
            }
        }

        private void OpenFolderDialogHandler()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "이미지가 포함된 폴더를 선택하세요.";
                if (dlg.ShowDialog(this) == DialogResult.OK && Directory.Exists(dlg.SelectedPath))
                {
                    OpenFolder(dlg.SelectedPath);
                }
            }
        }

        public void OpenFolder(string folderPath)
        {
            try
            {
                var files = Directory.GetFiles(folderPath)
                    .Where(f => SupportedImageExts.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f, new NaturalStringComparer())
                    .ToList();

                if (files.Count > 0)
                {
                    LoadSingleImage(files[0]);
                    _settings.AddRecentItem(folderPath);
                }
                else
                {
                    var archives = Directory.GetFiles(folderPath)
                        .Where(f => SupportedArchiveExts.Contains(Path.GetExtension(f)))
                        .OrderBy(f => f, new NaturalStringComparer())
                        .ToList();
                    if (archives.Count > 0)
                    {
                        LoadArchive(archives[0], 0);
                        _settings.AddRecentItem(folderPath);
                    }
                    else
                    {
                        var pdfs = Directory.GetFiles(folderPath)
                            .Where(f => IsPdfFile(f))
                            .OrderBy(f => f, new NaturalStringComparer())
                            .ToList();
                        if (pdfs.Count > 0)
                        {
                            LoadPdf(pdfs[0], 0);
                            _settings.AddRecentItem(folderPath);
                        }
                        else
                        {
                            MessageBox.Show("선택한 폴더에 지원되는 이미지, 압축파일 또는 PDF가 없습니다.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("폴더 열기 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ApplyDarkMenuTheme(ToolStripDropDown menu)
        {
            if (menu == null) return;
            menu.Renderer = new DarkMenuRenderer();
            menu.BackColor = Color.FromArgb(24, 26, 32);
            menu.ForeColor = Color.FromArgb(240, 243, 250);
            menu.Font = new Font("Segoe UI", 9F);

            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = Color.FromArgb(240, 243, 250);
                item.BackColor = Color.FromArgb(24, 26, 32);
                item.Font = new Font("Segoe UI", 9F);

                ToolStripDropDownItem dropDownItem = item as ToolStripDropDownItem;
                if (dropDownItem != null)
                {
                    dropDownItem.DropDown.Renderer = new DarkMenuRenderer();
                    dropDownItem.DropDown.BackColor = Color.FromArgb(24, 26, 32);
                    dropDownItem.DropDown.ForeColor = Color.FromArgb(240, 243, 250);
                    dropDownItem.DropDown.Font = new Font("Segoe UI", 9F);
                    if (dropDownItem.HasDropDownItems)
                    {
                        ApplyDarkMenuTheme(dropDownItem.DropDown);
                    }
                }
            }
        }

        private void ShowContextMenu(Point pt)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.BackColor = Color.FromArgb(24, 26, 32);
            menu.ForeColor = Color.FromArgb(240, 243, 250);
            menu.Font = new Font("Segoe UI", 9F);

            // 1. 파일 & 폴더 열기
            menu.Items.Add("파일 열기... (Ctrl+O)", null, (s, e) => OpenFileDialogHandler());
            menu.Items.Add("폴더 열기... (Ctrl+Shift+O)", null, (s, e) => OpenFolderDialogHandler());

            // 2. 최근 열어본 파일/폴더 서브메뉴
            ToolStripMenuItem recentSub = new ToolStripMenuItem("최근 열어본 항목");
            recentSub.BackColor = Color.FromArgb(24, 26, 32);
            recentSub.ForeColor = Color.FromArgb(240, 243, 250);
            PopulateRecentMenu(recentSub);
            menu.Items.Add(recentSub);

            menu.Items.Add(new ToolStripSeparator());

            // 3. 외부 편집기로 열기
            string editorName = string.IsNullOrEmpty(_settings.ExternalEditorPath) ? "그림판" : Path.GetFileNameWithoutExtension(_settings.ExternalEditorPath);
            var editItem = menu.Items.Add("외부 편집기 (" + editorName + ") 로 열기 (Ctrl+E)", null, (s, e) => OpenWithExternalEditor());
            if (_rawBitmap == null) editItem.Enabled = false;

            menu.Items.Add(new ToolStripSeparator());

            // 4. 보기 및 맞춤 모드
            ToolStripMenuItem viewSub = new ToolStripMenuItem("보기 모드 (V)");
            viewSub.BackColor = Color.FromArgb(24, 26, 32);
            viewSub.ForeColor = Color.FromArgb(240, 243, 250);
            var vSingle = viewSub.DropDownItems.Add("1장 단면 보기 (Ctrl+1)", null, (s, e) => SetViewMode(ViewLayout.Single));
            var vDualRtl = viewSub.DropDownItems.Add("2장 양면 보기 - 우에서 좌 ◁ (만화/망가) (Ctrl+2)", null, (s, e) => SetViewMode(ViewLayout.DualRtl));
            var vDualLtr = viewSub.DropDownItems.Add("2장 양면 보기 - 좌에서 우 ▷ (일반 서적) (Ctrl+3)", null, (s, e) => SetViewMode(ViewLayout.DualLtr));
            var vWebtoon = viewSub.DropDownItems.Add("웹툰 보기 - 세로 연속 스크롤 (Ctrl+4)", null, (s, e) => SetViewMode(ViewLayout.Webtoon));
            if (_settings.ViewMode == ViewLayout.Single) ((ToolStripMenuItem)vSingle).Checked = true;
            else if (_settings.ViewMode == ViewLayout.DualRtl) ((ToolStripMenuItem)vDualRtl).Checked = true;
            else if (_settings.ViewMode == ViewLayout.DualLtr) ((ToolStripMenuItem)vDualLtr).Checked = true;
            else if (_settings.ViewMode == ViewLayout.Webtoon) ((ToolStripMenuItem)vWebtoon).Checked = true;

            viewSub.DropDownItems.Add(new ToolStripSeparator());
            var vSmart = new ToolStripMenuItem("펼침면(스프레드) 스마트 단독 감지 (Shift+V)");
            vSmart.Checked = _settings.SmartSpreadDetection;
            vSmart.Click += (s, e) => ToggleSmartSpreadDetection();
            viewSub.DropDownItems.Add(vSmart);

            menu.Items.Add(viewSub);

            ToolStripMenuItem fitSub = new ToolStripMenuItem("화면 맞춤 / 크기");
            fitSub.BackColor = Color.FromArgb(24, 26, 32);
            fitSub.ForeColor = Color.FromArgb(240, 243, 250);
            var fWin = fitSub.DropDownItems.Add("화면에 맞춤 (Fit to Screen) [F]", null, (s, e) => SetFitMode(DisplayFitMode.FitWindow));
            var fWidth = fitSub.DropDownItems.Add("가로 폭에 맞춤 (Fit to Width) [W]", null, (s, e) => SetFitMode(DisplayFitMode.FitWidth));
            var fHeight = fitSub.DropDownItems.Add("세로 높이에 맞춤 (Fit to Height) [Shift+F]", null, (s, e) => SetFitMode(DisplayFitMode.FitHeight));
            var fOrig = fitSub.DropDownItems.Add("원본 크기 (100% 1:1) [O]", null, (s, e) => SetFitMode(DisplayFitMode.Original));
            if (_settings.FitMode == DisplayFitMode.FitWindow) ((ToolStripMenuItem)fWin).Checked = true;
            else if (_settings.FitMode == DisplayFitMode.FitWidth) ((ToolStripMenuItem)fWidth).Checked = true;
            else if (_settings.FitMode == DisplayFitMode.FitHeight) ((ToolStripMenuItem)fHeight).Checked = true;
            else if (_settings.FitMode == DisplayFitMode.Original) ((ToolStripMenuItem)fOrig).Checked = true;

            fitSub.DropDownItems.Add(new ToolStripSeparator());
            var stretchSubItem = new ToolStripMenuItem("작은 그림 크게 보기 (확대 허용) [S]");
            stretchSubItem.Checked = _settings.StretchSmallImages;
            stretchSubItem.Click += (s, e) => ToggleStretchSmallImages();
            fitSub.DropDownItems.Add(stretchSubItem);

            fitSub.DropDownItems.Add(new ToolStripSeparator());
            fitSub.DropDownItems.Add("돋보기 확대 (+)", null, (s, e) => ZoomIn());
            fitSub.DropDownItems.Add("돋보기 축소 (-)", null, (s, e) => ZoomOut());
            menu.Items.Add(fitSub);

            ToolStripMenuItem filterSub = new ToolStripMenuItem("이미지 필터 (Filter)");
            filterSub.BackColor = Color.FromArgb(24, 26, 32);
            filterSub.ForeColor = Color.FromArgb(240, 243, 250);
            var fOff = filterSub.DropDownItems.Add("끄기 (Off) [0]", null, (s, e) => SetFilter(FilterType.Off));
            var fSharp = filterSub.DropDownItems.Add("선명하게 (Sharpen) [1]", null, (s, e) => SetFilter(FilterType.Sharpen));
            var fBlur = filterSub.DropDownItems.Add("흐리게 (Blur) [2]", null, (s, e) => SetFilter(FilterType.Blur));
            var fMed = filterSub.DropDownItems.Add("중간 부드럽게 (Medium) [3]", null, (s, e) => SetFilter(FilterType.Medium));
            var fUnsharp = filterSub.DropDownItems.Add("꿀뷰 언샤프 마스크 (Unsharp) [4]", null, (s, e) => SetFilter(FilterType.UnsharpMask));
            var fManga = filterSub.DropDownItems.Add("스캔본 만화 톤 보정 (Manga Clean) [5]", null, (s, e) => SetFilter(FilterType.MangaClean));
            var fInvert = filterSub.DropDownItems.Add("야간 반전 읽기 (Invert Dark) [6]", null, (s, e) => SetFilter(FilterType.Invert));

            if (_currentFilter == FilterType.Off) ((ToolStripMenuItem)fOff).Checked = true;
            else if (_currentFilter == FilterType.Sharpen) ((ToolStripMenuItem)fSharp).Checked = true;
            else if (_currentFilter == FilterType.Blur) ((ToolStripMenuItem)fBlur).Checked = true;
            else if (_currentFilter == FilterType.Medium) ((ToolStripMenuItem)fMed).Checked = true;
            else if (_currentFilter == FilterType.UnsharpMask) ((ToolStripMenuItem)fUnsharp).Checked = true;
            else if (_currentFilter == FilterType.MangaClean) ((ToolStripMenuItem)fManga).Checked = true;
            else if (_currentFilter == FilterType.Invert) ((ToolStripMenuItem)fInvert).Checked = true;
            menu.Items.Add(filterSub);

            ToolStripMenuItem interpSub = new ToolStripMenuItem("보간 방식 (Interpolation) [I]");
            interpSub.BackColor = Color.FromArgb(24, 26, 32);
            interpSub.ForeColor = Color.FromArgb(240, 243, 250);
            var iBicubic = interpSub.DropDownItems.Add("고품질 쌍입방 (Bicubic) - 만화/일러스트 권장", null, (s, e) => SetInterpolationMode(InterpolationModeType.Bicubic));
            var iBilinear = interpSub.DropDownItems.Add("쌍선형 (Bilinear) - 부드러움 & 고속", null, (s, e) => SetInterpolationMode(InterpolationModeType.Bilinear));
            var iNearest = interpSub.DropDownItems.Add("최근접 이웃 (Nearest Neighbor) - 픽셀아트/도트 보존", null, (s, e) => SetInterpolationMode(InterpolationModeType.NearestNeighbor));
            if (_settings.InterpolationMode == InterpolationModeType.Bicubic) ((ToolStripMenuItem)iBicubic).Checked = true;
            else if (_settings.InterpolationMode == InterpolationModeType.Bilinear) ((ToolStripMenuItem)iBilinear).Checked = true;
            else if (_settings.InterpolationMode == InterpolationModeType.NearestNeighbor) ((ToolStripMenuItem)iNearest).Checked = true;
            menu.Items.Add(interpSub);

            menu.Items.Add(new ToolStripSeparator());

            // 5. 보기 및 탐색
            menu.Items.Add("이미지 정보 & EXIF (Tab)", null, (s, e) => ToggleExifOverlay());
            menu.Items.Add("압축 파일 탐색기 (E)", null, (s, e) => ToggleExplorer());
            var rotItem = menu.Items.Add("시계방향 90° 회전 (R)", null, (s, e) => RotateCw());
            if (_rawBitmap == null) rotItem.Enabled = false;
            menu.Items.Add("마우스 휠 모드 즉시 전환 (Z)", null, (s, e) => ToggleWheelMode());

            var autoHideItem = new ToolStripMenuItem("UI 자동 숨김 (H)");
            autoHideItem.Checked = _settings.AutoHideUi;
            autoHideItem.Click += (s, e) => ToggleAutoHide();
            menu.Items.Add(autoHideItem);

            var minimapItem = new ToolStripMenuItem("미니맵 내비게이션 표시 (M)");
            minimapItem.Checked = _showMinimap;
            minimapItem.Click += (s, e) => ToggleMinimap();
            menu.Items.Add(minimapItem);

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add("다음 파일 / 압축파일 (])", null, (s, e) => NextArchiveOrFile());
            menu.Items.Add("이전 파일 / 압축파일 ([)", null, (s, e) => PrevArchiveOrFile(false));

            menu.Items.Add(new ToolStripSeparator());

            var delItem = menu.Items.Add("휴지통으로 보내기 (Delete)", null, (s, e) => DeleteCurrentItemToRecycleBin());
            if (GetTotalItems() <= 0) delItem.Enabled = false;

            menu.Items.Add(new ToolStripSeparator());

            // 6. 환경 설정
            menu.Items.Add("환경 설정 (F2)", null, (s, e) => OpenSettingsDialog());

            // 서브메뉴 전체를 포함하여 다크 테마 일관 적용
            ApplyDarkMenuTheme(menu);

            menu.Opened += (s, e) => { _contextMenuOpen = true; };
            menu.Closed += (s, e) => { _contextMenuOpen = false; };

            menu.Show(this, pt);
        }

        private void PopulateRecentMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            if (!_settings.EnableRecentList || _settings.RecentItems == null || _settings.RecentItems.Count == 0)
            {
                var empty = parent.DropDownItems.Add("(최근 열어본 항목 없음)");
                empty.Enabled = false;
                empty.ForeColor = Color.FromArgb(140, 145, 160);
                return;
            }

            foreach (string p in _settings.RecentItems)
            {
                string path = p;
                string iconPrefix = "";
                if (Directory.Exists(path)) iconPrefix = "[폴더] ";
                else if (IsArchiveFile(path)) iconPrefix = "[압축] ";
                else if (IsPdfFile(path)) iconPrefix = "[PDF] ";

                string fn = Path.GetFileName(path);
                string label = iconPrefix + (string.IsNullOrEmpty(fn) ? path : fn);

                var item = parent.DropDownItems.Add(label, null, (s, e) => OpenRecentPath(path));
                item.ToolTipText = path;
                item.ForeColor = Color.FromArgb(240, 243, 250);
                item.BackColor = Color.FromArgb(24, 26, 32);
            }

            parent.DropDownItems.Add(new ToolStripSeparator());
            var clearItem = parent.DropDownItems.Add("최근 기록 모두 지우기", null, (s, e) =>
            {
                _settings.ClearRecentItems();
            });
            clearItem.ForeColor = Color.FromArgb(255, 120, 120);
            clearItem.BackColor = Color.FromArgb(24, 26, 32);
        }

        private void OpenRecentPath(string path)
        {
            if (Directory.Exists(path))
            {
                OpenFolder(path);
            }
            else if (File.Exists(path))
            {
                if (IsArchiveFile(path))
                {
                    LoadArchive(path, 0);
                }
                else if (IsPdfFile(path))
                {
                    LoadPdf(path, 0);
                }
                else
                {
                    LoadSingleImage(path);
                }
            }
            else
            {
                if (MessageBox.Show("해당 파일 또는 폴더를 찾을 수 없습니다:\n" + path + "\n\n최근 기록에서 제거하시겠습니까?", "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _settings.RecentItems.Remove(path);
                    _settings.Save();
                }
            }
        }

        private void OpenWithExternalEditor()
        {
            if (_rawBitmap == null)
            {
                MessageBox.Show("열려 있는 이미지가 없습니다.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string targetFile = "";

                if (_singleImages.Count > 0 && _currentIndex >= 0 && _currentIndex < _singleImages.Count)
                {
                    targetFile = _singleImages[_currentIndex];
                }
                else if (_zipEngine != null && _zipEngine.Count > 0 && _currentIndex >= 0)
                {
                    string entryName = _zipEngine.GetEntryName(_currentIndex);
                    string tempDir = Path.Combine(Path.GetTempPath(), "nyaa-view");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                    string safeName = Path.GetFileName(entryName);
                    targetFile = Path.Combine(tempDir, "temp_" + safeName);

                    ImageFormat fmt = ImageFormat.Png;
                    string ext = Path.GetExtension(safeName).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg") fmt = ImageFormat.Jpeg;
                    else if (ext == ".bmp") fmt = ImageFormat.Bmp;
                    else if (ext == ".gif") fmt = ImageFormat.Gif;

                    _rawBitmap.Save(targetFile, fmt);
                }
                else if (_pdfEngine != null && _pdfEngine.PageCount > 0 && _currentIndex >= 0)
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "nyaa-view");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                    string safeName = string.Format("pdf_page_{0}.png", _currentIndex + 1);
                    targetFile = Path.Combine(tempDir, safeName);
                    _rawBitmap.Save(targetFile, ImageFormat.Png);
                }

                if (string.IsNullOrEmpty(targetFile) || !File.Exists(targetFile))
                {
                    MessageBox.Show("편집할 이미지 파일을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string editor = _settings.ExternalEditorPath;
                if (string.IsNullOrEmpty(editor) || !File.Exists(editor))
                {
                    System.Diagnostics.Process.Start("mspaint.exe", "\"" + targetFile + "\"");
                }
                else
                {
                    System.Diagnostics.Process.Start(editor, "\"" + targetFile + "\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("외부 편집기 실행 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                string first = files[0];
                if (Directory.Exists(first))
                {
                    OpenFolder(first);
                }
                else
                {
                    if (IsArchiveFile(first))
                    {
                        LoadArchive(first, 0);
                    }
                    else if (IsPdfFile(first))
                    {
                        LoadPdf(first, 0);
                    }
                    else
                    {
                        LoadSingleImage(first);
                    }
                }
            }
        }

        // ==========================================
        // 웹툰 모드 (세로 무한 연속 스크롤) 전용 메서드
        // ==========================================

        private float GetEstimatedAspectRatio()
        {
            lock (_webtoonLock)
            {
                if (_webtoonPageSizes.Count > 0)
                {
                    float sum = 0f;
                    int count = 0;
                    foreach (var sz in _webtoonPageSizes.Values)
                    {
                        if (sz.Width > 0 && sz.Height > 0)
                        {
                            sum += ((float)sz.Height / sz.Width);
                            count++;
                        }
                    }
                    if (count > 0) return sum / count;
                }
            }
            if (_rawBitmap != null && _rawBitmap.Width > 0)
            {
                return (float)_rawBitmap.Height / _rawBitmap.Width;
            }
            return 1.4f; // 일반적인 웹툰 컷 표준 종횡비
        }

        private float GetPageHeight(int index, float colWidth)
        {
            Size sz;
            lock (_webtoonLock)
            {
                if (_webtoonPageSizes.TryGetValue(index, out sz) && sz.Width > 0 && sz.Height > 0)
                {
                    return colWidth * ((float)sz.Height / sz.Width);
                }
            }
            return colWidth * GetEstimatedAspectRatio();
        }

        private float CalculateScrollYForIndex(int targetIndex)
        {
            int total = GetTotalItems();
            if (targetIndex <= 0 || total <= 0) return 0f;
            if (targetIndex >= total) targetIndex = total - 1;

            int viewW = this.ClientSize.Width;
            float colWidth = Math.Max(100f, viewW * _webtoonZoom);
            float y = 0f;
            for (int i = 0; i < targetIndex; i++)
            {
                y += GetPageHeight(i, colWidth);
            }
            return y;
        }

        private float GetWebtoonMaxScroll()
        {
            int total = GetTotalItems();
            if (total <= 0) return 0f;

            int viewW = this.ClientSize.Width;
            int viewH = this.ClientSize.Height;
            int topH = (!_settings.AutoHideUi && _topBar != null && _topBar.Visible) ? _topBar.Height : 0;
            int navH = (!_settings.AutoHideUi && _navBar != null && _navBar.Visible) ? _navBar.Height : 0;
            int botH = (!_settings.AutoHideUi) ? ((_gifBar != null && _gifBar.Visible ? _gifBar.Height : 0) + navH + (_statusStrip != null && _statusStrip.Visible ? _statusStrip.Height : 0)) : 0;
            int availH = Math.Max(10, viewH - topH - botH);

            float colWidth = Math.Max(100f, viewW * _webtoonZoom);
            float totalH = 0f;
            for (int i = 0; i < total; i++)
            {
                totalH += GetPageHeight(i, colWidth);
            }
            return Math.Max(0f, totalH - availH);
        }

        private void ClampWebtoonScroll()
        {
            float maxScroll = GetWebtoonMaxScroll();
            if (_webtoonScrollY > maxScroll) _webtoonScrollY = maxScroll;
            if (_webtoonScrollY < 0f) _webtoonScrollY = 0f;
            if (_webtoonTargetScrollY > maxScroll) _webtoonTargetScrollY = maxScroll;
            if (_webtoonTargetScrollY < 0f) _webtoonTargetScrollY = 0f;

            if (_webtoonZoom <= 1.0f)
            {
                _webtoonPanX = 0f;
            }
            else
            {
                int viewW = this.ClientSize.Width;
                float colWidth = viewW * _webtoonZoom;
                float maxPanX = (colWidth - viewW) / 2f;
                if (_webtoonPanX > maxPanX) _webtoonPanX = maxPanX;
                if (_webtoonPanX < -maxPanX) _webtoonPanX = -maxPanX;
            }
        }

        private void SmoothScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_settings.ViewMode != ViewLayout.Webtoon)
            {
                if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
                return;
            }

            float diff = _webtoonTargetScrollY - _webtoonScrollY;
            if (Math.Abs(diff) <= 0.5f)
            {
                _webtoonScrollY = _webtoonTargetScrollY;
                if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
            }
            else
            {
                // 지수 감속 보간 (Ease-out Lerp): 목표 지점에 접근할수록 부드럽게 감속
                float step = diff * 0.26f;
                if (Math.Abs(step) < 1.0f)
                {
                    step = Math.Sign(diff) * Math.Min(Math.Abs(diff), 1.0f);
                }
                _webtoonScrollY += step;
            }

            ClampWebtoonScroll();
            UpdateWebtoonCurrentIndex();
            this.Invalidate();
        }

        private void SmoothScrollWebtoonBy(float deltaY)
        {
            float maxScroll = GetWebtoonMaxScroll();

            // 부드러운 스크롤 비활성화 설정 시 즉각 스크롤
            if (!_settings.WebtoonSmoothScroll)
            {
                ScrollWebtoon(deltaY);
                return;
            }

            // 상단/하단 끝 도달 시 연속 휠 회전에 의한 인접 압축파일 전환
            if (deltaY > 0 && _webtoonTargetScrollY >= maxScroll - 2f)
            {
                _webtoonBoundaryCounter++;
                if (_webtoonBoundaryCounter >= 3)
                {
                    _webtoonBoundaryCounter = 0;
                    if (!string.IsNullOrEmpty(_nextZipPath) && File.Exists(_nextZipPath))
                    {
                        NextArchiveOrFile();
                    }
                    else if (!string.IsNullOrEmpty(_nextPdfPath) && File.Exists(_nextPdfPath))
                    {
                        LoadPdf(_nextPdfPath, 0);
                    }
                }
                return;
            }
            else if (deltaY < 0 && _webtoonTargetScrollY <= 2f)
            {
                _webtoonBoundaryCounter++;
                if (_webtoonBoundaryCounter >= 3)
                {
                    _webtoonBoundaryCounter = 0;
                    if (!string.IsNullOrEmpty(_prevZipPath) && File.Exists(_prevZipPath))
                    {
                        MoveToPrevArchive(true);
                    }
                    else if (!string.IsNullOrEmpty(_prevPdfPath) && File.Exists(_prevPdfPath))
                    {
                        LoadPdf(_prevPdfPath, -1);
                    }
                }
                return;
            }
            else
            {
                _webtoonBoundaryCounter = 0;
            }

            // 목표 스크롤 지점 누적 및 최대 누적 거리 제한 (빠른 연속 휠 회전 시 과도한 오버슈트 방지)
            _webtoonTargetScrollY += deltaY;
            if (_webtoonTargetScrollY < 0f) _webtoonTargetScrollY = 0f;
            if (_webtoonTargetScrollY > maxScroll) _webtoonTargetScrollY = maxScroll;

            float maxLead = Math.Max(this.ClientSize.Height * 2.5f, 2500f);
            if (_webtoonTargetScrollY > _webtoonScrollY + maxLead)
            {
                _webtoonTargetScrollY = _webtoonScrollY + maxLead;
            }
            else if (_webtoonTargetScrollY < _webtoonScrollY - maxLead)
            {
                _webtoonTargetScrollY = _webtoonScrollY - maxLead;
            }

            if (_smoothScrollTimer != null && !_smoothScrollTimer.Enabled)
            {
                _smoothScrollTimer.Start();
            }
        }

        private void ScrollWebtoon(float deltaY)
        {
            if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
            float prev = _webtoonScrollY;
            _webtoonScrollY += deltaY;
            ClampWebtoonScroll();
            _webtoonTargetScrollY = _webtoonScrollY;
            UpdateWebtoonCurrentIndex();
            this.Invalidate();

            // 상단/하단 끝 도달 시 연속 휠 회전에 의한 인접 압축파일 전환
            float maxScroll = GetWebtoonMaxScroll();
            if (deltaY > 0 && prev >= maxScroll - 2f)
            {
                _webtoonBoundaryCounter++;
                if (_webtoonBoundaryCounter >= 3)
                {
                    _webtoonBoundaryCounter = 0;
                    if (!string.IsNullOrEmpty(_nextZipPath) && File.Exists(_nextZipPath))
                    {
                        NextArchiveOrFile();
                    }
                    else if (!string.IsNullOrEmpty(_nextPdfPath) && File.Exists(_nextPdfPath))
                    {
                        LoadPdf(_nextPdfPath, 0);
                    }
                }
            }
            else if (deltaY < 0 && prev <= 2f)
            {
                _webtoonBoundaryCounter++;
                if (_webtoonBoundaryCounter >= 3)
                {
                    _webtoonBoundaryCounter = 0;
                    if (!string.IsNullOrEmpty(_prevZipPath) && File.Exists(_prevZipPath))
                    {
                        MoveToPrevArchive(true);
                    }
                    else if (!string.IsNullOrEmpty(_prevPdfPath) && File.Exists(_prevPdfPath))
                    {
                        LoadPdf(_prevPdfPath, -1);
                    }
                }
            }
            else
            {
                _webtoonBoundaryCounter = 0;
            }
        }

        private void ScrollToPageIndex(int targetIdx)
        {
            int total = GetTotalItems();
            if (total <= 0) return;
            targetIdx = Math.Max(0, Math.Min(total - 1, targetIdx));
            if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
            _webtoonScrollY = CalculateScrollYForIndex(targetIdx);
            _webtoonTargetScrollY = _webtoonScrollY;
            ClampWebtoonScroll();
            _currentIndex = targetIdx;
            UpdateStatusLabelText();
            UpdateNavBar();
            this.Invalidate();
        }

        private void UpdateWebtoonCurrentIndex()
        {
            int total = GetTotalItems();
            if (total <= 0) return;

            int viewW = this.ClientSize.Width;
            int topH = (!_settings.AutoHideUi && _topBar != null && _topBar.Visible) ? _topBar.Height : 0;
            int navH = (!_settings.AutoHideUi && _navBar != null && _navBar.Visible) ? _navBar.Height : 0;
            int botH = (!_settings.AutoHideUi) ? ((_gifBar != null && _gifBar.Visible ? _gifBar.Height : 0) + navH + (_statusStrip != null && _statusStrip.Visible ? _statusStrip.Height : 0)) : 0;
            int availH = Math.Max(10, this.ClientSize.Height - topH - botH);

            float colWidth = Math.Max(100f, viewW * _webtoonZoom);
            float targetY = _webtoonScrollY + (availH * 0.35f);

            float accumY = 0f;
            int foundIndex = 0;
            for (int i = 0; i < total; i++)
            {
                float h = GetPageHeight(i, colWidth);
                if (targetY >= accumY && targetY < accumY + h)
                {
                    foundIndex = i;
                    break;
                }
                accumY += h;
                if (i == total - 1) foundIndex = total - 1;
            }

            if (foundIndex != _currentIndex)
            {
                _currentIndex = foundIndex;
                UpdateStatusLabelText();
                UpdateNavBar();
            }
        }

        private void UpdateZoomLabelWebtoon()
        {
            if (_zoomLabel != null)
            {
                _zoomLabel.Text = string.Format("{0}%", (int)(_webtoonZoom * 100));
            }
        }

        private void DrawWebtoon(Graphics g)
        {
            int total = GetTotalItems();
            if (total <= 0) return;

            int viewW = this.ClientSize.Width;
            int viewH = this.ClientSize.Height;
            int topH = (!_settings.AutoHideUi && _topBar != null && _topBar.Visible) ? _topBar.Height : 0;
            int navH = (!_settings.AutoHideUi && _navBar != null && _navBar.Visible) ? _navBar.Height : 0;
            int botH = (!_settings.AutoHideUi) ? ((_gifBar != null && _gifBar.Visible ? _gifBar.Height : 0) + navH + (_statusStrip != null && _statusStrip.Visible ? _statusStrip.Height : 0)) : 0;
            int availH = Math.Max(10, viewH - topH - botH);

            float colWidth = Math.Max(100f, viewW * _webtoonZoom);
            float colX = (viewW - colWidth) / 2f + _webtoonPanX;

            float[] pageY = new float[total + 1];
            pageY[0] = 0f;
            for (int i = 0; i < total; i++)
            {
                pageY[i + 1] = pageY[i] + GetPageHeight(i, colWidth);
            }
            float totalH = pageY[total];
            float maxScroll = Math.Max(0f, totalH - availH);
            if (_webtoonScrollY > maxScroll) _webtoonScrollY = maxScroll;
            if (_webtoonScrollY < 0f) _webtoonScrollY = 0f;

            switch (_settings.InterpolationMode)
            {
                case InterpolationModeType.NearestNeighbor:
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    break;
                case InterpolationModeType.Bilinear:
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    break;
                case InterpolationModeType.Bicubic:
                default:
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    break;
            }

            Rectangle clipRect = new Rectangle(0, topH, viewW, availH);
            g.SetClip(clipRect);

            int firstVisible = -1;
            int lastVisible = -1;

            for (int i = 0; i < total; i++)
            {
                float pageTop = pageY[i] - _webtoonScrollY;
                float pageBottom = pageY[i + 1] - _webtoonScrollY;

                if (pageBottom <= 0f) continue;
                if (pageTop >= availH) break;

                if (firstVisible == -1) firstVisible = i;
                lastVisible = i;

                int drawY = topH + (int)Math.Round(pageTop);
                int nextDrawY = topH + (int)Math.Round(pageBottom);
                int drawH = nextDrawY - drawY;
                int drawX = (int)Math.Round(colX);
                int drawW = (int)Math.Round(colWidth);

                Bitmap bmp = null;
                lock (_webtoonLock)
                {
                    if (_webtoonCache.ContainsKey(i))
                    {
                        bmp = _webtoonCache[i];
                    }
                }

                if (bmp != null)
                {
                    g.DrawImage(bmp, new Rectangle(drawX, drawY, drawW, drawH));
                }
                else
                {
                    using (Brush phBg = new SolidBrush(Color.FromArgb(24, 26, 32)))
                    {
                        g.FillRectangle(phBg, drawX, drawY, drawW, drawH);
                    }
                    using (Pen phBorder = new Pen(Color.FromArgb(40, 44, 55), 1))
                    {
                        g.DrawRectangle(phBorder, drawX, drawY, drawW, drawH);
                    }
                    using (Font phFont = new Font("Segoe UI", 11F, FontStyle.Regular))
                    using (Brush phTextBrush = new SolidBrush(Color.FromArgb(130, 135, 150)))
                    using (StringFormat phSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        string loadText = string.Format("P. {0} 로딩 중...", i + 1);
                        g.DrawString(loadText, phFont, phTextBrush, new RectangleF(drawX, drawY, drawW, drawH), phSf);
                    }
                }
            }

            g.ResetClip();

            if (firstVisible != -1 && lastVisible != -1)
            {
                TriggerWebtoonPrecache(firstVisible, lastVisible, total);
            }

            if (totalH > availH)
            {
                DrawWebtoonScrollbar(g, topH, availH, viewW, _webtoonScrollY, totalH);
            }
        }

        private void DrawWebtoonScrollbar(Graphics g, int topH, int availH, int viewW, float scrollY, float totalH)
        {
            int trackW = 6;
            int trackX = viewW - trackW - 2;
            int trackY = topH + 4;
            int trackH = availH - 8;

            if (trackH < 20) return;

            using (Brush trackBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            {
                g.FillRectangle(trackBrush, trackX, trackY, trackW, trackH);
            }

            float thumbRatio = (float)availH / totalH;
            int thumbH = Math.Max(24, (int)(trackH * thumbRatio));
            float maxScroll = totalH - availH;
            float scrollRatio = (maxScroll > 0) ? (scrollY / maxScroll) : 0f;
            int thumbY = trackY + (int)((trackH - thumbH) * scrollRatio);

            using (Brush thumbBrush = new SolidBrush(Color.FromArgb(140, 140, 150, 170)))
            {
                g.FillRectangle(thumbBrush, trackX, thumbY, trackW, thumbH);
            }
        }

        private void TriggerWebtoonPrecache(int firstVisible, int lastVisible, int total)
        {
            int reqStart = Math.Max(0, firstVisible - 2);
            int reqEnd = Math.Min(total - 1, lastVisible + 8);

            bool allCached = true;
            lock (_webtoonLock)
            {
                for (int i = reqStart; i <= reqEnd; i++)
                {
                    if (!_webtoonCache.ContainsKey(i))
                    {
                        allCached = false;
                        break;
                    }
                }
            }

            if (allCached) return;

            int gen = Interlocked.Increment(ref _webtoonPrecacheGen);

            ThreadPool.QueueUserWorkItem(state =>
            {
                if (gen != _webtoonPrecacheGen) return;

                List<int> priorityList = new List<int>();
                for (int i = firstVisible; i <= lastVisible; i++) priorityList.Add(i);
                for (int i = lastVisible + 1; i <= reqEnd; i++) priorityList.Add(i);
                for (int i = firstVisible - 1; i >= reqStart; i--) priorityList.Add(i);

                bool anyLoaded = false;
                foreach (int idx in priorityList)
                {
                    if (gen != _webtoonPrecacheGen) return;

                    bool isCached;
                    lock (_webtoonLock)
                    {
                        isCached = _webtoonCache.ContainsKey(idx);
                    }
                    if (isCached) continue;

                    string name;
                    Bitmap raw = GetBitmapAtIndex(idx, out name);
                    if (raw != null)
                    {
                        Bitmap filtered = (_currentFilter != FilterType.Off) ? FilterEngine.ApplyFilter(raw, _currentFilter) : raw;
                        lock (_webtoonLock)
                        {
                            _webtoonCache[idx] = filtered;
                            _webtoonPageSizes[idx] = new Size(filtered.Width, filtered.Height);
                        }
                        anyLoaded = true;

                        PruneWebtoonCache(firstVisible, lastVisible);
                    }
                }

                if (anyLoaded && gen == _webtoonPrecacheGen)
                {
                    if (!this.IsDisposed && this.IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            if (_settings.ViewMode == ViewLayout.Webtoon)
                            {
                                this.Invalidate();
                            }
                        }));
                    }
                }
            });
        }

        private void PruneWebtoonCache(int firstVisible, int lastVisible)
        {
            lock (_webtoonLock)
            {
                int minKeep = Math.Max(0, firstVisible - 15);
                int maxKeep = lastVisible + 25;

                List<int> toRemove = new List<int>();
                foreach (int k in _webtoonCache.Keys)
                {
                    if (k < minKeep || k > maxKeep)
                    {
                        toRemove.Add(k);
                    }
                }

                foreach (int k in toRemove)
                {
                    Bitmap bmp = _webtoonCache[k];
                    _webtoonCache.Remove(k);
                    if (bmp != null)
                    {
                        if (_singleImages != null && _singleImages.Count > 0)
                        {
                            try { bmp.Dispose(); } catch { }
                        }
                        else if (_currentFilter != FilterType.Off)
                        {
                            try { bmp.Dispose(); } catch { }
                        }
                    }
                }
            }
        }

        private void FreeWebtoonCache()
        {
            lock (_webtoonLock)
            {
                Interlocked.Increment(ref _webtoonPrecacheGen);
                foreach (var bmp in _webtoonCache.Values)
                {
                    if (bmp != null)
                    {
                        if (_singleImages != null && _singleImages.Count > 0)
                        {
                            try { bmp.Dispose(); } catch { }
                        }
                        else if (_currentFilter != FilterType.Off)
                        {
                            try { bmp.Dispose(); } catch { }
                        }
                    }
                }
                _webtoonCache.Clear();
                _webtoonPageSizes.Clear();
            }
        }

        private void ResetWebtoonState()
        {
            if (_smoothScrollTimer != null) _smoothScrollTimer.Stop();
            FreeWebtoonCache();
            _webtoonScrollY = 0f;
            _webtoonTargetScrollY = 0f;
            _webtoonZoom = 1.0f;
            _webtoonPanX = 0f;
            _webtoonBoundaryCounter = 0;
            UpdateZoomLabelWebtoon();
        }

        private void FreeCurrentBitmaps(bool disposingAll = false)
        {
            if (_filteredBitmap != null && _filteredBitmap != _rawBitmap)
            {
                _filteredBitmap.Dispose();
                _filteredBitmap = null;
            }
            if (_filteredBitmap2 != null && _filteredBitmap2 != _rawBitmap2)
            {
                _filteredBitmap2.Dispose();
                _filteredBitmap2 = null;
            }

            // ZipEngine 및 PdfEngine 캐시가 아닌 단일 파일 로드된 이미지인 경우 _rawBitmap 직접 해제 (웹툰 모드 제외)
            bool isManaged = (_zipEngine != null && _zipEngine.Count > 0) || (_pdfEngine != null && _pdfEngine.PageCount > 0);
            if ((!isManaged && _settings.ViewMode != ViewLayout.Webtoon) || disposingAll)
            {
                if (_rawBitmap != null)
                {
                    IDisposable stream = _rawBitmap.Tag as IDisposable;
                    if (stream != null)
                    {
                        try { stream.Dispose(); } catch { }
                    }
                    _rawBitmap.Dispose();
                    _rawBitmap = null;
                }
                if (_rawBitmap2 != null)
                {
                    IDisposable stream2 = _rawBitmap2.Tag as IDisposable;
                    if (stream2 != null)
                    {
                        try { stream2.Dispose(); } catch { }
                    }
                    _rawBitmap2.Dispose();
                    _rawBitmap2 = null;
                }
            }
            else
            {
                _rawBitmap = null; // 엔진이 캐시 수명 관리
                _rawBitmap2 = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_hoverTimer != null)
                {
                    _hoverTimer.Stop();
                    _hoverTimer.Dispose();
                    _hoverTimer = null;
                }
                if (_smoothScrollTimer != null)
                {
                    _smoothScrollTimer.Stop();
                    _smoothScrollTimer.Dispose();
                    _smoothScrollTimer = null;
                }
                if (_gifTimer != null)
                {
                    _gifTimer.Stop();
                    _gifTimer.Dispose();
                    _gifTimer = null;
                }
                if (_zipEngine != null)
                {
                    _zipEngine.Dispose();
                    _zipEngine = null;
                }
                if (_pdfEngine != null)
                {
                    _pdfEngine.Dispose();
                    _pdfEngine = null;
                }
                if (_explorerForm != null && !_explorerForm.IsDisposed)
                {
                    _explorerForm.Dispose();
                    _explorerForm = null;
                }
                if (_filterContext != null)
                {
                    _filterContext.Dispose();
                    _filterContext = null;
                }
                if (_viewModeContext != null)
                {
                    _viewModeContext.Dispose();
                    _viewModeContext = null;
                }
                if (_fitModeContext != null)
                {
                    _fitModeContext.Dispose();
                    _fitModeContext = null;
                }
                if (_borderHitFilters != null)
                {
                    foreach (var filter in _borderHitFilters)
                    {
                        try { filter.ReleaseHandle(); } catch { }
                    }
                    _borderHitFilters.Clear();
                }
                FreeWebtoonCache();
                ClearAdjacentEngines();
                FreeCurrentBitmaps(true);
            }
            base.Dispose(disposing);
        }
    }
 
     public class DarkMenuRenderer : ToolStripProfessionalRenderer
     {
         public DarkMenuRenderer() : base(new DarkMenuColorTable()) { }
 
         protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
         {
             // 메뉴 항목 글씨 가독성: 어두운 배경 위에서 언제나 또렷하고 선명하게 렌더링
             if (!e.Item.Enabled)
             {
                 e.TextColor = Color.FromArgb(135, 140, 155); // 비활성화 항목도 어두운 배경에서 식별 가능한 은회색
             }
             else if (e.Item.Selected)
             {
                 e.TextColor = Color.FromArgb(255, 255, 255); // 마우스 호버/선택 시 선명한 순백색
             }
             else
             {
                 e.TextColor = Color.FromArgb(240, 243, 250); // 기본 텍스트: 매우 밝은 오프화이트
             }
             base.OnRenderItemText(e);
         }
 
         protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
         {
             // 서브메뉴 화살표 (▶) 가독성 확보
             e.ArrowColor = e.Item.Enabled ? Color.FromArgb(215, 220, 235) : Color.FromArgb(135, 140, 155);
             base.OnRenderArrow(e);
         }
 
         protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
         {
             // 체크 표시 영역 (✓) 다크 테마 블루 악센트 렌더링
             Rectangle rect = new Rectangle(e.ImageRectangle.Left - 1, e.ImageRectangle.Top + 1, e.ImageRectangle.Width + 2, e.ImageRectangle.Height - 2);
             using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 75, 145)))
             {
                 e.Graphics.FillRectangle(b, rect);
             }
             using (Pen p = new Pen(Color.FromArgb(80, 130, 230), 1f))
             {
                 e.Graphics.DrawRectangle(p, rect);
             }
 
             int cx = rect.Left + rect.Width / 2;
             int cy = rect.Top + rect.Height / 2;
             Point[] checkPoints = new Point[]
             {
                 new Point(cx - 4, cy),
                 new Point(cx - 1, cy + 3),
                 new Point(cx + 5, cy - 3)
             };
             using (Pen checkPen = new Pen(Color.White, 2f))
             {
                 e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                 e.Graphics.DrawLines(checkPen, checkPoints);
             }
         }
 
         protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
         {
             int y = e.Item.Height / 2;
             using (Pen p = new Pen(Color.FromArgb(50, 54, 68)))
             {
                 e.Graphics.DrawLine(p, 8, y, e.Item.Width - 8, y);
             }
         }
     }
 
     public class DarkMenuColorTable : ProfessionalColorTable
     {
         public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 26, 32); } }
         public override Color ImageMarginGradientBegin { get { return Color.FromArgb(28, 30, 38); } }
         public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(28, 30, 38); } }
         public override Color ImageMarginGradientEnd { get { return Color.FromArgb(28, 30, 38); } }
         public override Color MenuBorder { get { return Color.FromArgb(58, 63, 78); } }
         public override Color MenuItemBorder { get { return Color.FromArgb(70, 110, 200); } }
         public override Color MenuItemSelected { get { return Color.FromArgb(42, 54, 80); } }
         public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(42, 54, 80); } }
         public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(42, 54, 80); } }
         public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(34, 44, 66); } }
         public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(34, 44, 66); } }
         public override Color CheckBackground { get { return Color.FromArgb(40, 75, 145); } }
         public override Color CheckSelectedBackground { get { return Color.FromArgb(50, 90, 170); } }
         public override Color CheckPressedBackground { get { return Color.FromArgb(60, 105, 195); } }
         public override Color SeparatorDark { get { return Color.FromArgb(50, 54, 68); } }
         public override Color SeparatorLight { get { return Color.FromArgb(24, 26, 32); } }
     }
}
