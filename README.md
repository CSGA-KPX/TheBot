私用QQ机器人（汉考克）
===========

主要提供功能：
* FF14：市场信息查询；配方计算；海钓攻略和时刻表；暖暖攻略；票据兑换计算等。
	** 目前使用Universalis.app和LibDmf两个数据源。
* EVE：市场信息查询；配方计算；制造总览；LP兑换计算等。
* 其他：简易计算器，吃什么。

运行环境
=======
Windows .NET FW 4.7.2 / Linux Mono

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