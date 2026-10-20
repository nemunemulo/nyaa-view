using System;
using System.IO;
using System.Windows.Forms;

namespace NyaaView
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            MainForm mainForm = null;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                Exception ex = e.Exception;
                bool isMemory = (ex is OutOfMemoryException);

                if (isMemory)
                {
                    long availMb = SystemMemoryHelper.GetAvailablePhysicalMemoryMb();
                    bool isRealRamShortage = (availMb >= 0 && availMb < 250);

                    if (isRealRamShortage)
                    {
                        string msg = string.Format(
                            "시스템 가용 메모리가 부족하여 작업을 완료할 수 없습니다. (현재 가용 RAM: {0} MB)\n\n" +
                            "안정적인 열람을 위해 [환경 설정 (F2) > 캐시 & 성능]에서 캐시 메모리 할당량을 줄이거나(예: 512MB 이하), 실행 중인 다른 응용 프로그램을 종료하여 메모리를 확보해 주세요.\n\n" +
                            "지금 바로 캐시 설정 창을 여시겠습니까?",
                            availMb);

                        DialogResult dr = MessageBox.Show(msg, "nyaa-view 시스템 메모리 부족 안내", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (dr == DialogResult.Yes && mainForm != null && !mainForm.IsDisposed)
                        {
                            mainForm.OpenSettingsDialog(3); // 3: 캐시 & 성능 탭
                        }
                    }
                    else
                    {
                        string msg = "이미지 파일이 손상되었거나 지원되지 않는 비정상 규격(손상된 헤더, 비정상 픽셀 크기 등)입니다.\n\n" +
                                     "파일이 정상적인 이미지 규격인지 확인해 주세요.";
                        MessageBox.Show(msg, "nyaa-view 이미지 디코딩 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("오류가 발생했습니다: " + ex.Message, "nyaa-view 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Exception ex = e.ExceptionObject as Exception;
                string msg = ex != null ? ex.Message : "알 수 없는 시스템 예외";
                MessageBox.Show("치명적인 오류: " + msg, "nyaa-view 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 명령행 인자 검사 (확장자 연결 등록/해제)
            if (args != null && args.Length > 0)
            {
                string first = args[0].ToLowerInvariant();
                if (first == "/register" || first == "--register" || first == "/assoc")
                {
                    FileAssociation.RegisterAssociations(FileAssociation.GetAllSupportedExts());
                    MessageBox.Show("모든 지원 확장자(이미지, 만화 압축, PDF)가 nyaa-view에 성공적으로 연결되었습니다!", "nyaa-view 확장자 연결", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (first == "/unregister" || first == "--unregister" || first == "/unassoc")
                {
                    FileAssociation.UnregisterAssociations(FileAssociation.GetAllSupportedExts());
                    MessageBox.Show("nyaa-view 확장자 연결이 해제되었습니다.", "nyaa-view 확장자 연결 해제", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            mainForm = new MainForm();

            // 명령행 인자로 파일이 전달된 경우 (탐색기에서 "연결 프로그램" 또는 더블클릭으로 실행 시)
            if (args != null && args.Length > 0 && File.Exists(args[0]))
            {
                string target = args[0];
                mainForm.Shown += (s, e) =>
                {
                    if (MainForm.IsArchiveFile(target))
                    {
                        mainForm.LoadArchive(target, 0);
                    }
                    else if (MainForm.IsPdfFile(target))
                    {
                        mainForm.LoadPdf(target, 0);
                    }
                    else
                    {
                        mainForm.LoadSingleImage(target);
                    }
                };
            }

            Application.Run(mainForm);
        }
    }
}
