module Server


open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Giraffe

open Shared

open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open System
open System.Security.Claims
open System.Threading.Tasks

let authenticate : HttpFunc -> HttpContext -> HttpFuncResult =
    requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme >=> text "please authenticate")


let webApp : HttpFunc -> HttpContext -> HttpFuncResult =
    let authenticated =
        warbler (fun _ -> requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme))

    choose [ Api.create Api.guestApi; authenticated >=> Api.create Api.authenticatedApi ]

type Saturn.Application.ApplicationBuilder with

    /// Adds token authentication to the application.
    /// Taken from: https://www.azurefromthetrenches.com/token-authentication-with-fsharp-safe-and-fable-remoting/
    [<CustomOperation("use_token_authentication")>]
    member __.UseTokenAuthentication (state: ApplicationState) : ApplicationState =
        let middleware (app: IApplicationBuilder) =
            app.UseAuthentication()

        let service (s : IServiceCollection) =
            s.AddAuthentication(fun options ->
                options.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
                options.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme
            ).AddJwtBearer(fun options ->
                // The address of the token-issuing authentication server.
                // The JWT bearer authentication middleware will use this URI to find and retrieve the public key that can be used to validate the token’s signature.
                // It will also confirm that the iss parameter in the token matches this URI.
                options.Authority <- "https://certificateissuer.example.com"
                // The intended recipient of the incoming token or the resource that the token grants access to.
                // If the value specified in this parameter doesn’t match the aud parameter in the token, the token will be rejected because it was meant to be used for accessing a different resource.
                // Note that different security token providers have different behaviors regarding what is used as the ‘aud’ claim (some use the URI of a resource a user wants to access, others use scope names).
                // Be sure to use an audience that makes sense given the tokens you plan to accept.
                options.Audience <- "https://localhost:8080"
                options.TokenValidationParameters <- TokenValidationParameters(
                    NameClaimType = ClaimTypes.NameIdentifier
                    // ValidateIssuer = true,
                    // ValidateAudience = true
                )
            ) |> ignore

            s

        { state with
            ServicesConfig = service::state.ServicesConfig
            AppConfigs     = middleware::state.AppConfigs
            CookiesAlreadyAdded = true
        }

let builder = WebApplication.CreateBuilder()
let domain = $"https://" + builder.Configuration["Auth0:Domain"] + "/"

type HasScopeRequirement (scope: string, issue: string) =
    do
        if String.IsNullOrEmpty(scope) then
            raise <| ArgumentNullException(nameof(scope))

        if String.IsNullOrEmpty(issue) then
            raise <| ArgumentNullException(nameof(issue))

    interface IAuthorizationRequirement

    member _.Scope = scope
    member _.Issuer = issue

type HasScopeHandler () =
    inherit AuthorizationHandler<HasScopeRequirement>()

    override _.HandleRequirementAsync (context: AuthorizationHandlerContext, requirement: HasScopeRequirement) : Task =
        if not <| context.User.HasClaim(fun claim -> claim.Type = "scope" && claim.Issuer = requirement.Issuer) then
            Task.CompletedTask
        else
            let scopes =
                context.User.FindFirst(fun c ->
                    c.Type = "scope" &&
                    c.Issuer = requirement.Issuer
                ).Value.Split(' ')

            if scopes |> Array.contains requirement.Scope then
                context.Succeed(requirement)

            Task.CompletedTask

let app =
    application {
        use_jwt_authentication_with_config (fun options ->
            options.Authority <- domain
            options.Audience <- builder.Configuration["Auth0:Audience"]

            options.TokenValidationParameters <- TokenValidationParameters(
                NameClaimType = ClaimTypes.NameIdentifier
            )
        )

        service_config (fun services ->
            services.AddAuthorization(fun auth ->
                auth.AddPolicy("read:messages", fun policy ->
                    policy.Requirements.Add(HasScopeRequirement("read:messages", domain))
                )
            ) |> ignore

            services.AddSingleton<IAuthorizationHandler, HasScopeHandler>()
        )

        use_router webApp
        memory_cache
        use_static "public"
        use_gzip
    }

[<EntryPoint>]
let main _ =
    run app
    0