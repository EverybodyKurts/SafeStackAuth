module Storage

open Shared

let todos = ResizeArray()

let addTodo (todo: Todo) : Result<unit, string> =
    if Todo.isValid todo.Description then
        todos.Add todo
        Ok()
    else
        Error "Invalid todo"

do
    addTodo (Todo.create "Create new SAFE project") |> ignore
    addTodo (Todo.create "Write your app") |> ignore
    addTodo (Todo.create "Ship it!!!") |> ignore