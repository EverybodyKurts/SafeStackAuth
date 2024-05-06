module Auth

open System
open System.IO
open System.Security.Claims
open Microsoft.IdentityModel.JsonWebTokens

open Shared

let private algorithm : string = Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256

let private createPassPhrase () : byte array =
    let crypto = System.Security.Cryptography.RandomNumberGenerator.Create()
    let randomNumber = Array.init 32 byte
    crypto.GetBytes(randomNumber)
    randomNumber

let secret : string =
    let fi = FileInfo("./temp/token.txt")

    if not fi.Exists then
        let passPhrase = createPassPhrase ()

        if not fi.Directory.Exists then
            fi.Directory.Create()

        File.WriteAllBytes(fi.FullName, passPhrase)

    File.ReadAllBytes(fi.FullName) |> System.Text.Encoding.UTF8.GetString

let issuer = "safebookstore.io"

let generateToken (username: string) : string =
    let ``1 hour from now`` = DateTime.UtcNow.AddHours(1.0)

    [
        Claim(JwtRegisteredClaimNames.Sub, username)
        Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    ]
    |> Saturn.Auth.generateJWT (secret, algorithm) issuer ``1 hour from now``

let createUserData (login: Login) : UserData =
    {
        UserName = UserName login.UserName
        Token = generateToken login.UserName
    }

let login (login: Login) : UserData =
    if login.IsValid() then
        login |> createUserData
    else
        failwith $"User '{login.UserName}' can't be logged in"