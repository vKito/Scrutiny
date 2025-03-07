﻿namespace UsageExample.Playwright

open System
open Microsoft.Playwright
open Scrutiny
open Scrutiny.Operators
open Scrutiny.Scrutiny

open Xunit

type GlobalState(page: IPage, logger: string -> unit) =
    member val Logger = logger with get
    member val Page = page with get
    member val IsSignedIn = false with get, set
    member val Username = "MyUsername" with get, set
    member val Number = 42

    member x.GetInputValueAsync (selector: string) =
        task {
            let! element = x.Page.QuerySelectorAsync(selector)
            let! value = element.EvaluateAsync("e => e.value")
            return value.ToString()
        }

type LoggedInComment() =
    member val Comment = String.Empty with get, set

module rec ScrutinyStateMachine =
    let signIn =
        fun (globalState: GlobalState) ->
            page {
                name "Sign In"
                onEnter (fun _ ->
                    globalState.Logger "Checking on page sign in"
                    (task {
                        globalState.Logger "Sign in: Looking for header text"
                        let! headerText = globalState.Page.InnerTextAsync("#header") 
                        Assert.Equal("Sign In", headerText)
                    }).GetAwaiter().GetResult()
                )

                transition ((fun _ ->
                    (task {
                        globalState.Logger "Sign in: Clicking on home"
                        do! globalState.Page.ClickAsync("#home")
                    }).GetAwaiter().GetResult()
                ) ==> home)

                transition
                    ((fun _ ->
                        (task {
                            globalState.Logger "Sign in: Filling in username"
                            globalState.Username <- "kaeedo"
                            do! globalState.Page.FillAsync("#username", globalState.Username)

                            globalState.Logger "Sign in: Filling in number"
                            do! globalState.Page.FillAsync("#number", globalState.Number.ToString())

                            globalState.IsSignedIn <- true

                            globalState.Logger "Sign in: clicking text=sign in"
                            do! globalState.Page.ClickAsync("css=button >> text=Sign In")
                        }).GetAwaiter().GetResult()
                    ) ==> loggedInHome)

                action (fun _ ->
                    (task {
                        globalState.Logger "Sign in: filling username"
                        do! globalState.Page.FillAsync("#username", "MyUsername")

                        globalState.Logger "Sign in: getting username"
                        
                        let! username = globalState.GetInputValueAsync("#username")
                        Assert.Equal("MyUsername", username)
                    }).GetAwaiter().GetResult()
                )
                action (fun _ ->
                    (task {
                        globalState.Logger "Sign in: filling number"
                        do! globalState.Page.FillAsync("#number", "42") 

                        globalState.Logger "Sign in: getting number"
                        let! number = globalState.GetInputValueAsync("#number")
                        Assert.Equal("42", number)
                    }).GetAwaiter().GetResult()
                )

                action (fun _ ->
                    (task {
                        let! username = globalState.GetInputValueAsync("#username")
                        let! number = globalState.GetInputValueAsync("#number")

                        let signInButtonSelector = "css=button >> text=Sign In"

                        if String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(number) then
                            do! globalState.Page.ClickAsync(signInButtonSelector) 
                        else
                            do! globalState.Page.FillAsync("#username", String.Empty) 
                            do! globalState.Page.ClickAsync(signInButtonSelector) 
                        
                        let! errorMessage = globalState.Page.QuerySelectorAsync("#ErrorMessage") 
                        Assert.NotNull(errorMessage)
                        let! displayState = errorMessage.EvaluateAsync("e => e.style.display")

                        Assert.False(displayState.ToString() = "none")
                    }).GetAwaiter().GetResult()
                )

                onExit (fun _ -> globalState.Logger "Exiting sign in")
            }

    let loggedInComment =
        fun (globalState: GlobalState) ->

            page {
                name "Logged In Comment"

                localState (LoggedInComment())

                transition ((fun _ ->
                    (task{
                        do! globalState.Page.ClickAsync("#home")
                    }).GetAwaiter().GetResult()
                ) ==> loggedInHome)

                action (fun ls ->
                    (task {
                        do! globalState.Page.ClickAsync("#openModal")

                        ls.Comment <- "This is my super comment"

                        do! globalState.Page.FillAsync("#comment", ls.Comment)
                        do! globalState.Page.ClickAsync("#modalFooterSave")
                    }).GetAwaiter().GetResult()
                )

                onEnter (fun _ ->
                    globalState.Logger "Checking comment is logged in"
                    (task {
                        let! openModal = globalState.Page.QuerySelectorAsync("#openModal")
                        Assert.NotNull(openModal)
                        let! displayState = openModal.EvaluateAsync("e => e.style.display")

                        Assert.False(displayState.ToString() = "none")
                    }).GetAwaiter().GetResult()
                )

                onExit (fun ls -> 
                    (task {
                        let! comments = globalState.Page.QuerySelectorAllAsync("#commentsUl>li")
                        let comments = comments |> List.ofSeq

                        let writtenComment =
                            comments
                            |> List.tryFind(fun c ->
                                (task {
                                    let! text = c.InnerTextAsync()
                                    return text = sprintf "%s wrote:\n%s" globalState.Username ls.Comment
                                }).GetAwaiter().GetResult()
                            )

                        Assert.True(writtenComment.IsSome)
                    }).GetAwaiter().GetResult()
                    globalState.Logger "Exiting comment logged in"
                )
            }

    let loggedInHome =
        fun (globalState: GlobalState) ->
            page {
                name "Logged in Home"

                transition ((fun _ ->
                    (task {
                        do! globalState.Page.ClickAsync("#comment")
                    }).GetAwaiter().GetResult()
                ) ==> loggedInComment)
                transition ((fun _ ->
                    (task {
                        do! globalState.Page.ClickAsync("#logout")
                    }).GetAwaiter().GetResult()
                ) ==> home)

                onEnter (fun _ ->
                    globalState.Logger "Checking on page home logged in"

                    (task {
                        let! welcomeText = globalState.Page.QuerySelectorAsync("#welcomeText")
                        Assert.NotNull(welcomeText)
                        let! displayState = welcomeText.EvaluateAsync("e => e.style.display")

                        Assert.False(displayState.ToString() = "none")

                        let! welcomeText = globalState.Page.InnerTextAsync("#welcomeText")

                        Assert.Equal(sprintf "Welcome %s" globalState.Username, welcomeText);
                    }).GetAwaiter().GetResult()
                )

                exitAction (fun _ ->
                    globalState.Logger "Exiting!"
                    (task {
                        do! globalState.Page.ClickAsync("#logout")
                    }).GetAwaiter().GetResult()
                )
            }

    let comment =
        fun (globalState: GlobalState) ->
            page {
                name "Comment"
                onEnter (fun _ ->
                    globalState.Logger "Checking on page comment"

                    (task {
                        let! headerText = globalState.Page.InnerTextAsync("#header") 
                        Assert.Equal("Comments", headerText)
                    }).GetAwaiter().GetResult()
                )

                transition ((fun _ ->
                    (task {
                        do! globalState.Page.ClickAsync("#home")
                    }).GetAwaiter().GetResult()
                ) ==> home)
                transition ((fun _ ->
                    (task {
                        do! globalState.Page.ClickAsync("#signin")
                    }).GetAwaiter().GetResult()
                ) ==> signIn)

                onExit (fun _ -> globalState.Logger "Exiting comment")
            }

    let home =
        fun (globalState: GlobalState) ->
            page {
                name "Home"
                onEnter (fun _ ->
                    globalState.Logger "Checking on page home"

                    (task {
                        let! headerText = globalState.Page.InnerTextAsync("#header") 
                        Assert.Equal("Home", headerText)
                    }).GetAwaiter().GetResult()
                )

                transition ((fun _ ->
                    (task {
                        do! globalState.Page.ClickAsync("#comment") 
                    }).GetAwaiter().GetResult()
                ) ==> comment)
                transition ((fun _ ->
                    (task {
                        do! globalState.Page.ClickAsync("#signin") 
                    }).GetAwaiter().GetResult()
                ) ==> signIn)

                onExit (fun _ ->
                    globalState.Logger "Exiting home"
                )
            }
