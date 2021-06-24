## [2.0.1.0] 2021-06-24

### TheBot
- 增加#洗澡水作为#幻想药的别名。
- 实现.st .pc指令。调整.ra .rb .en .sc 等指令使用当前角色卡。
- Dicer.RandomDicer暂时不使用共享实例，等线程安全复核后改回。
- 调整GenericRPN实现，操作符支持单目操作。
- 投骰表达式中D支持单目操作，如D100等。
- 禁用AppShareConverter，等待后续数据分析后更新。

### FsCqHttp
- EventContext改名为PostContent移动至KPX.FsCqHttp
- OptionBase改用ConcurrentDictionary避免线程问题。
- 修复OptionCell.IsDefined逻辑问题。
- 增加GroupEssence事件。
- 修复GroupNotifyEvent事件转换错误。
- 修复#help对没有LongHelp字段指令无响应的问题。
- Utils.TextResponse中字符串测量类改为非静态。避免多线程使用开销。

## [2.0.0.0] 2021-06-17
- 调整FsCqHttp：使用DU替换了一些常用类型，补充了相关类型的单元测试。
- 修复：cqhttp使用string上报格式时异常。
- 修复：TheBot.Utils.Dicer在最大值域时异常。
- 修复：MessageEvent.DisplayName没有考虑匿名情况。

## [1.0.1.0] 2021-06-06
- 不再使用程序文件md5作为##su校验依据