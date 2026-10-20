using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NyaaView
{
    public class ExplorerForm : Form
    {
        private FlowLayoutPanel _flowPanel;
        private TextBox _searchBox;
        private NumericUpDown _thumbCountNum;
        private ComboBox _sortCombo;
        private Label _folderLabel;
        private Button _openFolderBtn;

        private string _currentFolder = string.Empty;
        private List<string> _allFiles = new List<string>();
        private BackgroundWorker _loaderWorker;

        public event Action<string, int> OnSelectArchiveItem; // (zipPath, startIndex)
        public event Action<string, int> OnSelectImageItem;   // (imagePath, startIndex)

        public ExplorerForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "nyaa-view - 압축파일 썸네일 탐색기";
            this.Size = new Size(1100, 750);
            this.MinimumSize = new Size(800, 500);
            this.BackColor = Color.FromArgb(24, 26, 32);
            this.ForeColor = Color.FromArgb(240, 240, 245);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // 상단 툴바 패널
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(32, 35, 43),
                Padding = new Padding(12, 10, 12, 10)
            };

            _openFolderBtn = new Button
            {
                Text = "📂 폴더 선택",
                Width = 100,
                Height = 32,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(58, 122, 254),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _openFolderBtn.FlatAppearance.BorderSize = 0;
            _openFolderBtn.Click += (s, e) => OpenFolderDialog();

            _folderLabel = new Label
            {
                Text = "폴더가 선택되지 않았습니다.",
                AutoSize = false,
                Width = 350,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Left,
                ForeColor = Color.FromArgb(170, 175, 190),
                Padding = new Padding(10, 0, 0, 0)
            };

            // 우측 컨트롤 (정렬, 미리보기 장수, 검색)
            _searchBox = new TextBox
            {
                Width = 140,
                Height = 28,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _searchBox.TextChanged += (s, e) => ApplyFilter();

            Label searchLbl = new Label
            {
                Text = "🔍 검색:",
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(8, 6, 4, 0)
            };

            _sortCombo = new ComboBox
            {
                Width = 120,
                Height = 28,
                Dock = DockStyle.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _sortCombo.Items.AddRange(new object[] { "이름순 (가나다)", "이름순 (역순)", "크기 큰 순", "최신 수정순" });
            _sortCombo.SelectedIndex = 0;
            _sortCombo.SelectedIndexChanged += (s, e) => ApplyFilter();

            Label sortLbl = new Label
            {
                Text = "정렬:",
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(8, 6, 4, 0)
            };

            _thumbCountNum = new NumericUpDown
            {
                Width = 50,
                Height = 28,
                Dock = DockStyle.Right,
                Minimum = 1,
                Maximum = 10,
                Value = 5,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.White
            };
            _thumbCountNum.ValueChanged += (s, e) => RefreshCards();

            Label thumbLbl = new Label
            {
                Text = "미리보기 장수:",
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(8, 6, 4, 0)
            };

            topPanel.Controls.Add(_folderLabel);
            topPanel.Controls.Add(_openFolderBtn);
            topPanel.Controls.Add(_searchBox);
            topPanel.Controls.Add(searchLbl);
            topPanel.Controls.Add(_sortCombo);
            topPanel.Controls.Add(sortLbl);
            topPanel.Controls.Add(_thumbCountNum);
            topPanel.Controls.Add(thumbLbl);

            // 메인 플로우 패널
            _flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(18, 19, 22),
                Padding = new Padding(16)
            };

            this.Controls.Add(_flowPanel);
            this.Controls.Add(topPanel);

            _loaderWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            _loaderWorker.DoWork += LoaderWorker_DoWork;
            _loaderWorker.ProgressChanged += LoaderWorker_ProgressChanged;
            _loaderWorker.RunWorkerCompleted += LoaderWorker_RunWorkerCompleted;
        }

        private List<Control> _pendingWorkerArgs = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_loaderWorker != null)
                {
                    if (_loaderWorker.IsBusy) _loaderWorker.CancelAsync();
                    _loaderWorker.Dispose();
                    _loaderWorker = null;
                }
                if (_flowPanel != null)
                {
                    foreach (Control c in _flowPanel.Controls) c.Dispose();
                    _flowPanel.Controls.Clear();
                }
            }
            base.Dispose(disposing);
        }

        public void OpenFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;
            _currentFolder = folderPath;
            _folderLabel.Text = Path.GetFileName(folderPath) + " (" + folderPath + ")";

            var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp",
                ".tif", ".tiff", ".jxr", ".wdp", ".hdp", ".ico",
                ".tga", ".qoi", ".psd", ".dds",
                ".ppm", ".pgm", ".pnm"
            };
            var zipExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".zip", ".cbz" };
            var pdfExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf" };

            _allFiles = Directory.GetFiles(folderPath)
                .Where(f => zipExts.Contains(Path.GetExtension(f)) || imageExts.Contains(Path.GetExtension(f)) || pdfExts.Contains(Path.GetExtension(f)))
                .ToList();

            ApplyFilter();
        }

        private void OpenFolderDialog()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "이미지 및 압축파일이 있는 폴더를 선택하세요.";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    OpenFolder(dlg.SelectedPath);
                }
            }
        }

        private void ApplyFilter()
        {
            if (_allFiles == null || _allFiles.Count == 0) return;

            string q = _searchBox.Text.Trim().ToLower();
            var filtered = _allFiles.Where(f => string.IsNullOrEmpty(q) || Path.GetFileName(f).ToLower().Contains(q)).ToList();

            int sortIdx = _sortCombo.SelectedIndex;
            if (sortIdx == 0)
                filtered.Sort(new NaturalStringComparer());
            else if (sortIdx == 1)
            {
                filtered.Sort(new NaturalStringComparer());
                filtered.Reverse();
            }
            else if (sortIdx == 2)
                filtered = filtered.OrderByDescending(f => new FileInfo(f).Length).ToList();
            else if (sortIdx == 3)
                filtered = filtered.OrderByDescending(f => new FileInfo(f).LastWriteTime).ToList();

            DisplayFiles(filtered);
        }

        private void RefreshCards()
        {
            ApplyFilter();
        }

        private void DisplayFiles(List<string> files)
        {
            _flowPanel.SuspendLayout();
            foreach (Control oldCtrl in _flowPanel.Controls)
            {
                oldCtrl.Dispose();
            }
            _flowPanel.Controls.Clear();

            foreach (var file in files)
            {
                string ext = Path.GetExtension(file);
                bool isZip = ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase);
                bool isPdf = ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
                CardControl card = new CardControl(file, isZip, isPdf, (int)_thumbCountNum.Value);
                card.OnOpenRequested += (path, idx) =>
                {
                    if (isZip)
                    {
                        if (OnSelectArchiveItem != null) OnSelectArchiveItem(path, idx);
                    }
                    else
                    {
                        if (OnSelectImageItem != null) OnSelectImageItem(path, idx);
                    }
                };
                _flowPanel.Controls.Add(card);
            }

            _flowPanel.ResumeLayout();

            // 백그라운드에서 썸네일 비동기 로딩 (Busy 상태면 pending에 저장 후 이전 작업 취소)
            var controlsToLoad = new List<Control>(_flowPanel.Controls.Cast<Control>());
            if (_loaderWorker.IsBusy)
            {
                _pendingWorkerArgs = controlsToLoad;
                _loaderWorker.CancelAsync();
            }
            else
            {
                _loaderWorker.RunWorkerAsync(controlsToLoad);
            }
        }

        private void LoaderWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            List<Control> controls = (List<Control>)e.Argument;
            foreach (var ctrl in controls)
            {
                if (_loaderWorker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                CardControl card = ctrl as CardControl;
                if (card != null)
                {
                    card.LoadThumbnailData();
                    _loaderWorker.ReportProgress(0, card);
                }
            }
        }

        private void LoaderWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CardControl card = e.UserState as CardControl;
            if (card != null && !card.IsDisposed)
            {
                card.UpdateUI();
            }
        }

        private void LoaderWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_pendingWorkerArgs != null)
            {
                var next = _pendingWorkerArgs;
                _pendingWorkerArgs = null;
                if (!this.IsDisposed && _loaderWorker != null && !_loaderWorker.IsBusy)
                {
                    _loaderWorker.RunWorkerAsync(next);
                }
            }
        }
    }

    // 압축파일 / 이미지 / PDF 개별 카드 컴포넌트
    public class CardControl : Panel
    {
        public string FilePath { get; private set; }
        public bool IsZip { get; private set; }
        public bool IsPdf { get; private set; }
        public int MaxThumbs { get; private set; }

        private PictureBox _mainPic;
        private FlowLayoutPanel _stripPanel;
        private Label _titleLabel;
        private Label _infoLabel;
        private List<Bitmap> _loadedThumbs = new List<Bitmap>();
        private int _totalItems = 0;

        public event Action<string, int> OnOpenRequested;

        public CardControl(string filePath, bool isZip, bool isPdf, int maxThumbs)
        {
            this.FilePath = filePath;
            this.IsZip = isZip;
            this.IsPdf = isPdf;
            this.MaxThumbs = maxThumbs;

            this.Width = 320;
            this.Height = 310;
            this.BackColor = Color.FromArgb(32, 35, 43);
            this.Margin = new Padding(10);
            this.BorderStyle = BorderStyle.FixedSingle;

            // 헤더 라벨
            string icon = IsZip ? "📦 " : (IsPdf ? "📑 " : "🖼️ ");
            _titleLabel = new Label
            {
                Text = icon + Path.GetFileName(filePath),
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoEllipsis = true,
                Padding = new Padding(6, 4, 6, 0)
            };

            _infoLabel = new Label
            {
                Text = GetFileSizeStr(filePath) + ((isZip || isPdf) ? " (스캔 중...)" : ""),
                Dock = DockStyle.Top,
                Height = 18,
                ForeColor = Color.FromArgb(140, 145, 160),
                Font = new Font("Segoe UI", 8F),
                Padding = new Padding(6, 0, 6, 0)
            };

            // 대표 메인 프리뷰
            _mainPic = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = 180,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(18, 19, 22),
                Cursor = Cursors.Hand
            };
            _mainPic.DoubleClick += (s, e) => { if (OnOpenRequested != null) OnOpenRequested(FilePath, 0); };

            // 하단 1~N장 썸네일 스트립
            _stripPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(24, 26, 32),
                Padding = new Padding(4)
            };

            // 버튼
            Button openBtn = new Button
            {
                Text = "뷰어로 열기",
                Dock = DockStyle.Bottom,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            openBtn.FlatAppearance.BorderSize = 0;
            openBtn.Click += (s, e) => { if (OnOpenRequested != null) OnOpenRequested(FilePath, 0); };

            this.Controls.Add(_stripPanel);
            this.Controls.Add(openBtn);
            this.Controls.Add(_mainPic);
            this.Controls.Add(_infoLabel);
            this.Controls.Add(_titleLabel);
        }

        public void LoadThumbnailData()
        {
            if (IsZip)
            {
                var res = ZipEngine.ExtractThumbnails(FilePath, MaxThumbs);
                _loadedThumbs = res.Thumbnails;
                _totalItems = res.TotalImages;
            }
            else if (IsPdf)
            {
                var res = PdfEngine.ExtractThumbnails(FilePath, MaxThumbs);
                _loadedThumbs = res.Thumbnails;
                _totalItems = res.TotalPages;
            }
            else
            {
                try
                {
                    Bitmap bmp = ImageLoader.LoadFromFile(FilePath);
                    if (bmp != null)
                    {
                        _loadedThumbs.Add(bmp);
                        _totalItems = 1;
                    }
                }
                catch { }
            }
        }

        public void UpdateUI()
        {
            if (this.IsDisposed) return;

            if (IsZip)
            {
                _infoLabel.Text = GetFileSizeStr(FilePath) + " | " + _totalItems + "장 포함";
            }
            else if (IsPdf)
            {
                _infoLabel.Text = GetFileSizeStr(FilePath) + " | " + _totalItems + "페이지 (PDF)";
            }
            else
            {
                _infoLabel.Text = GetFileSizeStr(FilePath) + (_loadedThumbs.Count > 0 ? (" | " + _loadedThumbs[0].Width + "\u00D7" + _loadedThumbs[0].Height) : "");
            }

            if (_loadedThumbs != null && _loadedThumbs.Count > 0)
            {
                _mainPic.Image = _loadedThumbs[0];

                _stripPanel.SuspendLayout();
                _stripPanel.Controls.Clear();

                for (int i = 0; i < _loadedThumbs.Count; i++)
                {
                    int index = i;
                    Bitmap thumbBmp = _loadedThumbs[i];

                    PictureBox miniPic = new PictureBox
                    {
                        Image = thumbBmp,
                        Width = 46,
                        Height = 46,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        BackColor = Color.Black,
                        Margin = new Padding(3),
                        Cursor = Cursors.Hand
                    };

                    // 마우스 호버 시 대표 프리뷰 실시간 전환! (스크러빙 미리보기)
                    miniPic.MouseEnter += (s, e) =>
                    {
                        _mainPic.Image = thumbBmp;
                    };

                    // 클릭 시 해당 이미지 인덱스로 바로 뷰어 열기
                    miniPic.Click += (s, e) =>
                    {
                        if (OnOpenRequested != null) OnOpenRequested(FilePath, index);
                    };

                    _stripPanel.Controls.Add(miniPic);
                }
                _stripPanel.ResumeLayout();
            }
        }

        private static string GetFileSizeStr(string path)
        {
            try
            {
                long bytes = new FileInfo(path).Length;
                if (bytes < 1024) return bytes + " B";
                if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
                return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            }
            catch { return ""; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_mainPic != null)
                {
                    _mainPic.Image = null;
                }
                if (_loadedThumbs != null)
                {
                    foreach (var bmp in _loadedThumbs)
                    {
                        if (bmp != null)
                        {
                            IDisposable d = bmp.Tag as IDisposable;
                            if (d != null) try { d.Dispose(); } catch { }
                            bmp.Dispose();
                        }
                    }
                    _loadedThumbs.Clear();
                }
            }
            base.Dispose(disposing);
        }
    }
}
