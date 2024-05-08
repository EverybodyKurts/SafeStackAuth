module Server


open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Giraffe

open Shared

open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open System.Security.Claims

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

let app = application {
    use_jwt_authentication Auth.secret Auth.issuer
    use_router webApp
    memory_cache
    use_static "public"
    use_gzip
}

[<EntryPoint>]
let main _ =
    run app
    0