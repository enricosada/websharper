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

module WebSharper.Compiler.Assembler

module C = WebSharper.Core.JavaScript.Core
module I = WebSharper.Compiler.Inlining
module M = WebSharper.Compiler.Metadata
module Ma = WebSharper.Core.Macros
module P = WebSharper.Core.JavaScript.Packager
module Q = WebSharper.Core.Quotations
module R = WebSharper.Core.Reflection
module S = WebSharper.Core.JavaScript.Syntax
module V = WebSharper.Compiler.Validator

let Assemble (logger: Logger) (iP: I.Pool) mP (meta: M.T)
    (assembly: V.Assembly) =
    let remotingProviderNs =
        match assembly.RemotingProvider with
        | None -> ["WebSharper"; "Remoting"; "AjaxRemotingProvider"]
        | Some t ->
            let rec f acc (t: R.TypeDefinition) =
                match t.DeclaringType with
                | None ->
                    List.ofSeq ((defaultArg t.Namespace "").Split('.')) @ acc
                | Some t' -> f (t'.Name :: acc) t'
            f [t.Name] t
    let trans loc input =
        Translator.Translate logger iP mP remotingProviderNs meta loc input
    let visitCtor (c: V.Constructor) =
        match c.Kind with
        | V.InlineConstructor js ->
            match iP.Parse js with
            | I.Function f -> c.Slot.Method <- P.Syntax f
            | _ -> ()
        | V.JavaScriptConstructor q ->
            c.Slot.Method <-
                trans c.Location q
                |> Corrector.Correct (Corrector.Constructor c.Currying)
                |> C.Optimize
                |> P.Core
        | V.CoreConstructor x ->
            c.Slot.Method <- P.Core (C.Optimize x)
        | V.SyntaxConstructor x -> 
            c.Slot.Method <- P.Syntax x
        | _ -> ()
    let visitMethod (m: V.Method) =
        match m.Kind with
        | V.InlineMethod js ->
            match iP.Parse js with
            | I.Function f -> m.Slot.Method <- P.Syntax f
            | _  -> ()
        | V.JavaScriptMethod q ->
            m.Slot.Method <-
                trans m.Location q
                |> Corrector.Correct (Corrector.Method (m.Currying, m.Scope))
                |> C.Optimize
                |> P.Core
        | V.CoreMethod x ->
            m.Slot.Method <- P.Core (C.Optimize x)
        | V.SyntaxMethod x ->
            m.Slot.Method <- P.Syntax x
        | _ -> ()
    let visitProp (p: V.Property) =
        match p.Kind with
        | V.InlineModuleProperty js ->
            match iP.Parse js with
            | I.Function f -> p.Slot.Field <- P.Syntax f
            | _ -> ()
        | V.JavaScriptModuleProperty q ->
            p.Slot.Field <-
                trans p.Location q
                |> Corrector.Correct Corrector.Field
                |> C.Optimize
                |> P.Core
        | V.StubProperty -> ()
        | V.InterfaceProperty -> ()
        | V.FieldProperty _ -> ()
        | V.OptionalProperty _ -> ()
        | V.BasicProperty (g, s) ->
            Option.iter visitMethod g
            Option.iter visitMethod s
    let rec visitType (t: V.Type) =
        match t.Kind with
        | V.Class c ->
            match c.BaseClass with
            | Some bT ->
                match meta.DataType bT.DeclaringType with
                | Some (M.Class (addr, _, _)) -> c.Slot.BaseType <- Some addr
                | _ -> ()
            | _ -> ()
            List.iter visitCtor c.Constructors
        | _ -> ()
        List.iter visitMethod t.Methods
        List.iter visitProp t.Properties
        List.iter visitType t.Nested
    List.iter visitType assembly.Types
