module Server


open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Giraffe

open Shared

open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authorization;
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open System
open System.Threading.Tasks
open System.Security.Claims

let authenticate : HttpFunc -> HttpContext -> HttpFuncResult =
    requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme >=> text "please authenticate")


let webApp : HttpFunc -> HttpContext -> HttpFuncResult =
    let authenticated =
        warbler (fun _ -> requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme))

    choose [ Api.create Api.guestApi; authenticated >=> Api.create Api.authenticatedApi ]

let builder = WebApplication.CreateBuilder()
let domain = $"https://" + builder.Configuration["Auth0:Domain"] + "/"

type HasScopeRequirement (scope: string, issuer: string ) =
    do
        if String.IsNullOrEmpty(scope) then
            raise <| ArgumentNullException(nameof(scope))

        if String.IsNullOrEmpty(issuer) then
            raise <| ArgumentNullException(nameof(issuer))

    interface IAuthorizationRequirement

    member _.Scope = scope
    member _.Issuer = issuer

type HasScopeHander () =
    inherit AuthorizationHandler<HasScopeRequirement> ()

    override _.HandleRequirementAsync(context : AuthorizationHandlerContext, requirement : HasScopeRequirement) : Task =

        if (not <| context.User.HasClaim(fun claim -> claim.Type = "scope" && claim.Issuer = requirement.Issuer)) then
            Task.CompletedTask
        else
            let scopes = context.User.FindFirst(ClaimTypes.Role).Value.Split(' ')

            if scopes |> Array.contains requirement.Scope then
                context.Succeed(requirement)

            Task.CompletedTask

let app = application {
    use_jwt_authentication_with_config (fun options ->
        options.Authority <- domain
        options.Audience <- builder.Configuration["Auth0:Audience"]
        options.TokenValidationParameters <- TokenValidationParameters(
            NameClaimType = ClaimTypes.NameIdentifier
        )
    )
    service_config (fun services ->
        services.AddAuthorization(fun options ->
            options.AddPolicy("read:messages", fun policy ->
                policy.Requirements.Add(HasScopeRequirement("read:messages", domain))
            )
        ) |> ignore

        services.AddSingleton<IAuthorizationHandler, HasScopeHander>()
    )

    use_developer_exceptions
    use_router webApp
    memory_cache
    use_static "public"
    use_gzip
}

[<EntryPoint>]
let main _ =
    run app
    0