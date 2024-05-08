namespace Shared

open System

type Todo = { Id: Guid; Description: string }

module Todo =
    let isValid (description: string) =
        String.IsNullOrWhiteSpace description |> not

    let create (description: string) = {
        Id = Guid.NewGuid()
        Description = description
    }

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type JWT = string

// Login credentials.
type Login = {
    UserName: string
    Password: string
} with

    member this.IsValid() =
        not (
            (this.UserName <> "test" || this.Password <> "test")
            && (this.UserName <> "test2" || this.Password <> "test2")
        )

type UserName =
    | UserName of string

    member this.Value =
        match this with
        | UserName v -> v

type UserData = { UserName: UserName; Token: JWT }

type ITodosApi = {
    getTodos: unit -> Async<Todo list>
    addTodo: Todo -> Async<Todo>
    login: Login -> Async<UserData>
}
 type AuthenticatedApi = {
    userProfile: unit -> Async<string>
 }