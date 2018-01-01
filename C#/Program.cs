using System;
using RandM.RMLib;
using RMastermind.Properties;
using System.Threading;
using System.IO;

namespace RandM.RMastermind {
    class Program {
        // Random 256 bit hex strings to salt registration hash
        static string _PreSalt = "d0bffeb7d3eb800cd834318a868c378db1cf11d5b0a11b3a253ac95bad01307d";
        static string _PostSalt = "96940bf4b09e51303ba6e64e46280620cdb5b14fb500914ad2aae2ffbc4a9985";

        static int[] PegColours = { Crt.DarkGray, Crt.LightBlue, Crt.White }; //Blank, Right Colour Right Place, Right Colour Wrong Place}
        static int[] PieceColours = { Crt.DarkGray, Crt.LightGreen, Crt.LightCyan, Crt.LightRed, Crt.LightMagenta, Crt.Yellow, Crt.White }; //Blank, 6 Colours}
        static string PROG_VER = "RMastermind v18.01.01";

        struct TLine {
            public int[] Peg;
            public int[] Piece;
        }

        struct TStatusBar {
            public int Current; // The index (1 based) of the current status bar }
            public int Count;   // The number of status bars }
        }

        static int[] Answer = new int[4];
        static int Colour;
        static int CurLine;
        static int CurPiece;
        static bool GameOver;
        static TLine[] Lines = new TLine[10];
        static DateTime StartTime;
        static TStatusBar Status;

        static void Main(string[] args) {
            try {
                try {
                    RMLog.Handler += RMLog_Handler;
                    ValidateRegistration();

                    Status.Count = 3;
                    Status.Current = 0;

                    Door.MOREPrompt.ANSI = "|00 |1F                          Press Any Key To Continue                           ";
                    Door.MOREPrompt.ANSILength = 79;
                    Door.MOREPrompt.ASCII = "                           Press Any Key To Continue                           ";
                    Door.OnCLP += new EventHandler<CommandLineParameterEventArgs>(_OnCLP);
                    Door.OnHangUp += Door_OnHangUp;
                    Door.OnStatusBar += Door_OnStatusBar;
                    Door.OnSysOpKey += Door_OnSysOpKey;
                    Door.OnTimeOut += Door_OnTimeOut;
                    Door.OnTimeOutWarning += Door_OnTimeOutWarning;
                    Door.OnTimeUp += Door_OnTimeUp;
                    Door.OnTimeUpWarning += Door_OnTimeUpWarning;
                    Door.Startup();

                    NewGame();
                    ChangeMsg("Welcome to " + PROG_VER);

                    char Ch = '\0';
                    do {
                        char? TempCh = Door.ReadKey();
                        if (TempCh == null)
                            continue;

                        Ch = Char.ToUpper((char)TempCh);

                        if ((GameOver) && (Ch != 'N') && (Ch != 'Q'))
                            continue;

                        switch (Ch) {
                            case Door.ExtendedKeys.DownArrow:
                            case '2':
                                MoveColour(+1);
                                break;
                            case Door.ExtendedKeys.LeftArrow:
                            case '4':
                                MovePiece(-1);
                                break;
                            case Door.ExtendedKeys.RightArrow:
                            case '6':
                                MovePiece(+1);
                                break;
                            case Door.ExtendedKeys.UpArrow:
                            case '8':
                                MoveColour(-1);
                                break;
                            case 'H':
                                HighScores();
                                RedrawScreen();
                                break;
                            case 'N':
                                if (GameOver) { NewGame(); } else { ChangeMsg("Finish This Game First"); }
                                break;
                            case 'Q':
                                break; // Must have or "Q is not a valid choice!" will be displayed
                            case ' ':
                                GuessLine();
                                break;
                            case '\r':
                                PlacePiece();
                                break;
                            default:
                                ChangeMsg(Ch + " is not a valid choice!");
                                break;
                        }
                    } while (Ch != 'Q');
                    HighScores();
                } catch (Exception ex) {
                    File.AppendAllText("ex.log", ex.ToString() + Environment.NewLine);
                }
            } finally {
                Door.Shutdown();
            }
        }

        static void RMLog_Handler(object sender, RMLogEventArgs e) {
            File.AppendAllText("rmlog.log", e.Message + Environment.NewLine);
        }

        // Update the message line with a new MSG }
        static void ChangeMsg(string Msg) {
            Door.GotoXY(26, 22);
            Door.TextAttr(31);
            Door.Write(StringUtils.PadRight(Msg, ' ', 54));
        }

        // Check TEMPLINE against the ANSWER, looking for right colour in right spot
        //  Return CNT which is the number of black pegs found }
        static int CheckBlackPegs(ref TLine TempLine) {
            int Cnt = 0;
            for (int i = 0; i < 4; i++) {
                if (TempLine.Piece[i] == Answer[i]) {
                    TempLine.Peg[Cnt] = 1;
                    Cnt++;
                }
            }
            return Cnt;
        }

        // Check to see if we ran out of guesses }
        static void CheckLost() {
            if (CurLine == 9) {
                GameOver = true;
                ChangeMsg("Game Over, You Lose!");
                DrawAnswer();
            }
        }

        // Check to see that the current line is valid
        //  It isn't if there are duplicate or blank pieces }
        static bool CheckValid() {
            for (int i = 0; i < 4; i++) {
                if (Lines[CurLine].Piece[i] == 0) {
                    ChangeMsg("You Must Pick Four Colours");
                    return false;
                }
                for (int j = 0; j < 4; j++) {
                    if ((Lines[CurLine].Piece[i] == Lines[CurLine].Piece[j]) && (i != j)) {
                        ChangeMsg("You Must Pick Four Unique Colours");
                        return false;
                    }
                }
            }
            return true;
        }


        // Check TEMPLINE against the ANSWER, looking for right colour in wrong spot
        //  Return CNT - BLACK which is the number of white pegs found }
        static int CheckWhitePegs(ref TLine TempLine, int Black) {
            int Cnt = Black;
            if (Black < 4) {
                for (int i = 0; i < 4; i++) {
                    for (int j = 0; j < 4; j++) {
                        if ((TempLine.Piece[i] == Answer[j]) && (i != j)) {
                            TempLine.Peg[Cnt] = 2;
                            Cnt++;
                        }
                    }
                }
            }
            return Cnt - Black;
        }

        // Check to see if all four pegs are black }
        static bool CheckWon() {
            for (int i = 0; i < 4; i++) {
                if (Lines[CurLine].Peg[i] != 1)
                    return false;
            }

            GameOver = true;
            ChangeMsg("Congratulations, You Win!");
            DrawAnswer();

            // Save the high score
            DateTime EndTime = DateTime.Now;
            TimeSpan TS = EndTime - StartTime;

            // TODOX SQLite code needs updating
            //using (RMSQLiteConnection DB = new RMSQLiteConnection("HighScores.sqlite", false)) {
            //    string SQL = "";
            //    SQL = "INSERT INTO HighScores (PlayerName, Seconds, RecordDate) VALUES (";
            //    SQL += DB.AddVarCharParameter(Door.DropInfo.Alias) + ", ";
            //    SQL += DB.AddIntParameter((int)TS.TotalSeconds) + ", ";
            //    SQL += DB.AddDateTimeParameter(StartTime) + ") ";
            //    DB.ExecuteNonQuery(SQL);
            //}

            return true;
        }

        // Draw the answer line }
        static void DrawAnswer() {
            for (int i = 0; i < 4; i++) {
                Door.GotoXY(4 + (i * 4), 2);
                Door.TextAttr(PieceColours[Answer[i]]);
                Door.Write("\xDB\xDB");
            }
        }

        // Highlight the currently selected colour }
        static void DrawColour() {
            Door.GotoXY(32, 8 + Colour);
            Door.TextAttr(PieceColours[Colour] + (7 * 16));
            Door.Write("\xFE\xFE");
        }

        // Draw the pegs for the current line }
        static void DrawPegs() {
            for (int i = 0; i < 4; i++) {
                Door.GotoXY(14 + i, 22 - (CurLine * 2));
                Door.TextAttr(PegColours[Lines[CurLine].Peg[i]]);
                Door.Write("\xF9");
            }
        }

        // Highlight the currently selected piece }
        static void DrawPiece() {
            Door.GotoXY(3 + (CurPiece * 2), 22 - (CurLine * 2));
            Door.TextAttr(7);
            Door.Write("[ ]");
            Door.GotoXY(4 + (CurPiece * 2), 22 - (CurLine * 2));
            Door.TextAttr(PieceColours[Lines[CurLine].Piece[CurPiece]]);
            Door.Write("\xFE");
        }

        // Un-highlight the current colour }
        static void EraseColour() {
            Door.GotoXY(32, 8 + Colour);
            Door.TextAttr(PieceColours[Colour]);
            Door.Write("\xFE\xFE");
        }

        // Un-highlight the current piece }
        static void ErasePiece() {
            Door.GotoXY(3 + (CurPiece * 2), 22 - (CurLine * 2));
            Door.TextAttr(0);
            Door.Write("   ");
            Door.GotoXY(4 + (CurPiece * 2), 22 - (CurLine * 2));
            Door.TextAttr(PieceColours[Lines[CurLine].Piece[CurPiece]]);
            Door.Write("\xFE");
        }

        // Generate a random ANSWER line }
        static void GenerateAnswer() {
            int[] A = { 0, 1, 2, 3, 4, 5, 6 };

            Random R = new Random();
            for (int i = 0; i < 4; i++) {
                int Num = 0;
                do {
                    Num = R.Next(1, 7);
                } while (A[Num] == 0);
                A[Num] = 0;
                Answer[i] = Num;
            }
        }

        // Guess the current line
        //  First validate, check for black pegs, white pegs,
        //  check for a win, for a loss }
        static void GuessLine() {
            if (CheckValid()) {
                TLine TempLine = Lines[CurLine];
                int Black = CheckBlackPegs(ref TempLine);
                int White = CheckWhitePegs(ref TempLine, Black);
                Lines[CurLine].Peg = TempLine.Peg;
                if (!CheckWon())
                    CheckLost();
                DrawPegs();
                if (GameOver) {
                    EraseColour();
                    ErasePiece();
                } else {
                    ChangeMsg("You Scored " + Black.ToString() + " Black Peg(s) and " + White.ToString() + " White Peg(s)");
                    ErasePiece();
                    CurPiece = 0;
                    CurLine++;
                    DrawPiece();
                }
            }
        }

        // Show the high scores
        static void HighScores() {
            Door.TextAttr(7);
            Door.ClrScr();

            Door.GotoXY(25, 2);
            Door.Write("|0ERMastermind Top 10 Fastest Solves");
            Door.DrawBox(2, 3, 79, 14, Crt.White, Crt.Blue, CrtPanel.BorderStyle.SingleV);
            Door.GotoXY(4, 3);
            Door.Write("|1F###");
            Door.CursorRight(1);
            Door.Write("Player Name");
            Door.CursorRight(29);
            Door.Write("Seconds");
            Door.CursorRight(1);
            Door.Write("Date");
            Door.GotoXY(1, 4);

            // TODOX SQLite code needs updating
            //using (RMSQLiteConnection DB = new RMSQLiteConnection("HighScores.sqlite", false))
            //{
            //    int i = 0;
            //    DB.ExecuteReader("SELECT * FROM HighScores ORDER BY Seconds, RecordDate LIMIT 10");
            //    while (DB.Reader.Read())
            //    {
            //        i++;
            //        Door.CursorRight(3);
            //        Door.Write(StringUtils.PadLeft(i.ToString(), '0', 2) + ". ");
            //        Door.Write(StringUtils.PadRight(DB.Reader["PlayerName"].ToString(), ' ', 40));
            //        Door.Write(StringUtils.PadLeft(DB.Reader["Seconds"].ToString(), ' ', 5) + "   ");
            //        Door.Write(StringUtils.PadRight(DB.Reader["RecordDate"].ToString(), ' ', 22));
            //        Door.WriteLn();
            //    }

            //    while (i < 10)
            //    {
            //        i++;
            //        Door.CursorRight(3);
            //        Door.WriteLn(StringUtils.PadLeft(i.ToString(), '0', 2) + ". ");
            //    }
            //}

            Door.GotoXY(1, 16);
            Door.More();
        }

        // Move the current colour up or down (negative = up, positive = down) }
        static void MoveColour(int Offset) {
            EraseColour();
            Colour = Colour + Offset;
            if (Colour > 6)
                Colour = 1;
            if (Colour < 1)
                Colour = 6;
            DrawColour();
        }

        // Move the current piece left or right (negative=left, positive = right) }
        static void MovePiece(int Offset) {
            ErasePiece();
            CurPiece = CurPiece + Offset;
            if (CurPiece > 3)
                CurPiece = 0;
            if (CurPiece < 0)
                CurPiece = 3;
            DrawPiece();
        }

        // Start a new game }
        static void NewGame() {
            Colour = 1;
            GameOver = false;
            CurLine = 0;
            CurPiece = 0;
            for (int i = 0; i < Lines.Length; i++) {
                Lines[i].Peg = new int[4];
                Lines[i].Piece = new int[4];
                for (int j = 0; j < 4; j++) {
                    Lines[i].Peg[j] = 0;
                    Lines[i].Piece[j] = 0;
                }
            }
            StartTime = DateTime.Now;
            RedrawScreen();
            GenerateAnswer();
#if DEBUG
            DrawAnswer();
#endif
        }

        // Place the currently selected colour at the current place on the board }
        static void PlacePiece() {
            Lines[CurLine].Piece[CurPiece] = Colour;
            MovePiece(+1);
        }

        // Redraw the pieces and pegs on the board }
        static void RedrawBoard() {
            for (int y = 0; y < 10; y++) {
                for (int x = 0; x < 4; x++) {
                    Door.GotoXY(4 + (x * 2), 22 - (y * 2));
                    Door.TextAttr(PieceColours[Lines[y].Piece[x]]);
                    Door.Write("\xFE");
                    Door.GotoXY(14 + x, 22 - (y * 2));
                    Door.TextAttr(PegColours[Lines[y].Peg[x]]);
                    Door.Write("\xF9");
                }
            }
        }

        // Redraw the entire screen }
        static void RedrawScreen() {
            ShowMainAns();
            RedrawBoard();
            DrawPiece();
            DrawColour();
        }

        // Display the main ANSI }
        static void ShowMainAns() {
            Door.Write(Resources.MAIN_ANS);
        }

        static void ValidateRegistration() {
            IniFile Ini = new IniFile(StringUtils.PathCombine(ProcessUtils.StartupPath, "RMastermind.ini"));
            string Name = Ini.ReadString("Registration", "Name", "");
            string Code = Ini.ReadString("Registration", "Code", "INVALID");

            string ValidKey = StringUtils.MD5(_PreSalt, Name, _PostSalt);
            if (Code.ToLower() != ValidKey.ToLower()) {
                Crt.ClrScr();
                Crt.WriteLn("Invalid Registration Key:");
                Crt.WriteLn("  Got ........ '" + Code + "'");
                Crt.WriteLn("  Expected ... '" + ValidKey + "'");
                Crt.WriteLn("Fix your RMastermind.ini to continue");
                Crt.ReadKey();
                Environment.Exit(1);
            }
        }

        // Event for when an unknown command-line parameter is found }
        static void _OnCLP(object sender, CommandLineParameterEventArgs e) {
            // This function is useless in this program, but I included it so you
            //  could see how it's used.  In something like an IRC client where you
            //  want to be able to specify what server to connect to, it would be
            //  useful.  The user could pass -Slocalhost, and this function
            //  would get an AKey value of "S" and an AValue value of "localhost" }
            switch (e.Key) {
                case 'X':
                    Crt.WriteLn("Parameter -X passed with value of: " + e.Value);
                    break;
            }
        }

        // Event for when the user drops carrier }
        static void Door_OnHangUp(object sender, EventArgs e) {
            ChangeMsg("Caller Dropped Carrier");
            System.Threading.Thread.Sleep(2500);
            throw new Exception("Caller Dropped Carrier"); //Environment.Exit(0);
        }

        // Event for when the status bar needs updating }
        static void Door_OnStatusBar(object sender, EventArgs e) {
            switch (Status.Current) {
                case 0:
                    Crt.FastWrite("þ                       þ F1=HELP þ RMASTERMIND þ Idle:       þ Left:          þ", 1, 25, 30);
                    Crt.FastWrite(StringUtils.PadRight(Door.DropInfo.RealName, ' ', 21), 3, 25, 31);
                    Crt.FastWrite("F1=HELP", 27, 25, 31);
                    Crt.FastWrite("RMASTERMIND", 37, 25, 31);
                    Crt.FastWrite(StringUtils.PadRight("Idle: " + StringUtils.SecToMS(Door.SecondsIdle), ' ', 11), 51, 25, 31);
                    Crt.FastWrite("Left: " + StringUtils.SecToHMS(Door.SecondsLeft), 65, 25, 31);
                    break;

                case 1:
                    Crt.FastWrite("þ F1: Toggle StatusBar þ Alt-C: SysOp Chat þ Alt-H: Hang-Up þ Alt-K: Kick User þ", 1, 25, 30);
                    Crt.FastWrite("F1: Toggle StatusBar", 3, 25, 31);
                    Crt.FastWrite("Alt-C: SysOp Chat", 26, 25, 31);
                    Crt.FastWrite("Alt-H: Hang-Up", 46, 25, 31);
                    Crt.FastWrite("Alt-K: Kick User", 63, 25, 31);
                    break;
                case 2:
                    Crt.FastWrite(StringUtils.PadRight(" " + PROG_VER + " is an R&M Door example program - http://www.randm.ca/", ' ', 80), 1, 25, 31);
                    break;
            }
        }

        // Event for when the sysop hits a key locally }
        static void Door_OnSysOpKey(object sender, CharEventArgs e) {
            switch (e.Character) {
                case '\x2E': //ALT-C
                    Door.TextAttr(7);
                    Door.ClrScr();
                    Door.WriteLn("The SysOp wants to speak to you!  (Press ESC to leave chat)");
                    Door.WriteLn();
                    Door.SysopChat();
                    RedrawScreen();
                    break;
                case '\x23': //ALT-H
                    ChangeMsg("You Are Unworthy (The SysOp Has Disconnected You)");
                    System.Threading.Thread.Sleep(2500);
                    Door.Disconnect();
                    Environment.Exit(0);
                    break;
                case '\x25': //ALT-K
                    ChangeMsg("You Are Unworthy (The SysOp Has Kicked You)");
                    System.Threading.Thread.Sleep(2500);
                    Environment.Exit(0);
                    break;
                case '\x3B': //F1
                    if (Status.Current == Status.Count - 1) { Status.Current = 0; } else { Status.Current++; }
                    break;
            }
        }

        // Event for when the user idles too long }
        static void Door_OnTimeOut(object sender, EventArgs e) {
            ChangeMsg("Come Back When You're Awake (Idle Limit Exceeded)");
            System.Threading.Thread.Sleep(2500);
            throw new Exception("Idle Limit Exceeded"); //Environment.Exit(0);
        }

        // Event for when the user needs an idle warning }
        static void Door_OnTimeOutWarning(object sender, EventArgs e) {
            ChangeMsg(Math.Round(Door.SecondsLeft / 60.0, 0, MidpointRounding.AwayFromZero).ToString() + " Minutes Until You're Kicked For Idling");
        }

        // Event for when the user runs out of time }
        static void Door_OnTimeUp(object sender, EventArgs e) {
            ChangeMsg("Come Back When You Have More Time (Ran Out Of Time");
            System.Threading.Thread.Sleep(2500);
            throw new Exception("Ran Out Of Time"); //Environment.Exit(0);
        }

        // Event for when the user needs a time warning }
        static void Door_OnTimeUpWarning(object sender, EventArgs e) {
            ChangeMsg(Math.Round(Door.SecondsLeft / 60.0, 0, MidpointRounding.AwayFromZero).ToString() + " Minutes Remaining This Call");
        }

    }
}
