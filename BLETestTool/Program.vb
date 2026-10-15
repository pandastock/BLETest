Imports System
Imports System.Threading
Imports System.Windows.Forms

Namespace BLETestTool
    Friend Module Program
        <STAThread>
        Public Sub Main()
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
            AddHandler Application.ThreadException, AddressOf OnAppException
            AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnDomainException
            Try
                Application.Run(New MainForm())
            Catch ex As Exception
                MessageBox.Show("启动异常：" & vbCrLf & ex.ToString(), "BLE调试工具",
                                MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Sub

        Private Sub OnAppException(sender As Object, e As ThreadExceptionEventArgs)
            MessageBox.Show("运行异常：" & vbCrLf & e.Exception.ToString(), "BLE调试工具",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Sub

        Private Sub OnDomainException(sender As Object, e As UnhandledExceptionEventArgs)
            Dim ex = TryCast(e.ExceptionObject, Exception)
            If ex IsNot Nothing Then
                MessageBox.Show("未处理异常：" & vbCrLf & ex.ToString(), "BLE调试工具",
                                MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        End Sub
    End Module
End Namespace
