# Publish / installer skeleton

- `publish.sh`：Release 发布 Protocols；在 Windows 上再发 WPF 到 `artifacts/publish/app`
- 安装包（MSI/Setup）二期再接，可接 WiX 或 `dotnet` MSIX

CI：Linux 跑 Core/Protocols 测试；`windows-latest` 编译 WPF。
