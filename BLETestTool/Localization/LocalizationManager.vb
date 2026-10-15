Imports System
Imports System.Globalization
Imports System.Reflection
Imports System.Resources
Imports System.Threading

Namespace BLETestTool
    ''' <summary>本地化管理器：根据当前 UI 语言读取嵌入式资源。</summary>
    Public Class LocalizationManager
        Private Shared _rm As ResourceManager

        Public Shared Sub Switch(language As String)
            Thread.CurrentThread.CurrentUICulture = New CultureInfo(language)
            _rm = New ResourceManager("BLETestTool.Resources.Strings", Assembly.GetExecutingAssembly())
        End Sub

        Public Shared Function T(key As String) As String
            If _rm Is Nothing Then
                _rm = New ResourceManager("BLETestTool.Resources.Strings", Assembly.GetExecutingAssembly())
            End If
            Dim v = _rm.GetString(key, Thread.CurrentThread.CurrentUICulture)
            If String.IsNullOrEmpty(v) Then Return key
            Return v
        End Function
    End Class
End Namespace
