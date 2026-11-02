# macOS 版运行说明

> 产物：`YuRiWuDuo.zip`（约 137 MB），解压后是 `余日无多.app`
> 版本：0.5 ｜ 架构：**Universal（Intel x86_64 + Apple Silicon arm64）**
> ⚠️ 这是从 Windows 交叉导出的，**未签名 / 未公证**，第一次打开需要绕一下 Gatekeeper（见下文）。

---

## 一、能不能跑

| 项目 | 状态 |
|---|---|
| 引擎 | Godot 4.7.2（.NET 版），导出模板官方 universal 包 |
| 可执行文件 | `Contents/MacOS/余日无多`，Mach-O **Fat 二进制（2 个架构）** |
| 资源 | `Contents/Resources/余日无多.pck`（16.5 MB，`GDPC` / 引擎 4.7.2，贴图场景数据都在里面） |
| .NET 运行时 | 两套都打包了：`Resources/data_ZombieExtraction_macos_arm64/`、`..._x86_64/`（各 186 个文件） |
| 图标 | `Contents/Resources/icon.icns` |
| 最低系统 | **Intel：macOS 11+**；**Apple Silicon：macOS 13+**（模板与 .NET 8 的要求，见 Info.plist 的 `LSMinimumSystemVersionByArchitecture`） |
| 签名 | ❌ 未签名（`codesign` 关闭）、❌ 未公证（notarization 关闭） |

游戏代码本身没有平台专属逻辑（唯一的 Windows 项是 D3D12 驱动设置，只在 Windows 生效），所以**理论上可以直接跑**。
但**我这边是 Windows，没法真机验证 macOS 启动**，第一次请按下面步骤试并看终端输出。

---

## 二、在 Mac 上第一次打开

1. 解压 `YuRiWuDuo.zip`（用 macOS 自带的归档工具，别用会丢权限的第三方工具），得到 `余日无多.app`。
2. **不要双击**。在 Finder 里 **右键 → 打开** → 再点「打开」。未签名应用这样才会被放行一次。
3. 如果提示「已损坏，无法打开，你应该将它移到废纸篓」或「无法验证开发者」，打开「终端」执行：

```bash
# Apple Silicon 必做：清隔离属性 + 补一个 ad-hoc 签名
xattr -cr ~/Downloads/余日无多.app
codesign --force --deep --sign - ~/Downloads/余日无多.app   # 只加 ad-hoc 签名，不需要开发者证书
```

（Intel 机器一般只要 `xattr -cr` 就够；Apple Silicon 要求二进制至少是 ad-hoc 签名的，所以第二条通常也必须。）

4. 再双击运行。第一次启动会比 Windows 版慢（要加载 .NET 运行时）。

> 如果双击完全没反应，可以在终端里直接跑可执行文件看报错：
> ```bash
> ~/Downloads/余日无多.app/Contents/MacOS/余日无多
> ```

---

## 三、注意事项

- **不要把 `.app` 里面的文件单独拿出来**：`Contents/Resources/余日无多.pck`（资源）和两个 `data_..._macos_*`（.NET 运行时）都是它的一部分。分享时请整个 `.app` 打包（用 `ditto -c -k --sequesterRsrc --keepParent 余日无多.app 余日无多.zip` 能保留可执行权限）。
- **移植到别的 Mac**：通过微信/U 盘传过去会重新带隔离属性，对方同样要 `xattr -cr`。
- **要正式分发**（不让人敲命令）：需要在 Mac 上用 Apple 开发者证书做 `codesign` + `notarytool` 公证，或者干脆在 Mac 上重新导出一次。
- **想改代码/贴图**：`Contents/Resources/余日无多.pck` 不是明文 PNG，要改就改源码项目再重新导出。

---

## 四、更省事的第二条路：在 Mac 上直接用引擎跑

如果交叉导出的 `.app` 起不来（签名/权限折腾），最稳的是用源码跑：

1. 在 Mac 装 **Godot 4.7.2 (.NET / mono 版)**：<https://godotengine.org/download/macos/>
2. 装 .NET SDK 8（`brew install dotnet-sdk` 或官方安装包）。
3. 打开 `test_1/新建游戏项目/project.godot`，等它导入完成（第一次会拉 NuGet 包）。
4. 按 F5 直接运行；也可以在编辑器里 `项目 → 导出 → macOS` 生成**本机签名**的 `.app`。

这样导出由 macOS 自己完成，签名/Gatekeeper 问题基本消失。

---

## 五、这次的导出是怎么做的（可复现）

```powershell
# 1) 装 macOS 导出模板（Windows 上原本只装了 Windows 模板）
#    从 Godot_v4.7.2-stable_mono_export_templates.tpz 里取出 templates/macos.zip，
#    放到 %APPDATA%\Godot\export_templates\4.7.2.stable.mono\macos.zip

# 2) 预设里加了一条 macOS（universal 架构、未签名、ETC2/ASTC 打开）
#    test_1\新建游戏项目\export_presets.cfg

# 3) 导出
& "D:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" `
    --headless --path "test_1\新建游戏项目" --export-release "macOS" `
    "_build\v0.5_格子背包_mac\YuRiWuDuo.zip"
```

两个坑记一下：

- macOS 模板只提供 **universal** 二进制，预设里架构选 `x86_64` 会报「未找到模板 godot_macos_release.x86_64」。
- universal / arm64 导出要求项目开启 **ETC2/ASTC** 纹理导入（`rendering/textures/vram_compression/import_etc2_astc=true`），否则直接拒绝导出。
