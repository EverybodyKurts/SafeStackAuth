namespace SAFE

open System
open Fable.Remoting.Server
open Microsoft.AspNetCore.Http

module ErrorHandling =
    let rec getRealException (ex: Exception) : Exception =
        match ex with
        | :? AggregateException as ex -> getRealException ex.InnerException
        | _ -> ex

    let errorHandler<'a> (ex: Exception) (routeInfo: RouteInfo<HttpContext>) : ErrorResult =
        Propagate(getRealException ex)