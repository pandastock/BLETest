Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports Windows.Foundation
Imports Windows.Devices.Bluetooth
Imports Windows.Devices.Bluetooth.Advertisement
Imports Windows.Devices.Bluetooth.GenericAttributeProfile

Namespace BLETestTool
    Public Enum LogLevel
        Info
        Warn
        Fatal
    End Enum

    ''' <summary>BLE 服务层：扫描、连接、服务/特征值枚举（基于 Windows.Devices.Bluetooth WinRT API）。</summary>
    Public Class BleManager
        Public Event AdvertisementReceived(device As BleDeviceInfo)
        Public Event Log(message As String, level As LogLevel)

        Private _watcher As BluetoothLEAdvertisementWatcher

        Public ReadOnly Property IsScanning As Boolean
            Get
                Return _watcher IsNot Nothing
            End Get
        End Property

        Public Sub StartScan()
            If _watcher IsNot Nothing Then Return
            _watcher = New BluetoothLEAdvertisementWatcher()
            _watcher.ScanningMode = BluetoothLEScanningMode.Active
            AddHandler _watcher.Received, AddressOf OnReceived
            AddHandler _watcher.Stopped, AddressOf OnStopped
            _watcher.Start()
            RaiseEvent Log("Scan started", LogLevel.Info)
        End Sub

        Public Sub StopScan()
            If _watcher Is Nothing Then Return
            _watcher.Stop()
            RemoveHandler _watcher.Received, AddressOf OnReceived
            RemoveHandler _watcher.Stopped, AddressOf OnStopped
            _watcher = Nothing
            RaiseEvent Log("Scan stopped", LogLevel.Info)
        End Sub

        Private Sub OnReceived(sender As BluetoothLEAdvertisementWatcher, e As BluetoothLEAdvertisementReceivedEventArgs)
            Try
                Dim info As New BleDeviceInfo()
                info.Address = e.BluetoothAddress
                info.LocalName = e.Advertisement.LocalName
                info.Rssi = e.RawSignalStrengthInDBm
                info.AdvertisementType = CInt(e.AdvertisementType)

                For Each sec In e.Advertisement.DataSections
                    Dim ds As New AdDataSection()
                    ds.AdType = sec.DataType
                    ds.Data = HexUtils.BufferToBytes(sec.Data)
                    ds.Length = CByte(ds.Data.Length + 1)
                    ds.AdTypeName = AdDataSection.GetAdTypeName(ds.AdType)
                    If ds.AdType = &H01 Then info.Flags = AdDataSection.ParseFlags(ds.Data)
                    info.DataSections.Add(ds)
                Next

                ' 若广播未直接带 LocalName，则从 AD 数据段解析设备名（0x09 完整名 / 0x08 缩写名）
                If String.IsNullOrEmpty(info.LocalName) Then
                    For Each ds In info.DataSections
                        If ds.AdType = &H09 OrElse ds.AdType = &H08 Then
                            Try
                                Dim nm = System.Text.Encoding.UTF8.GetString(ds.Data)
                                If Not String.IsNullOrWhiteSpace(nm) Then
                                    info.LocalName = nm.Trim()
                                    Exit For
                                End If
                            Catch
                            End Try
                        End If
                    Next
                End If

                RaiseEvent AdvertisementReceived(info)
            Catch ex As Exception
                RaiseEvent Log("AD parse error: " & ex.Message, LogLevel.Fatal)
            End Try
        End Sub

        Private Sub OnStopped(sender As BluetoothLEAdvertisementWatcher, e As BluetoothLEAdvertisementWatcherStoppedEventArgs)
            ' 扫描停止回调（可扩展）
        End Sub

        Public Shared Async Function GetDeviceAsync(address As ULong) As Task(Of BluetoothLEDevice)
            Return Await BluetoothLEDevice.FromBluetoothAddressAsync(address)
        End Function

        ''' <summary>在后台线程等待一个 WinRT 异步操作，并带上超时，防止界面假死。超时/失败返回 Nothing。</summary>
        Public Shared Async Function AwaitOp(Of T As Class)(op As IAsyncOperation(Of T),
                                                            timeoutMs As Integer, tag As String) As Task(Of T)
            Try
                Dim opTask = Task.Run(Async Function() Await op)
                Dim winner = Await Task.WhenAny(opTask, Task.Delay(timeoutMs))
                If winner IsNot opTask Then
                    Return Nothing
                End If
                Return Await opTask
            Catch ex As Exception
                Return Nothing
            End Try
        End Function

        ''' <summary>枚举全部服务及特征值。</summary>
        Public Shared Async Function LoadServicesAsync(device As BluetoothLEDevice) As Task(Of List(Of BleServiceInfo))
            Dim result As New List(Of BleServiceInfo)()
            Dim svcResult = Await AwaitOp(device.GetGattServicesAsync(), 8000, "GetGattServices")
            If svcResult Is Nothing Then Return result
            For Each svc In svcResult.Services
                Dim s As New BleServiceInfo()
                s.Uuid = svc.Uuid
                s.Name = GattUuids.GetServiceName(svc.Uuid)
                s.Handle = svc.AttributeHandle

                Dim chResult = Await AwaitOp(svc.GetCharacteristicsAsync(), 5000, "GetCharacteristics")
                If chResult IsNot Nothing Then
                    For Each ch In chResult.Characteristics
                        Dim c As New BleCharacteristicInfo()
                        c.Uuid = ch.Uuid
                        c.Name = GattUuids.GetCharacteristicName(ch.Uuid)
                        c.Properties = ch.CharacteristicProperties
                        c.Handle = ch.AttributeHandle
                        c.GattChar = ch

                        Dim dResult = Await AwaitOp(ch.GetDescriptorsAsync(), 4000, "GetDescriptors")
                        If dResult IsNot Nothing Then
                            For Each d In dResult.Descriptors
                                Dim di As New BleDescriptorInfo()
                                di.Uuid = d.Uuid
                                di.Name = GattUuids.GetDescriptorName(d.Uuid)
                                di.Handle = d.AttributeHandle
                                c.Descriptors.Add(di)
                            Next
                        End If

                        s.Characteristics.Add(c)
                    Next
                End If
                result.Add(s)
            Next
            Return result
        End Function
    End Class
End Namespace
