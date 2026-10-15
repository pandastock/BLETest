Imports System
Imports System.Collections.Generic
Imports Windows.Devices.Bluetooth.GenericAttributeProfile

Namespace BLETestTool
    ''' <summary>GATT 特征值信息。</summary>
    Public Class BleCharacteristicInfo
        Public Property Uuid As Guid
        Public Property Name As String
        Public Property Properties As GattCharacteristicProperties
        Public Property Handle As UShort
        Public Property GattChar As GattCharacteristic
        Public Property Descriptors As New List(Of BleDescriptorInfo)

        Public Function PropertiesText() As String
            Dim parts As New List(Of String)
            If (Properties And GattCharacteristicProperties.Read) <> 0 Then parts.Add("Read")
            If (Properties And GattCharacteristicProperties.Write) <> 0 Then parts.Add("Write")
            If (Properties And GattCharacteristicProperties.WriteWithoutResponse) <> 0 Then parts.Add("WriteWithoutResponse")
            If (Properties And GattCharacteristicProperties.Notify) <> 0 Then parts.Add("Notify")
            If (Properties And GattCharacteristicProperties.Indicate) <> 0 Then parts.Add("Indicate")
            If parts.Count = 0 Then Return "None"
            Return String.Join(" ", parts)
        End Function
    End Class
End Namespace
