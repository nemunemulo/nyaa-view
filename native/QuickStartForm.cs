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

        private void InitializeComponent()
        {
            this.Text = "nyaa-view 빠른 시작 안내";
            this.ClientSize = new Size(580, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(16, 17, 20);
            this.ForeColor = Color.FromArgb(235, 238, 245);
            this.Font = new Font("맑은 고딕", 9.5F, FontStyle.Regular);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // 상단 헤더
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = Color.FromArgb(22, 24, 28),
                Padding = new Padding(20, 14, 20, 0)
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
                Height = 28,
                Font = new Font("맑은 고딕", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 255, 255)
            };
            Label subTitleLabel = new Label
            {
                Text = "편안한 감상을 위해 마우스 휠 동작 방식과 캐시 설정을 확인해 주세요.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(145, 150, 165)
            };
            headerPanel.Controls.Add(subTitleLabel);
            headerPanel.Controls.Add(titleLabel);

            // 하단 버튼 바
            Panel footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = Color.FromArgb(20, 21, 25),
                Padding = new Padding(20, 10, 20, 10)
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
                Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.FlatAppearance.MouseOverBackColor = Color.FromArgb(98, 145, 255);
            btnStart.Click += (s, e) => OnStartClicked();
            footerPanel.Controls.Add(btnStart);

            // 중앙 스크롤 컨텐츠
            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(20, 16, 20, 16)
            };

            // 1. 마우스 휠 동작 카드
            Panel wheelCard = CreateCardPanel(190);
            wheelCard.Dock = DockStyle.Top;

            Label wheelHeader = new Label
            {
                Text = "1. 마우스 휠 동작 방식 선택",
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 160, 255)
            };

            _rbWheelPageFlip = new RadioButton
            {
                Text = "페이지 넘기기 (이전 / 다음 장 이동) [만화책 및 웹툰 추천]",
                Location = new Point(14, 34),
                Size = new Size(520, 26),
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 238, 245),
                Cursor = Cursors.Hand
            };
            Label wheelPageDesc = new Label
            {
                Text = "마우스 휠을 굴려 만화책이나 웹툰을 다음 페이지로 빠르게 넘깁니다.",
                Location = new Point(34, 62),
                AutoSize = true,
                ForeColor = Color.FromArgb(135, 140, 155),
                Font = new Font("맑은 고딕", 8.5F, FontStyle.Regular)
            };

            _rbWheelZoom = new RadioButton
            {
                Text = "화면 확대 / 축소 (Zoom In / Zoom Out) [사진 및 그래픽 추천]",
                Location = new Point(14, 92),
                Size = new Size(520, 26),
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 238, 245),
                Cursor = Cursors.Hand
            };
            Label wheelZoomDesc = new Label
            {
                Text = "마우스 휠을 굴려 이미지를 확대하거나 축소합니다.",
                Location = new Point(34, 120),
                AutoSize = true,
                ForeColor = Color.FromArgb(135, 140, 155),
                Font = new Font("맑은 고딕", 8.5F, FontStyle.Regular)
            };

            Label wheelTip = new Label
            {
                Text = "Tip: 열람 중 단축키 [Z]로 언제든 즉시 전환 가능하며, Ctrl+휠로 반대 동작을 바로 수행합니다.",
                Location = new Point(14, 154),
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 125, 140),
                Font = new Font("맑은 고딕", 8.5F, FontStyle.Regular)
            };

            wheelCard.Controls.Add(wheelTip);
            wheelCard.Controls.Add(wheelZoomDesc);
            wheelCard.Controls.Add(_rbWheelZoom);
            wheelCard.Controls.Add(wheelPageDesc);
            wheelCard.Controls.Add(_rbWheelPageFlip);
            wheelCard.Controls.Add(wheelHeader);

            Panel gap1 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 2. 캐시 메모리 설정 카드
            int ramGb = SystemMemoryHelper.GetInstalledRamGb();
            _halfRamMb = SystemMemoryHelper.GetHalfPhysicalMemoryMb();

            Panel cacheCard = CreateCardPanel(205);
            cacheCard.Dock = DockStyle.Top;

            Label cacheHeader = new Label
            {
                Text = string.Format("2. 캐시 메모리 설정 (PC 감지 RAM: {0} GB)", ramGb > 0 ? ramGb.ToString() : "미상"),
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 160, 255)
            };

            Label cacheDesc = new Label
            {
                Text = "캐시가 충분할수록 대용량 웹툰 스크롤 시 끊김이 사라지고 부드럽게 감상할 수 있습니다.",
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = Color.FromArgb(135, 140, 155),
                Font = new Font("맑은 고딕", 8.5F, FontStyle.Regular)
            };

            _rbCache512 = new RadioButton
            {
                Text = "512 MB (가벼운 일반 이미지 열람)",
                Location = new Point(14, 56),
                Size = new Size(520, 24),
                ForeColor = Color.FromArgb(235, 238, 245),
                Cursor = Cursors.Hand
            };

            _rbCache1024 = new RadioButton
            {
                Text = "1024 MB (1 GB) [기본 추천 - 고해상도 만화 & 웹툰]",
                Location = new Point(14, 84),
                Size = new Size(520, 26),
                ForeColor = Color.FromArgb(235, 238, 245),
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            _rbCache2048 = new RadioButton
            {
                Text = "2048 MB (2 GB) [4K 초고화질 & 무한 연속 스크롤 웹툰 최적]",
                Location = new Point(14, 114),
                Size = new Size(520, 24),
                ForeColor = Color.FromArgb(235, 238, 245),
                Cursor = Cursors.Hand
            };

            string halfRamText = string.Format("시스템 메모리 권장 절반 ({0} MB - 최고 성능)", _halfRamMb);
            _rbCacheHalfRam = new RadioButton
            {
                Text = halfRamText,
                Location = new Point(14, 142),
                Size = new Size(520, 24),
                ForeColor = Color.FromArgb(235, 238, 245),
                Cursor = Cursors.Hand
            };

            cacheCard.Controls.Add(_rbCacheHalfRam);
            cacheCard.Controls.Add(_rbCache2048);
            cacheCard.Controls.Add(_rbCache1024);
            cacheCard.Controls.Add(_rbCache512);
            cacheCard.Controls.Add(cacheDesc);
            cacheCard.Controls.Add(cacheHeader);

            Panel gap2 = new Panel { Dock = DockStyle.Top, Height = 14 };

            // 3. 안내 카드 (F2 안내)
            Panel noticeCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.FromArgb(22, 28, 40),
                Padding = new Padding(14)
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
                Text = "상세한 설정 안내\n단축키 변경, 색상 필터(야간 반전/언샤프), 파일 확장자 연결 등 더욱 상세한 설정은\n프로그램 실행 후 언제든지 [F2] 키(환경 설정)에서 자유롭게 변경하실 수 있습니다.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 205, 255),
                Font = new Font("맑은 고딕", 9F, FontStyle.Regular)
            };
            noticeCard.Controls.Add(noticeLabel);

            contentPanel.Controls.Add(noticeCard);
            contentPanel.Controls.Add(gap2);
            contentPanel.Controls.Add(cacheCard);
            contentPanel.Controls.Add(gap1);
            contentPanel.Controls.Add(wheelCard);

            this.Controls.Add(contentPanel);
            this.Controls.Add(footerPanel);
            this.Controls.Add(headerPanel);
        }

        private Panel CreateCardPanel(int height)
        {
            Panel card = new Panel
            {
                Height = height,
                BackColor = Color.FromArgb(22, 24, 28),
                Padding = new Padding(14)
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
