# Windows 息屏时间设置工具 - 开发计划

## 一、项目概述

### 1.1 项目目标
开发一款轻量级的Windows系统托盘应用，用于快速设置Windows屏幕超时时间（息屏时间）。

### 1.2 核心功能
- 系统托盘常驻
- 右键菜单选择息屏时间（1分钟到5小时，以及"从不"）
- 实时同步Windows系统电源设置
- 显示当前选中的时间选项
- 关于对话框
- 退出功能

### 1.3 兼容性要求
- 支持 Windows 7 及以上版本
- 主要测试平台：Windows 10、Windows 11

## 二、技术方案设计

### 2.1 开发语言和框架选择

**方案A：C# + Windows Forms（推荐）**
- ✅ 优点：
  - 原生Windows API支持完善
  - 系统托盘组件（NotifyIcon）内置
  - 开发效率高
  - 兼容性好
- ✅ 推荐理由：最适合Windows系统托盘应用开发

**方案B：C++ + Win32 API**
- ✅ 优点：性能最优，体积最小
- ❌ 缺点：开发复杂度高，代码量大

**方案C：Python + pystray**
- ✅ 优点：开发快速
- ❌ 缺点：需要Python运行时，打包体积大

**最终选择：C# + Windows Forms**

### 2.2 核心技术点

#### 2.2.1 系统托盘实现
- 使用 `System.Windows.Forms.NotifyIcon`
- 设置图标、提示文本
- 实现右键上下文菜单（ContextMenuStrip）

#### 2.2.2 Windows电源设置API
- 使用 `SystemParametersInfo` API 设置屏幕超时
- 或使用 `PowerSetActiveScheme` 和 `PowerWriteACValueIndex` API
- 需要调用 `user32.dll` 和 `powrprof.dll`

**关键API：**
```csharp
[DllImport("powrprof.dll", SetLastError = true)]
static extern uint PowerWriteACValueIndex(
    IntPtr RootPowerKey,
    ref Guid SchemeGuid,
    ref Guid SubGroupOfPowerSettingsGuid,
    ref Guid PowerSettingGuid,
    uint AcValueIndex
);

[DllImport("powrprof.dll")]
static extern uint PowerSetActiveScheme(
    IntPtr UserPowerKey,
    ref Guid ActivePolicyGuid
);
```

#### 2.2.3 设置持久化
- 使用 `Application.UserAppDataPath` 存储配置文件
- 格式：JSON 或 XML
- 存储内容：当前选中的时间选项

#### 2.2.4 权限要求
- 修改系统电源设置可能需要管理员权限
- 考虑是否需要以管理员身份运行

## 三、系统架构设计

### 3.1 模块划分

```
ScreenTimeoutApp
├── MainForm.cs              # 主窗体（隐藏，仅用于消息循环）
├── TrayIconManager.cs       # 托盘图标管理
├── PowerSettingsManager.cs  # 电源设置管理
├── ConfigManager.cs         # 配置管理
├── TimeOption.cs            # 时间选项数据模型
└── Program.cs               # 程序入口
```

### 3.2 数据模型

```csharp
public class TimeOption
{
    public string DisplayName { get; set; }  // 显示名称："1分钟"、"从不"等
    public int Minutes { get; set; }          // 分钟数，-1表示"从不"
    public uint PowerIndex { get; set; }      // Windows电源设置索引值
}
```

### 3.3 时间选项映射表

| 显示名称 | 分钟数 | Windows API索引值 |
|---------|--------|------------------|
| 1分钟   | 1      | 1                |
| 2分钟   | 2      | 2                |
| 3分钟   | 3      | 3                |
| 5分钟   | 5      | 5                |
| 10分钟  | 10     | 10               |
| 15分钟  | 15     | 15               |
| 20分钟  | 20     | 20               |
| 25分钟  | 25     | 25               |
| 30分钟  | 30     | 30               |
| 45分钟  | 45     | 45               |
| 1小时   | 60     | 60               |
| 2小时   | 120    | 120              |
| 3小时   | 180    | 180              |
| 4小时   | 240    | 240              |
| 5小时   | 300    | 300              |
| 从不    | -1     | 0                |

## 四、开发步骤

### 阶段一：项目初始化（1-2小时）
1. ✅ 创建C# Windows Forms项目
2. ✅ 配置项目属性（单实例、无控制台窗口）
3. ✅ 添加必要的NuGet包（如需要）

### 阶段二：核心功能开发（4-6小时）

#### 2.1 托盘图标和菜单（1-2小时）
- [ ] 创建NotifyIcon组件
- [ ] 设计右键菜单结构
- [ ] 实现菜单项点击事件
- [ ] 添加图标资源

#### 2.2 电源设置API封装（2-3小时）
- [ ] 研究Windows电源设置API
- [ ] 封装PowerSettingsManager类
- [ ] 实现读取当前设置
- [ ] 实现写入新设置
- [ ] 处理权限和错误情况

#### 2.3 配置管理（1小时）
- [ ] 实现ConfigManager类
- [ ] 保存/加载当前选中项
- [ ] 应用启动时恢复设置

#### 2.4 UI完善（1小时）
- [ ] 实现关于对话框
- [ ] 菜单项选中状态显示（勾选标记）
- [ ] 错误提示和用户反馈

### 阶段三：测试和优化（2-3小时）
1. [ ] 功能测试（各时间选项）
2. [ ] 兼容性测试（Windows 10/11）
3. [ ] 异常处理测试
4. [ ] 性能优化
5. [ ] 代码清理和注释

### 阶段四：打包和发布（1-2小时）
1. [ ] 创建安装程序或便携版
2. [ ] 添加应用图标
3. [ ] 编写使用说明
4. [ ] 准备发布版本

## 五、关键技术难点

### 5.1 Windows电源设置API复杂性
- **挑战**：不同Windows版本的API可能略有差异
- **解决方案**：
  - 使用兼容性最好的API方法
  - 添加版本检测
  - 提供降级方案

### 5.2 权限问题
- **挑战**：修改系统电源设置可能需要管理员权限
- **解决方案**：
  - 检测当前权限
  - 如需要，提示用户以管理员身份运行
  - 或使用UAC提升权限

### 5.3 单实例运行
- **挑战**：防止应用重复启动
- **解决方案**：使用Mutex或命名管道实现单实例

## 六、用户界面设计

### 6.1 托盘图标
- 简洁的图标设计（如时钟或电源图标）
- 鼠标悬停显示提示："屏幕超时设置"

### 6.2 右键菜单结构
```
┌─────────────────────┐
│ 1分钟          ✓    │
│ 2分钟               │
│ 3分钟               │
│ ...                 │
│ 5小时               │
│ 从不                │
├─────────────────────┤
│ 关于                │
│ 退出                │
└─────────────────────┘
```

### 6.3 关于对话框
- 应用名称和版本
- 简要说明
- 开发者信息（可选）
- 确定按钮

## 七、文件结构

```
ScreenTimeout/
├── ScreenTimeoutApp/
│   ├── Properties/
│   │   ├── AssemblyInfo.cs
│   │   └── Resources.resx
│   ├── MainForm.cs
│   ├── TrayIconManager.cs
│   ├── PowerSettingsManager.cs
│   ├── ConfigManager.cs
│   ├── TimeOption.cs
│   ├── AboutDialog.cs
│   ├── Program.cs
│   └── App.config
├── Resources/
│   └── icon.ico
├── README.md
├── 开发计划.md
└── ScreenTimeoutApp.sln
```

## 八、测试计划

### 8.1 功能测试
- [ ] 所有时间选项都能正确设置
- [ ] 设置后Windows系统设置同步更新
- [ ] 重启应用后能恢复上次选择
- [ ] 关于对话框正常显示
- [ ] 退出功能正常

### 8.2 兼容性测试
- [ ] Windows 10 测试
- [ ] Windows 11 测试
- [ ] Windows 7/8.1 测试（如需要）

### 8.3 边界测试
- [ ] 无权限情况处理
- [ ] API调用失败处理
- [ ] 配置文件损坏处理

## 九、风险评估

| 风险 | 可能性 | 影响 | 应对措施 |
|------|--------|------|----------|
| Windows API兼容性问题 | 中 | 高 | 充分测试，准备降级方案 |
| 权限不足导致设置失败 | 中 | 中 | 添加权限检测和提示 |
| 不同Windows版本行为差异 | 低 | 中 | 版本检测和适配 |

## 十、后续优化方向

1. 添加开机自启动选项
2. 支持电池模式下的不同设置
3. 添加快捷键支持
4. 多语言支持
5. 更丰富的图标和UI设计

---

## 开发时间估算

- **总开发时间**：8-13小时
- **预计完成时间**：1-2个工作日（按每天6小时计算）
