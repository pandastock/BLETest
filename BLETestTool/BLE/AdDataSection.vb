Imports System
Imports System.Collections.Generic

Namespace BLETestTool
    ''' <summary>一条广播数据段（Length / AD Type / Data）。</summary>
    Public Class AdDataSection
        Public Property Length As Byte
        Public Property AdType As Byte
        Public Property Data As Byte()
        Public Property AdTypeName As String

        Public Shared Function GetAdTypeName(adType As Byte) As String
            Select Case adType
                Case &H01 : Return "Flags"
                Case &H02 : Return "Incomplete 16-bit UUIDs"
                Case &H03 : Return "Complete 16-bit UUIDs"
                Case &H04 : Return "Incomplete 32-bit UUIDs"
                Case &H05 : Return "Complete 32-bit UUIDs"
                Case &H06 : Return "Incomplete 128-bit UUIDs"
                Case &H07 : Return "Complete 128-bit UUIDs"
                Case &H08 : Return "Short Local Name"
                Case &H09 : Return "Device Name"
                Case &H0A : Return "TX Power Level"
                Case &H16 : Return "Service Data - 16-bit UUID"
                Case &H20 : Return "Service Data - 32-bit UUID"
                Case &H21 : Return "Service Data - 128-bit UUID"
                Case &HFF : Return "Manufacturer Specific"
                Case Else : Return String.Format("0x{0:X2}", adType)
            End Select
        End Function

        ''' <summary>解析 Flags 段（0x01）。</summary>
        Public Shared Function ParseFlags(data As Byte()) As String
            If data Is Nothing OrElse data.Length = 0 Then Return "N/A"
            Dim parts As New List(Of String)
            Dim f As Byte = data(0)
            If (f And &H01) <> 0 Then parts.Add("LE Limited Discoverable Mode")
            If (f And &H02) <> 0 Then parts.Add("LE General Discoverable Mode")
            If (f And &H04) <> 0 Then parts.Add("BR/EDR Not Supported")
            If (f And &H08) <> 0 Then parts.Add("Simultaneous LE and BR/EDR")
            If parts.Count = 0 Then Return "0x" & f.ToString("X2")
            Return String.Join(", ", parts)
        End Function
    End Class
End Namespace
