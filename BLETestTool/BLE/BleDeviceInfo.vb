Imports System
Imports System.Collections.Generic

Namespace BLETestTool
    ''' <summary>扫描到的 BLE 设备信息。</summary>
    Public Class BleDeviceInfo
        Public Property Address As ULong
        Public Property LocalName As String
        Public Property Rssi As Short
        Public Property AdvertisementType As Integer
        Public Property Flags As String
        Public Property DataSections As New List(Of AdDataSection)
        Public Property IsConnected As Boolean

        Public Function DisplayName() As String
            If Not String.IsNullOrEmpty(LocalName) Then Return LocalName
            Return HexUtils.MacToString(Address)
        End Function

        Public Function AdTypeName() As String
            Select Case AdvertisementType
                Case 0 : Return "Connectable/Undirected"
                Case 1 : Return "Connectable/Directed"
                Case 2 : Return "Scannable/Undirected"
                Case 3 : Return "Non-Connectable/Undirected"
                Case 4 : Return "ScanResponse"
                Case Else : Return AdvertisementType.ToString()
            End Select
        End Function
    End Class
End Namespace
