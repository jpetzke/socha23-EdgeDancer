namespace Weights
{
    public enum EWeight
    {
        WID_FISH_MATERIAL,
        WID_MOBILITY,
        WID_AGILITY,
        WID_SCOREABILITY,
        WID_CAPTURED_FLOE_PCT,
        WID_TRAPPED_PENALTY,
        WID_STATIC_0_MOVE_MARGIN,
        WID_LMR_MINIMUM_MOVES,
        WID_RAZOR_MARGIN,
        WEIGHT_COUNT
    };

    public class TWeights
    {
        public const int MG = 0;
        public const int EG = 1;
        public const int NXW = -1;  // WEIGHT DOES NOT EXIST

        public static readonly int[] WEIGHT_BITS = { 8, 8, 8, 8, 7, 10, 8, 5, 8 };

        public static readonly string[] WEIGHT_NAMES = {
            "FISH_MATERIAL",
            "MOBILITY",
            "AGILITY",
            "SCOREABILITY",
            "CAPTURED_FLOE_PCT",
            "TRAPPED_PENALTY",
            "STATIC_0_MOVE_MARGIN",
            "LMR_MINIMUM_MOVES",
            "RAZOR_MARGIN"
        };

        public static readonly int[,] WEIGHT_DEFAULTS =
        {
            { 100, 119 },        // FISH_MATERIAL                       :  Value of collected fishes
            { 246, 111 },        // MOBILITY                            :  Mobility Factor    Score = Factor * (ln(sq_cnt+2) -2)
            { 128,   1 },        // AGILITY                             :  Number of directions    Score =  Factor * (ln(sq_cnt+1) -1)
            { 255, 194 },        // SCOREABILITY                        :  Number of fishes in move pattern    Score = Factor * (ln(sq_cnt+2) -2)
            { 10,   74 },        // CAPTURED_FLOE_PCT                   :  Used pct of fish value of a captured flow
            { 848, 279 },        // TRAPPED_PENALTY                     :  Penalty if a penguin is trapped
            { 118,  39 },        // STATIC_0_MOVE_MARGIN                :  0 move margin per remaining depth
            {   5,   7 },        // LMR_MINIMUM_MOVES                   :  Moves searched without reduction
            { 250, 164 }         // RAZOR_MARGIN                        :  Distance to alpha required to razor that node
        };

        public int[,] weights = new int[(int)EWeight.WEIGHT_COUNT, 2];

        public TWeights()
        {
            InitToDefault();
        }
        public void InitToDefault()
        {
            for (EWeight weight = (EWeight)0; weight < EWeight.WEIGHT_COUNT; weight++)
            {
                weights[(int)weight, MG] = WEIGHT_DEFAULTS[(int)weight, MG];
                weights[(int)weight, EG] = WEIGHT_DEFAULTS[(int)weight, EG];
            }
        }
        public EWeight GetWeightId(string name)
        {
            for (EWeight weight = (EWeight)0; weight < EWeight.WEIGHT_COUNT; weight++) if (WEIGHT_NAMES[(int)weight] == name) return weight;
            return EWeight.WEIGHT_COUNT; // No matching weight found
        }
        public void SetWeight(EWeight id, int mg, int eg) { weights[(int)id, MG] = mg; weights[(int)id, EG] = eg; }
        public int GetWeightMG(EWeight id) { return id < EWeight.WEIGHT_COUNT ? weights[(int)id, MG] : 0; }
        public int GetWeightEG(EWeight id) { return id < EWeight.WEIGHT_COUNT ? weights[(int)id, EG] : 0; }
    }
}
