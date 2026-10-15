Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Windows.Devices.Bluetooth
Imports Windows.Devices.Bluetooth.Advertisement
Imports Windows.Devices.Bluetooth.GenericAttributeProfile

Namespace BLETestTool
    Public Class MainForm
        Inherits Form

        ' ---- 状态 ----
        Private ReadOnly _ble As New BleManager()
        Private _rowMap As New Dictionary(Of ULong, Integer)
        Private _rows As New List(Of Integer)
        Private _totalScanned As Integer
        Private _pendingAds As New List(Of BleDeviceInfo)()
        Private _pendingLock As New Object()
        Private _refreshTimer As Timer
        Private _lastW As Integer = -1
        Private _lastH As Integer = -1
        Private _opsPanel As Panel
        Private _hNotify As Label
        Private _hRead As Label
        Private _hWrite As Label
        Private _connectedDev As BluetoothLEDevice
        Private _connectedInfo As BleDeviceInfo
        Private _selectedChar As BleCharacteristicInfo
        Private _selectedRow As Integer = -1
        Private _recvBytes As Long
        Private _recvLastBytes As Long
        Private _recvSpeed As Double
        Private _sendBytes As Long
        Private _sendLastBytes As Long
        Private _sendSpeed As Double
        Private _fileBytes As Byte()
        Private _uiMap As New Dictionary(Of Object, String)
        Private _gattServices As List(Of BleServiceInfo)
        Private _filterText As String = ""
        Private _rssiMin As Integer = -100

        ' ---- 控件 ----
        Private dgvDevices As DataGridView
        Private adGrid As DataGridView
        Private permGrid As DataGridView
        Private _adMenu As ContextMenuStrip
        Private dgvGatt As DataGridView
        Private cmbView As ComboBox
        Private lblTotalScanned As Label
        Private lblDevName As Label
        Private lblDevMac As Label
        Private lblDevConn As Label
        Private txtWrite As TextBox
        Private chkNotify As CheckBox
        Private chkHexDisplay As CheckBox
        Private chkHexSend As CheckBox
        Private btnRead As Button
        Private btnWriteNoResp As Button
        Private btnDisconnectConn As Button
        Private logList As ListBox
        Private btnScanner As Button
        Private _scanning As Boolean
        Private menuStrip As MenuStrip
        Private _timer As Timer
        Private _mainSplit As SplitContainer
        Private _broadcastSplit As SplitContainer
        Private _colSplit As SplitContainer
        Private _rightSplit As SplitContainer

        Public Sub New()
            LocalizationManager.Switch("zh-CN")
            BuildUi()
            WireBle()
            StartSpeedTimer()
            Log(LocalizationManager.T("AppTitle") & " - ready")
        End Sub

        ' ================= 界面构建 =================

        Private Const BUILD_TAG As String = " · b1014ax"

        Private Sub BuildUi()
            Me.Text = "散修测试"
            Try
                Using s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("BLETestTool.AppIcon.ico")
                    If s IsNot Nothing Then Me.Icon = New Icon(s)
                End Using
            Catch
            End Try
            Me.ClientSize = New Size(1160, 800)
            Me.StartPosition = FormStartPosition.CenterScreen
            Me.WindowState = FormWindowState.Maximized
            Me.MinimumSize = New Size(1024, 720)
            Me.Font = New Font("Microsoft YaHei UI", 9.0F)
            Me.BackColor = Color.FromArgb(238, 243, 249)

            ' ---- 整体布局：上(两列) + 广播数据横条 + 下(日志) ----
            ' 注意 Orientation 语义：Vertical=左右并排，Horizontal=上下堆叠
            ' _mainSplit 上下：Panel1=上(广播横条容器)，Panel2=下(日志) => Horizontal
            _mainSplit = New SplitContainer()
            _mainSplit.Dock = DockStyle.Fill
            _mainSplit.Orientation = Orientation.Horizontal
            _mainSplit.SplitterWidth = 6
            Me.Controls.Add(_mainSplit)

            ' _broadcastSplit 上下：Panel1=上(两列内容)，Panel2=下(广播数据通栏横条) => Horizontal
            _broadcastSplit = New SplitContainer()
            _broadcastSplit.Dock = DockStyle.Fill
            _broadcastSplit.Orientation = Orientation.Horizontal
            _broadcastSplit.SplitterWidth = 6
            _mainSplit.Panel1.Controls.Add(_broadcastSplit)

            ' _colSplit 左右：Panel1=左(设备列表)，Panel2=右(服务/特征值) => Vertical
            _colSplit = New SplitContainer()
            _colSplit.Dock = DockStyle.Fill
            _colSplit.Orientation = Orientation.Vertical
            _colSplit.SplitterWidth = 6
            _broadcastSplit.Panel1.Controls.Add(_colSplit)

            ' --- 左侧：设备列表 ---
            Dim pLeft = MakePanel(8, 32, 470, 430)
            DockInto(pLeft, _colSplit.Panel1)
            pLeft.BackColor = Color.FromArgb(250, 252, 255)
            Dim hLeft = MakeLabel(8, 6, 200, 20, pLeft, "DeviceList")
            hLeft.Font = New Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)

            btnScanner = MakeButton(8, 24, 130, 32, pLeft, "Scanner")
            AddHandler btnScanner.Click, AddressOf OnScannerClick
            Dim btnBonded = MakeButton(146, 24, 130, 32, pLeft, "Bonded")
            AddHandler btnBonded.Click, AddressOf OnBondedClick
            Dim btnFilter = MakeButton(284, 24, 130, 32, pLeft, "Filter")
            AddHandler btnFilter.Click, AddressOf OnFilterClick

            dgvDevices = New DataGridView()
            dgvDevices.Location = New Point(8, 68)
            dgvDevices.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
            dgvDevices.AutoGenerateColumns = False
            dgvDevices.AllowUserToAddRows = False
            dgvDevices.AllowUserToDeleteRows = False
            dgvDevices.AllowUserToResizeRows = False
            dgvDevices.RowHeadersVisible = False
            dgvDevices.ReadOnly = True
            dgvDevices.MultiSelect = False
            dgvDevices.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            dgvDevices.BackgroundColor = Color.White
            dgvDevices.BorderStyle = BorderStyle.FixedSingle
            dgvDevices.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            dgvDevices.ScrollBars = ScrollBars.Vertical
            dgvDevices.RowTemplate.Height = 48
            dgvDevices.EnableHeadersVisualStyles = False
            dgvDevices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            dgvDevices.ColumnHeadersHeight = 24
            dgvDevices.ColumnHeadersDefaultCellStyle = New DataGridViewCellStyle() With {
                .Font = New Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                .BackColor = Color.FromArgb(238, 242, 248),
                .Alignment = DataGridViewContentAlignment.MiddleLeft
            }
            dgvDevices.DefaultCellStyle = New DataGridViewCellStyle() With {
                .Font = New Font("Microsoft YaHei UI", 9F),
                .Alignment = DataGridViewContentAlignment.MiddleLeft
            }
            dgvDevices.Columns.Add("cName", LocalizationManager.T("DeviceName"))
            dgvDevices.Columns.Add("cMac", LocalizationManager.T("MacAddress"))
            dgvDevices.Columns.Add("cAddrType", LocalizationManager.T("AddrType"))
            dgvDevices.Columns.Add("cRssi", LocalizationManager.T("RSSI"))
            dgvDevices.Columns.Add("cAdType", LocalizationManager.T("BroadcastType"))
            Dim cConn As New DataGridViewButtonColumn()
            cConn.HeaderText = LocalizationManager.T("Connect")
            cConn.Name = "cConnect"
            cConn.Text = LocalizationManager.T("Connect")
            cConn.FlatStyle = FlatStyle.Flat
            cConn.Width = 58
            cConn.DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204)
            dgvDevices.Columns.Add(cConn)
            Dim cPair As New DataGridViewButtonColumn()
            cPair.HeaderText = LocalizationManager.T("Pair")
            cPair.Name = "cPair"
            cPair.Text = LocalizationManager.T("Pair")
            cPair.UseColumnTextForButtonValue = True
            cPair.FlatStyle = FlatStyle.Flat
            cPair.Width = 52
            cPair.DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204)
            dgvDevices.Columns.Add(cPair)
            Dim cClear As New DataGridViewButtonColumn()
            cClear.HeaderText = LocalizationManager.T("Clear")
            cClear.Name = "cClear"
            cClear.Text = LocalizationManager.T("Clear")
            cClear.UseColumnTextForButtonValue = True
            cClear.FlatStyle = FlatStyle.Flat
            cClear.Width = 52
            cClear.DefaultCellStyle.ForeColor = Color.FromArgb(0, 102, 204)
            dgvDevices.Columns.Add(cClear)
            dgvDevices.Columns("cName").AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            dgvDevices.Columns("cName").Width = 140
            dgvDevices.Columns("cMac").Width = 118
            dgvDevices.Columns("cAddrType").Width = 64
            dgvDevices.Columns("cRssi").Width = 50
            dgvDevices.Columns("cAdType").Width = 88
            AddHandler dgvDevices.CellContentClick, AddressOf OnDeviceGridCellClick
            AddHandler dgvDevices.SelectionChanged, AddressOf OnDeviceGridSelectionChanged
            pLeft.Controls.Add(dgvDevices)

            ' --- 广播数据（通栏横条，放在日志上方）---
            Dim pBroadcast = MakePanel(8, 0, 1144, 160)
            DockInto(pBroadcast, _broadcastSplit.Panel2)
            pBroadcast.BackColor = Color.FromArgb(250, 252, 255)
            Dim hMid = MakeLabel(8, 4, 200, 20, pBroadcast, "Advertisement")
            hMid.Font = New Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)

            ' 广播数据（左）与特征权限状态（右）：横向分栏
            Dim adSplit = New SplitContainer()
            adSplit.Location = New Point(8, 28)
            adSplit.Size = New Size(1128, 204)
            adSplit.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
            adSplit.Orientation = Orientation.Vertical
            adSplit.SplitterWidth = 6
            adSplit.FixedPanel = FixedPanel.Panel2
            adSplit.Panel2MinSize = 250
            pBroadcast.Controls.Add(adSplit)
            pBroadcast.Controls.SetChildIndex(adSplit, 0)
            adSplit.SplitterDistance = Math.Max(300, adSplit.Width - 266)

            adGrid = New DataGridView()
            adGrid.Dock = DockStyle.Fill
            adGrid.RowHeadersVisible = False
            adGrid.AllowUserToAddRows = False
            adGrid.AllowUserToDeleteRows = False
            adGrid.AllowUserToResizeRows = False
            adGrid.ReadOnly = True
            adGrid.BackgroundColor = SystemColors.Window
            adGrid.BorderStyle = BorderStyle.None
            adGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            adGrid.ColumnHeadersHeight = 24
            adGrid.Columns.Add("cLen", LocalizationManager.T("Length"))
            adGrid.Columns.Add("cType", LocalizationManager.T("AdType"))
            adGrid.Columns.Add("cData", LocalizationManager.T("Data"))
            adGrid.Columns("cLen").Width = 64
            adGrid.Columns("cType").Width = 120
            adGrid.Columns("cData").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            ' 广播数据表右键菜单：复制选中的内容
            _adMenu = New ContextMenuStrip()
            Dim miCopy As New ToolStripMenuItem(LocalizationManager.T("Copy"))
            _uiMap(miCopy) = "Copy"
            AddHandler miCopy.Click, Sub(s, e) CopySelectedAdCells()
            _adMenu.Items.Add(miCopy)
            adGrid.ContextMenuStrip = _adMenu
            adSplit.Panel1.Controls.Add(adGrid)

            ' 右侧：特征权限状态表（点击 Characteristic 时刷新）
            permGrid = New DataGridView()
            permGrid.Dock = DockStyle.Fill
            permGrid.RowHeadersVisible = False
            permGrid.AllowUserToAddRows = False
            permGrid.AllowUserToDeleteRows = False
            permGrid.AllowUserToResizeRows = False
            permGrid.ReadOnly = True
            permGrid.BackgroundColor = Color.White
            permGrid.BorderStyle = BorderStyle.None
            permGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            permGrid.ColumnHeadersHeight = 24
            permGrid.RowTemplate.Height = 20
            permGrid.Columns.Add("cPerm", LocalizationManager.T("PermName"))
            Dim chkCol As New DataGridViewCheckBoxColumn()
            chkCol.Name = "cEnabled"
            chkCol.HeaderText = LocalizationManager.T("PermEnabled")
            chkCol.Width = 70
            permGrid.Columns.Add(chkCol)
            permGrid.Columns("cPerm").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            AddPermRows()
            adSplit.Panel2.Controls.Add(permGrid)

            lblTotalScanned = MakeLabel(8, 134, 500, 20, pBroadcast)
            lblTotalScanned.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left

            ' --- 右侧：服务/特征值 ---
            Dim pRight = MakePanel(486, 32, 650, 430)
            DockInto(pRight, _colSplit.Panel2)
            pRight.BackColor = Color.FromArgb(250, 252, 255)
            Dim hRight = MakeLabel(8, 6, 200, 20, pRight, "Services")
            hRight.Font = New Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)

            lblDevName = MakeLabel(8, 28, 230, 18, pRight)
            lblDevName.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
            lblDevMac = MakeLabel(8, 56, 230, 18, pRight)
            lblDevMac.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
            lblDevConn = MakeLabel(8, 84, 110, 18, pRight)
            btnDisconnectConn = MakeButton(120, 82, 110, 24, pRight, "DisconnectConn")
            AddHandler btnDisconnectConn.Click, AddressOf OnDisconnectConnClick

            ' 右列上下分栏：上=Notifications(可滚动)，下=Service树(可滚动)，中间可拖动
            _rightSplit = New SplitContainer()
            _rightSplit.Orientation = Orientation.Horizontal
            _rightSplit.SplitterWidth = 6
            _rightSplit.Location = New Point(0, 112)
            _rightSplit.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
            _rightSplit.Panel1MinSize = 160
            _rightSplit.Panel2MinSize = 40
            pRight.Controls.Add(_rightSplit)

            ' Panel1：Notifications 操作面板（通栏满宽、可滚动）
            _opsPanel = New Panel()
            _opsPanel.Dock = DockStyle.Fill
            _opsPanel.AutoScroll = True
            _opsPanel.BackColor = Color.FromArgb(250, 252, 255)
            _rightSplit.Panel1.Controls.Add(_opsPanel)
            AddHandler _opsPanel.Resize, AddressOf OnOpsResize
            _hNotify = MakeLabel(4, 2, 304, 18, _opsPanel, "Notifications")
            _hNotify.Font = New Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            chkNotify = MakeCheckBox(4, 22, 140, 18, _opsPanel, "Notify")
            AddHandler chkNotify.CheckedChanged, AddressOf OnNotifyCheckedChanged

            ' --- Read（通栏满宽行）---
            _hRead = MakeLabel(4, 46, 80, 18, _opsPanel, "Read")
            _hRead.Font = New Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            chkHexDisplay = MakeCheckBox(88, 46, 120, 18, _opsPanel, "HexDisplay")
            chkHexDisplay.TextAlign = ContentAlignment.MiddleLeft
            btnRead = MakeButton(4, 66, 88, 26, _opsPanel, "ReadBtn")
            AddHandler btnRead.Click, AddressOf OnReadClick

            ' --- Write（通栏满宽行）---
            _hWrite = MakeLabel(4, 96, 80, 18, _opsPanel, "Write")
            _hWrite.Font = New Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            chkHexSend = MakeCheckBox(88, 96, 120, 18, _opsPanel, "HexSend")
            chkHexSend.TextAlign = ContentAlignment.MiddleLeft
            txtWrite = New TextBox()
            txtWrite.Location = New Point(4, 136)
            txtWrite.Size = New Size(320, 26)
            txtWrite.Multiline = True
            txtWrite.ScrollBars = ScrollBars.Vertical
            txtWrite.Font = New Font("Consolas", 9F)
            _opsPanel.Controls.Add(txtWrite)
            ' 写入按钮与上方文本框拉开间隔
            btnWriteNoResp = MakeButton(4, 168, 96, 26, _opsPanel, "WriteNoResponse")
            AddHandler btnWriteNoResp.Click, AddressOf OnWriteNoRespClick

            ' Panel2：Service 属性表（视图下拉 + 分类/简单 双模式）
            Dim pService = New Panel()
            pService.Dock = DockStyle.Fill
            pService.BackColor = Color.FromArgb(250, 252, 255)
            _rightSplit.Panel2.Controls.Add(pService)
            Dim lblView = MakeLabel(8, 7, 40, 18, pService, "View")
            cmbView = New ComboBox()
            cmbView.DropDownStyle = ComboBoxStyle.DropDownList
            cmbView.Items.AddRange(New Object() {"Categorized", "Simple"})
            cmbView.Location = New Point(48, 4)
            cmbView.Size = New Size(150, 22)
            cmbView.Font = New Font("Microsoft YaHei UI", 9F)
            cmbView.SelectedIndex = 0
            AddHandler cmbView.SelectedIndexChanged, AddressOf OnViewChanged
            pService.Controls.Add(cmbView)

            dgvGatt = New DataGridView()
            dgvGatt.Location = New Point(8, 30)
            dgvGatt.Size = New Size(460, 200)
            dgvGatt.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
            dgvGatt.ReadOnly = True
            dgvGatt.AllowUserToAddRows = False
            dgvGatt.AllowUserToResizeRows = False
            dgvGatt.RowHeadersVisible = False
            dgvGatt.MultiSelect = False
            dgvGatt.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            dgvGatt.BorderStyle = BorderStyle.FixedSingle
            dgvGatt.BackgroundColor = Color.White
            dgvGatt.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            dgvGatt.Columns.Add("cHandle", "Handle")
            dgvGatt.Columns.Add("cUuid", "UUID")
            dgvGatt.Columns.Add("cDesc", "UUID Description")
            dgvGatt.Columns.Add("cValue", "Value")
            dgvGatt.Columns.Add("cProps", "Properties")
            dgvGatt.Columns("cHandle").MinimumWidth = 70
            dgvGatt.Columns("cHandle").FillWeight = 12
            dgvGatt.Columns("cUuid").MinimumWidth = 90
            dgvGatt.Columns("cUuid").FillWeight = 15
            dgvGatt.Columns("cDesc").FillWeight = 34
            dgvGatt.Columns("cValue").FillWeight = 32
            dgvGatt.Columns("cProps").FillWeight = 7
            AddHandler dgvGatt.CellClick, AddressOf OnGattCellClick
            pService.Controls.Add(dgvGatt)

            ' --- 底部：日志 ---
            Dim pLog = MakePanel(8, 470, 1144, 170)
            DockInto(pLog, _mainSplit.Panel2)
            pLog.BackColor = Color.FromArgb(250, 252, 255)
            Dim hLog = MakeLabel(4, 2, 80, 20, pLog, "Log")
            hLog.Font = New Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
            logList = New ListBox()
            logList.Location = New Point(4, 24)
            logList.Size = New Size(1136, 142)
            logList.BorderStyle = BorderStyle.None
            logList.HorizontalScrollbar = True
            logList.Font = New Font("Consolas", 8.5F)
            pLog.Controls.Add(logList)

            BuildMenu()
            ApplyLanguage()
            ResetOps()
        End Sub

        Private Sub BuildMenu()
            menuStrip = New MenuStrip()
            menuStrip.Dock = DockStyle.Top
            menuStrip.Font = New Font("Microsoft YaHei UI", 9.5F)

            Dim mDevice = MakeMenuItem(menuStrip.Items, "MenuDeviceList")
            Dim itScan = MakeMenuItem(mDevice.DropDownItems, "Scanner")
            Dim itStop = MakeMenuItem(mDevice.DropDownItems, "StopScan")
            AddHandler itScan.Click, AddressOf OnScannerClick
            AddHandler itStop.Click, AddressOf OnStopScanClick

            Dim mLang = MakeMenuItem(menuStrip.Items, "MenuLanguage")
            AddLanguageSubmenu(mLang.DropDownItems)

            Dim mHelp = MakeMenuItem(menuStrip.Items, "MenuHelp")
            Dim itAbout = MakeMenuItem(mHelp.DropDownItems, "About")
            AddHandler itAbout.Click, AddressOf OnAboutClick

            Me.MainMenuStrip = menuStrip
            Me.Controls.Add(menuStrip)
            menuStrip.BringToFront()
        End Sub

        Private Sub AddLanguageSubmenu(items As ToolStripItemCollection)
            Dim itZh = MakeMenuItem(items, "LanguageZh")
            Dim itEn = MakeMenuItem(items, "LanguageEn")
            itZh.Tag = "zh-CN"
            itEn.Tag = "en-US"
            AddHandler itZh.Click, AddressOf OnLanguageClick
            AddHandler itEn.Click, AddressOf OnLanguageClick
        End Sub

        ' ================= 控件工厂 =================

        Private Sub DockInto(c As Control, target As Control)
            If Me.Controls.Contains(c) Then Me.Controls.Remove(c)
            target.Controls.Add(c)
            c.Dock = DockStyle.Fill
        End Sub

        Private Sub PlaceInto(c As Control, target As Control)
            If Me.Controls.Contains(c) Then Me.Controls.Remove(c)
            target.Controls.Add(c)
        End Sub

        Private Function MakePanel(x As Integer, y As Integer, w As Integer, h As Integer) As Panel
            Dim p As New Panel()
            p.Location = New Point(x, y)
            p.Size = New Size(w, h)
            p.BorderStyle = BorderStyle.FixedSingle
            p.BackColor = Color.White
            Me.Controls.Add(p)
            Return p
        End Function

        Private Function MakeLabel(x As Integer, y As Integer, w As Integer, h As Integer,
                                   parent As Control, Optional key As String = Nothing) As Label
            Dim l As New Label()
            l.Location = New Point(x, y)
            l.Size = New Size(w, h)
            l.AutoEllipsis = True
            l.Font = New Font("Microsoft YaHei UI", 9F)
            If key IsNot Nothing Then
                l.Text = LocalizationManager.T(key)
                _uiMap(l) = key
            End If
            parent.Controls.Add(l)
            Return l
        End Function

        Private Function MakeButton(x As Integer, y As Integer, w As Integer, h As Integer,
                                    parent As Control, Optional key As String = Nothing) As Button
            Dim b As New Button()
            b.Location = New Point(x, y)
            b.Size = New Size(w, h)
            b.Font = New Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            b.FlatStyle = FlatStyle.Flat
            b.BackColor = Color.FromArgb(0, 102, 204)
            b.ForeColor = Color.White
            b.FlatAppearance.BorderColor = Color.FromArgb(0, 102, 204)
            If key IsNot Nothing Then
                b.Text = LocalizationManager.T(key)
                _uiMap(b) = key
            End If
            parent.Controls.Add(b)
            Return b
        End Function

        Private Function MakeCheckBox(x As Integer, y As Integer, w As Integer, h As Integer,
                                      parent As Control, Optional key As String = Nothing) As CheckBox
            Dim c As New CheckBox()
            c.Location = New Point(x, y)
            c.Size = New Size(w, h)
            c.Font = New Font("Microsoft YaHei UI", 9F)
            If key IsNot Nothing Then
                c.Text = LocalizationManager.T(key)
                _uiMap(c) = key
            End If
            parent.Controls.Add(c)
            Return c
        End Function

        Private Function MakeMenuItem(owner As ToolStripItemCollection, key As String) As ToolStripMenuItem
            Dim mi As New ToolStripMenuItem(LocalizationManager.T(key))
            _uiMap(mi) = key
            owner.Add(mi)
            Return mi
        End Function

        ' ================= 本地化 =================

        Private Sub ApplyLanguage()
            For Each kv In _uiMap
                SetText(kv.Key, LocalizationManager.T(kv.Value))
            Next
            Me.Text = "散修测试"
            adGrid.Columns("cLen").HeaderText = LocalizationManager.T("Length")
            adGrid.Columns("cType").HeaderText = LocalizationManager.T("AdType")
            adGrid.Columns("cData").HeaderText = LocalizationManager.T("Data")
            lblTotalScanned.Text = String.Format(LocalizationManager.T("TotalScanned"), _totalScanned.ToString("D3"))
            For Each idx In _rows
                If idx >= 0 AndAlso idx < dgvDevices.Rows.Count AndAlso dgvDevices.Rows(idx).Tag IsNot Nothing Then
                    UpdateRowCells(dgvDevices.Rows(idx), DirectCast(dgvDevices.Rows(idx).Tag, BleDeviceInfo))
                End If
            Next
            UpdateConnectedLabels()
            UpdateRecvStats()
            UpdateSendStats()
            If cmbView IsNot Nothing Then
                Dim si = cmbView.SelectedIndex
                cmbView.Items.Clear()
                cmbView.Items.AddRange(New Object() {LocalizationManager.T("Categorized"), LocalizationManager.T("Simple")})
                If si >= 0 AndAlso si < cmbView.Items.Count Then cmbView.SelectedIndex = si
            End If
            If _selectedRow >= 0 AndAlso _selectedRow < dgvDevices.Rows.Count AndAlso dgvDevices.Rows(_selectedRow).Tag IsNot Nothing Then
                RefreshAdFor(DirectCast(dgvDevices.Rows(_selectedRow).Tag, BleDeviceInfo))
            End If
        End Sub

        Private Sub SetText(target As Object, text As String)
            If target Is Nothing Then Return
            If TypeOf target Is Control Then
                Dim c = DirectCast(target, Control)
                If Not c.IsDisposed Then c.Text = text
            ElseIf TypeOf target Is ToolStripItem Then
                Dim t = DirectCast(target, ToolStripItem)
                If Not t.IsDisposed Then t.Text = text
            End If
        End Sub

        ' ================= BLE 事件 =================

        Private Sub WireBle()
            AddHandler _ble.AdvertisementReceived, AddressOf OnAdvertisementReceived
            AddHandler _ble.Log, AddressOf OnBleLog
        End Sub

        Private Sub OnBleLog(message As String, level As LogLevel)
            Log(message, level)
        End Sub

        Private Sub OnAdvertisementReceived(device As BleDeviceInfo)
            ' 只在后台线程收集，统一由定时器合并刷新，避免扫描时 UI 事件风暴卡死
            SyncLock _pendingLock
                _pendingAds.Add(device)
            End SyncLock
        End Sub

        ''' <summary>把一条广播数据应用到界面（在 UI 线程执行）。</summary>
        Private Sub ApplyAdToUi(device As BleDeviceInfo)
            If _rowMap.ContainsKey(device.Address) Then
                Dim idx = _rowMap(device.Address)
                If idx >= 0 AndAlso idx < dgvDevices.Rows.Count Then
                    Dim row = dgvDevices.Rows(idx)
                    Dim info As BleDeviceInfo = DirectCast(row.Tag, BleDeviceInfo)
                    info.Rssi = device.Rssi
                    ' 仅在拿到非空名称时才覆盖，避免设备在部分广播包没带名称时名字回退成 MAC
                    If Not String.IsNullOrEmpty(device.LocalName) Then info.LocalName = device.LocalName
                    info.Flags = device.Flags
                    info.DataSections = device.DataSections
                    UpdateRowCells(row, info)
                    If _selectedRow = idx Then RefreshAdFor(info)
                End If
            Else
                _totalScanned += 1
                lblTotalScanned.Text = String.Format(LocalizationManager.T("TotalScanned"), _totalScanned.ToString("D3"))
                Dim idx = AddDeviceRow(device)
                _rowMap(device.Address) = idx
                _rows.Add(idx)
            End If
        End Sub

        ''' <summary>点击“过滤”：弹出对话框输入设备名称/MAC 并按 RSSI 范围过滤设备列表；留空显示全部。</summary>
        Private Sub OnFilterClick(sender As Object, e As EventArgs)
            Dim dlg As New Form()
            dlg.Text = LocalizationManager.T("Filter")
            dlg.Icon = Me.Icon
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog
            dlg.Size = New Size(480, 220)
            dlg.MaximizeBox = False
            dlg.MinimizeBox = False
            dlg.ShowInTaskbar = False
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.BackColor = Color.White

            ' 搜索图标 + 名称/MAC 输入框
            Dim pic As New PictureBox()
            pic.Location = New Point(18, 20)
            pic.Size = New Size(22, 22)
            pic.SizeMode = PictureBoxSizeMode.Zoom
            pic.BackColor = Color.FromArgb(250, 246, 238)
            pic.Image = MakeSearchIcon(22)
            dlg.Controls.Add(pic)
            Dim txt As New TextBox()
            txt.Location = New Point(46, 22)
            txt.Size = New Size(410, 24)
            txt.Font = New Font("Microsoft YaHei UI", 9F)
            txt.Text = _filterText
            dlg.Controls.Add(txt)

            ' RSSI 过滤：信号图标 + 自绘滚动条选择阈值（-100 ~ -40 dBm），始终生效
            Dim picRssi As New PictureBox()
            picRssi.Location = New Point(16, 78)
            picRssi.Size = New Size(24, 24)
            picRssi.SizeMode = PictureBoxSizeMode.Zoom
            picRssi.BackColor = Color.White
            picRssi.Image = MakeSignalIcon(24)
            dlg.Controls.Add(picRssi)
            Dim bar As New RssiBar()
            bar.Location = New Point(46, 78)
            bar.Size = New Size(300, 30)
            bar.Minimum = -100
            bar.Maximum = -40
            bar.Value = Math.Max(-100, Math.Min(-40, _rssiMin))
            dlg.Controls.Add(bar)
            Dim lblRssi As New Label()
            lblRssi.Text = "RSSI: " & bar.Value.ToString() & " dBm"
            lblRssi.Location = New Point(356, 82)
            lblRssi.Size = New Size(110, 20)
            lblRssi.Font = New Font("Microsoft YaHei UI", 9F)
            dlg.Controls.Add(lblRssi)
            AddHandler bar.ValueChanged, Sub(s, ev)
                lblRssi.Text = "RSSI: " & bar.Value.ToString() & " dBm"
            End Sub

            Dim btnOk As New Button()
            btnOk.Text = LocalizationManager.T("FilterApply")
            btnOk.Location = New Point(238, 140)
            btnOk.Size = New Size(90, 30)
            btnOk.DialogResult = DialogResult.OK
            dlg.Controls.Add(btnOk)
            Dim btnCancel As New Button()
            btnCancel.Text = LocalizationManager.T("FilterCancel")
            btnCancel.Location = New Point(336, 140)
            btnCancel.Size = New Size(90, 30)
            btnCancel.DialogResult = DialogResult.Cancel
            dlg.Controls.Add(btnCancel)
            dlg.AcceptButton = btnOk
            dlg.CancelButton = btnCancel

            If dlg.ShowDialog(Me) = DialogResult.OK Then
                _filterText = txt.Text.Trim()
                _rssiMin = bar.Value
                ApplyDeviceFilter()
            End If
            dlg.Dispose()
        End Sub

        ''' <summary>绘制放大镜搜索图标（无外部图片依赖，图标完全位于位图内不裁剪）。</summary>
        Private Function MakeSearchIcon(size As Integer) As Bitmap
            Dim bmp As New Bitmap(size, size)
            Using g As Graphics = Graphics.FromImage(bmp)
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.Clear(Color.Transparent)
                Dim pen As New Pen(Color.FromArgb(78, 86, 96), Math.Max(2, CInt(size * 0.14)))
                ' 镜片圆：完整落在位图内
                Dim r = CInt(size * 0.30)
                Dim cx = CInt(size * 0.40)
                Dim cy = CInt(size * 0.40)
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2)
                ' 手柄：从镜片右下沿向坐下角方向，完全在界内
                pen.StartCap = LineCap.Round
                pen.EndCap = LineCap.Round
                g.DrawLine(pen, cx + CInt(r * 0.75), cy + CInt(r * 0.75), CInt(size * 0.80), CInt(size * 0.80))
                pen.Dispose()
            End Using
            Return bmp
        End Function

        ''' <summary>绘制信号/广播图标：实心扇形（底部顶点向上展开，上宽下窄），深灰填充 + 浅灰描边，对应参考图。</summary>
        Private Function MakeSignalIcon(size As Integer) As Bitmap
            Dim bmp As New Bitmap(size, size)
            Using g As Graphics = Graphics.FromImage(bmp)
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.Clear(Color.Transparent)
                Dim ox = CInt(size * 0.5)
                Dim oy = CInt(size * 0.86)
                Dim r = CInt(size * 0.42)
                Dim rect = New Rectangle(ox - r, oy - r, r * 2, r * 2)
                ' 上宽下窄的扇形楔块（230°~310°，顶点朝下）
                Using b As New SolidBrush(Color.FromArgb(76, 82, 92))
                    g.FillPie(b, rect, 230.0F, 80.0F)
                End Using
                ' 浅灰描边
                Using p As New Pen(Color.FromArgb(152, 160, 170), Math.Max(2, CInt(size * 0.06)))
                    g.DrawPie(p, rect, 230.0F, 80.0F)
                End Using
            End Using
            Return bmp
        End Function

        ''' <summary>按 _filterText 与 RSSI 阈值过滤设备列表：匹配设备名称或 MAC（不区分大小写）且 RSSI >= 所选阈值；无过滤则全部显示。</summary>
        Private Sub ApplyDeviceFilter()
            If dgvDevices Is Nothing Then Return
            For Each row As DataGridViewRow In dgvDevices.Rows
                If row.Tag Is Nothing Then Continue For
                Dim info As BleDeviceInfo = DirectCast(row.Tag, BleDeviceInfo)
                If String.IsNullOrEmpty(_filterText) AndAlso _rssiMin <= -100 Then
                    row.Visible = True
                    Continue For
                End If
                Dim nameMatch = String.IsNullOrEmpty(_filterText) OrElse
                                info.DisplayName().IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                HexUtils.MacToString(info.Address).IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0
                Dim rssiOk = info.Rssi >= _rssiMin
                row.Visible = nameMatch AndAlso rssiOk
            Next
        End Sub

        ''' <summary>定时合并刷新：每 200ms 把积压的广播数据一次性刷到界面。</summary>
        Private Sub OnRefreshTimerTick(sender As Object, e As EventArgs)
            Dim batch As List(Of BleDeviceInfo)
            SyncLock _pendingLock
                batch = _pendingAds
                _pendingAds = New List(Of BleDeviceInfo)()
            End SyncLock
            If batch.Count = 0 Then Return
            For Each device In batch
                ApplyAdToUi(device)
            Next
            If Not String.IsNullOrEmpty(_filterText) OrElse _rssiMin > -100 Then ApplyDeviceFilter()
        End Sub

        ''' <summary>停止扫描时立即把剩余数据刷完。</summary>
        Private Sub FlushPending()
            OnRefreshTimerTick(Nothing, EventArgs.Empty)
        End Sub

        ''' <summary>Notifications 面板宽度随窗体变化时，让写入框与标题随宽度铺满（留右缘边距）。</summary>
        Private Sub OnOpsResize()
            Dim w = _opsPanel.ClientSize.Width
            If w <= 40 Then Return
            Dim fw = Math.Max(120, w - 16)
            _hNotify.Width = fw
            txtWrite.Width = fw
        End Sub

        ' ================= 设备行 =================

        Private Function AddDeviceRow(info As BleDeviceInfo) As Integer
            Dim idx = dgvDevices.Rows.Add()
            Dim row = dgvDevices.Rows(idx)
            row.Tag = info
            UpdateRowCells(row, info)
            Return idx
        End Function

        Private Sub UpdateRowCells(row As DataGridViewRow, info As BleDeviceInfo)
            row.Cells("cName").Value = info.DisplayName()
            row.Cells("cMac").Value = HexUtils.MacToString(info.Address)
            row.Cells("cAddrType").Value = AddressTypeText(info.Address)
            row.Cells("cRssi").Value = info.Rssi.ToString() & " " & LocalizationManager.T("DBm")
            row.Cells("cAdType").Value = info.AdTypeName()
            Dim btn As DataGridViewButtonCell = TryCast(row.Cells("cConnect"), DataGridViewButtonCell)
            If btn IsNot Nothing Then btn.Value = If(info.IsConnected, LocalizationManager.T("Disconnect"), LocalizationManager.T("Connect"))
        End Sub

        ''' <summary>由地址最高字节推断地址类型：最高两位非 00 即为随机地址（Random），否则 Public。</summary>
        Private Function AddressTypeText(address As ULong) As String
            Dim top As ULong = (address >> 40) And &HFFUL
            If (top And &HC0UL) <> 0UL Then
                Return LocalizationManager.T("Random")
            End If
            Return LocalizationManager.T("Public")
        End Function

        Private Sub SelectDevice(info As BleDeviceInfo, rowIndex As Integer)
            If rowIndex >= 0 AndAlso rowIndex < dgvDevices.Rows.Count Then
                dgvDevices.ClearSelection()
                dgvDevices.Rows(rowIndex).Selected = True
            End If
            _selectedRow = rowIndex
            RefreshAdFor(info)
        End Sub

        Private Sub OnDeviceGridCellClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Dim row = dgvDevices.Rows(e.RowIndex)
            If row.Tag Is Nothing Then Return
            Dim info As BleDeviceInfo = DirectCast(row.Tag, BleDeviceInfo)
            If e.ColumnIndex = dgvDevices.Columns("cConnect").Index Then
                If info.IsConnected Then OnDisconnectDevice(info) Else OnConnectDevice(info)
            ElseIf e.ColumnIndex = dgvDevices.Columns("cPair").Index Then
                OnPairDevice(info)
            ElseIf e.ColumnIndex = dgvDevices.Columns("cClear").Index Then
                OnClearDevice(info)
            End If
        End Sub

        Private Sub OnDeviceGridSelectionChanged(sender As Object, e As EventArgs)
            If dgvDevices.SelectedRows.Count = 1 Then
                Dim row = dgvDevices.SelectedRows(0)
                If row.Tag IsNot Nothing AndAlso _selectedRow <> row.Index Then
                    _selectedRow = row.Index
                    RefreshAdFor(DirectCast(row.Tag, BleDeviceInfo))
                End If
            End If
        End Sub

        Private Sub RefreshAdFor(info As BleDeviceInfo)
            adGrid.Rows.Clear()
            For Each ds In info.DataSections
                adGrid.Rows.Add(ds.Length, ds.AdTypeName, HexUtils.BytesToHex(ds.Data, True))
            Next
        End Sub

        ' ================= 设备操作 =================

        Private Async Sub OnConnectDevice(info As BleDeviceInfo)
            Try
                Log(LocalizationManager.T("LogConnected"), LogLevel.Info, GetDeviceTag(info))
                If _connectedDev IsNot Nothing Then
                    Try : _connectedDev.Dispose() : Catch : End Try
                    _connectedDev = Nothing
                End If
                Dim dev = Await BleManager.AwaitOp(BluetoothLEDevice.FromBluetoothAddressAsync(info.Address), 6000, "Connect")
                If dev Is Nothing Then
                    Log("Connect failed/timeout", LogLevel.Fatal, GetDeviceTag(info))
                    Return
                End If
                _connectedDev = dev
                _connectedInfo = info
                info.IsConnected = True
                UpdateRowByAddress(info)
                UpdateConnectedLabels()

                Dim mtu = Await TryReadMtu(dev)
                If mtu > 0 Then Log(String.Format(LocalizationManager.T("LogMtu"), mtu), LogLevel.Info, GetDeviceTag(info))
                Await LoadServicesAsync(dev)
            Catch ex As Exception
                Log("Connect error: " & ex.Message, LogLevel.Fatal, GetDeviceTag(info))
            End Try
        End Sub

        Private Async Function TryReadMtu(dev As BluetoothLEDevice) As Task(Of Integer)
            Try
                Dim session = Await BleManager.AwaitOp(
                    GattSession.FromDeviceIdAsync(BluetoothDeviceId.FromId(dev.DeviceId)), 4000, "GattSession")
                If session IsNot Nothing Then Return session.MaxPduSize
            Catch
            End Try
            Return -1
        End Function

        Private Sub OnDisconnectDevice(info As BleDeviceInfo)
            If Not info.IsConnected AndAlso Not ReferenceEquals(_connectedInfo, info) Then Return
            If _connectedDev IsNot Nothing Then
                Try
                    If _selectedChar IsNot Nothing Then
                        RemoveHandler _selectedChar.GattChar.ValueChanged, AddressOf OnValueChanged
                    End If
                    _connectedDev.Dispose()
                Catch
                End Try
                _connectedDev = Nothing
            End If
            If _connectedInfo IsNot Nothing Then _connectedInfo.IsConnected = False
            _connectedInfo = Nothing
            _selectedChar = Nothing
            _gattServices = Nothing
            If dgvGatt IsNot Nothing Then dgvGatt.Rows.Clear()
            UpdateConnectedLabels()
            ResetOps()
            UpdateRowByAddress(info)
            Log(LocalizationManager.T("LogDisconnected"), LogLevel.Info, GetDeviceTag(info))
        End Sub

        Private Sub OnDisconnectConnClick(sender As Object, e As EventArgs)
            If _connectedInfo IsNot Nothing Then OnDisconnectDevice(_connectedInfo)
        End Sub

        Private Async Sub OnPairDevice(info As BleDeviceInfo)
            Try
                Dim dev = Await BluetoothLEDevice.FromBluetoothAddressAsync(info.Address)
                If dev IsNot Nothing Then
                    Dim result = Await dev.DeviceInformation.Pairing.PairAsync()
                    If result.Status = Windows.Devices.Enumeration.DevicePairingResultStatus.Paired Then
                        Log(LocalizationManager.T("LogPairOk"), LogLevel.Info, GetDeviceTag(info))
                    Else
                        Log(LocalizationManager.T("LogPairFail") & ": " & result.Status.ToString(), LogLevel.Warn, GetDeviceTag(info))
                    End If
                End If
            Catch ex As Exception
                Log("Pair error: " & ex.Message, LogLevel.Fatal, GetDeviceTag(info))
            End Try
        End Sub

        Private Sub OnClearDevice(info As BleDeviceInfo)
            If ReferenceEquals(_connectedInfo, info) Then OnDisconnectDevice(info)
            If _rowMap.ContainsKey(info.Address) Then
                Dim idx = _rowMap(info.Address)
                If idx >= 0 AndAlso idx < dgvDevices.Rows.Count Then
                    dgvDevices.Rows.RemoveAt(idx)
                End If
                _rowMap.Remove(info.Address)
                _rows.Remove(idx)
                If _selectedRow = idx Then _selectedRow = -1
            End If
        End Sub

        Private Sub UpdateRowByAddress(info As BleDeviceInfo)
            If _rowMap.ContainsKey(info.Address) Then
                Dim idx = _rowMap(info.Address)
                If idx >= 0 AndAlso idx < dgvDevices.Rows.Count Then
                    UpdateRowCells(dgvDevices.Rows(idx), info)
                End If
            End If
        End Sub

        Private Function GetDeviceTag(info As BleDeviceInfo) As String
            Return info.DisplayName() & " " & HexUtils.MacToString(info.Address)
        End Function

        Private Sub UpdateConnectedLabels()
            lblDevName.Text = LocalizationManager.T("DeviceName") & ": " & If(_connectedInfo Is Nothing, "-", _connectedInfo.DisplayName())
            lblDevMac.Text = LocalizationManager.T("MacAddress") & ": " & If(_connectedInfo Is Nothing, "-", HexUtils.MacToString(_connectedInfo.Address))
            lblDevConn.Text = If(_connectedInfo IsNot Nothing AndAlso _connectedInfo.IsConnected,
                                 LocalizationManager.T("ConnectedTrue"), LocalizationManager.T("ConnectedFalse"))
            btnDisconnectConn.Enabled = _connectedInfo IsNot Nothing AndAlso _connectedInfo.IsConnected
        End Sub

        ' ================= 服务/特征值 =================

        ''' <summary>视图下拉变化：切换 分类/简单 渲染方式（复用已缓存的服务数据）。</summary>
        Private Sub OnViewChanged(sender As Object, e As EventArgs)
            If _gattServices IsNot Nothing Then
                PopulateGatt(_gattServices)
            Else
                If dgvGatt IsNot Nothing Then dgvGatt.Rows.Clear()
            End If
        End Sub

        ''' <summary>连接后枚举服务/特征值/描述符并填充属性表。</summary>
        Private Async Function LoadServicesAsync(dev As BluetoothLEDevice) As Task
            Dim services = Await BleManager.LoadServicesAsync(dev)
            _gattServices = services
            PopulateGatt(services)
            Log(LocalizationManager.T("LogServiceLoaded"), LogLevel.Info, GetDeviceTag(_connectedInfo))
            Log(LocalizationManager.T("LogCharLoaded"), LogLevel.Info, GetDeviceTag(_connectedInfo))
        End Function

        ''' <summary>按当前视图模式填充属性表：分类=服务分段加粗+子行缩进；简单=平铺。</summary>
        Private Sub PopulateGatt(services As List(Of BleServiceInfo))
            If dgvGatt Is Nothing Then Return
            dgvGatt.SuspendLayout()
            dgvGatt.Rows.Clear()
            Dim cat As Boolean = cmbView IsNot Nothing AndAlso cmbView.SelectedIndex = 0
            For Each s In services
                ' 服务声明行 0x2800
                Dim svcVal = HexUtils.BytesToHex(GattUuids.UuidBytes(s.Uuid), True)
                If Not String.IsNullOrEmpty(svcVal) Then svcVal &= " (" & s.Name & ")"
                Dim svcHandle = "0x" & s.Handle.ToString("X4")
                Dim ri = dgvGatt.Rows.Add(svcHandle, "0x2800", "Primary Service Declaration: " & s.Name, svcVal, "")
                If cat Then
                    dgvGatt.Rows(ri).DefaultCellStyle.Font = New Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
                    dgvGatt.Rows(ri).DefaultCellStyle.BackColor = Color.FromArgb(224, 235, 255)
                End If
                For Each c In s.Characteristics
                    ' 特征值声明行 0x2803（句柄=特征句柄-1，值=属性+句柄+UUID）
                    Dim dHandle = Math.Max(0, c.Handle - 1)
                    Dim declVal = BuildDeclarationValue(c)
                    Dim dh = "0x" & dHandle.ToString("X4")
                    dgvGatt.Rows.Add(If(cat, "    - " & dh, dh), "0x2803", "Characteristic Declaration", declVal, "")
                    ' 特征值行
                    Dim propsHex = "0x" & CByte(CInt(c.Properties) And &HFF).ToString("X2")
                    Dim ch = "0x" & c.Handle.ToString("X4")
                    Dim riC = dgvGatt.Rows.Add(If(cat, "        - " & ch, ch), GattUuids.UuidDisplay(c.Uuid), c.Name, "", propsHex)
                    dgvGatt.Rows(riC).Tag = c
                    ' 描述符行
                    For Each d In c.Descriptors
                        Dim dd = "0x" & d.Handle.ToString("X4")
                        dgvGatt.Rows.Add(If(cat, "            - " & dd, dd), GattUuids.UuidDisplay(d.Uuid), d.Name, "", "")
                    Next
                Next
            Next
            dgvGatt.ResumeLayout()
        End Sub

        ''' <summary>构造 0x2803 特征值声明的值：属性字节 + 句柄(2 字节小端) + UUID 字节。</summary>
        Private Function BuildDeclarationValue(c As BleCharacteristicInfo) As String
            Dim parts As New List(Of Byte)
            parts.Add(CByte(CInt(c.Properties) And &HFF))
            Dim h As Integer = CInt(c.Handle)
            parts.Add(CByte(h And &HFF))
            parts.Add(CByte((h >> 8) And &HFF))
            parts.AddRange(GattUuids.UuidBytes(c.Uuid))
            Return HexUtils.BytesToHex(parts.ToArray(), True)
        End Function

        ''' <summary>点击属性表行：若是特征值行则选中并启用读取/写入/通知；否则清空。</summary>
        Private Sub OnGattCellClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 OrElse dgvGatt.Rows(e.RowIndex).Tag Is Nothing Then
                _selectedChar = Nothing
                chkNotify.Enabled = False
                btnRead.Enabled = False
                btnWriteNoResp.Enabled = False
                UpdatePermGrid(Nothing)
                Return
            End If
            Dim c = TryCast(dgvGatt.Rows(e.RowIndex).Tag, BleCharacteristicInfo)
            If c Is Nothing Then
                _selectedChar = Nothing
                chkNotify.Enabled = False
                btnRead.Enabled = False
                btnWriteNoResp.Enabled = False
                UpdatePermGrid(Nothing)
            Else
                _selectedChar = c
                chkNotify.Enabled = (c.Properties And GattCharacteristicProperties.Notify) <> 0
                btnRead.Enabled = (c.Properties And GattCharacteristicProperties.Read) <> 0
                btnWriteNoResp.Enabled = (c.Properties And GattCharacteristicProperties.WriteWithoutResponse) <> 0
                UpdatePermGrid(c)
            End If
        End Sub

        ''' <summary>填充权限状态表的固定行（属性名 + 未启用）。</summary>
        Private Sub AddPermRows()
            Dim names As String() = {
                LocalizationManager.T("PropBroadcast"),
                LocalizationManager.T("PropRead"),
                LocalizationManager.T("PropWriteNoResp"),
                LocalizationManager.T("PropWrite"),
                LocalizationManager.T("PropNotify"),
                LocalizationManager.T("PropIndicate"),
                LocalizationManager.T("PropAuthSigned"),
                LocalizationManager.T("PropExt")
            }
            For Each n In names
                permGrid.Rows.Add(n, False)
            Next
        End Sub

        ''' <summary>刷新权限状态表：显示所选 Characteristic 的可用属性；无选中则全部未启用。</summary>
        Private Sub UpdatePermGrid(c As BleCharacteristicInfo)
            If permGrid Is Nothing Then Return
            Dim has As Boolean = c IsNot Nothing
            If Not has Then
                For Each row As DataGridViewRow In permGrid.Rows
                    row.Cells("cEnabled").Value = False
                Next
                Return
            End If
            permGrid.Rows(0).Cells("cEnabled").Value = (c.Properties And GattCharacteristicProperties.Broadcast) <> 0
            permGrid.Rows(1).Cells("cEnabled").Value = (c.Properties And GattCharacteristicProperties.Read) <> 0
            permGrid.Rows(2).Cells("cEnabled").Value = (c.Properties And GattCharacteristicProperties.WriteWithoutResponse) <> 0
            permGrid.Rows(3).Cells("cEnabled").Value = (c.Properties And GattCharacteristicProperties.Write) <> 0
            permGrid.Rows(4).Cells("cEnabled").Value = (c.Properties And GattCharacteristicProperties.Notify) <> 0
            permGrid.Rows(5).Cells("cEnabled").Value = (c.Properties And GattCharacteristicProperties.Indicate) <> 0
            permGrid.Rows(6).Cells("cEnabled").Value = (c.Properties And GattCharacteristicProperties.AuthenticatedSignedWrites) <> 0
            permGrid.Rows(7).Cells("cEnabled").Value = (c.Properties And GattCharacteristicProperties.ExtendedProperties) <> 0
        End Sub

        ' ================= 特征值操作 =================

        Private Sub ResetOps()
            chkNotify.Checked = False
            chkNotify.Enabled = False
            btnRead.Enabled = False
            btnWriteNoResp.Enabled = False
            UpdatePermGrid(Nothing)
            _recvBytes = 0 : _recvLastBytes = 0 : _recvSpeed = 0.0
            _sendBytes = 0 : _sendLastBytes = 0 : _sendSpeed = 0.0
            _fileBytes = Nothing
            UpdateRecvStats()
            UpdateSendStats()
        End Sub

        Private Async Sub OnNotifyCheckedChanged(sender As Object, e As EventArgs)
            If _selectedChar Is Nothing Then
                chkNotify.Checked = False
                Return
            End If
            Try
                If chkNotify.Checked Then
                    AddHandler _selectedChar.GattChar.ValueChanged, AddressOf OnValueChanged
                    Dim status = Await _selectedChar.GattChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify)
                    If status = GattCommunicationStatus.Success Then
                        Log(LocalizationManager.T("LogNotifyOn"), LogLevel.Info, GetDeviceTag(_connectedInfo))
                    Else
                        Log("Enable notify failed: " & status.ToString(), LogLevel.Warn)
                        chkNotify.Checked = False
                    End If
                Else
                    RemoveHandler _selectedChar.GattChar.ValueChanged, AddressOf OnValueChanged
                    Await _selectedChar.GattChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None)
                    Log(LocalizationManager.T("LogNotifyOff"), LogLevel.Info, GetDeviceTag(_connectedInfo))
                End If
            Catch ex As Exception
                Log("Notify error: " & ex.Message, LogLevel.Fatal)
                chkNotify.Checked = False
            End Try
        End Sub

        Private Sub OnValueChanged(sender As GattCharacteristic, e As GattValueChangedEventArgs)
            Dim bytes = HexUtils.BufferToBytes(e.CharacteristicValue)
            Ui(Sub()
                   _recvBytes += bytes.Length
                   Log(HexUtils.BytesToHex(bytes, True), LogLevel.Info)
                   UpdateRecvStats()
               End Sub)
        End Sub

        Private Async Sub OnReadClick(sender As Object, e As EventArgs)
            If _selectedChar Is Nothing Then
                Log(LocalizationManager.T("LogNoCharSelected"), LogLevel.Warn)
                Return
            End If
            Try
                Dim result = Await _selectedChar.GattChar.ReadValueAsync()
                If result.Status = GattCommunicationStatus.Success Then
                    Dim bytes = HexUtils.BufferToBytes(result.Value)
                    _recvBytes += bytes.Length
                    Log(String.Format(LocalizationManager.T("LogRead"), bytes.Length), LogLevel.Info, GetDeviceTag(_connectedInfo))
                    Log(HexUtils.BytesToHex(bytes, True), LogLevel.Info)
                    UpdateRecvStats()
                Else
                    Log("Read failed: " & result.Status.ToString(), LogLevel.Fatal)
                End If
            Catch ex As Exception
                Log("Read error: " & ex.Message, LogLevel.Fatal)
            End Try
        End Sub

        Private Function BuildSendBytes() As Byte()
            If chkHexSend.Checked Then
                Try
                    Return HexUtils.HexToBytes(txtWrite.Text)
                Catch ex As Exception
                    Log("Invalid HEX: " & ex.Message, LogLevel.Fatal)
                    Return Nothing
                End Try
            Else
                Return Encoding.UTF8.GetBytes(txtWrite.Text)
            End If
        End Function

        Private Async Sub OnWriteNoRespClick(sender As Object, e As EventArgs)
            If _selectedChar Is Nothing Then
                Log(LocalizationManager.T("LogNoCharSelected"), LogLevel.Warn)
                Return
            End If
            Dim bytes As Byte() = BuildSendBytes()
            If bytes Is Nothing OrElse bytes.Length = 0 Then Return
            Try
                Dim result = Await _selectedChar.GattChar.WriteValueWithResultAsync(
                    HexUtils.BytesToBuffer(bytes), GattWriteOption.WriteWithoutResponse)
                If result.Status = GattCommunicationStatus.Success Then
                    _sendBytes += bytes.Length
                    Log(String.Format(LocalizationManager.T("LogWrite"), bytes.Length), LogLevel.Info, GetDeviceTag(_connectedInfo))
                    UpdateSendStats()
                Else
                    Log("Write No Response failed: " & result.Status.ToString(), LogLevel.Fatal)
                End If
            Catch ex As Exception
                Log("Write No Response error: " & ex.Message, LogLevel.Fatal)
            End Try
        End Sub

        Private Sub UpdateRecvStats()
            ' 接收统计已移除
        End Sub

        Private Sub UpdateSendStats()
            ' 发送统计已移除
        End Sub

        Private Sub StartSpeedTimer()
            _timer = New Timer()
            _timer.Interval = 1000
            AddHandler _timer.Tick, AddressOf OnTimerTick
            _timer.Start()
        End Sub

        Private Sub OnTimerTick(sender As Object, e As EventArgs)
            _recvSpeed = (_recvBytes - _recvLastBytes) * 1.0
            _sendSpeed = (_sendBytes - _sendLastBytes) * 1.0
            _recvLastBytes = _recvBytes
            _sendLastBytes = _sendBytes
            UpdateRecvStats()
            UpdateSendStats()
        End Sub

        ' ================= 菜单/工具 =================

        Private Sub OnScannerClick(sender As Object, e As EventArgs)
            If _scanning Then
                _ble.StopScan()
                If _refreshTimer IsNot Nothing Then
                    _refreshTimer.Stop()
                    FlushPending()
                End If
                _scanning = False
                If btnScanner IsNot Nothing Then btnScanner.Text = LocalizationManager.T("Scanner")
            Else
                ' 先重新初始化蓝牙：停掉旧 watcher（若残留）并释放
                _ble.StopScan()
                ' 清空扫描框，重新添加扫描到的设备（务必连地址去重表一起清）
                dgvDevices.Rows.Clear()
                _rows.Clear()
                _rowMap.Clear()
                _selectedRow = -1
                _totalScanned = 0
                lblTotalScanned.Text = String.Format(LocalizationManager.T("TotalScanned"), _totalScanned.ToString("D3"))
                _scanning = True
                If btnScanner IsNot Nothing Then btnScanner.Text = LocalizationManager.T("StopScan")
                ' 延时一小段再真正启动扫描，确保旧 watcher 完全释放、新扫描一定生效
                Dim initTimer As New Timer()
                initTimer.Interval = 300
                AddHandler initTimer.Tick, Sub(s, ev)
                    initTimer.Stop()
                    initTimer.Dispose()
                    _ble.StartScan()
                    If _refreshTimer IsNot Nothing Then _refreshTimer.Start()
                End Sub
                initTimer.Start()
            End If
        End Sub

        Private Sub OnStopScanClick(sender As Object, e As EventArgs)
            _ble.StopScan()
            If _refreshTimer IsNot Nothing Then
                _refreshTimer.Stop()
                FlushPending()
            End If
            _scanning = False
            If btnScanner IsNot Nothing Then btnScanner.Text = LocalizationManager.T("Scanner")
        End Sub

        ''' <summary>点击“已配对”：弹出已配对设备管理对话框（顶部图标菜单栏 + 设备地址列表）。Remove 可解绑删除，Refresh 重枚举，Clear 清空。</summary>
        Private Sub OnBondedClick(sender As Object, e As EventArgs)
            Dim dlg As New Form()
            dlg.Text = LocalizationManager.T("PairedDevices")
            dlg.Size = New Size(640, 500)
            dlg.MinimumSize = New Size(560, 420)
            dlg.MinimizeBox = False
            dlg.MaximizeBox = False
            dlg.ShowInTaskbar = False
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.BackColor = Color.White

            ' 设备地址列表（仅 Device Address 一列，随窗体缩放）
            Dim grid As New DataGridView()
            grid.Dock = DockStyle.Fill
            grid.ReadOnly = True
            grid.AllowUserToAddRows = False
            grid.AllowUserToResizeRows = False
            grid.RowHeadersVisible = False
            grid.MultiSelect = False
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            grid.BorderStyle = BorderStyle.FixedSingle
            grid.BackgroundColor = Color.White
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            grid.Columns.Add("cAddr", "Device Address")
            grid.Columns("cAddr").MinimumWidth = 300
            dlg.Controls.Add(grid)

            ' 枚举已配对设备并填充网格（AEP 选择器优先，回退到配对状态选择器）
            Dim LoadPaired As Action = Async Sub()
                                            grid.Rows.Clear()
                                            Try
                                                Dim aep As String = "((System.Devices.Aep.ProtocolId:=""{bb7bb05e-5972-42b5-94fc-76eaa7084d49}"") AND (System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True))"
                                                Dim props As String() = {"System.Devices.Aep.DeviceAddress", "System.ItemNameDisplay"}
                                                Dim devs = Await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(aep, props)
                                                If devs.Count = 0 Then
                                                    Dim sel = BluetoothLEDevice.GetDeviceSelectorFromPairingState(True)
                                                    devs = Await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(sel, props)
                                                End If
                                                For Each di In devs
                                                    Dim mac = DeviceMac(di)
                                                    Dim r = grid.Rows.Add(mac & " (" & AddrTypeOfMac(mac) & ")")
                                                    grid.Rows(r).Tag = di
                                                Next
                                            Catch
                                            End Try
                                        End Sub

            ' 顶部菜单栏：带图标的 Remove / Clear / Refresh
            Dim toolBar As New ToolStrip()
            toolBar.Dock = DockStyle.Top
            toolBar.GripStyle = ToolStripGripStyle.Hidden
            toolBar.BackColor = Color.White
            toolBar.Padding = New Padding(6, 2, 0, 2)
            Dim btnRefresh = New ToolStripButton("Refresh", MakeToolIcon("refresh"), Sub(s2, e2) LoadPaired.Invoke())
            Dim btnClear = New ToolStripButton("Clear", MakeToolIcon("clear"), Sub(s2, e2) grid.Rows.Clear())
            Dim btnRemove = New ToolStripButton("Remove", MakeToolIcon("remove"), Async Sub(s2, e2)
                                                                                        Try
                                                                                            If grid.CurrentRow Is Nothing OrElse grid.CurrentRow.Tag Is Nothing Then Return
                                                                                            Dim di = TryCast(grid.CurrentRow.Tag, Windows.Devices.Enumeration.DeviceInformation)
                                                                                            If di Is Nothing Then Return
                                                                                            Dim result = Await di.Pairing.UnpairAsync()
                                                                                            If result.Status = Windows.Devices.Enumeration.DeviceUnpairingResultStatus.Unpaired Then
                                                                                                grid.Rows.Remove(grid.CurrentRow)
                                                                                            End If
                                                                                        Catch
                                                                                        End Try
                                                                                    End Sub)
            toolBar.Items.AddRange(New ToolStripItem() {btnRemove, btnClear, btnRefresh})
            dlg.Controls.Add(toolBar)

            LoadPaired.Invoke()
            dlg.ShowDialog(Me)
            dlg.Dispose()
        End Sub

        ''' <summary>绘制工具栏图标（16x16），符号带对应功能色：add=绿+号、remove=红减号、clear=橙垃圾桶、refresh=蓝循环箭头。</summary>
        Private Function MakeToolIcon(kind As String) As Image
            Dim iconColor As Color
            Select Case kind
                Case "add" : iconColor = Color.FromArgb(0, 153, 68)
                Case "remove" : iconColor = Color.FromArgb(204, 0, 0)
                Case "clear" : iconColor = Color.FromArgb(230, 126, 0)
                Case Else : iconColor = Color.FromArgb(0, 102, 204)
            End Select
            Dim bmp As New Bitmap(16, 16)
            bmp.SetResolution(96, 96)
            Using g = Graphics.FromImage(bmp)
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias
                g.Clear(Color.Transparent)
                Dim pen As New Pen(iconColor, 1.8F)
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round
                Select Case kind
                    Case "add"
                        g.DrawLine(pen, 8.0F, 3.5F, 8.0F, 12.5F)
                        g.DrawLine(pen, 3.5F, 8.0F, 12.5F, 8.0F)
                    Case "remove"
                        g.DrawLine(pen, 3.5F, 8.0F, 12.5F, 8.0F)
                    Case "clear"
                        g.DrawLine(pen, 2.5F, 4.0F, 13.5F, 4.0F)
                        g.DrawLine(pen, 5.5F, 2.0F, 10.5F, 2.0F)
                        g.DrawRectangle(pen, 3.5F, 4.0F, 9.0F, 10.0F)
                    Case "refresh"
                        g.DrawArc(pen, 2.5F, 2.5F, 11.0F, 11.0F, 35, 285)
                        g.DrawLine(pen, 13.5F, 3.2F, 11.0F, 1.2F)
                        g.DrawLine(pen, 13.5F, 3.2F, 9.4F, 3.6F)
                End Select
                pen.Dispose()
            End Using
            Return bmp
        End Function



        ''' <summary>由 MAC 首字节推断地址类型(Random/Public)。</summary>
        Private Function AddrTypeOfMac(mac As String) As String
            Try
                Dim first = Integer.Parse(mac.Substring(0, 2), System.Globalization.NumberStyles.HexNumber)
                If (first And &HC0) <> 0 Then Return "Random"
                Return "Public"
            Catch
                Return "Public"
            End Try
        End Function

        ''' <summary>从设备 Id 中提取 MAC 地址（形如 00:11:22:33:44:55）。</summary>
        Private Function ParseMacFromId(id As String) As String
            Dim m = System.Text.RegularExpressions.Regex.Match(id, "(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}")
            If m.Success Then Return m.Value.ToUpperInvariant()
            Return id
        End Function

        ''' <summary>取设备 MAC：优先用 AEP 的 DeviceAddress 属性，否则从 Id 提取。</summary>
        Private Function DeviceMac(di As Windows.Devices.Enumeration.DeviceInformation) As String
            Try
                If di.Properties IsNot Nothing AndAlso di.Properties.ContainsKey("System.Devices.Aep.DeviceAddress") Then
                    Dim raw = di.Properties("System.Devices.Aep.DeviceAddress").ToString()
                    Dim h = raw.Replace(":", "").Replace("-", "").Trim()
                    If h.Length >= 12 Then
                        h = h.Substring(0, 12).ToUpperInvariant()
                        Dim parts As New List(Of String)
                        For i As Integer = 0 To 11 Step 2
                            parts.Add(h.Substring(i, 2))
                        Next
                        Return String.Join(":", parts)
                    End If
                End If
            Catch
            End Try
            Return ParseMacFromId(di.Id)
        End Function

        Private Sub OnLanguageClick(sender As Object, e As EventArgs)
            Dim it = DirectCast(sender, ToolStripMenuItem)
            LocalizationManager.Switch(DirectCast(it.Tag, String))
            ApplyLanguage()
            Log("Language switched to " & LocalizationManager.T("AppTitle"))
        End Sub

        Private Sub OnAboutClick(sender As Object, e As EventArgs)
            MessageBox.Show(LocalizationManager.T("AboutText"), LocalizationManager.T("About"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Sub

        ' ================= 通用 =================

        Private Sub Ui(action As MethodInvoker)
            If IsHandleCreated AndAlso InvokeRequired Then
                BeginInvoke(action)
            Else
                action()
            End If
        End Sub

        Private Sub Log(message As String, Optional level As LogLevel = LogLevel.Info, Optional tag As String = Nothing)
            Ui(Sub()
                   Dim ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                   Dim line As String = ts & " [" & level.ToString().ToUpper() & "]"
                   If Not String.IsNullOrEmpty(tag) Then line &= " " & tag
                   line &= " " & message
                   logList.Items.Add(line)
                   While logList.Items.Count > 1000
                       logList.Items.RemoveAt(0)
                   End While
                   logList.TopIndex = logList.Items.Count - 1
               End Sub)
        End Sub

        ''' <summary>把广播数据表当前选中的单元格内容复制到剪贴板（右键菜单“复制”）。</summary>
        Private Sub CopySelectedAdCells()
            Try
                If adGrid Is Nothing OrElse adGrid.SelectedCells.Count = 0 Then Return
                Dim parts As New System.Text.StringBuilder()
                Dim prevRow As Integer = -1
                For Each cell As DataGridViewCell In adGrid.SelectedCells
                    If cell.RowIndex <> prevRow Then
                        If parts.Length > 0 Then parts.AppendLine()
                        prevRow = cell.RowIndex
                    Else
                        parts.Append(vbTab)
                    End If
                    parts.Append(If(cell.Value Is Nothing, "", cell.Value.ToString()))
                Next
                Clipboard.SetText(parts.ToString())
            Catch ex As Exception
                MessageBox.Show("复制失败: " & ex.Message)
            End Try
        End Sub


        ''' <summary>让右侧 Notifications 面板与属性表显式撑满右侧列宽（锚定边距在小尺寸时算错，故显式设宽）。</summary>
        Private Sub StretchRightBoxes()
            ' _opsPanel 已 Dock=Fill、dgvGatt 已 Anchor 撑满，无需手动拉宽
        End Sub

        ''' <summary>统一重设三段 SplitContainer 的分割距离：日志固定在底部约170、广播数据固定在日志上方约150、左右列按宽度自适应。窗口每次缩放都重算，避免设备列表区域高度错乱被广播数据盖住。</summary>
        Private Sub LayoutSplitters()
            Try
                If _mainSplit IsNot Nothing AndAlso _mainSplit.Height > 240 Then
                    _mainSplit.SplitterDistance = Math.Max(200, _mainSplit.Height - 170)
                End If
                If _broadcastSplit IsNot Nothing AndAlso _broadcastSplit.Height > 320 Then
                    ' 广播数据+权限状态表固定在约232高（可显示全部8项属性），视图/写入区随剩余高度伸缩，互不重叠
                    _broadcastSplit.SplitterDistance = Math.Max(160, _broadcastSplit.Height - 232)
                End If
                If _colSplit IsNot Nothing AndAlso _colSplit.Width > 320 Then
                    ' 左列(设备列表)按窗体宽度约55%随窗体增宽，容纳 名称/MAC/地址类型/RSSI/广播类型/操作 等列
                    _colSplit.SplitterDistance = Math.Max(240, Math.Min(_colSplit.Width - 260, CInt(_colSplit.Width * 0.55)))
                End If
            Catch
            End Try
        End Sub

        ''' <summary>设备列表 DataGridView：宽度/高度跟随左列容器（按钮下方填满），行紧凑连续、自动滚动，无空白。</summary>
        Private Sub LayoutDeviceBox()
            If dgvDevices Is Nothing Then Return
            Dim parent = dgvDevices.Parent
            If parent Is Nothing Then Return
            Dim pw = parent.ClientSize.Width
            Dim ph = parent.ClientSize.Height
            If pw <= 0 OrElse ph <= 0 Then Return
            ' 按钮高 32 顶 y24，网格从 y68 开始
            dgvDevices.SetBounds(8, 68, Math.Max(180, pw - 16), Math.Max(160, ph - 80))
        End Sub

        ''' <summary>右侧布局：_rightSplit 显式填满右列(避开顶部设备信息区)；Notifications 面板固定约 215(容纳写入/发送区)，Service 占剩余更高可滚动；service 树铺满右列宽。</summary>
        Private Sub LayoutRightBoxes()
            Try
                If _rightSplit IsNot Nothing AndAlso _rightSplit.Parent IsNot Nothing Then
                    Dim pw = _rightSplit.Parent.ClientSize.Width
                    Dim ph = _rightSplit.Parent.ClientSize.Height
                    If pw > 0 AndAlso ph > 130 Then
                        _rightSplit.SetBounds(0, 112, pw, ph - 112)
                        _rightSplit.SplitterDistance = Math.Max(160, Math.Min(215, _rightSplit.Height - 120))
                    End If
                End If
                If dgvGatt IsNot Nothing AndAlso dgvGatt.Parent IsNot Nothing Then
                    Dim pw2 = dgvGatt.Parent.ClientSize.Width
                    If pw2 > 40 Then dgvGatt.Width = Math.Max(120, pw2 - 16)
                End If
                ' 设备名称/MAC地址 信息标签宽度与写入文本框一致(右列宽-16)，随窗体变化
                If lblDevName IsNot Nothing AndAlso lblDevName.Parent IsNot Nothing Then
                    Dim pw3 = lblDevName.Parent.ClientSize.Width
                    If pw3 > 40 Then
                        Dim fw3 = Math.Max(120, pw3 - 16)
                        lblDevName.Width = fw3
                        lblDevMac.Width = fw3
                    End If
                End If
            Catch
            End Try
        End Sub

        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            If _refreshTimer Is Nothing Then
                _refreshTimer = New Timer()
                _refreshTimer.Interval = 200
                AddHandler _refreshTimer.Tick, AddressOf OnRefreshTimerTick
            End If
            Try
                LayoutSplitters()
                StretchRightBoxes()
                LayoutDeviceBox()
                LayoutRightBoxes()
            Catch
            End Try
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            ' 节流：尺寸没实际变化就不重排，避免分割器/子控件引发的级联 Resize 导致卡顿
            If Me.Width = _lastW AndAlso Me.Height = _lastH Then Return
            _lastW = Me.Width
            _lastH = Me.Height
            Try
                LayoutSplitters()
                StretchRightBoxes()
                LayoutDeviceBox()
                LayoutRightBoxes()
            Catch
            End Try
        End Sub

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            Try
                _ble.StopScan()
                If _refreshTimer IsNot Nothing Then _refreshTimer.Stop()
                If _timer IsNot Nothing Then _timer.Stop()
                If _connectedDev IsNot Nothing Then
                    Try : _connectedDev.Dispose() : Catch : End Try
                End If
            Catch
            End Try
            MyBase.OnFormClosing(e)
        End Sub

        ''' <summary>自绘个性化 RSSI 滚动条：圆角轨道 + 蓝色渐变填充 + 圆形滑块，可拖动选择阈值。</summary>
        Private Class RssiBar
            Inherits Control

            Private _min As Integer = -100
            Private _max As Integer = -40
            Private _val As Integer = -100
            Private _dragging As Boolean = False

            Public Event ValueChanged(sender As Object, e As EventArgs)

            Public Property Minimum As Integer
                Get
                    Return _min
                End Get
                Set(v As Integer)
                    _min = v
                    If _val < _min Then _val = _min
                    Invalidate()
                End Set
            End Property

            Public Property Maximum As Integer
                Get
                    Return _max
                End Get
                Set(v As Integer)
                    _max = v
                    If _val > _max Then _val = _max
                    Invalidate()
                End Set
            End Property

            Public Property Value As Integer
                Get
                    Return _val
                End Get
                Set(v As Integer)
                    v = Math.Max(_min, Math.Min(_max, v))
                    If _val <> v Then
                        _val = v
                        RaiseEvent ValueChanged(Me, EventArgs.Empty)
                        Invalidate()
                    End If
                End Set
            End Property

            Private Function ThumbX() As Integer
                Dim pad = 12
                Dim span = _max - _min
                If span <= 0 Then Return pad
                Dim usable = Math.Max(1, Me.Width - pad * 2)
                Return pad + CInt((CSng(_val - _min) / span) * usable)
            End Function

            Protected Overrides Sub OnPaint(e As PaintEventArgs)
                MyBase.OnPaint(e)
                Dim g = e.Graphics
                g.SmoothingMode = SmoothingMode.AntiAlias
                Dim h = Me.ClientSize.Height
                Dim pad = 12
                Dim trackY = CInt(h / 2)
                Dim trackH = 6
                Dim rect = New Rectangle(pad, trackY - trackH \ 2, Math.Max(1, Me.Width - pad * 2), trackH)
                Using b As New SolidBrush(Color.FromArgb(224, 230, 238))
                    FillRoundRect(g, rect, trackH, b)
                End Using
                Dim tx = ThumbX()
                If tx > pad Then
                    Dim fillRect = New Rectangle(pad, trackY - trackH \ 2, tx - pad, trackH)
                    Using b As New LinearGradientBrush(fillRect, Color.FromArgb(96, 160, 245), Color.FromArgb(46, 110, 230), LinearGradientMode.Horizontal)
                        FillRoundRect(g, fillRect, trackH, b)
                    End Using
                End If
                Dim r = 9
                Using b As New SolidBrush(Color.White)
                    g.FillEllipse(b, tx - r, trackY - r, r * 2, r * 2)
                End Using
                Using p As New Pen(Color.FromArgb(46, 110, 230), 2)
                    g.DrawEllipse(p, tx - r, trackY - r, r * 2, r * 2)
                End Using
            End Sub

            Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
                MyBase.OnMouseDown(e)
                _dragging = True
                SetFromX(e.X)
            End Sub

            Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
                MyBase.OnMouseMove(e)
                If _dragging Then SetFromX(e.X)
            End Sub

            Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
                MyBase.OnMouseUp(e)
                _dragging = False
            End Sub

            Private Sub SetFromX(x As Integer)
                Dim pad = 12
                Dim usable = Math.Max(1, Me.Width - pad * 2)
                Dim ratio = CSng(x - pad) / usable
                ratio = Math.Max(0.0F, Math.Min(1.0F, ratio))
                Value = _min + CInt(Math.Round(ratio * (_max - _min)))
            End Sub

            Private Shared Sub FillRoundRect(g As Graphics, r As Rectangle, rad As Integer, brush As Brush)
                rad = Math.Max(1, rad)
                Using gp As New GraphicsPath()
                    Dim d = rad * 2
                    gp.AddArc(r.X, r.Y, d, d, 180, 90)
                    gp.AddArc(r.Right - d, r.Y, d, d, 270, 90)
                    gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
                    gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
                    gp.CloseFigure()
                    g.FillPath(brush, gp)
                End Using
            End Sub
        End Class
    End Class
End Namespace
