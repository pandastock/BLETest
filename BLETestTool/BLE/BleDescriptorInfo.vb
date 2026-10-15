Imports System

Namespace BLETestTool
    ''' <summary>GATT 描述符信息（如客户端特征配置 0x2902）。</summary>
    Public Class BleDescriptorInfo
        Public Property Uuid As Guid
        Public Property Name As String
        Public Property Handle As UShort
    End Class
End Namespace
