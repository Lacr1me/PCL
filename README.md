# PCL Lacr1me 自用版

第三方基于 [龙腾猫跃的 Plain Craft Launcher](https://github.com/Meloong-Git/PCL) 独立修改，不代表官方。基于上游 **2.99.0.0 / 6b9a50d**，仅恢复 Minecraft 下载、版本修改和整合包安装入口。原作者署名、赞助入口、正版购买提示及 `LICENCE` 保留。

公开源码将 `RestrictMcDownload` 改为 `False`。上游公开的 `ModSecret.vb` 缺少正式版的部分实现，因此直接编译公开源码不等于完整正式版。

为保留本人已有正式版的完整功能，提供 [本地补丁工具](tools/RestoreDownloads)。工具以本人已有的 **2.99.0.0 正式版 EXE** 为基础，从同版本源码构建结果中移植四个下载方法体，并恢复版本修改页的 Minecraft 卡片。保留原程序的私有实现和全部嵌入资源，添加“Lacr1me 自用版”版本标记。工具会验证所有未修改方法和资源保持一致；不会读取账号令牌或修改注册表。

```powershell
# 先用 Visual Studio MSBuild 构建公开源码，生成方法体供体
MSBuild "Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" /restore /t:Build /p:Configuration=Release
# 需要 .NET Framework 4.8 开发包；补丁工具使用 .NET 10 SDK
dotnet run --project tools/RestoreDownloads -- "原版备份.exe" "Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe" "PCL Lacr1me 自用版.exe" "patch-report.json"
```

安装前备份原 EXE，关闭 PCL，再用生成文件替换 EXE，保留原来的 `PCL` 和 `.minecraft` 文件夹。未来安装官方更新会覆盖此本地修改，需要重新核对对应版本；工具拒绝处理其他版本。仓库不包含个人配置、账号数据、私有实现源码或修改后的正式版二进制。

---

# Plain Craft Launcher

[![Stars](https://img.shields.io/github/stars/Meloong-Git/PCL?style=flat&logo=data:image/svg%2bxml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZlcnNpb249IjEiIHdpZHRoPSIxNiIgaGVpZ2h0PSIxNiI+PHBhdGggZD0iTTggLjI1YS43NS43NSAwIDAgMSAuNjczLjQxOGwxLjg4MiAzLjgxNSA0LjIxLjYxMmEuNzUuNzUgMCAwIDEgLjQxNiAxLjI3OWwtMy4wNDYgMi45Ny43MTkgNC4xOTJhLjc1MS43NTEgMCAwIDEtMS4wODguNzkxTDggMTIuMzQ3bC0zLjc2NiAxLjk4YS43NS43NSAwIDAgMS0xLjA4OC0uNzlsLjcyLTQuMTk0TC44MTggNi4zNzRhLjc1Ljc1IDAgMCAxIC40MTYtMS4yOGw0LjIxLS42MTFMNy4zMjcuNjY4QS43NS43NSAwIDAgMSA4IC4yNVoiIGZpbGw9IiNlYWM1NGYiLz48L3N2Zz4=&logoSize=auto&label=Stars&labelColor=444444&color=eac54f)](https://github.com/Meloong-Git/PCL/)
[![Issues](https://img.shields.io/github/issues/Meloong-Git/PCL?style=flat&label=Issues&labelColor=444444&color=1F883D)](https://github.com/Meloong-Git/PCL/issues)
[![哔哩哔哩](https://img.shields.io/badge/动态-BiliBili-00A4DB?style=flat&labelColor=444444&logoSize=auto)](https://space.bilibili.com/11343203/dynamic)
[![爱发电](https://img.shields.io/badge/赞助-爱发电-946ce6?style=flat&labelColor=444444&logoSize=auto)](https://meloong.com/afd/a/LTCat)

包含 PCL 的大部分源代码，包括 UI 库、动画模块、下载模块、Minecraft 启动模块等！<br/>
代码并不是实时更新的，它会在每次 PCL 发布更新时同步一次。<br/>
非常感谢社区的大力支持！

<br/>

### 相关资源
- [PCL 下载](https://meloong.com/pcl)：下载可免费使用的正式版 PCL！
- [PCL 功能投票](https://github.com/Meloong-Git/PCL/discussions/2)：来参加投票吧，开发者会优先处理票数较高的项目！
- [帮助文档库](https://github.com/LTCatt/PCL2Help)：PCL 内置帮助文档的存储库
