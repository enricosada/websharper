// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2014 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

/// Tests Remoting functionality, including instance and static
/// remote functions, returning async, unit and sync values, and
/// sending/returning unions, lists, options, scalars and records.
namespace WebSharper.Web.Tests

open WebSharper
open WebSharper.JavaScript
module H = WebSharper.Html.Client.Tags

module Server =

    let counter = ref 0

    type OptionsRecord =
        {
            [<OptionalField>] x : option<int>
            y : option<int>
        }

    [<Remote>]
    let reset () =
        counter := 0
        async.Return ()

    [<Remote>]
    let sleep () =
        System.Threading.Thread.Sleep 100
        async.Return ()

    [<Remote>]
    let f1 () =
        incr counter

    [<Remote>]
    let f2 () =
        incr counter
        async { return () }

    [<Remote>]
    let f3 (x: int) =
        x + 1

    [<Remote>]
    let f4 (x: int) =
        async { return x + 1 }

    [<Remote>]
    let f5 (x: option<int>) =
        match x with
        | None   -> 0
        | Some x -> x + 1
        |> async.Return

    [<Remote>]
    let f6 (x: string) (y: string) =
        x + y
        |> async.Return

    [<Remote>]
    let f7 (x: string, y: string) =
        x + y
        |> async.Return

    [<Remote>]
    let f8 (xy: float * float) =
        fst xy + snd xy
        |> async.Return

    [<Remote>]
    let f9 (x: list<int>) =
        List.rev x
        |> async.Return

    [<Remote>]
    let f10 (x: System.DateTime) =
        x.AddDays 1.0
        |> async.Return

    [<Remote>]
    let f11 (x: int) =
        (x, x + 1)
        |> async.Return

    [<Remote>]
    let f12 (x: System.TimeSpan) min =
        x.Add(System.TimeSpan.FromMinutes min)
        |> async.Return

    [<Remote>]
    let add2_2ToMap m =
        m |> Map.add 2 2
        |> async.Return

    [<Remote>]
    let add2ToSet s =
        s |> Set.add 2
        |> async.Return

    type T1 =
        | A of int
        | B of int * T1

        [<JavaScript>]
        member this.Head =
            match this with
            | A x      -> x
            | B (x, _) -> x

    [<Remote>]
    let f13 x y =
        B (x, y)
        |> async.Return

    type T2 =
        {
            X : string
        }

        [<JavaScript>]
        member this.Body =
            this.X

    [<Remote>]
    let f14 x =
        { x with X = x.X + "!" }
        |> async.Return

    [<Remote>]
    let f15 (x: string) = async.Return x

    [<Remote>]
    let f16 (r: OptionsRecord) =
        async.Return { x = r.y; y = r.x }

    [<Remote>]
    let LoginAs (username: string) =
        let ctx = Web.Remoting.GetContext()
        async {
            do! ctx.UserSession.LoginUser(username)
            return! ctx.UserSession.GetLoggedInUser()
        }

    [<Remote>]
    let GetLoggedInUser () =
        let ctx = Web.Remoting.GetContext()
        async {
            return! ctx.UserSession.GetLoggedInUser()
        }

    [<Remote>]
    let Logout () =
        let ctx = Web.Remoting.GetContext()
        async {
            do! ctx.UserSession.Logout()
            return! ctx.UserSession.GetLoggedInUser()
        }

    [<JavaScript>]
    [<System.Serializable>]
    type BaseClass() =
        let mutable x = 0
        member this.Zero = x

    [<JavaScript>]
    [<System.Serializable>]
    type DescendantClass() =
        inherit BaseClass()
        let mutable x = 1
        member this.One = x

    [<Remote>]
    let f17 (x: DescendantClass) =
        if x.Zero = 0 && x.One = 1
        then Some (DescendantClass())
        else None
        |> async.Return

    [<Remote>]
    let reverse (x: string) =
        new System.String(Array.rev (x.ToCharArray()))
        |> async.Return

    type IHandler =

        [<Remote>]
        abstract member M1 : unit -> unit

        [<Remote>]
        abstract member M2 : unit -> Async<unit>

        [<Remote>]
        abstract member M3 : int -> Async<int>

        [<Remote>]
        abstract member M4 : int * int -> Async<int>

        [<Remote>]
        abstract member M5 : int -> int -> Async<int>

    type Handler() =
        interface IHandler with

            member this.M1() =
                incr counter

            member this.M2() =
                incr counter
                async.Return ()

            member this.M3 x =
                async.Return (x + 1)

            member this.M4 (a, b) =
                async.Return (a + b)

            member this.M5 a b =
                async.Return (a + b)

    do
        SetRpcHandlerFactory {
            new IRpcHandlerFactory with
                member this.Create t =
                    if t = typeof<IHandler> then
                        Some (Handler() :> obj)
                    else
                        None
        }

    [<Remote>]
    let count () = async.Return counter.Value

type Harness [<JavaScript>] () =
    let mutable expected = 0
    let mutable executed = 0
    let mutable passed = 0
    let mutable name = "?"

    [<JavaScript>]
    member this.Test t =
        name <- t

    [<JavaScript>]
    member this.AsyncEquals a b =
        expected <- expected + 1
        async {
            try
                let! v = a
                do executed <- executed + 1
                return
                    if v = b then
                        passed <- passed + 1
                        Console.Log("pass:", name)
                    else
                        Console.Log("fail:", name, v, b)
            with e ->
                return Console.Log("fail with exception:", name, e)
        }

    [<JavaScript>]
    member this.AsyncSatisfy a prop =
        expected <- expected + 1
        async {
            try
                let! v = a
                do executed <- executed + 1
                return
                    if prop v then
                        passed <- passed + 1
                        Console.Log("pass:", name)
                    else
                        Console.Log("fail:", name, v)
            with e ->
                return Console.Log("fail with exception:", name, e)
        }

    [<JavaScript>]
    member this.AssertEquals a b =
        expected <- expected + 1
        executed <- executed + 1
        if a = b then
            passed <- passed + 1
            Console.Log("pass:", name)
        else
            Console.Log("fail:", name, a, b)

    [<JavaScript>]
    member this.Summarize() =
        H.Div [
            H.Div [H.Text ("Expected: " + string expected)]
            H.Div [H.Text ("Executed: " + string executed)]
            H.Div [H.Text ("Passed: " + string passed)]
        ]

module RemotingTestSuite =

    [<JavaScript>]
    let harness = Harness()

    [<JavaScript>]
    let test n = harness.Test n

    [<JavaScript>]
    let satisfy a b = harness.AsyncSatisfy a b

    [<JavaScript>]
    let ( =? ) a b = harness.AsyncEquals a b

    [<JavaScript>]
    let ( =?!) a b = harness.AssertEquals a b

    [<JavaScript>]
    let RunTests (dom: WebSharper.JavaScript.Dom.Element) =
        Console.Log("Starting tests", dom)
        async {
            do test "unit -> unit"
            do! Server.reset()
            do Server.f1()
            do! Server.sleep ()
            do! Server.count() =? 1

            do test "unit -> Async<unit>"
            do! Server.f2()
            do! Server.count() =? 2

            do test "int -> int"
            do Server.f3 15 =?! 16

            do test "int -> Async<int>"
            do! Server.f4 8 =? 9

            do test "option<int> -> Async<int>"
            do! Server.f5 None =? 0
            do! Server.f5 (Some -40) =? -39

            do test "string -> string -> Async<string>"
            do! Server.f6 "a" "b" =? "ab"

            do test "string * string -> Async<string>"
            do! Server.f7 ("a", "b") =? "ab"

            do test "(float * float) -> Async<float>"
            do! Server.f8 (2.3, 4.5) =? 2.3 + 4.5

            do test "list<int> -> Async<list<int>>"
            do! Server.f9 [1; 2; 3] =? [3; 2; 1]
            do! satisfy (Server.f9 [1; 2; 3]) (fun list ->
                Array.ofSeq list = [| 3; 2; 1 |])

            do test "DateTime -> Async<DateTime>"
            let dt = System.DateTime.UtcNow
            do! Server.f10 dt =? dt.AddDays 1.0

            do test "int -> Async<int * int>"
            do! Server.f11 40 =? (40, 41)

            do test "TimeSpan -> float -> Async<TimeSpan>"
            let ts = System.TimeSpan.FromSeconds 14123.
            do! Server.f12 ts 1.25 =? ts.Add (System.TimeSpan.FromMinutes 1.25)

            do test "int -> T1 -> Async<T1>"
            do! Server.f13 40 (Server.B (8, Server.A 9)) =? 
                Server.B (40, Server.B (8, Server.A 9))
            do! satisfy (Server.f13 8 (Server.A 9)) (fun x ->
                x.Head = 8)

            do test "T2 -> Async<T2>"
            do! Server.f14 { X = "X" } =? { X = "X!" }
            do! satisfy (Server.f14 {X = "X"}) (fun x ->
                x.Body = "X!")

            do test "Null string"
            do! Server.f15 null =? null

            do test "{None; Some} -> {Some; None}"
            do! Server.f16 { x = None; y = Some 12 } =? { x = Some 12; y = None }

            do test "{Some; None} -> {None; Some}"
            do! Server.f16 { x = Some 12; y = None } =? { x = None; y = Some 12 }

            do test "Automatic field rename"
            do! satisfy (Server.f17 (Server.DescendantClass())) (fun x ->
                x |> Option.exists (fun x -> x.Zero = 0 && x.One = 1))

            do test "Map<int,int> -> Map<int,int>"
            do! Server.add2_2ToMap (Map.ofArray [| 1, 1 |]) =? Map.ofArray [| 1, 1; 2, 2 |]

            do test "Set<int> -> Set<int>"
            do! Server.add2ToSet (Set.ofArray [| 0; 1; 3; 4 |]) =? Set.ofArray [| 0 .. 4 |]

            do test "LoginUser()"
            do! Server.LoginAs("some_test_user") =? Some "some_test_user"
            do! Server.GetLoggedInUser() =? Some "some_test_user"

            do test "Logout()"
            do! Server.Logout() =? None
            do! Server.GetLoggedInUser() =? None

            do test "M1"
            do Remote<Server.IHandler>.M1()
            do! Server.sleep()
            do! Server.count() =? 3

            do test "M2"
            do! Remote<Server.IHandler>.M2()
            do! Server.count() =? 4

            do test "M3"
            do! Remote<Server.IHandler>.M3 40 =? 41

            do test "M4"
            do! Remote<Server.IHandler>.M4 (1, 2) =? 3

            do test "M5"
            do! Remote<Server.IHandler>.M5 3 6 =? 9

            do test "reverse"
            do! Server.reverse "abc#quit;;" =? ";;tiuq#cba"
            do! Server.reverse "c#" =? "#c"
            do! Server.reverse "\u00EF\u00BB\u00BF" =? "\u00BF\u00BB\u00EF"
            do! Server.reverse "c\127\127\127#" =? "#\127\127\127c"
            do ignore (dom.AppendChild (harness.Summarize().Dom))
        }
        |> Async.Start

type RemotingTests() =
    inherit Web.Control()

    [<JavaScript>]
    override this.Body =
        let d = H.Div []
        RemotingTestSuite.RunTests d.Dom
        d :> _
