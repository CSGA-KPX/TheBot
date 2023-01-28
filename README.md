私用QQ机器人（汉考克）
===========
学习F#的休闲项目，有大量抬血压的代码。
如有建议和问题请提交Issue或者发Discussions。

主要提供功能：
* FF14：市场信息查询；配方计算；海钓攻略和时刻表；暖暖攻略；票据兑换计算等。
* EVE：市场信息查询；配方计算；制造总览；LP兑换计算等。
* 其他：简易计算器，吃什么。

项目说明
=======
运行于.NET 7。
* 需要在系统中安装一个等宽字体。
* 可以通过环境变量、配置文件或者命令行参数等传递配置（只有我自己用就不写了）。

FsCqHttp
========
当时.NET平台的几个cq-http-api框架都不满足需求，所以还是自己整了个轮子。
主要功能如下：
* 支持正向WebSocket和反向WebSocket连接方式。
* 实现了核心的OneBot API和CQ码段，不常用的只留了个stub。
* 感觉异步收益不大，具体指令处理使用同步执行，并使用MailBoxProcessor控制并发数。
    * 如果以后出现IO密集但是计算不密集指令的时候会考虑处理异步。
* 提供TestContext，可用于简单的指令测试。
* 提供TextResponse/TextTable，在不依赖外部组件的前提下实现简单的文字排版。
* 提供UserOption，辅助处理指令参数。

致谢
===

本项目使用了免费的[Rider](https://www.jetbrains.com/rider/)开发环境，感谢JetBrains提供免费的开源授权。

FF14数据转储来自： [MansonGit](https://github.com/MansonGit/ffxiv-datamining-jp)、[thewakingsands](https://github.com/thewakingsands/ffxiv-datamining-cn/)和[suzutan](https://github.com/suzutan/ffxiv-datamining-jp/)。

