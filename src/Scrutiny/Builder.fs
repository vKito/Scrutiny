﻿namespace rec Scrutiny

open System

type ScrutinizeState =
    { Pages: PageState seq
      CurrentState: PageState
      EntryFunction: unit -> unit -> PageState
      EntryPage: PageBuilder }

//[<CustomComparison; CustomEquality>]
type PageState =
    { Name: string
      EntryCheck: unit -> unit
      Transitions: List<((unit -> unit) * (unit -> PageState))>
      Actions: List<unit -> unit>
      // DesiredOutcome: List of actions transition to PageState?
      Exit: unit -> unit }

    (*interface IComparable<PageState> with
        member this.CompareTo other =
            compare this.Name other.Name
    interface IComparable with
        member this.CompareTo obj =
            match obj with
            | null -> 1
            | :? PageState as other -> (this :> IComparable<_>).CompareTo other
            | _ -> invalidArg "obj" "not a PageState"

    interface IEquatable<PageState> with
        member this.Equals other =
            this.Name = other.Name

    override this.Equals obj =
        match obj with
          | :? PageState as other -> (this :> IEquatable<_>).Equals other
          | _ -> false
    override this.GetHashCode() =
        hash this.Name*)

type PageBuilder() =
    member __.Yield(_): PageState =
        { PageState.Name = ""
          EntryCheck = fun _ -> ()
          Transitions = []
          Actions = []
          Exit = fun _ -> () }

    [<CustomOperation("name")>]
    member __.Name(state, handler): PageState =
        { state with Name = handler }

    [<CustomOperation("entryCheck")>]
    member __.EntryCheck(state, handler): PageState =
        { state with EntryCheck = handler }

    [<CustomOperation("transition")>]
    member __.Transitions(state, handler): PageState =
        { state with Transitions = handler :: state.Transitions }

    [<CustomOperation("action")>]
    member __.Actions(state, handler): PageState = 
        { state with Actions = handler :: state.Actions }

    [<CustomOperation("exitFunction")>]
    member __.ExitFunction(state, handler): PageState =
        { state with Exit = handler }

type ClickFlowBuilder() =
    member __.Yield(_): ScrutinizeState =
        let defaultState =
            { PageState.Name = ""
              EntryCheck = fun _ -> ()
              Transitions = []
              Actions = []
              Exit = fun _ -> () }

        { ScrutinizeState.Pages = []
          CurrentState = defaultState
          EntryFunction = fun _ -> fun _ -> defaultState
          EntryPage = new PageBuilder() }

    [<CustomOperation("entryFunction")>]
    member __.EntryFunction(state, handler) =
        { state with EntryFunction = handler }

    [<CustomOperation("pages")>]
    member __.Pages(state, handler) =
        { state with Pages = handler }

module Scrutiny =
    let timer = System.Diagnostics.Stopwatch()
    let clickFlow = ClickFlowBuilder()
    let page = PageBuilder()
    let scrutinize (startFn: unit -> unit -> PageState) =
        let random = new Random()

        let randomTransition (transitions: List<(unit -> unit) * (unit -> PageState)>) =
            let upper = transitions |> Seq.length
            let randomIndex = random.Next(upper)

            let transition, nextState = transitions.[randomIndex]
            transition() |> ignore
            nextState()
            
        let rec clickAround next =
            timer.Restart()
            next.EntryCheck()
            printfn "Entry check took: %ims" timer.ElapsedMilliseconds
            let chance = random.NextDouble()
            printfn "Chance: %f " chance
            match chance > 0.9 with
            | true -> next.Exit()
            | false ->
                timer.Restart()
                let possibleTransitions = next.Transitions 
                let nextState = randomTransition possibleTransitions
                printfn "Navigatin to nextstate took: %ims" timer.ElapsedMilliseconds
                clickAround nextState

        let nextState = startFn()
        clickAround (nextState())
