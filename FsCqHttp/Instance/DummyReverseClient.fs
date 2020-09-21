namespace KPX.FsCqHttp.Instance

open System
open System.Threading
open System.Net

type DummyReverseClient() = 
    let logger = NLog.LogManager.GetLogger("DummyReverseClient")