using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NyaaView
{
    public class QuickStartForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private Settings _settings;
        private RadioButton _rbWheelPageFlip;
        private RadioButton _rbWheelZoom;

        private RadioButton _rbCache512;
        private RadioButton _rbCache1024;
        private RadioButton _rbCache2048;
        private RadioButton _rbCacheHalfRam;
        private int _halfRamMb = 4096;

        public QuickStartForm(Settings settings)
        {
            _settings = settings ?? Settings.Load();
            InitializeComponent();
            ApplyDarkTitleBar();
            LoadCurrentValues();
        }

        public Settings GetUpdatedSettings()
        {
            return _settings;
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

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                if (this.Height > workArea.Height - 20)
                {
                    this.Height = workArea.Height - 20;
                }
                if (this.Bottom > workArea.Bottom)
                {
                    this.Top = Math.Max(workArea.Top + 10, workArea.Bottom - this.Height - 10);
                }
                if (this.Top < workArea.Top)
                {
                    this.Top = workArea.Top + 10;
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "nyaa-view 빠른 시작 안내";
            this.ClientSize = new Size(560, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(16, 17, 20);
            this.ForeColor = Color.FromArgb(235, 238, 245);
            this.Font = new Font("맑은 고딕", 9F, FontStyle.Regular);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // 1. 하단 버튼 바 (먼저 Dock = Bottom 설정하여 항상 최하단 고정)
            Panel footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(20, 21, 25),
                Padding = new Padding(16, 8, 16, 8)
            };
            footerPanel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(40, 44, 55), 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, footerPanel.Width, 0);
                }
            };

            Button btnStart = new Button
            {
                Text = "nyaa-view 시작하기",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(78, 128, 240),
                ForeColor = Color.White,
                Font = new Font("맑은 고딕", 10.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.FlatAppearance.MouseOverBackColor = Color.FromArgb(98, 145, 255);
            btnStart.Click += (s, e) => OnStartClicked();
            footerPanel.Controls.Add(btnStart);

            // 2. 상단 헤더
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(22, 24, 28),
                Padding = new Padding(16, 8, 16, 0)
            };
            headerPanel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(40, 44, 55), 1))
                {
                    e.Graphics.DrawLine(p, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
            };

            Label titleLabel = new Label
            {
                Text = "nyaa-view 빠른 시작 안내",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 255, 255)
            };
            Label subTitleLabel = new Label
            {
                Text = "마우스 휠 동작 방식과 캐시 설정을 확인해 주세요.",
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("맑은 고딕", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(145, 150, 165)
            };
            headerPanel.Controls.Add(subTitleLabel);
            headerPanel.Controls.Add(titleLabel);

            // 3. 중앙 스크롤 컨텐츠
            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(16, 10, 16, 10)
            };

            // [카드 1] 마우스 휠 동작 카드 (컴팩트 106px)
            Panel wheelCard = CreateCardPanel(106);
            wheelCard.Dock = DockStyle.Top;

            Label wheelHeader = new Label
            {
                Text = "1. 마우스 휠 동작 방식 선택",
                Location = new Point(12, 10),
                AutoSize = true,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 160, 255)
            };

            Label wheelTip = new Label
            {
                Text = "(단축키 [Z]로 언제든 즉시 전환 / Ctrl+휠로 반대 동작)",
                Location = new Point(190, 11),
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 125, 140),
                Font = new Font("맑은 고딕", 8F, FontStyle.Regular)
            };

            _rbWheelPageFlip = new RadioButton
            {
                Text = "페이지 넘기기 (이전 / 다음 장 이동) [만화/웹툰 추천]",
                Location = new Point(14, 38),
                Size = new Size(500, 24),
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 238, 245),
                Cursor = Cursors.Hand
            };
            Label wheelPageDesc = new Label
            {
                Text = "• 마우스 휠로 만화책이나 웹툰을 다음 페이지로 빠르게 넘깁니다.",
                Location = new Point(34, 60),
                AutoSize = true,
                ForeColor = Color.FromArgb(135, 140, 155),
                Font = new Font("맑은 고딕", 8F, FontStyle.Regular)
            };

            _rbWheelZoom = new RadioButton
            {
                Text = "화면 확대 / 축소 (Zoom In / Zoom Out) [사진/그래픽 추천]",
                Location = new Point(14, 80),
                Size = new Size(500, 24),
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 238, 245),
                Cursor = Cursors.Hand
            };

            wheelCard.Controls.Add(_rbWheelZoom);
            wheelCard.Controls.Add(wheelPageDesc);
            wheelCard.Controls.Add(_rbWheelPageFlip);
            wheelCard.Controls.Add(wheelTip);
            wheelCard.Controls.Add(wheelHeader);

            Panel gap1 = new Panel { Dock = DockStyle.Top, Height = 10 };

            // [카드 2] 캐시 메모리 설정 카드 (컴팩트 2열 배치 116px)
            int ramGb = SystemMemoryHelper.GetInstalledRamGb();
            _halfRamMb = SystemMemoryHelper.GetHalfPhysicalMemoryMb();

            Panel cacheCard = CreateCardPanel(116);
            cacheCard.Dock = DockStyle.Top;

            Label cacheHeader = new Label
            {
                Text = string.Format("2. 캐시 메모리 설정 (PC 감지 RAM: {0} GB)", ramGb > 0 ? ramGb.ToString() : "미상"),
                Location = new Point(12, 10),
                AutoSize = true,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 160, 255)
            };

            Label cacheDesc = new Label
            {
                Text = "웹툰 연속 스크롤 시 끊김 없는 부드러운 감상을 위해 메모리를 할당합니다.",
                Location = new Point(14, 32),
                AutoSize = true,
                ForeColor = Color.FromArgb(135, 140, 155),
                Font = new Font("맑은 고딕", 8F, FontStyle.Regular)
            };

            // 1열 (좌측)
            _rbCache1024 = new RadioButton
            {
                Text = "1024 MB (1 GB) [기본 추천]",
                Location = new Point(14, 54),
                Size = new Size(250, 24),
                ForeColor = Color.FromArgb(235, 238, 245),
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            _rbCache512 = new RadioButton
            {
                Text = "512 MB (일반 이미지)",
                Location = new Point(14, 82),
                Size = new Size(250, 24),
                ForeColor = Color.FromArgb(210, 215, 225),
                Cursor = Cursors.Hand
            };

            // 2열 (우측)
            _rbCache2048 = new RadioButton
            {
                Text = "2048 MB (2 GB) [웹툰 최적]",
                Location = new Point(270, 54),
                Size = new Size(250, 24),
                ForeColor = Color.FromArgb(235, 238, 245),
                Cursor = Cursors.Hand
            };

            string halfRamText = string.Format("시스템 권장 절반 ({0} MB)", _halfRamMb);
            _rbCacheHalfRam = new RadioButton
            {
                Text = halfRamText,
                Location = new Point(270, 82),
                Size = new Size(250, 24),
                ForeColor = Color.FromArgb(210, 215, 225),
                Cursor = Cursors.Hand
            };

            cacheCard.Controls.Add(_rbCacheHalfRam);
            cacheCard.Controls.Add(_rbCache2048);
            cacheCard.Controls.Add(_rbCache512);
            cacheCard.Controls.Add(_rbCache1024);
            cacheCard.Controls.Add(cacheDesc);
            cacheCard.Controls.Add(cacheHeader);

            Panel gap2 = new Panel { Dock = DockStyle.Top, Height = 10 };

            // [카드 3] 상세 설정 F2 안내 카드 (컴팩트 50px)
            Panel noticeCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(22, 28, 40),
                Padding = new Padding(12, 8, 12, 8)
            };
            noticeCard.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(45, 65, 110), 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, noticeCard.Width - 1, noticeCard.Height - 1);
                }
            };

            Label noticeLabel = new Label
            {
                Text = "※ 단축키 변경, 색상 필터, 파일 연결 등 상세 설정은 프로그램 실행 후\n언제든지 [F2] 키 (환경 설정)에서 자유롭게 변경하실 수 있습니다.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 205, 255),
                Font = new Font("맑은 고딕", 8.5F, FontStyle.Regular)
            };
            noticeCard.Controls.Add(noticeLabel);

            contentPanel.Controls.Add(noticeCard);
            contentPanel.Controls.Add(gap2);
            contentPanel.Controls.Add(cacheCard);
            contentPanel.Controls.Add(gap1);
            contentPanel.Controls.Add(wheelCard);

            this.Controls.Add(contentPanel);
            this.Controls.Add(headerPanel);
            this.Controls.Add(footerPanel);

            headerPanel.SendToBack();
            footerPanel.SendToBack();
            contentPanel.BringToFront();
        }

        private Panel CreateCardPanel(int height)
        {
            Panel card = new Panel
            {
                Height = height,
                BackColor = Color.FromArgb(22, 24, 28),
                Padding = new Padding(12)
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(36, 40, 50), 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                }
            };
            return card;
        }

        private void LoadCurrentValues()
        {
            // 휠 모드: 1 = 페이지 넘김, 0 = 줌
            if (_settings.WheelMode == 1)
            {
                _rbWheelPageFlip.Checked = true;
            }
            else
            {
                _rbWheelZoom.Checked = true;
            }

            // 캐시 메모리
            int curMb = _settings.CacheLimitMb;
            if (curMb <= 512)
            {
                _rbCache512.Checked = true;
            }
            else if (curMb <= 1024)
            {
                _rbCache1024.Checked = true;
            }
            else if (curMb <= 2048)
            {
                _rbCache2048.Checked = true;
            }
            else
            {
                _rbCacheHalfRam.Checked = true;
            }
        }

        private void OnStartClicked()
        {
            _settings.WheelMode = _rbWheelPageFlip.Checked ? 1 : 0;

            if (_rbCache512.Checked) _settings.CacheLimitMb = 512;
            else if (_rbCache1024.Checked) _settings.CacheLimitMb = 1024;
            else if (_rbCache2048.Checked) _settings.CacheLimitMb = 2048;
            else if (_rbCacheHalfRam.Checked) _settings.CacheLimitMb = _halfRamMb;

            _settings.FirstRunTutorialShown = true;
            _settings.Save();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
