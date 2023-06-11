
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace EgdeDancer
{
	public struct TPattern
	{
		// DIRECTIONS
		public ulong[] WEST = new ulong[(int)ESquare.SQ_CNT];
		public ulong[] EAST = new ulong[(int)ESquare.SQ_CNT];
		public ulong[] NW = new ulong[(int)ESquare.SQ_CNT];
		public ulong[] NE = new ulong[(int)ESquare.SQ_CNT];
		public ulong[] SW = new ulong[(int)ESquare.SQ_CNT];
		public ulong[] SE = new ulong[(int)ESquare.SQ_CNT];

		public ulong[] NEXT = new ulong[(int)ESquare.SQ_CNT];	// marks all the squares that are next to a square (2..6 neighbours are possible)

		// ATTACK PATTERN ON EMPTY BOARD
		public ulong[] PENGUIN = new ulong[(int)ESquare.SQ_CNT];

		public TPattern()
		{
			for (ESquare _sq = ESquare.A1; _sq < ESquare.SQ_CNT; _sq++)
			{
				int sq = (int)_sq;
				WEST[sq] = EAST[sq] = NW[sq] = NE[sq] = SW[sq] = SE[sq] = 0;

				int file = Types.FILE_NO[sq];
				int rank = Types.RANK_NO[sq];

				for (int i = 0; i < 8; i++)
				{
					if (i < file) WEST[sq] |= Types.BB_SQUARES[(int)Types.GetSquare(i, rank)];
					if (i > file) EAST[sq] |= Types.BB_SQUARES[(int)Types.GetSquare(i, rank)];
				}

				int next;

				// NW direction increases sq index by 7 on odd ranks and by 8 on even ranks
				next = sq;
				while (Types.SQUARE_TO_NW[next])
				{
                    next += 7 + (Types.RANK_NO[next] + 1) % 2;
                    NW[sq] |= Types.BB_SQUARES[next];
				}

				// NE direction increases sq index by 8 on odd ranks and by 9 on even ranks 
				next = sq;
				while (Types.SQUARE_TO_NE[next])
				{
					next += 8 + (Types.RANK_NO[next] + 1) % 2;
                    NE[sq] |= Types.BB_SQUARES[next];
                }

                // SW direction decreases sq index by 8 on even ranks and by 9 on odd ranks
                next = sq;
				while (Types.SQUARE_TO_SW[next])
				{
					next -= (8 + Types.RANK_NO[next] % 2);
                    SW[sq] |= Types.BB_SQUARES[next];
                }

                // SE direction decreases sq index by 7 on even ranks and by 8 on odd ranks
                next = sq;
				while (Types.SQUARE_TO_SE[next])
				{
					next -= (7 + Types.RANK_NO[next] % 2);
                    SE[sq] |= Types.BB_SQUARES[next];
                }

                // NEXT - marks all the direct neighbours to a square
                // sq index offset of neighbours is -9 -8 -7 -1 +1 +7 +8 +9 as long as they don't hit the edge of the board
                NEXT[sq] = 0;
				if (Types.FILE_NO[sq] > 0) NEXT[sq] |= Types.BB_SQUARES[sq-1];	// WEST
				if (Types.FILE_NO[sq] < 7) NEXT[sq] |= Types.BB_SQUARES[sq+1];  // EAST

				
				if (Types.SQUARE_TO_SW[sq]) NEXT[sq] |= Types.BB_SQUARES[sq - 8 - Types.RANK_NO[sq] % 2];    // SW
				if (Types.SQUARE_TO_SE[sq]) NEXT[sq] |= Types.BB_SQUARES[sq - 7 - Types.RANK_NO[sq] % 2];    // SE
				
				if (Types.SQUARE_TO_NW[sq]) NEXT[sq] |= Types.BB_SQUARES[sq + 7 + (Types.RANK_NO[sq] + 1) % 2]; // NW
				if (Types.SQUARE_TO_NE[sq]) NEXT[sq] |= Types.BB_SQUARES[sq + 8 + (Types.RANK_NO[sq] + 1) % 2]; // NE
	
				// The Penguin pattern is the combination of all directions
				PENGUIN[sq] = EAST[sq] | WEST[sq] | NW[sq] | NE[sq] | SW[sq] | SE[sq];
			}
		}
	}
	public struct TState
	{
		public EPiece[] squares = new EPiece[(int)ESquare.SQ_CNT];
		public ulong[] penguinOfColor = new ulong[(int)EColor.BOTH_SIDES];
		public ulong[] fishesOfCount = new ulong[5];                            // fishesOfCount[0] = BitBoard of holes, fishesOfCount[1..4] = BitBoard with fishes of that number 
		public int[] fishesCollected = new int[(int)EColor.BOTH_SIDES];
		public ulong blocker;                                                   // marks all blockers on board = penguins of both colors and holes
		public ulong trapped;                                                   // marks the penguins of both colors that can no longer move
		public int totalFishes;													// the total number of fishes on the board

		public EColor sideToMove;
		public ulong hash;          // Zobrist Hash
		public int hmc;             // Half Move Clock for Ply Count

		public TState()
		{
			hash = 0;
			hmc = 0;
			sideToMove = EColor.RED;
			blocker = 0xFFFFFFFFFFFFFFFF;  // empty board means every square is a blocker
			trapped = 0x0;				   	
			totalFishes = 0;

			for (ESquare sq = ESquare.A1; sq < ESquare.SQ_CNT; sq++) squares[(int)sq] = EPiece.EMPTY;
			penguinOfColor[(int)EColor.RED] = penguinOfColor[(int)EColor.BLUE] = 0;
			for (int count = 0; count < 5; count++) fishesOfCount[count] = 0;
			fishesCollected[(int)EColor.RED] = fishesCollected[(int)EColor.BLUE] = 0;
		}
	}
	public class THistoryData
	{
		public ulong hash;
		public uint move;
		public int value;
	}
	public struct TZKeys
	{
		public ulong[,] squares = new ulong[(int)ESquare.SQ_CNT, 8];	// Red Penguin, Blue Penguin, 0, 0 and 1-4 Fishes 
		public ulong sideToMove;
		public ulong[] redFishesCollected = new ulong[128];				// max possible score not exactly known yet but 128 should be safe, blue score hashing not needed  

		public TZKeys() 
		{
			int rnd_idx = 0;
			for (ESquare sq = ESquare.A1; sq < ESquare.SQ_CNT; sq++)
            {
				for (EPiece p = EPiece.R_PENGUIN; p <= EPiece.FISH_4; p++)
				{
					squares[(int)sq, (int)p] = p != EPiece.PIECE_CNT && p != EPiece.EMPTY ? squares[(int)sq, (int)p] = EDRandom.GetRandomNumber(rnd_idx++) : 0;
				}
			}
			sideToMove = EDRandom.GetRandomNumber(rnd_idx++);
			
			redFishesCollected[0] = 0;
			for (int i = 1; i < redFishesCollected.Length; i++) redFishesCollected[i] = EDRandom.GetRandomNumber(rnd_idx++);
		}
	}
	public class TBoard
	{
		public static int GAME_PHASE_GRADIENT_MAX = 50;

		public TPattern pattern;
		public TState state;
		public THistoryData[] history = new THistoryData[Types.MAX_PLY];
		public TZKeys zKeys;
		private TMoveList[] movelists = new TMoveList[Types.MAX_PLY];

		public TBoard()
		{
			pattern = new TPattern();
			state = new TState();
			zKeys = new TZKeys();
			for (int i = 0; i < history.Length; i++) history[i] = new THistoryData();
			for (int i = 0; i < movelists.Length; i++) movelists[i] = new TMoveList();

			SetFEN(GenerateRandomStartFEN(4));
		}
		public bool Print()
		{
			Console.WriteLine("--- Board ---\t\t--- Data ---\t\t--- Trapped ---");
			for (int rank = 7; rank >= 0; rank--)
			{
				for (int file = 0; file < 8; file++)
				{
					Console.Write("{0} ", Types.PIECE_SYMBOLS[(int)state.squares[(int)Types.GetSquare(file, rank)]]);
				}
				switch (rank)
				{
					case 7: Console.Write("\tSideToMove: {0} \t{1}", state.sideToMove,BitBoardToStr(state.trapped)); break;
					case 6: Console.Write("  \tRed Score : {0}+{1}\\{2}", state.fishesCollected[(int)EColor.RED], GetReachableFishes(EColor.RED), state.totalFishes); break;
					case 5: Console.Write("\tBlue Score: {0}+{1}\\{2}", state.fishesCollected[(int)EColor.BLUE], GetReachableFishes(EColor.BLUE), state.totalFishes); break;
					case 3: Console.Write("\t--- Signature ---\t--- Game Info ---"); break;
					case 2: Console.Write("  \tHash: {0:x}\tMG: {1}%", state.hash, 100*GetGamePhaseGradient()/GAME_PHASE_GRADIENT_MAX); break;
					case 1: Console.Write("\tHash: {0:x}\tEG: {1}%", CalculateHash(), 100*(GAME_PHASE_GRADIENT_MAX-GetGamePhaseGradient())/GAME_PHASE_GRADIENT_MAX); break;
				}
				Console.WriteLine();
				if ((rank % 2) == 1) Console.Write(" ");
			}
			Console.WriteLine("\nFEN: " + GetFEN());
			TMoveList list = GetAllMoves();
			Console.Write("Moves: {0} ", list.count);
			for (int i = 0; i < list.count; i++)
			{
				if (i == 0 || Move.GetFromSquare(list.moves[i]) != Move.GetFromSquare(list.moves[i - 1])) Console.Write("\n[{0}]: ", Types.SQUARE_NAMES[(int)Move.GetFromSquare(list.moves[i])]);
				Console.Write(Move.SAN(list.moves[i]) + " ");
			}
			Console.WriteLine("");
            ulong p = state.penguinOfColor[(int)EColor.RED] | state.penguinOfColor[(int)EColor.BLUE];
			int floeCnt = 0;
            while (p != 0)
            {
                ESquare sq = (ESquare)BitOps.GetAndClearLsb(ref p);
                ulong floe = GetFloe(sq);
				if (floe != 0)
				{
					Console.WriteLine("Penguin on {0} owns floe: {1}", Types.SQUARE_NAMES[(int)sq], BitBoardToStr(floe));
					floeCnt++;
				}
            }
			Console.WriteLine("{0} captured floe{1} found", floeCnt, floeCnt == 1 ? "" : "s") ;

			ulong area = GetReachableFields(EColor.RED);
			Console.WriteLine("Reachable for RED: " + BitBoardToStr(area));
            area = GetReachableFields(EColor.BLUE);
            Console.WriteLine("Reachable for BLUE: " + BitBoardToStr(area));

            return true;
		}
		public bool SetFEN(string fen)
		{
			// FEN Format squares(A1..H8) stm red_fishes blue_fishes hmc

			string[] strList = fen.Split(' ');

			if (strList[0].Length != 64)
			{
				Console.WriteLine("Invalid FEN! FEN contains {0} elements. Expected are 64 elements", strList[0].Length);
				return true;
			}

			char c;

			state.penguinOfColor[(int)EColor.RED] = state.penguinOfColor[(int)EColor.BLUE] = 0;
			state.fishesOfCount[0] = state.fishesOfCount[1] = state.fishesOfCount[2] = state.fishesOfCount[3] = state.fishesOfCount[4] = 0;

			// Set the board quares and bitboards
			for (ESquare sq = ESquare.A1; sq < ESquare.SQ_CNT; sq++)
			{
				c = strList[0].ElementAt((int)sq);
				switch (c)
				{
					case 'r':
						state.squares[(int)sq] = EPiece.R_PENGUIN;
						state.penguinOfColor[(int)EColor.RED] |= Types.BB_SQUARES[(int)sq];
						state.fishesOfCount[0] |= Types.BB_SQUARES[(int)sq];
						break;
					case 'b':
						state.squares[(int)sq] = EPiece.B_PENGUIN;
						state.penguinOfColor[(int)EColor.BLUE] |= Types.BB_SQUARES[(int)sq];
						state.fishesOfCount[0] |= Types.BB_SQUARES[(int)sq];
						break;
					case '0': state.squares[(int)sq] = EPiece.EMPTY; state.fishesOfCount[0] |= Types.BB_SQUARES[(int)sq]; break;
					case '1': state.squares[(int)sq] = EPiece.FISH_1; state.fishesOfCount[1] |= Types.BB_SQUARES[(int)sq]; break;
					case '2': state.squares[(int)sq] = EPiece.FISH_2; state.fishesOfCount[2] |= Types.BB_SQUARES[(int)sq]; break;
					case '3': state.squares[(int)sq] = EPiece.FISH_3; state.fishesOfCount[3] |= Types.BB_SQUARES[(int)sq]; break;
                    case '4': state.squares[(int)sq] = EPiece.FISH_4; state.fishesOfCount[4] |= Types.BB_SQUARES[(int)sq]; break;
                    default:
						Console.WriteLine("FEN contains illegal character '{0}' at position {1}", c, (int)sq);
						return true;
				}
			}

			if (strList.Length < 2)
			{
				Console.WriteLine("FEN misses siteToMove part");
				return true;
			}

			c = strList[1].ElementAt(0);
			switch (c)
			{
				case 'r': state.sideToMove = EColor.RED; break;
				case 'b': state.sideToMove = EColor.BLUE; break;
				default:
					Console.WriteLine("FEN contains illegal character '{0}' for siteToMove part", c);
					return true;
			}

			state.fishesCollected[(int)EColor.RED] = 0;
			state.fishesCollected[(int)EColor.BLUE] = 0;
			state.hmc = 0;

			if (strList.Length > 4)
			{
				try
				{
					state.fishesCollected[(int)EColor.RED] = int.Parse(strList[2]);
				}
				catch { Console.WriteLine("RED fishcount missing, assuming 0."); }

				try
				{
					state.fishesCollected[(int)EColor.BLUE] = int.Parse(strList[3]);
				}
				catch { Console.WriteLine("BLUE fishcount missing, assuming 0."); }

				try
				{
					state.hmc = int.Parse(strList[4]);
				}
				catch { Console.WriteLine("HMC missing, assuming 0."); }

			}

			// correct hmc and fish count if specified as less than already placed penguins
            int redPenguinCount = BitOps.PopCount(state.penguinOfColor[(int)EColor.RED]);
            int bluePenguinCount = BitOps.PopCount(state.penguinOfColor[(int)EColor.BLUE]);
            state.hmc = Math.Max(state.hmc, redPenguinCount + bluePenguinCount);
            state.fishesCollected[(int)EColor.RED] = Math.Max(state.fishesCollected[(int)EColor.RED], redPenguinCount);
            state.fishesCollected[(int)EColor.BLUE] = Math.Max(state.fishesCollected[(int)EColor.BLUE], bluePenguinCount);

            // calulate the total fishes of the game, this is the sum of the already collected fishes and the remaining ones
            state.totalFishes = state.fishesCollected[(int)EColor.RED] + state.fishesCollected[(int)EColor.BLUE];
			for (int i = 1; i < 5; i++) state.totalFishes += i * BitOps.PopCount(state.fishesOfCount[i]); 

            // Complete the state by filling the BitBoards
            state.blocker = state.fishesOfCount[0] | state.penguinOfColor[(int)EColor.RED] | state.penguinOfColor[(int)EColor.BLUE];
			state.hash = CalculateHash();
			state.trapped = CalculateTrappedPenguins();

			return true;
		}
		public string GetFEN()
		{
			string fen = "";
			for (ESquare sq = ESquare.A1; sq < ESquare.SQ_CNT; sq++) fen += Types.PIECE_SYMBOLS[(int)state.squares[(int)sq]];
			fen += state.sideToMove == EColor.RED ? " r" : " b";
			fen += String.Format(" {0} {1} {2}", state.fishesCollected[(int)EColor.RED], state.fishesCollected[(int)EColor.BLUE], state.hmc);
			return fen;
		}
		public uint GetLastMove()
		{
			return state.hmc > 0 ? history[state.hmc - 1].move : Move.INVALID_MOVE;
		}
		public string GenerateRandomStartFEN(int penguinCount)
        {
			EPiece[] squares = new EPiece[64];
			int remainingFishes = 72;
            Random rnd = new();

            for (int i = 0; i < squares.Length / 2; i++)
            {
				EPiece p = EPiece.EMPTY;

				int r = rnd.Next(remainingFishes);
				if (r > 4) 
				{
					int fish = r / 20 + 1;
					remainingFishes -= fish;
					p = Types.PIECE_OF_FISH[fish];
				};
                squares[i] = p;								// set in upper half of board
                squares[squares.Length - 1 - i] = p;		// mirror into the 2nd half board
            }

			for (int i = 0; i < penguinCount; i++)
            {
				int index = rnd.Next(i * 6, (i + 1) * 6);
				squares[index] = EPiece.R_PENGUIN;
				squares[squares.Length - 1 - index] = EPiece.B_PENGUIN;
			}

			string fen = "";
			for (int i = 0; i < squares.Length; i++) fen += Types.PIECE_SYMBOLS[(int)squares[i]];
			fen += " r 0 0 0";
			return fen;
		}
		public string BitBoardToStr(ulong b)
        {
			// return a string of the names of the marked squares in a bitboard
			string s = "";
			while (b != 0)
			{
				ESquare next = (ESquare)BitOps.GetAndClearLsb(ref b);
				s += Types.SQUARE_NAMES[(int)next] + " ";
			}
			return s;
		}
		public int GetReachableFishes(EColor side)
		{
			ulong area = GetReachableFields(side);
			int fishes = 0;
			for (int i = 1; i < 5; i++) fishes += i * BitOps.PopCount(area & state.fishesOfCount[i]);
			return fishes;
		}
		public ulong GetReachableFields(EColor side)
		{
			ulong penguins = state.penguinOfColor[(int) side];
			ulong area = 0;

			while ((penguins & ~area) != 0)
			{
				ESquare sq = (ESquare) BitOps.GetAndClearLsb(ref penguins);
				FloodFillArea(sq, ref area);
			}

			return area;
		}
		public ulong GetFloe(ESquare sq)
		{
			// returns the bits of a flow owned by a penguin on sq, returns 0 if the penguin does not own a flow
			Debug.Assert(state.squares[(int)sq] == EPiece.R_PENGUIN || state.squares[(int)sq] == EPiece.B_PENGUIN);
			
			EColor side = Types.COLOR_OF[(int) state.squares[(int)sq]];
			ulong floe = 0;

			if (FloodFillFloe(sq, side, ref floe)) return floe & ~Types.BB_SQUARES[(int) sq];

			return 0;
		}
        private void FloodFillArea(ESquare sq, ref ulong area)
        {
            area |= Types.BB_SQUARES[(int)sq];

            ulong next = pattern.NEXT[(int)sq] & ~area & ~(state.fishesOfCount[0] | state.penguinOfColor[(int) EColor.RED] | state.penguinOfColor[(int)EColor.BLUE]);

            while (next != 0)
            {
                ESquare n = (ESquare)BitOps.GetAndClearLsb(ref next);
                FloodFillArea(n, ref area);
            }
        }
        private bool FloodFillFloe(ESquare sq, EColor side, ref ulong floe)
		{
			EColor enemySide = side == EColor.RED ? EColor.BLUE : EColor.RED;
			
			floe |= Types.BB_SQUARES[(int) sq];
			bool isFloe = true;

			if ((pattern.NEXT[(int)sq] & state.penguinOfColor[(int)enemySide]) != 0) return false;
            ulong next = pattern.NEXT[(int)sq] & ~floe & ~state.fishesOfCount[0];

            while (next != 0 && isFloe)
			{
				ESquare n = (ESquare) BitOps.GetAndClearLsb(ref next);
				isFloe = FloodFillFloe(n, side, ref floe);
			}

			return isFloe;
		}

        // Calculate a gradient for the phase of the game depending on the number of still existing floes
        // GAME_PHASE_GRADIENT_MAX or more = Opening or MG -> 0 for EG
        public int GetGamePhaseGradient() => Math.Min(GAME_PHASE_GRADIENT_MAX, 64 - BitOps.PopCount(state.fishesOfCount[0]));
		// Interpolate a value between MG and EG
		public int InterpolateValue(int mg, int eg) => (mg * GetGamePhaseGradient() + eg * (GAME_PHASE_GRADIENT_MAX - GetGamePhaseGradient())) / GAME_PHASE_GRADIENT_MAX;

        public string GetPlayedMovesAsSAN()
		{
			string m = "";
			for (int i = 0; i < state.hmc; i++) if (history[i].move != Move.INVALID_MOVE) m += Move.SAN(history[i].move) + " ";    // when we set a fen with hmc >0 we miss the initial moves
			return m;
		}
		public TMoveList GetAllMoves()
		{
			return GetAllMoves(-1);
		}
		public bool IsSetPhase()
		{
			Debug.Assert((state.hmc > 7) || BitOps.PopCount(state.penguinOfColor[(int)EColor.RED] | state.penguinOfColor[(int)EColor.BLUE]) == state.hmc);
			return state.hmc < 8;
		}
		public TMoveList GetAllMoves(int ply)
		{
			TMoveList list = ply >= 0 && ply < movelists.Length ? movelists[ply] : new TMoveList();
			Debug.Assert(list.count == 0);

			int _stm = (int)state.sideToMove;

			EPiece mp = (EPiece) _stm;
			ulong bitb = state.penguinOfColor[_stm];

			// Setup phase until all penguins are on the board, only set moves are generated to squares with 1 fish
			if (BitOps.PopCount(bitb) < Types.PENGUIN_COUNT)
            {
				bitb = state.fishesOfCount[1];
				while (bitb != 0)
				{
					ESquare toSq = (ESquare)BitOps.GetAndClearLsb(ref bitb);
					list.AddMove(Move.Create(mp, toSq));
				}
				return list;
			}

			while (bitb != 0)     // loop through the 4 penguins
			{
				ESquare fromSq = (ESquare) BitOps.GetAndClearLsb(ref bitb);
				ulong attacks = GetPenguinAttacks(fromSq);
				while (attacks != 0)
                {
					ESquare toSq = (ESquare) BitOps.GetAndClearLsb(ref attacks);
					list.AddMove(Move.Create(mp, fromSq, toSq, state.squares[(int)toSq]));
				}
            }

			if (list.count == 0 && !GameOver()) list.AddMove(Move.NULL_MOVE); // if one side has no move but the other still has we have to pass and make a NULL move
			return list;
		}
		public TMoveList GetThisMove(int ply, uint move)
		{
			TMoveList list = ply >= 0 && ply < movelists.Length ? movelists[ply] : new TMoveList();

			// return a list that contains only one given move, usually the hash move
			Debug.Assert(list.count == 0);
			list.count = 0;

			if (GameOver()) return list;

			list.AddMove(move);
			return list;
		}
		public bool IsValidMove(uint move)
        {
			EPiece piece = Move.GetMovedPiece(move);
			ESquare fromSq = Move.GetFromSquare(move);
			ESquare toSq = Move.GetToSquare(move);
			EPiece fishes = Move.GetFish(move);

			move = Move.GetID(move);
			if (move == Move.INVALID_MOVE) return false;
			if (move == Move.NULL_MOVE) return true;		// we assume a NULL move as always legal

			// check whether the game is already over, then no move is legal
			if (GameOver()) return false;

			// check whether a piece is moved from the player on the turn
			if (Types.COLOR_OF[(int)piece] != state.sideToMove) return false;

			// Each move must collect 1-3 fishes
			if (fishes != EPiece.FISH_1 && fishes != EPiece.FISH_2 && fishes != EPiece.FISH_3 && fishes != EPiece.FISH_4) return false;

			// do the fishes collected with this move match the number of fishes at the target square
			if (fishes != state.squares[(int)toSq]) return false;

			// check for a set move
			if (Move.IsSetMove(move) && fishes == EPiece.FISH_1 && BitOps.PopCount(state.penguinOfColor[(int) state.sideToMove]) < Types.PENGUIN_COUNT) return true;

			// check whether the moved piece is really at this place on the board
			if (state.squares[(int)fromSq] != piece) return false;

			// check valid move pattern TODO: Consider initial placement
			if ((GetPenguinAttacks(fromSq) & Types.BB_SQUARES[(int)toSq]) == 0) return false;

			return true;
		}
		public ulong CalculateTrappedPenguins()
        {
			// This is a slow method to calculate the trapped penguins to init the trapped BitBoard
			// The trapped BitBoard is normally updated in makeMove
			
			ulong b = 0;
			ulong targets = ~state.blocker;

			ulong p = state.penguinOfColor[(int)EColor.RED] | state.penguinOfColor[(int)EColor.BLUE];
			while (p != 0)
            {
				ESquare sq = (ESquare) BitOps.GetAndClearLsb(ref p);
				if ((pattern.NEXT[(int)sq] & targets) == 0) b |= Types.BB_SQUARES[(int)sq];
            }
			return b;
		}
		public ulong CalculateHash()
		{
			// This is a slow method to calculate the hash to initialize it
			// The hash is normally updated in makeMove
			ulong hash = 0;

			for (ESquare sq = ESquare.A1; sq < ESquare.SQ_CNT; sq++)
			{
				if (state.squares[(int) sq] != EPiece.EMPTY) hash ^= zKeys.squares[(int)sq, (int)state.squares[(int) sq]];
			}
			if (state.sideToMove == EColor.RED) hash ^= zKeys.sideToMove;

            Debug.Assert(state.fishesCollected[(int) EColor.RED] < zKeys.redFishesCollected.Length);
			hash ^= zKeys.redFishesCollected[state.fishesCollected[(int)EColor.RED]];

			return hash;
		}
		public void MakeMove(uint move)
        {
			if (Move.GetID(move) != Move.NULL_MOVE) MakeRegularMove(move); else MakeNullMove();
        }
		public void UnMakeMove(uint move)
		{
			if (Move.GetID(move) != Move.NULL_MOVE) UnMakeRegularMove(move); else UnMakeNullMove();
		}
		public void MakeRegularMove(uint move)
        {
			Debug.Assert(IsValidMove(move));
			Debug.Assert(state.hmc < Types.MAX_PLY);

			history[state.hmc].move = move;                         // save the move that was made in this position
			history[state.hmc].hash = state.hash;					// save the old hash

			state.hmc++;                                            // Increase the move counter
			MovePieceFromTo(move);                                  // Move the Piece, Remove Fishes and Update Score

			state.sideToMove = (EColor)(EColor.BLUE - state.sideToMove);  // Update Side to Move
			state.hash ^= zKeys.sideToMove;

			Debug.Assert(state.hash == CalculateHash());
			Debug.Assert(state.trapped == CalculateTrappedPenguins());
			Debug.Assert(state.blocker == (state.fishesOfCount[0] | state.penguinOfColor[(int)EColor.RED] | state.penguinOfColor[(int)EColor.BLUE]));
		}
		public void UnMakeRegularMove(uint move)
		{

			state.sideToMove = (EColor)(EColor.BLUE - state.sideToMove);  // Update Side to Move
			state.hash ^= zKeys.sideToMove;

			MovePieceToFrom(move);										// Move the Piece back, Restore Fishes and Score
			state.hmc--;

			Debug.Assert(state.hash == CalculateHash());
			Debug.Assert(state.trapped == CalculateTrappedPenguins());
		}
		public void MakeNullMove()
		{
			Debug.Assert(state.hmc < Types.MAX_PLY);

			history[state.hmc].move = Move.NULL_MOVE;               // save the move that was made in this position
			history[state.hmc].hash = state.hash;                   // save the old hash

			state.hmc++;                                            // Increase the move counter

			state.sideToMove = (EColor)(EColor.BLUE - state.sideToMove);  // Update Side to Move
			state.hash ^= zKeys.sideToMove;

			Debug.Assert(state.hash == CalculateHash());
		}
		public void UnMakeNullMove()
		{

			state.sideToMove = (EColor)(EColor.BLUE - state.sideToMove);  // Update Side to Move
			state.hash ^= zKeys.sideToMove;
			state.hmc--;

			Debug.Assert(state.hash == CalculateHash());
		}
		public bool GameOver()
        {
			// if all penguins are set and all are trapped the game is over
			if (state.trapped != 0 && 
				state.hmc > 7 && 
				(state.penguinOfColor[(int)EColor.RED] | state.penguinOfColor[(int)EColor.BLUE]) == state.trapped) return true;
			return false;
        }
		public EColor Winner()
        {
			if (!GameOver()) return EColor.NO_COLOR;
			if (state.fishesCollected[(int)EColor.RED] > state.fishesCollected[(int)EColor.BLUE]) return EColor.RED; else
				if (state.fishesCollected[(int)EColor.RED] < state.fishesCollected[(int)EColor.BLUE]) return EColor.BLUE;
			
			return EColor.NO_COLOR;
		}
		public void MovePieceFromTo(uint move)
        {
			EPiece movedPiece = Move.GetMovedPiece(move);
			ESquare fromSq = Move.GetFromSquare(move);
			ESquare toSq = Move.GetToSquare(move);
			EColor stm = Types.COLOR_OF[(int)movedPiece];
			EPiece fishes = Move.GetFish(move);
			int fishCount = Types.FISH_COUNT[(int)fishes];
			bool isSetMove = Move.IsSetMove(move);

			Debug.Assert(isSetMove || state.squares[(int)fromSq] == movedPiece);
			Debug.Assert(state.sideToMove == Types.COLOR_OF[(int)movedPiece]);
			Debug.Assert(fishes == state.squares[(int)toSq]);

			// Step 1: Remove the fishes from the fishes bitboard
			state.fishesOfCount[fishCount] &= ~Types.BB_SQUARES[(int)toSq];
			state.fishesOfCount[0] |= Types.BB_SQUARES[(int)toSq];
			state.hash ^= zKeys.squares[(int)toSq, (int) state.squares[(int)toSq]];

			// Step 2: Remove the penguin from the old square and put it on the new square
			//         In case of a set move skip the removal part of the operation
			if (!Move.IsSetMove(move))
			{
				state.penguinOfColor[(int)stm] &= ~Types.BB_SQUARES[(int)fromSq];
				state.squares[(int)fromSq] = EPiece.EMPTY;
				state.hash ^= zKeys.squares[(int)fromSq, (int)movedPiece];
			}

			state.penguinOfColor[(int) stm] |=  Types.BB_SQUARES[(int)toSq];
			state.squares[(int)toSq] = movedPiece;
			state.hash ^= zKeys.squares[(int)toSq, (int)movedPiece];

			// Step 3: Add the fishes to the collected score
			if (movedPiece == EPiece.R_PENGUIN) state.hash ^= zKeys.redFishesCollected[state.fishesCollected[(int)EColor.RED]];
			state.fishesCollected[(int)stm] += fishCount;
			if (movedPiece == EPiece.R_PENGUIN) state.hash ^= zKeys.redFishesCollected[state.fishesCollected[(int)EColor.RED]];

			// Step 4: Update the blocker board by adding the new toSq to the blockers
			state.blocker |= Types.BB_SQUARES[(int)toSq];

			// Step 5: Check whether the moving piece is now trapped or whether it trappes an adjecent piece on its new location
			ulong targets = ~state.blocker;
			if ((pattern.NEXT[(int)toSq] & targets) == 0) state.trapped |= Types.BB_SQUARES[(int)toSq];   // the moving piece itself has no targets anymore
			
			ulong neighbours = pattern.NEXT[(int)toSq] & (state.penguinOfColor[(int)EColor.RED] | state.penguinOfColor[(int)EColor.BLUE]);
			while (neighbours != 0)
            {
				ESquare nb = (ESquare) BitOps.GetAndClearLsb(ref neighbours);
				if ((pattern.NEXT[(int) nb] & targets) == 0) state.trapped |= Types.BB_SQUARES[(int) nb];   // a neighbour has no targets anymore
			}
		}
		public void MovePieceToFrom(uint move)
		{
			EPiece movedPiece = Move.GetMovedPiece(move);
			ESquare fromSq = Move.GetFromSquare(move);
			ESquare toSq = Move.GetToSquare(move);
			EColor stm = Types.COLOR_OF[(int)movedPiece];
			EPiece fishes = Move.GetFish(move);
			int fishCount = Types.FISH_COUNT[(int)fishes];

			// Step 1: Remove the trapped flag from the toSq and untrap all neighbours of the toSq
			state.trapped &= ~Types.BB_SQUARES[(int)toSq];
			ulong neighbours = pattern.NEXT[(int)toSq] & (state.penguinOfColor[(int)EColor.RED] | state.penguinOfColor[(int)EColor.BLUE]);
			while (neighbours != 0)
			{
				ESquare nb = (ESquare)BitOps.GetAndClearLsb(ref neighbours);
				state.trapped &= ~Types.BB_SQUARES[(int)nb];   
			}

			// Step 2:  Update the blocker board by removing the toSq from the blockers
			state.blocker &= ~Types.BB_SQUARES[(int)toSq];

			// Step 3: remove the fishes from the collected score
			if (movedPiece == EPiece.R_PENGUIN) state.hash ^= zKeys.redFishesCollected[state.fishesCollected[(int)EColor.RED]];
			state.fishesCollected[(int)stm] -= fishCount;
			if (movedPiece == EPiece.R_PENGUIN) state.hash ^= zKeys.redFishesCollected[state.fishesCollected[(int)EColor.RED]];

			// Step 4: Remove the penguin from the new TO square, put the fishes back and put the penguin back on the old FROM square
			//         In case of a set move skip the putting back part of the operation
			state.penguinOfColor[(int)stm] &= ~Types.BB_SQUARES[(int)toSq];
			state.squares[(int)toSq] = fishes;
			state.hash ^= zKeys.squares[(int)toSq, (int)movedPiece];

			if (!Move.IsSetMove(move))
			{
				state.penguinOfColor[(int)stm] |= Types.BB_SQUARES[(int)fromSq];
				state.squares[(int)fromSq] = movedPiece;
				state.hash ^= zKeys.squares[(int)fromSq, (int)movedPiece];
			}

			// Step 5: Update the fish bitboard by adding the fishes back
			state.fishesOfCount[fishCount] |= Types.BB_SQUARES[(int)toSq];
            state.fishesOfCount[0] &= ~Types.BB_SQUARES[(int)toSq];
			state.hash ^= zKeys.squares[(int)toSq, (int)state.squares[(int)toSq]];
		}
		public int GetMaxFishes()
		{
			if (IsSetPhase()) return 1;
			for (int fishCnt = 4; fishCnt > 0; fishCnt--) if (state.fishesOfCount[fishCnt] != 0) return fishCnt;
			return 0;
        }
		public bool IsTrappingMove(uint mv)
		{
			ulong holes = state.blocker | Types.BB_SQUARES[(int)Move.GetToSquare(mv)];
			ulong penguins = Move.GetMovedPiece(mv) == EPiece.R_PENGUIN ? state.penguinOfColor[(int)EColor.BLUE] : state.penguinOfColor[(int)EColor.RED];
			penguins &= pattern.NEXT[(int) Move.GetToSquare(mv)];

            while (penguins != 0)
			{
				ESquare sq = (ESquare) BitOps.GetAndClearLsb(ref penguins);
				if ((pattern.NEXT[(int)sq] & ~holes) == 0) return true;
			}
			return false;
		}
        public uint LastMove() 
		{
			if (state.hmc > 1) return history[state.hmc - 1].move; else return Move.INVALID_MOVE; 
		}
        public uint Next2LastMove()
        {
			if (state.hmc > 1) return history[state.hmc - 2].move; else return Move.INVALID_MOVE;
        }
        public ulong Perft(int level)
		{
			if (level <= 0) return 0;

			ulong result = 0;
			TMoveList list = GetAllMoves();

			if (level == 1)
			{
				return (ulong)list.count;
			}

			for (int i = 0; i < list.count; i++)
			{
				MakeMove(list.moves[i]);
				result += Perft(level - 1);
				UnMakeMove(list.moves[i]);
			}
			return result;
		}
		public TDivideList Divide(int level)
        {
			TDivideList dml = new TDivideList();
			TMoveList ml = GetAllMoves();

			for (int i = 0; i < ml.count; i++)
			{
				MakeMove(ml.moves[i]);
				ulong subPerft = Perft(level - 1);
				UnMakeMove(ml.moves[i]);

				dml.AddMove(ml.moves[i], subPerft);
			}

			return dml;
		}
		public uint StrToMove(string san)
        {
			ESquare fromSq, toSq;
			EPiece movedPiece, fishes;
			char c0, c1, c2, c3;

			if (san == "") return Move.INVALID_MOVE;
			if (san == "0000") return Move.NULL_MOVE;

			san = System.Text.RegularExpressions.Regex.Replace(san, @"\s+", "");		// remove all white spaces

			if (san.Length != 4) return Move.INVALID_MOVE;

			c0 = san.ElementAt(0);
			c1 = san.ElementAt(1);
			c2 = san.ElementAt(2);
			c3 = san.ElementAt(3);

			if ((c0 < 'a') || (c0 > 'h')) return Move.INVALID_MOVE;
			if ((c2 < 'a') || (c2 > 'h')) return Move.INVALID_MOVE;
			if ((c1 < '1') || (c1 > '8')) return Move.INVALID_MOVE;
			if ((c3 < '1') || (c3 > '8')) return Move.INVALID_MOVE;

			fromSq = Types.GetSquare(c0 - 97, c1 - 49);
			toSq = Types.GetSquare(c2 - 97, c3 - 49);
			bool isSetMove = fromSq == toSq;

			movedPiece = state.squares[(int)fromSq];
			if (isSetMove) movedPiece = (EPiece) state.sideToMove;
			fishes = state.squares[(int)toSq];

			uint move = !isSetMove ? Move.Create(movedPiece, fromSq, toSq, fishes) : Move.Create(movedPiece, toSq);
			if (IsValidMove(move)) return move;
			
			return Move.INVALID_MOVE;
        }
		public int Plys2Mate(int aValue)
		{
			if (IsMateScore(aValue))
			{
				if (aValue > 0) return (int)(1 + EScores.INFINITY - aValue);
				else return (int)(1 + EScores.INFINITY + aValue);
			}

			return (int)EScores.INFINITY;
		}
		public bool IsMateScore(int aValue)
		{
			return (aValue != (int)EScores.NO_SCORE) && ((aValue < -(int)EScores.INFINITY + Types.MAX_PLY) || (aValue > (int)EScores.INFINITY - Types.MAX_PLY));
		}
		public string ScoreToStr(int aValue)
		{
			int turns, plys;

			if (IsMateScore(aValue))
			{
				if (aValue > 0)
				{
					plys = (int)EScores.INFINITY - aValue + 1;
				}
				else
				{
					plys = (int)EScores.INFINITY + aValue + 1;
				}

				turns = plys / 2;

				if (aValue < 0)
				{
					return String.Format("mate -{0}", turns);
				}
				else
				{
					return String.Format("mate {0}", turns);
				}
			}

			return String.Format("cp {0}", aValue);
		}
		public int GetMateScore(int ply)
		{
			return (int)EScores.INFINITY - ply;
		}
		public int GetMatedScore(int ply)
		{
			return -(int)EScores.INFINITY + ply;
		}
		public ulong GetPenguinAttacks(ESquare _sq)
        {
			ulong bitb, result = 0;
			int bit;
			int sq = (int) _sq;

			bitb = state.blocker & pattern.WEST[sq];																		// get the bits to the WEST
			bit = BitOps.GetMsb(bitb);																						// get the square of the most EAST blocker
			bitb = bitb != 0 ? pattern.WEST[sq] & ~pattern.WEST[bit] & ~Types.BB_SQUARES[bit] : pattern.WEST[sq];	        // remove the squares behind the blocker and the blocker itself
			result |= bitb;

			bitb = state.blocker & pattern.EAST[sq];																		// get the bits to the EAST
			bit = BitOps.GetLsb(bitb);																						// get the square of the most WEST blocker
			bitb = bitb != 0 ? pattern.EAST[sq] & ~pattern.EAST[bit] & ~Types.BB_SQUARES[bit] : pattern.EAST[sq];			// remove the squares behind the blocker and the blocker itself
			result |= bitb;

			bitb = state.blocker & pattern.NE[sq];																			// get the bits to the NE
			bit = BitOps.GetLsb(bitb);																						// get the square of the 1st blocker
			bitb = bitb != 0 ? pattern.NE[sq] & ~pattern.NE[bit] & ~Types.BB_SQUARES[bit] : pattern.NE[sq];					// remove the squares behind the blocker and the blocker itself
			result |= bitb;
			
			bitb = state.blocker & pattern.NW[sq];																			// get the bits to the NW
			bit = BitOps.GetLsb(bitb);																						// get the square of the 1st blocker
			bitb = bitb != 0 ? pattern.NW[sq] & ~pattern.NW[bit] & ~Types.BB_SQUARES[bit] : pattern.NW[sq];					// remove the squares behind the blocker and the blocker itself
			result |= bitb;
            
            bitb = state.blocker & pattern.SW[sq];																			// get the bits to the SW
			bit = BitOps.GetMsb(bitb);																						// get the square of the 1st blocker
			bitb = bitb != 0 ? pattern.SW[sq] & ~pattern.SW[bit] & ~Types.BB_SQUARES[bit] : pattern.SW[sq];					// remove the squares behind the blocker and the blocker itself
			result |= bitb;
            
            bitb = state.blocker & pattern.SE[sq];																			// get the bits to the SE
			bit = BitOps.GetMsb(bitb);																						// get the square of the most WEST blocker
			bitb = bitb != 0 ? pattern.SE[sq] & ~pattern.SE[bit] & ~Types.BB_SQUARES[bit] : pattern.SE[sq];					// remove the squares behind the blocker and the blocker itself
			result |= bitb;
            
            return result;
        }

	}
}