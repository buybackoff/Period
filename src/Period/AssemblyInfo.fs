namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Period")>]
[<assembly: AssemblyProductAttribute("Period")>]
[<assembly: AssemblyDescriptionAttribute("The most convenient structure for financial and economic time series data, period.")>]
[<assembly: AssemblyVersionAttribute("0.0.1")>]
[<assembly: AssemblyFileVersionAttribute("0.0.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.1"
