# BLE 调试工具 (BLETestTool) V1.1

VB.NET WinForms 实现的 BLE（Bluetooth Low Energy）调试工具，界面仿照参考截图，支持中英文切换。
用于扫描周边 BLE 设备、查看广播数据、连接设备、枚举服务/特征值、执行读写与通知订阅，并实时记录操作日志。

## 功能
- 扫描 BLE 设备（名称 / MAC / RSSI），支持 连接 / 断开 / 配对 / 清除
- 解析广播数据：MAC、广播类型、Flags、Length / AD Type / Data
- 连接后枚举 Service / Characteristic（UUID、属性、Handle）
- 特征值操作：Read、Write（Write 与 WriteWithoutResponse）、Notify（含实时字节数与速率）
- 支持 HEX / 文本两种收发方式、文件发送
- 底部滚动日志（时间 / 级别 / 设备 / 消息）
- 中文 / English 界面切换（资源文件本地化）

## 技术方案（重要）
- **VB.NET + WinForms + .NET 8（`net8.0-windows10.0.22621.0`）**
- BLE 使用 **Windows 官方 WinRT 蓝牙 API（`Windows.Devices.Bluetooth`）**
- **零额外依赖**：WinRT 类型由目标框架 `net8.0-windows10.0.X` 自动内置（CsWinRT 投影），**不需要装任何包**

> 为什么不用 .NET Framework 4.8 / 32feet.NET / Microsoft.Windows.SDK.Contracts？
> - 32feet.NET 不含这套 BLE API；
> - .NET Framework 引用 WinRT 需要手动指向具体版本的 `Windows.winmd`，路径极易失败；
> - `Microsoft.Windows.SDK.Contracts` 是旧式 winmd 包，在 .NET 5+ 会触发 NETSDK1130。
> 最干净的方案就是 `net8.0-windows10.0.22621.0`，自带 WinRT，无需任何包。

## 运行环境（必需）
- **Windows 10 1903+**（BLE 需要 Windows 10 及以上，WinRT API 仅在 Windows 可用）
- **.NET 8 SDK** 与 **Visual Studio 2022（17.8+）**（或任意支持 .NET 8 的编辑器）
  - 若本机没有 .NET 8 SDK：到 https://dotnet.microsoft.com/download 安装 .NET 8 SDK（或 VS2022 安装器勾选“.NET 桌面开发”）。
- 真实的蓝牙适配器（支持 BLE），系统蓝牙驱动正常

> 注意：BLE 真机收发依赖 Windows 的 WinRT 蓝牙栈，因此本工具**必须在 Windows 上运行**，
> 无法在 Linux / 虚拟机（无蓝牙透传）上做真机联调。

## 构建与运行
1. 用 VS2022 打开 `BLETestTool.sln`（无需还原任何 NuGet 包，WinRT 由目标框架内置）
3. 确认默认 SDK 为 `net8.0-windows`，F5 编译运行

## 工程结构
```
BLETestTool/
├── BLETestTool.sln
└── BLETestTool/
    ├── Program.vb                       入口
    ├── MainForm.vb                      主界面 + 逻辑编排
    ├── BLE/
    │   ├── BleManager.vb                BLE 服务层（扫描/连接/服务枚举，WinRT）
    │   ├── BleDeviceInfo.vb             设备信息
    │   ├── BleServiceInfo.vb            服务信息
    │   ├── BleCharacteristicInfo.vb     特征值信息
    │   ├── AdDataSection.vb             广播数据段 + AD Type 解析
    │   └── GattUuids.vb                 常用 UUID 名称映射
    ├── Utils/HexUtils.vb                HEX / IBuffer 转换
    ├── Localization/LocalizationManager.vb 本地化管理
    └── Resources/Strings[.zh-CN|.en-US].resx 多语言资源
```

## 依赖 API 说明（Windows.Devices.Bluetooth）
| 功能 | 命名空间 / 类型 |
|---|---|
| 扫描 | `Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher` |
| 设备 | `Windows.Devices.Bluetooth.BluetoothLEDevice` |
| GATT | `Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService / GattCharacteristic` |
| MTU | `Windows.Devices.Bluetooth.GenericAttributeProfile.GattSession.MaxPduSize` |
| 配对 | `Windows.Devices.Enumeration.DevicePairingResultStatus` |
| 数据 | `Windows.Storage.Streams.DataReader / DataWriter / IBuffer` |

## 已知说明 / 限制
- **MTU 读取**：通过 `GattSession.MaxPduSize` 读取；部分设备/平台返回 0 或 -1 时日志不打印 MTU。
- **Bonded（已配对列表）**：当前版本为占位，仅输出日志提示；需要可后续基于 `DeviceInformation.FindAllAsync` 扩展。
- **广播解析**：仅解析截图所需的 AD Type（Flags、Device Name、Manufacturer Specific 等）。
- **配对**：部分系统会弹出配对确认，属于系统行为。
- 本工程代码按 `Windows.Devices.Bluetooth` 标准 WinRT API 编写，由于 BLE 必须在 Windows 真机运行，
  **未能在当前开发环境编译/真机验证**；若个别 API 与目标框架内置的 WinRT 版本有出入，请按编译器提示微调。
