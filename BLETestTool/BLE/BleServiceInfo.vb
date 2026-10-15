Imports System
Imports System.Collections.Generic

Namespace BLETestTool
    ''' <summary>GATT 服务信息。</summary>
    Public Class BleServiceInfo
        Public Property Uuid As Guid
        Public Property Name As String
        Public Property Handle As UShort
        Public Property Characteristics As New List(Of BleCharacteristicInfo)
    End Class
End Namespace
