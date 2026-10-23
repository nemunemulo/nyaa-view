using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NyaaView
{
    public class SettingsForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private Settings _settings;
        public Settings CurrentSettings { get { return _settings; } }

        // 사이드바 내비게이션
        private Panel _sideNav;
        private Panel _contentContainer;
        private List<Button> _navButtons = new List<Button>();
        private int _activeTabIndex = 0;

        // 탭별 메인 패널
        private Panel _panelKeys;
        private Panel _panelMouse;
        private Panel _panelFiles;
        private Panel _panelPerf;
        private Panel _panelAbout;

        // 파일 & 외부 편집기 컨트롤
        private CheckBox _chkEnableRecent;
        private NumericUpDown _numRecentMax;
        private Label _lblRecentCount;
        private Button _btnClearRecent;
        private RadioButton _radioEditorDefault;
        private RadioButton _radioEditorCustom;
        private TextBox _txtEditorPath;
        private Button _btnBrowseEditor;

        // 단축키 컨트롤
        private ListBox _nextKeysBox;
        private ListBox _prevKeysBox;
        private ComboBox _exifKeyCombo;
        private ComboBox _explorerKeyCombo;
        private ComboBox _fitKeyCombo;
        private ComboBox _rotateKeyCombo;
        private ComboBox _wheelToggleKeyCombo;

        // 마우스 컨트롤
        private Panel _cardOptionZoom;
        private Panel _cardOptionPage;
        private RadioButton _radioZoom;
        private RadioButton _radioPage;
        private CheckBox _chkWheelInvert;
        private CheckBox _chkWheelCtrlAlt;
        private CheckBox _chkAutoHideUi;

        // 만화책 보기 & 맞춤 컨트롤
        private ComboBox _comboDefaultViewMode;
        private ComboBox _comboDefaultFitMode;
        private ComboBox _comboDefaultInterpolation;
        private ComboBox _comboPageTransition;
        private CheckBox _chkStretchSmallImages;
        private CheckBox _chkSmartSpreadDetection;
        private CheckBox _chkWebtoonSmoothScroll;
        private CheckBox _chkWebtoonInertialDrag;
        private ComboBox _comboUpscaleMode;

        // 캐시 컨트롤
        private RadioButton _radioCacheMb;
        private RadioButton _radioCacheCount;
        private TrackBar _sliderCache;
        private NumericUpDown _numCacheVal;
        private Label _lblCacheUnit;
        private Label _lblRamInfo;
        private Label _lblCacheDesc;
        private FlowLayoutPanel _presetPanel;
        private ComboBox _comboAdjacentZip;
        private CheckBox _chkUnlimitedPixels;
        private bool _isUpdatingCacheUi = false;

        public SettingsForm(Settings initialSettings)
        {
            _settings = initialSettings ?? Settings.Load();
            InitializeComponent();
            ApplyDarkTitleBar();
            LoadValuesToUi();
            SwitchTab(0);
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "nyaa-view 환경 설정";
            this.Size = new Size(850, 670);
            this.MinimumSize = new Size(820, 640);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            this.BackColor = Color.FromArgb(16, 17, 20);
            this.ForeColor = Color.FromArgb(240, 240, 245);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // 1. 하단 액션 바 (저장 / 취소 / 기본값)
            Panel bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(22, 24, 28),
                Padding = new Padding(16, 11, 16, 11)
            };

            Button btnReset = CreateActionButton("↺ 기본값으로 복원", Color.FromArgb(36, 39, 48), Color.FromArgb(170, 175, 190), 135);
            btnReset.Dock = DockStyle.Left;
            btnReset.Click += (s, e) =>
            {
                if (MessageBox.Show("모든 환경 설정을 기본값으로 되돌리시겠습니까?", "설정 복원", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _settings.ResetToDefaults();
                    LoadValuesToUi();
                }
            };

            Button btnCancel = CreateActionButton("취소", Color.FromArgb(42, 46, 58), Color.White, 90);
            btnCancel.Dock = DockStyle.Right;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            Button btnSave = CreateActionButton("저장 및 적용", Color.FromArgb(78, 128, 240), Color.White, 110);
            btnSave.Dock = DockStyle.Right;
            btnSave.Click += (s, e) =>
            {
                SaveUiToValues();
                _settings.Save();
                this.DialogResult = DialogResult.OK;
            };

            bottomBar.Controls.Add(btnReset);
            bottomBar.Controls.Add(btnSave);
            bottomBar.Controls.Add(new Label { Dock = DockStyle.Right, Width = 10 });
            bottomBar.Controls.Add(btnCancel);

            // 2. 좌측 사이드바 내비게이션
            _sideNav = new Panel
            {
                Dock = DockStyle.Left,
                Width = 190,
                BackColor = Color.FromArgb(22, 24, 28),
                Padding = new Padding(10, 16, 10, 16)
            };

            Label navTitle = new Label
            {
                Text = "nyaa-view",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 159, 243),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            Label navSubtitle = new Label
            {
                Text = "ENVIRONMENT SETTINGS",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(110, 115, 130),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            Panel navDivider = new Panel { Dock = DockStyle.Top, Height = 12 };

            _navButtons.Add(CreateNavButton("단축키 & 내비", 0));
            _navButtons.Add(CreateNavButton("마우스 & 휠 조작", 1));
            _navButtons.Add(CreateNavButton("파일 & 확장자 연결", 2));
            _navButtons.Add(CreateNavButton("캐시 & 성능", 3));
            _navButtons.Add(CreateNavButton("프로그램 정보", 4));

            _sideNav.Controls.Add(_navButtons[4]);
            _sideNav.Controls.Add(_navButtons[3]);
            _sideNav.Controls.Add(_navButtons[2]);
            _sideNav.Controls.Add(_navButtons[1]);
            _sideNav.Controls.Add(_navButtons[0]);
            _sideNav.Controls.Add(navDivider);
            _sideNav.Controls.Add(navSubtitle);
            _sideNav.Controls.Add(navTitle);

            // 3. 우측 메인 콘텐츠 컨테이너
            _contentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 15, 18),
                Padding = new Padding(20, 18, 20, 18)
            };

            _panelKeys = BuildKeysPanel();
            _panelMouse = BuildMousePanel();
            _panelFiles = BuildFilesPanel();
            _panelPerf = BuildPerfPanel();
            _panelAbout = BuildAboutPanel();

            _contentContainer.Controls.Add(_panelAbout);
            _contentContainer.Controls.Add(_panelPerf);
            _contentContainer.Controls.Add(_panelFiles);
            _contentContainer.Controls.Add(_panelMouse);
            _contentContainer.Controls.Add(_panelKeys);

            this.Controls.Add(_contentContainer);
            this.Controls.Add(_sideNav);
            this.Controls.Add(bottomBar);
        }

        private Button CreateNavButton(string text, int index)
        {
            Button btn = new Button
            {
                Text = text,
                UseMnemonic = false,
                Dock = DockStyle.Top,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(170, 175, 190),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 35, 42);
            btn.Click += (s, e) => SwitchTab(index);
            return btn;
        }

        public void SwitchTab(int index)
        {
            _activeTabIndex = index;

            for (int i = 0; i < _navButtons.Count; i++)
            {
                Button b = _navButtons[i];
                if (i == index)
                {
                    b.BackColor = Color.FromArgb(35, 40, 52);
                    b.ForeColor = Color.FromArgb(100, 160, 255);
                    b.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
                else
                {
                    b.BackColor = Color.Transparent;
                    b.ForeColor = Color.FromArgb(160, 165, 180);
                    b.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
                }
            }

            _panelKeys.Visible = (index == 0);
            _panelMouse.Visible = (index == 1);
            _panelFiles.Visible = (index == 2);
            _panelPerf.Visible = (index == 3);
            _panelAbout.Visible = (index == 4);
        }

        // ==========================================
        // 1. 단축키 & 키 설정 패널 (모던 카드형 UI)
        // ==========================================
        private Panel BuildKeysPanel()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

            // 상단 안내
            Label title = new Label
            {
                Text = "단축키 & 키보드 조작 설정",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White
            };
            Label sub = new Label
            {
                Text = "다음/이전 이미지로 넘어가는 키를 복수로 등록하여 원하는 키 조합으로 자유롭게 감상할 수 있습니다.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 145, 160)
            };

            Panel spacer0 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 다음/이전 키 2열 레이아웃 카드
            Panel dualKeyCard = CreateCardPanel(185);
            dualKeyCard.Dock = DockStyle.Top;

            TableLayoutPanel tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            // [다음 키 영역]
            Panel nextCol = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) };
            Label nextTitle = new Label { Text = "▶ 다음 이미지 (Next Image) [복수 지원]", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(78, 128, 240) };
            _nextKeysBox = CreateDarkListBox();
            Panel nextAction = new Panel { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(0, 4, 0, 0) };
            Button addNextBtn = CreateSmallActionButton("+ 키 등록...", (s, e) => PromptAddKey(_nextKeysBox));
            Button delNextBtn = CreateSmallActionButton("- 삭제", (s, e) => RemoveSelectedKey(_nextKeysBox));
            nextAction.Controls.Add(delNextBtn);
            nextAction.Controls.Add(addNextBtn);
            nextCol.Controls.Add(_nextKeysBox);
            nextCol.Controls.Add(nextAction);
            nextCol.Controls.Add(nextTitle);

            // [이전 키 영역]
            Panel prevCol = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
            Label prevTitle = new Label { Text = "◀ 이전 이미지 (Prev Image) [복수 지원]", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(255, 159, 243) };
            _prevKeysBox = CreateDarkListBox();
            Panel prevAction = new Panel { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(0, 4, 0, 0) };
            Button addPrevBtn = CreateSmallActionButton("+ 키 등록...", (s, e) => PromptAddKey(_prevKeysBox));
            Button delPrevBtn = CreateSmallActionButton("- 삭제", (s, e) => RemoveSelectedKey(_prevKeysBox));
            prevAction.Controls.Add(delPrevBtn);
            prevAction.Controls.Add(addPrevBtn);
            prevCol.Controls.Add(_prevKeysBox);
            prevCol.Controls.Add(prevAction);
            prevCol.Controls.Add(prevTitle);

            tlp.Controls.Add(nextCol, 0, 0);
            tlp.Controls.Add(prevCol, 1, 0);
            dualKeyCard.Controls.Add(tlp);

            Panel spacer1 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 주요 기능 단축키 카드
            Panel funcCard = CreateCardPanel(210);
            funcCard.Dock = DockStyle.Top;

            Label funcTitle = new Label
            {
                Text = "주요 기능 기본 단축키",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White
            };

            TableLayoutPanel flp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(0, 4, 0, 0)
            };
            flp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            flp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            flp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            flp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            flp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            flp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            flp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            _exifKeyCombo = CreateKeyCombo();
            _explorerKeyCombo = CreateKeyCombo();
            _fitKeyCombo = CreateKeyCombo();
            _rotateKeyCombo = CreateKeyCombo();
            _wheelToggleKeyCombo = CreateKeyCombo();

            AddFuncRow(flp, 0, "•  정보 & EXIF 오버레이 토글:", _exifKeyCombo);
            AddFuncRow(flp, 1, "•  압축 파일 탐색기 창 열기:", _explorerKeyCombo);
            AddFuncRow(flp, 2, "•  화면 맞춤 (Fit to Screen):", _fitKeyCombo);
            AddFuncRow(flp, 3, "•  시계방향 90° 회전:", _rotateKeyCombo);
            AddFuncRow(flp, 4, "•  마우스 휠 모드 즉시 전환:", _wheelToggleKeyCombo);

            funcCard.Controls.Add(flp);
            funcCard.Controls.Add(funcTitle);

            p.Controls.Add(funcCard);
            p.Controls.Add(spacer1);
            p.Controls.Add(dualKeyCard);
            p.Controls.Add(spacer0);
            p.Controls.Add(sub);
            p.Controls.Add(title);

            return p;
        }

        // ==========================================
        // 2. 마우스 & 휠 스크롤 설정 패널
        // ==========================================
        private Panel BuildMousePanel()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

            Label title = new Label
            {
                Text = "마우스 & 휠 조작 설정",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White
            };
            Label sub = new Label
            {
                Text = "마우스 휠 스크롤을 확대/축소로 쓸지, 만화책처럼 앞뒷장 넘기기로 쓸지 선택할 수 있습니다.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 145, 160)
            };

            Panel spacer0 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 휠 동작 선택 모던 카드
            Panel wheelCard = CreateCardPanel(180);
            wheelCard.Dock = DockStyle.Top;

            Label wheelTitle = new Label { Text = "마우스 휠(스크롤) 기본 동작 방식", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.White };

            _cardOptionZoom = CreateSelectableCard(
                "•  이미지 확대 / 축소 (Zoom 모드) [기본값]",
                "마우스 휠을 돌리면 마우스 커서 위치를 중심으로 부드럽게 확대 및 축소합니다.",
                () => SetWheelMode(0)
            );
            _cardOptionZoom.Location = new Point(14, 38);
            _cardOptionZoom.Size = new Size(530, 58);

            _cardOptionPage = CreateSelectableCard(
                "•  이전 / 다음 이미지 넘기기 (Page Flip 모드)",
                "마우스 휠을 위/아래로 굴리면 이전 장, 다음 장으로 신속하게 넘어갑니다.",
                () => SetWheelMode(1)
            );
            _cardOptionPage.Location = new Point(14, 104);
            _cardOptionPage.Size = new Size(530, 58);

            _radioZoom = new RadioButton { Visible = false };
            _radioPage = new RadioButton { Visible = false };

            wheelCard.Controls.Add(_cardOptionPage);
            wheelCard.Controls.Add(_cardOptionZoom);
            wheelCard.Controls.Add(wheelTitle);

            Panel spacer1 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 세부 옵션 카드
            Panel optCard = CreateCardPanel(140);
            optCard.Dock = DockStyle.Top;
            Label optTitle = new Label { Text = "마우스 & 뷰어 화면 옵션", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.White };

            _chkWheelInvert = new CheckBox
            {
                Text = "마우스 휠 스크롤 방향 반전 (Invert Scroll)",
                Location = new Point(14, 34),
                AutoSize = true,
                ForeColor = Color.FromArgb(230, 235, 245),
                Cursor = Cursors.Hand
            };

            _chkWheelCtrlAlt = new CheckBox
            {
                Text = "Ctrl 키를 누른 채 휠 스크롤 시 반대 동작 수행 (페이지 모드일 때 Ctrl+휠로 줌)",
                Location = new Point(14, 62),
                AutoSize = true,
                ForeColor = Color.FromArgb(230, 235, 245),
                Cursor = Cursors.Hand
            };

            _chkAutoHideUi = new CheckBox
            {
                Text = "화면 넓게 보기 (마우스 오버 시 상단/하단 표시, 평소 UI 자동 숨김) [단축키 H]",
                Location = new Point(14, 90),
                AutoSize = true,
                ForeColor = Color.FromArgb(230, 235, 245),
                Cursor = Cursors.Hand
            };

            optCard.Controls.Add(_chkAutoHideUi);
            optCard.Controls.Add(_chkWheelCtrlAlt);
            optCard.Controls.Add(_chkWheelInvert);
            optCard.Controls.Add(optTitle);

            Panel spacer2 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 만화책 보기 & 화면 맞춤 카드
            Panel mangaCard = CreateCardPanel(335);
            mangaCard.Dock = DockStyle.Top;
            Label mangaTitle = new Label { Text = "만화책 보기 모드 & 화면 맞춤 기본값", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.White };

            Label lblView = new Label { Text = "기본 보기 모드:", Location = new Point(14, 36), AutoSize = true, ForeColor = Color.FromArgb(200, 205, 215) };
            _comboDefaultViewMode = new ComboBox
            {
                Location = new Point(125, 32),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _comboDefaultViewMode.Items.AddRange(new object[] { "1장 단면 보기", "2장 보기 (우에서 좌 ◁ 만화)", "2장 보기 (좌에서 우 ▷ 서적)", "웹툰 연속 스크롤 (세로 연결)" });

            Label lblFit = new Label { Text = "기본 화면 맞춤:", Location = new Point(14, 70), AutoSize = true, ForeColor = Color.FromArgb(200, 205, 215) };
            _comboDefaultFitMode = new ComboBox
            {
                Location = new Point(125, 66),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _comboDefaultFitMode.Items.AddRange(new object[] { "화면 맞춤 (Fit to Screen)", "가로 폭 맞춤 (Fit to Width)", "세로 높이 맞춤 (Fit to Height)", "원본 크기 (100% 1:1)" });

            Label lblInterp = new Label { Text = "기본 보간 방식:", Location = new Point(14, 104), AutoSize = true, ForeColor = Color.FromArgb(200, 205, 215) };
            _comboDefaultInterpolation = new ComboBox
            {
                Location = new Point(125, 100),
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _comboDefaultInterpolation.Items.AddRange(new object[] {
                "고품질 쌍입방 (Bicubic - 권장)",
                "쌍선형 (Bilinear - 부드러움/고속)",
                "최근접 이웃 (Nearest - 픽셀아트/도트)"
            });

            Label lblTrans = new Label { Text = "페이지 전환 효과:", Location = new Point(14, 138), AutoSize = true, ForeColor = Color.FromArgb(200, 205, 215) };
            _comboPageTransition = new ComboBox
            {
                Location = new Point(125, 134),
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _comboPageTransition.Items.AddRange(new object[] {
                "끄기 (즉시 전환) [기본값]",
                "암전 / 페이드 (Fade through Black)",
                "좌우 슬라이드 (Push Slide)",
                "페이지 넘김 (Page Turn / Wipe)"
            });

            _chkStretchSmallImages = new CheckBox
            {
                Text = "작은 그림 크게 보기 (화면/폭 맞춤 시 100% 이상 확대 허용) [단축키 S]",
                Location = new Point(14, 170),
                AutoSize = true,
                ForeColor = Color.FromArgb(230, 235, 245),
                Cursor = Cursors.Hand
            };

            _chkSmartSpreadDetection = new CheckBox
            {
                Text = "펼침면(스프레드) 스마트 단독 감지 (가로로 넓은 이미지는 1장으로 자동 표시) [단축키 Shift+V]",
                Location = new Point(14, 198),
                AutoSize = true,
                ForeColor = Color.FromArgb(230, 235, 245),
                Cursor = Cursors.Hand
            };

            _chkWebtoonSmoothScroll = new CheckBox
            {
                Text = "웹툰 모드 마우스 휠 부드러운 스크롤 (감속 보간 부드러운 이동)",
                Location = new Point(14, 226),
                AutoSize = true,
                ForeColor = Color.FromArgb(230, 235, 245),
                Cursor = Cursors.Hand
            };

            _chkWebtoonInertialDrag = new CheckBox
            {
                Text = "웹툰 모드 마우스 드래그 관성 스크롤 (웹/스마트폰처럼 휙휙 가속도 슬라이딩)",
                Location = new Point(14, 254),
                AutoSize = true,
                ForeColor = Color.FromArgb(230, 235, 245),
                Cursor = Cursors.Hand
            };

            Label lblUpscale = new Label { Text = "업스케일링 엔진:", Location = new Point(14, 286), AutoSize = true, ForeColor = Color.FromArgb(200, 205, 215) };
            _comboUpscaleMode = new ComboBox
            {
                Location = new Point(125, 282),
                Width = 330,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _comboUpscaleMode.Items.AddRange(new object[] {
                "0. 끄기 (Off - 기본값) [U]",
                "1. 스마트 엣지 (Lanczos/CAS 선명도 복원 - FSR 스타일)",
                "2. 2D 만화 펜선 복원 (Anime4K 라인 복원 & 안티앨리어싱)"
            });

            mangaCard.Controls.Add(_comboUpscaleMode);
            mangaCard.Controls.Add(lblUpscale);
            mangaCard.Controls.Add(_chkWebtoonInertialDrag);
            mangaCard.Controls.Add(_chkWebtoonSmoothScroll);
            mangaCard.Controls.Add(_chkSmartSpreadDetection);
            mangaCard.Controls.Add(_chkStretchSmallImages);
            mangaCard.Controls.Add(_comboPageTransition);
            mangaCard.Controls.Add(lblTrans);
            mangaCard.Controls.Add(_comboDefaultInterpolation);
            mangaCard.Controls.Add(lblInterp);
            mangaCard.Controls.Add(_comboDefaultFitMode);
            mangaCard.Controls.Add(lblFit);
            mangaCard.Controls.Add(_comboDefaultViewMode);
            mangaCard.Controls.Add(lblView);
            mangaCard.Controls.Add(mangaTitle);

            Panel spacer3 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 단축 팁 카드
            Panel tipCard = CreateCardPanel(115);
            tipCard.Dock = DockStyle.Top;
            Label tipTitle = new Label { Text = "실시간 보기 & 조작 단축 팁", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(255, 159, 243) };
            Label tipBody = new Label
            {
                Text = "• [V] 키로 1장 ↔ 2장(우→좌 만화) ↔ 2장(좌→우)을 언제든 즉시 전환할 수 있습니다.\n• [I] 키로 보간 방식(Bicubic ↔ Bilinear ↔ Nearest Neighbor)을 실시간 변경할 수 있습니다.\n• [F] 화면맞춤, [W] 가로 폭맞춤, [Shift+F] 세로 높이맞춤, [O] 원본100%, [S] 작은그림 확대 토글\n• 폭맞춤(W) 시 마우스 휠을 굴리면 상하 스크롤이 작동하여 긴 만화/웹툰을 부드럽게 감상할 수 있습니다.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(170, 175, 190),
                Font = new Font("Segoe UI", 8.5F)
            };
            tipCard.Controls.Add(tipBody);
            tipCard.Controls.Add(tipTitle);

            p.Controls.Add(tipCard);
            p.Controls.Add(spacer3);
            p.Controls.Add(mangaCard);
            p.Controls.Add(spacer2);
            p.Controls.Add(optCard);
            p.Controls.Add(spacer1);
            p.Controls.Add(wheelCard);
            p.Controls.Add(spacer0);
            p.Controls.Add(sub);
            p.Controls.Add(title);

            return p;
        }

        // ==========================================
        // 3. 파일 & 외부 편집기 패널
        // ==========================================
        private Panel BuildFilesPanel()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

            Label title = new Label
            {
                Text = "파일 열기 및 외부 그래픽 편집기 설정",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White
            };
            Label sub = new Label
            {
                Text = "우클릭 메뉴의 최근 열람 목록(히스토리) 보관 정책과 단축키(Ctrl+E)로 실행할 외부 편집기를 지정합니다.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 145, 160)
            };

            Panel spacer0 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // --- 카드 1: 최근 열기 목록 설정 ---
            Panel recentCard = CreateCardPanel(200);
            recentCard.Dock = DockStyle.Top;

            Label recentTitle = new Label
            {
                Text = "최근 열어본 파일 및 폴더 기록 (Recent History)",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White
            };

            _chkEnableRecent = new CheckBox
            {
                Text = "최근 열어본 항목(파일 및 폴더) 기록 기능 활성화",
                Location = new Point(14, 38),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 245),
                Cursor = Cursors.Hand
            };

            // 최근 개수 행
            Label lblMaxTitle = new Label
            {
                Text = "목록 최대 보관 개수 (1 ~ 10개):",
                Location = new Point(16, 75),
                AutoSize = true,
                ForeColor = Color.FromArgb(195, 200, 215),
                Font = new Font("Segoe UI", 9F)
            };

            _numRecentMax = new NumericUpDown
            {
                Location = new Point(220, 72),
                Width = 70,
                Minimum = 1,
                Maximum = 10,
                Value = 10,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };

            // 최근 기록 지우기 행
            _lblRecentCount = new Label
            {
                Text = "현재 저장된 최근 기록: 0개",
                Location = new Point(16, 115),
                AutoSize = true,
                ForeColor = Color.FromArgb(170, 175, 190),
                Font = new Font("Segoe UI", 9F)
            };

            _btnClearRecent = CreateSmallActionButton("기록 지우기", (s, e) =>
            {
                if (_settings.RecentItems != null && _settings.RecentItems.Count > 0)
                {
                    if (MessageBox.Show("저장된 최근 파일/폴더 기록을 모두 삭제하시겠습니까?", "최근 목록 초기화", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _settings.ClearRecentItems();
                        UpdateRecentCountLabel();
                    }
                }
                else
                {
                    MessageBox.Show("삭제할 최근 기록이 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
            _btnClearRecent.Location = new Point(220, 110);
            _btnClearRecent.Dock = DockStyle.None;
            _btnClearRecent.Size = new Size(110, 28);

            Label recentDesc = new Label
            {
                Text = "• 우클릭 메뉴의 [최근 열기] 서브메뉴에 최근 열람한 이미지 파일, 압축파일, 폴더가 실시간으로 기록됩니다.\n• 체크 해제 시 우클릭 메뉴에 최근 목록이 표시되지 않으며 추가로 기록되지 않습니다.",
                Location = new Point(14, 150),
                Size = new Size(580, 38),
                ForeColor = Color.FromArgb(140, 145, 160),
                Font = new Font("Segoe UI", 8.2F)
            };

            _chkEnableRecent.CheckedChanged += (s, e) =>
            {
                bool en = _chkEnableRecent.Checked;
                lblMaxTitle.Enabled = en;
                _numRecentMax.Enabled = en;
                _lblRecentCount.Enabled = en;
                _btnClearRecent.Enabled = en;
            };

            recentCard.Controls.Add(recentDesc);
            recentCard.Controls.Add(_btnClearRecent);
            recentCard.Controls.Add(_lblRecentCount);
            recentCard.Controls.Add(_numRecentMax);
            recentCard.Controls.Add(lblMaxTitle);
            recentCard.Controls.Add(_chkEnableRecent);
            recentCard.Controls.Add(recentTitle);

            Panel spacer1 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // --- 카드 2: 외부 그래픽 편집기 설정 ---
            Panel editorCard = CreateCardPanel(230);
            editorCard.Dock = DockStyle.Top;

            Label editorTitle = new Label
            {
                Text = "외부 그래픽 편집기 연결 (단축키: Ctrl + E)",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White
            };

            _radioEditorDefault = new RadioButton
            {
                Text = "Windows 기본 그림판 (mspaint.exe) 사용 [기본값]",
                Location = new Point(14, 38),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 245),
                Cursor = Cursors.Hand,
                Checked = true
            };

            _radioEditorCustom = new RadioButton
            {
                Text = "사용자 지정 외부 편집기 프로그램 (.exe) 사용 (포토샵, 클립스튜디오 등)",
                Location = new Point(14, 70),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 245),
                Cursor = Cursors.Hand
            };

            _txtEditorPath = new TextBox
            {
                Location = new Point(34, 102),
                Width = 430,
                Height = 26,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F)
            };

            _btnBrowseEditor = CreateSmallActionButton("찾아보기...", (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "외부 편집기 실행 파일 (.exe) 선택";
                    ofd.Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        _txtEditorPath.Text = ofd.FileName;
                        _radioEditorCustom.Checked = true;
                    }
                }
            });
            _btnBrowseEditor.Location = new Point(474, 101);
            _btnBrowseEditor.Dock = DockStyle.None;
            _btnBrowseEditor.Size = new Size(95, 27);

            Label editorDesc = new Label
            {
                Text = "• 이미지를 감상하다가 우클릭 메뉴의 [외부 편집기로 열기] 또는 Ctrl+E 단축키를 누르면 실행됩니다.\n• 압축파일(ZIP) 내부 이미지를 보고 있을 때도 임시 파일(%TEMP%)로 자동 압축 해제 후 안전하게 연결 실행됩니다.",
                Location = new Point(14, 150),
                Size = new Size(580, 48),
                ForeColor = Color.FromArgb(140, 145, 160),
                Font = new Font("Segoe UI", 8.2F)
            };

            EventHandler onEditorRadioChanged = (s, e) =>
            {
                bool isCustom = _radioEditorCustom.Checked;
                _txtEditorPath.Enabled = isCustom;
                _btnBrowseEditor.Enabled = isCustom;
            };
            _radioEditorDefault.CheckedChanged += onEditorRadioChanged;
            _radioEditorCustom.CheckedChanged += onEditorRadioChanged;

            editorCard.Controls.Add(editorDesc);
            editorCard.Controls.Add(_btnBrowseEditor);
            editorCard.Controls.Add(_txtEditorPath);
            editorCard.Controls.Add(_radioEditorCustom);
            editorCard.Controls.Add(_radioEditorDefault);
            editorCard.Controls.Add(editorTitle);

            Panel spacer2 = new Panel { Dock = DockStyle.Top, Height = 14 };
            Panel assocCard = BuildAssociationCard();

            p.Controls.Add(assocCard);
            p.Controls.Add(spacer2);
            p.Controls.Add(editorCard);
            p.Controls.Add(spacer1);
            p.Controls.Add(recentCard);
            p.Controls.Add(spacer0);
            p.Controls.Add(sub);
            p.Controls.Add(title);

            return p;
        }

        private Panel BuildAssociationCard()
        {
            Panel card = CreateCardPanel(340);
            card.Dock = DockStyle.Top;

            Label title = new Label
            {
                Text = "Windows 파일 확장자 연결 (기본 프로그램 등록)",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White
            };

            Label sub = new Label
            {
                Text = "탐색기에서 더블클릭 시 nyaa-view로 자동 열리도록 현재 사용자의 Windows 파일 연결을 등록합니다.",
                Location = new Point(14, 34),
                Size = new Size(580, 20),
                ForeColor = Color.FromArgb(170, 175, 190),
                Font = new Font("Segoe UI", 8.5F)
            };

            // 프리셋 버튼 패널
            Panel presetBar = new Panel
            {
                Location = new Point(14, 58),
                Size = new Size(580, 28),
                BackColor = Color.Transparent
            };

            Dictionary<string, CheckBox> extBoxes = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);

            Button btnSelImages = CreateSmallActionButton("🖼️ 이미지만", (s, e) =>
            {
                foreach (var kvp in extBoxes)
                {
                    bool isImg = !kvp.Key.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                                 !kvp.Key.Equals(".cbz", StringComparison.OrdinalIgnoreCase) &&
                                 !kvp.Key.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
                    kvp.Value.Checked = isImg;
                }
            });
            btnSelImages.Location = new Point(0, 0);
            btnSelImages.Size = new Size(95, 26);
            btnSelImages.Dock = DockStyle.None;

            Button btnSelComic = CreateSmallActionButton("📚 만화/PDF만", (s, e) =>
            {
                foreach (var kvp in extBoxes)
                {
                    bool isComic = kvp.Key.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                                   kvp.Key.Equals(".cbz", StringComparison.OrdinalIgnoreCase) ||
                                   kvp.Key.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
                    kvp.Value.Checked = isComic;
                }
            });
            btnSelComic.Location = new Point(102, 0);
            btnSelComic.Size = new Size(110, 26);
            btnSelComic.Dock = DockStyle.None;

            Button btnSelAll = CreateSmallActionButton("✓ 전체 선택", (s, e) =>
            {
                foreach (var chk in extBoxes.Values) chk.Checked = true;
            });
            btnSelAll.Location = new Point(220, 0);
            btnSelAll.Size = new Size(85, 26);
            btnSelAll.Dock = DockStyle.None;

            Button btnDeselAll = CreateSmallActionButton("✗ 전체 해제", (s, e) =>
            {
                foreach (var chk in extBoxes.Values) chk.Checked = false;
            });
            btnDeselAll.Location = new Point(312, 0);
            btnDeselAll.Size = new Size(85, 26);
            btnDeselAll.Dock = DockStyle.None;

            presetBar.Controls.Add(btnSelImages);
            presetBar.Controls.Add(btnSelComic);
            presetBar.Controls.Add(btnSelAll);
            presetBar.Controls.Add(btnDeselAll);

            // 체크박스 컨테이너
            Panel chkContainer = new Panel
            {
                Location = new Point(14, 92),
                Size = new Size(580, 150),
                BackColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10, 6, 10, 6)
            };

            // 1열: 표준 이미지
            Label lblGrp1 = new Label
            {
                Text = "표준 이미지",
                Location = new Point(10, 6),
                Size = new Size(140, 18),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255)
            };
            chkContainer.Controls.Add(lblGrp1);

            int y1 = 26;
            foreach (string ext in FileAssociation.StandardImageExts)
            {
                CheckBox chk = CreateExtCheckBox(ext);
                chk.Location = new Point(12, y1);
                chkContainer.Controls.Add(chk);
                extBoxes[ext] = chk;
                y1 += 20;
            }

            // 2열: 만화 압축 & PDF
            Label lblGrp2 = new Label
            {
                Text = "만화 압축 & 문서",
                Location = new Point(175, 6),
                Size = new Size(140, 18),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 159, 243)
            };
            chkContainer.Controls.Add(lblGrp2);

            int y2 = 26;
            foreach (string ext in FileAssociation.ComicAndDocExts)
            {
                CheckBox chk = CreateExtCheckBox(ext);
                chk.Location = new Point(177, y2);
                chkContainer.Controls.Add(chk);
                extBoxes[ext] = chk;
                y2 += 20;
            }

            // 3열: 그래픽 & 전문 포맷 (2단으로 깔끔하게 배치)
            Label lblGrp3 = new Label
            {
                Text = "전문/그래픽 포맷",
                Location = new Point(340, 6),
                Size = new Size(220, 18),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 230, 160)
            };
            chkContainer.Controls.Add(lblGrp3);

            int y3 = 26;
            int x3 = 342;
            int colIdx = 0;
            foreach (string ext in FileAssociation.GraphicsExts)
            {
                CheckBox chk = CreateExtCheckBox(ext);
                chk.Location = new Point(x3, y3);
                chkContainer.Controls.Add(chk);
                extBoxes[ext] = chk;
                y3 += 20;
                colIdx++;
                if (colIdx == 6)
                {
                    x3 += 105;
                    y3 = 26;
                }
            }

            // 하단 액션 버튼
            Button btnApplyAssoc = CreateActionButton("선택한 확장자 연결 적용", Color.FromArgb(78, 128, 240), Color.White, 160);
            btnApplyAssoc.Location = new Point(14, 250);
            btnApplyAssoc.Dock = DockStyle.None;
            btnApplyAssoc.Height = 32;
            btnApplyAssoc.Click += (s, e) =>
            {
                var selected = new List<string>();
                foreach (var kvp in extBoxes)
                {
                    if (kvp.Value.Checked) selected.Add(kvp.Key);
                }

                if (selected.Count == 0)
                {
                    MessageBox.Show("연결할 확장자를 1개 이상 선택해주세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool ok = FileAssociation.RegisterAssociations(selected);
                if (ok)
                {
                    MessageBox.Show(
                        string.Format("{0}개의 확장자가 nyaa-view에 연결되었습니다!\n\n※ Windows 탐색기에서 파일을 더블클릭하면 nyaa-view로 즉시 열립니다.\n(Windows 10/11 시스템 정책상 최초 1회 기본 앱 선택 알림창이 뜰 수 있습니다.)", selected.Count),
                        "확장자 연결 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("확장자 등록 중 오류가 발생했습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Button btnRemoveAssoc = CreateActionButton("선택한 확장자 연결 해제", Color.FromArgb(48, 52, 64), Color.FromArgb(220, 225, 235), 160);
            btnRemoveAssoc.Location = new Point(182, 250);
            btnRemoveAssoc.Dock = DockStyle.None;
            btnRemoveAssoc.Height = 32;
            btnRemoveAssoc.Click += (s, e) =>
            {
                var selected = new List<string>();
                foreach (var kvp in extBoxes)
                {
                    if (kvp.Value.Checked) selected.Add(kvp.Key);
                }

                if (selected.Count == 0)
                {
                    MessageBox.Show("해제할 확장자를 1개 이상 선택해주세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (MessageBox.Show(string.Format("선택한 {0}개 확장자의 nyaa-view 연결을 해제하시겠습니까?", selected.Count), "연결 해제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    FileAssociation.UnregisterAssociations(selected);
                    foreach (string ext in selected)
                    {
                        if (extBoxes.ContainsKey(ext)) extBoxes[ext].Checked = false;
                    }
                    MessageBox.Show("확장자 연결이 해제되었습니다.", "해제 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            Button btnWinDefault = CreateActionButton("⚙️ Windows 기본 앱 설정", Color.FromArgb(36, 40, 50), Color.FromArgb(170, 180, 205), 180);
            btnWinDefault.Location = new Point(350, 250);
            btnWinDefault.Dock = DockStyle.None;
            btnWinDefault.Height = 32;
            btnWinDefault.Click += (s, e) => FileAssociation.OpenWindowsDefaultAppsSettings();

            Label tipLbl = new Label
            {
                Text = "• 관리자 권한(UAC) 없이 현재 사용자 환경에 즉시 안전하게 적용됩니다.\n• Windows 11에서는 [Windows 기본 앱 설정] 버튼을 눌러 기본 사진 뷰어로 영구 지정할 수도 있습니다.",
                Location = new Point(14, 292),
                Size = new Size(580, 36),
                ForeColor = Color.FromArgb(140, 145, 160),
                Font = new Font("Segoe UI", 8.2F)
            };

            card.Controls.Add(tipLbl);
            card.Controls.Add(btnWinDefault);
            card.Controls.Add(btnRemoveAssoc);
            card.Controls.Add(btnApplyAssoc);
            card.Controls.Add(chkContainer);
            card.Controls.Add(presetBar);
            card.Controls.Add(sub);
            card.Controls.Add(title);

            return card;
        }

        private CheckBox CreateExtCheckBox(string ext)
        {
            string label = ext.ToUpperInvariant().TrimStart('.');
            CheckBox chk = new CheckBox
            {
                Text = label,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(230, 235, 245),
                Cursor = Cursors.Hand,
                Checked = FileAssociation.IsAssociated(ext)
            };
            return chk;
        }

        private void UpdateRecentCountLabel()
        {
            if (_lblRecentCount != null)
            {
                int count = (_settings.RecentItems != null) ? _settings.RecentItems.Count : 0;
                _lblRecentCount.Text = string.Format("현재 저장된 최근 기록: {0}개", count);
            }
        }

        // ==========================================
        // 4. 캐시 & 성능 설정 패널
        // ==========================================
        private Panel BuildPerfPanel()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

            Label title = new Label
            {
                Text = "캐시 메모리 & 성능 설정",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White
            };
            Label sub = new Label
            {
                Text = "압축 파일(ZIP) 열람 시 메모리 사전 디코딩 캐시 정책 및 인접 압축 파일 선행 캐싱을 조절합니다.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 145, 160)
            };

            Panel spacer0 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // --- 카드 1: 압축 파일 메모리 캐시 ---
            Panel cacheCard = CreateCardPanel(275);
            cacheCard.Dock = DockStyle.Top;

            Label cacheTitle = new Label
            {
                Text = "만화책 & 웹툰 대용량 사전 메모리 캐시 설정 (ZIP / PDF / 폴더 공통)",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White
            };

            int totalRamGb = SystemMemoryHelper.GetInstalledRamGb();
            long totalRamMb = SystemMemoryHelper.GetTotalPhysicalMemoryMb();
            int halfRamMb = SystemMemoryHelper.GetHalfPhysicalMemoryMb();

            _lblRamInfo = new Label
            {
                Text = string.Format("💻 시스템 물리 RAM: {0} GB ({1:N0} MB) | 슬라이더 최대 허용치: 절반인 {2:N0} MB", totalRamGb, totalRamMb, halfRamMb),
                Location = new Point(14, 34),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(115, 175, 255)
            };

            // 1. 캐시 모드 라디오 버튼
            _radioCacheMb = new RadioButton
            {
                Text = "메모리 용량 기준 (MB) [권장]",
                Location = new Point(14, 58),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Checked = true
            };

            _radioCacheCount = new RadioButton
            {
                Text = "페이지 수 기준 (장)",
                Location = new Point(230, 58),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(180, 185, 195),
                Cursor = Cursors.Hand
            };

            _radioCacheMb.CheckedChanged += (s, e) => { if (_radioCacheMb.Checked) OnCacheModeChanged(); };
            _radioCacheCount.CheckedChanged += (s, e) => { if (_radioCacheCount.Checked) OnCacheModeChanged(); };

            // 2. 슬라이더 + 수동 입력칸(NumericUpDown) + 단위 라벨
            _sliderCache = new TrackBar
            {
                Location = new Point(14, 92),
                Width = 360,
                Minimum = 64,
                Maximum = (int)Math.Max(1024, halfRamMb),
                Value = 256,
                TickStyle = TickStyle.None
            };

            _numCacheVal = new NumericUpDown
            {
                Location = new Point(386, 92),
                Width = 85,
                Height = 25,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Right,
                Minimum = 32,
                Maximum = 65536,
                Increment = 128,
                Value = 256
            };

            _lblCacheUnit = new Label
            {
                Location = new Point(478, 94),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 140, 255),
                Text = "MB"
            };

            _sliderCache.ValueChanged += (s, e) =>
            {
                if (!_isUpdatingCacheUi)
                {
                    _isUpdatingCacheUi = true;
                    try
                    {
                        int v = _sliderCache.Value;
                        if (v >= _numCacheVal.Minimum && v <= _numCacheVal.Maximum)
                            _numCacheVal.Value = v;
                        UpdateCacheLabel();
                    }
                    finally { _isUpdatingCacheUi = false; }
                }
            };

            _numCacheVal.ValueChanged += (s, e) =>
            {
                if (!_isUpdatingCacheUi)
                {
                    _isUpdatingCacheUi = true;
                    try
                    {
                        int v = (int)_numCacheVal.Value;
                        if (v >= _sliderCache.Minimum && v <= _sliderCache.Maximum)
                            _sliderCache.Value = v;
                        else if (v > _sliderCache.Maximum)
                            _sliderCache.Value = _sliderCache.Maximum;
                        else if (v < _sliderCache.Minimum)
                            _sliderCache.Value = _sliderCache.Minimum;
                        UpdateCacheLabel();
                    }
                    finally { _isUpdatingCacheUi = false; }
                }
            };

            // 3. MB 퀵 프리셋 버튼 패널
            _presetPanel = new FlowLayoutPanel
            {
                Location = new Point(14, 132),
                Size = new Size(620, 34),
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            Label presetLabel = new Label
            {
                Text = "빠른 프리셋:",
                AutoSize = true,
                ForeColor = Color.FromArgb(170, 175, 190),
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 6, 8, 0)
            };
            _presetPanel.Controls.Add(presetLabel);

            List<int> presetList = new List<int> { 256, 512, 1024, 2048, 4096 };
            if (halfRamMb > 512 && !presetList.Contains((int)halfRamMb))
            {
                presetList.Add((int)halfRamMb);
            }

            foreach (int mb in presetList)
            {
                int val = mb;
                string btnText = (val == (int)halfRamMb && val > 4096)
                    ? string.Format("RAM 50% ({0}M)", val)
                    : (val >= 1024 ? string.Format("{0} GB", val / 1024) : (val + " MB"));

                Button pBtn = new Button
                {
                    Text = btnText,
                    Width = (btnText.Length > 8 ? 105 : 68),
                    Height = 26,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(36, 40, 52),
                    ForeColor = Color.FromArgb(220, 225, 235),
                    Font = new Font("Segoe UI", 8.2F),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 6, 0)
                };
                pBtn.FlatAppearance.BorderSize = 0;
                pBtn.Click += (s, e) =>
                {
                    if (_radioCacheMb.Checked)
                    {
                        _isUpdatingCacheUi = true;
                        try
                        {
                            _numCacheVal.Value = Math.Max(_numCacheVal.Minimum, Math.Min(_numCacheVal.Maximum, val));
                            _sliderCache.Value = Math.Max(_sliderCache.Minimum, Math.Min(_sliderCache.Maximum, val));
                            UpdateCacheLabel();
                        }
                        finally { _isUpdatingCacheUi = false; }
                    }
                };
                _presetPanel.Controls.Add(pBtn);
            }

            // 4. 설명 라벨
            _lblCacheDesc = new Label
            {
                Text = "디코딩된 비트맵 메모리를 추적하여 한도(MB) 내에서 웹툰/만화 페이지를 스마트 RAM 캐싱합니다.\n대용량 메모리(1GB~4GB 이상) 설정 시 웹툰을 처음부터 끝까지 0초 지연 없이 즉시 탐색할 수 있습니다. (초과 시 먼 페이지부터 자동 해제)",
                Location = new Point(14, 175),
                Size = new Size(600, 56),
                ForeColor = Color.FromArgb(150, 155, 170),
                Font = new Font("Segoe UI", 8.5F)
            };

            cacheCard.Controls.Add(_lblCacheDesc);
            cacheCard.Controls.Add(_presetPanel);
            cacheCard.Controls.Add(_lblCacheUnit);
            cacheCard.Controls.Add(_numCacheVal);
            cacheCard.Controls.Add(_sliderCache);
            cacheCard.Controls.Add(_radioCacheCount);
            cacheCard.Controls.Add(_radioCacheMb);
            cacheCard.Controls.Add(_lblRamInfo);
            cacheCard.Controls.Add(cacheTitle);

            Panel spacer1 = new Panel { Dock = DockStyle.Top, Height = 12 };

            // --- 카드 2: 인접 압축 파일(ZIP) 선행 프리캐싱 ---
            Panel adjacentCard = CreateCardPanel(160);
            adjacentCard.Dock = DockStyle.Top;

            Label adjTitle = new Label
            {
                Text = "인접 압축 파일(ZIP) 선행 프리캐싱 (연속 읽기)",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White
            };

            Label adjSub = new Label
            {
                Text = "같은 폴더 내 이전/다음 권 압축 파일이 있을 때 백그라운드에서 미리 열어두어 무딜레이로 전환합니다.",
                Location = new Point(14, 32),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 145, 160)
            };

            Label adjModeLabel = new Label
            {
                Text = "선행 캐싱 대상:",
                Location = new Point(14, 62),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 225, 235)
            };

            _comboAdjacentZip = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(125, 58),
                Width = 320,
                Font = new Font("Segoe UI", 9F),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22
            };
            _comboAdjacentZip.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                Color bg = isSelected ? Color.FromArgb(48, 62, 92) : Color.FromArgb(28, 31, 40);
                Color fg = Color.White;
                using (SolidBrush b = new SolidBrush(bg))
                {
                    e.Graphics.FillRectangle(b, e.Bounds);
                }
                string txt = _comboAdjacentZip.Items[e.Index].ToString();
                using (SolidBrush tb = new SolidBrush(fg))
                {
                    e.Graphics.DrawString(txt, _comboAdjacentZip.Font, tb, e.Bounds.X + 6, e.Bounds.Y + 3);
                }
            };
            _comboAdjacentZip.Items.Add("0. 사용 안 함 (꺼짐 - 기본값)");
            _comboAdjacentZip.Items.Add("1. 뒷 파일 1개만 미리 캐싱 (다음 권 ▷)");
            _comboAdjacentZip.Items.Add("2. 앞 파일 1개만 미리 캐싱 (◁ 이전 권)");
            _comboAdjacentZip.Items.Add("3. 앞/뒤 1개씩 모두 미리 캐싱 (이전 권 + 다음 권)");
            _comboAdjacentZip.SelectedIndex = 0;

            Label adjDesc = new Label
            {
                Text = "만화책 1권의 마지막 페이지에서 다음으로 넘길 때 2권으로 즉각 0.00초 만에 부드럽게 이어집니다.\n백그라운드 스레드에서 첫 페이지만 가볍게 사전 디코딩하므로 UI 멈춤이나 불필요한 메모리 낭비가 없습니다.",
                Location = new Point(14, 98),
                Size = new Size(600, 48),
                ForeColor = Color.FromArgb(150, 155, 170),
                Font = new Font("Segoe UI", 8.5F)
            };

            adjacentCard.Controls.Add(adjDesc);
            adjacentCard.Controls.Add(_comboAdjacentZip);
            adjacentCard.Controls.Add(adjModeLabel);
            adjacentCard.Controls.Add(adjSub);
            adjacentCard.Controls.Add(adjTitle);

            Panel spacer2 = new Panel { Dock = DockStyle.Top, Height = 12 };

            // --- 카드 3: 초고화소 픽셀 안전 한도 해제 ---
            Panel pixelCard = CreateCardPanel(140);
            pixelCard.Dock = DockStyle.Top;

            Label pixelTitle = new Label
            {
                Text = "초고화소 사진 & 픽셀 안전 한도",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White
            };

            Label pixelSub = new Label
            {
                Text = "기본 상태에서도 스마트폰 200MP 최고화소 및 초광각 파노라마가 모두 열립니다.",
                Location = new Point(14, 32),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 145, 160)
            };

            _chkUnlimitedPixels = new CheckBox
            {
                Text = "초고화소 픽셀 안전 한도 완전 해제 (무제한 로드 허용)",
                Location = new Point(16, 62),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 240, 255),
                Cursor = Cursors.Hand,
                Checked = _settings.UnlimitedPixelLimit
            };

            Label pixelDesc = new Label
            {
                Text = "• 기본 안전 모드: 최대 5억 화소(500MP, 갤럭시 200MP 카메라의 2.5배) 및 가로 200,000px 파노라마 지원\n• 체크 시: 기가픽셀(Gigapixel) 등 모든 픽셀/해상도/파일 크기 제한을 해제하여 무제한으로 엽니다. (가용 RAM 범위 내)",
                Location = new Point(14, 92),
                Size = new Size(620, 38),
                ForeColor = Color.FromArgb(150, 155, 170),
                Font = new Font("Segoe UI", 8.5F)
            };

            pixelCard.Controls.Add(pixelDesc);
            pixelCard.Controls.Add(_chkUnlimitedPixels);
            pixelCard.Controls.Add(pixelSub);
            pixelCard.Controls.Add(pixelTitle);

            p.Controls.Add(pixelCard);
            p.Controls.Add(spacer2);
            p.Controls.Add(adjacentCard);
            p.Controls.Add(spacer1);
            p.Controls.Add(cacheCard);
            p.Controls.Add(spacer0);
            p.Controls.Add(sub);
            p.Controls.Add(title);

            return p;
        }

        private void OnCacheModeChanged()
        {
            if (_radioCacheMb == null || _sliderCache == null || _numCacheVal == null) return;

            if (_radioCacheMb.Checked)
            {
                _radioCacheMb.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                _radioCacheMb.ForeColor = Color.White;
                _radioCacheCount.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                _radioCacheCount.ForeColor = Color.FromArgb(180, 185, 195);

                int maxSlider = SystemMemoryHelper.GetHalfPhysicalMemoryMb();

                _sliderCache.Minimum = 64;
                _sliderCache.Maximum = maxSlider;
                _sliderCache.SmallChange = 64;
                _sliderCache.LargeChange = 256;

                _numCacheVal.Minimum = 32;
                _numCacheVal.Maximum = 65536;
                _numCacheVal.Increment = 128;

                int mbVal = _settings.CacheLimitMb > 0 ? _settings.CacheLimitMb : 256;
                _isUpdatingCacheUi = true;
                try
                {
                    _numCacheVal.Value = Math.Max(_numCacheVal.Minimum, Math.Min(_numCacheVal.Maximum, mbVal));
                    _sliderCache.Value = Math.Max(_sliderCache.Minimum, Math.Min(_sliderCache.Maximum, mbVal));
                }
                finally { _isUpdatingCacheUi = false; }

                if (_presetPanel != null) _presetPanel.Visible = true;
                if (_lblCacheDesc != null)
                {
                    _lblCacheDesc.Text = "디코딩된 비트맵 메모리를 추적하여 한도(MB) 내에서 웹툰/만화 페이지를 스마트 RAM 캐싱합니다.\n대용량 메모리(1GB~4GB 이상) 설정 시 웹툰을 처음부터 끝까지 0초 지연 없이 즉시 탐색할 수 있습니다. (초과 시 먼 페이지부터 자동 해제)";
                }
            }
            else
            {
                _radioCacheMb.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                _radioCacheMb.ForeColor = Color.FromArgb(180, 185, 195);
                _radioCacheCount.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                _radioCacheCount.ForeColor = Color.White;

                _sliderCache.Minimum = 1;
                _sliderCache.Maximum = 50;
                _sliderCache.SmallChange = 1;
                _sliderCache.LargeChange = 5;

                _numCacheVal.Minimum = 1;
                _numCacheVal.Maximum = 100;
                _numCacheVal.Increment = 1;

                int countVal = _settings.PrecacheCount > 0 ? _settings.PrecacheCount : 10;
                _isUpdatingCacheUi = true;
                try
                {
                    _numCacheVal.Value = Math.Max(_numCacheVal.Minimum, Math.Min(_numCacheVal.Maximum, countVal));
                    _sliderCache.Value = Math.Max(_sliderCache.Minimum, Math.Min(_sliderCache.Maximum, countVal));
                }
                finally { _isUpdatingCacheUi = false; }

                if (_presetPanel != null) _presetPanel.Visible = false;
                if (_lblCacheDesc != null)
                {
                    _lblCacheDesc.Text = "현재 보고 있는 이미지를 기준으로 앞/뒤 N장을 고정적으로 백그라운드 메모리에 미리 디코딩합니다.\n연속으로 빠르게 넘겨도 딜레이 없이 0.01초 만에 즉각 렌더링됩니다. (권장: 10장)";
                }
            }
            UpdateCacheLabel();
        }

        // ==========================================
        // 4. 프로그램 정보 패널
        // ==========================================
        private Panel BuildAboutPanel()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

            Label title = new Label { Text = "프로그램 정보 & 오픈소스 라이선스", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.White };
            Panel spacer0 = new Panel { Dock = DockStyle.Top, Height = 14 };

            Panel aboutCard = CreateCardPanel(285);
            aboutCard.Dock = DockStyle.Top;

            Label appLogo = new Label { Text = "nyaa-view (냐-뷰)", Location = new Point(16, 16), AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.FromArgb(255, 159, 243) };
            Label appDesc = new Label
            {
                Text = "Windows용 경량 이미지 및 만화/전자책 뷰어\n\n" +
                       "• 지원 포맷: JPEG, PNG, GIF, WebP, BMP, TIFF, PSD, TGA, DDS, QOI, PDF, ZIP, CBZ\n" +
                       "• 압축 파일: ZIP/CBZ 아카이브 인메모리 스트리밍 (한글 CP949 호환)\n" +
                       "• 보기 모드: 1장 보기, 2장 양면 보기(만화책/서적), 웹툰 세로 연속 스크롤 스트림\n" +
                       "• 필터 엔진: 3×3 컨볼루션 선명/흐림/중간, 언샤프 마스크, 만화 톤 보정, 야간 반전\n" +
                       "• 메타데이터: Tab 키 EXIF 카메라 촬영 정보 HUD 오버레이\n" +
                       "• 라이선스: MIT License (자유로운 사용, 수정, 배포 허용 오픈소스)",
                Location = new Point(16, 48),
                Size = new Size(530, 165),
                ForeColor = Color.FromArgb(190, 195, 210),
                Font = new Font("Segoe UI", 8.8F)
            };

            Button btnTutorial = CreateActionButton("빠른 시작 안내 가이드 다시 보기", Color.FromArgb(36, 45, 65), Color.FromArgb(200, 220, 255), 230);
            btnTutorial.Location = new Point(16, 230);
            btnTutorial.Click += (s, e) =>
            {
                using (QuickStartForm tut = new QuickStartForm(_settings))
                {
                    if (tut.ShowDialog(this) == DialogResult.OK)
                    {
                        _settings = tut.GetUpdatedSettings();
                        LoadValuesToUi();
                    }
                }
            };

            aboutCard.Controls.Add(btnTutorial);
            aboutCard.Controls.Add(appDesc);
            aboutCard.Controls.Add(appLogo);

            p.Controls.Add(aboutCard);
            p.Controls.Add(spacer0);
            p.Controls.Add(title);

            return p;
        }

        // ==========================================
        // UI 헬퍼 컴포넌트들
        // ==========================================
        private Panel CreateCardPanel(int height)
        {
            Panel card = new Panel
            {
                Height = height,
                BackColor = Color.FromArgb(22, 24, 30),
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 0, 12)
            };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(42, 46, 58), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };
            return card;
        }

        private Panel CreateSelectableCard(string title, string desc, Action onClick)
        {
            Panel card = new Panel
            {
                BackColor = Color.FromArgb(28, 31, 39),
                Cursor = Cursors.Hand,
                Padding = new Padding(12, 10, 12, 10)
            };

            Label lblTitle = new Label { Text = title, Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.White };
            Label lblDesc = new Label { Text = desc, Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(145, 150, 165) };

            lblTitle.Click += (s, e) => onClick();
            lblDesc.Click += (s, e) => onClick();
            card.Click += (s, e) => onClick();

            card.Controls.Add(lblDesc);
            card.Controls.Add(lblTitle);

            card.Paint += (s, e) =>
            {
                bool isSelected = false;
                if (title.Contains("Zoom") && _radioZoom.Checked) isSelected = true;
                if (title.Contains("Page") && _radioPage.Checked) isSelected = true;

                Color borderColor = isSelected ? Color.FromArgb(78, 128, 240) : Color.FromArgb(45, 49, 62);
                int borderWidth = isSelected ? 2 : 1;

                using (Pen pen = new Pen(borderColor, borderWidth))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };

            return card;
        }

        private ListBox CreateDarkListBox()
        {
            ListBox lb = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 17, 22),
                ForeColor = Color.FromArgb(235, 235, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                ItemHeight = 24,
                DrawMode = DrawMode.OwnerDrawFixed
            };

            lb.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                Color bg = isSelected ? Color.FromArgb(42, 52, 75) : Color.FromArgb(16, 17, 22);
                Color fg = isSelected ? Color.FromArgb(120, 175, 255) : Color.FromArgb(230, 230, 240);

                using (SolidBrush b = new SolidBrush(bg))
                {
                    e.Graphics.FillRectangle(b, e.Bounds);
                }

                string txt = lb.Items[e.Index].ToString();
                using (Font font = new Font("Segoe UI", 9F, isSelected ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(fg))
                {
                    e.Graphics.DrawString("•  " + txt, font, tb, e.Bounds.X + 6, e.Bounds.Y + 3);
                }
            };

            return lb;
        }

        private Button CreateSmallActionButton(string text, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Dock = DockStyle.Left,
                Width = 90,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(36, 40, 52),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 0, 6, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 54, 70);
            btn.Click += onClick;
            return btn;
        }

        private Button CreateActionButton(string text, Color bg, Color fg, int width)
        {
            Button btn = new Button
            {
                Text = text,
                Width = width,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private ComboBox CreateKeyCombo()
        {
            ComboBox cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(28, 31, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22
            };
            cb.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                Color bg = isSelected ? Color.FromArgb(48, 62, 92) : Color.FromArgb(28, 31, 40);
                Color fg = Color.White;
                using (SolidBrush b = new SolidBrush(bg))
                {
                    e.Graphics.FillRectangle(b, e.Bounds);
                }
                string txt = cb.Items[e.Index].ToString();
                using (SolidBrush tb = new SolidBrush(fg))
                {
                    e.Graphics.DrawString(txt, cb.Font, tb, e.Bounds.X + 6, e.Bounds.Y + 3);
                }
            };
            var commonKeys = new Keys[]
            {
                Keys.Tab, Keys.E, Keys.F, Keys.R, Keys.Z, Keys.G, Keys.Q, Keys.W, Keys.A, Keys.S, Keys.D,
                Keys.Space, Keys.Enter, Keys.Escape, Keys.F1, Keys.F2, Keys.F11, Keys.F12,
                Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown
            };
            foreach (Keys k in commonKeys)
            {
                cb.Items.Add(k);
            }
            return cb;
        }

        private void AddFuncRow(TableLayoutPanel p, int row, string label, Control ctrl)
        {
            Label lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(195, 200, 215),
                Font = new Font("Segoe UI", 9F)
            };
            p.Controls.Add(lbl, 0, row);
            p.Controls.Add(ctrl, 1, row);
        }

        private void SetWheelMode(int mode)
        {
            _radioZoom.Checked = (mode == 0);
            _radioPage.Checked = (mode == 1);
            if (_cardOptionZoom != null) _cardOptionZoom.Invalidate();
            if (_cardOptionPage != null) _cardOptionPage.Invalidate();
        }

        private void UpdateCacheLabel()
        {
            if (_lblCacheUnit != null && _numCacheVal != null)
            {
                if (_radioCacheMb != null && _radioCacheMb.Checked)
                {
                    int val = (int)_numCacheVal.Value;
                    if (val >= 1024)
                    {
                        _lblCacheUnit.Text = string.Format("MB ({0:F1} GB)", val / 1024.0);
                    }
                    else
                    {
                        _lblCacheUnit.Text = "MB";
                    }
                }
                else
                {
                    _lblCacheUnit.Text = "장";
                }
            }
        }

        // ==========================================
        // 데이터 로드 & 저장
        // ==========================================
        private void LoadValuesToUi()
        {
            // 다음 키
            _nextKeysBox.Items.Clear();
            foreach (Keys k in _settings.NextKeys)
            {
                _nextKeysBox.Items.Add(new KeyItem(k));
            }

            // 이전 키
            _prevKeysBox.Items.Clear();
            foreach (Keys k in _settings.PrevKeys)
            {
                _prevKeysBox.Items.Add(new KeyItem(k));
            }

            // 기능 키
            SetComboKey(_exifKeyCombo, _settings.ExifKey);
            SetComboKey(_explorerKeyCombo, _settings.ExplorerKey);
            SetComboKey(_fitKeyCombo, _settings.FitKey);
            SetComboKey(_rotateKeyCombo, _settings.RotateKey);
            SetComboKey(_wheelToggleKeyCombo, _settings.WheelToggleKey);

            // 마우스
            SetWheelMode(_settings.WheelMode);
            _chkWheelInvert.Checked = _settings.WheelInvert;
            _chkWheelCtrlAlt.Checked = _settings.WheelCtrlAlternate;
            _chkAutoHideUi.Checked = _settings.AutoHideUi;

            // 만화책 보기 & 맞춤 모드
            int vmIdx = (int)_settings.ViewMode;
            if (vmIdx >= 0 && vmIdx < _comboDefaultViewMode.Items.Count) _comboDefaultViewMode.SelectedIndex = vmIdx;
            else _comboDefaultViewMode.SelectedIndex = 0;

            int fmIdx = (int)_settings.FitMode;
            if (fmIdx >= 0 && fmIdx < _comboDefaultFitMode.Items.Count) _comboDefaultFitMode.SelectedIndex = fmIdx;
            else _comboDefaultFitMode.SelectedIndex = 0;

            int imIdx = (int)_settings.InterpolationMode;
            if (imIdx >= 0 && imIdx < _comboDefaultInterpolation.Items.Count) _comboDefaultInterpolation.SelectedIndex = imIdx;
            else _comboDefaultInterpolation.SelectedIndex = 0;

            int ptIdx = (int)_settings.PageTransition;
            if (ptIdx >= 0 && ptIdx < _comboPageTransition.Items.Count) _comboPageTransition.SelectedIndex = ptIdx;
            else _comboPageTransition.SelectedIndex = 0;

            _chkStretchSmallImages.Checked = _settings.StretchSmallImages;
            _chkSmartSpreadDetection.Checked = _settings.SmartSpreadDetection;
            _chkWebtoonSmoothScroll.Checked = _settings.WebtoonSmoothScroll;
            _chkWebtoonInertialDrag.Checked = _settings.WebtoonInertialDrag;

            if (_comboUpscaleMode != null)
            {
                int uIdx = (int)_settings.UpscaleMode;
                if (uIdx >= 0 && uIdx < _comboUpscaleMode.Items.Count) _comboUpscaleMode.SelectedIndex = uIdx;
                else _comboUpscaleMode.SelectedIndex = 0;
            }

            // 파일 & 외부 편집기
            _chkEnableRecent.Checked = _settings.EnableRecentList;
            _numRecentMax.Value = Math.Max(1, Math.Min(10, _settings.RecentMaxCount));
            UpdateRecentCountLabel();

            string editorPath = _settings.ExternalEditorPath;
            if (string.IsNullOrEmpty(editorPath) ||
                editorPath.Trim().Equals("mspaint", StringComparison.OrdinalIgnoreCase) ||
                editorPath.Trim().Equals("mspaint.exe", StringComparison.OrdinalIgnoreCase))
            {
                _radioEditorDefault.Checked = true;
                _radioEditorCustom.Checked = false;
                _txtEditorPath.Text = "";
                _txtEditorPath.Enabled = false;
                _btnBrowseEditor.Enabled = false;
            }
            else
            {
                _radioEditorDefault.Checked = false;
                _radioEditorCustom.Checked = true;
                _txtEditorPath.Text = editorPath;
                _txtEditorPath.Enabled = true;
                _btnBrowseEditor.Enabled = true;
            }

            // 인접 압축 파일 캐시
            if (_comboAdjacentZip != null)
            {
                int adjIdx = (int)_settings.AdjacentZipCache;
                if (adjIdx >= 0 && adjIdx < _comboAdjacentZip.Items.Count)
                    _comboAdjacentZip.SelectedIndex = adjIdx;
                else
                    _comboAdjacentZip.SelectedIndex = 0;
            }

            // 초고화소 픽셀 안전 한도 해제
            if (_chkUnlimitedPixels != null)
            {
                _chkUnlimitedPixels.Checked = _settings.UnlimitedPixelLimit;
            }

            // 캐시
            if (_settings.CacheMode == CacheLimitMode.MegaBytes)
            {
                if (_radioCacheMb != null) _radioCacheMb.Checked = true;
                if (_radioCacheCount != null) _radioCacheCount.Checked = false;
            }
            else
            {
                if (_radioCacheMb != null) _radioCacheMb.Checked = false;
                if (_radioCacheCount != null) _radioCacheCount.Checked = true;
            }
            OnCacheModeChanged();
        }

        private void SaveUiToValues()
        {
            // 다음 키
            List<Keys> nextList = new List<Keys>();
            foreach (var item in _nextKeysBox.Items)
            {
                KeyItem ki = item as KeyItem;
                if (ki != null && !nextList.Contains(ki.Key))
                    nextList.Add(ki.Key);
            }
            if (nextList.Count > 0) _settings.NextKeys = nextList;

            // 이전 키
            List<Keys> prevList = new List<Keys>();
            foreach (var item in _prevKeysBox.Items)
            {
                KeyItem ki = item as KeyItem;
                if (ki != null && !prevList.Contains(ki.Key))
                    prevList.Add(ki.Key);
            }
            if (prevList.Count > 0) _settings.PrevKeys = prevList;

            // 기능 키
            _settings.ExifKey = GetComboKey(_exifKeyCombo, Keys.Tab);
            _settings.ExplorerKey = GetComboKey(_explorerKeyCombo, Keys.E);
            _settings.FitKey = GetComboKey(_fitKeyCombo, Keys.F);
            _settings.RotateKey = GetComboKey(_rotateKeyCombo, Keys.R);
            _settings.WheelToggleKey = GetComboKey(_wheelToggleKeyCombo, Keys.Z);

            // 마우스
            _settings.WheelMode = _radioPage.Checked ? 1 : 0;
            _settings.WheelInvert = _chkWheelInvert.Checked;
            _settings.WheelCtrlAlternate = _chkWheelCtrlAlt.Checked;
            _settings.AutoHideUi = _chkAutoHideUi.Checked;

            // 만화책 보기 & 맞춤 모드
            _settings.ViewMode = (ViewLayout)Math.Max(0, Math.Min(3, _comboDefaultViewMode.SelectedIndex));
            _settings.FitMode = (DisplayFitMode)Math.Max(0, Math.Min(3, _comboDefaultFitMode.SelectedIndex));
            _settings.InterpolationMode = (InterpolationModeType)Math.Max(0, Math.Min(2, _comboDefaultInterpolation.SelectedIndex));
            _settings.PageTransition = (PageTransitionEffect)Math.Max(0, Math.Min(3, _comboPageTransition.SelectedIndex));
            _settings.StretchSmallImages = _chkStretchSmallImages.Checked;
            _settings.SmartSpreadDetection = _chkSmartSpreadDetection.Checked;
            _settings.WebtoonSmoothScroll = _chkWebtoonSmoothScroll.Checked;
            _settings.WebtoonInertialDrag = _chkWebtoonInertialDrag.Checked;
            if (_comboUpscaleMode != null)
            {
                _settings.UpscaleMode = (UpscaleMode)Math.Max(0, Math.Min(2, _comboUpscaleMode.SelectedIndex));
            }

            // 파일 & 외부 편집기
            _settings.EnableRecentList = _chkEnableRecent.Checked;
            _settings.RecentMaxCount = (int)_numRecentMax.Value;
            if (_radioEditorDefault.Checked)
            {
                _settings.ExternalEditorPath = "";
            }
            else
            {
                _settings.ExternalEditorPath = _txtEditorPath.Text.Trim();
            }

            // 인접 압축 파일 캐시
            if (_comboAdjacentZip != null)
            {
                _settings.AdjacentZipCache = (AdjacentArchiveCacheMode)Math.Max(0, Math.Min(3, _comboAdjacentZip.SelectedIndex));
            }

            // 초고화소 픽셀 안전 한도 해제
            if (_chkUnlimitedPixels != null)
            {
                _settings.UnlimitedPixelLimit = _chkUnlimitedPixels.Checked;
                ImageLoader.UnlimitedPixelLimit = _settings.UnlimitedPixelLimit;
            }

            // 캐시
            _settings.CacheMode = (_radioCacheMb != null && _radioCacheMb.Checked) ? CacheLimitMode.MegaBytes : CacheLimitMode.PageCount;
            if (_settings.CacheMode == CacheLimitMode.MegaBytes)
            {
                _settings.CacheLimitMb = (int)_numCacheVal.Value;
            }
            else
            {
                _settings.PrecacheCount = (int)_numCacheVal.Value;
            }
        }

        private void PromptAddKey(ListBox targetBox)
        {
            using (KeyCaptureDialog dlg = new KeyCaptureDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.CapturedKey != Keys.None)
                {
                    KeyItem item = new KeyItem(dlg.CapturedKey);
                    if (!targetBox.Items.Contains(item))
                    {
                        targetBox.Items.Add(item);
                    }
                }
            }
        }

        private void RemoveSelectedKey(ListBox targetBox)
        {
            if (targetBox.SelectedIndex >= 0)
            {
                targetBox.Items.RemoveAt(targetBox.SelectedIndex);
            }
        }

        private void SetComboKey(ComboBox cb, Keys key)
        {
            if (!cb.Items.Contains(key))
            {
                cb.Items.Add(key);
            }
            cb.SelectedItem = key;
        }

        private Keys GetComboKey(ComboBox cb, Keys fallback)
        {
            if (cb.SelectedItem is Keys) return (Keys)cb.SelectedItem;
            return fallback;
        }
    }

    // 모던 다크모드 키 입력 캡처 다이얼로그
    public class KeyCaptureDialog : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public Keys CapturedKey { get; private set; }

        public KeyCaptureDialog()
        {
            this.Text = "새 단축키 등록";
            this.Size = new Size(360, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(20, 22, 28);
            this.ForeColor = Color.White;
            this.KeyPreview = true;

            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
            }
            catch { }

            Label title = new Label
            {
                Text = "⌨ 등록할 키를 누르세요...",
                Dock = DockStyle.Top,
                Height = 46,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 159, 243)
            };

            Label prompt = new Label
            {
                Text = "방향키, 문자, 숫자, PageUp/Down, 스페이스 등\n원하는 키를 키보드에서 한 번 누르시면 등록됩니다.\n\n(취소하려면 ESC를 누르세요)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(170, 175, 190)
            };

            this.Controls.Add(prompt);
            this.Controls.Add(title);

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    return;
                }

                if (e.KeyCode != Keys.None)
                {
                    CapturedKey = e.KeyCode;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            };
        }
    }
}
