Imports System.Text
Imports Windows.Storage.Streams

Namespace BLETestTool
    Public Module HexUtils

        ''' <summary>字节数组转十六进制字符串。</summary>
        Public Function BytesToHex(bytes As Byte(), Optional withSpace As Boolean = False) As String
            If bytes Is Nothing OrElse bytes.Length = 0 Then Return ""
            Dim sb As New StringBuilder(bytes.Length * (2 + If(withSpace, 1, 0)))
            For i As Integer = 0 To bytes.Length - 1
                If withSpace AndAlso i > 0 Then sb.Append(" ")
                sb.Append(bytes(i).ToString("X2"))
            Next
            Return sb.ToString()
        End Function

        ''' <summary>十六进制字符串转字节数组（忽略空格、0x 前缀等）。</summary>
        Public Function HexToBytes(hex As String) As Byte()
            If String.IsNullOrEmpty(hex) Then Return New Byte() {}
            Dim cleaned As String = hex.Replace("0x", "").Replace("0X", "")
            Dim parts = cleaned.Split({" ", ",", "-", ":", vbTab, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
            Dim out As New List(Of Byte)
            For Each p In parts
                If p.Length Mod 2 <> 0 Then p = "0" & p
                For i As Integer = 0 To p.Length - 1 Step 2
                    out.Add(Convert.ToByte(p.Substring(i, 2), 16))
                Next
            Next
            Return out.ToArray()
        End Function

        ''' <summary>WinRT IBuffer 转字节数组。</summary>
        Public Function BufferToBytes(buffer As IBuffer) As Byte()
            If buffer Is Nothing OrElse buffer.Length = 0 Then Return New Byte() {}
            Using reader = DataReader.FromBuffer(buffer)
                Dim data(CInt(reader.UnconsumedBufferLength) - 1) As Byte
                reader.ReadBytes(data)
                Return data
            End Using
        End Function

        ''' <summary>字节数组转 WinRT IBuffer。</summary>
        Public Function BytesToBuffer(bytes As Byte()) As IBuffer
            Using writer As New DataWriter()
                writer.WriteBytes(bytes)
                Return writer.DetachBuffer()
            End Using
        End Function

        ''' <summary>BLE 广播地址（ulong，little-endian）转 MAC 字符串。</summary>
        Public Function MacToString(address As ULong) As String
            Dim b = BitConverter.GetBytes(address)
            Return String.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                                 b(5), b(4), b(3), b(2), b(1), b(0))
        End Function
    End Module
End Namespace
