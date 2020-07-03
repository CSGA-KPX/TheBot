私用QQ机器人（汉考克）
===========
警告：作者非计算机专业，纯属东拼西凑作品。阅读代码可能会引起身体和精神不适，本人概不负责。

运行环境
=======
Win .NET Framework 4.7.2/ Linux Mono

已知问题
=======
* Mono上GDI+处理和Windows不同，文本转图像输出会有bug。

项目说明
=======
|子项目 |用途|
|-----|----|
|LibFFXIV.Network |网络数据定义（私下交流）|
|LibFFXIV.GameData.Raw |捆绑的CSV数据库 （私下交流）|
|LibDmf\* | 私人服务器服务模块（暂不开源） |
|FsCqHttp |酷Q交互轮子，凑合用 |
|BotData |打包数据和底层数据处理 |
|TheBot |本体 |
|TheBot.Utils |辅助工具 |
|TheBot.Module |命令模块 | 