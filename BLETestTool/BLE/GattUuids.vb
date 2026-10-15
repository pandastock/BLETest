Imports System
Imports System.Collections.Generic

Namespace BLETestTool
    ''' <summary>常用 BLE UUID → 友好名称映射。</summary>
    Public Module GattUuids
        Private ReadOnly _services As Dictionary(Of Guid, String) = BuildServiceMap()
        Private ReadOnly _chars As Dictionary(Of Guid, String) = BuildCharMap()

        ''' <summary>构造 16 位标准 BLE UUID（xxxx-0000-1000-8000-00805F9B34FB）。</summary>
        Public Function MakeUuid16(value As UShort) As Guid
            Return New Guid(CInt(value), CShort(0), CShort(&H1000),
                            &H80, &H00, &H00, &H80, &H5F, &H9B, &H34, &HFB)
        End Function

        Private Function BuildServiceMap() As Dictionary(Of Guid, String)
            Dim d As New Dictionary(Of Guid, String)
            d(MakeUuid16(&H1800)) = "GenericAccess"
            d(MakeUuid16(&H1801)) = "GenericAttribute"
            d(MakeUuid16(&HFFE0)) = "Unknown Service"
            Return d
        End Function

        Private Function BuildCharMap() As Dictionary(Of Guid, String)
            Dim d As New Dictionary(Of Guid, String)
            d(MakeUuid16(&HFFE1)) = "Unknown Characteristic"
            d(MakeUuid16(&HFFE2)) = "Unknown Characteristic"
            d(MakeUuid16(&HFFE3)) = "Unknown Characteristic"
            Return d
        End Function

        Public Function GetServiceName(uuid As Guid) As String
            If _services.ContainsKey(uuid) Then Return _services(uuid)
            Return "UUID " & ShortUuid(uuid)
        End Function

        Public Function GetCharacteristicName(uuid As Guid) As String
            If _chars.ContainsKey(uuid) Then Return _chars(uuid)
            Return "UUID " & ShortUuid(uuid)
        End Function

        ''' <summary>取 UUID 低 16 位短名称（如 0000FFE0 → FFE0）。</summary>
        Public Function ShortUuid(uuid As Guid) As String
            Dim b = uuid.ToByteArray()
            Dim v As Integer = CInt(b(0)) Or (CInt(b(1)) << 8)
            Return v.ToString("X4")
        End Function

        ''' <summary>是否为 16 位标准 BLE UUID（xxxx-0000-1000-8000-00805F9B34FB）。</summary>
        Public Function IsUuid16(uuid As Guid) As Boolean
            Dim b = uuid.ToByteArray()
            ' Guid.ToByteArray：字节0-3=Data1(LE)、4-5=Data2(LE)、6-7=Data3(LE)、8-15=尾部
            Return b(4) = &H00 AndAlso b(5) = &H00 AndAlso
                   b(6) = &H00 AndAlso b(7) = &H10 AndAlso
                   b(8) = &H80 AndAlso b(9) = &H00 AndAlso b(10) = &H00 AndAlso b(11) = &H80 AndAlso
                   b(12) = &H5F AndAlso b(13) = &H9B AndAlso b(14) = &H34 AndAlso b(15) = &HFB
        End Function

        ''' <summary>UUID 显示文本：16 位→0xXXXX；128 位→32 位无连字符大写十六进制。</summary>
        Public Function UuidDisplay(uuid As Guid) As String
            If IsUuid16(uuid) Then
                Dim b = uuid.ToByteArray()
                Dim v As Integer = CInt(b(0)) Or (CInt(b(1)) << 8)
                Return "0x" & v.ToString("X4")
            End If
            Return uuid.ToString("N").ToUpperInvariant()
        End Function

        ''' <summary>取 UUID 的 ATT 字节（16 位→2 字节小端；128 位→16 字节）。</summary>
        Public Function UuidBytes(uuid As Guid) As Byte()
            If IsUuid16(uuid) Then
                Dim b = uuid.ToByteArray()
                Return {b(0), b(1)}
            End If
            Return uuid.ToByteArray()
        End Function

        ''' <summary>描述符友好名称（如 0x2902 → Client Characteristic Configuration）。</summary>
        Public Function GetDescriptorName(uuid As Guid) As String
            If IsUuid16(uuid) Then
                Dim b = uuid.ToByteArray()
                Dim v As Integer = CInt(b(0)) Or (CInt(b(1)) << 8)
                Select Case v
                    Case &H2900 : Return "Characteristic Extended Properties"
                    Case &H2901 : Return "Characteristic User Description"
                    Case &H2902 : Return "Client Characteristic Configuration"
                    Case &H2903 : Return "Server Characteristic Configuration"
                    Case &H2904 : Return "Characteristic Presentation Format"
                    Case &H2905 : Return "Characteristic Aggregate Format"
                    Case &H2906 : Return "Valid Range"
                    Case Else : Return "Descriptor " & v.ToString("X4")
                End Select
            End If
            Return "Descriptor"
        End Function
    End Module
End Namespace
