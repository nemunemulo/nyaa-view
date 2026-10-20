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
                    string msg = "고해상도 이미지 또는 대용량 만화/웹툰을 처리하는 중 메모리가 부족하거나 캐시 범위를 초과했습니다.\n\n" +
                                 "안정적인 열람을 위해 [환경 설정 (F2) > 캐시 & 성능]에서 캐시 메모리 할당량(MB)을 늘려주세요.\n\n" +
                                 "지금 바로 캐시 설정 창을 여시겠습니까?";

                    DialogResult dr = MessageBox.Show(msg, "nyaa-view 캐시 메모리 안내", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (dr == DialogResult.Yes && mainForm != null && !mainForm.IsDisposed)
                    {
                        mainForm.OpenSettingsDialog(3); // 3: 캐시 & 성능 탭
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
