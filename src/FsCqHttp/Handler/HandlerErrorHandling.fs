namespace KPX.FsCqHttp.Handler

open System


type ErrorLevel =
    | IgnoreError
    | UnknownError
    | InputError
    | ModuleError
    | SystemError
    | ExternalError

    override x.ToString() =
        match x with
        | IgnoreError -> "无视"
        | UnknownError -> "未知错误"
        | InputError -> "输入错误"
        | ModuleError -> "模块错误"
        | SystemError -> "系统错误"
        | ExternalError -> "外部错误"

/// 仅用于中断执行，日志不会记录该异常信息
exception IgnoreException

/// 用于包装ErrorLevel的异常类型
///
/// 用于内部实现代码不方便的调用相关AbortExecution方法时抛出
type ModuleException(level: ErrorLevel, msg: string) =
    inherit Exception(msg)

    new(level: ErrorLevel, fmt: string, [<ParamArray>] args: obj []) = ModuleException(level, String.Format(fmt, args))

    member _.ErrorLevel = level
