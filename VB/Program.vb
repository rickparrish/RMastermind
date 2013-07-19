Imports System.Text
Imports System.Windows.Forms

Module Program
    Private PegColours As Integer() = {Crt.DarkGray, Crt.LightBlue, Crt.White} 'Blank, Right Colour Right Place, Right Colour Wrong Place}
    Private PieceColours As Integer() = {Crt.DarkGray, Crt.LightGreen, Crt.LightCyan, Crt.LightRed, Crt.LightMagenta, Crt.Yellow, Crt.White} 'Blank, 6 Colours}
    Private PROG_VER As String = "RMastermind v10.09.08"

    Structure TLine
        Public Peg As Integer()
        Public Piece As Integer()
    End Structure

    Structure TStatusBar
        Public Current As Integer ' The index (1 based) of the current status bar }
        Public Count As Integer   ' The number of status bars }
    End Structure

    Private Answer(4) As Integer
    Private Colour As Integer
    Private CurLine As Integer
    Private CurPiece As Integer
    Private GameOver As Boolean
    Private Lines(10) As TLine
    Private StartTime As Date
    Private Status As TStatusBar

    Sub Main(ByVal args As String())
        ValidateRegistration()

        Status.Count = 3
        Status.Current = 0

        Door.MOREPrompts.ANSI = "|00 |1F                          Press Any Key To Continue                           "
        Door.MOREPrompts.ANSILength = 79
        Door.MOREPrompts.ASCII = "                           Press Any Key To Continue                           "
        AddHandler Door.OnCLP, AddressOf _OnCLP
        Door.OnHangUp = New Door.OnHangUpCallback(AddressOf _OnHangUp)
        Door.OnStatusBar = New Door.OnStatusBarCallback(AddressOf _OnStatusBar)
        Door.OnSysOpKey = New Door.OnSysOpKeyCallback(AddressOf _OnSysOpKey)
        Door.OnTimeOut = New Door.OnTimeOutCallback(AddressOf _OnTimeOut)
        Door.OnTimeOutWarning = New Door.OnTimeOutWarningCallback(AddressOf _OnTimeOutWarning)
        Door.OnTimeUp = New Door.OnTimeUpCallback(AddressOf _OnTimeUp)
        Door.OnTimeUpWarning = New Door.OnTimeUpWarningCallback(AddressOf _OnTimeUpWarning)
        Door.Startup(args)

        NewGame()
        ChangeMsg("Welcome to " + PROG_VER)

        Dim Ch As Char = Chr(0)
        Do While (Ch <> "Q")
            Dim TempCh As Char? = Door.ReadKey()
            If (TempCh Is Nothing) Then Continue Do

            Ch = Char.ToUpper(CChar(TempCh))

            If ((GameOver) And (Ch <> "H") And (Ch <> "N") And (Ch <> "Q")) Then Continue Do

            Select Case Ch
                Case "2"c : MoveColour(+1)
                Case "4"c : MovePiece(-1)
                Case "6"c : MovePiece(+1)
                Case "8"c : MoveColour(-1)
                Case "H"c
                    HighScores()
                    RedrawScreen()
                Case "N"c
                    If (GameOver) Then
                        NewGame()
                    Else
                        ChangeMsg("Finish This Game First")
                    End If
                Case " "c : GuessLine()
                Case Chr(13) : PlacePiece()
            End Select
        Loop
        HighScores()
        Door.Shutdown()
    End Sub

    ' Update the message line with a new MSG }
    Sub ChangeMsg(ByVal Msg As String)

        Door.GotoXY(26, 22)
        Door.TextAttr(31)
        Door.Write(StringUtils.PadRight(Msg, " "c, 54))
    End Sub

    ' Check TEMPLINE against the ANSWER, looking for right colour in right spot
    '  Return CNT which is the number of black pegs found }
    Function CheckBlackPegs(ByRef TempLine As TLine) As Integer
        Dim Cnt As Integer = 0
        For I As Integer = 0 To 3
            If (TempLine.Piece(I) = Answer(I)) Then
                TempLine.Peg(Cnt) = 1
                Cnt += 1
            End If
        Next
        Return Cnt
    End Function

    ' Check to see if we ran out of guesses }
    Sub CheckLost()
        If (CurLine = 9) Then
            GameOver = True
            ChangeMsg("Game Over, You Lose!")
            DrawAnswer()
        End If
    End Sub

    ' Check to see that the current line is valid
    '  It isn't if there are duplicate or blank pieces }
    Function CheckValid() As Boolean
        For I As Integer = 0 To 3
            If (Lines(CurLine).Piece(I) = 0) Then
                ChangeMsg("You Must Pick Four Colours")
                Return False
            End If

            For J As Integer = 0 To 3
                If ((Lines(CurLine).Piece(I) = Lines(CurLine).Piece(J)) And (I <> J)) Then
                    ChangeMsg("You Must Pick Four Unique Colours")
                    Return False
                End If
            Next
        Next
        Return True
    End Function


    ' Check TEMPLINE against the ANSWER, looking for right colour in wrong spot
    '  Return CNT - BLACK which is the number of white pegs found }
    Function CheckWhitePegs(ByRef TempLine As TLine, ByVal Black As Integer) As Integer
        Dim Cnt As Integer = Black

        If (Black < 4) Then
            For I As Integer = 0 To 3
                For J As Integer = 0 To 3
                    If ((TempLine.Piece(I) = Answer(J)) And (I <> J)) Then
                        TempLine.Peg(Cnt) = 2
                        Cnt += 1
                    End If
                Next
            Next
        End If

        Return Cnt - Black
    End Function

    ' Check to see if all four pegs are black }
    Function CheckWon() As Boolean
        For I As Integer = 0 To 3
            If (Lines(CurLine).Peg(I) <> 1) Then Return False
        Next

        GameOver = True
        ChangeMsg("Congratulations, You Win!")
        DrawAnswer()

        ' Save the high score
        Dim EndTime As Date = DateAndTime.Now
        Dim TS As TimeSpan = EndTime - StartTime
        Using DB As New RMSQLiteConnection("HighScores.sqlite", False)
            Dim SQL As String = ""
            SQL = "INSERT INTO HighScores (PlayerName, Seconds, RecordDate) VALUES ("
            SQL += DB.AddVarCharParameter(Door.DropInfo.Alias) + ", "
            SQL += DB.AddIntParameter(Convert.ToInt32(TS.TotalSeconds)) + ", "
            SQL += DB.AddDateTimeParameter(StartTime) + ") "
            DB.ExecuteNonQuery(SQL)
        End Using

        Return True
    End Function

    ' Draw the answer line }
    Sub DrawAnswer()
        For I As Integer = 0 To 3
            Door.GotoXY(4 + (I * 4), 2)
            Door.TextAttr(PieceColours(Answer(I)))
            Door.Write(Chr(&HDB) + Chr(&HDB))
        Next
    End Sub

    ' Highlight the currently selected colour }
    Sub DrawColour()
        Door.GotoXY(32, 8 + Colour)
        Door.TextAttr(PieceColours(Colour) + (7 * 16))
        Door.Write(Chr(&HFE) + Chr(&HFE))
    End Sub

    ' Draw the pegs for the current line }
    Sub DrawPegs()
        For I As Integer = 0 To 3
            Door.GotoXY(14 + I, 22 - (CurLine * 2))
            Door.TextAttr(PegColours(Lines(CurLine).Peg(I)))
            Door.Write(Chr(&HF9))
        Next
    End Sub

    ' Highlight the currently selected piece }
    Sub DrawPiece()
        Door.GotoXY(3 + (CurPiece * 2), 22 - (CurLine * 2))
        Door.TextAttr(7)
        Door.Write("[ ]")
        Door.GotoXY(4 + (CurPiece * 2), 22 - (CurLine * 2))
        Door.TextAttr(PieceColours(Lines(CurLine).Piece(CurPiece)))
        Door.Write(Chr(&HFE))
    End Sub

    ' Un-highlight the current colour }
    Sub EraseColour()
        Door.GotoXY(32, 8 + Colour)
        Door.TextAttr(PieceColours(Colour))
        Door.Write(Chr(&HFE) + Chr(&HFE))
    End Sub

    ' Un-highlight the current piece }
    Sub ErasePiece()
        Door.GotoXY(3 + (CurPiece * 2), 22 - (CurLine * 2))
        Door.TextAttr(0)
        Door.Write("   ")
        Door.GotoXY(4 + (CurPiece * 2), 22 - (CurLine * 2))
        Door.TextAttr(PieceColours(Lines(CurLine).Piece(CurPiece)))
        Door.Write(Chr(&HFE))
    End Sub

    ' Generate a random ANSWER line }
    Sub GenerateAnswer()
        Dim A As Integer() = {0, 1, 2, 3, 4, 5, 6}

        Dim R As New Random()
        For I As Integer = 0 To 3
            Dim Num As Integer = 0
            Do While (A(Num) = 0)
                Num = R.Next(1, 7)
            Loop
            A(Num) = 0
            Answer(I) = Num
        Next
    End Sub

    ' Guess the current line
    '  First validate, check for black pegs, white pegs,
    '  check for a win, for a loss }
    Sub GuessLine()
        If (CheckValid()) Then
            Dim TempLine As TLine = Lines(CurLine)
            Dim Black As Integer = CheckBlackPegs(TempLine)
            Dim White As Integer = CheckWhitePegs(TempLine, Black)
            Lines(CurLine).Peg = TempLine.Peg
            If (Not (CheckWon())) Then CheckLost()
            DrawPegs()
            If (GameOver) Then
                EraseColour()
                ErasePiece()
            Else
                ChangeMsg("You Scored " + Black.ToString() + " Black Peg(s) and " + White.ToString() + " White Peg(s)")
                ErasePiece()
                CurPiece = 0
                CurLine += 1
                DrawPiece()
            End If
        End If
    End Sub

    ' Show the high scores
    Sub HighScores()
        Door.TextAttr(7)
        Door.ClrScr()

        Door.GotoXY(25, 2)
        Door.Write("|0ERMastermind Top 10 Fastest Solves")
        Door.DrawBox(2, 3, 79, 14, Crt.White, Crt.Blue, CrtPanel.BorderStyle.SingleV)
        Door.GotoXY(4, 3)
        Door.Write("|1F###")
        Door.CursorRight(1)
        Door.Write("Player Name")
        Door.CursorRight(29)
        Door.Write("Seconds")
        Door.CursorRight(1)
        Door.Write("Date")
        Door.GotoXY(1, 4)

        Using DB As New RMSQLiteConnection("HighScores.sqlite", False)
            Dim I As Integer = 0
            DB.ExecuteReader("SELECT * FROM HighScores ORDER BY Seconds, RecordDate LIMIT 10")
            Do While (DB.Reader.Read())
                I += 1
                Door.CursorRight(3)
                Door.Write(StringUtils.PadLeft(I.ToString(), "0"c, 2) + ". ")
                Door.Write(StringUtils.PadRight(DB.Reader("PlayerName").ToString(), " "c, 40))
                Door.Write(StringUtils.PadLeft(DB.Reader("Seconds").ToString(), " "c, 5) + "   ")
                Door.Write(StringUtils.PadRight(DB.Reader("RecordDate").ToString(), " "c, 22))
                Door.WriteLn()
            Loop

            Do While (I < 10)
                I += 1
                Door.CursorRight(3)
                Door.WriteLn(StringUtils.PadLeft(I.ToString(), "0"c, 2) + ". ")
            Loop
        End Using

        Door.GotoXY(1, 16)
        Door.More()
    End Sub

    ' Move the current colour up or down (negative = up, positive = down) }
    Sub MoveColour(ByVal Offset As Integer)
        EraseColour()
        Colour = Colour + Offset
        If (Colour > 6) Then Colour = 1
        If (Colour < 1) Then Colour = 6
        DrawColour()
    End Sub

    ' Move the current piece left or right (negative=left, positive = right) }
    Sub MovePiece(ByVal Offset As Integer)
        ErasePiece()
        CurPiece = CurPiece + Offset
        If (CurPiece > 3) Then CurPiece = 0
        If (CurPiece < 0) Then CurPiece = 3
        DrawPiece()
    End Sub

    ' Start a new game }
    Sub NewGame()
        Colour = 1
        GameOver = False
        CurLine = 0
        CurPiece = 0
        For I As Integer = 0 To Lines.Length - 1
            Array.Resize(Lines(I).Peg, 4)
            Array.Resize(Lines(I).Piece, 4)
            For J As Integer = 0 To 3
                Lines(I).Peg(J) = 0
                Lines(I).Piece(J) = 0
            Next
        Next
        StartTime = DateTime.Now
        RedrawScreen()
        GenerateAnswer()
#If DEBUG Then
        DrawAnswer()
#End If
    End Sub

    ' Place the currently selected colour at the current place on the board }
    Sub PlacePiece()
        Lines(CurLine).Piece(CurPiece) = Colour
        MovePiece(+1)
    End Sub

    ' Redraw the pieces and pegs on the board }
    Sub RedrawBoard()
        For Y As Integer = 0 To 9
            For X As Integer = 0 To 3
                Door.GotoXY(4 + (X * 2), 22 - (Y * 2))
                Door.TextAttr(PieceColours(Lines(Y).Piece(X)))
                Door.Write(Chr(&HFE))
                Door.GotoXY(14 + X, 22 - (Y * 2))
                Door.TextAttr(PegColours(Lines(Y).Peg(X)))
                Door.Write(Chr(&HF9))
            Next
        Next
    End Sub

    ' Redraw the entire screen }
    Sub RedrawScreen()
        ShowMainAns()
        RedrawBoard()
        DrawPiece()
        DrawColour()
    End Sub

    ' Display the main ANSI }
    Sub ShowMainAns()
        Door.Write(Encoding.Default.GetString(My.Resources.MAIN_ANS))
    End Sub

    Sub ValidateRegistration()
        Dim Ini As New IniFile(Application.StartupPath + "\\RMastermind.ini")
        Dim Name As String = Ini.ReadString("Registration", "Name", "")
        Dim Code As String = Ini.ReadString("Registration", "Code", "INVALID")

        Dim ValidKey As String = StringUtils.MD5("asdf", Name, "jkl;")
        If (Code.ToLower() <> ValidKey.ToLower()) Then
            Crt.ClrScr()
            Crt.WriteLn("Invalid Registration Key:")
            Crt.WriteLn("  Got ........ '" + Code + "'")
            Crt.WriteLn("  Expected ... '" + ValidKey + "'")
            Crt.WriteLn("Fix your RMastermind.ini to continue")
            Crt.ReadKey()
            Environment.Exit(1)
        End If
    End Sub

    ' Event for when an unknown command-line parameter is found }
    Sub _OnCLP(sender As Object, e As CommandLineParameterEventArgs)
        ' This function is useless in this program, but I included it so you
        '  could see how it's used.  In something like an IRC client where you
        '  want to be able to specify what server to connect to, it would be
        '  useful.  The user could pass -Slocalhost, and this function
        '  would get an AKey value of "S" and an AValue value of "localhost" }
        Select Case e.Key
            Case "X"c : Crt.WriteLn("Parameter -X passed with value of: " + e.Value)
        End Select
    End Sub

    ' Event for when the user drops carrier }
    Sub _OnHangUp()
        ChangeMsg("Caller Dropped Carrier")
        System.Threading.Thread.Sleep(2500)
        Environment.Exit(0)
    End Sub

    ' Event for when the status bar needs updating }
    Sub _OnStatusBar()
        Select Case Status.Current
            Case 0
                Crt.FastWrite("þ                       þ F1=HELP þ RMASTERMIND þ Idle:       þ Left:          þ", 1, 25, 30)
                Crt.FastWrite(StringUtils.PadRight(Door.DropInfo.RealName, " "c, 21), 3, 25, 31)
                Crt.FastWrite("F1=HELP", 27, 25, 31)
                Crt.FastWrite("RMASTERMIND", 37, 25, 31)
                Crt.FastWrite(StringUtils.PadRight("Idle: " + StringUtils.SecToMS(Door.TimeIdle()), " "c, 11), 51, 25, 31)
                Crt.FastWrite("Left: " + StringUtils.SecToHMS(Door.TimeLeft()), 65, 25, 31)
            Case 1
                Crt.FastWrite("þ F1: Toggle StatusBar þ Alt-C: SysOp Chat þ Alt-H: Hang-Up þ Alt-K: Kick User þ", 1, 25, 30)
                Crt.FastWrite("F1: Toggle StatusBar", 3, 25, 31)
                Crt.FastWrite("Alt-C: SysOp Chat", 26, 25, 31)
                Crt.FastWrite("Alt-H: Hang-Up", 46, 25, 31)
                Crt.FastWrite("Alt-K: Kick User", 63, 25, 31)
            Case 2
                Crt.FastWrite(StringUtils.PadRight(" " + PROG_VER + " is an R&M Door example program - http://www.randm.ca/", " "c, 80), 1, 25, 31)
        End Select
    End Sub

    ' Event for when the sysop hits a key locally }
    Function _OnSysOpKey(ByVal AKey As Char) As Boolean
        Select Case AKey
            Case Chr(&H2E) 'ALT-C
                Door.TextAttr(7)
                Door.ClrScr()
                Door.WriteLn("The SysOp wants to speak to you!  (Press ESC to leave chat)")
                Door.WriteLn()
                Door.SysOpChat()
                RedrawScreen()
                Return True
            Case Chr(&H23) 'ALT-H
                ChangeMsg("You Are Unworthy (The SysOp Has Disconnected You)")
                System.Threading.Thread.Sleep(2500)
                Door.Disconnect()
                Door.Shutdown()
                Environment.Exit(0)
                Return True
            Case Chr(&H25) 'ALT-K
                ChangeMsg("You Are Unworthy (The SysOp Has Kicked You)")
                System.Threading.Thread.Sleep(2500)
                Door.Shutdown()
                Environment.Exit(0)
                Return True
            Case Chr(&H3B) 'F1
                If (Status.Current = Status.Count - 1) Then
                    Status.Current = 0
                Else
                    Status.Current += 1
                End If
                If (Door.OnStatusBar <> Nothing) Then Door.OnStatusBar()
                Return True
        End Select

        Return False
    End Function

    ' Event for when the user idles too long }
    Sub _OnTimeOut()
        ChangeMsg("Come Back When You're Awake (Idle Limit Exceeded)")
        System.Threading.Thread.Sleep(2500)
        Environment.Exit(0)
    End Sub

    ' Event for when the user needs an idle warning }
    Sub _OnTimeOutWarning(ByVal AMinutes As Integer)
        ChangeMsg(AMinutes.ToString() + " Minutes Until You're Kicked For Idling")
    End Sub

    ' Event for when the user runs out of time }
    Sub _OnTimeUp()
        ChangeMsg("Come Back When You Have More Time (Ran Out Of Time")
        System.Threading.Thread.Sleep(2500)
        Environment.Exit(0)
    End Sub

    ' Event for when the user needs a time warning }
    Sub _OnTimeUpWarning(ByVal AMinutes As Integer)
        ChangeMsg(AMinutes.ToString() + " Minutes Remaining This Call")
    End Sub
End Module
