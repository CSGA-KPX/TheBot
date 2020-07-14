[<RequireQualifiedAccess>]
module KPX.FsCqHttp.Config

[<RequireQualifiedAccess>]
module Logging = 
    let mutable LogEventPost = false
    let mutable LogApiCall = false
    let mutable LogCommandCall = true

[<RequireQualifiedAccess>]
module Command = 
    let CommandStart = "#"

[<RequireQualifiedAccess>]
module Output = 
    let mutable TextLengthLimit = 3000
    let mutable ImageOutputFont = "Sarasa Fixed CL"
    let mutable ImageOutputSize = 12.0f