using System.Diagnostics;
using System.Numerics;
using Weights;

namespace EgdeDancer
{
    public class TEvaluator
    {
        private static int EVAL_FEATURES = 6;       // Total number of Evaluation features listed below
        private static int EVAL_MATERIAL = 0;       // Score for fishes already collected
        private static int EVAL_MOBILITY = 1;       // Score for reachable squares
        private static int EVAL_AGILITY = 2;        // Score for open directions
        private static int EVAL_SCOREABILITY = 3;   // Score for fishes in move pattern
        private static int EVAL_FLOES = 4;          // Score fishes on a captured floe 
        private static int EVAL_TRAPPED = 5;        // Score for fishes in move pattern

        TValues values;
        TCache cache;

        // Score list both sides per feature and game phase
        private int[,,] scores = new int[(int)EColor.BOTH_SIDES, EVAL_FEATURES, 2];

        private TBoard board;
        private ulong[] attackedBy = new ulong[(int) EColor.BOTH_SIDES];    // save the attacked squares per side

        public TEvaluator(ref TBoard b, ref TWeights weights)
        {
            board = b;
            values = new TValues(weights);
            cache = new TCache();

            Init();
        }
        public void CalculateValuesFromWeights(TWeights weights)  // weights have changed, recalculate the values to be used in eval
        {
            values.CalculateValuesFromWeights(weights);
            cache.Reset();
        }
        private void Init()
        {
            for (int i = 0; i < EVAL_FEATURES; i++)
            {
                scores[(int)EColor.RED, i, TWeights.MG] = scores[(int)EColor.BLUE, i, TWeights.MG] = 0;
                scores[(int)EColor.RED, i, TWeights.EG] = scores[(int)EColor.BLUE, i, TWeights.EG] = 0;
            }

            attackedBy[(int)EColor.RED] = attackedBy[(int)EColor.BLUE] = 0;
        }
        public int GetCacheStatistics()
        {
            return (int) (cache.probeCount > 0 ? 100 * cache.hitCount / cache.probeCount : 0);
        }
        public int Evaluate()
        {
            int c_value = 0;
            if (cache.ProbeCache(board.state.hash, ref c_value)) return c_value;

            Init();

            for (EColor side = EColor.RED; side <= EColor.BLUE; side++)
            {
                EvaluateMaterial(side);
                EvaluatePositionalScores(side);
                EvaluateFloes(side);
                EvaluateTrapped(side);
            }
            
            // calulate the overall score based on the sub scores of red and blue
            int red_score_mg = 0, blue_score_mg = 0;
            int red_score_eg = 0, blue_score_eg = 0;

            for (int i = 0; i < EVAL_FEATURES; i++)
            {
                red_score_mg += scores[(int)EColor.RED, i, TWeights.MG]; blue_score_mg += scores[(int)EColor.BLUE, i, TWeights.MG];
                red_score_eg += scores[(int)EColor.RED, i, TWeights.EG]; blue_score_eg += scores[(int)EColor.BLUE, i, TWeights.EG];
            }

            int mg = blue_score_mg - red_score_mg;
            int eg = blue_score_eg - red_score_eg;

            // Phase Model for building final score from mg and eg values:  score = (mg * phase + eg * (maxphase - phase)) / maxphase
            int phase = board.GetGamePhaseGradient();   
            int score = (mg * phase + eg * (TBoard.GAME_PHASE_GRADIENT_MAX - phase)) / TBoard.GAME_PHASE_GRADIENT_MAX; 

            // negate the value for negamax
            if (board.state.sideToMove == EColor.RED) score = -score;

            // store value in eval cache
            cache.Store2Cache(board.state.hash, score);

            return score;
        }
        public bool Print()
        {
            int rc = Evaluate();

            int _red = (int)EColor.RED;
            int _blue = (int)EColor.BLUE;

            int red_score_mg = 0, blue_score_mg = 0;
            int red_score_eg = 0, blue_score_eg = 0;

            for (int i = 0; i < EVAL_FEATURES; i++)
            {
                red_score_mg += scores[(int)EColor.RED, i, TWeights.MG]; blue_score_mg += scores[(int)EColor.BLUE, i, TWeights.MG];
                red_score_eg += scores[(int)EColor.RED, i, TWeights.EG]; blue_score_eg += scores[(int)EColor.BLUE, i, TWeights.EG];
            }

            int mg = blue_score_mg - red_score_mg;
            int eg = blue_score_eg - red_score_eg;

            Console.WriteLine(String.Format("                     |      Red      |       Blue    |      Total"));
            Console.WriteLine(String.Format("--> Red minimizes    |    MG     EG  |     MG    EG  |    MG    EG"));
            Console.WriteLine(String.Format("---------------------+---------------+---------------+---------------"));
            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Fishes               | {0,6:N2} {1,6:N2} | {2,6:N2} {3,6:N2} | {4,6:N2} {5,6:N2}", 
                    (float)scores[_red, EVAL_MATERIAL, TWeights.MG] / 100, (float)scores[_red, EVAL_MATERIAL, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_MATERIAL, TWeights.MG] / 100, (float)scores[_blue, EVAL_MATERIAL, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_MATERIAL, TWeights.MG] / 100 - (float)scores[_red, EVAL_MATERIAL, TWeights.MG] / 100,
                    (float)scores[_blue, EVAL_MATERIAL, TWeights.EG] / 100 - (float)scores[_red, EVAL_MATERIAL, TWeights.EG] / 100));
            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Mobility             | {0,6:N2} {1,6:N2} | {2,6:N2} {3,6:N2} | {4,6:N2} {5,6:N2}",
                    (float)scores[_red, EVAL_MOBILITY, TWeights.MG] / 100, (float)scores[_red, EVAL_MOBILITY, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_MOBILITY, TWeights.MG] / 100, (float)scores[_blue, EVAL_MOBILITY, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_MOBILITY, TWeights.MG] / 100 - (float)scores[_red, EVAL_MOBILITY, TWeights.MG] / 100,
                    (float)scores[_blue, EVAL_MOBILITY, TWeights.EG] / 100 - (float)scores[_red, EVAL_MOBILITY, TWeights.EG] / 100));
            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Agility              | {0,6:N2} {1,6:N2} | {2,6:N2} {3,6:N2} | {4,6:N2} {5,6:N2}",
                    (float)scores[_red, EVAL_AGILITY, TWeights.MG] / 100, (float)scores[_red, EVAL_AGILITY, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_AGILITY, TWeights.MG] / 100, (float)scores[_blue, EVAL_AGILITY, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_AGILITY, TWeights.MG] / 100 - (float)scores[_red, EVAL_AGILITY, TWeights.MG] / 100,
                    (float)scores[_blue, EVAL_AGILITY, TWeights.EG] / 100 - (float)scores[_red, EVAL_AGILITY, TWeights.EG] / 100));
            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Scoreability         | {0,6:N2} {1,6:N2} | {2,6:N2} {3,6:N2} | {4,6:N2} {5,6:N2}",
                    (float)scores[_red, EVAL_SCOREABILITY, TWeights.MG] / 100, (float)scores[_red, EVAL_SCOREABILITY, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_SCOREABILITY, TWeights.MG] / 100, (float)scores[_blue, EVAL_SCOREABILITY, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_SCOREABILITY, TWeights.MG] / 100 - (float)scores[_red, EVAL_SCOREABILITY, TWeights.MG] / 100,
                    (float)scores[_blue, EVAL_SCOREABILITY, TWeights.EG] / 100 - (float)scores[_red, EVAL_SCOREABILITY, TWeights.EG] / 100));
            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Captured Floes       | {0,6:N2} {1,6:N2} | {2,6:N2} {3,6:N2} | {4,6:N2} {5,6:N2}",
                    (float)scores[_red, EVAL_FLOES, TWeights.MG] / 100, (float)scores[_red, EVAL_FLOES, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_FLOES, TWeights.MG] / 100, (float)scores[_blue, EVAL_FLOES, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_FLOES, TWeights.MG] / 100 - (float)scores[_red, EVAL_FLOES, TWeights.MG] / 100,
                    (float)scores[_blue, EVAL_FLOES, TWeights.EG] / 100 - (float)scores[_red, EVAL_FLOES, TWeights.EG] / 100));
            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Trapped Penguins     | {0,6:N2} {1,6:N2} | {2,6:N2} {3,6:N2} | {4,6:N2} {5,6:N2}",
                    (float)scores[_red, EVAL_TRAPPED, TWeights.MG] / 100, (float)scores[_red, EVAL_TRAPPED, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_TRAPPED, TWeights.MG] / 100, (float)scores[_blue, EVAL_TRAPPED, TWeights.EG] / 100,
                    (float)scores[_blue, EVAL_TRAPPED, TWeights.MG] / 100 - (float)scores[_red, EVAL_TRAPPED, TWeights.MG] / 100,
                    (float)scores[_blue, EVAL_TRAPPED, TWeights.EG] / 100 - (float)scores[_red, EVAL_TRAPPED, TWeights.EG] / 100));
            Console.WriteLine(String.Format("---------------------+---------------+---------------+---------------"));
            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Totals               | {0,6:N2} {1,6:N2} | {2,6:N2} {3,6:N2} | {4,6:N2} {5,6:N2}",
                    (float)red_score_mg / 100, (float)red_score_eg / 100,
                    (float)blue_score_mg / 100, (float)blue_score_eg / 100,
                    (float)blue_score_mg / 100 - (float)red_score_mg / 100,
                    (float)blue_score_eg / 100 - (float)red_score_eg / 100));

            Console.WriteLine("");

            int phase = board.GetGamePhaseGradient();
            int score = (mg * phase + eg * (TBoard.GAME_PHASE_GRADIENT_MAX - phase)) / TBoard.GAME_PHASE_GRADIENT_MAX;

            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Scaling Score between {0}% MG [{1,4:N2}] and {2}% EG [{3,4:N2}] = {4,4:N2}", 
                100 * phase/ TBoard.GAME_PHASE_GRADIENT_MAX, (float) mg / 100 * phase / TBoard.GAME_PHASE_GRADIENT_MAX,
                100 * (TBoard.GAME_PHASE_GRADIENT_MAX-phase) / TBoard.GAME_PHASE_GRADIENT_MAX, (float) eg / 100 * (TBoard.GAME_PHASE_GRADIENT_MAX-phase) / TBoard.GAME_PHASE_GRADIENT_MAX,
                (float) score/100));

            if (board.state.sideToMove == EColor.RED) score = -score;
            Console.WriteLine(String.Format(System.Globalization.CultureInfo.GetCultureInfo("en-US"), "Negamax Adjustment for side to move ({0}): {1,0:N2}", Types.COLOR_NAMES[(int)board.state.sideToMove], (float)score / 100));

            return true;
        }
        private void EvaluateMaterial(EColor side)
        {
            int _side = (int)side;
            scores[_side, EVAL_MATERIAL, TWeights.MG] += board.state.fishesCollected[_side] * values.MULTI_FISHES.mg;
            scores[_side, EVAL_MATERIAL, TWeights.EG] += board.state.fishesCollected[_side] * values.MULTI_FISHES.eg;
        }
        private void EvaluatePositionalScores(EColor side)
        {
            int _side = (int)side;

            ulong penguins = board.state.penguinOfColor[_side] & ~board.state.trapped;
            while (penguins != 0)
            {
                ESquare sq = (ESquare)BitOps.GetAndClearLsb(ref penguins);

                ulong attacks = board.GetPenguinAttacks(sq);
                attackedBy[_side] |= attacks;

                int mobility = BitOps.PopCount(attacks);
                scores[_side, EVAL_MOBILITY, TWeights.MG] += values.MOBILITY[mobility].mg;
                scores[_side, EVAL_MOBILITY, TWeights.EG] += values.MOBILITY[mobility].eg;

                int agility = BitOps.PopCount(attacks & board.pattern.NEXT[(int) sq]);
                scores[_side, EVAL_AGILITY, TWeights.MG] += values.AGILITY[agility].mg;
                scores[_side, EVAL_AGILITY, TWeights.EG] += values.AGILITY[agility].eg;

                int scoreability = 0;
                for (int fishCnt = 1; fishCnt < 5; fishCnt++) scoreability += fishCnt * BitOps.PopCount(attacks & board.state.fishesOfCount[fishCnt]);
                scoreability = Math.Min(values.SCOREABILITY.Length -1, scoreability);  // cap the score value for very large numbers 
                scores[_side, EVAL_SCOREABILITY, TWeights.MG] += values.SCOREABILITY[scoreability].mg;
                scores[_side, EVAL_SCOREABILITY, TWeights.EG] += values.SCOREABILITY[scoreability].eg;

            }
         }
        private void EvaluateTrapped(EColor side)
        {
            int _side = (int)side;
            ulong trapped = board.state.trapped & board.state.penguinOfColor[_side];
            if (trapped == 0) return;

            scores[_side, EVAL_TRAPPED, TWeights.MG] += BitOps.PopCount(trapped) * values.TRAPPED_PENALTY.mg;
            scores[_side, EVAL_TRAPPED, TWeights.EG] += BitOps.PopCount(trapped) * values.TRAPPED_PENALTY.eg;
        }
        private void EvaluateFloes(EColor side)
        {
            if (board.IsSetPhase()) return;

            int _side = (int) side;
            int _enemy = side == EColor.RED ? (int) EColor.BLUE : (int) EColor.RED;

            ulong penguins = board.state.penguinOfColor[_side] & ~board.state.trapped;
            ulong ignore = 0;
            while (penguins != 0)
            {
                ESquare sq = (ESquare)BitOps.GetAndClearLsb(ref penguins);

                ulong attacks = board.GetPenguinAttacks(sq);
                if ((attacks & attackedBy[_enemy]) == 0)        // penguin can only control a floe if he does not cross any enemy move pattern
                {
                    ulong floe = board.GetFloe(sq) & ~ignore;
                    ignore |= floe;                             // save the squares with already counted fishes
                    if (floe != 0)
                    {
                        int fishValue = 0;
                        for (int fishCnt = 1; fishCnt < 5; fishCnt++) fishValue += fishCnt * BitOps.PopCount(floe & board.state.fishesOfCount[fishCnt]);

                        scores[_side, EVAL_FLOES, TWeights.MG] += fishValue * values.CAPTURED_FLOE_PCT.mg;
                        scores[_side, EVAL_FLOES, TWeights.EG] += fishValue * values.CAPTURED_FLOE_PCT.eg;
                    }
                }
            }
        }


    }

}