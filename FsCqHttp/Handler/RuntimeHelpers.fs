module KPX.FsCqHttp.Handler.RuntimeHelpers

/// 用于中断调用，不记录日志
exception IgnoreException 

/// 用户输入引发的错误，不记录日志
exception UserErrorException