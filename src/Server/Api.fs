module Api

open Microsoft.AspNetCore.Http
open System
open System.Security.Claims

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe

open SAFE
open Shared

let create (api: HttpContext -> 'a) : HttpFunc -> HttpContext -> HttpFuncResult =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromContext api
    |> Remoting.withErrorHandler ErrorHandling.errorHandler
    |> Remoting.buildHttpHandler


let guestApi ctx = {
    getTodos = fun () -> async { return Storage.todos |> List.ofSeq }
    addTodo =
        fun todo -> async {
            return
                match Storage.addTodo todo with
                | Ok() -> todo
                | Error e -> failwith e
        }

    login = fun user -> async { return Auth.login user }
}

let authenticatedApi (context: HttpContext) : AuthenticatedApi =
    let claims = context.User.Claims |> Seq.map _.Value |> String.concat ", "
    {
        userProfile = fun () -> async { return claims }
    }