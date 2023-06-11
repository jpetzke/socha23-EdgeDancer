#define TRANSPOSITION_TABLE

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Formats.Asn1;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Weights;

namespace EgdeDancer
{
    // TLimits stores the possible limits for search sent with a UCI go command
    public struct TLimits
    {
        public int[] time = new int[(int)EColor.BOTH_SIDES];
        public int[] inc = new int[(int)EColor.BOTH_SIDES];
        public int movestogo, depth, movetime, mate, infinite, ponder;
        public ulong nodes;
        public long starttime;
        public TLimits()
        {
            starttime = Now();
            nodes = 0;
            time[(int)EColor.RED] = time[(int)EColor.BLUE] = inc[(int)EColor.RED] = inc[(int)EColor.BLUE] = movestogo = depth = movetime = mate = infinite = ponder = 0;
        }
        public bool Use_Time_Management()
        {
            return ((uint)(mate | movetime | depth | infinite) | nodes) == 0;
        }
        public static long Now() { return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; }
    }
    public class TStats
    {
        public ulong nodes;
        public long plyStartTime;
        public int selDepth;
        public TStats() { Reset(); }
        public void Reset()
        {
            nodes = 0;
            selDepth = 0;
            plyStartTime = TLimits.Now();
        }
        public long GetElapsedTime() { return TLimits.Now() - plyStartTime; }
        public long GetNPS() { long t = TLimits.Now() - plyStartTime; return t > 0 ? (long)nodes * 1000 / t : (long)nodes * 1000; }
    }

    public class TEngine
    {
        private TInputThread inputThread;
        private TBoard board;
        private Transposition tt;
        private bool forcedStop;
        private TPV pv;
        private TStats stats;
        private TTimeCtrl timeCtrl;
        private TEvaluator eval;
        private TWeights weights;
        private TValues values;
        private THistoryTable history;
        private TKiller killer;
        private TCounterMoves counters;

        private int nodes_between_tc;
        private int tc_nodes_counter;

        public TEngine()
        {
            Console.WriteLine(String.Format("Hey, Thanks for the fish! Software Challenge 2023 {0} {1}.{2} {3}", Types.NAME_FULL, Types.VERSION, Types.SUBVERSION, Types.DEBUG ? "DEBUG" : ""));

            inputThread = new TInputThread();
            board = new TBoard();
            tt = new Transposition();
            pv = new TPV();
            stats = new TStats();
            timeCtrl = new TTimeCtrl();
            weights = new TWeights();
            history = new THistoryTable();
            values = new TValues(weights);
            eval = new TEvaluator(ref board, ref weights);
            killer = new TKiller();
            counters = new TCounterMoves();

            forcedStop = true;
        }
        public void Run()
        {
            for (; ; )   // Loop forever
            {
                if (!ProcessGUIMessages(10)) break;
            }
        }
        public bool ProcessGUIMessages(int sleep)
        {
            if (inputThread.isNewInputAvailable())
            {
                string input = inputThread.getNewInput();
                if (input.Length > 0) return InputHandler(input);
            }
            else if (sleep > 0) Thread.Sleep(sleep);

            return true;
        }
        private bool InputHandler(string input)
        {
            string[] subs = input.Split(' ');

            // required
            if (subs[0] == "quit") return false;
            if (subs[0] == "print") return board.Print();
            if (subs[0] == "perft") return Perft(input);
            if (subs[0] == "divide") return Divide(input);
            if (subs[0] == "weights") return Weights();
            if (subs[0] == "eval") return eval.Print();
            if (subs[0] == "setweight") return SetWeight(input);
            if (subs[0] == "execute") return Execute(input);

            if (subs[0] == "position") return Position(input);
            if (subs[0] == "go") return Go(input);
            if (subs[0] == "uci") return UCI();
            if (subs[0] == "ucinewgame") return UciNewGame();
            if (subs[0] == "isready") return IsReady();
            if (subs[0] == "stop") return Stop();

            if (subs[0] == "test") return Test();

            if (subs[0].Length == 4 && board.IsValidMove(board.StrToMove(subs[0])))
            {
                board.MakeMove(board.StrToMove(subs[0]));
                return true;
            }

            Console.WriteLine($"Illegal command: {input}");
            return true;
        }
        private static bool UCI()
        {
            Console.WriteLine(String.Format("id name {0} {1}.{2}", Types.NAME_FULL, Types.VERSION, Types.SUBVERSION));
            Console.WriteLine("id author Jonas Petzke");
            Console.WriteLine("uciok");
            return true;
        }

        private bool UciNewGame()
        {
            // Reset all collected heuristics and position information
            Reset();
            return true;
        }
        private bool Reset()
        {
            tt.ResetHash();
            killer.ResetSlots();
            counters.ResetSlots();
            history.Reset();
        
            return true;
        }
        private static bool IsReady()
        {
            Console.WriteLine("readyok");
            return true;
        }
        private bool Stop()
        {
            forcedStop = true;
            return true;
        }
        public bool Execute(string input)
        {
            string[] parts = input.Split(' ');
            string filename;
            string[] lines;

            if (parts.Length > 1)
            {
                filename = parts[1];
                try
                {
                    lines = System.IO.File.ReadAllLines(filename);
                }
                catch
                {
                    Console.WriteLine("Error reading file: " + filename);
                    return true;
                }
            }
            else
            {
                Console.WriteLine("Usage: execute <filename>");
                return true;
            }

            for (int i = 0; i < lines.Length; i++) InputHandler(lines[i]);
            return true;
        }
        private bool Weights()
        {
            string divider = " ---+------------------------------------------+---------------+-----";
            Console.WriteLine(String.Format(" {0,2} | {1,-40} | {2,6} {3,6} | {4,4}", "Id", "Name", "MG", "EG", "Max"));
            Console.WriteLine(divider);

            int bitcount = 0, weightsumMG = 0, weightsumEG = 0;
            for (EWeight id = (EWeight)0; id < EWeight.WEIGHT_COUNT; id++)
            {
                Console.WriteLine(String.Format(" {0,2} | {1,-40} | {2,6} {3,6} | {4,4}", (int)id, TWeights.WEIGHT_NAMES[(int)id], weights.GetWeightMG(id), (weights.GetWeightEG(id) != -1 ? weights.GetWeightEG(id) : "---"), Math.Pow(2, TWeights.WEIGHT_BITS[(int)id]) - 1));
                bitcount += TWeights.WEIGHT_BITS[(int)id];
                weightsumMG += weights.GetWeightMG(id);
                weightsumEG += weights.GetWeightEG(id);
            }
            Console.WriteLine(divider);
            Console.WriteLine(String.Format(" {0} weights defined. Tunable bits: {1}   Check Sum MG : {2}   Check Sum EG : {3}", (int)EWeight.WEIGHT_COUNT, bitcount, weightsumMG, weightsumEG));
            return true;
        }

        private bool SetWeight(string input)
        {
            string[] parts = input.Split(' ');
            bool error, quiet;

            error = parts.Length < 2;
            if (!error && (parts[1] == "default"))
            {
                weights.InitToDefault();
                eval.CalculateValuesFromWeights(weights);
                Console.WriteLine("weights are resetted to defaults");
                return true;
            }

            // Set individual weights - setweight id 5 values 42 43
            error &= parts.Length < 6;                              // if we not reset to default 6 parts are required 
            error &= parts[1] != "name" && parts[1] != "id";        // we require a reserved word at position 1
            error &= parts[3] != "values";                          // we require a reserved word at position 3
            quiet = parts[parts.Length - 1] == "-q";                // if the last parameter is -q we don't output success

            if (error)
            {
                Console.WriteLine("Usage: setweight name <name> values <mg eg> [-q]\n       setweight id <id> values <mg eg> [-q]\n       setweight default");
                return true;
            }

            EWeight id = EWeight.WEIGHT_COUNT; // Init with Error Value
            try
            {
                id = (EWeight)Int32.Parse(parts[2]);
            }
            catch
            {
                id = weights.GetWeightId(parts[2]);
            }
            if (id == EWeight.WEIGHT_COUNT)
            {
                Console.WriteLine("Incorrect Weight Id: " + parts[2]);
                return true;
            }

            double upperbound = Math.Pow(2, TWeights.WEIGHT_BITS[(int)id]) - 1;
            try
            {
                int mg = Int32.Parse(parts[4]);
                int eg = Int32.Parse(parts[5]);

                if (mg > upperbound) throw new ArgumentException(parts[4]);
                if (eg > upperbound) throw new ArgumentException(parts[5]);
                weights.SetWeight(id, mg, eg);
                values.CalculateValuesFromWeights(weights);
                eval.CalculateValuesFromWeights(weights);
                if (!quiet) Console.WriteLine(String.Format("Setting {0} id: {1} to new values of {2}, {3}", TWeights.WEIGHT_NAMES[(int)id], (int)id, mg, eg));
            }
            catch { Console.WriteLine(String.Format("Incorrect value for weight {0} id: {1}. Valid range: 0 - {2}", TWeights.WEIGHT_NAMES[(int)id], id, upperbound)); }

            return true;
        }
        private bool Perft(string input)
        {
            string[] subs = input.Split(' ');
            int level = subs.Length > 1 ? Int32.Parse(subs[1]) : 4;

            var stopwatch = new Stopwatch();

            for (int i = 1; i <= level; i++)
            {
                stopwatch.Start();
                ulong p = board.Perft(i);
                stopwatch.Stop();

                Console.WriteLine(String.Format("Perft {0,2}: {1,12:N0}\t{2,6:N0} ms", i, p, stopwatch.ElapsedMilliseconds));
                stopwatch.Reset();
            }

            return true;
        }
        private bool Divide(string input)
        {
            string[] subs = input.Split(' ');
            int level = subs.Length > 1 ? Int32.Parse(subs[1]) : 4;

            TDivideList list = board.Divide(level);
            Console.WriteLine("Available Moves: {0}", list.count);
            ulong perft = 0;
            for (int i = 0; i < list.count; i++)
            {
                perft += list.perfts[i];
                Console.WriteLine("{0}: {1,8:N0}", Move.SAN(list.moves[i]), list.perfts[i]);
            }
            Console.WriteLine("Total Nodes: {0,8:N0}", perft);

            return true;
        }

        public bool Position(string input) // Function needs to be public as it might also be called from the outside 
        {
            string[] subs = input.Split(' ');

            if (subs.Length > 1 && subs[1] == "startpos") board.SetFEN("32121122123br11b31r1b221213213223221b11322213r11211r13322112 r 0 0 0");
            if (subs.Length > 2 && subs[1] == "fen")
            {
                string fen = subs[2];
                int i = 3;
                while (i < subs.Length && subs[i] != "moves") fen += " " + subs[i++];
                board.SetFEN(fen);

                if (i < subs.Length && subs[i] == "moves")
                {
                    for (int j = i + 1; j < subs.Length; j++)
                    {
                        uint move = board.StrToMove(subs[j]);
                        if (board.IsValidMove(move)) board.MakeMove(move);
                        else
                        {
                            Console.WriteLine(String.Format("Invalid move {0} in Position string at index {1}", subs[j], j));
                            break;
                        }
                    }
                    tt.AgeHash(); // Age Hash Once
                }
            }

            return true;
        }
        private bool Go(string input)
        {
            string[] parts = input.Split(' ');
            TLimits limits = new TLimits();

            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i - 1] == "rtime") limits.time[(int)EColor.RED] = Int32.Parse(parts[i]);
                else
                if (parts[i - 1] == "btime") limits.time[(int)EColor.BLUE] = Int32.Parse(parts[i]);
                else
                if (parts[i - 1] == "rinc") limits.inc[(int)EColor.RED] = Int32.Parse(parts[i]);
                else
                if (parts[i - 1] == "binc") limits.inc[(int)EColor.BLUE] = Int32.Parse(parts[i]);
                else
                if (parts[i - 1] == "movestogo") limits.movestogo = Int32.Parse(parts[i]);
                else
                if (parts[i - 1] == "depth") limits.depth = Int32.Parse(parts[i]);
                else
                if (parts[i - 1] == "nodes") limits.nodes = (ulong)Int64.Parse(parts[i]);
                else
                if (parts[i - 1] == "movetime") limits.movetime = Int32.Parse(parts[i]);
                if (parts[i - 1] == "mate") limits.mate = Int32.Parse(parts[i]);
                if (parts[i] == "infinite") limits.infinite = 1;
                else
                if (parts[i] == "ponder") limits.ponder = 1;
            }

            uint move = Search(ref limits);
            if (board.GameOver()) Console.WriteLine("Game Over");
            else
                Console.WriteLine(String.Format("bestmove {0}", Move.SAN(move)));
            return true;
        }
        private void InitNodeCounter(int tcNodes)
        {
            nodes_between_tc = tcNodes;
            tc_nodes_counter = nodes_between_tc;
        }
        private void CollectPV(int d)
        {
            uint bestMove = Move.INVALID_MOVE;
            THashResult hashR = new THashResult();

            // in order to prevent infinite loops in the PV collection we stop at max depth
            if (board.state.hmc + d >= Types.MAX_PLY) return;

            tt.ProbeHash(board.state.hash, (uint)board.state.hmc, ref hashR);

            // retrieve the position from the TT, when it is not found the PV ends here
            if (hashR.flag == ETTResponse.TT_EXACT)
            {
                // if the move is not valid due to a Key Collission isValidMove will return false
                if (board.IsValidMove(hashR.move)) bestMove = hashR.move;
            }
            else return;

            if (bestMove == Move.INVALID_MOVE) return;

            // if we find a valid bestMove from a TT_EXACT position in the hash table we add it
            // if the move was not added (because it would lead to a loop) we return
            if (!pv.AddPVMove(d, bestMove, board.state.hash)) return;

            board.MakeMove(bestMove);
            CollectPV(d + 1);
            board.UnMakeMove(bestMove);
        }
        public uint Search(ref TLimits limits) // Function needs to be public as it might also be called from the outside SOCHA Logic GetMove Function
        {
            uint move = Move.INVALID_MOVE;
            uint bestmove = Move.INVALID_MOVE;
            int currentIterativeDeepeningDepth = 1;
            ulong totalNodes = 0;
            ulong totalTime = 0;
            pv.Reset();

            // setup the root move list
            TMoveList ml = board.GetAllMoves();
            TRootMoveList rml = new TRootMoveList(ref ml);
            ml.Clear();

            // init the forcedstop flag, when this is set to true we quit search
            forcedStop = false;

            // if no moves are possible we just quit
            // we can do this because in case we have no move but the game is not over the movelist should contain the null move and not be empty
            if (rml.count == 0) return Move.INVALID_MOVE;

            // we initialize the last ply eval to NOVALUE, as we don't know it from search 
            int lastPlyEvalValue = (int)EScores.NO_SCORE;

            // Setup the Time Control
            timeCtrl.Init(limits, board.state.sideToMove);
            InitNodeCounter(timeCtrl.GetNodesBetweenTC());

            // we peek the transposition table, if we find the position we start search at the depth found there
            THashResult hashR = new THashResult();
            if (tt.ProbeHash(board.state.hash, (uint)board.state.hmc, ref hashR) == ETTResponse.TT_EXACT)
            {
                currentIterativeDeepeningDepth = Math.Min(timeCtrl.GetMaxDepth(), hashR.depth);
            }

            do
            {
                // we reset the statistics so we collect for the current ply
                stats.Reset();

                // start a seach at the root level -> lastPlyEvalValue will contain the value of the returned move
                move = StartSearchAtRootLevel(ref rml, currentIterativeDeepeningDepth, ref lastPlyEvalValue);

                // collect the pv also from the hash table
                CollectPV(0);

                if (move != Move.INVALID_MOVE)          // only if we got a valid move we take it
                {
                    bestmove = move;
                    Console.WriteLine(String.Format("info depth {0} score {1} nodes {2} nps {3} time {4} pv {5}",
                        currentIterativeDeepeningDepth,
                        board.ScoreToStr(lastPlyEvalValue),
                        stats.nodes,
                        stats.GetNPS(),
                        stats.GetElapsedTime(),
                        pv.GetPVAsStr()));
                }
                totalNodes += stats.nodes;
                totalTime = (ulong)(TLimits.Now() - limits.starttime);

                currentIterativeDeepeningDepth++;

            } while (!forcedStop &&
                     timeCtrl.SearchAnotherPly() &&
                     currentIterativeDeepeningDepth <= timeCtrl.GetMaxDepth() &&
                     currentIterativeDeepeningDepth <= board.Plys2Mate(lastPlyEvalValue));

            // we set forcedStop to true, so we know the engine is not searching anymore
            forcedStop = true;

            // if we did not get a best move so far we return what we found even if we not finished searching
            if (bestmove == Move.INVALID_MOVE) bestmove = rml.moves[0].move;

            // Output total time and nodes
            Console.WriteLine(String.Format("info nodes {0} time {1} nps {2} pcthit {3}", totalNodes, totalTime, totalTime > 0 ? totalNodes * 1000 / totalTime : 1000, eval.GetCacheStatistics()));
            return bestmove;
        }
        public uint StartSearchAtRootLevel(ref TRootMoveList rml, int currentIDDepth, ref int lastEvaluationValue)
        {
            const int INF = (int)EScores.INFINITY;

            int i;
            int alpha = -INF, value = -INF, best = (int)EScores.NO_SCORE;

            // we start a new iteration, clear the pv buffer
            pv.pv[0] = Move.INVALID_MOVE;

            // 1st. lookup the position in the Transposition Table ---------
            // only exact value are expected as this is a PV node

            THashResult hashR = new THashResult();
            if (tt.ProbeHash(board.state.hash, (uint)board.state.hmc, ref hashR) == ETTResponse.TT_EXACT)
            {
                // validate the hash move that it is legal
                if (board.IsValidMove(hashR.move))
                {
                    // move the hash move to the top of the list, so it is searched first
                    rml.MoveToTop(hashR.move);

                    // if the hash move has enough draft we may terminate the search here already
                    if (hashR.depth >= currentIDDepth)
                    {
                        lastEvaluationValue = hashR.merit;
                        return hashR.move;                      // we return the hash move
                    }
                }
            }

            rml.ResetScores(); // set all scores to NOVALUE
            uint[] subtree_pv = new uint[Types.MAX_PLY];

            for (i = 0; i < rml.count; i++)
            {
                if (!forcedStop)
                {
                    alpha = rml.GetMinAlpha(1);
                    
                    board.MakeMove(rml.moves[i].move);
                    value = -PVS(1, currentIDDepth - 1, 0, -INF, -alpha, ENullMove.NULL_MOVE_NO, ref subtree_pv);
                    board.UnMakeMove(rml.moves[i].move);

                    if (!forcedStop & (value > alpha))
                    {
                        // update the best pv if we have a best move
                        if (value > best)
                        {
                            best = value;

                            pv.pv[0] = rml.moves[i].move;
                            for (int j = 0; j < Types.MAX_PLY; j++)
                            {
                                if (Move.INVALID_MOVE == (pv.pv[j + 1] = subtree_pv[j])) break; // add the moves from the subtree
                            }
                        }

                        rml.SetDataAt(i, value, 0);
                        rml.SetPVforMove(rml.moves[i].move, ref subtree_pv);
                        rml.Sort();
                    }
                    else rml.SetDataAt(i, (int)EScores.NO_SCORE, 0);
                }
                else rml.SetDataAt(i, (int)EScores.NO_SCORE, 0);
            }

            // *** all moves have been done ***

            if (best != (int)EScores.NO_SCORE) lastEvaluationValue = best;    // save the return score back to lastEvaluationValue

            // save the result back to the transposition table
            // if we finished the ply (!forcedStop) it is the move at position 0
            // if we had to stop before we were done, but finished at least 1 move, we save the best move so far with a lower draft, 
            // so we remember this move but the ply is researched when questioned again
            if (!forcedStop)
            {
                tt.Store2Hash(board.state.hash, board.state.hmc, currentIDDepth, (int)ETTResponse.TT_EXACT, rml.moves[0].value, rml.moves[0].move);
            }
            else
            {
                if (alpha > -INF) tt.Store2Hash(board.state.hash, board.state.hmc, currentIDDepth - 1, (int)ETTResponse.TT_EXACT, rml.moves[0].value, rml.moves[0].move);
            }

            // if we only have 1 valid move we only do a ply 6 search to speed things up
            // we do this by setting forceStop = true to break the search as soon as possible
            if (rml.count == 1 && currentIDDepth > 6) forcedStop = true;

            // return the move at position 0 when we finished at least 1 move (alpha > -INFINITY), it is the best, otherwise return nothing
            if (alpha == -INF) return Move.INVALID_MOVE;

            return rml.moves[0].move;
        }
        public int PVS(int ply, int depth, int extension, int alpha, int beta, ENullMove nullMove, ref uint[] pv)
        {
            int value = (int)EScores.NO_SCORE;

            // depth should be 0 or greater
            Debug.Assert(depth >= 0);

            // check for a PV Node
            bool isPVNode = beta - alpha > 1;

            uint[] subtree_pv = new uint[Types.MAX_PLY];           // buffer to collect the pv for the moves that we make in this node
            if (isPVNode) pv[0] = Move.INVALID_MOVE;               // in this node we start a new pv that is empty

            // we increase the searched nodes count
            stats.nodes++;

            // have a look at the clock if the necessary number of nodes are processed
            if (!forcedStop)
            {
                if (--tc_nodes_counter <= 0)
                {
                    forcedStop = timeCtrl.TimeUp();
                    tc_nodes_counter = nodes_between_tc;

                    // we also check for new input
                    ProcessGUIMessages(0);
                }
            }
            if (forcedStop) return alpha;

            Debug.Assert(board.state.hmc <= Types.MAX_PLY);

            // We check first whether the game is over
            if (board.GameOver())
            {
                EColor winner = board.Winner();
                if (winner == EColor.NO_COLOR) return (int)EScores.DRAW;
                if (winner == board.state.sideToMove) return board.GetMateScore(ply);
                if (winner != board.state.sideToMove) return board.GetMatedScore(ply);
            }

            /**************************************************************
        	 * At the horizon we call *qSearch* and return that value
	         **************************************************************/
            if (depth <= 0 || ply >= Types.MAX_PLY - 1)
            {
                // return eval.Evaluate();
                return QSearch(ply, depth, alpha, beta, subtree_pv);
            }
            Debug.Assert(depth > 0);

            /*******************************************************************************
	         * Lookup the position in the Transposition Table							   *
             *																			   *
	         * ahashEntry is returned                                                      *
             * hashEntry.merit contains the ply adjusted merit (for mate scores)           *
             * the hashEntry contains the unadjusted merit, which should not be used       *
	         *******************************************************************************/

            uint hashMove = Move.INVALID_MOVE;    // we set it to INVALID because we don't know whether we can fill them with valid moves

            #if TRANSPOSITION_TABLE
            THashResult hashR = new THashResult();

            switch (tt.ProbeHash(board.state.hash, (uint)board.state.hmc, ref hashR))
            {
                case ETTResponse.TT_EXACT:
                    if (hashR.depth >= depth) return hashR.merit;

                    hashMove = hashR.move;
                    break;

                case ETTResponse.TT_BETA:
                    if (hashR.depth >= depth)
                    {
                        if (hashR.merit >= beta)
                        {
                            // Todo after Killer Moves: if (hashR.move) killerMoves.storeNewKiller(ply, hashR.move);
                            return beta;
                        }
                    }
                    hashMove = hashR.move;
                    break;

                case ETTResponse.TT_ALPHA:
                    if (hashR.depth >= depth)
                    {
                        if (hashR.merit <= alpha) return alpha;
                    }
                    hashMove = hashR.move;  // in ALL nodes we also have a move that was either a cutmove earlier or the 1st move that was searched
                    break;                  // if we don't have a better move yet we continue to search this move first
            }
            #endif // TRANSPOSITION_TABLE

            //---------- END OF CACHE LOOKUP --------------------------

            // get the static eval
            int s_eval = eval.Evaluate();
            board.history[board.state.hmc].value = s_eval;

            // can TT entry be used as better eval estimate
            if (hashR.flag != ETTResponse.TT_NOTFOUND)
            {
                if (hashR.flag == ETTResponse.TT_ALPHA && s_eval > hashR.merit) s_eval = hashR.merit;
                if (hashR.flag == ETTResponse.TT_BETA && s_eval < hashR.merit) s_eval = hashR.merit;
            }

            // Razoring on lower depths
            // Skipped in PV Nodes and near the end of the game
            int RAZOR_MARGIN = board.InterpolateValue(values.RAZOR_MARGIN.mg, values.RAZOR_MARGIN.eg);

            if (depth < 3 &&
               !isPVNode &&
               board.state.hmc < 40 &&
                s_eval + RAZOR_MARGIN <= alpha &&
               !board.IsMateScore(beta))
            {
                if (depth < 1) return QSearch(ply, 0, alpha, alpha + 1, pv);

                int r_alpha = alpha - values.RAZOR_MARGIN.mg;
                int rv = QSearch(ply, 0, r_alpha, r_alpha + 1, pv);
                if (rv <= r_alpha) return alpha;
            }

            // Static 0 move - eval - margin > beta
            // if our score is very good already
            // TODO: make the margin <100> tuneable
            int FUTILITY_MARGIN = board.InterpolateValue(values.STATIC_0_MOVE_MARGIN.mg, values.STATIC_0_MOVE_MARGIN.eg);

            if (depth < 6 &&                                                // in the final plys already
               !board.IsMateScore(alpha) &&                                 // not being mated or mating already
                (s_eval - FUTILITY_MARGIN * depth >= beta)) return beta;	// our score is already very good

            TMoveList ml = null;
            uint bestMove = Move.INVALID_MOVE;
            uint killer0 = Move.INVALID_MOVE;     // moves from the killer tables
            uint killer1 = Move.INVALID_MOVE;
            uint counter = Move.INVALID_MOVE;

            int movesSearched = 0;            // count the number of processed moves

            ETTResponse flag = ETTResponse.TT_ALPHA;    // we think pessimitic and think no move is able to increase alpha so alpha stays our upper bound

            /*******************************************************************
             * We perfrom an incremental move generation 
	         *******************************************************************/
            EMoveList mlStartIdx = EMoveList.HASH_MOVE_LIST;    // which move list is first processed (hash move only, all moves...)
            EMoveList mlEndIdx = EMoveList.REMAINING_MOVES;     // the remaining moves is the default last list		

            // if we have a hash move we start with it otherwise we use the tactical moves first
            mlStartIdx = hashMove != Move.INVALID_MOVE ? EMoveList.HASH_MOVE_LIST : EMoveList.REMAINING_MOVES;

            for (EMoveList j = mlStartIdx; j <= mlEndIdx; j++)
            {
                switch (j)
                {
                    case EMoveList.HASH_MOVE_LIST:
                        if (board.IsValidMove(hashMove)) ml = board.GetThisMove(ply, hashMove); else continue;
                        break;

                    case EMoveList.REMAINING_MOVES:
                        ml = board.GetAllMoves(ply);
                        if (hashMove != Move.INVALID_MOVE) ml.DeleteMove(Move.GetID(hashMove));
                        killer.GetKillers(ply, ref killer0, ref killer1);
                        killer0 = killer0 != hashMove && board.IsValidMove(killer0) ? killer0 : Move.INVALID_MOVE;
                        killer1 = killer1 != hashMove && board.IsValidMove(killer1) ? killer1 : Move.INVALID_MOVE;
                        counter = counters.GetCounter(board.Next2LastMove(), board.LastMove());

                        if (ml.count > 1)
                        {
                            AwardMoveValues(ref ml, killer0, killer1, counter);
                            ml.Sort();
                        }
                        break;

                    case EMoveList.ALL_MOVES:
                        ml = board.GetAllMoves(ply);
                        break;
                }

                int LMR_MINIMUM_MOVES = board.InterpolateValue(values.LMR_MINIMUM_MOVES.mg, values.LMR_MINIMUM_MOVES.eg);

                for (int i = 0; i < ml.count; i++)
                {
                    // we check that every move we process here is legal
                    Debug.Assert(board.IsValidMove(ml.moves[i]));

                    if (!forcedStop)
                    {
                        movesSearched++;

                        board.MakeMove(ml.moves[i]);

                        // the 1st move is fully searched
                        if ((movesSearched == 1) || (depth <= 2))
                        {
                            if (movesSearched == 1) bestMove = ml.moves[0];
                            value = -PVS(ply + 1, depth - 1, extension, -beta, -alpha, ENullMove.NULL_MOVE_OK, ref subtree_pv);
                        }
                        else
                        {
                            /*-----------------------------------------------------------------
                            * LMR                                                             |
                            * performed a reduced search (depth-2) for uninteresting moves    |
                            * if at least n moves have been normally searched				  |
                            * the move is no capture or promotion							  |
                            * in a PV node more moves are searched							  |
                            ------------------------------------------------------------------*/
                            if (j == EMoveList.REMAINING_MOVES &&
                                !isPVNode &&
                                movesSearched > LMR_MINIMUM_MOVES &&
                                depth > 1 &&
                                board.state.hmc < 52)
                            {
                                int lmrReduction = 2 + depth / 8;

                                int newDepth = depth;
                                while (lmrReduction > 0 && newDepth > 1) { newDepth--; lmrReduction--; }
                                Debug.Assert(newDepth >= 1);

                                // if we have a reduction perform a reduced search otherwise set value to alpha + 1 which triggers a research
                                value = newDepth < depth ? -PVS(ply + 1, newDepth - 1, extension, -alpha - 1, -alpha, ENullMove.NULL_MOVE_OK, ref subtree_pv) : alpha + 1;
                            }
                            else value = alpha + 1;   // ensure we do a search with full depth, if we did not reduce

                            if (value > alpha)        // the reduced search was above alpha or we did not reduce
                            {                         // then perfom a std. 0 window search
                                value = -PVS(ply + 1, depth - 1, extension, -alpha - 1, -alpha, ENullMove.NULL_MOVE_OK, ref subtree_pv);

                                if (isPVNode && value > alpha) // a research makes only sense in PVNodes
                                {
                                    value = -PVS(ply + 1, depth - 1, extension, -beta, -alpha, ENullMove.NULL_MOVE_OK, ref subtree_pv);
                                }
                            }
                        }

                        board.UnMakeMove(ml.moves[i]);

                        if (value >= beta)
                        {
                            if (!forcedStop) tt.Store2Hash(board.state.hash, board.state.hmc, depth, (int)ETTResponse.TT_BETA, beta, ml.moves[i]);

                            // store killer if the move is good even if it does not capture the current maximum of fishes
                            if (Move.GetFishCount(ml.moves[i]) < board.GetMaxFishes())
                            {
                                killer.StoreNewKiller(ply, Move.GetID(ml.moves[i]));
                                counters.StoreNewCounter(board.Next2LastMove(), board.LastMove(), Move.GetID(ml.moves[i]));
                            }

                            int bonus = depth * depth;
                            history.Update(ml.moves[i], bonus);
                            for (int k = 0; k < i; k++) history.Update(ml.moves[k], -bonus);

                            ml.Clear();
                            return beta;
                        }

                        if (value > alpha)
                        {
                            alpha = value;

                            flag = ETTResponse.TT_EXACT;   // at the moment we are in the PV

                            bestMove = ml.moves[i];

                            // update the pv
                            pv[0] = ml.moves[i];
                            for (int k = 0; k < Types.MAX_PLY; k++)
                            {
                                if (Move.INVALID_MOVE == (pv[k + 1] = subtree_pv[k])) break; // copy the moves from the subtree that this move generated to the pv until we hit a a null move
                            }
                        }
                    }
                } // next i

                ml.Clear();
            } // 

            if (!forcedStop) tt.Store2Hash(board.state.hash, board.state.hmc, depth, (int)flag, alpha, bestMove);

            return alpha;
        }
        private int QSearch(int ply, int depth, int alpha, int beta, uint[] pv)
        {
            Debug.Assert(depth <= 0); // qSearch can only be called for depth <= 0, otherwise the pv is messed up

            TMoveList ml = null;
            uint hashMove = Move.INVALID_MOVE, mv, bestMove = Move.INVALID_MOVE;

            int i;

            int value;

            uint[] subtree_pv = new uint[64]; // buffer to collect the pv for the moves that we make in this node

            /************************************************************************
             * At depth 0 we don't count nodes or check for known endgame or mate   *
             * as this was done in pvs already before we called qSearch		        *
             ************************************************************************/
            if (depth < 0)
            {
                if (beta - alpha > 1) pv[0] = Move.INVALID_MOVE; // in this node we start a new pv that is empty

                stats.nodes++;

                // have a look at the clock if the necessary number of nodes are processed
                if (!forcedStop)
                {
                    if (--tc_nodes_counter <= 0)
                    {
                        forcedStop = timeCtrl.TimeUp();
                        tc_nodes_counter = nodes_between_tc;

                        // we also check for new input
                        ProcessGUIMessages(0);
                    }
                }
                if (forcedStop) return alpha;
            }

            // We check now whether the game is over
            if (board.GameOver())
            {
                EColor winner = board.Winner();
                if (winner == EColor.NO_COLOR) return (int)EScores.DRAW;
                if (winner == board.state.sideToMove) return board.GetMateScore(ply);
                if (winner != board.state.sideToMove) return board.GetMatedScore(ply);
            }

            // Transposition Table Lookup

            #if TRANSPOSITION_TABLE
            THashResult hashR = new THashResult();
            tt.ProbeHash(board.state.hash, (uint)board.state.hmc, ref hashR);

            if (hashR.flag == ETTResponse.TT_BETA || hashR.flag == ETTResponse.TT_EXACT)
            {
                if (hashR.merit >= beta) return beta;
                hashMove = hashR.move;
            }

            if (hashR.flag == ETTResponse.TT_ALPHA || hashR.flag == ETTResponse.TT_EXACT)
            {
                if (hashR.merit <= alpha) return alpha;
                hashMove = hashR.move;
            }
            #endif

            // break search if we are to deep
            if (ply >= 60) return eval.Evaluate();
            if (depth < -3 && board.state.sideToMove == EColor.RED) return eval.Evaluate();

            // what is the score if we do nothing
            int stand_pat = eval.Evaluate();

            // can TT entry be used as better eval estimate
            if (hashR.flag != ETTResponse.TT_NOTFOUND)
            {
                if (hashR.flag == ETTResponse.TT_ALPHA && stand_pat > hashR.merit) stand_pat = hashR.merit;
                if (hashR.flag == ETTResponse.TT_BETA && stand_pat < hashR.merit) stand_pat = hashR.merit;
            }

            // the score is already to good, this branch will not happen
            if (stand_pat >= beta) return beta;

            if (alpha < stand_pat) alpha = stand_pat;

            /*******************************************************************
             * We perfrom an incremental move generation 
	         *******************************************************************/
            EMoveList mlStartIdx = 0;                           // which move list is first processed (hash move only, all moves...)
            EMoveList mlEndIdx = EMoveList.REMAINING_MOVES;     // the remaining moves is the default last list		

            // if we have a hash move we start with it otherwise we use the tactical moves first
            mlStartIdx = hashMove != Move.INVALID_MOVE ? EMoveList.HASH_MOVE_LIST : EMoveList.REMAINING_MOVES;

            for (EMoveList j = mlStartIdx; j <= mlEndIdx; j++)
            {
                switch (j)
                {
                    case EMoveList.HASH_MOVE_LIST:
                        if (board.IsValidMove(hashMove)) ml = board.GetThisMove(ply, hashMove); else continue;
                        break;

                    case EMoveList.REMAINING_MOVES:
                        ml = board.GetAllMoves(ply);
                        if (hashMove != Move.INVALID_MOVE) ml.DeleteMove(Move.GetID(hashMove));

                        if (ml.count > 1)
                        {
                            AwardMoveValues(ref ml);
                            ml.Sort();
                        }
                        break;
                }

                for (i = 0; i < ml.count; i++)
                {
                    mv = ml.moves[i];

                    if (!forcedStop)
                    {
                        // only consider moves that collect the maximum number of fishes or a trapping an enemy penguin
                        if (board.IsSetPhase() && !board.IsTrappingMove(mv)) continue;
                        if (!board.IsTrappingMove(mv) && Types.FISH_COUNT[(int) Move.GetFish(mv)] < board.GetMaxFishes()) continue;

                        board.MakeMove(mv);
                        value = -QSearch(ply + 1, depth - 1, -beta, -alpha, subtree_pv);
                        board.UnMakeMove(mv);

                        if (value > alpha)
                        {
                            bestMove = ml.moves[i];
                            if (value >= beta)
                            {
                                // store good moves, the main search will pick them up at depth 1 in the next iteration
                                if (!forcedStop) tt.Store2Hash(board.state.hash, board.state.hmc, 0, (int)ETTResponse.TT_BETA, beta, mv);

                                ml.Clear();
                                return beta;
                            }

                            alpha = value;

                            // update the pv
                            pv[0] = mv;
                            for (int k = 0; k < 60; k++)
                            {
                                if (Move.INVALID_MOVE == (pv[k + 1] = subtree_pv[k])) break; // copy the moves from the subtree that this move generated to the pv until we hit a a null move
                            }
                        }
                    }
                } // next i

                ml.Clear();
            } // next j

            if (!forcedStop && depth == 0) tt.Store2Hash(board.state.hash, board.state.hmc, 0, (int)ETTResponse.TT_ALPHA, alpha, bestMove);

            return alpha;
        }
        private void AwardMoveValues(ref TMoveList ml, uint killer0, uint killer1, uint counter)
        {
            // Award move ordering points
            // We order the moves by first by captured fishes and a small positional bonus
            int i;
            uint orderValue = 0, mv;
            uint order_base;


            for (i = 0; i < ml.count; i++)
            {
                mv = Move.GetID(ml.moves[i]);
                if (mv == Move.INVALID_MOVE) continue;

                order_base = 2058;
                if (mv == killer0) order_base += 4002; else
                if (mv == counter) order_base += 4001; else
                if (mv == killer1) order_base += 4000;

                orderValue = (uint)(order_base +
                                    4000 * (Types.FISH_COUNT[(int)Move.GetFish(mv)] - 1) +
                                       1 * history.getScore(mv) +
                                      10 * Types.SQUARE_BONUS[(int)Move.GetToSquare(mv)] + 
                                      -1 * Types.SQUARE_BONUS[(int)Move.GetFromSquare(mv)]);


                Debug.Assert(orderValue >= 0 && orderValue < 32767);    
                
                ml.moves[i] = Move.SetValue(mv, orderValue);
            }
        }
        private void AwardMoveValues(ref TMoveList ml)
        {
            // Award move ordering points
            // We order the moves by first by captured fishes and a small positional bonus
            int i;
            uint orderValue = 0, mv;
            uint order_base;


            for (i = 0; i < ml.count; i++)
            {
                mv = Move.GetID(ml.moves[i]);
                if (mv == Move.INVALID_MOVE) continue;

                order_base = 2058;

                orderValue = (uint)(order_base +
                                     4000 * (Types.FISH_COUNT[(int)Move.GetFish(mv)] - 1) +
                                        1 * history.getScore(mv) +
                                       10 * Types.SQUARE_BONUS[(int)Move.GetToSquare(mv)] +
                                       -1 * Types.SQUARE_BONUS[(int)Move.GetFromSquare(mv)]);


                Debug.Assert(orderValue >= 0 && orderValue < 32767);

                ml.moves[i] = Move.SetValue(mv, orderValue);
            }
        }
        public bool Test()
        {
            /*
            string fen;
            fen = "2r112r232311132r23332r3322211bb2322322b211b23122133322212222 r 0 0 0";
            board.SetFEN(fen);
            Perft("perft 4");

            fen = "322r31133333rr32rb231212b122121132111233221231b222322112b332 r 0 0 0";
            board.SetFEN(fen);
            Perft("perft 4");

            fen = "323231123221313323232333r312133131332r1321r31222rb1b1222bb32 r 0 0 0";
            board.SetFEN(fen);
            Perft("perft 4");

            fen = "233r33111132rr3122232132331213r322332112132b11223b313123b32b r 0 0 0";
            board.SetFEN(fen);
            Perft("perft 4");

            fen = "000r02000r00101b000010r02b000000r01120b00001122311111b111111 r 0 0 0";
            board.SetFEN(fen);
            Perft("perft 8");

            fen = "000r02000r00101b000010r02b000000r01120b00001000000011b100000 r 0 0 0";
            board.SetFEN(fen);
            Perft("perft 8");

            string fen = "12340122b12302210000b1131b10211221104r4b00020000310r2221r0303r43 r 0 0 0";
            board.SetFEN(fen);
            ulong p = board.state.penguinOfColor[(int)EColor.RED] | board.state.penguinOfColor[(int)EColor.BLUE];
            while (p != 0)
            {
                ESquare sq = (ESquare)BitOps.GetAndClearLsb(ref p);
                ulong floe = board.GetFloe(sq);
                if (floe != 0) Console.WriteLine("Floe for {0}: {1}", Types.SQUARE_NAMES[(int)sq], board.BitBoardToStr(floe));
            }
            */

            string fen = "12r4r111000000000001b1031b1020122110000b00020000311r2221r0303r43 r 5 4 8";
            board.SetFEN(fen);
            board.Print();

            return true;
        }
    }


}
